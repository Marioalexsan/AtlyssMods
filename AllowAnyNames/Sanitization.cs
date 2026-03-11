using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Marioalexsan.AllowAnyNames;

// Note: This is not safe with multithreading
public static class Sanitization
{
    private static readonly StringBuilder SanitizeBuilder = new(1024);

    private static readonly List<(int Position, int Length)> InvalidTagsCache = new(128);
    private static readonly Stack<string> TagStack = new(128);
    private static readonly Regex TagMatcher = new Regex("""<([^\=>]*)(?:=([^>]*))?>""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // This deals with cleaning up / limiting rich text that is otherwise accepted by AAN
    public static string SanitizeRichTextName(string richTextName)
    {
        SanitizeBuilder.Clear();
        SanitizeBuilder.Append(richTextName);

        // Pass #1: remove stray unpaired '<' and '>' characters

        int lastTagStart = -1;

        for (int i = 0; i < SanitizeBuilder.Length; i++)
        {
            if (SanitizeBuilder[i] == '<')
            {
                if (lastTagStart != -1)
                {
                    // Last '<' was junk
                    SanitizeBuilder.Remove(lastTagStart, 1);
                    i--;
                }

                lastTagStart = i;
            }
            else if (SanitizeBuilder[i] == '>')
            {
                if (lastTagStart == -1)
                {
                    // This '>' is junk
                    SanitizeBuilder.Remove(i--, 1);
                }
                else
                {
                    lastTagStart = -1;
                }
            }
        }

        if (lastTagStart != -1)
        {
            // The very last '<' is junk since it was not closed
            SanitizeBuilder.Remove(lastTagStart, 1);
        }

        // Pass #2: sanitize tags and enforce tags to be closed properly
        InvalidTagsCache.Clear();
        TagStack.Clear();

        foreach (Match match in TagMatcher.Matches(SanitizeBuilder.ToString()))
        {
            var tagName = match.Groups[1].Value;
            var param = match.Groups[2].Success ? match.Groups[2].Value : null;

            static bool ValidateParam([NotNullWhen(true)] string? param)
            {
                if (param == null)
                    return false;

                for (int i = 0; i < param.Length; i++)
                {
                    if (char.IsWhiteSpace(param[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            static bool ValidateColor(string? param)
            {
                if (!ValidateParam(param))
                    return false;

                if (param.Length == 9 && param[0] == '#')
                {
                    var firstNibble = char.ToLowerInvariant(param[7]);

                    // Check that alpha is at least 0x20 by checking the first nibble
                    return '2' <= firstNibble && firstNibble <= '9' || 'a' <= firstNibble && firstNibble <= 'f';
                }

                return true;
            }

            static bool ValidateSize(string? param) => ValidateParam(param) && int.TryParse(param, out var size) && 8 <= size && size <= 30;

            bool isValid = tagName switch
            {
                "align" => false,
                "allcaps" => true,
                "alpha" => false,
                "b" => true,
                "br" => false,
                "color" => ValidateColor(param),
                "cspace" => false,
                "font" => true,
                "font-weight" => true,
                "gradient" => false,
                "i" => true,
                "indent" => false,
                "line-height" => false,
                "link" => false,
                "lowercase" => true,
                "margin" => false,
                "mark" => false,
                "mspace" => true,
                "nobr" => false,
                "noparse" => true,
                "page" => false,
                "pos" => false,
                "rotate" => true,
                "s" => true,
                "size" => ValidateSize(param),
                "smallcaps" => true,
                "space" => false,
                "sprite" => false,
                "style" => false,
                "sub" => true,
                "sup" => true,
                "u" => true,
                "uppercase" => true,
                "voffset" => false,
                "width" => false,
                // Disallow other opening tags
                _ => tagName.Length > 0 && tagName[0] == '/'
            };

            if (isValid)
            {
                if (tagName.StartsWith('/'))
                {
                    if (TagStack.TryPeek(out var lastTag) && tagName.AsSpan()[1..].Equals(lastTag.AsSpan(), StringComparison.InvariantCulture))
                    {
                        TagStack.Pop();
                    }
                    else
                    {
                        isValid = false;
                    }
                }
                else
                {
                    TagStack.Push(tagName);
                }
            }

            if (!isValid)
            {
                InvalidTagsCache.Add(new()
                {
                    Position = match.Index,
                    Length = match.Length
                });
            }
        }

        int positionOffset = 0;

        for (int i = 0; i < InvalidTagsCache.Count; i++)
        {
            var (Position, Length) = InvalidTagsCache[i];

            SanitizeBuilder.Remove(Position - positionOffset, Length);
            positionOffset += Length;
        }

        // Pass #3: close all remaining tags forcefully
        while (TagStack.TryPop(out var tag))
        {
            SanitizeBuilder.Append("</").Append(tag).Append('>');
        }

        return SanitizeBuilder.ToString();
    }

    static void TestSanitization()
    {
        string[] names = [
            "test123", // No tags
            "<color=blue>test123</color>", // Valid tag
            "<color=red><color=blue>test123</color></color>", // Nested valid tags
            "<color=blue>test123</color><b>test234</b>", // Sequential valid tags
            "test123</b></color>", // Invalid closing tags
            "<>test123</>", // Empty tags
            "<color=blue><b>test123", // Unclosed valid opening tags
            "<color =blue>test123</color>", // Invalid tag name
            "<color= blue>test123</color>", // Invalid tag parameter
            "<b=blue>test123</b>", // Valid tag name, but parameters are not allowed
            "<color=blue>test123</color=red>", // Invalid closing tag since parameters are not allowed

            // Size tag validation
            "<size=1>test123</size>", // Invalid
            "<size=7>test123</size>", // Invalid
            "<size=8>test123</size>", // Valid
            "<size=30>test123</size>", // Valid
            "<size=31>test123</size>", // Invalid
            "<size=999>test123</size>", // Invalid
            "<size=123.45>test123</size>", // Invalid
            "<size=big>test123</size>", // Invalid

            // Color tag validation
            "<color=#FFFFFF>test123</size>", // Valid
            "<color=#000000FF>test123</size>", // Valid
            "<color=#000000AF>test123</size>", // Valid
            "<color=#0000009F>test123</size>", // Valid
            "<color=#0000002F>test123</size>", // Valid
            "<color=#00000020>test123</size>", // Invalid
            "<color=#0000001F>test123</size>", // Invalid
            "<color=#0000000F>test123</size>", // Invalid
            "<color=#00000000>test123</size>", // Invalid


            // Stray '<' - invalid and stripped before parsing the rest
            "<test123<", 
            "<<test123<<",
            "<<b>test123<</b>",
            "<<<b>test123<<</b>",
            "<b><test123</b><",
            "<b><<test123</b><<",

            // Stray '>' - invalid and stripped before parsing the rest
            ">test123>",
            ">>test123>>",
            "><b>test123></b>",
            ">><b>test123>></b>",
            "<b>>test123</b>>",
            "<b>>>test123</b>>>",
        ];

        foreach (var name in names)
        {
            Logging.LogInfo($"Name sanitization: {name} => {Sanitization.SanitizeRichTextName(name)}");
        }
    }
}