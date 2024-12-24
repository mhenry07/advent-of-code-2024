using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Core;

public class Utf8ByteStringEqualityComparer
    : IAlternateEqualityComparer<ReadOnlySpan<byte>, string>, IEqualityComparer<string>
{
    public string Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public bool Equals(ReadOnlySpan<byte> alternate, string other)
    {
        if (alternate.Length != other.Length)
            return false;

        for (var i = 0; i < alternate.Length; i++)
            if (alternate[i] != other[i])
                return false;

        return true;
    }

    public bool Equals(string? x, string? y) => x == y;

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        var hashCode = new HashCode();
        hashCode.AddBytes(alternate);
        return hashCode.ToHashCode();
    }

    /// <remarks>Only intended for small strings</remarks>
    public int GetHashCode([DisallowNull] string obj)
    {
        if (obj.Length > 256)
            throw new ArgumentOutOfRangeException(nameof(obj));

        Span<byte> bytes = stackalloc byte[obj.Length];
        Encoding.UTF8.GetBytes(obj, bytes);

        return GetHashCode(bytes);
    }
}
