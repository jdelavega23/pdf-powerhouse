namespace PdfEngine.Core.Models;

public enum FormFieldType
{
    Text,
    CheckBox,
    ComboBox,
    Signature
}

public class FormFieldDefinition
{
    public int PageNumber { get; set; } = 1;
    public string FieldName { get; set; } = "campo_texto";
    public FormFieldType Type { get; set; } = FormFieldType.Text;
    public double X { get; set; } = 50;
    public double Y { get; set; } = 100;
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 24;
    public string DefaultValue { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new(); // For ComboBox / List
    public bool IsRequired { get; set; }
    public bool IsReadOnly { get; set; }
}

public class FormBuildRequest
{
    public byte[]? BasePdf { get; set; } // If null, creates a blank branded template document
    public string DocumentTitle { get; set; } = "Formulario Interactivo Rellenable";
    public string Subtitle { get; set; } = "Documento oficial con campos interactivos";
    public List<FormFieldDefinition> Fields { get; set; } = new();
}

public class FormBuildResult
{
    public bool Success { get; set; }
    public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
    public int FieldsCreated { get; set; }
    public List<string> FieldNames { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
