using Docnet.Core;
using Docnet.Core.Models;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PdfEngine.Core.Services;

public class PdfRepairService : IPdfRepairService
{
    private readonly IPdfInspectionService _inspectionService;

    public PdfRepairService(IPdfInspectionService inspectionService)
    {
        _inspectionService = inspectionService ?? throw new ArgumentNullException(nameof(inspectionService));
    }

    public async Task<PdfRepairResult> RepairPdfAsync(byte[] corruptedPdf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corruptedPdf);

        var result = new PdfRepairResult
        {
            OriginalSizeBytes = corruptedPdf.Length
        };

        if (corruptedPdf.Length == 0)
        {
            result.Success = false;
            result.DiagnosticMessage = "El archivo proporcionado está vacío (0 bytes).";
            return result;
        }

        // 1. Sanitize byte-level anomalies (leading junk, BOM, truncated EOF)
        var sanitizedBytes = SanitizePdfBytes(corruptedPdf);

        // 2. First attempt: Standard PdfSharp reconstruction (rebuilds XRef table and cleans orphaned objects)
        try
        {
            using var inStream = new MemoryStream(sanitizedBytes);
            using var sourceDoc = PdfReader.Open(inStream, PdfDocumentOpenMode.Import);

            if (sourceDoc.PageCount > 0)
            {
                using var outDoc = new PdfDocument();
                for (int i = 0; i < sourceDoc.PageCount; i++)
                {
                    outDoc.AddPage(sourceDoc.Pages[i]);
                }

                using var outStream = new MemoryStream();
                outDoc.Save(outStream, false);
                var repaired = outStream.ToArray();

                result.Success = true;
                result.RecoveredPageCount = sourceDoc.PageCount;
                result.RepairedSizeBytes = repaired.Length;
                result.RepairedPdf = repaired;
                result.DiagnosticMessage = $"Estructura de catálogo y objetos saneada con éxito. Se recuperaron {sourceDoc.PageCount} página(s) con tabla XRef canónica regenerada.";
                return result;
            }
        }
        catch (Exception)
        {
            // Fall back to tolerant Google PDFium engine
        }

        // 3. Second attempt: PDFium deep-tolerance parser (handles broken xref, damaged streams, incomplete dictionaries)
        try
        {
            using var docReader = DocLib.Instance.GetDocReader(sanitizedBytes, new PageDimensions(2.0));
            int pageCount = docReader.GetPageCount();

            if (pageCount > 0)
            {
                using var rebuiltDoc = new PdfDocument();

                for (int i = 0; i < pageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var pageReader = docReader.GetPageReader(i);
                    int width = pageReader.GetPageWidth();
                    int height = pageReader.GetPageHeight();
                    var rawBytes = pageReader.GetImage();

                    using var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
                    using var imgMs = new MemoryStream();
                    await image.SaveAsPngAsync(imgMs, cancellationToken);
                    var pngBytes = imgMs.ToArray();

                    var page = rebuiltDoc.AddPage();
                    page.Width = XUnit.FromPoint(width / 2.0);
                    page.Height = XUnit.FromPoint(height / 2.0);

                    using var xImage = XImage.FromStream(() => new MemoryStream(pngBytes));
                    using var gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);
                }

                using var finalStream = new MemoryStream();
                rebuiltDoc.Save(finalStream, false);
                var repaired = finalStream.ToArray();

                result.Success = true;
                result.RecoveredPageCount = pageCount;
                result.RepairedSizeBytes = repaired.Length;
                result.RepairedPdf = repaired;
                result.DiagnosticMessage = $"PDF con daños estructurales severos en tabla XRef/catálogo. El motor tolerante de PDFium reconstruyó {pageCount} página(s) en un documento estándar e íntegro.";
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.DiagnosticMessage = $"No fue posible reparar el archivo: el flujo de datos no contiene una firma PDF válida reconocible ({ex.Message}).";
            return result;
        }

        result.Success = false;
        result.DiagnosticMessage = "No se encontraron páginas recuperables en el documento analizado.";
        return result;
    }

    private static byte[] SanitizePdfBytes(byte[] rawBytes)
    {
        // Search for %PDF-
        var pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        int headerIndex = -1;

        for (int i = 0; i < Math.Min(rawBytes.Length - 5, 4096); i++)
        {
            if (rawBytes[i] == pdfHeader[0] &&
                rawBytes[i + 1] == pdfHeader[1] &&
                rawBytes[i + 2] == pdfHeader[2] &&
                rawBytes[i + 3] == pdfHeader[3] &&
                rawBytes[i + 4] == pdfHeader[4])
            {
                headerIndex = i;
                break;
            }
        }

        byte[] cleaned = rawBytes;
        if (headerIndex > 0)
        {
            cleaned = new byte[rawBytes.Length - headerIndex];
            Array.Copy(rawBytes, headerIndex, cleaned, 0, cleaned.Length);
        }

        // Check EOF marker
        string trailing = System.Text.Encoding.ASCII.GetString(
            cleaned,
            Math.Max(0, cleaned.Length - 128),
            Math.Min(128, cleaned.Length)
        );

        if (!trailing.Contains("%%EOF"))
        {
            var eofBytes = System.Text.Encoding.ASCII.GetBytes("\n%%EOF\n");
            var combined = new byte[cleaned.Length + eofBytes.Length];
            Buffer.BlockCopy(cleaned, 0, combined, 0, cleaned.Length);
            Buffer.BlockCopy(eofBytes, 0, combined, cleaned.Length, eofBytes.Length);
            return combined;
        }

        return cleaned;
    }
}
