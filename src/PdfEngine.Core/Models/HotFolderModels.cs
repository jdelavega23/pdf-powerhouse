namespace PdfEngine.Core.Models;

public enum HotFolderAction
{
    ConvertToPdfA,
    Compress,
    Repair,
    FlattenForms,
    OfficeToPdf
}

public sealed record HotFolderConfiguration
{
    public bool Enabled { get; init; } = true;
    public string InputPath { get; init; } = "./hotfolders/input";
    public string ProcessedPath { get; init; } = "./hotfolders/processed";
    public string FailedPath { get; init; } = "./hotfolders/failed";
    public HotFolderAction Action { get; init; } = HotFolderAction.ConvertToPdfA;
    public int PollingIntervalSeconds { get; init; } = 3;
}

public sealed record HotFolderLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Level { get; init; } = "INFO";
    public string Message { get; init; } = string.Empty;
}

public sealed record HotFolderStatus
{
    public bool IsActive { get; init; }
    public HotFolderConfiguration Configuration { get; init; } = new();
    public int ProcessedCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<HotFolderLogEntry> RecentLogs { get; init; } = Array.Empty<HotFolderLogEntry>();
}
