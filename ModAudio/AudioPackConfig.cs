using Newtonsoft.Json;
using Newtonsoft.Json.Schema;

namespace Marioalexsan.ModAudio;

public class AudioPackConfig
{
    /// <summary>
    /// An unique identifier for your audio pack.
    /// It would be a good idea to not change this once you publish your pack.
    /// For example, you could use "YourName_YourPackName" as the identifier.
    /// </summary>
    [JsonProperty("id", Required = Required.DisallowNull)]
    public string Id { get; set; } = "";

    /// <summary>
    /// A user-readable display name for your audio pack. Used in the UI.
    /// </summary>
    [JsonProperty("display_name", Required = Required.DisallowNull)]
    public string DisplayName { get; set; } = "";

    public class Settings
    {
        /// <summary>
        /// If true, vanilla clips will be automatically loaded and replaced based on name.
        /// For example, _mu_flyby.ogg would be loaded and replace _mu_flyby in-game.
        /// </summary>
        [JsonProperty("autoload_replacement_clips", Required = Required.DisallowNull)]
        public bool AutoloadReplacementClips { get; set; } = true;
    }

    /// <summary>
    /// Holds settings for the audio pack.
    /// </summary>
    [JsonProperty("settings", Required = Required.DisallowNull)]
    public Settings AudioPackSettings { get; set; } = new();

    public class AudioClipData
    {
        /// <summary>
        /// An unique name for your clip.
        /// It would be a good idea to use something that wouldn't conflict with other pack clip names.
        /// </summary>
        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; set; } = "";

        /// <summary>
        /// The relative path to your clip's audio file.
        /// </summary>
        [JsonProperty("path", Required = Required.DisallowNull)]
        public string Path { get; set; } = "";

