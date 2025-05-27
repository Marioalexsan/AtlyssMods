using UnityEngine;

namespace Marioalexsan.ModAudio;

internal static class AudioEngine
{
    private struct SourceState
    {
        // Previous state
        public AudioClip? Clip;
        public float Volume;
        public float Pitch;
        public bool Loop;

        // (supposedly) Current state
        public AudioClip? AppliedClip;
        public float AppliedVolume;
        public float AppliedPitch;
        public bool AppliedLoop;

        // Flags
        public bool DisableRouting;
        public bool IsOneShotSource;
        public bool IsOverlay;
        public bool IsCustomEvent;
        public bool OneShotStopsIfSourceStops;

        // Temporary Flags
        public bool JustRouted;
        public bool JustUsedDefaultClip;
        public bool WasStoppedOrDisabled;

        // Misc
        public AudioSource? OneShotOrigin;
    }

    private static readonly System.Random RNG = new();
    private static readonly System.Diagnostics.Stopwatch Watch = new();

    private static readonly Dictionary<AudioSource?, SourceState> TrackedSources = [];
    private static readonly HashSet<AudioSource?> TrackedPlayOnAwakeSources = [];

    public static List<AudioPack> AudioPacks { get; } = [];
    public static IEnumerable<AudioPack> EnabledPacks => AudioPacks.Where(x => x.Enabled);

    private static AudioClip EmptyClip
    {
        get
        {
            // Setting this too low might cause it to fail for playOnAwake sources
            // This is due to the detection method in Update(), which relies on scanning audio sources every frame
            // This is why we need to use a minimum size (a few game frames at least).

            const int EmptyClipSizeInSamples = 16384; // 0.37 seconds
            return _emptyClip ??= AudioClipLoader.GenerateEmptyClip("___nothing___", EmptyClipSizeInSamples);
        }
    }
    private static AudioClip? _emptyClip;

    public static void PlayCustomEvent(AudioClip clip, AudioSource target)
    {
        var source = CreateOneShotFromSource(target);

        source.volume = 1f;
        source.pitch = 1f;
        source.panStereo = 0f;
        source.clip = clip;

        TrackedSources[source] = TrackedSources[source] with { IsCustomEvent = true };

        source.Play();
    }

    private static bool IsValidTarget(AudioSource source, AudioPackConfig.Route route)
    {
        var trackedData = TrackedSources[source];

        var originalClipName = trackedData.Clip?.name;
        bool matchesOriginalClip = false;

        if (originalClipName != null)
        {
            for (int i = 0; i < route.OriginalClips.Count; i++)
            {
                if (route.OriginalClips[i] == originalClipName)
                {
                    matchesOriginalClip = true;
                    break;
                }
            }
        }

        if (!matchesOriginalClip)
            return false;

        if (route.FilterBySources.Count > 0)
        {
            var sourceName = source.name;
            var matchesSource = false;

            for (int i = 0; i < route.FilterBySources.Count; i++)
            {
                if (route.FilterBySources[i] == sourceName)
                {
                    matchesSource = true;
                    break;
                }
            }

            if (!matchesSource)
                return false;
        }

        if (route.FilterByObject.Count > 0)
        {
            var transform = source.transform;

            var matchesObject = false;

            while (transform != null)
            {
                var gameObjectName = transform.gameObject.name;

                for (int i = 0; i < route.FilterByObject.Count; i++)
                {
                    if (route.FilterByObject[i] == gameObjectName)
                    {
                        matchesObject = true;
                        break;
                    }
                }

                transform = transform.parent;
            }

            if (!matchesObject)
                return false;
        }

        if (route.FilterByMapName.Count > 0)
        {
            var matchesMap = false;
            var currentMap = Player._mainPlayer?.Network_playerMapInstance?._mapName;

            for (int i = 0; i < route.FilterByMapName.Count; i++)
            {
                if (route.FilterByMapName[i] == "___nomap___" && currentMap == null)
                {
                    matchesMap = true;
                    break;
                }
                else if (route.FilterByMapName[i] == currentMap)
                {
                    matchesMap = true;
                    break;
                }
            }

            if (!matchesMap)
                return false;
        }

        return true;
    }

    public static void HardReload() => Reload(hardReload: true);

    public static void SoftReload() => Reload(hardReload: false);

