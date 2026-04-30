using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RustMod {
public sealed class WebServer : IDisposable, IRustDrop<WebServer> {
    private IntPtr _ptr;

    private IntPtr _checked_ptr => _ptr == IntPtr.Zero ? throw new NullReferenceException() : _ptr;

    public WebServer(string bindAddress = "127.0.0.1", ushort port = 3302) {
        _ptr = Native.start_program_webserver(bindAddress, port);

        if (_ptr == IntPtr.Zero) {
            throw new InvalidOperationException($"Failed to start Rust webserver on {bindAddress}:{port}");
        }
    }

    ~WebServer() {
        Dispose();
    }

    public UIntPtr ApplyPending() {
        return Native.webserver_apply_pending(_checked_ptr);
    }

    public ProgramLibrary ProgramLibrary() {
        return new ProgramLibrary(Native.webserver_program_library(_checked_ptr));
    }

    public IEnumerable<PendingUpload> DrainPendingUploads() {
        using var uploads = Native.webserver_drain_pending_uploads(_checked_ptr);
        foreach (var upload in uploads.Items()) {
            yield return upload.ToWrapper();
        }
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
        public static extern UIntPtr webserver_apply_pending(IntPtr webServer);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr webserver_program_library(IntPtr webServer);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern OwnedSliceReturn<PendingUploadFFI> webserver_drain_pending_uploads(IntPtr webServer);
    }

    public static void RustDrop(ref IntPtr ptr) {
        Native.drop_web_server(ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn) {
        Native.drop_owned_slice_web_server(ref ownedSliceReturn);
    }
}

public sealed class PendingUpload {
    public readonly string Name;
    public readonly byte[] Bytes;

    public PendingUpload(string name, byte[] bytes) {
        Name = name;
        Bytes = bytes;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct PendingUploadFFI : IRustDrop<PendingUploadFFI> {
    public OwnedByteSliceReturn name;
    public OwnedByteSliceReturn bytes;

    public PendingUpload ToWrapper() {
        return new PendingUpload(name.ReadUtf8(), bytes.ToArray());
    }

    private static class Native {
        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_pending_upload_entry(ref IntPtr ptr);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_owned_slice_pending_upload_entry(
            ref OwnedSliceReturn<PendingUploadFFI> ownedSliceReturn);
    }

    public static void RustDrop(ref IntPtr ptr) {
        Native.drop_pending_upload_entry(ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<PendingUploadFFI> ownedSliceReturn) {
        Native.drop_owned_slice_pending_upload_entry(ref ownedSliceReturn);
    }
}
}
