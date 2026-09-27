namespace PdfEngine.Core.Models;

public class InvoiceParty
{
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty; // NIF / CIF / VAT
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "España";
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Bic { get; set; } = string.Empty;
}

public class InvoiceItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1.0m;
    public decimal UnitPrice { get; set; } = 0.0m;
    public decimal TaxRatePercent { get; set; } = 21.0m; // 21%, 10%, 4%, 0%
    public decimal DiscountPercent { get; set; } = 0.0m;

    public decimal Subtotal => Math.Round(Quantity * UnitPrice * (1.0m - (DiscountPercent / 100.0m)), 2);
    public decimal TaxAmount => Math.Round(Subtotal * (TaxRatePercent / 100.0m), 2);
    public decimal Total => Subtotal + TaxAmount;
}

public class InvoiceTaxBreakdown
{
    public decimal TaxRatePercent { get; set; }
    public decimal TaxableBase { get; set; }
    public decimal TaxAmount { get; set; }
}

public class InvoiceGenerationRequest
{
    public string InvoiceNumber { get; set; } = "FAC-2026-0001";
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; } = DateTime.UtcNow.AddDays(30);
    public string Currency { get; set; } = "EUR";
    public string CurrencySymbol { get; set; } = "€";

    public InvoiceParty Issuer { get; set; } = new();
    public InvoiceParty Customer { get; set; } = new();

    public List<InvoiceItem> Items { get; set; } = new();

    public decimal IrpfRatePercent { get; set; } = 0.0m; // 15% or 7% for Spanish autónomos
    public string PaymentMethod { get; set; } = "Transferencia Bancaria";
    public string Notes { get; set; } = "Gracias por su confianza.";
    public string LegalNotice { get; set; } = "Régimen General. Factura emitida de conformidad con la Ley 18/2022 y RD 1619/2012.";

    public bool IncludeSepaQr { get; set; } = true;
    public bool EmbedFacturXXml { get; set; } = true;

    public string PrimaryColorHex { get; set; } = "#0f172a"; // Executive Navy
    public string AccentColorHex { get; set; } = "#0284c7"; // Cyan/Blue
    public byte[]? LogoBytes { get; set; }
}

public class InvoiceGenerationResult
{
    public bool Success { get; set; }
    public byte[] PdfBytes { get; set; } = Array.Empty<byte>();
    public string ElectronicInvoiceXml { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalIrpf { get; set; }
    public decimal TotalAmount { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public bool FacturXEmbedded { get; set; }
    public bool QrCodeGenerated { get; set; }
    public List<InvoiceTaxBreakdown> TaxBreakdown { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
