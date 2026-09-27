namespace PdfEngine.Core.Models;

public record PdfProtectionOptions
{
    public string UserPassword { get; init; } = string.Empty;
    public string OwnerPassword { get; init; } = string.Empty;
    public bool PermitPrint { get; init; } = true;
    public bool PermitCopyContent { get; init; } = true;
    public bool PermitModifyDocument { get; init; } = false;
    public bool PermitAnnotations { get; init; } = true;
    public bool PermitFormsFill { get; init; } = true;
    public bool PermitAccessibility { get; init; } = true;
}
