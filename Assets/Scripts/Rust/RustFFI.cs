using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RustMod {
public static class RustFFI {
    public const string DllName = "stationeers_emu";
    private const string DllFileName = DllName + ".dll";
    private const string NativePayloadFileName = DllName + ".native";

    public delegate void RefAction<T>(ref T value);

    private static IntPtr _nativeLibrary;

    public static T Call<T>(Func<T> nativeCall) {
        EnsureLoaded();
        return nativeCall();
    }

    public static void Call<T>(RefAction<T> nativeCall, ref T value) {
        EnsureLoaded();
        nativeCall(ref value);
    }

    private static void EnsureLoaded() {
        if (_nativeLibrary != IntPtr.Zero) {
            return;
        }

        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(assemblyDirectory)) {
            throw new InvalidOperationException("Unable to determine RustMod assembly directory.");
        }

        var nativePayloadPath = Path.Combine(assemblyDirectory, "Native", NativePayloadFileName);
        if (!File.Exists(nativePayloadPath)) {
            throw new DllNotFoundException($"Native Rust payload was not found: '{nativePayloadPath}'");
        }

        var nativeDllPath = Path.Combine(
            Path.GetTempPath(),
            "StationeersRustMod",
            DllFileName);
        CopyPayloadToDll(nativePayloadPath, nativeDllPath);

        _nativeLibrary = LoadLibrary(nativeDllPath);
        if (_nativeLibrary == IntPtr.Zero) {
            var error = Marshal.GetLastWin32Error();
            throw new DllNotFoundException($"Failed to load native Rust DLL '{nativeDllPath}'. Win32 error: {error}");
        }
    }

    private static void CopyPayloadToDll(string nativePayloadPath, string nativeDllPath) {
        Directory.CreateDirectory(Path.GetDirectoryName(nativeDllPath)!);

        var sourceInfo = new FileInfo(nativePayloadPath);
        var targetInfo = new FileInfo(nativeDllPath);
        if (targetInfo.Exists &&
            targetInfo.Length == sourceInfo.Length &&
            targetInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc) {
            return;
        }

        File.Copy(nativePayloadPath, nativeDllPath, true);
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);
}
}
