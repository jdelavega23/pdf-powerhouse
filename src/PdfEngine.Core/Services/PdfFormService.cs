using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.AcroForms;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfFormService : IPdfFormService
{
    public Task<List<PdfFormField>> InspectFormsAsync(byte[] sourcePdf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePdf);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fieldsList = new List<PdfFormField>();

            using var ms = new MemoryStream(sourcePdf);
            using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Modify);

            var acroForm = doc.AcroForm;
            if (acroForm == null || acroForm.Fields == null || acroForm.Fields.Names.Length == 0)
            {
                return fieldsList;
            }

            foreach (var fieldName in acroForm.Fields.Names)
            {
                var field = acroForm.Fields[fieldName];
                if (field == null) continue;

                var fieldModel = new PdfFormField
                {
                    Name = field.Name ?? fieldName,
                    ReadOnly = field.ReadOnly
                };

                switch (field)
                {
                    case PdfTextField textField:
                        fieldModel.Type = "Text";
                        fieldModel.Value = textField.Text ?? string.Empty;
                        break;

                    case PdfCheckBoxField checkBoxField:
                        fieldModel.Type = "CheckBox";
                        fieldModel.Value = checkBoxField.Checked ? "true" : "false";
                        break;

                    case PdfRadioButtonField radioField:
                        fieldModel.Type = "RadioButton";
                        fieldModel.Value = radioField.SelectedIndex.ToString();
                        break;

                    case PdfComboBoxField comboField:
                        fieldModel.Type = "ComboBox";
                        fieldModel.Value = comboField.Value?.ToString() ?? string.Empty;
                        break;

                    case PdfListBoxField listField:
                        fieldModel.Type = "ListBox";
                        fieldModel.Value = listField.Value?.ToString() ?? string.Empty;
                        break;

                    default:
                        fieldModel.Type = "Field";
                        fieldModel.Value = field.Value?.ToString() ?? string.Empty;
                        break;
                }

                fieldsList.Add(fieldModel);
            }

            return fieldsList;
        }, cancellationToken);
    }

    public Task<FormFlattenResult> FlattenFormsAsync(byte[] sourcePdf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePdf);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inspected = new List<PdfFormField>();

            using var inStream = new MemoryStream(sourcePdf);
            using var doc = PdfReader.Open(inStream, PdfDocumentOpenMode.Modify);

            int flattenedCount = 0;
            var acroForm = doc.AcroForm;

            if (acroForm != null && acroForm.Fields != null && acroForm.Fields.Names.Length > 0)
            {
                // 1. Gather all form fields and values
                foreach (var fieldName in acroForm.Fields.Names)
                {
                    var field = acroForm.Fields[fieldName];
                    if (field == null) continue;

                    string val = string.Empty;
                    string type = "Field";

                    if (field is PdfTextField txt)
                    {
                        type = "Text";
                        val = txt.Text ?? string.Empty;
                    }
                    else if (field is PdfCheckBoxField chk)
                    {
                        type = "CheckBox";
                        val = chk.Checked ? "true" : "false";
                    }
                    else if (field is PdfRadioButtonField rad)
                    {
                        type = "RadioButton";
                        val = rad.SelectedIndex.ToString();
                    }
                    else if (field is PdfComboBoxField cmb)
                    {
                        type = "ComboBox";
                        val = cmb.Value?.ToString() ?? string.Empty;
                    }
                    else if (field is PdfListBoxField lst)
                    {
                        type = "ListBox";
                        val = lst.Value?.ToString() ?? string.Empty;
                    }
                    else
                    {
                        val = field.Value?.ToString() ?? string.Empty;
                    }

                    inspected.Add(new PdfFormField
                    {
                        Name = field.Name ?? fieldName,
                        Type = type,
                        Value = val,
                        ReadOnly = true
                    });

                    flattenedCount++;
                }

                // 2. Remove AcroForm from Catalog to permanently revoke interactive editing
                if (doc.Internals.Catalog.Elements.ContainsKey("/AcroForm"))
                {
                    doc.Internals.Catalog.Elements.Remove("/AcroForm");
                }

                // 3. Remove Widget annotations from all pages
                foreach (var page in doc.Pages)
                {
                    if (page.Elements.ContainsKey("/Annots"))
                    {
                        var annots = page.Elements.GetArray("/Annots");
                        if (annots != null)
                        {
                            var toRemove = new List<PdfItem>();
                            foreach (var item in annots)
                            {
                                if (item is PdfReference pref && pref.Value is PdfDictionary dict)
                                {
                                    if (dict.Elements.GetString("/Subtype") == "/Widget")
                                    {
                                        toRemove.Add(item);
                                    }
                                }
                                else if (item is PdfDictionary directDict)
                                {
                                    if (directDict.Elements.GetString("/Subtype") == "/Widget")
                                    {
                                        toRemove.Add(item);
                                    }
                                }
                            }

                            foreach (var item in toRemove)
                            {
                                annots.Elements.Remove(item);
                            }

                            if (annots.Elements.Count == 0)
                            {
                                page.Elements.Remove("/Annots");
                            }
                        }
                    }
                }
            }

            using var outStream = new MemoryStream();
            doc.Save(outStream, false);

            return new FormFlattenResult
            {
                FlattenedFieldsCount = flattenedCount,
                InspectedFields = inspected,
                FlattenedPdf = outStream.ToArray()
            };
        }, cancellationToken);
    }
}
