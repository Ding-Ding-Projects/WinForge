using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DewEncryption.Core;

public sealed class DewSecretService
{
    private const int CryptProtectUiForbidden = 0x1;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DewEncryption.DewDrive.Password.v1");

    public bool IsSupported => OperatingSystem.IsWindows();

    public string ProtectForCurrentUser(string secret)
    {
        if (string.IsNullOrEmpty(secret) || !IsSupported)
        {
            return string.Empty;
        }

        byte[] clearBytes = Encoding.UTF8.GetBytes(secret);
        DataBlob input = CreateBlob(clearBytes);
        DataBlob entropy = CreateBlob(Entropy);
        DataBlob output = default;
        try
        {
            if (!CryptProtectData(ref input, "Dew Drive password", ref entropy, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return Convert.ToBase64String(ReadBlob(output));
        }
        finally
        {
            FreeHGlobal(input);
            FreeHGlobal(entropy);
            FreeLocal(output);
        }
    }

    public string UnprotectForCurrentUser(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret) || !IsSupported)
        {
            return string.Empty;
        }

        byte[] protectedBytes;
        try
        {
            protectedBytes = Convert.FromBase64String(protectedSecret);
        }
        catch (FormatException)
        {
            return string.Empty;
        }

        DataBlob input = CreateBlob(protectedBytes);
        DataBlob entropy = CreateBlob(Entropy);
        DataBlob output = default;
        IntPtr description = IntPtr.Zero;
        try
        {
            if (!CryptUnprotectData(ref input, out description, ref entropy, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output))
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(ReadBlob(output));
        }
        finally
        {
            FreeHGlobal(input);
            FreeHGlobal(entropy);
            FreeLocal(output);
            if (description != IntPtr.Zero)
            {
                LocalFree(description);
            }
        }
    }

    private static DataBlob CreateBlob(byte[] data)
    {
        IntPtr pointer = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, pointer, data.Length);
        return new DataBlob(data.Length, pointer);
    }

    private static byte[] ReadBlob(DataBlob blob)
    {
        if (blob.DataPointer == IntPtr.Zero || blob.DataLength <= 0)
        {
            return [];
        }

        byte[] data = new byte[blob.DataLength];
        Marshal.Copy(blob.DataPointer, data, 0, data.Length);
        return data;
    }

    private static void FreeHGlobal(DataBlob blob)
    {
        if (blob.DataPointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(blob.DataPointer);
        }
    }

    private static void FreeLocal(DataBlob blob)
    {
        if (blob.DataPointer != IntPtr.Zero)
        {
            LocalFree(blob.DataPointer);
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? dataDescription,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        out IntPtr dataDescription,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DataBlob(int dataLength, IntPtr dataPointer)
    {
        public readonly int DataLength = dataLength;
        public readonly IntPtr DataPointer = dataPointer;
    }
}
