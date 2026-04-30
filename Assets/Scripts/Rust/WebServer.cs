using System;
using System.Runtime.InteropServices;

namespace RustMod {
public sealed class WebServer : IDisposable, IRustDrop<WebServer> {
    private IntPtr _ptr;

    private IntPtr _checked_ptr => _ptr == IntPtr.Zero ? throw new NullReferenceException() : _ptr;

    public WebServer(string bindAddress = "127.0.0.1", ushort port = 3302) {
        _ptr = RustFFI.Call(() => Native.start_program_webserver(bindAddress, port));

        if (_ptr == IntPtr.Zero) {
            throw new InvalidOperationException($"Failed to start Rust webserver on {bindAddress}:{port}");
        }
    }

    ~WebServer() {
        Dispose();
    }

    public UIntPtr ApplyPending(ProgramLibrary programLibrary) {
        return RustFFI.Call(() => Native.webserver_apply_pending(_checked_ptr, programLibrary.NativePtr));
    }

    public void Dispose() {
        if (_ptr != IntPtr.Zero) {
            RustDrop(ref _ptr);
        }
    }

    private static class Native {
        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_web_server(ref IntPtr webServer);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_owned_slice_web_server(
            ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr start_program_webserver(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string bindAddress,
            ushort port);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr webserver_apply_pending(IntPtr webServer, IntPtr programLibrary);
    }

    public static void RustDrop(ref IntPtr ptr) {
        RustFFI.Call(Native.drop_web_server, ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn) {
        RustFFI.Call(Native.drop_owned_slice_web_server, ref ownedSliceReturn);
    }
}
}
