using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfInvoiceService
{
    Task<InvoiceGenerationResult> GenerateInvoiceAsync(InvoiceGenerationRequest request, CancellationToken cancellationToken = default);
    string GenerateFacturXXml(InvoiceGenerationRequest request);
    byte[] GenerateSepaPaymentQrPng(InvoiceParty issuer, string invoiceNumber, decimal amountEur, string remittanceInfo);
}
