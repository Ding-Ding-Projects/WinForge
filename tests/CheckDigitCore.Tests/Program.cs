using WinForge.Services;

using Scheme = WinForge.Services.CheckDigitService.Scheme;

var failures = new List<string>();
var passed = 0;

Run("Luhn accepts a separated Visa number", () => Valid(Scheme.Luhn, "4111 1111-1111 1111", "1"));
Run("Luhn reports an altered checksum as invalid", () => Invalid(Scheme.Luhn, "4111111111111112", "1"));
Run("Luhn detects official 19-digit Discover range boundaries", () =>
{
    Brand(Scheme.Luhn, "6011000000000000001", "Discover");
    Brand(Scheme.Luhn, "6589000000000000006", "Discover");
});
Run("Luhn does not mislabel the adjacent 6590 range as Discover", () => NoBrand(Scheme.Luhn, "6590000000000000004", "Discover"));
Run("Luhn rejects non-ASCII numerals", () => ParseFailure(Scheme.Luhn, "４１１１"));
Run("Luhn arithmetic remains bounded for long input", () => Valid(Scheme.Luhn, new string('0', 10_000), "0"));
Run("ISBN-10 accepts X in the final position", () => Valid(Scheme.Isbn10, "0-8044-2957-x", "X"));
Run("ISBN-13 accepts a 978 sample", () => Valid(Scheme.Isbn13, "978-0-306-40615-7", "7"));
Run("ISBN-13 accepts a 979 sample", () => Valid(Scheme.Isbn13, "979-10-90636-07-1", "1"));
Run("ISBN-13 rejects a valid non-book EAN", () => ParseFailure(Scheme.Isbn13, "4006381333931"));
Run("EAN-13 still accepts the same product code", () => Valid(Scheme.Ean13, "4006381333931", "1"));
Run("UPC-A accepts a standard sample", () => Valid(Scheme.UpcA, "036000291452", "2"));
Run("IBAN accepts the registered GB structure", () => Valid(Scheme.Iban, "GB82 WEST 1234 5698 7654 32", "82"));
Run("IBAN accepts the registered DE structure", () => Valid(Scheme.Iban, "DE89370400440532013000", "89"));
Run("IBAN accepts and normalizes alphanumeric FR", () => Valid(Scheme.Iban, "fr14 2004 1010 0505 0001 3m02 606", "14"));
Run("IBAN accepts alphanumeric MT", () => Valid(Scheme.Iban, "MT84MALT011000012345MTLCAST001S", "84"));
Run("IBAN computes corrected check digits", () => Invalid(Scheme.Iban, "GB00 WEST 1234 5698 7654 32", "82"));
Run("IBAN requires ASCII numeric check digits", () => ParseFailure(Scheme.Iban, "GBAHU"));
Run("IBAN rejects a short registered-country value", () => ParseFailure(Scheme.Iban, "GB39A"));
Run("IBAN rejects an unregistered country", () => ParseFailure(Scheme.Iban, "ZZ6600000000000"));
Run("IBAN rejects a BBAN with the wrong classes", () => ParseFailure(Scheme.Iban, "GB85ABCDEFGHIJKLMNOPQR"));
Run("IBAN rejects non-ASCII check digits", () => ParseFailure(Scheme.Iban, "GB８２WEST12345698765432"));
Run("IBAN rejects Unicode letters that case-fold to ASCII", () => ParseFailure(Scheme.Iban, "MT84MALT011000012345MTLCAST001ſ"));
Run("IBAN registry exactly matches the independent SWIFT Release 102 fixture", () =>
{
    string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "iban-registry-release-102.txt");
    string[] rows = File.ReadAllLines(fixturePath)
        .Where(line => line.Length > 0 && line[0] != '#')
        .ToArray();
    Equal(89, rows.Length, "registered prefix count");
    foreach (string row in rows)
        Equal(2, row.Count(character => character == '|'), "fixture delimiter count");
    string canonical = string.Concat(rows.Select(row => row.Replace('|', '\t') + "\n"));
    Equal(canonical, CheckDigitService.IbanRegistryCanonicalForTests(), "canonical registry");
});

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} Check Digit managed parity tests");
    return 0;
}

foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} Check Digit managed parity tests");
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

static void Valid(Scheme scheme, string input, string computed)
{
    CheckDigitService.Result result = CheckDigitService.Validate(scheme, input);
    Assert(result.Ok, $"parse failed: {result.Detail}");
    Assert(result.Valid, $"checksum failed: {result.Detail}");
    Equal(computed, result.Computed, "computed check");
}

static void Invalid(Scheme scheme, string input, string computed)
{
    CheckDigitService.Result result = CheckDigitService.Validate(scheme, input);
    Assert(result.Ok, $"parse failed: {result.Detail}");
    Assert(!result.Valid, "altered checksum was accepted");
    Equal(computed, result.Computed, "computed check");
}

static void ParseFailure(Scheme scheme, string input)
{
    CheckDigitService.Result result = CheckDigitService.Validate(scheme, input);
    Assert(!result.Ok, "structurally invalid input reached checksum validation");
}

static void Brand(Scheme scheme, string input, string expectedBrand)
{
    CheckDigitService.Result result = CheckDigitService.Validate(scheme, input);
    Assert(result.Ok && result.Valid, $"valid branded value failed: {result.Detail}");
    Assert(result.Detail.Contains(expectedBrand, StringComparison.Ordinal), $"brand '{expectedBrand}' was not detected");
}

static void NoBrand(Scheme scheme, string input, string excludedBrand)
{
    CheckDigitService.Result result = CheckDigitService.Validate(scheme, input);
    Assert(result.Ok && result.Valid, $"valid unbranded value failed: {result.Detail}");
    Assert(!result.Detail.Contains(excludedBrand, StringComparison.Ordinal), $"out-of-range value was labelled '{excludedBrand}'");
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
