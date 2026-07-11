using System.Text.Json;
using WinForge.Models;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("failed unprotect retains the opaque DPAPI key during unrelated edits", FailedUnprotectRetainsOpaqueKey);
Run("failed protect leaves the existing provider file byte-for-byte unchanged", FailedProtectLeavesFileUntouched);
Run("an empty protector result is rejected like a protect failure", EmptyProtectionOutputLeavesFileUntouched);
Run("a deliberate replacement key can recover an unreadable key", ExplicitReplacementRecoversUnreadableKey);
Run("new non-empty API keys are persisted only through the protector", NewKeyIsProtected);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} AI Chat persistence tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} AI Chat persistence tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
    }
}

void FailedUnprotectRetainsOpaqueKey()
{
    using var fixture = new ProviderFileFixture();
    fixture.Write(new AiProvider { Id = "openai", Name = "Original", Kind = AiProviderKind.OpenAiCompatible, ApiKey = "dpapi:opaque-old" });
    var protector = new FakeProtector { FailUnprotect = true };
    var store = new AiProviderPersistence(fixture.Path, protector);

    var provider = store.Load().Single();
    Equal("", provider.ApiKey, "unreadable key was exposed as plaintext");
    Assert(store.HasUnreadableSecret(provider.Id), "unreadable key was not tracked");

    provider.Name = "Renamed without touching key";
    Assert(store.TrySave(new[] { provider }), "unrelated edit should retain the opaque key");

    var saved = fixture.Read().Single();
    Equal("dpapi:opaque-old", saved.ApiKey, "unreadable key was overwritten with an empty value");
    Equal("Renamed without touching key", saved.Name, "unrelated edit was not persisted");
    Assert(store.HasUnreadableSecret(provider.Id), "opaque key tracking was lost after a preserving save");
}

void FailedProtectLeavesFileUntouched()
{
    using var fixture = new ProviderFileFixture();
    fixture.Write(new AiProvider { Id = "openai", Name = "Original", Kind = AiProviderKind.OpenAiCompatible, ApiKey = "dpapi:old-key" });
    var before = File.ReadAllText(fixture.Path);
    var protector = new FakeProtector { FailProtect = true, PlainByBlob = { ["dpapi:old-key"] = "secret" } };
    var store = new AiProviderPersistence(fixture.Path, protector);

    var provider = store.Load().Single();
    Equal("secret", provider.ApiKey, "fake protector did not load the original key");
    provider.Name = "Attempted rename";

    Assert(!store.TrySave(new[] { provider }), "protect failure was reported as a successful save");
    Equal(before, File.ReadAllText(fixture.Path), "provider file changed after protect failed");
    Equal(1, protector.ProtectCalls, "protect failure was not exercised exactly once");
}

void EmptyProtectionOutputLeavesFileUntouched()
{
    using var fixture = new ProviderFileFixture();
    fixture.Write(new AiProvider { Id = "openai", Name = "Original", Kind = AiProviderKind.OpenAiCompatible, ApiKey = "dpapi:old-key" });
    var before = File.ReadAllText(fixture.Path);
    var protector = new FakeProtector { ReturnEmptyProtectedSecret = true, PlainByBlob = { ["dpapi:old-key"] = "secret" } };
    var store = new AiProviderPersistence(fixture.Path, protector);

    var provider = store.Load().Single();
    provider.Name = "Attempted rename";
    Assert(!store.TrySave(new[] { provider }), "empty protected output was accepted");
    Equal(before, File.ReadAllText(fixture.Path), "provider file changed after an empty protected output");
}

void ExplicitReplacementRecoversUnreadableKey()
{
    using var fixture = new ProviderFileFixture();
    fixture.Write(new AiProvider { Id = "openai", Name = "Original", Kind = AiProviderKind.OpenAiCompatible, ApiKey = "dpapi:opaque-old" });
    var protector = new FakeProtector { FailUnprotect = true };
    var store = new AiProviderPersistence(fixture.Path, protector);

    var provider = store.Load().Single();
    provider.ApiKey = "replacement-secret";
    Assert(store.TrySave(new[] { provider }), "explicit replacement was not saved");

    Equal("dpapi:fake-18-1", fixture.Read().Single().ApiKey, "replacement key was not protected");
    Assert(!store.HasUnreadableSecret(provider.Id), "replaced key still reported unreadable");
}

void NewKeyIsProtected()
{
    using var fixture = new ProviderFileFixture();
    var protector = new FakeProtector();
    var store = new AiProviderPersistence(fixture.Path, protector);
    var provider = new AiProvider { Id = "new", Name = "New", Kind = AiProviderKind.OpenAiCompatible, ApiKey = "new-secret" };

    Assert(store.TrySave(new[] { provider }), "new provider was not saved");
    var stored = fixture.Read().Single().ApiKey;
    Equal("dpapi:fake-10-1", stored, "new API key was written without protection");
    Assert(!stored.Contains("new-secret", StringComparison.Ordinal),
        "new API key leaked as a plaintext field");
}

static void Equal<T>(T expected, T actual, string message) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeProtector : IAiProviderSecretProtector
{
    public bool FailProtect { get; init; }
    public bool FailUnprotect { get; init; }
    public bool ReturnEmptyProtectedSecret { get; init; }
    public int ProtectCalls { get; private set; }
    public Dictionary<string, string> PlainByBlob { get; } = new(StringComparer.Ordinal);

    public bool TryProtect(string plain, out string protectedSecret)
    {
        ProtectCalls++;
        protectedSecret = "";
        if (FailProtect) return false;
        if (ReturnEmptyProtectedSecret) return true;
        protectedSecret = $"dpapi:fake-{plain.Length}-{ProtectCalls}";
        return true;
    }

    public bool TryUnprotect(string protectedSecret, out string plain)
    {
        plain = "";
        if (FailUnprotect) return false;
        if (!PlainByBlob.TryGetValue(protectedSecret, out var stored)) return false;
        plain = stored;
        return true;
    }
}

sealed class ProviderFileFixture : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinForge-AiChatPersistence.Tests", Guid.NewGuid().ToString("N"));
    public string Path { get; }

    public ProviderFileFixture()
    {
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, "ai-providers.json");
    }

    public void Write(params AiProvider[] providers) =>
        File.WriteAllText(Path, JsonSerializer.Serialize(providers, new JsonSerializerOptions { WriteIndented = true }));

    public List<AiProvider> Read() =>
        JsonSerializer.Deserialize<List<AiProvider>>(File.ReadAllText(Path)) ?? throw new InvalidOperationException("provider file did not deserialize");

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch { }
    }
}
