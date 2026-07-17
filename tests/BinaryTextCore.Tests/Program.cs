using System.Text;
using WinForge.Services;

using NumBase = WinForge.Services.BinaryTextService.NumBase;

var failures = new List<string>();
var passed = 0;

Run("encodes padded binary bytes", () => Equal("01001000 01101001", Encode("Hi", NumBase.Binary), "binary"));
Run("encodes decimal bytes", () => Equal("72 105", Encode("Hi", NumBase.Decimal), "decimal"));
Run("encodes octal bytes", () => Equal("110 151", Encode("Hi", NumBase.Octal), "octal"));
Run("encodes uppercase two-digit hexadecimal bytes", () => Equal("48 69", Encode("Hi", NumBase.Hex), "hex"));
Run("encodes UTF-8 multibyte text", () => Equal("195 169 240 159 153 130", Encode("é🙂", NumBase.Decimal), "UTF-8 decimal"));
Run("decodes every supported separator", () => Equal("Hi!", Decode("01001000, 01101001\n00100001", NumBase.Binary), "mixed separators"));
Run("accepts matching numeric prefixes", () => Equal("Hi", Decode("0x48 0X69", NumBase.Hex), "hex prefixes"));
Run("decodes supplementary UTF-8", () => Equal("🙂", Decode("240 159 153 130", NumBase.Decimal), "supplementary code point"));
Run("handles empty input", () =>
{
    Equal(string.Empty, Encode(string.Empty, NumBase.Hex), "encode empty");
    Equal(string.Empty, Decode("  \t,\r\n", NumBase.Hex), "decode empty");
    Equal(string.Empty, Decode("\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000", NumBase.Hex), "Unicode whitespace only");
    Equal("H", Decode("\u00A048\u00A0", NumBase.Hex), "Unicode whitespace around code");
});
Run("rejects a prefix for another base", () => Invalid("0x48", NumBase.Decimal));
Run("rejects invalid digits and values above one byte", () =>
{
    Invalid("00000002", NumBase.Binary);
    Invalid("256", NumBase.Decimal);
});
Run("rejects malformed input atomically", () =>
{
    var result = BinaryTextService.Decode("48 GG 69", NumBase.Hex);
    Assert(!result.Ok, "malformed token was accepted");
    Equal(string.Empty, result.Text, "partial output");
});
Run("uses the managed UTF-8 replacement fallback for malformed continuations", () =>
{
    Equal("\uFFFD(", Decode("11000011 00101000", NumBase.Binary), "bad continuation");
    Equal("\uFFFDA", Decode("E1 80 41", NumBase.Hex), "three-byte continuation prefix");
    Equal("\uFFFDA", Decode("F0 9F 41", NumBase.Hex), "four-byte continuation prefix");
    Equal("\uFFFDA", Decode("F0 9F 92 41", NumBase.Hex), "long four-byte continuation prefix");
    Equal("\uFFFD\uFFFDA", Decode("E0 80 41", NumBase.Hex), "semantically invalid continuation prefix");
    Equal("\uFFFD\uFFFD", Decode("E0 80", NumBase.Hex), "truncated overlong prefix");
    Equal("\uFFFD\uFFFD", Decode("ED A0", NumBase.Hex), "truncated surrogate prefix");
    Equal("\uFFFD\uFFFD", Decode("F0 80", NumBase.Hex), "truncated low four-byte prefix");
    Equal("\uFFFD\uFFFD", Decode("F4 90", NumBase.Hex), "truncated high four-byte prefix");
});
Run("uses one replacement character for a truncated sequence", () =>
    Equal("\uFFFD", Decode("11110000 10011111 10010010", NumBase.Binary), "truncated sequence"));
Run("keeps the managed replacement behavior for overlong bytes", () =>
    Equal("\uFFFD\uFFFD", Decode("C0 AF", NumBase.Hex), "overlong bytes"));
Run("keeps managed replacement behavior for structurally valid invalid scalars", () =>
{
    Equal("\uFFFD\uFFFD\uFFFD", Decode("E0 80 80", NumBase.Hex), "overlong scalar");
    Equal("\uFFFD\uFFFD\uFFFD", Decode("ED A0 80", NumBase.Hex), "surrogate scalar");
    Equal("\uFFFD\uFFFD\uFFFD\uFFFD", Decode("F4 90 80 80", NumBase.Hex), "out-of-range scalar");
});
Run("replacement-encodes malformed UTF-16", () => Equal("EF BF BD", Encode("\uD800", NumBase.Hex), "unpaired surrogate"));
Run("round trips Unicode across all numeric bases", () =>
{
    foreach (var baseKind in new[] { NumBase.Binary, NumBase.Octal, NumBase.Decimal, NumBase.Hex })
        Equal("é🙂", Decode(Encode("é🙂", baseKind), baseKind), baseKind.ToString());
});

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} Binary Text managed oracle tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} Binary Text managed oracle tests");
return 1;

static string Encode(string input, NumBase baseKind)
{
    var result = BinaryTextService.Encode(input, baseKind);
    Assert(result.Ok, $"encode failed: {result.Error}");
    return result.Text;
}

static string Decode(string input, NumBase baseKind)
{
    var result = BinaryTextService.Decode(input, baseKind);
    Assert(result.Ok, $"decode failed: {result.Error}");
    return result.Text;
}

static void Invalid(string input, NumBase baseKind)
{
    var result = BinaryTextService.Decode(input, baseKind);
    Assert(!result.Ok, $"'{input}' was accepted as {baseKind}");
    Equal(string.Empty, result.Text, "failed decode output");
}

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

static void Equal<T>(T expected, T actual, string label) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
