namespace PdfEngine.Core.Models;

public enum PdfAProfile
{
    PdfA1b,
    PdfA2b
}

public class PdfAValidationResult
{
    public bool IsCompliant { get; set; }
    public string DetectedProfile { get; set; } = "None";
    public List<string> ComplianceIssues { get; set; } = new();
    public List<string> ComplianceChecksPassed { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public class PdfAConversionResult
{
    public bool Success { get; set; }
    public PdfAProfile TargetProfile { get; set; } = PdfAProfile.PdfA1b;
    public string DiagnosticMessage { get; set; } = string.Empty;
    public byte[] ConvertedPdf { get; set; } = Array.Empty<byte>();
}
