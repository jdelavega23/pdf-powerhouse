using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfConverterService
{
    Task<byte[]> ImagesToPdfAsync(IReadOnlyList<(string FileName, byte[] Bytes)> images, ImagesToPdfOptions options, CancellationToken cancellationToken = default);
    Task<byte[]> PdfToImagesZipAsync(byte[] pdfDocument, int dpi = 150, CancellationToken cancellationToken = default);
}
