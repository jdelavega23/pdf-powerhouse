using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfPageNumberService : IPdfPageNumberService
{
    public Task<byte[]> AddPageNumbersAsync(byte[] pdfDocument, PageNumberOptions options, CancellationToken cancellationToken = default)
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

            var font = new XFont("Arial", options.FontSize > 0 ? options.FontSize : 10, XFontStyle.Regular);
            var (r, g, b) = ParseHexColor(options.FontColorHex);
            var brush = new XSolidBrush(XColor.FromArgb(r, g, b));

            int totalPages = document.PageCount;
            int startIdx = Math.Clamp(options.StartPage - 1, 0, totalPages - 1);
            int visualNum = options.StartingNumber;

            for (int i = startIdx; i < totalPages; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = document.Pages[i];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                var label = options.Format
                    .Replace("{n}", visualNum.ToString())
                    .Replace("{total}", totalPages.ToString());

                var textSize = gfx.MeasureString(label, font);
                double margin = Math.Max(10, options.MarginPt);
                double pw = page.Width.Point;
                double ph = page.Height.Point;

                double x;
                double y;

                switch (options.Position)
                {
                    case PageNumberPosition.BottomLeft:
                        x = margin;
                        y = ph - margin;
                        break;
                    case PageNumberPosition.BottomCenter:
                        x = (pw - textSize.Width) / 2;
                        y = ph - margin;
                        break;
                    case PageNumberPosition.TopLeft:
                        x = margin;
                        y = margin + textSize.Height;
                        break;
                    case PageNumberPosition.TopCenter:
                        x = (pw - textSize.Width) / 2;
                        y = margin + textSize.Height;
                        break;
                    case PageNumberPosition.TopRight:
                        x = pw - margin - textSize.Width;
                        y = margin + textSize.Height;
                        break;
                    case PageNumberPosition.BottomRight:
                    default:
                        x = pw - margin - textSize.Width;
                        y = ph - margin;
                        break;
                }

                gfx.DrawString(label, font, brush, x, y);
                visualNum++;
            }

            using var outputStream = new MemoryStream();
            document.Save(outputStream, false);
            return outputStream.ToArray();
        }, cancellationToken);
    }

    private static (byte R, byte G, byte B) ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return (85, 85, 85);
        hex = hex.TrimStart('#');

        if (hex.Length == 6 &&
            byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return (r, g, b);
        }

        return (85, 85, 85);
    }
}
