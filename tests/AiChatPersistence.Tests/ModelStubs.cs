namespace WinForge.Models;

// AiChat.cs also declares streaming DTOs that reference LocalizedText. The persistence
// harness exercises only AiProvider, so this minimal test-local declaration keeps the
// linked production model independent from the full WinUI model graph.
public sealed class LocalizedText
{
    public LocalizedText(string en, string zh) { En = en; Zh = zh; }
    public string En { get; }
    public string Zh { get; }
}
