using System.Buffers.Text;

var useExample = false;
var exampleBytes = "xmul(2,4)%&mul[3,7]!@^do_not_mul(5,5)+mul(32,64]then(mul(11,8)mul(8,5))"u8;

var bytes = useExample
    ? exampleBytes
    : File.ReadAllBytes("input.txt").AsSpan();

var total = 0;
for (var i = 0; i < bytes.Length; i++)
{
    var mul = "mul("u8;
    if (!bytes[i..].StartsWith(mul))
        continue;

    i += mul.Length;
    if (!Utf8Parser.TryParse(bytes[i..], out int a, out int aLength))
        continue;

    i += aLength;
    if (!TryReadByte(bytes, i, out var comma) || comma != (byte)',')
        continue;

    i++;
    if (!Utf8Parser.TryParse(bytes[i..], out int b, out int bLength))
        continue;

    i += bLength;
    if (!TryReadByte(bytes, i, out var closeParen) || closeParen != (byte)')')
        continue;

    total += a * b;
}

Console.WriteLine($"Part 1 answer: {total}");

static bool TryReadByte(ReadOnlySpan<byte> bytes, int index, out byte result)
{
    if (index >= bytes.Length - 1)
    {
        result = default;
        return false;
    }

    result = bytes[index];
    return true;
}
