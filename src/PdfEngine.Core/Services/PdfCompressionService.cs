using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfCompressionService : IPdfCompressionService
{
    public Task<CompressionResult> CompressPdfAsync(byte[] pdfDocument, CompressionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var inputStream = new MemoryStream(pdfDocument);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            if (document.PageCount == 0)
                throw new InvalidOperationException("El documento no contiene páginas.");

            // Configure stream compression
            document.Options.NoCompression = false;
            document.Options.CompressContentStreams = true;
            document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;

            // Remove unneeded metadata in extreme compression mode
            if (options.Level == CompressionLevel.Extreme)
            {
                document.Info.Title = string.Empty;
                document.Info.Author = string.Empty;
                document.Info.Keywords = string.Empty;
                document.Info.Subject = string.Empty;
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            var compressedBytes = outputStream.ToArray();

            // If compressed result is accidentally larger (e.g. already compressed input), keep the smaller one
            if (compressedBytes.Length >= pdfDocument.Length && options.Level != CompressionLevel.Extreme)
            {
                // Return original with 0% saved
                return new CompressionResult
                {
                    CompressedPdf = pdfDocument,
                    OriginalSizeBytes = pdfDocument.Length,
                    CompressedSizeBytes = pdfDocument.Length
                };
            }

            return new CompressionResult
            {
                CompressedPdf = compressedBytes,
                OriginalSizeBytes = pdfDocument.Length,
                CompressedSizeBytes = compressedBytes.Length
            };
        }, cancellationToken);
    }
}
