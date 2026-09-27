# PDF POWERHOUSE 🚀

<div align="center">

[![Continuous Integration](https://github.com/jdelavega23/pdf-powerhouse/actions/workflows/ci.yml/badge.svg)](https://github.com/jdelavega23/pdf-powerhouse/actions)
![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg?style=flat-square&logo=dotnet)
![Google PDFium](https://img.shields.io/badge/Google-PDFium%20C%2B%2B-red.svg?style=flat-square)
![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)
![Privacy: 100% Local](https://img.shields.io/badge/Privacy-100%25%20Local%20%7C%20No%20Cloud-success.svg?style=flat-square)
![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)

**High-Performance, Sovereign & Privacy-First PDF Processing Engine & REST API**  
*100% On-Premise & Local Memory Streaming • Zero Cloud Tracking • Zero Subscription Fees*

Designed & Engineered by **Juan Manuel de la Vega** ([@jdelavega23](https://github.com/jdelavega23))

[Features](#-key-features) • [Architecture](#-technical-architecture) • [Quickstart](#-quickstart) • [Docker](#-docker--containers) • [Roadmap](#-roadmap) • [Contributing](#-contributing)

</div>

---

## ⚡ Vision & Philosophy

Tired of abusive subscription fees ($25/mo to Adobe Acrobat or ILovePDF) and uploading sensitive personal, legal, or medical documents to third-party cloud servers?

**PDF Powerhouse** is an open-source, enterprise-grade PDF suite engineered for **absolute local privacy (GDPR / HIPAA compliant)** and blazing speed. All operations are executed directly in RAM using native C++ Google PDFium and .NET 9, leaving zero file traces behind.

---

## ✨ Key Features

- **Blazing Native Rendering**: Official **Google PDFium C++** (x64) bindings render pages to PNG in milliseconds with ultra-crisp resolution.
- **Document Manipulation**:
  - `Merge`: Ultra-fast PDF concatenation preserving vector graphics, bookmarks, and fonts.
  - `Split`: Automatic splitting into single pages or ranges, with instant `.ZIP` packaging.
  - `Extract`: Surgical page extraction by custom ranges (e.g. `1-3, 5, 8-10`).
  - `Rotate`: 90°, 180°, 270° orientation correction (per page or whole document).
  - `Watermark`: Vector watermark overlay with customizable font, angle, opacity, and color.
- **Security & Integrity**:
  - `Protect`: Multi-level password encryption (User / Owner) with granular permission flags (printing, copying, annotations).
  - `Unlock`: Permission stripping and decryption.
  - `Sign`: PKCS#7 / PAdES digital signatures with X.509 certificates (`.pfx` / `.p12`), visual cryptographic stamps, and self-signed certificate generator.
- **Advanced Engineering**:
  - `PDF/A Validator & Converter`: Official ISO 19005-1 / ISO 19005-2 compliance for judicial and public administration submissions.
  - `Local OCR`: Native Tesseract 5 engine (300 DPI pre-render + multilingual recognition with automatic model downloads).
  - `Doctor PDF`: Structural diagnosis and automated repair of corrupted streams.
  - `Office Converter`: Universal conversion of Office documents (`.docx`, `.xlsx`, `.pptx`, `.csv`, `.txt`, `.rtf`) to vector PDF.
  - `Table Extractor`: Intelligent table boundary detection with export to structured CSV / Excel.
  - `Hot Folders`: Background watchdog service for unattended directory processing (`input/` ➔ `processed/` or `failed/`).
  - `Batch Studio`: Concurrent multi-threaded processing with ZIP packaging and forensic audit reports.

---

## 🧱 Technical Architecture

```mermaid
graph TD
    Client[Web SPA / Desktop / CLI / CURL] -->|HTTP Multipart & Streaming| API[ASP.NET Core 9 Minimal API]
    API --> Core[PdfEngine.Core]
    
    subgraph "PdfEngine.Core Engines"
        Core --> PDFium["Google PDFium (Native C++ x64 via Docnet)"]
        Core --> PdfSharp["PdfSharpCore (Vector Engine, Merge, Split, Watermark)"]
        Core --> Crypto["BouncyCastle (Cryptography, AES-256, PKCS#7/PAdES)"]
        Core --> OCR["Tesseract 5 Native (Multilingual OCR)"]
        Core --> PdfPig["UglyToad PdfPig (AcroForms, Text Coordinates, Repair)"]
    end
```

### 1. `PdfEngine.Core`
Isolated domain layer containing clean interfaces (`IPdfMergeService`, `IPdfSigningService`, `IPdfOcrService`, etc.) and zero UI dependencies. Fully testable in isolation.

### 2. `PdfEngine.Api`
High-throughput ASP.NET Core 9 Minimal API with:
- Interactive Swagger / OpenAPI UI at `/swagger`
- Embedded reactive Web Dashboard at `/` (`wwwroot/index.html`)
- In-memory stream processing with zero temporary disk writes

### 3. `PdfEngine.Tests`
Automated test suite using xUnit. 34/34 tests passing with dynamic in-memory PDF generation.

---

## 🚀 Quickstart

### 1. Run Automated Tests
```bash
dotnet test tests/PdfEngine.Tests/PdfEngine.Tests.csproj
```

### 2. Start the API & Web Dashboard
```bash
dotnet run --project src/PdfEngine.Api/PdfEngine.Api.csproj
```

Open your browser at:
- **Interactive Web App**: [http://localhost:5000](http://localhost:5000)
- **Interactive Swagger Documentation**: [http://localhost:5000/swagger](http://localhost:5000/swagger)

---

## 🐳 Docker & Containers

Deploy anywhere with a single command:

```bash
docker-compose up -d --build
```

Access the service immediately on port `5000`.

---

## 🗺️ Roadmap & Milestones

- [x] **Phase 1**: Core C# Engine + Google PDFium + REST API + Web Dashboard + Tests.
- [x] **Phase 2**: PKCS#7 / PAdES Digital Signatures (`.pfx` / `.p12` certificates & visual stamps).
- [x] **Phase 3**: Native Tesseract 5 OCR (300 DPI rendering + multilingual auto-model fetch).
- [x] **Phase 4**: Doctor PDF (structural repair), Booklet / 2-Up Imposition, AcroForms inspector.
- [x] **Phase 5**: Batch Studio concurrent queue processing with ZIP export.
- [x] **Phase 6**: PDF/A Official Compliance (ISO 19005-1 / ISO 19005-2) for government & courts.
- [x] **Phase 7**: Smart Table Extraction to CSV / Excel.
- [x] **Phase 8**: Universal Office documents to PDF converter.
- [x] **Phase 9**: Hot Folders Watchdog for background headless automation.

---

## 🤝 Contributing

Contributions, issues, and feature requests are welcome!  
Check out our [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

---

## 🔒 Security

For responsible vulnerability reporting, please see [SECURITY.md](SECURITY.md).

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.
Google PDFium, Tesseract, and bundled dependencies retain their respective open-source licenses.