        /// <summary>
        /// A volume modifier for your clip.
        /// This is the only place where you can amplify audio by using modifiers above 1.0.
        /// </summary>
        [JsonProperty("volume", Required = Required.DisallowNull)]
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// If true, extension is ignored (i.e. the first audio file matching the name is loaded).
        /// If false, extension is taken into account (i.e. the exact audio file is loaded).
        /// </summary>
        [JsonProperty("ignore_clip_extension", Required = Required.DisallowNull)]
        public bool IgnoreClipExtension { get; set; } = false;
    }

    /// <summary>
    /// A list of custom clips defined by your audio pack.
    /// </summary>
    [JsonProperty("custom_clips", Required = Required.DisallowNull)]
    public List<AudioClipData> CustomClips { get; set; } = [];

    public class Route
    {
        public class ClipSelection
        {
            /// <summary>
            /// The name of the clip.
            /// A special value of "___default___" will select the original clip as replacement.
            /// A special value of "___nothing___" replaces the audio clip with an empty one.
            /// </summary>
            [JsonProperty("name", Required = Required.Always)]
            public string Name { get; set; } = "";

            /// <summary>
            /// How often it should be selected compared to other clips.
            /// </summary>
            [JsonProperty("weight", Required = Required.DisallowNull)]
            public float Weight { get; set; } = 1f;

            /// <summary>
            /// Volume adjustment for this selection.
            /// </summary>
            [JsonProperty("volume", Required = Required.DisallowNull)]
            public float Volume { get; set; } = 1f;

            /// <summary>
            /// Pitch adjustment for this selection.
            /// </summary>
            [JsonProperty("pitch", Required = Required.DisallowNull)]
            public float Pitch { get; set; } = 1f;
        }

        /// <summary>
        /// If true, overlays can only play if the replacement from the same route has been selected.
        /// If false, overlays can play separately from the replacement.
        /// </summary>
        [JsonProperty("link_overlay_and_replacement", Required = Required.DisallowNull)]
        public bool LinkOverlayAndReplacement { get; set; } = true;

        /// <summary>
        /// If true, overlays will stop if the source audio is stopped programatically.
        /// If false, overlays will continue to play even if the source audio is stopped.
        /// </summary>
        [JsonProperty("overlay_stops_if_source_stops", Required = Required.DisallowNull)]
        public bool OverlayStopsIfSourceStops { get; set; } = true;

        /// <summary>
        /// If true, replacement effects (volume, pitch, etc.) are modifiers on top of the original source.
        /// If false, replacement effects override the original source's effects.
        /// </summary>
        [JsonProperty("relative_replacement_effects", Required = Required.DisallowNull)]
        public bool RelativeReplacementEffects { get; set; } = true;

        /// <summary>
        /// If true, overlay effects (volume, pitch, etc.) are modifiers on top of the original source.
        /// If false, overlay effects override the original source's effects.
        /// </summary>
        [JsonProperty("relative_overlay_effects", Required = Required.DisallowNull)]
        public bool RelativeOverlayEffects { get; set; } = false;

        /// <summary>
        /// If true, overlay effects (volume, pitch, etc.) will not be played again if the audio source has been restarted while playing.
        /// If false, overlay effects will play on every playthrough, including restarts.
        /// This may be helpful to deal with sources that are played multiple times in the same frame.
        /// </summary>
        [JsonProperty("overlays_ignore_restarts", Required = Required.DisallowNull)]
        public bool OverlaysIgnoreRestarts { get; set; } = false;

        /// <summary>
        /// A list of clips that will be affected by this route.
        /// </summary>
        [JsonProperty("original_clips", Required = Required.Always)]
        public List<string> OriginalClips { get; set; } = [];

        /// <summary>
        /// A list of clips to be used as replacements for this route.
        /// </summary>
        [JsonProperty("replacement_clips", Required = Required.DisallowNull)]
        public List<ClipSelection> ReplacementClips { get; set; } = [];

        /// <summary>
        /// A list of clips to be used as overlays for this route.
        /// </summary>
        [JsonProperty("overlay_clips", Required = Required.DisallowNull)]
        public List<ClipSelection> OverlayClips { get; set; } = [];

        /// <summary>
        /// A list of source names that act as filters. If empty, the filter is disabled.
        /// You can use this to discriminate between audio played using the same clip from different sources.
        /// </summary>
        [JsonProperty("filter_by_sources", Required = Required.DisallowNull)]
        public List<string> FilterBySources { get; set; } = [];

        /// <summary>
        /// A list of object names in the hierarchy that act as filters. If empty, the filter is disabled.
        /// You can use this to discriminate between audio played using the same clip from different objects.
        /// </summary>
        [JsonProperty("filter_by_object", Required = Required.DisallowNull)]
        public List<string> FilterByObject { get; set; } = [];

        /// <summary>
        /// A list of map instance names that act as filters. If empty, the filter is disabled.
        /// You can use this to discriminate between audio played in different maps.
        /// As a special case, ___nomap___ matches any cases where there isn't an active map available.
        /// </summary>
        [JsonProperty("filter_by_map_name", Required = Required.DisallowNull)]
        public List<string> FilterByMapName { get; set; } = [];

        /// <summary>
        /// Determines how often the replacement from this route will be used relative to other replacements.
        /// </summary>
        [JsonProperty("replacement_weight", Required = Required.DisallowNull)]
        public float ReplacementWeight { get; set; } = 1f;

        /// <summary>
        /// Volume modifier for the audio source.
        /// </summary>
        [JsonProperty("volume", Required = Required.DisallowNull)]
        public float Volume { get; set; } = 1f;

        /// <summary>
        /// Pitch modifier for the audio source.
        /// </summary>
        [JsonProperty("pitch", Required = Required.DisallowNull)]
        public float Pitch { get; set; } = 1f;

        /// <summary>
        /// If set to true, the audio source will be forced to loop even if it normally shouldn't.
        /// </summary>
        [JsonProperty("force_loop", Required = Required.DisallowNull)]
        public bool ForceLoop { get; set; } = false;
    }

    /// <summary>
    /// A list of routes defined by your audio pack.
    /// </summary>
    [JsonProperty("routes", Required = Required.DisallowNull)]
    public List<Route> Routes { get; set; } = [];

    public static AudioPackConfig ReadJSON(Stream stream)
    {
        List<string> warnings = [];

        var reader = new StreamReader(stream);
        var data = JsonConvert.DeserializeObject<AudioPackConfig>(reader.ReadToEnd(), new JsonSerializerSettings()
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            Error = (object obj, Newtonsoft.Json.Serialization.ErrorEventArgs e) =>
            {
                if (e.ErrorContext.Error is JsonException exception)
                {
                    if (exception.Message.Contains("Could not find member"))
                    {
                        warnings.Add(exception.Message);
                        e.ErrorContext.Handled = true;
                    }
                }
            }
        }) ?? throw new InvalidOperationException("JSON deserialization returned a null object.");

        foreach (var warning in warnings)
        {
            Logging.LogWarning(warning);
        }

        return data;
    }

    public static AudioPackConfig ConvertFromRoutes(RouteConfig routeConfig)
    {
        var config = new AudioPackConfig
        {
            CustomClips = routeConfig.Routes
                .SelectMany(x => x.ReplacementClips.Concat(x.OverlayClips))
                .Select(x => x.Name)
                .Where(x => x != "___default___" && x != "___nothing___") // TODO: Move these magic strings to a constant
                .Distinct()
                .Select(x => new AudioClipData()
                {
                    Name = x,
                    Path = x,
                    IgnoreClipExtension = true,
                    Volume = routeConfig.ClipVolumes.ContainsKey(x) ? routeConfig.ClipVolumes[x] : 1f
                })
                .ToList(),
            Routes = routeConfig.Routes,
            Id = routeConfig.Id,
            DisplayName = routeConfig.DisplayName,
        };

        foreach (var clipVolume in routeConfig.ClipVolumes)
        {
            if (!config.CustomClips.Any(x => x.Name == clipVolume.Key))
                Logging.LogWarning($"Couldn't find clip {clipVolume.Key} to set volume for.");
        }

        return config;
    }

    public static string GenerateSchema()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return new JsonSchemaGenerator().Generate(typeof(AudioPackConfig)).ToString();
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
