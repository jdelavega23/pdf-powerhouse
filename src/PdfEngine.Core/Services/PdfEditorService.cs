using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfEditorService : IPdfEditorService
{
    public Task<byte[]> AddTextAnnotationAsync(byte[] pdfDocument, TextAnnotationOptions options, CancellationToken cancellationToken = default)
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

            int targetIndex = Math.Clamp(options.PageNumber - 1, 0, document.PageCount - 1);
            var page = document.Pages[targetIndex];

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var fontStyle = XFontStyle.Regular;
            if (options.IsBold && options.IsItalic) fontStyle = XFontStyle.BoldItalic;
            else if (options.IsBold) fontStyle = XFontStyle.Bold;
            else if (options.IsItalic) fontStyle = XFontStyle.Italic;

            var font = new XFont("Arial", options.FontSize, fontStyle);
            var (tr, tg, tb) = ParseHexColor(options.FontColorHex);
            var textBrush = new XSolidBrush(XColor.FromArgb(tr, tg, tb));

            // Optional background fill (e.g. highlighter / note box)
            if (!string.IsNullOrWhiteSpace(options.BackgroundColorHex))
            {
                var (br, bg, bb) = ParseHexColor(options.BackgroundColorHex);
                var bgBrush = new XSolidBrush(XColor.FromArgb(br, bg, bb));
                var size = gfx.MeasureString(options.Text, font);
                gfx.DrawRectangle(bgBrush, options.PositionX - 2, options.PositionY - size.Height + 2, size.Width + 4, size.Height + 2);
            }

            gfx.DrawString(options.Text, font, textBrush, options.PositionX, options.PositionY);

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> AddImageAnnotationAsync(byte[] pdfDocument, ImageAnnotationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ImageBytes == null || options.ImageBytes.Length == 0)
            throw new ArgumentException("Los bytes de la imagen no pueden estar vacíos.", nameof(options));

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var inputStream = new MemoryStream(pdfDocument);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            if (document.PageCount == 0)
                throw new InvalidOperationException("El documento no contiene páginas.");

            int targetIndex = Math.Clamp(options.PageNumber - 1, 0, document.PageCount - 1);
            var page = document.Pages[targetIndex];

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var xImage = XImage.FromStream(() => new MemoryStream(options.ImageBytes));

            gfx.DrawImage(xImage, options.PositionX, options.PositionY, options.Width, options.Height);

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> ApplyRedactionAsync(byte[] pdfDocument, RedactionOptions options, CancellationToken cancellationToken = default)
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

            int targetIndex = Math.Clamp(options.PageNumber - 1, 0, document.PageCount - 1);
            var page = document.Pages[targetIndex];

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var (r, g, b) = ParseHexColor(options.FillColorHex);
            var fillBrush = new XSolidBrush(XColor.FromArgb(r, g, b));

            // Solid censor block
            gfx.DrawRectangle(fillBrush, options.PositionX, options.PositionY, options.Width, options.Height);

            // Optional overlay text
            if (!string.IsNullOrWhiteSpace(options.OverlayText) && options.Height >= 10)
            {
                var labelFont = new XFont("Arial", Math.Min(options.Height * 0.5, 10), XFontStyle.Bold);
                var rect = new XRect(options.PositionX, options.PositionY, options.Width, options.Height);
                gfx.DrawString(options.OverlayText, labelFont, XBrushes.White, rect, XStringFormats.Center);
            }

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<byte[]> BatchEditAsync(byte[] pdfDocument, IReadOnlyList<BatchEditItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(items);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var inputStream = new MemoryStream(pdfDocument);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            if (document.PageCount == 0)
                throw new InvalidOperationException("El documento no contiene páginas.");

            var grouped = items.GroupBy(x => x.PageNumber);

            foreach (var group in grouped)
            {
                int pageIndex = Math.Clamp(group.Key - 1, 0, document.PageCount - 1);
                var page = document.Pages[pageIndex];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                foreach (var item in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // If this is an existing text replacement or edit, erase the original text area first
                    if (item.IsReplacement && item.OriginalX.HasValue && item.OriginalY.HasValue && item.OriginalWidth.HasValue && item.OriginalHeight.HasValue)
                    {
                        var bgHex = item.BackgroundColorHex ?? "#FFFFFF";
                        var (er, eg, eb) = ParseHexColor(bgHex);
                        var eraseRect = new XRect(
                            Math.Max(0, item.OriginalX.Value - 1),
                            Math.Max(0, item.OriginalY.Value - 1),
                            item.OriginalWidth.Value + 2,
                            item.OriginalHeight.Value + 2);
                        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(er, eg, eb)), eraseRect);
                    }

                    if (item.Type == "text" && !string.IsNullOrWhiteSpace(item.Text))
                    {
                        var fontStyle = XFontStyle.Regular;
                        if (item.IsBold && item.IsItalic) fontStyle = XFontStyle.BoldItalic;
                        else if (item.IsBold) fontStyle = XFontStyle.Bold;
                        else if (item.IsItalic) fontStyle = XFontStyle.Italic;

                        var font = new XFont("Arial", item.FontSize > 0 ? item.FontSize : 14, fontStyle);
                        var (tr, tg, tb) = ParseHexColor(item.ColorHex);
                        var brush = new XSolidBrush(XColor.FromArgb(tr, tg, tb));

                        if (!string.IsNullOrWhiteSpace(item.BackgroundColorHex) && !item.IsReplacement)
                        {
                            var (br, bg, bb) = ParseHexColor(item.BackgroundColorHex);
                            var bgRect = new XRect(item.X, item.Y, Math.Max(item.Width, 20), Math.Max(item.Height, font.Height));
                            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(br, bg, bb)), bgRect);
                        }

                        var textRect = new XRect(item.X, item.Y, Math.Max(item.Width, 500), Math.Max(item.Height, font.Height * 2));
                        gfx.DrawString(item.Text, font, brush, textRect, XStringFormats.TopLeft);
                    }
                    else if (item.Type == "highlight")
                    {
                        var highlightBrush = new XSolidBrush(XColor.FromArgb(90, 255, 235, 59));
                        gfx.DrawRectangle(highlightBrush, item.X, item.Y, item.Width, item.Height);
                    }
                    else if (item.Type == "redaction")
                    {
                        var (r, g, b) = ParseHexColor(item.ColorHex);
                        var fillBrush = new XSolidBrush(XColor.FromArgb(r, g, b));
                        gfx.DrawRectangle(fillBrush, item.X, item.Y, item.Width, item.Height);

                        if (!string.IsNullOrWhiteSpace(item.Text) && item.Height >= 10)
                        {
                            var labelFont = new XFont("Arial", Math.Min(item.Height * 0.5, 9), XFontStyle.Bold);
                            var rect = new XRect(item.X, item.Y, item.Width, item.Height);
                            gfx.DrawString(item.Text, labelFont, XBrushes.White, rect, XStringFormats.Center);
                        }
                    }
                    else if ((item.Type == "image" || item.Type == "drawing") && !string.IsNullOrWhiteSpace(item.ImageBase64))
                    {
                        try
                        {
                            var rawBase64 = item.ImageBase64;
                            if (rawBase64.Contains(',')) rawBase64 = rawBase64.Split(',')[1];
                            var imgBytes = Convert.FromBase64String(rawBase64);
                            var xImage = XImage.FromStream(() => new MemoryStream(imgBytes));
                            gfx.DrawImage(xImage, item.X, item.Y, item.Width, item.Height);
                        }
                        catch
                        {
                            // Ignorar fallo de decodificación de imagen individual
                        }
                    }
                }
            }

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    private static (byte R, byte G, byte B) ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return (0, 0, 0);
        hex = hex.TrimStart('#');

        if (hex.Length == 6 &&
            byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return (r, g, b);
        }

        return (0, 0, 0);
    }

    public Task<IReadOnlyList<PdfTextBlock>> ExtractTextBlocksAsync(byte[] pdfDocument, int pageNumber, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = UglyToad.PdfPig.PdfDocument.Open(pdfDocument);
            if (pageNumber < 1 || pageNumber > document.NumberOfPages)
                return (IReadOnlyList<PdfTextBlock>)Array.Empty<PdfTextBlock>();

            var page = document.GetPage(pageNumber);
            var pageHeight = page.Height;

            // Get words sorted by vertical position (top to bottom), then left to right
            var words = page.GetWords()
                .OrderByDescending(w => w.BoundingBox.Top)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            if (words.Count == 0)
                return (IReadOnlyList<PdfTextBlock>)Array.Empty<PdfTextBlock>();

            // Group words into lines based on vertical baseline alignment
            var lines = new List<List<UglyToad.PdfPig.Content.Word>>();
            foreach (var word in words)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var matchedLine = lines.FirstOrDefault(l =>
                {
                    var lastWord = l.Last();
                    return Math.Abs(lastWord.BoundingBox.Bottom - word.BoundingBox.Bottom) < 4.0 &&
                           Math.Abs(lastWord.BoundingBox.Top - word.BoundingBox.Top) < 4.0;
                });

                if (matchedLine != null)
                {
                    matchedLine.Add(word);
                }
                else
                {
                    lines.Add(new List<UglyToad.PdfPig.Content.Word> { word });
                }
            }

            var blocks = new List<PdfTextBlock>();
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sortedLineWords = line.OrderBy(w => w.BoundingBox.Left).ToList();
                var text = string.Join(" ", sortedLineWords.Select(w => w.Text)).Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var minLeft = sortedLineWords.Min(w => w.BoundingBox.Left);
                var maxRight = sortedLineWords.Max(w => w.BoundingBox.Right);
                var maxTop = sortedLineWords.Max(w => w.BoundingBox.Top);
                var minBottom = sortedLineWords.Min(w => w.BoundingBox.Bottom);

                // Convert bottom-left PDF coordinates to top-left coordinates for screen & PdfSharp
                var x = minLeft;
                var y = pageHeight - maxTop;
                var width = maxRight - minLeft;
                var height = maxTop - minBottom;

                var firstLetter = sortedLineWords.FirstOrDefault(w => w.Letters.Count > 0)?.Letters.FirstOrDefault();
                var fontSize = firstLetter != null ? firstLetter.PointSize : 12;
                var fontName = sortedLineWords.FirstOrDefault()?.FontName ?? "Arial";

                // Extract hex color if available
                var colorHex = "#000000";
                if (firstLetter != null && firstLetter.Color != null)
                {
                    try
                    {
                        var (r, g, b) = firstLetter.Color.ToRGBValues();
                        colorHex = $"#{(int)Math.Clamp(r * 255, 0, 255):X2}{(int)Math.Clamp(g * 255, 0, 255):X2}{(int)Math.Clamp(b * 255, 0, 255):X2}";
                    }
                    catch
                    {
                        colorHex = "#000000";
                    }
                }

                blocks.Add(new PdfTextBlock
                {
                    PageNumber = pageNumber,
                    X = Math.Round(x, 2),
                    Y = Math.Round(y, 2),
                    Width = Math.Round(width, 2),
                    Height = Math.Round(height, 2),
                    Text = text,
                    FontSize = Math.Round(fontSize, 1),
                    FontName = fontName,
                    ColorHex = colorHex
                });
            }

            return (IReadOnlyList<PdfTextBlock>)blocks;
        }, cancellationToken);
    }
}
