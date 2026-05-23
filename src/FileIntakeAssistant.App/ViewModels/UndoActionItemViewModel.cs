using FileIntakeAssistant.Core.Models;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class UndoActionItemViewModel
{
    public UndoActionItemViewModel(UndoActionRecord record)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
    }

    public UndoActionRecord Record { get; }

    public long Id => Record.Id ?? 0;

    public long TargetFileRecordId => Record.TargetFileRecordId;

    public string UndoType => Record.UndoType;

    public string OriginalPath => Record.OriginalPath;

    public string ResultingPath => Record.ResultingPath;

    public string Status => Record.Status;

    public DateTimeOffset CreatedAt => Record.CreatedAt;

    public DateTimeOffset? PerformedAt => Record.PerformedAt;
}
