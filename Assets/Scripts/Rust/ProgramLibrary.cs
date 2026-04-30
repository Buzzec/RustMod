using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RustMod {
public sealed class ProgramLibrary : IDisposable, IRustDrop<ProgramLibrary> {
    private IntPtr _ptr = RustFFI.Call(Native.new_program_library);

    private IntPtr _checked_ptr => _ptr == IntPtr.Zero ? throw new NullReferenceException() : _ptr;
    internal IntPtr NativePtr => _checked_ptr;

    ~ProgramLibrary() {
        Dispose();
    }

    public void Dispose() {
        if (_ptr != IntPtr.Zero) {
            RustDrop(ref _ptr);
        }
    }

    public IEnumerable<ProgramEntry> Programs() {
        using var programs = RustFFI.Call(() => Native.program_library_programs(_checked_ptr));

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
        public static extern IntPtr new_program_library();

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern OwnedSliceReturn<ProgramEntryFFI> program_library_programs(IntPtr programLibrary);
    }

    public static void RustDrop(ref IntPtr ptr) {
        RustFFI.Call(Native.drop_program_library, ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<ProgramLibrary> ownedSliceReturn) {
        RustFFI.Call(Native.drop_owned_slice_program_library, ref ownedSliceReturn);
    }
}
}
