using System.Globalization;
using System.Text;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using QRCoder;

namespace PdfEngine.Core.Services;

public class PdfInvoiceService : IPdfInvoiceService
{
    private static readonly CultureInfo SpanishCulture = new("es-ES");

    public Task<InvoiceGenerationResult> GenerateInvoiceAsync(InvoiceGenerationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new InvoiceGenerationResult
            {
                InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? "FAC-" + DateTime.Now.ToString("yyyyMMdd-HHmm") : request.InvoiceNumber
            };

            // 1. Calculations
            if (request.Items == null || request.Items.Count == 0)
            {
                request.Items = new List<InvoiceItem>
                {
                    new() { Description = "Servicios Profesionales de Desarrollo y Consultoría", Quantity = 1, UnitPrice = 1000m, TaxRatePercent = 21m }
                };
            }

            decimal subtotal = 0m;
            var taxGroups = new Dictionary<decimal, (decimal Base, decimal Tax)>();

            foreach (var item in request.Items)
            {
                decimal itemSubtotal = item.Subtotal;
                subtotal += itemSubtotal;

                decimal rate = item.TaxRatePercent;
                decimal itemTax = item.TaxAmount;

                if (!taxGroups.ContainsKey(rate))
                {
                    taxGroups[rate] = (0m, 0m);
                }

                var current = taxGroups[rate];
                taxGroups[rate] = (current.Base + itemSubtotal, current.Tax + itemTax);
            }

            decimal totalTax = 0m;
            foreach (var kvp in taxGroups)
            {
                totalTax += kvp.Value.Tax;
                result.TaxBreakdown.Add(new InvoiceTaxBreakdown
                {
                    TaxRatePercent = kvp.Key,
                    TaxableBase = Math.Round(kvp.Value.Base, 2),
                    TaxAmount = Math.Round(kvp.Value.Tax, 2)
                });
            }

            decimal totalIrpf = 0m;
            if (request.IrpfRatePercent > 0)
            {
                totalIrpf = Math.Round(subtotal * (request.IrpfRatePercent / 100m), 2);
            }

            decimal totalAmount = Math.Round(subtotal + totalTax - totalIrpf, 2);

            result.Subtotal = subtotal;
            result.TotalTax = totalTax;
            result.TotalIrpf = totalIrpf;
            result.TotalAmount = totalAmount;

            // 2. Generate Factur-X / ZUGFeRD XML
            string xml = GenerateFacturXXml(request);
            result.ElectronicInvoiceXml = xml;

            // 3. Generate SEPA QR if requested
            byte[]? qrPng = null;
            if (request.IncludeSepaQr && !string.IsNullOrWhiteSpace(request.Issuer.Iban))
            {
                try
                {
                    qrPng = GenerateSepaPaymentQrPng(request.Issuer, result.InvoiceNumber, totalAmount, $"{result.InvoiceNumber} - {request.Customer.Name}");
                    result.QrCodeGenerated = true;
                }
                catch
                {
                    result.QrCodeGenerated = false;
                }
            }

            // 4. Render Visual Executive PDF
            using var doc = new PdfDocument();
            doc.Info.Title = $"Factura {result.InvoiceNumber} - {request.Issuer.Name}";
            doc.Info.Author = request.Issuer.Name;
            doc.Info.Subject = $"Factura Comercial Electrónica {result.InvoiceNumber}";
            doc.Info.Creator = "PDF Powerhouse (Motor JDL)";

            var page = doc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4; // 595 x 842 points
            page.Orientation = PdfSharpCore.PageOrientation.Portrait;

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                DrawInvoicePage(gfx, page, request, result, qrPng);
            }

            // 5. Embed Factur-X XML into PDF/A-3 if enabled
            if (request.EmbedFacturXXml && !string.IsNullOrEmpty(xml))
            {
                try
                {
                    EmbedFileInPdf(doc, "factur-x.xml", Encoding.UTF8.GetBytes(xml), "Factura Electrónica Factur-X / ZUGFeRD EN 16931");
                    result.FacturXEmbedded = true;
                }
                catch
                {
                    result.FacturXEmbedded = false;
                }
            }

            using var outStream = new MemoryStream();
            doc.Save(outStream, false);
            result.PdfBytes = outStream.ToArray();
            result.Success = true;
            result.Message = $"Factura {result.InvoiceNumber} generada con éxito ({result.TotalAmount:N2} {request.CurrencySymbol}).";

