namespace PdfEngine.Core.Models;

public record WatermarkOptions
{
    public string Text { get; init; } = "CONFIDENCIAL";
    public double FontSize { get; init; } = 48;
    public string FontColorHex { get; init; } = "#808080"; // Gray default
    public double Opacity { get; init; } = 0.3; // 0.0 to 1.0
    public double RotationDegrees { get; init; } = -45;
    public bool BehindContent { get; init; } = false;
    public IReadOnlyList<int>? TargetPages { get; init; } = null; // null means all pages (1-based)
}