    private static void Reload(bool hardReload)
    {
        Watch.Restart();

        try
        {
            Logging.LogInfo("Reloading engine...");

            // I like cleaning audio sources
            CleanupSources();

            if (hardReload)
            {
                // Get rid of one-shots forcefully

                OptimizedMethods.CachedForeach(
                    TrackedSources,
                    static (in KeyValuePair<AudioSource?, SourceState> source) =>
                    {
                        if (source.Value.IsOneShotSource)
                        {
                            TrackedSources.Remove(source.Key);
                            UnityEngine.Object.Destroy(source.Key);
                        }
                    }
                );
            }

            // Restore previous state
            Dictionary<AudioSource, bool> wasPlayingPreviously = [];

            foreach (var audio in UnityEngine.Object.FindObjectsOfType<AudioSource>(true))
            {
                wasPlayingPreviously[audio] = audio.isPlaying;

                if (wasPlayingPreviously[audio])
                {
                    audio.Stop();
                }
            }

            // Restore original state
            foreach (var source in TrackedSources)
            {
                if (source.Key != null)
                {
                    source.Key.clip = source.Value.Clip;
                    source.Key.volume = source.Value.Volume;
                    source.Key.pitch = source.Value.Pitch;
                    source.Key.loop = source.Value.Loop;
                }
            }

            TrackedSources.Clear();

            if (hardReload)
            {
                Logging.LogInfo("Reloading audio packs...");

                // Clean up handles from streams
                foreach (var pack in AudioPacks)
                {
                    foreach (var handle in pack.OpenStreams)
                        handle.Dispose();
                }

                AudioPacks.Clear();
                AudioPacks.AddRange(AudioPackLoader.LoadAudioPacks());
                ModAudio.Plugin.InitializePackConfiguration(); // TODO I wish ModAudio plugin ref wouldn't be here
            }

            Logging.LogInfo("Preloading audio data...");
            foreach (var pack in AudioPacks)
            {
                if (pack.Enabled && pack.PendingClipsToLoad.Count > 0)
                {
                    // If a pack is enabled, we should preload all of the in-memory clips
                    // Opening a ton of streams at the start is not great though, so those remain on-demand

                    var clipsToPreload = pack.PendingClipsToLoad.Keys.ToArray();

                    foreach (var clip in clipsToPreload)
                    {
                        _ = pack.TryGetReadyClip(clip, out _);
                    }

                    Logging.LogInfo($"{pack.Config.Id} - {clipsToPreload.Length} clips preloaded.");
                }
            }
            Logging.LogInfo("Audio data preloaded.");

            // Restart audio
            foreach (var audio in UnityEngine.Object.FindObjectsOfType<AudioSource>(true))
            {
                if (wasPlayingPreviously[audio])
                    audio.Play();
            }

            Logging.LogInfo("Done with reload!");
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(Reload)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
        }

        Watch.Stop();

        Logging.LogInfo($"Reload took {Watch.ElapsedMilliseconds} milliseconds.");
    }

    public static void Update()
    {
        try
        {
            foreach (var audio in UnityEngine.Object.FindObjectsOfType<AudioSource>(true))
            {
                if (audio.playOnAwake)
                {
                    // This is to detect playOnAwake audio sources that have been played
                    // directly by the engine and not via the script API

                    if (!TrackedPlayOnAwakeSources.Contains(audio) && audio.isActiveAndEnabled && audio.isPlaying)
                    {
                        AudioPlayed(audio);
                    }
                    else if (TrackedPlayOnAwakeSources.Contains(audio) && !audio.isActiveAndEnabled && !audio.isPlaying)
                    {
                        AudioStopped(audio, false);
                    }
                }
            }

            CleanupSources();
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(Update)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
        }
    }

    private static void CleanupSources()
    {
        // Cleanup dead play on awake sounds
        OptimizedMethods.CachedForeach(
            TrackedPlayOnAwakeSources,
            static (in AudioSource? source) =>
            {
                if (source == null)
                {
                    TrackedPlayOnAwakeSources.Remove(source);
                }
            }
        );

        // Cleanup stale stuff
        OptimizedMethods.CachedForeach(
            TrackedSources,
            static (in KeyValuePair<AudioSource?, SourceState> source) =>
            {
                if (source.Key == null)
                {
                    TrackedSources.Remove(source.Key);
                }
                else if (source.Value.IsOneShotSource && !source.Key.isPlaying)
                {
                    TrackedSources.Remove(source.Key);
                    AudioStopped(source.Key, false);
                    UnityEngine.Object.Destroy(source.Key);
                }
            }
        );
    }

