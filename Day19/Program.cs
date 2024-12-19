using System.Diagnostics.CodeAnalysis;
using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    r, wr, b, g, bwu, rb, gb, br

    brwrr
    bggr
    gbbr
    rrbgbr
    ubwu
    bwurrg
    brgr
    bbrgwb
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

using var designRanges = new PoolableList<Range>();
using var patternRanges = new PoolableList<byte[]>();
var section = 0;
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
    {
        section++;
        continue;
    }

    switch (section)
    {
        // towel patterns
        case 0:
            foreach (var patternRange in line.Split(", "u8))
                patternRanges.Add(line[patternRange].ToArray());

            break;

        // desired designs
        case 1:
            designRanges.Add(range);
            break;
    }
}

var memoized = new Dictionary<byte[], long>(new ByteSpanEqualityComparer());
var lookup = memoized.GetAlternateLookup<ReadOnlySpan<byte>>();
var patterns = patternRanges.Span;
var total1 = 0L;
var total2 = 0L;
foreach (var range in designRanges.Span)
{
    var design = bytes[range];
    //Console.WriteLine($"Attempting design: {Encoding.UTF8.GetString(design)}");

    var combinations = CountCombinations(design, patterns, lookup);
    if (combinations > 0)
        total1++;

    total2 += combinations;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Part 2: {total2}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static long CountCombinations(
    ReadOnlySpan<byte> design, ReadOnlySpan<byte[]> patterns,
    Dictionary<byte[], long>.AlternateLookup<ReadOnlySpan<byte>> memoized)
{
    if (design.IsEmpty)
        return 1;

    if (memoized.TryGetValue(design, out var combinations))
        return combinations;

    var count = 0L;
    foreach (var pattern in patterns)
        if (design.StartsWith(pattern))
            count += CountCombinations(design[pattern.Length..], patterns, memoized);

    if (count > 0)
        memoized[design] = count;

    return count;
}

class ByteSpanEqualityComparer : IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>, IEqualityComparer<byte[]>
{
    public byte[] Create(ReadOnlySpan<byte> alternate) => [.. alternate];

    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

    public bool Equals(byte[]? x, byte[]? y)
    {
        if (x is null ^ y is null)
            return false;

        if (x is null && y is null)
            return true;

        return Equals(x.AsSpan(), y!);
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        var hashCode = new HashCode();
        hashCode.AddBytes(alternate);
        return hashCode.ToHashCode();
    }

    public int GetHashCode([DisallowNull] byte[] obj) => GetHashCode(obj.AsSpan());
}
