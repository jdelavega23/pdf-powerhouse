# Contributing to PDF Powerhouse 🚀

Thank you for your interest in contributing to **PDF Powerhouse**! We welcome all contributions that help improve performance, add new document processing capabilities, fix bugs, or expand documentation.

---

## 🛠️ Development Setup

### Prerequisites
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or newer
* C++ Runtime (required for native PDFium / Docnet bindings)
* Docker & Docker Compose (optional, for containerized testing)

### Clone & Build
```bash
git clone https://github.com/jdelavega23/pdf-powerhouse.git
cd pdf-powerhouse
dotnet build
```

### Running Tests
Before submitting any changes, ensure all automated tests pass:
```bash
dotnet test tests/PdfEngine.Tests/PdfEngine.Tests.csproj
```

---

## 📜 Contribution Guidelines

1. **Fork the Repository**: Create your branch from `main` (`git checkout -b feature/awesome-feature`).
2. **Architecture Decoupling**: Keep business and PDF logic inside `PdfEngine.Core` with clear contracts under `Contracts/`. Keep `PdfEngine.Api` focused strictly on endpoint mapping and HTTP streaming.
3. **In-Memory Streaming**: Prefer in-memory processing (`MemoryStream`) over writing temporary files to disk whenever possible, to maintain total privacy and zero-footprint operation.
4. **Testing**: Add xUnit unit tests in `PdfEngine.Tests` for every new service or feature.
5. **Clean Commits**: Write clear, descriptive commit messages.
6. **Pull Requests**: Open a PR targeting `main` with a detailed explanation of your changes.

---

## 💡 Code Style

* Standard C# coding conventions.
* Enable nullable reference types (`<Nullable>enable</Nullable>`).
* Format code with `dotnet format` before pushing.

---

## 💬 Community & Feedback

Feel free to open an [Issue](https://github.com/jdelavega23/pdf-powerhouse/issues) for bug reports, architectural questions, or feature proposals.
