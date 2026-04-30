using System;
using System.Runtime.InteropServices;

namespace RustMod {
public sealed class Execution : IDisposable, IRustDrop<Execution> {
    private IntPtr _ptr;

    private IntPtr _checked_ptr => _ptr == IntPtr.Zero ? throw new NullReferenceException() : _ptr;

    public ulong ProgramKey => Native.execution_program_key(_checked_ptr);

    public Execution(ProgramLibrary library, ulong programKey) {
        _ptr = Native.execution_new(library.NativePtr, programKey);
        if (_ptr == IntPtr.Zero) {
            throw new InvalidOperationException($"Failed to create execution for program {programKey}");
        }
    }

    private Execution(IntPtr ptr) {
        _ptr = ptr == IntPtr.Zero ? throw new InvalidOperationException("Failed to deserialize execution") : ptr;
    }

    ~Execution() {
        Dispose();
    }

    public ExecutionStepResult Execute(ulong clocks, ProgramLibrary library) {
        var result = Native.execution_execute(_checked_ptr, clocks, library.NativePtr);
        try {
            return result.ToWrapper();
        } finally {
            Native.drop_execution_step_result(ref result);
        }
    }

    public byte[] Serialize() {
        using var data = Native.execution_serialize(_checked_ptr);
        return data.ToArray();
    }

    public static Execution Deserialize(byte[] data) {
        data ??= Array.Empty<byte>();
        return new Execution(Native.execution_deserialize(data, (UIntPtr)data.Length));
    }

    public void Dispose() {
        if (_ptr != IntPtr.Zero) {
            RustDrop(ref _ptr);
        }
    }

    private static class Native {
        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_execution(ref IntPtr execution);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_owned_slice_execution(ref OwnedSliceReturn<Execution> ownedSliceReturn);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr execution_new(IntPtr library, ulong programKey);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ExecutionStepResultFFI execution_execute(IntPtr execution, ulong clocks, IntPtr library);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern OwnedByteSliceReturn execution_serialize(IntPtr execution);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr execution_deserialize(byte[] data, UIntPtr len);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong execution_program_key(IntPtr execution);

        [DllImport(RustFFI.DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void drop_execution_step_result(ref ExecutionStepResultFFI result);
    }

    public static void RustDrop(ref IntPtr ptr) {
        Native.drop_execution(ref ptr);
    }

    public static void RustDropOwnedSlice(ref OwnedSliceReturn<Execution> ownedSliceReturn) {
        Native.drop_owned_slice_execution(ref ownedSliceReturn);
    }
}

public enum ExecutionStatus {
    Ok = 0,
    OutOfClocks = 1,
    Exit = 2,
    Hcf = 3,
    Yield = 4,
    Sleep = 5,
    Log = 6,
    Error = 255,
}

public readonly struct ExecutionStepResult {
    public readonly ExecutionStatus Status;
    public readonly ulong Value;
    public readonly string Message;

    public ExecutionStepResult(ExecutionStatus status, ulong value, string message) {
        Status = status;
        Value = value;
        Message = message;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct ExecutionStepResultFFI {
    public ExecutionStatus status;
    public ulong value;
    public OwnedByteSliceReturn message;

    public ExecutionStepResult ToWrapper() {
        return new ExecutionStepResult(status, value, message.ReadUtf8());
    }
}
}
