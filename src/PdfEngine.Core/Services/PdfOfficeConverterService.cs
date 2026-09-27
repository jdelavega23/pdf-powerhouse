using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace PdfEngine.Core.Services;

public sealed class PdfOfficeConverterService : IPdfOfficeConverterService
{
    public async Task<bool> IsLibreOfficeAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "soffice",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<OfficeConversionResult> ConvertToPdfAsync(string fileName, byte[] fileBytes, OfficeConversionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(fileBytes);

        var opts = options ?? new OfficeConversionOptions();
        var docType = DetectType(fileName, opts.DocumentType);

        // Try LibreOffice Headless first for Office formats if available
        bool libreAvailable = await IsLibreOfficeAvailableAsync(cancellationToken);
        if (libreAvailable && (docType is OfficeDocumentType.Word or OfficeDocumentType.Excel or OfficeDocumentType.PowerPoint or OfficeDocumentType.Rtf))
        {
            try
            {
                var libreResult = await ConvertWithLibreOfficeAsync(fileName, fileBytes, docType, cancellationToken);
                if (libreResult.Success)
                {
                    return libreResult;
                }
            }
            catch
            {
                // Fall back to native C# engine
            }
        }

        // Native C# Conversion Engine
        try
        {
            byte[] pdfBytes = docType switch
            {
                OfficeDocumentType.Word => ConvertDocxToPdfNative(fileBytes, opts),
                OfficeDocumentType.Csv => ConvertCsvToPdfNative(fileBytes, opts),
                OfficeDocumentType.Text => ConvertTextToPdfNative(fileBytes, opts),
                OfficeDocumentType.Html => ConvertHtmlToPdfNative(fileBytes, opts),
                _ => ConvertTextToPdfNative(fileBytes, opts)
            };

            using var mem = new MemoryStream(pdfBytes);
            using var readDoc = PdfSharpCore.Pdf.IO.PdfReader.Open(mem, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.InformationOnly);

            return new OfficeConversionResult
            {
                Success = true,
                SourceFileName = fileName,
                DetectedType = docType,
                PdfBytes = pdfBytes,
                PageCount = readDoc.PageCount,
                EngineUsed = "Native-PdfEngine",
                ErrorMessage = null
            };
        }
        catch (Exception ex)
        {
            return new OfficeConversionResult
            {
                Success = false,
                SourceFileName = fileName,
                DetectedType = docType,
                PdfBytes = Array.Empty<byte>(),
                PageCount = 0,
                EngineUsed = "Native-PdfEngine",
                ErrorMessage = $"Error en conversión nativa: {ex.Message}"
            };
        }
    }

    private static OfficeDocumentType DetectType(string fileName, OfficeDocumentType explicitType)
    {
        if (explicitType != OfficeDocumentType.AutoDetect)
        {
            return explicitType;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".docx" or ".doc" => OfficeDocumentType.Word,
            ".xlsx" or ".xls" => OfficeDocumentType.Excel,
            ".pptx" or ".ppt" => OfficeDocumentType.PowerPoint,
            ".rtf" => OfficeDocumentType.Rtf,
            ".csv" or ".tsv" => OfficeDocumentType.Csv,
            ".html" or ".htm" => OfficeDocumentType.Html,
            _ => OfficeDocumentType.Text
        };
    }

