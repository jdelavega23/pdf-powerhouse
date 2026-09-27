using System.IO.Compression;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace PdfEngine.Core.Services;

public class PdfConverterService : IPdfConverterService
{
    private readonly IPdfInspectionService _inspectionService;

    public PdfConverterService(IPdfInspectionService inspectionService)
    {
        _inspectionService = inspectionService ?? throw new ArgumentNullException(nameof(inspectionService));
    }

    public Task<byte[]> ImagesToPdfAsync(IReadOnlyList<(string FileName, byte[] Bytes)> images, ImagesToPdfOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(options);
        if (images.Count == 0)
            throw new ArgumentException("Debe proporcionar al menos una imagen para convertir a PDF.", nameof(images));

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = new PdfDocument();

            foreach (var (fileName, bytes) in images)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (bytes == null || bytes.Length == 0) continue;

                var page = document.AddPage();
                page.Size = PageSize.A4;

                using var xImage = XImage.FromStream(() => new MemoryStream(bytes));

                if (options.AutoOrientation && xImage.PixelWidth > xImage.PixelHeight)
                {
                    page.Orientation = PageOrientation.Landscape;
                }
                else
                {
                    page.Orientation = PageOrientation.Portrait;
                }

                using var gfx = XGraphics.FromPdfPage(page);

                double pageWidth = page.Width.Point;
                double pageHeight = page.Height.Point;
                double margin = options.FitMode == ImageFitMode.FillPage ? 0 : Math.Max(0, options.MarginPt);

                double availW = pageWidth - (margin * 2);
                double availH = pageHeight - (margin * 2);

                double imgW = xImage.PixelWidth;
                double imgH = xImage.PixelHeight;

                if (options.FitMode == ImageFitMode.FitPage)
                {
                    double ratioW = availW / imgW;
                    double ratioH = availH / imgH;
                    double ratio = Math.Min(ratioW, ratioH);

                    double destW = imgW * ratio;
                    double destH = imgH * ratio;
                    double destX = margin + ((availW - destW) / 2);
                    double destY = margin + ((availH - destH) / 2);

                    gfx.DrawImage(xImage, destX, destY, destW, destH);
                }
                else if (options.FitMode == ImageFitMode.FillPage)
                {
                    gfx.DrawImage(xImage, 0, 0, pageWidth, pageHeight);
                }
                else
                {
                    // Original Size (at 72 DPI points)
                    double destX = Math.Max(0, (pageWidth - imgW) / 2);
                    double destY = Math.Max(0, (pageHeight - imgH) / 2);
                    gfx.DrawImage(xImage, destX, destY, imgW, imgH);
                }
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            return outputStream.ToArray();
        }, cancellationToken);
    }

    public async Task<byte[]> PdfToImagesZipAsync(byte[] pdfDocument, int dpi = 150, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        var metadata = await _inspectionService.InspectMetadataAsync(pdfDocument, null, cancellationToken);
        if (metadata.PageCount == 0)
            throw new InvalidOperationException("El PDF no contiene páginas para convertir.");

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            for (int i = 0; i < metadata.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pngBytes = await _inspectionService.RenderPageToPngAsync(pdfDocument, i, dpi, null, cancellationToken);
                var entryName = $"pagina_{(i + 1):D2}.png";
                var entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);

                using var entryStream = entry.Open();
                await entryStream.WriteAsync(pngBytes, cancellationToken);
            }
        }

        return zipStream.ToArray();
    }
}