    private static void TrackSource(AudioSource source)
    {
        if (!TrackedSources.ContainsKey(source))
        {
            TrackedSources.Add(source, new()
            {
                AppliedClip = source.clip,
                Clip = source.clip,
                Pitch = source.pitch,
                Loop = source.loop,
                Volume = source.volume,
                AppliedPitch = source.pitch,
                AppliedVolume = source.volume,
                AppliedLoop = source.loop
            });
        }
    }

    private static AudioSource CreateOneShotFromSource(AudioSource source)
    {
        GameObject targetObject = source.gameObject;

        // Note: some sound effects are played on particle systems that disable themselves after they're played
        // We need to check if that is the case, and move the target object somewhere higher in the hierarchy
        // Unfortunately there's no API to check if the particle system actually has stop behaviour set to disable

        int parentsToGoThrough = 3;

        do
        {
            var particleSystem = targetObject.GetComponent<ParticleSystem>();

            if (particleSystem == null)
                break;

            if (targetObject.transform.parent == null)
                break;

            targetObject = targetObject.transform.parent.gameObject;
        }
        while (parentsToGoThrough-- > 0);

        var oneShotSource = targetObject.AddComponent<AudioSource>();

        oneShotSource.volume = source.volume;
        oneShotSource.pitch = source.pitch;
        oneShotSource.clip = source.clip;
        oneShotSource.outputAudioMixerGroup = source.outputAudioMixerGroup;
        oneShotSource.loop = false; // Otherwise this won't play one-shot
        oneShotSource.ignoreListenerVolume = source.ignoreListenerVolume;
        oneShotSource.ignoreListenerPause = source.ignoreListenerPause;
        oneShotSource.velocityUpdateMode = source.velocityUpdateMode;
        oneShotSource.panStereo = source.panStereo;
        oneShotSource.spatialBlend = source.spatialBlend;
        oneShotSource.spatialize = source.spatialize;
        oneShotSource.spatializePostEffects = source.spatializePostEffects;
        oneShotSource.reverbZoneMix = source.reverbZoneMix;
        oneShotSource.bypassEffects = source.bypassEffects;
        oneShotSource.bypassListenerEffects = source.bypassListenerEffects;
        oneShotSource.bypassReverbZones = source.bypassReverbZones;
        oneShotSource.dopplerLevel = source.dopplerLevel;
        oneShotSource.spread = source.spread;
        oneShotSource.priority = source.priority;
        oneShotSource.mute = source.mute;
        oneShotSource.minDistance = source.minDistance;
        oneShotSource.maxDistance = source.maxDistance;
        oneShotSource.rolloffMode = source.rolloffMode;

        oneShotSource.playOnAwake = false; // This should be false for one shot sources, but whatever

        oneShotSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, source.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
        oneShotSource.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, source.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
        oneShotSource.SetCustomCurve(AudioSourceCurveType.SpatialBlend, source.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
        oneShotSource.SetCustomCurve(AudioSourceCurveType.Spread, source.GetCustomCurve(AudioSourceCurveType.Spread));

        TrackSource(oneShotSource);
        TrackedSources[oneShotSource] = TrackedSources[oneShotSource] with { IsOneShotSource = true, OneShotOrigin = source };

        return oneShotSource;
    }

    public static bool OneShotClipPlayed(AudioClip clip, AudioSource source, float volumeScale)
    {
        try
        {
            // Move to a dedicated audio source for better control. Note: This is likely overkill and might mess with other mods?

            var oneShotSource = CreateOneShotFromSource(source);
            oneShotSource.volume *= volumeScale;
            oneShotSource.clip = clip;

            TrackedSources[oneShotSource] = TrackedSources[oneShotSource] with
            {
                Clip = clip,
                AppliedClip = oneShotSource.clip,
                Volume = oneShotSource.volume,
                AppliedVolume = oneShotSource.volume,
            };

            oneShotSource.Play();

            return false;
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(OneShotClipPlayed)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
            Logging.LogError($"AudioSource that caused the crash:");
            Logging.LogError($"  name = {source?.name ?? "(null)"}");
            Logging.LogError($"  clip = {source?.clip?.name ?? "(null)"}");
            Logging.LogError($"AudioClip that caused the crash:");
            Logging.LogError($"  name = {clip?.name ?? "(null)"}");
            Logging.LogError($"Parameter {nameof(volumeScale)} was: {volumeScale}");
            return true;
        }
    }

    private static void LogAudio(AudioSource source)
    {
        float distance = float.MinValue;

        if (ModAudio.Plugin.UseMaxDistanceForLogging.Value && (bool)Player._mainPlayer)
        {
            distance = Vector3.Distance(Player._mainPlayer.transform.position, source.transform.position);

            if (distance > ModAudio.Plugin.MaxDistanceForLogging.Value)
                return;
        }

        var groupName = source.outputAudioMixerGroup?.name?.ToLower() ?? "(null)"; // This can be null, apparently...

        if (!ModAudio.Plugin.LogAmbience.Value && groupName == "ambience")
            return;

        if (!ModAudio.Plugin.LogGame.Value && groupName == "game")
            return;

        if (!ModAudio.Plugin.LogGUI.Value && groupName == "gui")
            return;

        if (!ModAudio.Plugin.LogMusic.Value && groupName == "music")
            return;

        if (!ModAudio.Plugin.LogVoice.Value && groupName == "voice")
            return;

        var originalClipName = TrackedSources[source].Clip?.name ?? "(null)";
        var currentClipName = source.clip?.name ?? "(null)";
        var clipChanged = TrackedSources[source].Clip != source.clip;

        if (TrackedSources[source].IsCustomEvent && !clipChanged && !ModAudio.Plugin.AlwaysLogCustomEventsPlayed.Value)
            return; // Skip logging custom events that do nothing (including playing the "default" sound, which is empty)

        if (TrackedSources[source].JustUsedDefaultClip)
        {
            // Needs a special case for display purposes, since the clip name is the same
            clipChanged = true;
            currentClipName = "___default___";
        }

        var originalVolume = TrackedSources[source].Volume;
        var currentVolume = TrackedSources[source].AppliedVolume;
        var volumeChanged = originalVolume != currentVolume;

        var originalPitch = TrackedSources[source].Pitch;
        var currentPitch = TrackedSources[source].AppliedPitch;
        var pitchChanged = originalPitch != currentPitch;

        var clipDisplay = clipChanged ? $"{originalClipName} > {currentClipName}" : originalClipName;
        var volumeDisplay = volumeChanged ? $"{originalVolume:F2} > {currentVolume:F2}" : $"{originalVolume:F2}";
        var pitchDisplay = pitchChanged ? $"{originalPitch:F2} > {currentPitch:F2}" : $"{originalPitch:F2}";

        var messageDisplay = $"Clip {clipDisplay} Src {source.name} Vol {volumeDisplay} Pit {pitchDisplay} Grp {groupName}";

        if (distance != float.MinValue)
            messageDisplay += $" Dst {distance:F2}";

        if (TrackedSources[source].IsOverlay)
            messageDisplay += " overlay";

        if (TrackedSources[source].IsOneShotSource)
            messageDisplay += " oneshot";

        if (TrackedSources[source].IsCustomEvent)
            messageDisplay += " event";

        if (TrackedSources[source].AppliedLoop)
        {
            if (TrackedSources[source].AppliedLoop != TrackedSources[source].Loop)
            {
                messageDisplay += " loop(forced)";
            }
            else
            {
                messageDisplay += " loop";
            }
        }

        Logging.LogInfo(messageDisplay, ModAudio.Plugin.LogAudioPlayed);
    }

    public static bool AudioPlayed(AudioSource source)
    {
        try
        {
            TrackSource(source);

            if (source.playOnAwake)
            {
                TrackedPlayOnAwakeSources.Add(source);
            }

            var wasPlaying = source.isPlaying;

            if (!Route(source))
            {
                LogAudio(source);
                return true;
            }

            TrackedSources[source] = TrackedSources[source] with
            {
                JustRouted = false,
                WasStoppedOrDisabled = false
            };

            bool requiresRestart = wasPlaying && !source.isPlaying;

            if (requiresRestart)
            {
                source.Play();
            }
            else
            {
                LogAudio(source);
            }

            // If a restart was required, then we already played the sound manually again, so let's skip the original
            return !requiresRestart;
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(AudioPlayed)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
            Logging.LogError($"AudioSource that caused the crash:");
            Logging.LogError($"  name = {source?.name ?? "(null)"}");
            Logging.LogError($"  clip = {source?.clip?.name ?? "(null)"}");
            return true;
        }
    }

    public static bool AudioStopped(AudioSource source, bool stopOneShots)
    {
        try
        {
            TrackSource(source);

            if (source.playOnAwake)
            {
                TrackedPlayOnAwakeSources.Remove(source);
            }

            if (stopOneShots)
            {
                OptimizedMethods.CachedForeach(
                    TrackedSources,
                    source,
                    static (in KeyValuePair<AudioSource?, SourceState> trackedSource, in AudioSource stoppedSource) =>
                    {
                        if (trackedSource.Value.IsOneShotSource && trackedSource.Value.OneShotStopsIfSourceStops && trackedSource.Value.OneShotOrigin == stoppedSource && trackedSource.Key != null && trackedSource.Key.isPlaying)
                        {
                            trackedSource.Key.Stop();
                        }
                    }
                );
            }

            TrackedSources[source] = TrackedSources[source] with { WasStoppedOrDisabled = true };

            return true;
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(AudioStopped)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
            Logging.LogError($"AudioSource that caused the crash:");
            Logging.LogError($"  name = {source?.name ?? "(null)"}");
            Logging.LogError($"  clip = {source?.clip?.name ?? "(null)"}");
            Logging.LogError($"Parameter {nameof(stopOneShots)} was: {stopOneShots}");
            return true;
        }
    }

    private static bool Route(AudioSource source)
    {
        var trackedData = TrackedSources[source];

        // Check for any changes in tracked sources' clips
        // If so, restore last volume / pitch and track new clip before routing

        if (source.clip != trackedData.AppliedClip)
        {
            TrackedSources[source] = TrackedSources[source] with
            {
                Clip = source.clip,
                AppliedClip = source.clip
            };

            if (Math.Abs(source.volume - trackedData.AppliedVolume) >= 0.005)
            {
                // Volume must have been changed externally, set it as new original volume
                TrackedSources[source] = TrackedSources[source] with
                {
                    Volume = source.volume,
                    AppliedVolume = source.volume
                };
            }
            else
            {
                // Restore original volume
                source.volume = trackedData.Volume;
            }

            if (Math.Abs(source.pitch - trackedData.AppliedPitch) >= 0.005)
            {
                // Pitch must have been changed externally, set it as new original pitch
                TrackedSources[source] = TrackedSources[source] with
                {
                    Pitch = source.pitch,
                    AppliedPitch = source.pitch
                };
            }
            else
            {
                // Restore original volume
                source.pitch = trackedData.Pitch;
            }

            if (source.loop != trackedData.AppliedLoop)
            {
                // Loop must have been changed externally, set it as new original loop
                TrackedSources[source] = TrackedSources[source] with
                {
                    Loop = source.loop,
                    AppliedLoop = source.loop
                };
            }
            else
            {
                // Restore original loop
                source.loop = trackedData.Loop;
            }
        }

        if (trackedData.JustRouted || trackedData.DisableRouting)
            return false;

        TrackedSources[source] = TrackedSources[source] with { JustRouted = true, JustUsedDefaultClip = false };

        // Get a replacement from routes

        List<(AudioPack, AudioPackConfig.Route)> replacements = [];

        foreach (var pack in EnabledPacks)
        {
            foreach (var route in pack.Config.Routes)
            {
                if (IsValidTarget(source, route))
                    replacements.Add((pack, route));
            }
        }

        (AudioPack Pack, AudioPackConfig.Route Route)? replacementRoute = null;

        if (replacements.Count > 0)
        {
            replacementRoute = SelectRandomWeighted(replacements);

            // Apply overall effects

            if (replacementRoute.Value.Route.RelativeReplacementEffects)
            {
                source.volume = trackedData.Volume * replacementRoute.Value.Route.Volume;
                source.pitch = trackedData.Pitch * replacementRoute.Value.Route.Pitch;
            }
            else
            {
                source.volume = replacementRoute.Value.Route.Volume;
                source.pitch = replacementRoute.Value.Route.Pitch;
            }

            if (replacementRoute.Value.Route.ForceLoop)
                source.loop = true;

            TrackedSources[source] = TrackedSources[source] with
            {
                AppliedPitch = source.pitch,
                AppliedVolume = source.volume,
                AppliedLoop = source.loop
            };

            // Apply replacement if needed

            if (replacementRoute.Value.Route.ReplacementClips.Count > 0)
            {
                var randomSelection = SelectRandomWeighted(replacementRoute.Value.Route.ReplacementClips);

                AudioClip? destinationClip;

                if (randomSelection.Name == "___default___")
                {
                    destinationClip = TrackedSources[source].Clip;
                    TrackedSources[source] = TrackedSources[source] with { JustUsedDefaultClip = true };
                }
                else if (randomSelection.Name == "___nothing___")
                {
                    destinationClip = EmptyClip;
                }
                else
                {
                    replacementRoute.Value.Pack.TryGetReadyClip(randomSelection.Name, out destinationClip);
                }

                if (destinationClip != null)
                {
                    source.volume *= randomSelection.Volume;
                    source.pitch *= randomSelection.Pitch;

                    TrackedSources[source] = TrackedSources[source] with
                    {
                        AppliedClip = destinationClip,
                        JustRouted = true,
                        AppliedPitch = source.pitch,
                        AppliedVolume = source.volume,
                        AppliedLoop = source.loop
                    };

                    if (source.clip != destinationClip && source.isPlaying)
                    {
                        source.Stop();
                    }

                    source.clip = destinationClip;
                }
                else
                {
                    Logging.LogWarning(Texts.AudioClipNotFound(randomSelection.Name));
                }
            }
        }

        List<(AudioPack Pack, AudioPackConfig.Route Route)> overlays = [];

        foreach (var pack in EnabledPacks)
        {
            foreach (var route in pack.Config.Routes)
            {
                if (route.OverlaysIgnoreRestarts && !(TrackedSources[source].WasStoppedOrDisabled || !source.isPlaying))
                    continue;

                if (route.OverlayClips.Count > 0 && IsValidTarget(source, route) && (!route.LinkOverlayAndReplacement || replacementRoute?.Route == route))
                    overlays.Add((pack, route));
            }
        }

        // Note: Overlays should not be able to trigger other overlays
        // Otherwise you can easily create infinite loops
        if (overlays.Count > 0 && !TrackedSources[source].IsOverlay)
        {
            foreach (var (Pack, Route) in overlays)
            {
                var randomSelection = SelectRandomWeighted(Route.OverlayClips);

                if (randomSelection.Name == "___nothing___")
                    continue;

                if (Pack.TryGetReadyClip(randomSelection.Name, out var selectedClip))
                {
                    var oneShotSource = CreateOneShotFromSource(source);
                    oneShotSource.clip = selectedClip;

                    if (Route.RelativeOverlayEffects)
                    {
                        oneShotSource.volume = trackedData.Volume * randomSelection.Volume;
                        oneShotSource.pitch = trackedData.Pitch * randomSelection.Pitch;
                    }
                    else
                    {
                        oneShotSource.volume = randomSelection.Volume;
                        oneShotSource.pitch = randomSelection.Pitch;
                    }

                    if (Route.ForceLoop)
                        oneShotSource.loop = true;

                    TrackedSources[oneShotSource] = TrackedSources[oneShotSource] with
                    {
                        Pitch = oneShotSource.pitch,
                        Volume = oneShotSource.volume,
                        AppliedPitch = oneShotSource.pitch,
                        AppliedVolume = oneShotSource.volume,
                        AppliedLoop = oneShotSource.loop,
                        Clip = oneShotSource.clip,
                        AppliedClip = oneShotSource.clip,
                        IsOverlay = true,
                        DisableRouting = true,
                        OneShotStopsIfSourceStops = Route.OverlayStopsIfSourceStops
                    };

                    oneShotSource.Play();
                }
                else
                {
                    Logging.LogWarning(Texts.AudioClipNotFound(randomSelection.Name));
                }
            }
        }

        return true;
    }

    private static (AudioPack Pack, AudioPackConfig.Route Route) SelectRandomWeighted(List<(AudioPack Pack, AudioPackConfig.Route Route)> routes)
    {
        var totalWeight = 0.0;

        for (int i = 0; i < routes.Count; i++)
            totalWeight += routes[i].Route.ReplacementWeight;

        var selectedIndex = -1;

        var randomValue = RNG.NextDouble() * totalWeight;

        do
        {
            selectedIndex++;
            randomValue -= routes[selectedIndex].Route.ReplacementWeight;
        }
        while (randomValue >= 0.0);

        return routes[selectedIndex];
    }

    private static AudioPackConfig.Route.ClipSelection SelectRandomWeighted(List<AudioPackConfig.Route.ClipSelection> selections)
    {
        var totalWeight = 0.0;

        for (int i = 0; i < selections.Count; i++)
            totalWeight += selections[i].Weight;

        var selectedIndex = -1;

        var randomValue = RNG.NextDouble() * totalWeight;

        do
        {
            selectedIndex++;
            randomValue -= selections[selectedIndex].Weight;
        }
        while (randomValue >= 0.0);

        return selections[selectedIndex];
    }
}