    private static async Task<OfficeConversionResult> ConvertWithLibreOfficeAsync(string fileName, byte[] fileBytes, OfficeDocumentType docType, CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pdfengine_office_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var tempInput = Path.Combine(tempDir, fileName);
            await File.WriteAllBytesAsync(tempInput, fileBytes, ct);

            var psi = new ProcessStartInfo
            {
                FileName = "soffice",
                Arguments = $"--headless --convert-to pdf --outdir \"{tempDir}\" \"{tempInput}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo iniciar el proceso de LibreOffice.");
            await process.WaitForExitAsync(ct);

            var outputPdfPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(fileName) + ".pdf");
            if (File.Exists(outputPdfPath))
            {
                var pdfBytes = await File.ReadAllBytesAsync(outputPdfPath, ct);
                using var mem = new MemoryStream(pdfBytes);
                using var readDoc = PdfSharpCore.Pdf.IO.PdfReader.Open(mem, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.InformationOnly);

                return new OfficeConversionResult
                {
                    Success = true,
                    SourceFileName = fileName,
                    DetectedType = docType,
                    PdfBytes = pdfBytes,
                    PageCount = readDoc.PageCount,
                    EngineUsed = "LibreOffice-Headless"
                };
            }

            return new OfficeConversionResult
            {
                Success = false,
                SourceFileName = fileName,
                DetectedType = docType,
                ErrorMessage = "LibreOffice no generó el archivo PDF esperado."
            };
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch { }
        }
    }

    private static byte[] ConvertDocxToPdfNative(byte[] docxBytes, OfficeConversionOptions options)
    {
        var paragraphs = new List<string>();

        try
        {
            using var mem = new MemoryStream(docxBytes);
            using var archive = new ZipArchive(mem, ZipArchiveMode.Read);
            var docEntry = archive.GetEntry("word/document.xml");
            if (docEntry != null)
            {
                using var entryStream = docEntry.Open();
                var xdoc = XDocument.Load(entryStream);
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                foreach (var p in xdoc.Descendants(w + "p"))
                {
                    var text = string.Concat(p.Descendants(w + "t").Select(t => t.Value));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        paragraphs.Add(text);
                    }
                }
            }
        }
        catch
        {
            // If binary .doc or corrupted docx, try raw text extraction
            var raw = Encoding.UTF8.GetString(docxBytes);
            paragraphs.Add(raw);
        }

        return RenderLinesToPdf(paragraphs, options.Title, options.FontSize);
    }

    private static byte[] ConvertTextToPdfNative(byte[] textBytes, OfficeConversionOptions options)
    {
        var text = Encoding.UTF8.GetString(textBytes);
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        return RenderLinesToPdf(lines, options.Title, options.FontSize);
    }

    private static byte[] ConvertHtmlToPdfNative(byte[] htmlBytes, OfficeConversionOptions options)
    {
        var html = Encoding.UTF8.GetString(htmlBytes);
        // Simple strip tags for native fallback
        var plain = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        var lines = plain.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        return RenderLinesToPdf(lines, options.Title, options.FontSize);
    }

    private static byte[] ConvertCsvToPdfNative(byte[] csvBytes, OfficeConversionOptions options)
    {
        var csvText = Encoding.UTF8.GetString(csvBytes);
        var rawLines = csvText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        var rows = new List<string[]>();
        foreach (var line in rawLines)
        {
            var parts = line.Split(new[] { options.Delimiter }, StringSplitOptions.None);
            rows.Add(parts);
        }

        using var doc = new PdfDocument();
        doc.Info.Title = options.Title;
        doc.Info.Creator = "PDF Powerhouse (Juan Manuel de la Vega)";

        if (rows.Count == 0)
        {
            var emptyPage = doc.AddPage();
            using var emptyGfx = XGraphics.FromPdfPage(emptyPage);
            emptyGfx.DrawString("Documento CSV Vacío", new XFont("Helvetica", 14, XFontStyle.Regular), XBrushes.Gray, new XPoint(40, 50));
            using var outMemEmpty = new MemoryStream();
            doc.Save(outMemEmpty, false);
            return outMemEmpty.ToArray();
        }

        int colCount = rows.Max(r => r.Length);
        double margin = 30.0;
        double pageWidth = XUnit.FromPoint(595).Point; // A4
        double availableWidth = pageWidth - (margin * 2);
        double colWidth = availableWidth / Math.Max(1, colCount);
        double rowHeight = 22.0;

        var font = new XFont("Helvetica", 9, XFontStyle.Regular);
        var boldFont = new XFont("Helvetica", 9, XFontStyle.Bold);
        var headerBrush = new XSolidBrush(XColor.FromArgb(240, 243, 246));
        var borderPen = new XPen(XColor.FromArgb(210, 215, 220), 0.5);

        PdfPage? currentPage = null;
        XGraphics? gfx = null;
        double currentY = margin;

        void NewPage()
        {
            gfx?.Dispose();
            currentPage = doc.AddPage();
            currentPage.Orientation = colCount > 5 ? PdfSharpCore.PageOrientation.Landscape : PdfSharpCore.PageOrientation.Portrait;
            pageWidth = currentPage.Width.Point;
            availableWidth = pageWidth - (margin * 2);
            colWidth = availableWidth / Math.Max(1, colCount);
            gfx = XGraphics.FromPdfPage(currentPage);
            currentY = margin + 30;

            // Draw Title Header
            gfx.DrawString(options.Title, new XFont("Helvetica", 12, XFontStyle.Bold), XBrushes.Navy, new XPoint(margin, margin + 15));
        }

        NewPage();

        for (int r = 0; r < rows.Count; r++)
        {
            if (currentY + rowHeight > currentPage!.Height.Point - margin)
            {
                NewPage();
            }

            var rowData = rows[r];
            bool isHeader = (r == 0);

            if (isHeader)
            {
                gfx!.DrawRectangle(headerBrush, margin, currentY, availableWidth, rowHeight);
            }
            else if (r % 2 == 1)
            {
                gfx!.DrawRectangle(new XSolidBrush(XColor.FromArgb(250, 252, 255)), margin, currentY, availableWidth, rowHeight);
            }

            for (int c = 0; c < colCount; c++)
            {
                var text = c < rowData.Length ? rowData[c].Trim('\"') : string.Empty;
                double cellX = margin + (c * colWidth);
                
                // Draw Cell Border
                gfx!.DrawRectangle(borderPen, cellX, currentY, colWidth, rowHeight);

                // Draw Text
                var cellFont = isHeader ? boldFont : font;
                var textBrush = isHeader ? XBrushes.Black : XBrushes.DarkSlateGray;

                // Truncate if too long
                if (text.Length > 25) text = text.Substring(0, 22) + "...";
                gfx.DrawString(text, cellFont, textBrush, new XPoint(cellX + 4, currentY + 15));
            }

            currentY += rowHeight;
        }

        gfx?.Dispose();

        using var outMem = new MemoryStream();
        doc.Save(outMem, false);
        return outMem.ToArray();
    }

    private static byte[] RenderLinesToPdf(IEnumerable<string> lines, string title, double fontSize)
    {
        using var doc = new PdfDocument();
        doc.Info.Title = title;
        doc.Info.Creator = "PDF Powerhouse (Juan Manuel de la Vega)";

        var font = new XFont("Helvetica", fontSize, XFontStyle.Regular);
        var titleFont = new XFont("Helvetica", fontSize + 4, XFontStyle.Bold);
        double margin = 40.0;
        double lineHeight = fontSize * 1.4;

        PdfPage page = doc.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double currentY = margin + 25;

        // Title
        gfx.DrawString(title, titleFont, XBrushes.DarkBlue, new XPoint(margin, currentY));
        currentY += lineHeight * 2;

        foreach (var line in lines)
        {
            if (currentY + lineHeight > page.Height.Point - margin)
            {
                gfx.Dispose();
                page = doc.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                currentY = margin + 20;
            }

            var cleanLine = line.Replace("\t", "    ");
            gfx.DrawString(cleanLine, font, XBrushes.Black, new XPoint(margin, currentY));
            currentY += lineHeight;
        }

        gfx.Dispose();

        using var outMem = new MemoryStream();
        doc.Save(outMem, false);
        return outMem.ToArray();
    }
}
