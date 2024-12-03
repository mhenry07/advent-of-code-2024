using System.Buffers.Text;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var example1Bytes = "xmul(2,4)%&mul[3,7]!@^do_not_mul(5,5)+mul(32,64]then(mul(11,8)mul(8,5))"u8;
var example2Bytes = "xmul(2,4)&mul[3,7]!^don't()_mul(5,5)+mul(32,64](mul(11,8)undo()?mul(8,5))"u8;

var bytes = useExample switch
{
    1 => example1Bytes,
    2 => example2Bytes,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var @do = "do()"u8;
var dont = "don't()"u8;
var mul = "mul("u8;

var enabled2 = true;
var total1 = 0;
var total2 = 0;
for (var i = 0; i < bytes.Length; i++)
{
    if (bytes[i..].StartsWith(@do))
    {
        enabled2 = true;
        i += @do.Length - 1;
        continue;
    }

    if (bytes[i..].StartsWith(dont))
    {
        enabled2 = false;
        i += dont.Length - 1;
        continue;
    }

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

    total1 += a * b;
    if (enabled2)
        total2 += a * b;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Part 2 answer: {total2}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

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
