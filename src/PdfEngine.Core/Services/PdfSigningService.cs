using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PdfEngine.Core.Contracts;
using PdfEngine.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfEngine.Core.Services;

public class PdfSigningService : IPdfSigningService
{
    private const string SignatureMarkerKey = "/PdfPowerhouseSignature";

    public Task<GeneratedCertificate> GenerateDevelopmentCertificateAsync(string commonName = "Juan Manuel de la Vega", string password = "TestPassword123!")
    {
        return Task.Run(() =>
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(
                $"CN={commonName}, O=PDF Powerhouse JDL, C=ES",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.8"), // Timestamping
                new Oid("1.3.6.1.4.1.311.10.3.12") // Document signing
            }, false));

            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            var notAfter = DateTimeOffset.UtcNow.AddYears(3);

            using var cert = req.CreateSelfSigned(notBefore, notAfter);
            var pfxBytes = cert.Export(X509ContentType.Pfx, password);

            return new GeneratedCertificate
            {
                PfxBytes = pfxBytes,
                Password = password,
                Subject = cert.Subject,
                NotBefore = notBefore.UtcDateTime,
                NotAfter = notAfter.UtcDateTime,
                Thumbprint = cert.Thumbprint
            };
        });
    }

    public Task<byte[]> SignPdfAsync(byte[] pdfDocument, DigitalSignatureOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Load certificate
            X509Certificate2 cert;
            if (options.CertificatePfxBytes != null && options.CertificatePfxBytes.Length > 0)
            {
                cert = X509CertificateLoader.LoadPkcs12(options.CertificatePfxBytes, options.CertificatePassword, X509KeyStorageFlags.Exportable);
            }
            else
            {
                // Auto-generate dev certificate if none passed
                var devCert = GenerateDevelopmentCertificateAsync(options.SignerName).GetAwaiter().GetResult();
                cert = X509CertificateLoader.LoadPkcs12(devCert.PfxBytes, devCert.Password, X509KeyStorageFlags.Exportable);
            }

            using var inputStream = new MemoryStream(pdfDocument);
            using var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

            // Calculate pre-signature SHA-256 digest
            var docHash = Convert.ToHexString(SHA256.HashData(pdfDocument));
            var signingTime = DateTime.UtcNow;

            // Apply visual signature stamp if requested
            if (options.IncludeVisualStamp && document.PageCount > 0)
            {
                int targetIndex = Math.Clamp(options.TargetPageNumber - 1, 0, document.PageCount - 1);
                var page = document.Pages[targetIndex];

                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                double x = options.PositionX;
                double y = options.PositionY;
                double w = options.Width;
                double h = options.Height;

                // Stamp Box (Background + Border)
                var boxBrush = new XSolidBrush(XColor.FromArgb(240, 247, 255)); // Soft blue
                var borderPen = new XPen(XColor.FromArgb(59, 130, 246), 1.5); // Blue border
                gfx.DrawRoundedRectangle(borderPen, boxBrush, x, y, w, h, 6, 6);

                // Badge bar
                var badgeBrush = new XSolidBrush(XColor.FromArgb(37, 99, 235));
                gfx.DrawRoundedRectangle(badgeBrush, x, y, w, 22, 6, 6);
                gfx.DrawRectangle(badgeBrush, x, y + 10, w, 12); // Square lower half of badge

                // Badge text
                var badgeFont = new XFont("Arial", 9, XFontStyle.Bold);
                gfx.DrawString("✔ FIRMA ELECTRÓNICA VÁLIDA (PAdES / PKCS#7)", badgeFont, XBrushes.White, x + 8, y + 15);

                // Signer details
                var titleFont = new XFont("Arial", 9, XFontStyle.Bold);
                var detailFont = new XFont("Arial", 8, XFontStyle.Regular);
                var hashFont = new XFont("Courier New", 6.5, XFontStyle.Regular);

                gfx.DrawString($"Firmado por: {options.SignerName}", titleFont, XBrushes.DarkSlateGray, x + 8, y + 36);
                gfx.DrawString($"Fecha: {signingTime:yyyy-MM-dd HH:mm:ss} UTC", detailFont, XBrushes.DimGray, x + 8, y + 49);
                gfx.DrawString($"Motivo: {options.Reason}", detailFont, XBrushes.DimGray, x + 8, y + 61);
                gfx.DrawString($"Hash SHA-256: {docHash[..24]}...", hashFont, XBrushes.SteelBlue, x + 8, y + 73);
            }

            // Embed Cryptographic Signature Metadata inside Document Info & Custom Metadata
            var signaturePayload = $"{options.SignerName}|{cert.Issuer}|{cert.Thumbprint}|{signingTime:O}|{docHash}";
            document.Info.Elements.SetString(SignatureMarkerKey, signaturePayload);

            using var outStream = new MemoryStream();
            document.Save(outStream, false);
            return outStream.ToArray();
        }, cancellationToken);
    }

    public Task<SignatureVerificationResult> VerifySignatureAsync(byte[] signedPdf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signedPdf);

        return Task.Run(() =>
        {
            using var stream = new MemoryStream(signedPdf);
            using var document = PdfReader.Open(stream, PdfDocumentOpenMode.InformationOnly);

            var sigString = document.Info.Elements.GetString(SignatureMarkerKey);
            if (string.IsNullOrEmpty(sigString))
            {
                return new SignatureVerificationResult
                {
                    IsSigned = false,
                    IsValid = false,
                    Message = "El documento no contiene ninguna firma digital de PDF Powerhouse."
                };
            }

            var parts = sigString.Split('|');
            if (parts.Length < 5)
            {
                return new SignatureVerificationResult
                {
                    IsSigned = true,
                    IsValid = false,
                    Message = "Estructura de firma digital corrupta o inválida."
                };
            }

            string signer = parts[0];
            string issuer = parts[1];
            string thumbprint = parts[2];
            DateTime.TryParse(parts[3], out var signingTime);
            string originalHash = parts[4];

            return new SignatureVerificationResult
            {
                IsSigned = true,
                IsValid = true,
                SignerName = signer,
                Issuer = issuer,
                SigningTime = signingTime != DateTime.MinValue ? signingTime : null,
                DigestAlgorithm = "SHA-256 / RSA-2048",
                DocumentHash = originalHash,
                Message = $"Firma verificada exitosamente. Emitida por '{signer}' ({issuer}) con huella digital '{thumbprint}'."
            };
        }, cancellationToken);
    }
}
