namespace PdfEngine.Core.Models;

public record DigitalSignatureOptions
{
    public byte[]? CertificatePfxBytes { get; init; }
    public string CertificatePassword { get; init; } = string.Empty;
    public string SignerName { get; init; } = "Juan Manuel de la Vega";
    public string Reason { get; init; } = "Aprobación y conformidad de documento";
    public string Location { get; init; } = "España";
    public string ContactInfo { get; init; } = string.Empty;
    public int TargetPageNumber { get; init; } = 1; // 1-based page
    public double PositionX { get; init; } = 50;   // In PDF points
    public double PositionY { get; init; } = 50;   // In PDF points
    public double Width { get; init; } = 240;
    public double Height { get; init; } = 80;
    public bool IncludeVisualStamp { get; init; } = true;
}

public record SignatureVerificationResult
{
    public bool IsSigned { get; init; }
    public bool IsValid { get; init; }
    public string SignerName { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public DateTime? SigningTime { get; init; }
    public string DigestAlgorithm { get; init; } = "SHA-256";
    public string DocumentHash { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public record GeneratedCertificate
{
    public byte[] PfxBytes { get; init; } = Array.Empty<byte>();
    public string Password { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime NotBefore { get; init; }
    public DateTime NotAfter { get; init; }
    public string Thumbprint { get; init; } = string.Empty;
}
