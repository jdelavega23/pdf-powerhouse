using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfAService
{
    Task<PdfAValidationResult> ValidatePdfAAsync(byte[] pdfDocument, CancellationToken cancellationToken = default);
    Task<PdfAConversionResult> ConvertToPdfAAsync(byte[] pdfDocument, PdfAProfile targetProfile = PdfAProfile.PdfA1b, CancellationToken cancellationToken = default);
}
