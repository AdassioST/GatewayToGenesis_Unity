using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

/// <summary>Windows user-scoped DPAPI protects the authentication key at rest. No embedded universal secret.</summary>
public static class SaveKeyProtection
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int size; public IntPtr data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    public static byte[] Protect(byte[] value) => Transform(value, true);
    public static byte[] Unprotect(byte[] value) => Transform(value, false);
    private static byte[] Transform(byte[] value, bool protect)
    {
        // Other targets must supply a platform keystore before release. Fail closed, never store a plaintext key.
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException("Save key protection currently supports Windows.");
        var input = new Blob { size = value.Length, data = Marshal.AllocHGlobal(value.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(value, 0, input.data, value.Length);
            bool ok = protect ? CryptProtectData(ref input, "Arcanoria world identity", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot access this Windows user's save identity key.");
            var result = new byte[output.size]; Marshal.Copy(output.data, result, 0, result.Length); return result;
        }
        finally { Marshal.FreeHGlobal(input.data); if (output.data != IntPtr.Zero) LocalFree(output.data); }
    }
}
