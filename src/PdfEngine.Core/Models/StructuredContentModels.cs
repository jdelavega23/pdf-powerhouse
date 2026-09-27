namespace PdfEngine.Core.Models;

public class StructuredPageContent
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public List<string> ExtractedUrls { get; set; } = new();
}

public class StructuredDocumentContent
{
    public int TotalPages { get; set; }
    public int TotalWords { get; set; }
    public int TotalCharacters { get; set; }
    public double EstimatedReadingMinutes { get; set; }
    public List<string> AllExtractedUrls { get; set; } = new();
    public List<StructuredPageContent> Pages { get; set; } = new();
    public string FullPlainText { get; set; } = string.Empty;
    public string FullMarkdownText { get; set; } = string.Empty;
}
