using System.Globalization;
using System.Text.RegularExpressions;

namespace FileIntakeAssistant.Core.Search;

public sealed class SearchIntentParser
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LastCount = new(@"\blast\s+(?<count>\d+|one|two|three|four|five|six|seven|eight|nine|ten)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LastNWeeks = new(@"\blast\s+(?<count>\d+|one|two|three|four|five|six|seven|eight|nine|ten)\s+weeks?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NWeeksAgo = new(@"\b(?<count>\d+|one|two|three|four|five|six|seven|eight|nine|ten)\s+weeks?\s+ago\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NDaysAgo = new(@"\b(?<count>\d+|one|two|three|four|five|six|seven|eight|nine|ten)\s+days?\s+ago\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10
    };

    private static readonly IReadOnlyDictionary<string, string[]> FileTypeExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Excel"] = [".xls", ".xlsx"],
            ["Spreadsheet"] = [".xls", ".xlsx", ".csv", ".tsv"],
            ["CSV"] = [".csv"],
            ["PDF"] = [".pdf"],
            ["Word"] = [".doc", ".docx"],
            ["Document"] = [".pdf", ".doc", ".docx", ".rtf", ".txt", ".md"],
            ["PowerPoint"] = [".ppt", ".pptx"],
            ["Presentation"] = [".ppt", ".pptx"],
            ["Image"] = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".tif", ".tiff"]
        };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "about", "for", "from", "i", "me", "my", "of", "please",
        "saved", "save", "show", "find", "open", "file", "files", "folder", "folders",
        "the", "to", "uh", "um", "that", "this", "with"
    };

    public SearchIntent Parse(string rawText, DateTimeOffset now)
    {
        var normalized = Normalize(rawText);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Unsupported(rawText, "Command text is required.");
        }

        if (ContainsAny(normalized, "delete", "remove", "overwrite", "erase", "move", "rename"))
        {
            return Unsupported(rawText, "Destructive file commands are not supported.");
        }

        var action = ParseAction(normalized);
        var target = normalized.Contains("folder", StringComparison.OrdinalIgnoreCase)
            || action == SearchIntentAction.OpenContainingFolder
                ? SearchResultTarget.Folder
                : SearchResultTarget.File;
        var count = ParseCount(normalized);
        var (fileType, extensions) = ParseFileType(normalized);
        var relevance = ParseRelevance(normalized);
        var dateRange = ParseDateRange(normalized, now);
        var sourceHint = normalized.Contains("download", StringComparison.OrdinalIgnoreCase)
            ? "downloaded"
            : null;
        var keywords = ExtractKeywords(normalized, fileType, relevance, dateRange);
        var sortMode = normalized.Contains("last", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("recent", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("saved", StringComparison.OrdinalIgnoreCase)
                ? SearchSortMode.MostRecent
                : SearchSortMode.BestMatch;

        return new SearchIntent(
            RawText: rawText,
            Action: action,
            Target: target,
            Count: count,
            FileType: fileType,
            Extensions: extensions,
            Relevance: relevance,
            Project: null,
            Topic: null,
            Keywords: keywords,
            DateRange: dateRange,
            SourceHint: sourceHint,
            SortMode: sortMode,
            IsAmbiguous: action == SearchIntentAction.ShowResults && keywords.Count == 0 && extensions.Count == 0 && relevance is null,
            UnsupportedReason: null);
    }

    private static SearchIntent Unsupported(string rawText, string reason)
    {
        return new SearchIntent(
            RawText: rawText,
            Action: SearchIntentAction.Unsupported,
            Target: SearchResultTarget.File,
            Count: null,
            FileType: null,
            Extensions: [],
            Relevance: null,
            Project: null,
            Topic: null,
            Keywords: [],
            DateRange: null,
            SourceHint: null,
            SortMode: SearchSortMode.BestMatch,
            IsAmbiguous: true,
            UnsupportedReason: reason);
    }

    private static SearchIntentAction ParseAction(string normalized)
    {
        if (normalized.Contains("open the folder", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("open folder", StringComparison.OrdinalIgnoreCase))
        {
            return SearchIntentAction.OpenContainingFolder;
        }

        return normalized.StartsWith("open ", StringComparison.OrdinalIgnoreCase)
            ? SearchIntentAction.OpenFiles
            : SearchIntentAction.ShowResults;
    }

    private static int? ParseCount(string normalized)
    {
        var match = LastCount.Match(normalized);
        return match.Success ? ParseNumber(match.Groups["count"].Value) : null;
    }

    private static (string? FileType, IReadOnlyList<string> Extensions) ParseFileType(string normalized)
    {
        foreach (var entry in FileTypeExtensions)
        {
            var key = entry.Key;
            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(key)}s?\b", RegexOptions.IgnoreCase))
            {
                return (key, entry.Value);
            }
        }

        return (null, []);
    }

    private static string? ParseRelevance(string normalized)
    {
        foreach (var relevance in new[] { "high", "medium", "low" })
        {
            if (Regex.IsMatch(normalized, $@"\b{relevance}\s+relevance\b", RegexOptions.IgnoreCase))
            {
                return relevance;
            }
        }

        return null;
    }

    private static SearchDateRange? ParseDateRange(string normalized, DateTimeOffset now)
    {
        var today = StartOfDay(now);
        var thisWeek = StartOfWeek(today);
        var thisMonth = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, today.Offset);
        var thisYear = new DateTimeOffset(today.Year, 1, 1, 0, 0, 0, today.Offset);

        if (ContainsPhrase(normalized, "today"))
        {
            return new SearchDateRange("today", today, today.AddDays(1));
        }

        if (ContainsPhrase(normalized, "yesterday"))
        {
            return new SearchDateRange("yesterday", today.AddDays(-1), today);
        }

        if (ContainsPhrase(normalized, "this week"))
        {
            return new SearchDateRange("this week", thisWeek, thisWeek.AddDays(7));
        }

        if (ContainsPhrase(normalized, "last week"))
        {
            return new SearchDateRange("last week", thisWeek.AddDays(-7), thisWeek);
        }

        var lastNWeeks = LastNWeeks.Match(normalized);
        if (lastNWeeks.Success)
        {
            var weeks = ParseNumber(lastNWeeks.Groups["count"].Value) ?? 1;
            return new SearchDateRange($"last {weeks} weeks", thisWeek.AddDays(-7 * weeks), thisWeek);
        }

        var nWeeksAgo = NWeeksAgo.Match(normalized);
        if (nWeeksAgo.Success)
        {
            var weeks = ParseNumber(nWeeksAgo.Groups["count"].Value) ?? 1;
            var from = thisWeek.AddDays(-7 * weeks);
            return new SearchDateRange($"{weeks} weeks ago", from, from.AddDays(7));
        }

        var nDaysAgo = NDaysAgo.Match(normalized);
        if (nDaysAgo.Success)
        {
            var days = ParseNumber(nDaysAgo.Groups["count"].Value) ?? 1;
            var from = today.AddDays(-days);
            return new SearchDateRange($"{days} days ago", from, from.AddDays(1));
        }

        if (ContainsPhrase(normalized, "this month"))
        {
            return new SearchDateRange("this month", thisMonth, thisMonth.AddMonths(1));
        }

        if (ContainsPhrase(normalized, "last month"))
        {
            return new SearchDateRange("last month", thisMonth.AddMonths(-1), thisMonth);
        }

        if (ContainsPhrase(normalized, "this year"))
        {
            return new SearchDateRange("this year", thisYear, thisYear.AddYears(1));
        }

        if (ContainsPhrase(normalized, "last year"))
        {
            return new SearchDateRange("last year", thisYear.AddYears(-1), thisYear);
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractKeywords(
        string normalized,
        string? fileType,
        string? relevance,
        SearchDateRange? dateRange)
    {
        var scrubbed = normalized;
        foreach (var phrase in new[]
        {
            "open the folder", "open folder", "containing folder", "last week", "this week",
            "last month", "this month", "last year", "this year", "two weeks ago"
        })
        {
            scrubbed = scrubbed.Replace(phrase, " ", StringComparison.OrdinalIgnoreCase);
        }

        scrubbed = LastNWeeks.Replace(scrubbed, " ");
        scrubbed = NWeeksAgo.Replace(scrubbed, " ");
        scrubbed = NDaysAgo.Replace(scrubbed, " ");
        scrubbed = LastCount.Replace(scrubbed, " ");

        if (fileType is not null)
        {
            scrubbed = Regex.Replace(scrubbed, $@"\b{Regex.Escape(fileType)}s?\b", " ", RegexOptions.IgnoreCase);
        }

        if (relevance is not null)
        {
            scrubbed = Regex.Replace(scrubbed, $@"\b{Regex.Escape(relevance)}\s+relevance\b", " ", RegexOptions.IgnoreCase);
        }

        if (dateRange is not null)
        {
            scrubbed = scrubbed.Replace(dateRange.Label, " ", StringComparison.OrdinalIgnoreCase);
        }

        return Whitespace.Split(scrubbed)
            .Select(word => word.Trim())
            .Where(word => word.Length > 1)
            .Where(word => !StopWords.Contains(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(word => word, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DateTimeOffset StartOfDay(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset value)
    {
        var daysSinceMonday = ((int)value.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return StartOfDay(value).AddDays(-daysSinceMonday);
    }

    private static bool ContainsPhrase(string normalized, string phrase)
    {
        return Regex.IsMatch(normalized, $@"\b{Regex.Escape(phrase)}\b", RegexOptions.IgnoreCase);
    }

    private static bool ContainsAny(string normalized, params string[] words)
    {
        return words.Any(word => Regex.IsMatch(normalized, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase));
    }

    private static int? ParseNumber(string value)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return NumberWords.TryGetValue(value, out var wordValue) ? wordValue : null;
    }

    private static string Normalize(string value)
    {
        var lower = value.ToLowerInvariant()
            .Replace('?', ' ')
            .Replace('.', ' ')
            .Replace(',', ' ')
            .Replace('!', ' ')
            .Replace('"', ' ')
            .Replace('\'', ' ');

        return Whitespace.Replace(lower, " ").Trim();
    }
}
