using PdfEngine.Core.Models;

namespace PdfEngine.Core.Contracts;

public interface IPdfSigningService
{
    /// <summary>
    /// Firma digitalmente un PDF utilizando un certificado X.509 (.pfx/.p12) y opcionalmente estampa una rúbrica visual.
    /// </summary>
    Task<byte[]> SignPdfAsync(byte[] pdfDocument, DigitalSignatureOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica la firma digital y la integridad del documento.
    /// </summary>
    Task<SignatureVerificationResult> VerifySignatureAsync(byte[] signedPdf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera un certificado digital autofirmado X.509 RSA-2048 válido para desarrollo y pruebas locales.
    /// </summary>
    Task<GeneratedCertificate> GenerateDevelopmentCertificateAsync(string commonName = "Juan Manuel de la Vega", string password = "TestPassword123!");
}
