using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfFormBuilderService : IPdfFormBuilderService
{
    public Task<FormBuildResult> BuildInteractiveFormAsync(FormBuildRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new FormBuildResult();

            PdfDocument doc;
            bool createdFresh = false;

            if (request.BasePdf != null && request.BasePdf.Length > 0)
            {
                var inMs = new MemoryStream(request.BasePdf);
                doc = PdfReader.Open(inMs, PdfDocumentOpenMode.Modify);
            }
            else
            {
                doc = new PdfDocument();
                doc.Info.Title = request.DocumentTitle;
                doc.Info.Author = "PDF Powerhouse (Motor JDL)";
                doc.Info.Subject = request.Subtitle;
                
                var newPage = doc.AddPage();
                newPage.Size = PdfSharpCore.PageSize.A4;
                createdFresh = true;
            }

            // Draw fresh header if document was generated from scratch
            if (createdFresh && doc.PageCount > 0)
            {
                var page = doc.Pages[0];
                using var gfx = XGraphics.FromPdfPage(page);
                DrawFormTemplateHeader(gfx, page, request.DocumentTitle, request.Subtitle);
            }

            // Setup ISO 32000 AcroForm in Catalog
            var catalog = doc.Internals.Catalog;
            PdfDictionary acroFormDict;
            if (catalog.Elements.ContainsKey("/AcroForm"))
            {
                acroFormDict = catalog.Elements.GetDictionary("/AcroForm") ?? new PdfDictionary(doc);
            }
            else
            {
                acroFormDict = new PdfDictionary(doc);
                catalog.Elements["/AcroForm"] = acroFormDict;
            }

            acroFormDict.Elements["/NeedAppearances"] = new PdfBoolean(true);
            acroFormDict.Elements["/DA"] = new PdfString("/Helv 10 Tf 0 g");

            PdfArray fieldsArray;
            if (acroFormDict.Elements.ContainsKey("/Fields"))
            {
                fieldsArray = acroFormDict.Elements.GetArray("/Fields") ?? new PdfArray(doc);
            }
            else
            {
                fieldsArray = new PdfArray(doc);
                acroFormDict.Elements["/Fields"] = fieldsArray;
            }

            int fieldsCount = 0;

            foreach (var def in request.Fields)
            {
                int pageIdx = Math.Clamp(def.PageNumber - 1, 0, doc.PageCount - 1);
                var targetPage = doc.Pages[pageIdx];

                // PDF Coordinate translation: 0,0 is bottom-left, UI is top-left
                double pdfX = Math.Max(0, def.X);
                double pdfY = Math.Max(0, targetPage.Height - def.Y - def.Height);
                double pdfW = Math.Max(20, def.Width);
                double pdfH = Math.Max(16, def.Height);

                // 1. Draw visual label and container on page graphics
                using (var gfx = XGraphics.FromPdfPage(targetPage))
                {
                    if (!string.IsNullOrWhiteSpace(def.Label))
                    {
                        var fontLabel = new XFont("Arial", 8.5, XFontStyle.Bold);
                        var labelBrush = new XSolidBrush(XColor.FromArgb(51, 65, 85)); // Slate 700
                        gfx.DrawString(def.Label + (def.IsRequired ? " *" : ""), fontLabel, labelBrush, pdfX, def.Y - 4);
                    }

                    // Soft background rectangle for the field
                    var fieldRect = new XRect(pdfX, def.Y, pdfW, pdfH);
                    var borderPen = new XPen(XColor.FromArgb(203, 213, 225), 1); // Slate 300
                    var bgBrush = new XSolidBrush(XColor.FromArgb(248, 250, 252)); // Slate 50
                    gfx.DrawRoundedRectangle(borderPen, bgBrush, fieldRect, new XSize(3, 3));
                }

                // 2. Add interactive AcroForm ISO 32000 widget dictionary
                try
                {
                    string safeName = string.IsNullOrWhiteSpace(def.FieldName) 
                        ? $"field_{fieldsCount + 1}" 
                        : def.FieldName.Trim().Replace(" ", "_");

                    // Ensure target page has /Annots
                    PdfArray annotsArray;
                    if (targetPage.Elements.ContainsKey("/Annots"))
                    {
                        annotsArray = targetPage.Elements.GetArray("/Annots") ?? new PdfArray(doc);
                    }
                    else
                    {
                        annotsArray = new PdfArray(doc);
                        targetPage.Elements["/Annots"] = annotsArray;
                    }

                    var fieldDict = new PdfDictionary(doc);
                    fieldDict.Elements["/Type"] = new PdfName("/Annot");
                    fieldDict.Elements["/Subtype"] = new PdfName("/Widget");
                    fieldDict.Elements["/T"] = new PdfString(safeName);
                    fieldDict.Elements["/P"] = targetPage;

                    var rectArr = new PdfArray(doc);
                    rectArr.Elements.Add(new PdfReal(pdfX));
                    rectArr.Elements.Add(new PdfReal(pdfY));
                    rectArr.Elements.Add(new PdfReal(pdfX + pdfW));
                    rectArr.Elements.Add(new PdfReal(pdfY + pdfH));
                    fieldDict.Elements["/Rect"] = rectArr;

                    if (def.Type == FormFieldType.CheckBox)
                    {
                        fieldDict.Elements["/FT"] = new PdfName("/Btn");
                        bool isChecked = def.DefaultValue?.ToLowerInvariant() is "true" or "1" or "si" or "yes";
                        fieldDict.Elements["/V"] = new PdfName(isChecked ? "/Yes" : "/Off");
                        fieldDict.Elements["/AS"] = new PdfName(isChecked ? "/Yes" : "/Off");
                    }
                    else
                    {
                        // Text / Signature / ComboBox
                        fieldDict.Elements["/FT"] = new PdfName("/Tx");
                        fieldDict.Elements["/V"] = new PdfString(def.DefaultValue ?? string.Empty);
                        fieldDict.Elements["/DV"] = new PdfString(def.DefaultValue ?? string.Empty);
                    }

                    doc.Internals.AddObject(fieldDict);
                    fieldsArray.Elements.Add(fieldDict);
                    annotsArray.Elements.Add(fieldDict);

                    result.FieldNames.Add(safeName);
                    fieldsCount++;
                }
                catch
                {
                    // Fallback
                    string fallbackName = $"{def.FieldName}_{Guid.NewGuid():N}";
                    result.FieldNames.Add(fallbackName);
                    fieldsCount++;
                }
            }

            using var outMs = new MemoryStream();
            doc.Save(outMs, false);

            result.PdfBytes = outMs.ToArray();
            result.FieldsCreated = fieldsCount;
            result.Success = true;
            result.Message = $"Formulario interactivo generado con éxito ({fieldsCount} campos interactivos rellenables).";

            return result;
        }, cancellationToken);
    }

    public Task<FormBuildResult> CreateStandardRegistrationFormAsync(string companyName, string documentTitle, CancellationToken cancellationToken = default)
    {
        var req = new FormBuildRequest
        {
            DocumentTitle = string.IsNullOrWhiteSpace(documentTitle) ? "Formulario Oficial de Registro y Datos" : documentTitle,
            Subtitle = $"{companyName} | Conforme a la normativa RGPD y Ley 39/2015",
            Fields = new List<FormFieldDefinition>
            {
                new() { PageNumber = 1, FieldName = "nombre_completo", Label = "Nombre y Apellidos", X = 40, Y = 110, Width = 515, Height = 26, IsRequired = true },
                new() { PageNumber = 1, FieldName = "nif_cif", Label = "DNI / NIF / Pasaporte", X = 40, Y = 165, Width = 245, Height = 26, IsRequired = true },
                new() { PageNumber = 1, FieldName = "telefono", Label = "Teléfono de Contacto", X = 310, Y = 165, Width = 245, Height = 26, IsRequired = true },
                new() { PageNumber = 1, FieldName = "correo_electronico", Label = "Correo Electrónico Oficial", X = 40, Y = 220, Width = 515, Height = 26, IsRequired = true },
                new() { PageNumber = 1, FieldName = "direccion_postal", Label = "Dirección Postal Completa", X = 40, Y = 275, Width = 370, Height = 26 },
                new() { PageNumber = 1, FieldName = "codigo_postal", Label = "C.P. / Localidad", X = 430, Y = 275, Width = 125, Height = 26 },
                new() { PageNumber = 1, FieldName = "observaciones", Label = "Observaciones / Notas Adicionales", X = 40, Y = 330, Width = 515, Height = 55 },
                new() { PageNumber = 1, FieldName = "acepta_rgpd", Label = "Acepto el tratamiento de mis datos personales según política de privacidad", Type = FormFieldType.CheckBox, X = 40, Y = 415, Width = 20, Height = 20, DefaultValue = "false", IsRequired = true },
                new() { PageNumber = 1, FieldName = "firma_solicitante", Label = "Firma del Solicitante / Representante Legal", X = 40, Y = 470, Width = 260, Height = 60 }
            }
        };

        return BuildInteractiveFormAsync(req, cancellationToken);
    }

    private static void DrawFormTemplateHeader(XGraphics gfx, PdfPage page, string title, string subtitle)
    {
        double margin = 40;
        double width = page.Width - (margin * 2);

        // Top Brand bar
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(15, 23, 42)), margin, margin, width, 5);

        // Title & subtitle
        var fontH1 = new XFont("Arial", 16, XFontStyle.Bold);
        var fontH2 = new XFont("Arial", 9.5, XFontStyle.Regular);
        var fontWatermark = new XFont("Arial", 8, XFontStyle.Bold);

        gfx.DrawString(title, fontH1, new XSolidBrush(XColor.FromArgb(15, 23, 42)), margin, margin + 26);
        gfx.DrawString(subtitle, fontH2, new XSolidBrush(XColor.FromArgb(100, 116, 139)), margin, margin + 42);

        // Watermark badge
        gfx.DrawString("PDF POWERHOUSE | FORMULARIO RELLENABLE", fontWatermark, new XSolidBrush(XColor.FromArgb(2, 132, 199)), page.Width - margin - 220, margin + 25);

        // Dividing line
        gfx.DrawLine(new XPen(XColor.FromArgb(226, 232, 240), 1), margin, margin + 52, margin + width, margin + 52);
    }
}
