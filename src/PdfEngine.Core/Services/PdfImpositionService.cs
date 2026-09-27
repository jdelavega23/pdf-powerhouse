using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfImpositionService : IPdfImpositionService
{
    public Task<byte[]> GenerateImpositionAsync(byte[] sourcePdf, ImpositionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePdf);
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var inStream = new MemoryStream(sourcePdf);
            using var sourceDoc = PdfReader.Open(inStream, PdfDocumentOpenMode.Import);

            if (sourceDoc.PageCount == 0)
            {
                throw new InvalidOperationException("El documento PDF no contiene páginas para realizar la imposición.");
            }

            var (pageSize, targetOrientation) = ResolveTargetDimensions(options);

            using var outDoc = new PdfDocument();
            using var formStream = new MemoryStream(sourcePdf);
            using var form = XPdfForm.FromStream(formStream);

            switch (options.Mode)
            {
                case ImpositionMode.TwoUp:
                    GenerateTwoUp(outDoc, form, sourceDoc.PageCount, pageSize, targetOrientation, options);
                    break;

                case ImpositionMode.FourUp:
                    GenerateFourUp(outDoc, form, sourceDoc.PageCount, pageSize, targetOrientation, options);
                    break;

                case ImpositionMode.Booklet:
                    GenerateBooklet(outDoc, form, sourceDoc.PageCount, pageSize, targetOrientation, options);
                    break;

                default:
                    throw new NotSupportedException($"Modo de imposición '{options.Mode}' no soportado.");
            }

            using var outStream = new MemoryStream();
            outDoc.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    private static (PageSize pageSize, PageOrientation orientation) ResolveTargetDimensions(ImpositionOptions options)
    {
        var pageSize = options.TargetSheetSize.ToUpperInvariant() switch
        {
            "A3" => PageSize.A3,
            "LETTER" => PageSize.Letter,
            "LEGAL" => PageSize.Legal,
            _ => PageSize.A4
        };

        // For TwoUp, FourUp and Booklet, landscape provides the optimal side-by-side aspect ratio for standard portrait source pages
        var orientation = PageOrientation.Landscape;

        return (pageSize, orientation);
    }

    private static void GenerateTwoUp(PdfDocument outDoc, XPdfForm form, int totalPages, PageSize size, PageOrientation orientation, ImpositionOptions options)
    {
        int sheets = (int)Math.Ceiling(totalPages / 2.0);

        for (int sheet = 0; sheet < sheets; sheet++)
        {
            var page = outDoc.AddPage();
            page.Size = size;
            page.Orientation = orientation;

            using var gfx = XGraphics.FromPdfPage(page);

            double margin = Math.Max(5, options.MarginPoints);
            double totalWidth = page.Width.Point;
            double totalHeight = page.Height.Point;

            double slotWidth = (totalWidth - (3 * margin)) / 2.0;
            double slotHeight = totalHeight - (2 * margin);

            var slotLeft = new XRect(margin, margin, slotWidth, slotHeight);
            var slotRight = new XRect(margin + slotWidth + margin, margin, slotWidth, slotHeight);

            int pageIdx1 = (sheet * 2) + 1;
            int pageIdx2 = (sheet * 2) + 2;

            if (pageIdx1 <= totalPages)
            {
                DrawPageInSlot(gfx, form, pageIdx1, slotLeft, options.DrawBorders);
            }

            if (pageIdx2 <= totalPages)
            {
                DrawPageInSlot(gfx, form, pageIdx2, slotRight, options.DrawBorders);
            }
        }
    }

    private static void GenerateFourUp(PdfDocument outDoc, XPdfForm form, int totalPages, PageSize size, PageOrientation orientation, ImpositionOptions options)
    {
        int sheets = (int)Math.Ceiling(totalPages / 4.0);

        for (int sheet = 0; sheet < sheets; sheet++)
        {
            var page = outDoc.AddPage();
            page.Size = size;
            page.Orientation = orientation;

            using var gfx = XGraphics.FromPdfPage(page);

            double margin = Math.Max(5, options.MarginPoints);
            double totalWidth = page.Width.Point;
            double totalHeight = page.Height.Point;

            double slotWidth = (totalWidth - (3 * margin)) / 2.0;
            double slotHeight = (totalHeight - (3 * margin)) / 2.0;

            var slots = new[]
            {
                new XRect(margin, margin, slotWidth, slotHeight),                                // Top-Left
                new XRect(margin + slotWidth + margin, margin, slotWidth, slotHeight),           // Top-Right
                new XRect(margin, margin + slotHeight + margin, slotWidth, slotHeight),          // Bottom-Left
                new XRect(margin + slotWidth + margin, margin + slotHeight + margin, slotWidth, slotHeight) // Bottom-Right
            };

            for (int i = 0; i < 4; i++)
            {
                int pageIdx = (sheet * 4) + i + 1;
                if (pageIdx <= totalPages)
                {
                    DrawPageInSlot(gfx, form, pageIdx, slots[i], options.DrawBorders);
                }
            }
        }
    }

    private static void GenerateBooklet(PdfDocument outDoc, XPdfForm form, int totalPages, PageSize size, PageOrientation orientation, ImpositionOptions options)
    {
        // Saddle-stitch booklet imposition
        // Total pages must be multiple of 4
        int paddedTotal = ((totalPages + 3) / 4) * 4;
        int sheetCount = paddedTotal / 4;

        double margin = Math.Max(5, options.MarginPoints);

        for (int s = 0; s < sheetCount; s++)
        {
            // --- Front side of sheet ---
            int frontLeftPage = paddedTotal - (2 * s);
            int frontRightPage = (2 * s) + 1;

            var frontPage = outDoc.AddPage();
            frontPage.Size = size;
            frontPage.Orientation = orientation;

            using (var gfx = XGraphics.FromPdfPage(frontPage))
            {
                double totalWidth = frontPage.Width.Point;
                double totalHeight = frontPage.Height.Point;
                double slotWidth = (totalWidth - (3 * margin)) / 2.0;
                double slotHeight = totalHeight - (2 * margin);

                var slotLeft = new XRect(margin, margin, slotWidth, slotHeight);
                var slotRight = new XRect(margin + slotWidth + margin, margin, slotWidth, slotHeight);

                if (frontLeftPage <= totalPages)
                {
                    DrawPageInSlot(gfx, form, frontLeftPage, slotLeft, options.DrawBorders);
                }
                if (frontRightPage <= totalPages)
                {
                    DrawPageInSlot(gfx, form, frontRightPage, slotRight, options.DrawBorders);
                }
            }

            // --- Back side of sheet ---
            int backLeftPage = (2 * s) + 2;
            int backRightPage = paddedTotal - (2 * s) - 1;

            var backPage = outDoc.AddPage();
            backPage.Size = size;
            backPage.Orientation = orientation;

            using (var gfx = XGraphics.FromPdfPage(backPage))
            {
                double totalWidth = backPage.Width.Point;
                double totalHeight = backPage.Height.Point;
                double slotWidth = (totalWidth - (3 * margin)) / 2.0;
                double slotHeight = totalHeight - (2 * margin);

                var slotLeft = new XRect(margin, margin, slotWidth, slotHeight);
                var slotRight = new XRect(margin + slotWidth + margin, margin, slotWidth, slotHeight);

                if (backLeftPage <= totalPages)
                {
                    DrawPageInSlot(gfx, form, backLeftPage, slotLeft, options.DrawBorders);
                }
                if (backRightPage <= totalPages)
                {
                    DrawPageInSlot(gfx, form, backRightPage, slotRight, options.DrawBorders);
                }
            }
        }
    }

    private static void DrawPageInSlot(XGraphics gfx, XPdfForm form, int pageNumber, XRect slot, bool drawBorders)
    {
        form.PageNumber = pageNumber;

        double srcWidth = form.PixelWidth > 0 ? form.PixelWidth : form.PointWidth;
        double srcHeight = form.PixelHeight > 0 ? form.PixelHeight : form.PointHeight;

        if (srcWidth <= 0 || srcHeight <= 0)
        {
            srcWidth = 595;
            srcHeight = 842;
        }

        double scale = Math.Min(slot.Width / srcWidth, slot.Height / srcHeight);
        double destWidth = srcWidth * scale;
        double destHeight = srcHeight * scale;

        double destX = slot.X + ((slot.Width - destWidth) / 2.0);
        double destY = slot.Y + ((slot.Height - destHeight) / 2.0);

        var destRect = new XRect(destX, destY, destWidth, destHeight);

        gfx.DrawImage(form, destRect);

        if (drawBorders)
        {
            var borderPen = new XPen(XColor.FromArgb(180, 200, 200, 200), 0.75);
            gfx.DrawRectangle(borderPen, destRect);
        }
    }
}
