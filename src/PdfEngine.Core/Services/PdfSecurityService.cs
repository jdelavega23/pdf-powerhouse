using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.Security;

namespace PdfEngine.Core.Services;

public class PdfSecurityService : IPdfSecurityService
{
    public Task<byte[]> ProtectPdfAsync(byte[] pdfDocument, PdfProtectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfDocument);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            var security = document.SecuritySettings;
            if (!string.IsNullOrEmpty(options.UserPassword))
            {
                security.UserPassword = options.UserPassword;
            }

            if (!string.IsNullOrEmpty(options.OwnerPassword))
            {
                security.OwnerPassword = options.OwnerPassword;
            }

            security.PermitPrint = options.PermitPrint;
            security.PermitExtractContent = options.PermitCopyContent;
            security.PermitModifyDocument = options.PermitModifyDocument;
            security.PermitAnnotations = options.PermitAnnotations;
            security.PermitFormsFill = options.PermitFormsFill;

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> UnlockPdfAsync(byte[] pdfDocument, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfDocument);
            // Open with password
            using var inputDoc = PdfReader.Open(inputStream, password, PdfDocumentOpenMode.Import);

            // Re-export into an unencrypted fresh document
            using var cleanDoc = new PdfDocument();
            for (int i = 0; i < inputDoc.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cleanDoc.AddPage(inputDoc.Pages[i]);
            }

            using var outStream = new MemoryStream();
            cleanDoc.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }
}
