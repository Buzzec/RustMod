using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RustMod {
public sealed class ProgramLibrary : IDisposable, IRustDrop<ProgramLibrary> {
    private IntPtr _ptr;

    private IntPtr _checked_ptr => _ptr == IntPtr.Zero ? throw new NullReferenceException() : _ptr;
    internal IntPtr NativePtr => _checked_ptr;

    internal ProgramLibrary(IntPtr ptr) {
        _ptr = ptr == IntPtr.Zero ? throw new InvalidOperationException("Failed to get Rust program library") : ptr;
    }

    public ProgramLibrary() : this(Native.program_library_new()) { }

    ~ProgramLibrary() {
        Dispose();
    }

    public void Dispose() {
        if (_ptr != IntPtr.Zero) {
            RustDrop(ref _ptr);
        }
    }

    public IEnumerable<ProgramEntry> Programs() {
        using var programs = Native.program_library_programs(_checked_ptr);

        foreach (var program in programs.Items()) {
            yield return program.ToWrapper();
        }
    }

    public byte[] Serialize() {
        using var data = Native.program_library_serialize(_checked_ptr);
        return data.ToArray();
    }

    public bool Deserialize(byte[] data) {
        data ??= Array.Empty<byte>();
        return Native.program_library_deserialize(_checked_ptr, data, (UIntPtr)data.Length);
    }

    public ulong AddElf(string name, byte[] data) {
        data ??= Array.Empty<byte>();
        return Native.program_library_add_elf(_checked_ptr, name ?? string.Empty, data, (UIntPtr)data.Length);
    }

    public bool Delete(ulong key) {
        return Native.program_library_delete(_checked_ptr, key);
    }

    public bool Rename(ulong key, string name) {
        return Native.program_library_rename(_checked_ptr, key, name ?? string.Empty);
    }

    private static class Native {
        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_program_library(ref IntPtr ptr);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_owned_slice_program_library(
            ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr program_library_new();

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern OwnedSliceReturn<ProgramEntryFFI> program_library_programs(IntPtr programLibrary);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern OwnedByteSliceReturn program_library_serialize(IntPtr programLibrary);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool program_library_deserialize(
            IntPtr programLibrary,
            byte[] data,
            UIntPtr len);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong program_library_add_elf(
            IntPtr programLibrary,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            byte[] data,
            UIntPtr len);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool program_library_delete(IntPtr programLibrary, ulong key);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool program_library_rename(
            IntPtr programLibrary,
            ulong key,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    }

    public static void RustDrop(ref IntPtr ptr) {
        Native.drop_program_library(ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn) {
        Native.drop_owned_slice_program_library(ref ownedSliceReturn);
    }
}
}
