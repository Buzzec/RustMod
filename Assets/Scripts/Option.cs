using System.Diagnostics.CodeAnalysis;

namespace RustMod {
public readonly struct Option<T> {
    public static Option<T> None => default;
    public static Option<T> Some(T value) => new Option<T>(value);

    public readonly bool isSome;
    private readonly T value;

    public Option(T value) {
        this.value = value;
        isSome = this.value is not null;
    }

    public bool IsSome(out T value) {
        value = this.value;
        return isSome;
    }
}
}