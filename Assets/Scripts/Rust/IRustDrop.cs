using System;

namespace RustMod {
public interface IRustDrop<T> where T : IRustDrop<T> {
    static void RustDrop(ref IntPtr ptr) => throw new NotSupportedException();
    static void RustDropOwnedSlice(ref OwnedSliceReturn<T> ownedSliceReturn) => throw new NotSupportedException();
}
}
