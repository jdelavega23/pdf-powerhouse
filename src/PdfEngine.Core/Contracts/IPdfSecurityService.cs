using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfSecurityService
{
    Task<byte[]> ProtectPdfAsync(byte[] pdfDocument, PdfProtectionOptions options, CancellationToken cancellationToken = default);
    Task<byte[]> UnlockPdfAsync(byte[] pdfDocument, string password, CancellationToken cancellationToken = default);
}
