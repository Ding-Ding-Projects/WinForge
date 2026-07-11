using WinForge.Services;

var missingPath = $@"Software\WinForge.Tests\missing-{Guid.NewGuid():N}";
var deleted = RegistryHelper.TryDeleteValue(RegRoot.HKCU, missingPath, "value", out var error);

if (deleted)
{
    Console.Error.WriteLine("FAIL registry helper treated a missing key as a successful interactive delete");
    return 1;
}

if (error is not InvalidOperationException)
{
    Console.Error.WriteLine($"FAIL registry helper did not preserve the missing-key error: {error?.GetType().Name ?? "none"}");
    return 1;
}

Console.WriteLine("PASS 1/1 Registry Helper failure tests (read-only missing-key fixture)");
return 0;
