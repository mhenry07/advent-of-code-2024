using System.Buffers;

namespace Core;

public sealed class PoolableList<T>(int minimumLength = PoolableList<T>.MinLength) : IDisposable
{
    public const int MinLength = 16;
    private T[] _buffer = ArrayPool<T>.Shared.Rent(Math.Max(minimumLength, MinLength));
    private int _length;

    public void Add(T item)
    {
        if (_length == _buffer.Length)
        {
            ObjectDisposedException.ThrowIf(_buffer.Length == 0, this);

            var old = _buffer;
            _buffer = ArrayPool<T>.Shared.Rent(old.Length * 2);
            old.AsSpan().CopyTo(_buffer);
            ArrayPool<T>.Shared.Return(old);
        }

        _buffer[_length++] = item;
    }

    public int Length => _length;

    public Span<T> Span => _buffer.AsSpan(0, _length);

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_buffer);
        _buffer = [];
        _length = 0;
    }
}