            return result;
        }, cancellationToken);
    }

    public string GenerateFacturXXml(InvoiceGenerationRequest request)
    {
        var sb = new StringBuilder();
        string issueDateStr = request.IssueDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string totalStr = request.Items.Sum(i => i.Total).ToString("F2", CultureInfo.InvariantCulture);
        string subtotalStr = request.Items.Sum(i => i.Subtotal).ToString("F2", CultureInfo.InvariantCulture);
        string taxStr = request.Items.Sum(i => i.TaxAmount).ToString("F2", CultureInfo.InvariantCulture);

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"");
        sb.AppendLine("    xmlns:qdt=\"urn:un:unece:uncefact:data:standard:QualifiedDataType:100\"");
        sb.AppendLine("    xmlns:ram=\"urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100\"");
        sb.AppendLine("    xmlns:udt=\"urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100\">");
        sb.AppendLine("  <rsm:ExchangedDocumentContext>");
        sb.AppendLine("    <ram:GuidelineSpecifiedDocumentContextParameter>");
        sb.AppendLine("      <ram:ID>urn:cen.eu:en16931:2017#compliant#urn:factur-x.eu:1p0:basic</ram:ID>");
        sb.AppendLine("    </ram:GuidelineSpecifiedDocumentContextParameter>");
        sb.AppendLine("  </rsm:ExchangedDocumentContext>");
        sb.AppendLine("  <rsm:ExchangedDocument>");
        sb.AppendLine($"    <ram:ID>{EscapeXml(request.InvoiceNumber)}</ram:ID>");
        sb.AppendLine("    <ram:TypeCode>380</ram:TypeCode>"); // 380 = Commercial Invoice
        sb.AppendLine("    <ram:IssueDateTime>");
        sb.AppendLine($"      <udt:DateTimeString format=\"102\">{issueDateStr}</udt:DateTimeString>");
        sb.AppendLine("    </ram:IssueDateTime>");
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            sb.AppendLine("    <ram:IncludedNote>");
            sb.AppendLine($"      <ram:Content>{EscapeXml(request.Notes)}</ram:Content>");
            sb.AppendLine("    </ram:IncludedNote>");
        }
        sb.AppendLine("  </rsm:ExchangedDocument>");
        sb.AppendLine("  <rsm:SupplyChainTradeTransaction>");
        
        // Items
        int lineId = 1;
        foreach (var item in request.Items)
        {
            sb.AppendLine("    <ram:IncludedSupplyChainTradeLineItem>");
            sb.AppendLine("      <ram:AssociatedDocumentLineDocument>");
            sb.AppendLine($"        <ram:LineID>{lineId++}</ram:LineID>");
            sb.AppendLine("      </ram:AssociatedDocumentLineDocument>");
            sb.AppendLine("      <ram:SpecifiedTradeProduct>");
            sb.AppendLine($"        <ram:Name>{EscapeXml(item.Description)}</ram:Name>");
            sb.AppendLine("      </ram:SpecifiedTradeProduct>");
            sb.AppendLine("      <ram:SpecifiedLineTradeAgreement>");
            sb.AppendLine("        <ram:GrossPriceProductTradePrice>");
            sb.AppendLine($"          <ram:ChargeAmount>{item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)}</ram:ChargeAmount>");
            sb.AppendLine("        </ram:GrossPriceProductTradePrice>");
            sb.AppendLine("      </ram:SpecifiedLineTradeAgreement>");
            sb.AppendLine("      <ram:SpecifiedLineTradeDelivery>");
            sb.AppendLine($"        <ram:BilledQuantity unitCode=\"C62\">{item.Quantity.ToString("F2", CultureInfo.InvariantCulture)}</ram:BilledQuantity>");
            sb.AppendLine("      </ram:SpecifiedLineTradeDelivery>");
            sb.AppendLine("      <ram:SpecifiedLineTradeSettlement>");
            sb.AppendLine("        <ram:ApplicableTradeTax>");
            sb.AppendLine("          <ram:TypeCode>VAT</ram:TypeCode>");
            sb.AppendLine($"          <ram:RateApplicablePercent>{item.TaxRatePercent.ToString("F2", CultureInfo.InvariantCulture)}</ram:RateApplicablePercent>");
            sb.AppendLine("        </ram:ApplicableTradeTax>");
            sb.AppendLine("        <ram:SpecifiedTradeSettlementLineMonetarySummation>");
            sb.AppendLine($"          <ram:LineTotalAmount>{item.Subtotal.ToString("F2", CultureInfo.InvariantCulture)}</ram:LineTotalAmount>");
            sb.AppendLine("        </ram:SpecifiedTradeSettlementLineMonetarySummation>");
            sb.AppendLine("      </ram:SpecifiedLineTradeSettlement>");
            sb.AppendLine("    </ram:IncludedSupplyChainTradeLineItem>");
        }

        // Trade Agreement (Seller & Buyer)
        sb.AppendLine("    <ram:ApplicableHeaderTradeAgreement>");
        sb.AppendLine("      <ram:SellerTradeParty>");
        sb.AppendLine($"        <ram:Name>{EscapeXml(request.Issuer.Name)}</ram:Name>");
        sb.AppendLine("        <ram:SpecifiedTaxRegistration>");
        sb.AppendLine($"          <ram:ID schemeID=\"VA\">{EscapeXml(request.Issuer.TaxId)}</ram:ID>");
        sb.AppendLine("        </ram:SpecifiedTaxRegistration>");
        sb.AppendLine("        <ram:PostalTradeAddress>");
        sb.AppendLine($"          <ram:PostcodeCode>{EscapeXml(request.Issuer.PostalCode)}</ram:PostcodeCode>");
        sb.AppendLine($"          <ram:LineOne>{EscapeXml(request.Issuer.Address)}</ram:LineOne>");
        sb.AppendLine($"          <ram:CityName>{EscapeXml(request.Issuer.City)}</ram:CityName>");
        sb.AppendLine("          <ram:CountryID>ES</ram:CountryID>");
        sb.AppendLine("        </ram:PostalTradeAddress>");
        sb.AppendLine("      </ram:SellerTradeParty>");

        sb.AppendLine("      <ram:BuyerTradeParty>");
        sb.AppendLine($"        <ram:Name>{EscapeXml(request.Customer.Name)}</ram:Name>");
        sb.AppendLine("        <ram:SpecifiedTaxRegistration>");
        sb.AppendLine($"          <ram:ID schemeID=\"VA\">{EscapeXml(request.Customer.TaxId)}</ram:ID>");
        sb.AppendLine("        </ram:SpecifiedTaxRegistration>");
        sb.AppendLine("        <ram:PostalTradeAddress>");
        sb.AppendLine($"          <ram:PostcodeCode>{EscapeXml(request.Customer.PostalCode)}</ram:PostcodeCode>");
        sb.AppendLine($"          <ram:LineOne>{EscapeXml(request.Customer.Address)}</ram:LineOne>");
        sb.AppendLine($"          <ram:CityName>{EscapeXml(request.Customer.City)}</ram:CityName>");
        sb.AppendLine("          <ram:CountryID>ES</ram:CountryID>");
        sb.AppendLine("        </ram:PostalTradeAddress>");
        sb.AppendLine("      </ram:BuyerTradeParty>");
        sb.AppendLine("    </ram:ApplicableHeaderTradeAgreement>");

        // Trade Settlement (Payment & Totals)
        sb.AppendLine("    <ram:ApplicableHeaderTradeSettlement>");
        sb.AppendLine($"      <ram:InvoiceCurrencyCode>{EscapeXml(request.Currency)}</ram:InvoiceCurrencyCode>");
        if (!string.IsNullOrWhiteSpace(request.Issuer.Iban))
        {
            sb.AppendLine("      <ram:SpecifiedTradeSettlementPaymentMeans>");
            sb.AppendLine("        <ram:TypeCode>58</ram:TypeCode>"); // 58 = SEPA Credit Transfer
            sb.AppendLine("        <ram:PayeePartyCreditorFinancialAccount>");
            sb.AppendLine($"          <ram:IBANID>{CleanIban(request.Issuer.Iban)}</ram:IBANID>");
            sb.AppendLine("        </ram:PayeePartyCreditorFinancialAccount>");
            if (!string.IsNullOrWhiteSpace(request.Issuer.Bic))
            {
                sb.AppendLine("        <ram:PayeeSpecifiedCreditorFinancialInstitution>");
                sb.AppendLine($"          <ram:BICID>{EscapeXml(request.Issuer.Bic)}</ram:BICID>");
                sb.AppendLine("        </ram:PayeeSpecifiedCreditorFinancialInstitution>");
            }
            sb.AppendLine("      </ram:SpecifiedTradeSettlementPaymentMeans>");
        }

        sb.AppendLine("      <ram:SpecifiedTradeSettlementHeaderMonetarySummation>");
        sb.AppendLine($"        <ram:LineTotalAmount>{subtotalStr}</ram:LineTotalAmount>");
        sb.AppendLine($"        <ram:TaxBasisTotalAmount>{subtotalStr}</ram:TaxBasisTotalAmount>");
        sb.AppendLine($"        <ram:TaxTotalAmount currencyID=\"{request.Currency}\">{taxStr}</ram:TaxTotalAmount>");
        sb.AppendLine($"        <ram:GrandTotalAmount>{totalStr}</ram:GrandTotalAmount>");
        sb.AppendLine($"        <ram:DuePayableAmount>{totalStr}</ram:DuePayableAmount>");
        sb.AppendLine("      </ram:SpecifiedTradeSettlementHeaderMonetarySummation>");
        sb.AppendLine("    </ram:ApplicableHeaderTradeSettlement>");

        sb.AppendLine("  </rsm:SupplyChainTradeTransaction>");
        sb.AppendLine("</rsm:CrossIndustryInvoice>");

        return sb.ToString();
    }

    public byte[] GenerateSepaPaymentQrPng(InvoiceParty issuer, string invoiceNumber, decimal amountEur, string remittanceInfo)
    {
        string cleanIban = CleanIban(issuer.Iban);
        string cleanBic = string.IsNullOrWhiteSpace(issuer.Bic) ? "" : issuer.Bic.Trim();
        string cleanName = string.IsNullOrWhiteSpace(issuer.Name) ? "EMPRESA" : issuer.Name.Trim();
        if (cleanName.Length > 70) cleanName = cleanName[..70];

        string cleanRemittance = string.IsNullOrWhiteSpace(remittanceInfo) ? invoiceNumber : remittanceInfo.Trim();
        if (cleanRemittance.Length > 140) cleanRemittance = cleanRemittance[..140];

        // European Payments Council Quick Response Code standard (EPC069-12)
        var payload = new StringBuilder();
        payload.AppendLine("BCD");
        payload.AppendLine("002");
        payload.AppendLine("1");
        payload.AppendLine("SCT");
        payload.AppendLine(cleanBic);
        payload.AppendLine(cleanName);
        payload.AppendLine(cleanIban);
        payload.AppendLine($"EUR{amountEur.ToString("F2", CultureInfo.InvariantCulture)}");
        payload.AppendLine(); // Empty Purpose
        payload.AppendLine(); // Empty Structured Ref
        payload.AppendLine(cleanRemittance); // Unstructured remittance

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(payload.ToString(), QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(12);
    }

    private static void DrawInvoicePage(XGraphics gfx, PdfPage page, InvoiceGenerationRequest req, InvoiceGenerationResult res, byte[]? qrPng)
    {
        var primaryColor = ParseColor(req.PrimaryColorHex, XColor.FromArgb(15, 23, 42)); // Slate 900
        var accentColor = ParseColor(req.AccentColorHex, XColor.FromArgb(2, 132, 199)); // Sky 600
        var grayBorder = XColor.FromArgb(226, 232, 240); // Slate 200
        var textDark = XColor.FromArgb(15, 23, 42); // Slate 900
        var textMuted = XColor.FromArgb(100, 116, 139); // Slate 500
        var bgLight = XColor.FromArgb(248, 250, 252); // Slate 50
        var bgWhite = XColor.FromArgb(255, 255, 255);

        var fontBrand = new XFont("Arial", 15, XFontStyle.Bold);
        var fontTitle = new XFont("Arial", 22, XFontStyle.Bold);
        var fontBody = new XFont("Arial", 8.5, XFontStyle.Regular);
        var fontBodyBold = new XFont("Arial", 8.5, XFontStyle.Bold);
        var fontSmall = new XFont("Arial", 7.5, XFontStyle.Regular);
        var fontSmallBold = new XFont("Arial", 7.5, XFontStyle.Bold);
        var fontMono = new XFont("Courier New", 8.5, XFontStyle.Bold);

        double margin = 40;
        double width = page.Width - (margin * 2);

        // --- 1. Top Decorative Brand Bar ---
        gfx.DrawRectangle(new XSolidBrush(accentColor), margin, 28, width, 4);

        // --- 2. Header Area ---
        double headerY = 44;

        // Left Column: Issuer (Company) Info
        gfx.DrawString(req.Issuer.Name, fontBrand, new XSolidBrush(primaryColor), margin, headerY + 12);
        
        double issuerTextY = headerY + 28;
        if (!string.IsNullOrWhiteSpace(req.Issuer.TaxId))
        {
            gfx.DrawString($"NIF / CIF: {req.Issuer.TaxId}", fontBodyBold, new XSolidBrush(textDark), margin, issuerTextY);
            issuerTextY += 13;
        }
        if (!string.IsNullOrWhiteSpace(req.Issuer.Address))
        {
            gfx.DrawString(req.Issuer.Address, fontBody, new XSolidBrush(textMuted), margin, issuerTextY);
            issuerTextY += 12;
        }
        string cityLine = $"{req.Issuer.PostalCode} {req.Issuer.City}".Trim();
        if (!string.IsNullOrWhiteSpace(cityLine))
        {
            gfx.DrawString(cityLine, fontBody, new XSolidBrush(textMuted), margin, issuerTextY);
            issuerTextY += 12;
        }
        if (!string.IsNullOrWhiteSpace(req.Issuer.Email))
        {
            gfx.DrawString(req.Issuer.Email, fontSmallBold, new XSolidBrush(accentColor), margin, issuerTextY);
            issuerTextY += 12;
        }

        // Right Column: "FACTURA" Title & Metadata Box
        double rightBoxW = 195;
        double rightBoxX = page.Width - margin - rightBoxW;

        // "FACTURA" headline
        var titleRect = new XRect(rightBoxX, headerY - 4, rightBoxW, 26);
        gfx.DrawString("FACTURA", fontTitle, new XSolidBrush(primaryColor), titleRect, XStringFormats.TopRight);

        // Number & Dates Box (68pt height with stacked dates to eliminate collisions)
        double badgeY = headerY + 24;
        double badgeH = 68;
        var badgeRect = new XRect(rightBoxX, badgeY, rightBoxW, badgeH);
        gfx.DrawRoundedRectangle(new XPen(grayBorder, 1), new XSolidBrush(bgLight), badgeRect, new XSize(4, 4));

        var badgeInnerRect = new XRect(rightBoxX + 12, badgeY + 8, rightBoxW - 24, badgeH - 16);
        gfx.DrawString("NÚMERO DE FACTURA", fontSmallBold, new XSolidBrush(textMuted), badgeInnerRect.X, badgeInnerRect.Y + 4);
        gfx.DrawString(res.InvoiceNumber, new XFont("Arial", 12, XFontStyle.Bold), new XSolidBrush(accentColor), badgeInnerRect.X, badgeInnerRect.Y + 20);
        
        gfx.DrawString($"Fecha emisión: {req.IssueDate:dd/MM/yyyy}", fontSmall, new XSolidBrush(textDark), badgeInnerRect.X, badgeInnerRect.Y + 36);
        if (req.DueDate.HasValue)
        {
            gfx.DrawString($"Fecha vencimiento: {req.DueDate.Value:dd/MM/yyyy}", fontSmallBold, new XSolidBrush(textMuted), badgeInnerRect.X, badgeInnerRect.Y + 48);
        }

        double afterHeaderY = Math.Max(issuerTextY + 12, badgeY + badgeH + 16);

        // --- 3. Customer Box ("Facturar a") ---
        double clientBoxH = 64;
        var clientBoxRect = new XRect(margin, afterHeaderY, width, clientBoxH);
        gfx.DrawRoundedRectangle(new XPen(grayBorder, 1), new XSolidBrush(bgLight), clientBoxRect, new XSize(4, 4));

        double clientPadX = margin + 14;
        gfx.DrawString("FACTURAR A:", fontSmallBold, new XSolidBrush(accentColor), clientPadX, afterHeaderY + 16);
        gfx.DrawString(req.Customer.Name, new XFont("Arial", 10.5, XFontStyle.Bold), new XSolidBrush(textDark), clientPadX, afterHeaderY + 32);

        string custDetails = string.Join("  •  ", new[]
        {
            !string.IsNullOrWhiteSpace(req.Customer.TaxId) ? $"NIF/CIF: {req.Customer.TaxId}" : null,
            !string.IsNullOrWhiteSpace(req.Customer.Address) ? req.Customer.Address : null,
            !string.IsNullOrWhiteSpace(req.Customer.City) ? $"{req.Customer.PostalCode} {req.Customer.City}" : null,
            !string.IsNullOrWhiteSpace(req.Customer.Email) ? req.Customer.Email : null
        }.Where(s => !string.IsNullOrEmpty(s)));

        if (!string.IsNullOrWhiteSpace(custDetails))
        {
            gfx.DrawString(custDetails, fontSmall, new XSolidBrush(textMuted), clientPadX, afterHeaderY + 48);
        }

        double tableY = afterHeaderY + clientBoxH + 18;

        // --- 4. Items Table ---
        double colWDesc = 240;
        double colWQty = 45;
        double colWPrice = 75;
        double colWVat = 50;
        double colWTotal = width - colWDesc - colWQty - colWPrice - colWVat; // 105

        double colXDesc = margin;
        double colXQty = colXDesc + colWDesc;
        double colXPrice = colXQty + colWQty;
        double colXVat = colXPrice + colWPrice;
        double colXTotal = colXVat + colWVat;

        // Header Row
        double tableHeaderHeight = 24;
        var tableHeaderRect = new XRect(margin, tableY, width, tableHeaderHeight);
        gfx.DrawRectangle(new XSolidBrush(primaryColor), tableHeaderRect);

        gfx.DrawString("DESCRIPCIÓN", fontSmallBold, XBrushes.White, new XRect(colXDesc + 10, tableY, colWDesc - 10, tableHeaderHeight), XStringFormats.CenterLeft);
        gfx.DrawString("CANT.", fontSmallBold, XBrushes.White, new XRect(colXQty, tableY, colWQty, tableHeaderHeight), XStringFormats.Center);
        gfx.DrawString("PRECIO", fontSmallBold, XBrushes.White, new XRect(colXPrice, tableY, colWPrice - 8, tableHeaderHeight), XStringFormats.CenterRight);
        gfx.DrawString("IVA %", fontSmallBold, XBrushes.White, new XRect(colXVat, tableY, colWVat, tableHeaderHeight), XStringFormats.Center);
        gfx.DrawString("SUBTOTAL", fontSmallBold, XBrushes.White, new XRect(colXTotal, tableY, colWTotal - 10, tableHeaderHeight), XStringFormats.CenterRight);

        double curRowY = tableY + tableHeaderHeight;
        bool isEven = false;
        double rowHeight = 24;

        foreach (var item in req.Items)
        {
            var rowRect = new XRect(margin, curRowY, width, rowHeight);
            if (isEven)
            {
                gfx.DrawRectangle(new XSolidBrush(bgLight), rowRect);
            }
            gfx.DrawLine(new XPen(grayBorder, 0.5), margin, curRowY + rowHeight, margin + width, curRowY + rowHeight);

            string desc = item.Description;
            if (desc.Length > 48) desc = desc[..45] + "...";

            gfx.DrawString(desc, fontBody, new XSolidBrush(textDark), new XRect(colXDesc + 10, curRowY, colWDesc - 10, rowHeight), XStringFormats.CenterLeft);
            gfx.DrawString(item.Quantity.ToString("0.##", SpanishCulture), fontBody, new XSolidBrush(textDark), new XRect(colXQty, curRowY, colWQty, rowHeight), XStringFormats.Center);
            gfx.DrawString($"{item.UnitPrice.ToString("N2", SpanishCulture)} {req.CurrencySymbol}", fontBody, new XSolidBrush(textDark), new XRect(colXPrice, curRowY, colWPrice - 8, rowHeight), XStringFormats.CenterRight);
            gfx.DrawString($"{item.TaxRatePercent.ToString("0.#", SpanishCulture)}%", fontBody, new XSolidBrush(textDark), new XRect(colXVat, curRowY, colWVat, rowHeight), XStringFormats.Center);
            gfx.DrawString($"{item.Subtotal.ToString("N2", SpanishCulture)} {req.CurrencySymbol}", fontBodyBold, new XSolidBrush(textDark), new XRect(colXTotal, curRowY, colWTotal - 10, rowHeight), XStringFormats.CenterRight);

            curRowY += rowHeight;
            isEven = !isEven;
        }

        // --- 5. Bottom Section: Left (Payment & QR) and Right (Totals Box) ---
        double bottomSectionY = curRowY + 16;

        // Totals Box (Right)
        double totalsBoxW = 200;
        double totalsBoxX = page.Width - margin - totalsBoxW;
        double curTotalsY = bottomSectionY;

        // Line 1: Base Imponible
        gfx.DrawString("Base Imponible:", fontBody, new XSolidBrush(textMuted), new XRect(totalsBoxX, curTotalsY, 100, 16), XStringFormats.CenterLeft);
        gfx.DrawString($"{res.Subtotal.ToString("N2", SpanishCulture)} {req.CurrencySymbol}", fontBodyBold, new XSolidBrush(textDark), new XRect(totalsBoxX + 90, curTotalsY, 110, 16), XStringFormats.CenterRight);
        curTotalsY += 18;

        // Taxes breakdown
        foreach (var tb in res.TaxBreakdown)
        {
            gfx.DrawString($"IVA {tb.TaxRatePercent.ToString("0.#", SpanishCulture)}%:", fontSmall, new XSolidBrush(textMuted), new XRect(totalsBoxX, curTotalsY, 100, 14), XStringFormats.CenterLeft);
            gfx.DrawString($"{tb.TaxAmount.ToString("N2", SpanishCulture)} {req.CurrencySymbol}", fontSmallBold, new XSolidBrush(textDark), new XRect(totalsBoxX + 90, curTotalsY, 110, 14), XStringFormats.CenterRight);
            curTotalsY += 16;
        }

        // IRPF if applicable
        if (res.TotalIrpf > 0)
        {
            var redBrush = new XSolidBrush(XColor.FromArgb(225, 29, 72)); // Rose 600
            gfx.DrawString($"Retención IRPF (-{req.IrpfRatePercent.ToString("0.#", SpanishCulture)}%):", fontSmallBold, redBrush, new XRect(totalsBoxX, curTotalsY, 110, 14), XStringFormats.CenterLeft);
            gfx.DrawString($"-{res.TotalIrpf.ToString("N2", SpanishCulture)} {req.CurrencySymbol}", fontSmallBold, redBrush, new XRect(totalsBoxX + 90, curTotalsY, 110, 14), XStringFormats.CenterRight);
            curTotalsY += 18;
        }

        curTotalsY += 6;

        // Grand Total Badge Box
        double totalBadgeH = 34;
        var grandTotalRect = new XRect(totalsBoxX, curTotalsY, totalsBoxW, totalBadgeH);
        gfx.DrawRoundedRectangle(new XSolidBrush(primaryColor), grandTotalRect, new XSize(4, 4));

        gfx.DrawString("TOTAL FACTURA", fontSmallBold, XBrushes.White, new XRect(totalsBoxX + 12, curTotalsY, 90, totalBadgeH), XStringFormats.CenterLeft);
        gfx.DrawString($"{res.TotalAmount.ToString("N2", SpanishCulture)} {req.CurrencySymbol}", new XFont("Arial", 12.5, XFontStyle.Bold), new XSolidBrush(XColor.FromArgb(56, 189, 248)), new XRect(totalsBoxX + 85, curTotalsY, totalsBoxW - 97, totalBadgeH), XStringFormats.CenterRight);

        // Payment & QR Box (Left)
        double payBoxW = width - totalsBoxW - 20; // ~295
        double payBoxH = Math.Max(120, curTotalsY + totalBadgeH - bottomSectionY);
        var payBoxRect = new XRect(margin, bottomSectionY, payBoxW, payBoxH);
        gfx.DrawRoundedRectangle(new XPen(grayBorder, 1), new XSolidBrush(bgLight), payBoxRect, new XSize(4, 4));

        double payContentX = margin + 14;
        double curPayY = bottomSectionY + 12;

        gfx.DrawString("DATOS DE PAGO BANCARIO", fontSmallBold, new XSolidBrush(accentColor), payContentX, curPayY);
        curPayY += 15;

        double qrSize = 70;

        if (!string.IsNullOrWhiteSpace(req.Issuer.Iban))
        {
            gfx.DrawString("IBAN:", fontSmallBold, new XSolidBrush(textMuted), payContentX, curPayY);
            curPayY += 11;
            gfx.DrawString(FormatIbanVisual(req.Issuer.Iban), fontMono, new XSolidBrush(primaryColor), payContentX, curPayY);
            curPayY += 14;
        }

        if (!string.IsNullOrWhiteSpace(req.Issuer.Bic))
        {
            gfx.DrawString($"BIC / SWIFT:  {req.Issuer.Bic}", fontSmall, new XSolidBrush(textDark), payContentX, curPayY);
            curPayY += 13;
        }

        gfx.DrawString($"Forma de pago:  {req.PaymentMethod}", fontSmall, new XSolidBrush(textDark), payContentX, curPayY);
        curPayY += 13;

        gfx.DrawString($"Referencia:  {res.InvoiceNumber}", fontSmallBold, new XSolidBrush(accentColor), payContentX, curPayY);

        // QR Code embedding: Placed inside a clean card on the right side of the payment box
        if (qrPng != null && qrPng.Length > 0)
        {
            try
            {
                var qrImg = XImage.FromStream(() => new MemoryStream(qrPng));
                double qrBoxX = margin + payBoxW - qrSize - 14;
                double qrBoxY = bottomSectionY + 14;

                // QR Frame
                var qrFrameRect = new XRect(qrBoxX - 4, qrBoxY - 4, qrSize + 8, qrSize + 8);
                gfx.DrawRoundedRectangle(new XPen(grayBorder, 1), new XSolidBrush(bgWhite), qrFrameRect, new XSize(3, 3));

                // QR Image
                gfx.DrawImage(qrImg, qrBoxX, qrBoxY, qrSize, qrSize);

                // QR Caption
                var qrCaptionRect = new XRect(qrBoxX - 10, qrBoxY + qrSize + 5, qrSize + 20, 12);
                gfx.DrawString("Escanear para pagar", fontSmallBold, new XSolidBrush(textMuted), qrCaptionRect, XStringFormats.Center);
            }
            catch { }
        }

        // --- 6. Footer Notes & Legal ---
        double footerY = page.Height - margin - 40;
        gfx.DrawLine(new XPen(grayBorder, 1), margin, footerY, margin + width, footerY);

        if (!string.IsNullOrWhiteSpace(req.Notes))
        {
            gfx.DrawString($"Nota: {req.Notes}", fontSmall, new XSolidBrush(textDark), margin, footerY + 12);
        }

        // Cleanly separate legal notice and Factur-X compliance badge on two separate lines
        if (!string.IsNullOrWhiteSpace(req.LegalNotice))
        {
            gfx.DrawString(req.LegalNotice, fontSmall, new XSolidBrush(textMuted), margin, footerY + 23);
        }

        string complianceTag = req.EmbedFacturXXml 
            ? "Factura Electrónica Europea Factur-X / ZUGFeRD EN 16931 | PDF Powerhouse (Motor JDL)" 
            : "Generado con PDF Powerhouse (Motor JDL)";

        gfx.DrawString(complianceTag, fontSmallBold, new XSolidBrush(accentColor), margin, footerY + 34);
    }

    private static void EmbedFileInPdf(PdfDocument doc, string filename, byte[] content, string description)
    {
        // Embed file conforming to PDF/A-3 (ISO 19005-3) and Factur-X specification
        var embeddedFileStream = new PdfDictionary(doc);
        embeddedFileStream.Elements["/Type"] = new PdfName("/EmbeddedFile");
        embeddedFileStream.Elements["/Subtype"] = new PdfName("/text#2Fxml");
        embeddedFileStream.CreateStream(content);

        var fileSpec = new PdfDictionary(doc);
        fileSpec.Elements["/Type"] = new PdfName("/Filespec");
        fileSpec.Elements["/F"] = new PdfString(filename);
        fileSpec.Elements["/UF"] = new PdfString(filename);
        fileSpec.Elements["/Desc"] = new PdfString(description);
        fileSpec.Elements["/EF"] = new PdfDictionary(doc);

        var efDict = (PdfDictionary)fileSpec.Elements["/EF"];
        efDict.Elements["/F"] = embeddedFileStream;
        efDict.Elements["/UF"] = embeddedFileStream;
        fileSpec.Elements["/AFRelationship"] = new PdfName("/Alternative"); // Factur-X requirement

        // Add to Names -> EmbeddedFiles
        var catalog = doc.Internals.Catalog;
        if (!catalog.Elements.ContainsKey("/Names"))
        {
            catalog.Elements["/Names"] = new PdfDictionary(doc);
        }
        var namesDict = catalog.Elements.GetDictionary("/Names") ?? new PdfDictionary(doc);
        catalog.Elements["/Names"] = namesDict;

        var embeddedFilesDict = new PdfDictionary(doc);
        var namesArray = new PdfArray(doc);
        namesArray.Elements.Add(new PdfString(filename));
        namesArray.Elements.Add(fileSpec);
        embeddedFilesDict.Elements["/Names"] = namesArray;
        namesDict.Elements["/EmbeddedFiles"] = embeddedFilesDict;

        // Associated Files (/AF) array for PDF/A-3
        var afArray = new PdfArray(doc);
        afArray.Elements.Add(fileSpec);
        catalog.Elements["/AF"] = afArray;
    }

    private static string CleanIban(string iban) =>
        string.IsNullOrWhiteSpace(iban) ? "" : iban.Replace(" ", "").Replace("-", "").ToUpperInvariant();

    private static string FormatIbanVisual(string iban)
    {
        var clean = CleanIban(iban);
        var sb = new StringBuilder();
        for (int i = 0; i < clean.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(clean[i]);
        }
        return sb.ToString();
    }

    private static string EscapeXml(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    private static XColor ParseColor(string? hex, XColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try
        {
            var clean = hex.Trim().TrimStart('#');
            if (clean.Length == 6)
            {
                byte r = byte.Parse(clean[..2], NumberStyles.HexNumber);
                byte g = byte.Parse(clean.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(clean.Substring(4, 2), NumberStyles.HexNumber);
                return XColor.FromArgb(r, g, b);
            }
        }
        catch { }
        return fallback;
    }
}
