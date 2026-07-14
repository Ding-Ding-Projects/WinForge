using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("tokenizes camelCase and digits", () => EqualSequence(new[] { "hello", "world", "42", "api" }, Tokenize("helloWorld42API"), "camelCase tokenization"));
Run("keeps acronym runs together", () => EqualSequence(new[] { "xmlhttp", "request" }, Tokenize("XMLHttpRequest"), "XMLHttpRequest tokenization"));
Run("splits digit boundaries", () => EqualSequence(new[] { "http", "2", "server" }, Tokenize("HTTP2Server"), "HTTP2Server tokenization"));
Run("splits every documented separator", () => EqualSequence(new[] { "hello", "world", "foo", "bar", "baz", "qux", "zap" }, Tokenize("hello_world-foo.bar/baz\\qux:zap"), "separator tokenization"));
Run("keeps CJK and camel boundaries aligned", () => EqualSequence(new[] { "漢字test" }, Tokenize("漢字Test"), "CJK tokenization"));
Run("handles empty and punctuation-only input", () =>
{
    EqualSequence(Array.Empty<string>(), Tokenize(string.Empty), "empty tokenization");
    EqualSequence(Array.Empty<string>(), Tokenize("!@#$"), "punctuation tokenization");
});
Run("renders every supported form in the right order", () =>
{
    var forms = AllForms("helloWorld42API");
    Equal(10, forms.Count, "form count");
    Equal("camelCase", forms[0].En, "camelCase label");
    Equal("helloWorld42Api", forms[0].Value, "camelCase value");
    Equal("HelloWorld42Api", forms[1].Value, "PascalCase value");
    Equal("hello_world_42_api", forms[2].Value, "snake_case value");
    Equal("hello-world-42-api", forms[3].Value, "kebab-case value");
    Equal("HELLO_WORLD_42_API", forms[4].Value, "CONSTANT_CASE value");
    Equal("Hello World 42 Api", forms[5].Value, "Title Case value");
    Equal("Hello world 42 api", forms[6].Value, "Sentence case value");
    Equal("hello.world.42.api", forms[7].Value, "dot.case value");
    Equal("hello/world/42/api", forms[8].Value, "path/case value");
    Equal("Hello-World-42-Api", forms[9].Value, "Train-Case value");
});
Run("preserves dotted-I and accent handling", () =>
{
    var forms = AllForms("İstanbul_ßeta_éclair");
    Equal("İstanbulßetaÉclair", forms[0].Value, "camelCase unicode value");
    Equal("İstanbul_ßeta_éclair", forms[2].Value, "snake_case unicode value");
});
Run("renders empty output for empty input", () =>
{
    var forms = AllForms(string.Empty);
    Equal(10, forms.Count, "empty form count");
    Equal(string.Empty, forms[0].Value, "empty camelCase");
    Equal(string.Empty, forms[9].Value, "empty train-case");
});

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} Case Converter managed oracle tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} Case Converter managed oracle tests");
return 1;

static List<string> Tokenize(string input)
{
    return CaseConvertService.Tokenize(input);
}

static List<(string En, string Zh, string Value)> AllForms(string input)
{
    return CaseConvertService.AllForms(input);
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

static void EqualSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException($"{label}: expected '{string.Join(", ", expected)}', got '{string.Join(", ", actual)}'.");
}
