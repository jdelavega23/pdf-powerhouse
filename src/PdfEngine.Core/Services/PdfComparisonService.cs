using Docnet.Core;
using Docnet.Core.Models;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PdfEngine.Core.Services;

public class PdfComparisonService : IPdfComparisonService
{
    public Task<ComparisonResult> ComparePagesAsync(byte[] pdfDocumentA, byte[] pdfDocumentB, ComparisonOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentA);
        ArgumentNullException.ThrowIfNull(pdfDocumentB);
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scaling = options.Dpi / 72.0;
            var dimensions = new PageDimensions(scaling);

            using var docReaderA = DocLib.Instance.GetDocReader(pdfDocumentA, dimensions);
            using var docReaderB = DocLib.Instance.GetDocReader(pdfDocumentB, dimensions);

            int totalPagesA = docReaderA.GetPageCount();
            int totalPagesB = docReaderB.GetPageCount();

            if (options.PageIndex < 0 || options.PageIndex >= totalPagesA)
            {
                throw new ArgumentOutOfRangeException(nameof(options.PageIndex), $"Índice de página {options.PageIndex} fuera de rango para Documento A ({totalPagesA} páginas).");
            }
            if (options.PageIndex >= totalPagesB)
            {
                throw new ArgumentOutOfRangeException(nameof(options.PageIndex), $"Índice de página {options.PageIndex} fuera de rango para Documento B ({totalPagesB} páginas).");
            }

            using var pageReaderA = docReaderA.GetPageReader(options.PageIndex);
            using var pageReaderB = docReaderB.GetPageReader(options.PageIndex);

            int wA = pageReaderA.GetPageWidth();
            int hA = pageReaderA.GetPageHeight();
            var rawBgraA = pageReaderA.GetImage();

            int wB = pageReaderB.GetPageWidth();
            int hB = pageReaderB.GetPageHeight();
            var rawBgraB = pageReaderB.GetImage();

            using var imgA = Image.LoadPixelData<Bgra32>(rawBgraA, wA, hA);
            using var imgB = Image.LoadPixelData<Bgra32>(rawBgraB, wB, hB);

            // Normalize dimensions if they differ slightly
            if (imgB.Width != imgA.Width || imgB.Height != imgA.Height)
            {
                imgB.Mutate(ctx => ctx.Resize(imgA.Width, imgA.Height));
            }

            int width = imgA.Width;
            int height = imgA.Height;
            using var diffImg = new Image<Rgba32>(width, height);

            int changedPixels = 0;
            double threshold = Math.Clamp(options.Sensitivity, 0.01, 0.50);

            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (int x = 0; x < width; x++)
                {
                    var pA = imgA[x, y];
                    var pB = imgB[x, y];

                    double deltaR = Math.Abs(pA.R - pB.R);
                    double deltaG = Math.Abs(pA.G - pB.G);
                    double deltaB = Math.Abs(pA.B - pB.B);
                    double diff = (deltaR + deltaG + deltaB) / (3.0 * 255.0);

                    if (diff > threshold)
                    {
                        changedPixels++;

                        double lumA = 0.299 * pA.R + 0.587 * pA.G + 0.114 * pA.B;
                        double lumB = 0.299 * pB.R + 0.587 * pB.G + 0.114 * pB.B;

                        if (lumA < lumB - 20)
                        {
                            // Content removed in Doc B (Highlight vivid Red)
                            diffImg[x, y] = new Rgba32(239, 68, 68, 235);
                        }
                        else if (lumB < lumA - 20)
                        {
                            // Content added in Doc B (Highlight vivid Green)
                            diffImg[x, y] = new Rgba32(34, 197, 94, 235);
                        }
                        else
                        {
                            // Color change / shift (Highlight Amber)
                            diffImg[x, y] = new Rgba32(245, 158, 11, 235);
                        }
                    }
                    else
                    {
                        // Match: subtle desaturated background
                        byte gray = (byte)(0.299 * pA.R + 0.587 * pA.G + 0.114 * pA.B);
                        // Dim slightly for high visual contrast with diff markers
                        byte dimmed = (byte)(gray * 0.85);
                        diffImg[x, y] = new Rgba32(dimmed, dimmed, dimmed, 180);
                    }
                }
            }

            // Export images to Base64
            using var msA = new MemoryStream();
            imgA.SaveAsPng(msA);
            string base64A = $"data:image/png;base64,{Convert.ToBase64String(msA.ToArray())}";

            using var msB = new MemoryStream();
            imgB.SaveAsPng(msB);
            string base64B = $"data:image/png;base64,{Convert.ToBase64String(msB.ToArray())}";

            using var msDiff = new MemoryStream();
            diffImg.SaveAsPng(msDiff);
            string base64Diff = $"data:image/png;base64,{Convert.ToBase64String(msDiff.ToArray())}";

            double totalPixels = width * height;
            double diffPct = totalPixels > 0 ? Math.Round((changedPixels / totalPixels) * 100.0, 2) : 0.0;

            return new ComparisonResult
            {
                PageIndex = options.PageIndex,
                TotalPagesDocA = totalPagesA,
                TotalPagesDocB = totalPagesB,
                DifferencePercentage = diffPct,
                PixelsChanged = changedPixels,
                ImageBase64A = base64A,
                ImageBase64B = base64B,
                DiffImageBase64 = base64Diff
            };
        }, cancellationToken);
    }
}
