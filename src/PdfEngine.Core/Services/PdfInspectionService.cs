using Docnet.Core;
using Docnet.Core.Models;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PdfEngine.Core.Services;

public class PdfInspectionService : IPdfInspectionService
{
    public Task<PdfMetadata> InspectMetadataAsync(byte[] pdfDocument, string? password = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run(() =>
        {
            using var stream = new MemoryStream(pdfDocument);
            
            // Try opening with password if provided, or information-only
            using var document = string.IsNullOrEmpty(password)
                ? PdfReader.Open(stream, PdfDocumentOpenMode.InformationOnly)
                : PdfReader.Open(stream, password, PdfDocumentOpenMode.InformationOnly);

            var pages = new List<PdfPageInfo>(document.PageCount);
            for (int i = 0; i < document.PageCount; i++)
            {
                var page = document.Pages[i];
                pages.Add(new PdfPageInfo
                {
                    PageIndex = i,
                    Width = page.Width.Point,
                    Height = page.Height.Point,
                    Rotation = page.Rotate
                });
            }

            return new PdfMetadata
            {
                Title = document.Info.Title ?? string.Empty,
                Author = document.Info.Author ?? string.Empty,
                Subject = document.Info.Subject ?? string.Empty,
                Keywords = document.Info.Keywords ?? string.Empty,
                Creator = document.Info.Creator ?? string.Empty,
                Producer = document.Info.Producer ?? string.Empty,
                PageCount = document.PageCount,
                CreationDate = document.Info.CreationDate != DateTime.MinValue ? document.Info.CreationDate : null,
                ModificationDate = document.Info.ModificationDate != DateTime.MinValue ? document.Info.ModificationDate : null,
                FileSizeBytes = pdfDocument.Length,
                IsEncrypted = document.SecuritySettings != null && document.SecuritySettings.DocumentSecurityLevel != PdfSharpCore.Pdf.Security.PdfDocumentSecurityLevel.None,
                Pages = pages
            };
        }, cancellationToken);
    }

    public Task<byte[]> RenderPageToPngAsync(byte[] pdfDocument, int pageIndex, int dpi = 150, string? password = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Using Google PDFium native engine via Docnet
            var scaling = dpi / 72.0; // 72 DPI is standard 100% PDF point scale
            var dimensions = new PageDimensions(scaling);

            using var docReader = string.IsNullOrEmpty(password)
                ? DocLib.Instance.GetDocReader(pdfDocument, dimensions)
                : DocLib.Instance.GetDocReader(pdfDocument, password, dimensions);

            if (pageIndex < 0 || pageIndex >= docReader.GetPageCount())
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page index {pageIndex} is out of bounds (0 to {docReader.GetPageCount() - 1}).");
            }

            using var pageReader = docReader.GetPageReader(pageIndex);
            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();
            var rawBgra = pageReader.GetImage(); // Returns BGRA byte array from PDFium

            // Convert BGRA bytes to PNG stream using ImageSharp
            using var image = Image.LoadPixelData<Bgra32>(rawBgra, width, height);
            using var outStream = new MemoryStream();
            image.SaveAsPng(outStream);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<string> ExtractPageTextAsync(byte[] pdfDocument, int pageIndex, string? password = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var docReader = string.IsNullOrEmpty(password)
                ? DocLib.Instance.GetDocReader(pdfDocument, new PageDimensions(1.0))
                : DocLib.Instance.GetDocReader(pdfDocument, password, new PageDimensions(1.0));

            if (pageIndex < 0 || pageIndex >= docReader.GetPageCount())
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page index {pageIndex} is out of bounds (0 to {docReader.GetPageCount() - 1}).");
            }

            using var pageReader = docReader.GetPageReader(pageIndex);
            return pageReader.GetText() ?? string.Empty;
        }, cancellationToken);
    }
}
