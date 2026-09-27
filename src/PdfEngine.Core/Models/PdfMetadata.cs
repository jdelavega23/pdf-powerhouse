namespace PdfEngine.Core.Models;

public record PdfMetadata
{
    public string Title { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Keywords { get; init; } = string.Empty;
    public string Creator { get; init; } = string.Empty;
    public string Producer { get; init; } = string.Empty;
    public int PageCount { get; init; }
    public DateTime? CreationDate { get; init; }
    public DateTime? ModificationDate { get; init; }
    public long FileSizeBytes { get; init; }
    public bool IsEncrypted { get; init; }
    public IReadOnlyList<PdfPageInfo> Pages { get; init; } = Array.Empty<PdfPageInfo>();
}
