using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfTransformService : IPdfTransformService
{
    public Task<byte[]> RotatePagesAsync(byte[] pdfDocument, int angleDegrees, IEnumerable<int>? targetPageNumbers = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfDocument);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            var targetPagesSet = targetPageNumbers != null ? new HashSet<int>(targetPageNumbers) : null;

            for (int i = 0; i < document.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int pageNum = i + 1;

                if (targetPagesSet == null || targetPagesSet.Contains(pageNum))
                {
                    var page = document.Pages[i];
                    int newAngle = (page.Rotate + angleDegrees) % 360;
                    if (newAngle < 0) newAngle += 360;
                    page.Rotate = newAngle;
                }
            }

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> ApplyWatermarkAsync(byte[] pdfDocument, WatermarkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfDocument);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            var targetPagesSet = options.TargetPages != null ? new HashSet<int>(options.TargetPages) : null;

            var (r, g, b) = ParseHexColor(options.FontColorHex);
            byte alpha = (byte)Math.Clamp((int)(options.Opacity * 255), 0, 255);
            var brush = new XSolidBrush(XColor.FromArgb(alpha, r, g, b));
            var font = new XFont("Arial", options.FontSize, XFontStyle.Bold);

            for (int i = 0; i < document.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int pageNum = i + 1;

                if (targetPagesSet == null || targetPagesSet.Contains(pageNum))
                {
                    var page = document.Pages[i];
                    var mode = options.BehindContent ? XGraphicsPdfPageOptions.Prepend : XGraphicsPdfPageOptions.Append;

                    using var gfx = XGraphics.FromPdfPage(page, mode);
                    gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
                    gfx.RotateTransform(options.RotationDegrees);
                    gfx.DrawString(options.Text, font, brush, 0, 0, XStringFormats.Center);
                }
            }

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> ReorderPagesAsync(byte[] pdfDocument, IReadOnlyList<int> newPageOrder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(newPageOrder);

        if (newPageOrder.Count == 0)
        {
            throw new ArgumentException("Page order list cannot be empty.", nameof(newPageOrder));
        }

        return Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfDocument);
            using var inputDoc = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);

            using var outputDoc = new PdfDocument();

            foreach (var pageNum in newPageOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int pageIndex = pageNum - 1;

                if (pageIndex < 0 || pageIndex >= inputDoc.PageCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(newPageOrder), $"Page {pageNum} is out of document bounds (1 to {inputDoc.PageCount}).");
                }

                outputDoc.AddPage(inputDoc.Pages[pageIndex]);
            }

            using var outStream = new MemoryStream();
            outputDoc.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    private static (byte R, byte G, byte B) ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return (128, 128, 128);
        hex = hex.TrimStart('#');

        if (hex.Length == 6 &&
            byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return (r, g, b);
        }

        return (128, 128, 128);
    }
}
