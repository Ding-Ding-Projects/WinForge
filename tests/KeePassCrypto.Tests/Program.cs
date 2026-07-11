using Konscious.Security.Cryptography;
using WinForge.Services;

internal static class Program
{
    private static readonly Guid Argon2dKdfUuid = new(new byte[]
        { 0xEF, 0x63, 0x6D, 0xDF, 0x8C, 0x29, 0x44, 0x4B, 0x91, 0xF7, 0xA9, 0xA4, 0x03, 0xE3, 0x0A, 0x0C });
    private static readonly Guid Argon2idKdfUuid = new(new byte[]
        { 0x9E, 0x29, 0x8B, 0x19, 0x56, 0xDB, 0x47, 0x73, 0xB2, 0x3D, 0xFC, 0x3E, 0xC6, 0xF0, 0xA1, 0xE6 });
    private static readonly byte[] CompositeKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] Salt = Enumerable.Range(0xA0, 16).Select(i => (byte)i).ToArray();
    private const ulong MemoryBytes = 32 * 1024;
    private const ulong Iterations = 3;
    private const uint Parallelism = 1;

    private static int Main()
    {
        var failures = new List<string>();
        var passed = 0;

        Run("Argon2d KDBX UUID keeps Argon2d derivation", Argon2dUuidUsesArgon2d);
        Run("Argon2id KDBX UUID selects Argon2id derivation", Argon2idUuidUsesArgon2id);
        Run("unknown Argon2 KDBX UUID is rejected", UnknownUuidIsRejected);
        Run("clipboard cleanup clears only the exact owned text", ClipboardCleanupRequiresExactOwnedText);

        if (failures.Count == 0)
        {
            Console.WriteLine($"PASS {passed}/{passed} KeePass crypto/clipboard tests");
            return 0;
        }

        foreach (var failure in failures) Console.Error.WriteLine(failure);
        Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} KeePass crypto/clipboard tests");
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
    }

    private static void Argon2dUuidUsesArgon2d()
    {
        byte[] actual = KeePassDatabase.DeriveArgon2ForKdf(Argon2dKdfUuid, CompositeKey, Salt, MemoryBytes, Iterations, Parallelism);
        byte[] expected = DeriveReference(new Argon2d(CompositeKey));
        AssertEqual(expected, actual, "Argon2d UUID produced the wrong transformed key");
    }

    private static void Argon2idUuidUsesArgon2id()
    {
        byte[] actual = KeePassDatabase.DeriveArgon2ForKdf(Argon2idKdfUuid, CompositeKey, Salt, MemoryBytes, Iterations, Parallelism);
        byte[] expectedId = DeriveReference(new Argon2id(CompositeKey));
        byte[] expectedD = DeriveReference(new Argon2d(CompositeKey));

        AssertEqual(expectedId, actual, "Argon2id UUID did not select Argon2id");
        Assert(!actual.SequenceEqual(expectedD), "Argon2id UUID silently used Argon2d");
    }

    private static void UnknownUuidIsRejected()
    {
        try
        {
            _ = KeePassDatabase.DeriveArgon2ForKdf(Guid.Empty, CompositeKey, Salt, MemoryBytes, Iterations, Parallelism);
            throw new InvalidOperationException("unknown KDF UUID was accepted");
        }
        catch (NotSupportedException)
        {
            // Expected: callers must not silently substitute a different Argon2 variant.
        }
    }

    private static void ClipboardCleanupRequiresExactOwnedText()
    {
        Assert(ClipboardOwnership.CanClearText("secret", "secret", 4, 4), "the owned secret should be eligible for cleanup");
        Assert(!ClipboardOwnership.CanClearText("secret", "replacement", 4, 4), "replacement clipboard text must never be cleared");
        Assert(!ClipboardOwnership.CanClearText("secret", null, 4, 4), "missing clipboard text must not be cleared");
        Assert(!ClipboardOwnership.CanClearText("", "", 4, 4), "an empty value is not an owned secret");
        Assert(!ClipboardOwnership.CanClearText("secret", "secret", 4, 5), "a stale cleanup generation must never clear a newer copy");
    }

    private static byte[] DeriveReference(Argon2 argon)
    {
        using (argon)
        {
            argon.Salt = Salt;
            argon.MemorySize = (int)(MemoryBytes / 1024); // KiB
            argon.Iterations = (int)Iterations;
            argon.DegreeOfParallelism = (int)Parallelism;
            return argon.GetBytes(32);
        }
    }

    private static void AssertEqual(byte[] expected, byte[] actual, string message)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
