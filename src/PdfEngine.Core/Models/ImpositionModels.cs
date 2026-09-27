namespace PdfEngine.Core.Models;

public enum ImpositionMode
{
    TwoUp,
    FourUp,
    Booklet
}

public class ImpositionOptions
{
    public ImpositionMode Mode { get; set; } = ImpositionMode.TwoUp;
    public string TargetSheetSize { get; set; } = "A4";
    public bool DrawBorders { get; set; } = true;
    public double MarginPoints { get; set; } = 15.0;
}
