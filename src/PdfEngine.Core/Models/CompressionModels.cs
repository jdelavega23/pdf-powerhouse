namespace PdfEngine.Core.Models;

public enum CompressionLevel
{
    Low,      // Alta fidelidad, reempaqueta streams y optimiza objetos
    Medium,   // Equilibrio recomendado (~40-60% reducción)
    Extreme   // Máxima compresión para sedes electrónicas y email
}

public record CompressionOptions
{
    public CompressionLevel Level { get; init; } = CompressionLevel.Medium;
}

public record CompressionResult
{
    public byte[] CompressedPdf { get; init; } = Array.Empty<byte>();
    public long OriginalSizeBytes { get; init; }
    public long CompressedSizeBytes { get; init; }
    public double SavedPercentage => OriginalSizeBytes > 0 
        ? Math.Max(0, Math.Round((1.0 - ((double)CompressedSizeBytes / OriginalSizeBytes)) * 100, 1))
        : 0;
}
