using System.Diagnostics.CodeAnalysis;

namespace Core;

public readonly ref struct RowOrderSpan<T>
{
    public RowOrderSpan(Span<T> span, int width, int height)
    {
        if (span.Length != width * height)
            throw new ArgumentException("Span length must be width * height");

        Span = span;
        Width = width;
        Height = height;
    }

    public Span<T> Span { get; }
    public int Width { get; }
    public int Height { get; }

    public bool IsInBounds(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public bool TryGet(int x, int y, [NotNullWhen(true)] out T? value)
    {
        var result = TryGetIndex(x, y, out var index);
        value = result
            ? Span[index]
            : default;

        return result;
    }

    public bool TryGetIndex(int x, int y, out int index)
    {
        var inBounds = IsInBounds(x, y);
        index = inBounds
            ? y * Width + x
            : -1;

        return inBounds;
    }

    public ref T GetRef(int x, int y)
    {
        if (TryGetIndex(x, y, out var index))
            return ref Span[index];

        throw new ArgumentException("Requested position is out of bounds");
    }
}
