namespace PdfEngine.Core.Models;

public record TextAnnotationOptions
{
    public int PageNumber { get; init; } = 1;
    public double PositionX { get; init; } = 100;
    public double PositionY { get; init; } = 100;
    public string Text { get; init; } = string.Empty;
    public double FontSize { get; init; } = 14;
    public string FontColorHex { get; init; } = "#000000";
    public string? BackgroundColorHex { get; init; } = null; // optional highlighter or background fill
    public bool IsBold { get; init; } = false;
    public bool IsItalic { get; init; } = false;
}

public record ImageAnnotationOptions
{
    public int PageNumber { get; init; } = 1;
    public double PositionX { get; init; } = 100;
    public double PositionY { get; init; } = 100;
    public double Width { get; init; } = 150;
    public double Height { get; init; } = 60;
    public byte[] ImageBytes { get; init; } = Array.Empty<byte>();
}

public record RedactionOptions
{
    public int PageNumber { get; init; } = 1;
    public double PositionX { get; init; } = 50;
    public double PositionY { get; init; } = 50;
    public double Width { get; init; } = 200;
    public double Height { get; init; } = 30;
    public string FillColorHex { get; init; } = "#000000"; // Solid black censor box
    public string? OverlayText { get; init; } = "[CENSURADO]";
}

public record BatchEditItem
{
    public string Type { get; init; } = "text"; // "text", "image", "redaction", "highlight", "drawing"
    public int PageNumber { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public string Text { get; init; } = string.Empty;
    public double FontSize { get; init; } = 14;
    public string ColorHex { get; init; } = "#000000";
    public string? BackgroundColorHex { get; init; }
    public bool IsBold { get; init; }
    public bool IsItalic { get; init; }
    public string? ImageBase64 { get; init; }

    // Existing text editing / in-place replacement
    public bool IsReplacement { get; init; } = false;
    public double? OriginalX { get; init; }
    public double? OriginalY { get; init; }
    public double? OriginalWidth { get; init; }
    public double? OriginalHeight { get; init; }
}

public record PdfTextBlock
{
    public int PageNumber { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public string Text { get; init; } = string.Empty;
    public double FontSize { get; init; } = 12;
    public string FontName { get; init; } = "Arial";
    public string ColorHex { get; init; } = "#000000";
}
