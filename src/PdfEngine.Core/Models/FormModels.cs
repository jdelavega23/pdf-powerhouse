namespace PdfEngine.Core.Models;

public class PdfFormField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
    public List<string> Options { get; set; } = new();
}

public class FormFlattenResult
{
    public int FlattenedFieldsCount { get; set; }
    public List<PdfFormField> InspectedFields { get; set; } = new();
    public byte[] FlattenedPdf { get; set; } = Array.Empty<byte>();
}
