using System.Text.RegularExpressions;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("literal builder escapes .NET metacharacters", LiteralEscapes);
Run("character class escapes class metacharacters", CharacterClassEscapes);
Run("guided pieces cover anchors groups alternation and quantifiers", GuidedPieces);
Run("invalid builder input is rejected", InvalidBuilderInput);
Run("valid captures and Unicode are reported", ValidCapturesAndUnicode);
Run("invalid patterns report syntax feedback", InvalidPattern);
Run("no-match evaluation stays successful", NoMatch);
Run("multiline option changes anchor behavior", Multiline);
Run("zero-width matches terminate safely", ZeroWidth);
Run("match results are capped", ResultCap);
Run("pattern and sample limits are enforced", SizeLimits);
Run("adversarial backtracking times out", AdversarialTimeout);
Run("plain text remains default and differs from regex", PlainVersusRegex);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} regex-builder safety tests");
    return 0;
}

foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} regex-builder safety tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static void LiteralEscapes()
{
    var piece = RegexBuilderService.Build(RegexBuilderService.PieceKind.Literal, "a.b+$");
    Assert(piece.Ok, "literal piece failed");
    Assert(piece.Token == @"a\.b\+\$", $"unexpected token: {piece.Token}");
    Assert(Regex.IsMatch("a.b+$", piece.Token), "escaped token does not match the literal");
}

static void CharacterClassEscapes()
{
    var piece = RegexBuilderService.Build(RegexBuilderService.PieceKind.CharacterClass, "a-]^", option: true);
    Assert(piece.Ok, "character-class piece failed");
    Assert(piece.Token == @"[^a\-\]\^]", $"unexpected class: {piece.Token}");
    _ = new Regex(piece.Token);
}

static void GuidedPieces()
{
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Anchor, null,
        anchor: RegexBuilderService.AnchorKind.StartOfString).Token == @"\A", "anchor mismatch");
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Group, @"\w+", "word").Token == @"(?<word>\w+)",
        "named group mismatch");
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Alternation, "cat", "dog").Token == "(?:cat|dog)",
        "alternation mismatch");
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Quantifier, @"\d", "2,5").Token == @"(?:\d){2,5}",
        "quantifier mismatch");
}

static void InvalidBuilderInput()
{
    Assert(!RegexBuilderService.Build(RegexBuilderService.PieceKind.Group, "x", "bad-name").Ok,
        "invalid group name passed");
    Assert(!RegexBuilderService.Build(RegexBuilderService.PieceKind.Alternation, "x", "").Ok,
        "missing alternation branch passed");
    Assert(!RegexBuilderService.Build(RegexBuilderService.PieceKind.Quantifier, "x", "5,2").Ok,
        "descending quantifier passed");
}

static void ValidCapturesAndUnicode()
{
    var result = RegexTesterService.Evaluate(@"(?<word>\p{L}+)", "你好 café", "$1", RegexOptions.None);
    Assert(result.Ok, result.Error ?? "evaluation failed");
    Assert(result.Matches.Count == 2, "expected two Unicode words");
    Assert(result.Matches.All(match => match.Groups.Count == 1), "capture groups were not reported");
}

static void InvalidPattern()
{
    var result = RegexTesterService.Evaluate("(", "text", "", RegexOptions.None);
    Assert(!result.Ok && !string.IsNullOrWhiteSpace(result.Error), "invalid syntax was accepted");
}

static void NoMatch()
{
    var result = RegexTesterService.Evaluate("z+", "abc", "", RegexOptions.None);
    Assert(result.Ok && result.Matches.Count == 0, "no-match result is incorrect");
}

static void Multiline()
{
    var without = RegexTesterService.Evaluate("^two$", "one\ntwo", "", RegexOptions.None);
    var with = RegexTesterService.Evaluate("^two$", "one\ntwo", "", RegexOptions.Multiline);
    Assert(without.Matches.Count == 0 && with.Matches.Count == 1, "multiline anchors did not change behavior");
}

static void ZeroWidth()
{
    var result = RegexTesterService.Evaluate(@"(?=a)", "aaa", "", RegexOptions.None);
    Assert(result.Ok && result.Matches.Count == 3, "zero-width iteration did not terminate correctly");
    Assert(result.Matches.All(match => match.Length == 0), "zero-width lengths are wrong");
}

static void ResultCap()
{
    string input = new('a', RegexTesterService.MaxMatches + 10);
    var result = RegexTesterService.Evaluate("a", input, "", RegexOptions.None);
    Assert(result.Ok && result.Matches.Count == RegexTesterService.MaxMatches && result.MatchesTruncated,
        "match cap was not enforced");
}

static void SizeLimits()
{
    var pattern = RegexTesterService.Evaluate(new string('x', RegexTesterService.MaxPatternLength + 1), "", "", RegexOptions.None);
    var sample = RegexTesterService.Evaluate("x", new string('x', RegexTesterService.MaxInputLength + 1), "", RegexOptions.None);
    Assert(!pattern.Ok && !sample.Ok, "pattern or sample limit was not enforced");
}

static void AdversarialTimeout()
{
    string input = new string('a', 50_000) + "!";
    var result = RegexTesterService.Evaluate("^(a+)+$", input, "", RegexOptions.None);
    Assert(!result.Ok && result.Error?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true,
        "adversarial pattern did not time out");
}

static void PlainVersusRegex()
{
    var plain = SearchPatternService.Match("abc", new SearchPatternService.Spec("a.c"));
    var regex = SearchPatternService.Match("abc", new SearchPatternService.Spec("a.c", UseRegex: true));
    Assert(plain.Ok && !plain.IsMatch, "plain text should treat dot literally");
    Assert(regex.Ok && regex.IsMatch, "regex mode should treat dot as a metacharacter");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
