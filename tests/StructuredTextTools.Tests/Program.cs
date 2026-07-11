using System.Text.Json;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("JSON/YAML root scalars and empty collections round-trip", RootValuesRoundTrip);
Run("YAML parser keeps hash after an escaped double quote", EscapedQuoteKeepsHash);
Run("Word Frequency character mode counts Unicode scalars", CharacterModeCountsUnicodeScalars);
Run("Word Frequency emits escaped CSV rows", CsvEscaping);
Run("XPath evaluates node sets and scalars", XPathNodeAndScalarResults);
Run("XPath returns friendly failures", XPathFailureResults);
Run("World Clock offset formatter keeps sign and minute component", OffsetFormatting);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} structured-text tool tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} structured-text tool tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static void RootValuesRoundTrip()
{
    foreach (var json in new[] { "null", "true", "42", "\"hello\"", "[]", "{}" })
    {
        var yaml = YamlJsonService.JsonToYaml(json);
        Assert(yaml.Ok, $"JSON to YAML failed for {json}: {yaml.Error}");
        var restored = YamlJsonService.YamlToJson(yaml.Output);
        Assert(restored.Ok, $"YAML to JSON failed for {json}; YAML was '{yaml.Output}': {restored.Error}");
        SameJson(json, restored.Output, $"root value {json}");
    }
}

static void EscapedQuoteKeepsHash()
{
    const string json = "{\"message\":\"before \\\" # after\"}";
    var yaml = YamlJsonService.JsonToYaml(json);
    Assert(yaml.Ok, $"JSON to YAML failed: {yaml.Error}");
    Assert(yaml.Output.Contains("\\\" #", StringComparison.Ordinal), "fixture did not contain an escaped quote before hash");

    var restored = YamlJsonService.YamlToJson(yaml.Output);
    Assert(restored.Ok, $"YAML to JSON failed: {restored.Error}");
    SameJson(json, restored.Output, "escaped quote/hash value");
}

static void CharacterModeCountsUnicodeScalars()
{
    var result = WordFreqService.Analyze("😀 😀A", WordFreqService.Mode.Characters,
        caseInsensitive: true, minLength: 1, stripPunctuation: true, removeStopWords: false);

    Equal(3, result.TotalTokens, "Unicode scalar token count");
    Equal(2, result.Rows.Single(row => row.Term == "😀").Count, "emoji count");
    Equal(1, result.Rows.Single(row => row.Term == "a").Count, "case-folded A count");
    Assert(!result.Rows.Any(row => row.Term.Length == 1 && char.IsSurrogate(row.Term[0])),
        "UTF-16 surrogate halves leaked into character results");
}

static void CsvEscaping()
{
    var result = WordFreqService.Analyze("alpha alpha beta", WordFreqService.Mode.Words,
        caseInsensitive: true, minLength: 1, stripPunctuation: true, removeStopWords: false);
    var csv = WordFreqService.ToCsv(result);
    Assert(csv.StartsWith("Rank,Term,Count,Percent", StringComparison.Ordinal), "CSV header missing");
    Assert(csv.Contains("\"alpha\"", StringComparison.Ordinal), "CSV terms are not quoted");
}

static void XPathNodeAndScalarResults()
{
    var nodes = XPathTesterService.Evaluate("<root><item id=\"1\">one</item><item id=\"2\">two</item></root>", "//item");
    Assert(nodes.Ok, nodes.ErrorEn ?? "node evaluation failed");
    Equal(2, nodes.Count, "node count");
    Equal("item", nodes.Matches[0].Name, "node name");

    var scalar = XPathTesterService.Evaluate("<root><item>one</item><item>two</item></root>", "count(//item)");
    Assert(scalar.Ok, scalar.ErrorEn ?? "scalar evaluation failed");
    Equal("2", scalar.Scalar, "scalar count");
}

static void XPathFailureResults()
{
    var invalidXml = XPathTesterService.Evaluate("<root>", "//root");
    Assert(!invalidXml.Ok && invalidXml.ErrorEn?.StartsWith("XML parse error:", StringComparison.Ordinal) == true,
        "invalid XML did not produce a friendly XML failure");

    var invalidXPath = XPathTesterService.Evaluate("<root />", "//*[ ");
    Assert(!invalidXPath.Ok && invalidXPath.ErrorEn?.StartsWith("XPath error:", StringComparison.Ordinal) == true,
        "invalid XPath did not produce a friendly XPath failure");
}

static void OffsetFormatting()
{
    Equal("UTC+05:30", WorldClockService.FormatOffset(TimeSpan.FromMinutes(330)), "positive offset");
    Equal("UTC-03:30", WorldClockService.FormatOffset(TimeSpan.FromMinutes(-210)), "negative offset");
}

static void SameJson(string expected, string actual, string context)
{
    using var expectedDocument = JsonDocument.Parse(expected);
    using var actualDocument = JsonDocument.Parse(actual);
    Assert(expectedDocument.RootElement.ValueKind == actualDocument.RootElement.ValueKind,
        $"{context}: JSON kinds differ");
    Assert(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(expected),
            System.Text.Json.Nodes.JsonNode.Parse(actual)),
        $"{context}: JSON values differ");
}

static void Equal<T>(T expected, T? actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
