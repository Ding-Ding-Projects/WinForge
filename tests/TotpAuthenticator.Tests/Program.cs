using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;
Run("RFC 6238 SHA-1 vectors", () => CheckVectors(TotpService.HashAlgo.Sha1, Encoding.ASCII.GetBytes("12345678901234567890"),
    new[] { 94287082, 7081804, 14050471, 89005924, 69279037, 65353130 }));
Run("RFC 6238 SHA-256 vectors", () => CheckVectors(TotpService.HashAlgo.Sha256, Encoding.ASCII.GetBytes("12345678901234567890123456789012"),
    new[] { 46119246, 68084774, 67062674, 91819424, 90698825, 77737706 }));
Run("RFC 6238 SHA-512 vectors", () => CheckVectors(TotpService.HashAlgo.Sha512, Encoding.ASCII.GetBytes("1234567890123456789012345678901234567890123456789012345678901234"),
    new[] { 90693936, 25091201, 99943326, 93441116, 38618901, 47863826 }));
Run("URI parser carries parameters", UriCarriesParameters);
Run("invalid parameters fail closed", InvalidParametersFailClosed);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} authenticator contract tests");
    return 0;
}
foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} authenticator contract tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

void CheckVectors(TotpService.HashAlgo algorithm, byte[] secretBytes, int[] expected)
{
    string secret = Base32(secretBytes);
    long[] times = { 59, 1111111109, 1111111111, 1234567890, 2000000000, 20000000000 };
    for (int i = 0; i < times.Length; i++)
    {
        string actual = TotpService.Compute(secret, 8, 30, algorithm, times[i]) ?? "";
        if (actual != expected[i].ToString("D8")) throw new InvalidOperationException($"{algorithm} at {times[i]}: {actual} != {expected[i]:D8}");
    }
}

void UriCarriesParameters()
{
    var parsed = TotpService.ParseUri("otpauth://totp/Example%3Aalice?secret=JBSWY3DPEHPK3PXP&issuer=Example&algorithm=SHA512&digits=8&period=45");
    if (parsed is null || parsed.Issuer != "Example" || parsed.Digits != 8 || parsed.Period != 45 || parsed.Algorithm != TotpService.HashAlgo.Sha512)
        throw new InvalidOperationException("URI parameters were not preserved.");
}

void InvalidParametersFailClosed()
{
    if (TotpService.Compute("INVALID0", 6, 30, TotpService.HashAlgo.Sha1, 0) is not null)
        throw new InvalidOperationException("invalid Base32 unexpectedly produced a code.");
    if (TotpService.Compute("JBSWY3DPEHPK3PXP", 11, 30, TotpService.HashAlgo.Sha1, 0) is not null)
        throw new InvalidOperationException("out-of-range digits unexpectedly produced a code.");
    if (TotpService.ParseUri("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&digits=oops") is not null)
        throw new InvalidOperationException("malformed digits unexpectedly fell back to the default.");
    if (TotpService.ParseUri("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&algorithm=MD5") is not null)
        throw new InvalidOperationException("unsupported algorithm unexpectedly fell back to SHA-1.");
    if (TotpService.ParseUri("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&period=3601") is not null)
        throw new InvalidOperationException("out-of-range period unexpectedly parsed.");
}

string Base32(byte[] bytes)
{
    const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    var output = new StringBuilder((bytes.Length * 8 + 4) / 5);
    int buffer = 0, bits = 0;
    foreach (byte value in bytes)
    {
        buffer = (buffer << 8) | value;
        bits += 8;
        while (bits >= 5)
        {
            bits -= 5;
            output.Append(alphabet[(buffer >> bits) & 31]);
        }
    }
    if (bits > 0) output.Append(alphabet[(buffer << (5 - bits)) & 31]);
    return output.ToString();
}
