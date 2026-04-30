using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RustMod {
[StructLayout(LayoutKind.Sequential)]
public struct OwnedSliceReturn<T> : IDisposable, ISliceReturn<T> where T : IRustDrop<T> {
    private IntPtr _ptr;
    private UIntPtr _size;

    public IntPtr ItemPtr => _ptr;
    public UIntPtr ItemSize => _size;

    public void Dispose() {
        if (ItemPtr != IntPtr.Zero) {
            IRustDrop<T>.RustDropOwnedSlice(ref this);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct OwnedByteSliceReturn : IDisposable, ISliceReturn<byte> {
    private IntPtr _ptr;
    private UIntPtr _size;

    public IntPtr ItemPtr => _ptr;
    public UIntPtr ItemSize => _size;

    public void Dispose() {
        if (ItemPtr != IntPtr.Zero) {
            Native.drop_owned_slice_u8(ref this);
        }
    }

    private static class Native {
        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_owned_slice_u8(ref OwnedByteSliceReturn ownedSliceReturn);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct BorrowedSliceReturn<T> : ISliceReturn<T> {
    private IntPtr _ptr;
    private UIntPtr _size;

    public IntPtr ItemPtr => _ptr;
    public UIntPtr ItemSize => _size;
}

public interface ISliceReturn<T> {
    public IntPtr ItemPtr { get; }
    public UIntPtr ItemSize { get; }
}

public static class SliceReturnExtensions {
    public static IEnumerable<T> Items<T>(this ISliceReturn<T> slice) {
        var count = (int)slice.ItemSize;
        var size = Marshal.SizeOf<T>();

        for (var i = 0; i < count; i++) {
            var itemPtr = IntPtr.Add(slice.ItemPtr, i * size);
            yield return Marshal.PtrToStructure<T>(itemPtr)!;
        }
    }

    public static string ReadUtf8(this ISliceReturn<byte> slice) {
        return Encoding.UTF8.GetString(slice.ToArray());
    }

    public static byte[] ToArray(this ISliceReturn<byte> slice) {
        var count = (int)slice.ItemSize;
        var bytes = new byte[count];
        if (count > 0) {
            Marshal.Copy(slice.ItemPtr, bytes, 0, count);
        }
        return bytes;
    }
}
}
