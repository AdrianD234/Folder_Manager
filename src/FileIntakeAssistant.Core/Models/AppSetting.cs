namespace FileIntakeAssistant.Core.Models;

public sealed record AppSetting(
    string Key,
    string ValueJson,
    DateTimeOffset UpdatedAt);
