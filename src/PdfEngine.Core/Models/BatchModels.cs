namespace PdfEngine.Core.Models;

public enum BatchOperationType
{
    Compress,
    Watermark,
    Numbering,
    Repair,
    FlattenForms,
    Rotate
}

public class BatchFileResult
{
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long OriginalSizeBytes { get; set; }
    public long ProcessedSizeBytes { get; set; }
    public byte[] ProcessedBytes { get; set; } = Array.Empty<byte>();
}

public class BatchProcessResult
{
    public int TotalFiles { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    public List<BatchFileResult> Results { get; set; } = new();
    public byte[] ZipArchiveBytes { get; set; } = Array.Empty<byte>();
}

public class BatchOperationOptions
{
    public BatchOperationType Operation { get; set; } = BatchOperationType.Compress;
    
    // Watermark options
    public string? WatermarkText { get; set; }
    public string? WatermarkColorHex { get; set; } = "#FF0000";
    public double? WatermarkOpacity { get; set; } = 0.3;
    public double? WatermarkFontSize { get; set; } = 48;
    public double? WatermarkRotation { get; set; } = -45;

    // Compression options
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Medium;

    // Numbering options
    public PageNumberPosition NumberPosition { get; set; } = PageNumberPosition.BottomRight;
    public string? NumberFormat { get; set; } = "Página {n} de {total}";

    // Rotation options
    public int RotateDegrees { get; set; } = 90;
}
