namespace Marioalexsan.ModAudio;

public class RouteConfig
{
    private static readonly char[] ReplacementSeparator = ['='];
    private static readonly char[] OverlaySeparator = ['@'];
    private static readonly char[] EffectSeparator = ['~'];
    private static readonly char[] RouteSeparators = [.. ReplacementSeparator, .. OverlaySeparator, .. EffectSeparator];

    private static readonly char[] FieldSeparator = [':'];
    private static readonly char[] ListSeparator = ['|'];

    public Dictionary<string, float> ClipVolumes { get; set; } = [];
    public List<AudioPackConfig.Route> Routes { get; set; } = [];
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";

    // Note: this format is stupid and dumb and I hate it and ugh why
    public static RouteConfig ReadTextFormat(string path)
    {
        using var stream = new StreamReader(File.OpenRead(path));

        var clipVolumes = new Dictionary<string, float>();
        var routes = new List<AudioPackConfig.Route>();
        var id = "";
        var displayName = "";

        string line;
        int lineNumber = -1;

        while ((line = stream.ReadLine()) != null)
        {
            lineNumber++;

            if (line.Trim() == "")
                continue; // Empty line

            if (line.Trim().StartsWith("#"))
                continue; // Comments

            if (line.Trim().StartsWith("%"))
            {
                ParseGlobalParamer(line);
                continue;
            }

            if (line.Contains("/") || line.IndexOfAny(EffectSeparator) == -1 && line.IndexOfAny(OverlaySeparator) == -1 && line.IndexOfAny(FieldSeparator) == -1 && line.IndexOfAny(ListSeparator) == -1)
            {
                ParseSimpleRouteFormat(line);
                continue;
            }

            if (!SplitRouteParts(line, out var clipNames, out var replacements, out var overlays, out var effects))
                continue;

            var route = new AudioPackConfig.Route
            {
                OriginalClips = clipNames
            };

            for (int i = 0; i < replacements.Count; i++)
            {
                var fields = replacements[i].Split(FieldSeparator).Select(x => x.Trim()).ToArray();

                if (fields.Length > 4)
                {
                    Logging.LogWarning($"Line {lineNumber}: Too many values defined for a target clip (expected at most 4), skipping it.");
                    continue;
                }

                var replacementName = fields[0];

                if (replacementName == "")
                {
                    Logging.LogWarning($"Line {lineNumber}: empty clip, ignoring it.");
                    replacements.RemoveAt(i--);
                }

                var randomWeight = ModAudio.DefaultWeight;
                var volume = 1f;
                var pitch = 1f;

                if (fields.Length > 1 && !float.TryParse(fields[1], out randomWeight))
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse random weight {fields[1]} for {replacementName}, defaulting to {randomWeight}.");

                if (fields.Length > 2 && !float.TryParse(fields[2], out volume))
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse volume {fields[2]} for {replacementName}, defaulting to {volume}.");

                if (fields.Length > 3 && !float.TryParse(fields[3], out pitch))
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse pitch {fields[3]} for {replacementName}, defaulting to {pitch}.");

                route.ReplacementClips.Add(new()
                {
                    Name = replacementName,
                    Weight = randomWeight,
                    Volume = volume,
                    Pitch = pitch
                });
            }

            routes.Add(route);

            for (int i = 0; i < overlays.Count; i++)
            {
                var fields = overlays[i].Split(FieldSeparator).Select(x => x.Trim()).ToArray();

                if (fields.Length > 4)
                {
                    Logging.LogWarning($"Line {lineNumber}: Too many values defined for a target clip (expected at most 4), skipping it.");
                    continue;
                }

                var overlayName = fields[0];

                if (overlayName == "")
                {
                    ModAudio.Plugin?.Logger?.LogWarning($"Line {lineNumber}: empty clip, ignoring it.");
                    overlays.RemoveAt(i--);
                }

                var randomWeight = ModAudio.DefaultWeight;
                var volume = 1f;
                var pitch = 1f;

                if (fields.Length > 1 && !float.TryParse(fields[1], out randomWeight))
                {
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse random weight {fields[1]} for {overlayName}, defaulting to {ModAudio.DefaultWeight}.");
                }

                if (fields.Length > 2 && !float.TryParse(fields[2], out volume))
                {
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse volume {fields[2]} for {overlayName}, defaulting to 1.");
                }

                if (fields.Length > 3 && !float.TryParse(fields[3], out pitch))
                {
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse pitch {fields[3]} for {overlayName}, defaulting to 1.");
                }

                route.OverlayClips.Add(new()
                {
                    Name = overlayName,
                    Weight = randomWeight,
                    Volume = volume,
                    Pitch = pitch
                });
            }

            for (int i = 0; i < effects.Count; i++)
            {
                var fields = effects[i].Split(FieldSeparator).Select(x => x.Trim()).ToArray();

                if (fields.Length == 1)
                {
                    Logging.LogWarning($"Line {lineNumber}: Expected a value for {fields[0]}.");
                    continue;
                }

                bool parsed = false;

                switch (fields[0])
                {
                    case "link_overlay_and_replacement":
                        if (parsed = bool.TryParse(fields[1], out bool link_overlay_and_replacement))
                            route.LinkOverlayAndReplacement = link_overlay_and_replacement;
                        break;
                    case "relative_replacement_effects":
                        if (parsed = bool.TryParse(fields[1], out bool relative_replacement_effects))
                            route.RelativeReplacementEffects = relative_replacement_effects;
                        break;
                    case "overlay_stops_if_source_stops":
                        if (parsed = bool.TryParse(fields[1], out bool overlay_stops_if_source_stops))
                            route.OverlayStopsIfSourceStops = overlay_stops_if_source_stops;
                        break;
                    case "relative_overlay_effects":
                        if (parsed = bool.TryParse(fields[1], out bool relative_overlay_effects))
                            route.RelativeOverlayEffects = relative_overlay_effects;
                        break;
                    case "overlays_ignore_restarts":
                        if (parsed = bool.TryParse(fields[1], out bool overlaysIgnoreRestarts))
                            route.OverlaysIgnoreRestarts = overlaysIgnoreRestarts;
                        break;
                    case "filter_by_sources":
                    case "sources":
                        route.FilterBySources = [.. fields[1..]];
                        parsed = true;
                        break;
                    case "filter_by_object":
                    case "object":
                        route.FilterByObject = [.. fields[1..]];
                        parsed = true;
                        break;
                    case "filter_by_map_name":
                    case "map_name":
                        route.FilterByMapName = [.. fields[1..]];
                        parsed = true;
                        break;
                    case "replacement_weight":
                    case "weight":
                        if (parsed = float.TryParse(fields[1], out float replacementWeight))
                            route.ReplacementWeight = replacementWeight;
                        break;
                    case "volume":
                        if (parsed = float.TryParse(fields[1], out float volume))
                            route.Volume = volume;
                        break;
                    case "pitch":
                        if (parsed = float.TryParse(fields[1], out float pitch))
                            route.Pitch = pitch;
                        break;
                    case "force_loop":
                        if (parsed = bool.TryParse(fields[1], out bool force_loop))
                            route.ForceLoop = force_loop;
                        break;
                    default:
                        Logging.LogWarning($"Line {lineNumber}: Unrecognized route effect / setting {fields[0]}.");
                        break;
                }

                if (!parsed)
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse {fields[0]}.");
            }
        }

        return new()
        {
            ClipVolumes = clipVolumes,
            Id = id,
            DisplayName = displayName,
            Routes = routes
        };

        bool SplitRouteParts(string line, out List<string> clipNames, out List<string> replacements, out List<string> overlays, out List<string> effects)
        {
            clipNames = [];
            replacements = [];
            overlays = [];
            effects = [];

            var parts = line.Split(RouteSeparators, 5, StringSplitOptions.None);

            var replacementArrayIndex = -1;
            var overlayArrayIndex = -1;
            var effectArrayIndex = -1;

            if (parts.Length > 5 || parts.Length == 1)
            {
                Logging.LogWarning($"Line {lineNumber}: Encountered a malformed route (expected source = replacement @ overlay ~ modifier), skipping it.");
                return false;
            }

            int nextIndex = line.IndexOfAny(RouteSeparators, 0);
            int nextArrayIndex = 1;

            if (nextIndex == -1)
            {
                Logging.LogWarning($"Line {lineNumber}: Encountered a malformed route (expected source = replacement @ overlay ~ effect), skipping it.");
                return false;
            }

            bool invalidRoute = false;

            while (nextIndex != -1)
            {
                if (line[nextIndex] == ReplacementSeparator[0])
                {
                    if (replacementArrayIndex != -1 || overlayArrayIndex != -1 || effectArrayIndex != -1)
                    {
                        invalidRoute = true;
                        break;
                    }

                    replacementArrayIndex = nextArrayIndex++;
                }
                else if (line[nextIndex] == OverlaySeparator[0])
                {
                    if (overlayArrayIndex != -1)
                    {
                        invalidRoute = true;
                        break;
                    }

                    overlayArrayIndex = nextArrayIndex++;
                }

                else if (line[nextIndex] == EffectSeparator[0])
                {
                    if (effectArrayIndex != -1)
                    {
                        invalidRoute = true;
                        break;
                    }

                    effectArrayIndex = nextArrayIndex++;
                }

                nextIndex = line.IndexOfAny(RouteSeparators, nextIndex + 1);
            }

            if (invalidRoute)
            {
                Logging.LogWarning($"Line {lineNumber}: Encountered a malformed route (expected source = replacement @ overlay ~ effect), skipping it.");
                return false;
            }

            bool overlaysComeFirst = overlayArrayIndex < effectArrayIndex;

            List<string> ParseListEntries(string part) => part.Trim().Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

            clipNames = ParseListEntries(parts[0]);

            for (int i = 0; i < clipNames.Count; i++)
            {
                if (clipNames[i] == "")
                {
                    Logging.LogWarning($"Line {lineNumber}: empty source clip, ignoring it.");
                    clipNames.RemoveAt(i--);
                }
            }

            if (clipNames.Count == 0)
            {
                Logging.LogWarning($"Line {lineNumber}: Expected at least one valid source clip, skipping route.");
                return false;
            }

            replacements = replacementArrayIndex == -1 ? [] : ParseListEntries(parts[replacementArrayIndex]);
            overlays = overlayArrayIndex == -1 ? [] : ParseListEntries(parts[overlayArrayIndex]);
            effects = effectArrayIndex == -1 ? [] : ParseListEntries(parts[effectArrayIndex]);

            return true;
        }

        void ParseGlobalParamer(string line)
        {
            if (line.Trim().StartsWith("%id "))
            {
                id = line.Trim()["%id ".Length..];
            }
            else if (line.Trim().StartsWith("%displayname "))
            {
                displayName = line.Trim()["%displayname ".Length..];
            }
            else if (line.Trim().StartsWith("%customclipvolume "))
            {
                var customClipData = line.Trim()["%customclipvolume ".Length..].Split('=');

                if (customClipData.Length != 2)
                    Logging.LogWarning($"Line {lineNumber}: Expected %customclipvolume clipName = volume.");

                if (customClipData[0].Trim() == "")
                    Logging.LogWarning($"Line {lineNumber}: Expected a clip name.");

                if (!float.TryParse(customClipData[1], out float clipVolume))
                    Logging.LogWarning($"Line {lineNumber}: Volume is not a number.");

                clipVolumes[customClipData[0].Trim()] = clipVolume;
            }
            else
            {
                Logging.LogWarning($"Line {lineNumber}: Unrecognized attribute {line.Trim().Substring(1)}.");
            }
        }

        void ParseSimpleRouteFormat(string line)
        {
            var simpleParts = line.Split(ReplacementSeparator, 3);

            if (simpleParts.Length != 2)
            {
                Logging.LogWarning($"Line {lineNumber}: Encountered a malformed route (expected key = value), skipping it.");
                return;
            }

            var fields = simpleParts[1].Split(['/'], 3);

            if (fields.Length > 2)
            {
                Logging.LogWarning($"Line {lineNumber}: Too many values defined for a route (expected at most 2), skipping it.");
                return;
            }

            var clipName = simpleParts[0].Trim();
            var replacementName = fields[0].Trim();
            var randomWeight = ModAudio.DefaultWeight;

            if (clipName.Trim() == "" || replacementName.Trim() == "")
            {
                Logging.LogWarning($"Line {lineNumber}: Either clip name or replacement was empty for a route, skipping it.");
                return;
            }

            if (fields.Length > 1)
            {
                if (!float.TryParse(fields[1], out randomWeight))
                {
                    Logging.LogWarning($"Line {lineNumber}: Couldn't parse random weight {fields[1]} for {clipName} => {replacementName}, defaulting to {ModAudio.DefaultWeight}.");
                }
            }

            routes.Add(new AudioPackConfig.Route()
            {
                OriginalClips = [clipName],
                ReplacementClips = [new() {
                        Name = replacementName
                    }],
                ReplacementWeight = randomWeight
            });
        }
    }
}
