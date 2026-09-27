namespace PdfEngine.Core.Models;

public class PdfRepairResult
{
    public bool Success { get; set; }
    public int RecoveredPageCount { get; set; }
    public long OriginalSizeBytes { get; set; }
    public long RepairedSizeBytes { get; set; }
    public string DiagnosticMessage { get; set; } = string.Empty;
    public byte[] RepairedPdf { get; set; } = Array.Empty<byte>();
}
