using System;
using System.Runtime.InteropServices;

namespace RustMod {
public class ProgramEntry {
    public readonly ulong key;
    public readonly string name;

    public ProgramEntry(ulong key, string name) {
        this.key = key;
        this.name = name;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct ProgramEntryFFI : IRustDrop<ProgramEntryFFI> {
    public ulong key;
    public BorrowedSliceReturn<byte> name;

    public ProgramEntry ToWrapper() {
        return new ProgramEntry(key, name.ReadUtf8());
    }

    private static class Native {
        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_program_entry(ref IntPtr ptr);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_owned_slice_program_entry(
            ref OwnedSliceReturn<ProgramEntryFFI> ownedSliceReturn);
    }

    public static void RustDrop(ref IntPtr ptr) {
        RustFFI.Call(Native.drop_program_entry, ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<ProgramEntryFFI> ownedSliceReturn) {
        RustFFI.Call(Native.drop_owned_slice_program_entry, ref ownedSliceReturn);
    }
}
}
