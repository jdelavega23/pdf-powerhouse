namespace PdfEngine.Core.Models;

public enum OfficeDocumentType
{
    AutoDetect,
    Word,
    Excel,
    PowerPoint,
    Text,
    Csv,
    Html,
    Rtf
}

public sealed record OfficeConversionOptions
{
    public OfficeDocumentType DocumentType { get; init; } = OfficeDocumentType.AutoDetect;
    public string Title { get; init; } = "Documento Convertido";
    public double FontSize { get; init; } = 10.0;
    public string Delimiter { get; init; } = ",";
}

public sealed record OfficeConversionResult
{
    public bool Success { get; init; }
    public string SourceFileName { get; init; } = string.Empty;
    public OfficeDocumentType DetectedType { get; init; }
    public byte[] PdfBytes { get; init; } = Array.Empty<byte>();
    public int PageCount { get; init; }
    public string EngineUsed { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}
