using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace RustMod {
public static class RustFFI {
    public const string DllName = "stationeers_emu";
    private const string DllFileName = DllName + ".dll";
    private const string NativePayloadFileName = DllName + ".native";

    public delegate void RefAction<T>(ref T value);

    private static IntPtr _nativeLibrary;
    private static readonly Native.DebugLogCallback DebugLogCallback = LogFromRust;
    private static readonly ConcurrentQueue<DebugLogEntry> PendingLogs = new();
    private static DebugLogPump _debugLogPump;

    public static void EnsureLoaded() {
        if (_nativeLibrary != IntPtr.Zero) {
            EnsureDebugLogPump();
            return;
        }

        EnsureDebugLogPump();

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

        Native.install_debug_logger(DebugLogCallback);
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

    private static void LogFromRust(byte level, IntPtr messagePtr, UIntPtr messageLen) {
        var message = DecodeUtf8(messagePtr, messageLen);
        PendingLogs.Enqueue(new DebugLogEntry(level, message));
    }

    private static string DecodeUtf8(IntPtr messagePtr, UIntPtr messageLen) {
        if (messagePtr == IntPtr.Zero || messageLen == UIntPtr.Zero) {
            return string.Empty;
        }

        var length = (int)messageLen;
        var bytes = new byte[length];
        Marshal.Copy(messagePtr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void EnsureDebugLogPump() {
        if (_debugLogPump != null) {
            return;
        }

        var gameObject = new GameObject("Rust Debug Log Pump") {
            hideFlags = HideFlags.HideAndDontSave
        };
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        _debugLogPump = gameObject.AddComponent<DebugLogPump>();
    }

    private readonly struct DebugLogEntry {
        public readonly byte Level;
        public readonly string Message;

        public DebugLogEntry(byte level, string message) {
            Level = level;
            Message = message;
        }
    }

    private sealed class DebugLogPump : MonoBehaviour {
        private void Update() {
            while (PendingLogs.TryDequeue(out var entry)) {
                switch (entry.Level) {
                    case 0:
                        Debug.LogError($"[{RustMod.PluginName}] {entry.Message}");
                        break;
                    case 1:
                        Debug.LogWarning($"[{RustMod.PluginName}] {entry.Message}");
                        break;
                    default:
                        Debug.Log($"[{RustMod.PluginName}] {entry.Message}");
                        break;
                }
            }
        }
    }

    private static class Native {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public delegate void DebugLogCallback(byte level, IntPtr message, UIntPtr messageLen);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void install_debug_logger(DebugLogCallback callback);
    }
}
}