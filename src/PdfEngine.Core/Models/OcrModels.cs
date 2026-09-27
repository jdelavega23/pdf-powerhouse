namespace PdfEngine.Core.Models;

public record OcrResult
{
    public string Text { get; init; } = string.Empty;
    public float MeanConfidence { get; init; }
    public int WordCount { get; init; }
    public long ProcessingTimeMs { get; init; }
    public byte[]? SearchablePdfBytes { get; init; }
}

public record OcrOptions
{
    public string Language { get; init; } = "spa+eng"; // Spanish + English default
    public bool GenerateSearchablePdf { get; init; } = true;
    public int Dpi { get; init; } = 300;
}
