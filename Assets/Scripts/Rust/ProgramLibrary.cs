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

    private static class Native {
        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_program_library(ref IntPtr ptr);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_owned_slice_program_library(
            ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern OwnedSliceReturn<ProgramEntryFFI> program_library_programs(IntPtr programLibrary);
    }

    public static void RustDrop(ref IntPtr ptr) {
        Native.drop_program_library(ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn) {
        Native.drop_owned_slice_program_library(ref ownedSliceReturn);
    }
}
}
