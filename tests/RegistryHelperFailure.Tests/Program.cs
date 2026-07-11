using WinForge.Services;

var missingPath = $@"Software\WinForge.Tests\missing-{Guid.NewGuid():N}";
var result = RegistryHelper.TryDeleteValue(RegRoot.HKCU, missingPath, "value");

if (result.Success)
{
    Console.Error.WriteLine("FAIL registry helper treated a missing key as a successful interactive delete");
    return 1;
}

if (result.Error is not InvalidOperationException)
{
    Console.Error.WriteLine($"FAIL registry helper did not preserve the missing-key error: {result.Error?.GetType().Name ?? "none"}");
    return 1;
}

Console.WriteLine("PASS 1/1 Registry Helper failure tests (read-only missing-key fixture)");
return 0;
