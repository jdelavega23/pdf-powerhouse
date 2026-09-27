using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace PdfEngine.Tests;

public static class PdfTestHelper
{
    public static byte[] CreateSamplePdf(int pageCount, string titlePrefix = "Documento de Prueba")
    {
        using var doc = new PdfDocument();
        doc.Info.Title = $"{titlePrefix} ({pageCount} páginas)";
        doc.Info.Author = "Juan Manuel de la Vega - PDF Powerhouse";
        doc.Info.Subject = "Pruebas unitarias de arquitectura PDF";

        var font = new XFont("Arial", 20, XFontStyle.Bold);
        var subFont = new XFont("Arial", 12, XFontStyle.Regular);

        for (int i = 0; i < pageCount; i++)
        {
            var page = doc.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);

            gfx.DrawString($"{titlePrefix} - Página {i + 1}", font, XBrushes.DarkBlue, 50, 80);
            gfx.DrawString($"Generado automáticamente por el motor de pruebas a las {DateTime.UtcNow:s}Z", subFont, XBrushes.DimGray, 50, 110);
            gfx.DrawRectangle(XPens.CadetBlue, 40, 40, page.Width - 80, page.Height - 80);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    public static byte[] CreateSamplePdfWithForm()
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 16, XFontStyle.Bold);
            gfx.DrawString("Formulario Oficial de Registro", font, XBrushes.Black, 50, 50);
        }

        var fieldDict = new PdfDictionary(doc);
        fieldDict.Elements["/FT"] = new PdfName("/Tx");
        fieldDict.Elements["/T"] = new PdfString("NombreCliente");
        fieldDict.Elements["/V"] = new PdfString("Juan Manuel de la Vega");
        fieldDict.Elements["/Subtype"] = new PdfName("/Widget");
        fieldDict.Elements["/Rect"] = new PdfArray(doc, new PdfReal(50), new PdfReal(50), new PdfReal(200), new PdfReal(70));
        doc.Internals.AddObject(fieldDict);

        var acroDict = new PdfDictionary(doc);
        var fieldsArray = new PdfArray(doc);
        fieldsArray.Elements.Add(fieldDict.Reference);
        acroDict.Elements["/Fields"] = fieldsArray;
        doc.Internals.Catalog.Elements["/AcroForm"] = acroDict;

        var annotsArray = new PdfArray(doc);
        annotsArray.Elements.Add(fieldDict.Reference);
        page.Elements["/Annots"] = annotsArray;

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    public static byte[] CreateSamplePdfWithTable()
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 11, XFontStyle.Bold);
        var regularFont = new XFont("Arial", 10, XFontStyle.Regular);

        gfx.DrawString("Reporte Financiero Mensual", titleFont, XBrushes.Navy, 50, 50);

        // Draw Table Header
        double y = 100;
        gfx.DrawString("ID", headerFont, XBrushes.Black, 50, y);
        gfx.DrawString("Concepto", headerFont, XBrushes.Black, 120, y);
        gfx.DrawString("Importe", headerFont, XBrushes.Black, 280, y);
        gfx.DrawString("Estado", headerFont, XBrushes.Black, 380, y);

        // Draw Table Rows
        string[][] data = new[]
        {
            new[] { "001", "Licencia Software", "1500 EUR", "Pagado" },
            new[] { "002", "Servidor Dedicado", "350 EUR", "Pagado" },
            new[] { "003", "Mantenimiento Cloud", "800 EUR", "Pendiente" }
        };

        foreach (var row in data)
        {
            y += 25;
            gfx.DrawString(row[0], regularFont, XBrushes.DarkSlateGray, 50, y);
            gfx.DrawString(row[1], regularFont, XBrushes.DarkSlateGray, 120, y);
            gfx.DrawString(row[2], regularFont, XBrushes.DarkSlateGray, 280, y);
            gfx.DrawString(row[3], regularFont, XBrushes.DarkSlateGray, 380, y);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }
}
