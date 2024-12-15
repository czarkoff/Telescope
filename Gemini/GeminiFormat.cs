using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace Telescope.Gemini;

public class GeminiLine(string prefix, string? content)
{
    public string Prefix { get; set; } = prefix;
    public string? Content { get; set; } = content;

    public static IEnumerable<GeminiLine> Parse(Uri uri, IEnumerable<string> geminiData)
    {
        List<GeminiLine> result = [];
        GeminiPreformatted? current = null;
        foreach (string line in geminiData)
        {
            if (current != null && line.StartsWith("```"))
            {
                yield return current;
                current = null;
            }
            else if (current != null)
            {
                current.AddLine(line);
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            else if (HeaderRegex.Match(line) is Match headerMatch && headerMatch.Success)
            {
                yield return new GeminiHeading(headerMatch.Groups[1].Value, headerMatch.Groups[2].Value);
            }
            else if (LinkRegex.Match(line) is Match linkMatch && linkMatch.Success)
            {
                yield return new GeminiLink(linkMatch.Groups[1].Value, uri, linkMatch.Groups[2].Value, linkMatch.Groups[3].Value);
            }
            else if (line.StartsWith("* "))
            {
                yield return new GeminiText(line.Substring(2).Trim());
            }
            else if (line.StartsWith(">"))
            {
                yield return new GeminiQuote(line.Substring(1).Trim());
            }
            else if (line.StartsWith("```"))
            {
                current = new GeminiPreformatted(line.Substring(3).Trim());
            }
            else
            {
                yield return new GeminiText(line.Trim());
            }
        }

        if (current != null)
            yield return current;
    }

    private static Regex LinkRegex = new Regex(@"^(=>)\s+(\S+)(?>\s+(\S.+)\s*)$", RegexOptions.Compiled);
    private static Regex HeaderRegex = new Regex(@"^(#+)\s+(\S.+)\s*$", RegexOptions.Compiled);
}

public class GeminiHeading(string prefix, string content) : GeminiLine(prefix, content)
{ }

public class GeminiText(string content) : GeminiLine(string.Empty, content)
{ }

public class GeminiLink(string prefix, Uri currentUrl, string url, string content) : GeminiLine(prefix, content)
{
    public string UrlHint { get; set; } = url;
    public Uri AbsoluteUrl { get; set; } = new Uri(currentUrl, url);
}

public class GeminiListItem(string content) : GeminiLine("•", content)
{ }

public class GeminiQuote(string content) : GeminiLine("> ", content)
{ }

public class GeminiPreformatted(string content) : GeminiLine("`", content)
{
    public string Body => string.Join(Environment.NewLine, _lines);

    internal void AddLine(string line) => _lines.Add(line);

    private List<string> _lines = [];
}