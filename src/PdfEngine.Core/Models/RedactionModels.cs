namespace PdfEngine.Core.Models;

public class DeepRedactionArea
{
    public int PageNumber { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string FillColorHex { get; set; } = "#000000";
    public string? OverlayText { get; set; } = "CENSURADO";
    public string TextColorHex { get; set; } = "#FFFFFF";
}

public class DeepRedactionOptions
{
    public List<DeepRedactionArea> Areas { get; set; } = new();
    public bool PermanentFlatten { get; set; } = true;
}
