namespace PdfEngine.Core.Models;

public class ComparisonOptions
{
    public int PageIndex { get; set; } = 0;
    public int Dpi { get; set; } = 150;
    public double Sensitivity { get; set; } = 0.10;
}

public class ComparisonResult
{
    public int PageIndex { get; set; }
    public int TotalPagesDocA { get; set; }
    public int TotalPagesDocB { get; set; }
    public double DifferencePercentage { get; set; }
    public int PixelsChanged { get; set; }
    public string ImageBase64A { get; set; } = string.Empty;
    public string ImageBase64B { get; set; } = string.Empty;
    public string DiffImageBase64 { get; set; } = string.Empty;
    public bool HasDifferences => PixelsChanged > 0;
}
