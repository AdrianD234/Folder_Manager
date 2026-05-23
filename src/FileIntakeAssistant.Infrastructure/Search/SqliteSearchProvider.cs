using System.Globalization;
using FileIntakeAssistant.Core.Search;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Infrastructure.Search;

public sealed class SqliteSearchProvider : IFileSearchProvider
{
    private readonly string _databasePath;

    public SqliteSearchProvider(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public string Name => "SQLite";

    public async Task<SearchProviderResult> SearchAsync(
        SearchIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var results = intent.Target == SearchResultTarget.Folder
            ? await SearchFoldersAsync(intent, cancellationToken).ConfigureAwait(false)
            : await SearchFilesAsync(intent, cancellationToken).ConfigureAwait(false);

        var limit = intent.Count.GetValueOrDefault(25);
        return new SearchProviderResult(
            Name,
            intent,
            results
                .OrderByDescending(result => result.Score)
                .ThenByDescending(result => result.MatchedAt)
                .ThenBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToArray());
    }

    private async Task<IReadOnlyList<SearchResult>> SearchFilesAsync(
        SearchIntent intent,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                fr.id,
                fr.current_filename,
                fr.current_path,
                fr.extension,
                fr.source_intake_folder_id,
                fr.first_seen_at,
                fr.last_seen_at,
                fr.stable_at,
                me.user_note,
                me.transcript_text,
                me.relevance,
                me.project,
                me.topic,
                me.tags_json,
                me.source_url,
                me.referrer_url,
                me.agent_summary
            FROM file_records fr
            LEFT JOIN metadata_entries me ON me.id = (
                SELECT id
                FROM metadata_entries latest_me
                WHERE latest_me.file_record_id = fr.id
                ORDER BY latest_me.updated_at DESC, latest_me.id DESC
                LIMIT 1
            )
            WHERE fr.is_meaningful = 1;
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var results = new List<SearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = SearchableFileRow.FromReader(reader);
            if (TryBuildFileResult(row, intent, out var result))
            {
                results.Add(result);
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<SearchResult>> SearchFoldersAsync(
        SearchIntent intent,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                fo.id,
                fo.display_name,
                fo.path,
                fo.folder_type,
                fo.created_at,
                fo.updated_at,
                fo.notes_json,
                me.user_note,
                me.transcript_text,
                me.relevance,
                me.project,
                me.topic,
                me.tags_json,
                me.source_url,
                me.referrer_url,
                me.agent_summary
            FROM folder_records fo
            LEFT JOIN metadata_entries me ON me.id = (
                SELECT id
                FROM metadata_entries latest_me
                WHERE latest_me.folder_record_id = fo.id
                ORDER BY latest_me.updated_at DESC, latest_me.id DESC
                LIMIT 1
            );
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var results = new List<SearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = SearchableFolderRow.FromReader(reader);
            if (TryBuildFolderResult(row, intent, out var result))
            {
                results.Add(result);
            }
        }

        return results;
    }

    private static bool TryBuildFileResult(
        SearchableFileRow row,
        SearchIntent intent,
        out SearchResult result)
    {
        result = null!;

        var reasons = new List<string>();
        var score = 0d;

        if (intent.Extensions.Count > 0)
        {
            if (!intent.Extensions.Contains(row.Extension, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            score += 40;
            reasons.Add($"file type {intent.FileType}");
        }

        if (!MatchesRelevance(row.Relevance, intent.Relevance, reasons, ref score))
        {
            return false;
        }

        var matchedAt = row.StableAt ?? row.LastSeenAt ?? row.FirstSeenAt;
        if (!MatchesDate(matchedAt, intent.DateRange, reasons, ref score))
        {
            return false;
        }

        if (!MatchesRequiredText(row.Project, intent.Project, "project", reasons, ref score)
            || !MatchesRequiredText(row.Topic, intent.Topic, "topic", reasons, ref score))
        {
            return false;
        }

        if (string.Equals(intent.SourceHint, "downloaded", StringComparison.OrdinalIgnoreCase))
        {
            if (row.SourceIntakeFolderId is null)
            {
                return false;
            }

            score += 5;
            reasons.Add("downloaded intake source");
        }

        var searchableText = row.SearchableText;
        var matchedKeywords = MatchKeywords(searchableText, intent.Keywords);
        if (intent.Keywords.Count > 0 && matchedKeywords.Count != intent.Keywords.Count)
        {
            return false;
        }

        score += matchedKeywords.Count * 10;
        reasons.AddRange(matchedKeywords.Select(keyword => $"keyword {keyword}"));
        score += RecencyScore(matchedAt);

        result = new SearchResult(
            Target: SearchResultTarget.File,
            RecordId: row.Id,
            DisplayName: row.CurrentFilename,
            Path: row.CurrentPath,
            ContainingFolder: Path.GetDirectoryName(row.CurrentPath),
            Extension: row.Extension,
            MatchedAt: matchedAt,
            Relevance: row.Relevance,
            Project: row.Project,
            Topic: row.Topic,
            MatchedReasons: reasons.Count == 0 ? ["recent file"] : reasons,
            Score: score);
        return true;
    }

    private static bool TryBuildFolderResult(
        SearchableFolderRow row,
        SearchIntent intent,
        out SearchResult result)
    {
        result = null!;

        var reasons = new List<string>();
        var score = 0d;
        var matchedAt = row.UpdatedAt ?? row.CreatedAt;

        if (!MatchesDate(matchedAt, intent.DateRange, reasons, ref score)
            || !MatchesRelevance(row.Relevance, intent.Relevance, reasons, ref score)
            || !MatchesRequiredText(row.Project, intent.Project, "project", reasons, ref score)
            || !MatchesRequiredText(row.Topic, intent.Topic, "topic", reasons, ref score))
        {
            return false;
        }

        var matchedKeywords = MatchKeywords(row.SearchableText, intent.Keywords);
        if (intent.Keywords.Count > 0 && matchedKeywords.Count != intent.Keywords.Count)
        {
            return false;
        }

        score += 25;
        score += matchedKeywords.Count * 10;
        reasons.Add("folder context");
        reasons.AddRange(matchedKeywords.Select(keyword => $"keyword {keyword}"));
        score += RecencyScore(matchedAt);

        result = new SearchResult(
            Target: SearchResultTarget.Folder,
            RecordId: row.Id,
            DisplayName: row.DisplayName,
            Path: row.Path,
            ContainingFolder: row.Path,
            Extension: null,
            MatchedAt: matchedAt,
            Relevance: row.Relevance,
            Project: row.Project,
            Topic: row.Topic,
            MatchedReasons: reasons,
            Score: score);
        return true;
    }

    private static bool MatchesRelevance(
        string? candidate,
        string? required,
        List<string> reasons,
        ref double score)
    {
        if (required is null)
        {
            return true;
        }

        if (!string.Equals(candidate, required, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        score += 25;
        reasons.Add($"{required} relevance");
        return true;
    }

    private static bool MatchesDate(
        DateTimeOffset? candidate,
        SearchDateRange? required,
        List<string> reasons,
        ref double score)
    {
        if (required is null)
        {
            return true;
        }

        if (candidate is null
            || candidate < required.FromInclusive
            || candidate >= required.ToExclusive)
        {
            return false;
        }

        score += 20;
        reasons.Add(required.Label);
        return true;
    }

    private static bool MatchesRequiredText(
        string? candidate,
        string? required,
        string label,
        List<string> reasons,
        ref double score)
    {
        if (required is null)
        {
            return true;
        }

        if (!ContainsNormalized(candidate, required))
        {
            return false;
        }

        score += 20;
        reasons.Add($"{label} {required}");
        return true;
    }

    private static IReadOnlyList<string> MatchKeywords(string text, IReadOnlyList<string> keywords)
    {
        return keywords
            .Where(keyword => ContainsNormalized(text, keyword))
            .ToArray();
    }

    private static bool ContainsNormalized(string? haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
        {
            return false;
        }

        var normalizedHaystack = NormalizeForSearch(haystack);
        var normalizedNeedle = NormalizeForSearch(needle);
        var singularNeedle = normalizedNeedle.EndsWith('s') && normalizedNeedle.Length > 3
            ? normalizedNeedle[..^1]
            : normalizedNeedle;

        return normalizedHaystack.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase)
            || normalizedHaystack.Contains(singularNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForSearch(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static double RecencyScore(DateTimeOffset? value)
    {
        return value is null ? 0 : Math.Clamp(value.Value.ToUnixTimeSeconds() / 1_000_000_000d, 0, 10);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }

    private sealed record SearchableFileRow(
        long Id,
        string CurrentFilename,
        string CurrentPath,
        string Extension,
        long? SourceIntakeFolderId,
        DateTimeOffset? FirstSeenAt,
        DateTimeOffset? LastSeenAt,
        DateTimeOffset? StableAt,
        string? UserNote,
        string? TranscriptText,
        string? Relevance,
        string? Project,
        string? Topic,
        string? TagsJson,
        string? SourceUrl,
        string? ReferrerUrl,
        string? AgentSummary)
    {
        public string SearchableText => string.Join(
            ' ',
            CurrentFilename,
            CurrentPath,
            Extension,
            UserNote,
            TranscriptText,
            Relevance,
            Project,
            Topic,
            TagsJson,
            SourceUrl,
            ReferrerUrl,
            AgentSummary);

        public static SearchableFileRow FromReader(SqliteDataReader reader)
        {
            return new SearchableFileRow(
                Id: reader.GetInt64(0),
                CurrentFilename: reader.GetString(1),
                CurrentPath: reader.GetString(2),
                Extension: reader.GetString(3),
                SourceIntakeFolderId: GetNullableInt64(reader, 4),
                FirstSeenAt: GetNullableDateTimeOffset(reader, 5),
                LastSeenAt: GetNullableDateTimeOffset(reader, 6),
                StableAt: GetNullableDateTimeOffset(reader, 7),
                UserNote: GetNullableString(reader, 8),
                TranscriptText: GetNullableString(reader, 9),
                Relevance: GetNullableString(reader, 10),
                Project: GetNullableString(reader, 11),
                Topic: GetNullableString(reader, 12),
                TagsJson: GetNullableString(reader, 13),
                SourceUrl: GetNullableString(reader, 14),
                ReferrerUrl: GetNullableString(reader, 15),
                AgentSummary: GetNullableString(reader, 16));
        }
    }

    private sealed record SearchableFolderRow(
        long Id,
        string DisplayName,
        string Path,
        string FolderType,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt,
        string? NotesJson,
        string? UserNote,
        string? TranscriptText,
        string? Relevance,
        string? Project,
        string? Topic,
        string? TagsJson,
        string? SourceUrl,
        string? ReferrerUrl,
        string? AgentSummary)
    {
        public string SearchableText => string.Join(
            ' ',
            DisplayName,
            Path,
            FolderType,
            NotesJson,
            UserNote,
            TranscriptText,
            Relevance,
            Project,
            Topic,
            TagsJson,
            SourceUrl,
            ReferrerUrl,
            AgentSummary);

        public static SearchableFolderRow FromReader(SqliteDataReader reader)
        {
            return new SearchableFolderRow(
                Id: reader.GetInt64(0),
                DisplayName: reader.GetString(1),
                Path: reader.GetString(2),
                FolderType: reader.GetString(3),
                CreatedAt: GetNullableDateTimeOffset(reader, 4),
                UpdatedAt: GetNullableDateTimeOffset(reader, 5),
                NotesJson: GetNullableString(reader, 6),
                UserNote: GetNullableString(reader, 7),
                TranscriptText: GetNullableString(reader, 8),
                Relevance: GetNullableString(reader, 9),
                Project: GetNullableString(reader, 10),
                Topic: GetNullableString(reader, 11),
                TagsJson: GetNullableString(reader, 12),
                SourceUrl: GetNullableString(reader, 13),
                ReferrerUrl: GetNullableString(reader, 14),
                AgentSummary: GetNullableString(reader, 15));
        }
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long? GetNullableInt64(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
