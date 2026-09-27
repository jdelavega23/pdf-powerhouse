using System.Globalization;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfRedactionService : IPdfRedactionService
{
    private readonly IPdfInspectionService _inspectionService;

    public PdfRedactionService(IPdfInspectionService inspectionService)
    {
        _inspectionService = inspectionService ?? throw new ArgumentNullException(nameof(inspectionService));
    }

    public async Task<byte[]> RedactPdfAsync(byte[] pdfDocument, DeepRedactionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Areas == null || options.Areas.Count == 0)
        {
            return pdfDocument;
        }

        // 1. Apply redaction blackouts and text overlays on the PDF document
        byte[] intermediatePdf;
        var redactedPageIndices = new HashSet<int>();

        using (var inStream = new MemoryStream(pdfDocument))
        using (var document = PdfReader.Open(inStream, PdfDocumentOpenMode.Modify))
        {
            var areasByPage = options.Areas.GroupBy(a => a.PageNumber);

            foreach (var group in areasByPage)
            {
                int pageNum = group.Key;
                if (pageNum < 1 || pageNum > document.PageCount) continue;

                int pageIndex = pageNum - 1;
                redactedPageIndices.Add(pageIndex);
                var page = document.Pages[pageIndex];

                using var gfx = XGraphics.FromPdfPage(page);

                foreach (var area in group)
                {
                    if (area.Width <= 0 || area.Height <= 0) continue;

                    var fillColor = ParseHexColor(area.FillColorHex, XColors.Black);
                    var fillBrush = new XSolidBrush(fillColor);

                    gfx.DrawRectangle(fillBrush, area.X, area.Y, area.Width, area.Height);

                    if (!string.IsNullOrWhiteSpace(area.OverlayText))
                    {
                        double fontSize = Math.Min(12, Math.Max(7, area.Height * 0.55));
                        var font = new XFont("Arial", fontSize, XFontStyle.Bold);
                        var textColor = ParseHexColor(area.TextColorHex, XColors.White);
                        var textBrush = new XSolidBrush(textColor);

                        var rect = new XRect(area.X, area.Y, area.Width, area.Height);
                        gfx.DrawString(area.OverlayText, font, textBrush, rect, XStringFormats.Center);
                    }
                }
            }

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            intermediatePdf = outStream.ToArray();
        }

        // 2. If PermanentFlatten is true, rasterize only the redacted pages with Google PDFium
        // This permanently destroys underlying text/vector streams, making reverse engineering 100% impossible.
        if (!options.PermanentFlatten || redactedPageIndices.Count == 0)
        {
            return intermediatePdf;
        }

        using var finalDoc = new PdfDocument();
        using var interStream = new MemoryStream(intermediatePdf);
        using var interDoc = PdfReader.Open(interStream, PdfDocumentOpenMode.Import);

        for (int i = 0; i < interDoc.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (redactedPageIndices.Contains(i))
            {
                // Render redacted page to high-res PNG (200 DPI for ultra crisp print quality)
                var pagePng = await _inspectionService.RenderPageToPngAsync(intermediatePdf, i, 200, null, cancellationToken);

                var sourcePage = interDoc.Pages[i];
                var newPage = finalDoc.AddPage();
                newPage.Width = sourcePage.Width;
                newPage.Height = sourcePage.Height;
                newPage.Orientation = sourcePage.Orientation;

                using var xImage = XImage.FromStream(() => new MemoryStream(pagePng));
                using var gfx = XGraphics.FromPdfPage(newPage);
                gfx.DrawImage(xImage, 0, 0, newPage.Width.Point, newPage.Height.Point);
            }
            else
            {
                // Keep untouched pages as pure native vector pages
                finalDoc.AddPage(interDoc.Pages[i]);
            }
        }

        using var finalStream = new MemoryStream();
        finalDoc.Save(finalStream, false);
        return finalStream.ToArray();
    }

    private static XColor ParseHexColor(string? hex, XColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        hex = hex.Trim().TrimStart('#');

        try
        {
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return XColor.FromArgb(r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                return XColor.FromArgb(a, r, g, b);
            }
        }
        catch
        {
            // Fallback on parse failure
        }

        return fallback;
    }
}
