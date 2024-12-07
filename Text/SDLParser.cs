using System.Text.RegularExpressions;
using EconomyBot.Logging;

namespace EconomyBot;

/// <summary>
/// This reads the file and reads it into a dictionary.
/// </summary>
public partial class SDLParser {

    private Logger logger = Logger.getClassLogger(nameof(SDLParser));

    public Dictionary<string, Artist> parse(string file) {
        var contents = File.ReadAllText(file);

        return ParseConfig(contents);
    }

    public Dictionary<string, Artist> ParseConfig(string content) {
        var artists = new Dictionary<string, Artist>();

        // Remove comments and normalize spacing
        content = RemoveComments(content);
        content = normalize().Replace(content, " ");

        // Extract the main artists section
        var match = artistRegex().Match(content);
        if (!match.Success) {
            throw new FormatException("Invalid config format: Missing (artists ...) section");
        }

        var artistsContent = match.Groups[1].Value.Trim();

        // Split into individual artist entries
        var entries = SplitEntries(artistsContent);

        foreach (var entry in entries) {
            if (string.IsNullOrWhiteSpace(entry)) continue;

            var artist = ParseArtistEntry(entry);
            if (artist != null) {
                artists[artist.name] = artist;
            }
        }

        return artists;
    }

    private string RemoveComments(string content) {
        var lines = content.Split('\n');
        var result = new List<string>();

        foreach (var line in lines) {
            var commentIndex = line.IndexOf(';');
            if (commentIndex >= 0) {
                // Only take the part before the comment
                result.Add(line[..commentIndex]);
            }
            else {
                result.Add(line);
            }
        }

        return string.Join("\n", result);
    }

    private List<string> SplitEntries(string content) {
        var entries = new List<string>();
        var depth = 0;
        var currentEntry = "";
        var inEntry = false;

        foreach (var c in content) {
            if (c == '(') {
                depth++;
                if (depth == 1) {
                    inEntry = true;
                    currentEntry = "(";
                    continue;
                }
            }
            else if (c == ')') {
                depth--;
                if (depth == 0 && inEntry) {
                    currentEntry += ")";
                    entries.Add(currentEntry.Trim());
                    currentEntry = "";
                    inEntry = false;
                    continue;
                }
            }

            if (inEntry) {
                currentEntry += c;
            }
        }

        return entries;
    }

    private Artist ParseArtistEntry(string entry) {
        //Console.Out.WriteLine("Artist: " + entry);
        // Remove outer parentheses
        entry = entry.Trim('(', ')').Trim();

        // Split the entry into parts, respecting quoted strings
        var parts = SplitQuotedString(entry);

        if (parts.Count < 3) // Must have at least id, display name, and full path
        {
            logger.warn("Malformed artist entry: " + entry);
            return null;
        }
        double volume, weight, historyRepeatPenalty, doubleRepeatPenalty;
        try {
            volume = parts.Count > 3 ? double.Parse(parts[3]) : 1.0;
        }
        catch (FormatException e) {
            logger.warn("Malformed artist entry: " + entry);
            return null;
        }
        try {
            weight = parts.Count > 4 ? double.Parse(parts[4]) : 1.0;
        }
        catch (FormatException e) {
            logger.warn("Malformed artist entry: " + entry);
            return null;
        }
        try {
            historyRepeatPenalty = parts.Count > 5 ? double.Parse(parts[5]) : 1.0;
        }
        catch (FormatException e) {
            logger.warn("Malformed artist entry: " + entry);
            return null;
        }
        try {
            doubleRepeatPenalty = parts.Count > 6 ? double.Parse(parts[6]) : 1.0;
        }
        catch (FormatException e) {
            logger.warn("Malformed artist entry: " + entry);
            return null;
        }
        var artist = new Artist(
            parts[1].Trim('"'),
            parts[2].Trim('"'),
            volume,
            weight,
            historyRepeatPenalty,
            doubleRepeatPenalty
        );

        return artist;
    }

    private List<string> SplitQuotedString(string input) {
        var result = new List<string>();
        var current = "";
        var inQuotes = false;

        foreach (var c in input) {
            if (c == '"') {
                inQuotes = !inQuotes;
                current += c;
            }
            else if (c == ' ' && !inQuotes) {
                if (!string.IsNullOrEmpty(current)) {
                    result.Add(current);
                    current = "";
                }
            }
            else {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current)) {
            result.Add(current);
        }

        return result;
    }

    [GeneratedRegex(@"\(artists(.*)\)", RegexOptions.Singleline)]
    private static partial Regex artistRegex();
    [GeneratedRegex(@"\r\n?|\n")]
    private static partial Regex normalize();
}

/// <summary>
/// The script environment.
/// </summary>
public class Env {
    public Dictionary<string, Artist> artists;
}