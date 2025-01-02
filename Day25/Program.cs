using System.Diagnostics;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    #####
    .####
    .####
    .####
    .#.#.
    .#...
    .....

    #####
    ##.##
    .#.##
    ...##
    ...#.
    ...#.
    .....

    .....
    #....
    #....
    #...#
    #.#.#
    #.###
    #####

    .....
    .....
    #.#..
    ###..
    ###.#
    ###.#
    #####

    .....
    .....
    .....
    #....
    #.#..
    #.#.#
    #####
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

const int MaxHeight = 5;

Span<byte> buffer = stackalloc byte[MaxHeight];
using var keys = new PoolableList<Key>();
using var locks = new PoolableList<Lock>();
var section = 0;
var sectionRow = 0;
var type = Type.Unknown;
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
    {
        var heights = new byte[buffer.Length];
        switch (type)
        {
            case Type.Key:
                for (var i = 0; i < heights.Length; i++)
                    heights[i] = (byte)(MaxHeight - buffer[i]);

                var key = new Key(heights);
                keys.Add(key);
                Console.WriteLine($"Key: {key}");
                break;
            case Type.Lock:
                buffer.CopyTo(heights);
                var @lock = new Lock(heights);
                locks.Add(@lock);
                Console.WriteLine($"Lock: {@lock}");
                break;
        }

        buffer.Clear();
        section++;
        sectionRow = 0;
        continue;
    }

    if (line.Length != buffer.Length)
        throw new InvalidDataException();

    if (sectionRow == 0)
    {
        type = Type.Unknown;
        if (line.SequenceEqual("....."u8))
            type = Type.Key;
        else if (line.SequenceEqual("#####"u8))
            type = Type.Lock;

        sectionRow++;
        continue;
    }
    else
    {
        for (var i = 0; i < line.Length; i++)
        {
            switch (type)
            {
                case Type.Key:
                    if (line[i] == (byte)'.')
                        buffer[i]++;
                    break;
                case Type.Lock:
                    if (line[i] == (byte)'#')
                        buffer[i]++;
                    break;
            }
        }
    }
}

var locksSpan = locks.Span;
var total1 = 0;
foreach (var key in keys.Span)
{
    foreach (var @lock in locksSpan)
    {
        if (!key.Overlaps(in @lock))
            total1++;
    }
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
record struct Key(byte[] Heights)
{
    const byte MaxHeight = 5;

    public readonly bool Overlaps(in Lock @lock)
    {
        if (Heights.Length != @lock.Heights.Length)
            return true;

        var keyHeights = Heights.AsSpan();
        var lockHeights = @lock.Heights.AsSpan();
        var length = Heights.Length;
        Span<byte> sums = stackalloc byte[length];
        for (var i = 0; i < length; i++)
        {
            sums[i] = (byte)(keyHeights[i] + lockHeights[i]);
            if (sums[i] > MaxHeight)
                return true;
        }

        return false;
    }

    private readonly string GetDebuggerDisplay() => ToString();
    public override readonly string ToString() => string.Join(',', Heights);
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
record struct Lock(byte[] Heights)
{
    private readonly string GetDebuggerDisplay() => ToString();
    public override readonly string ToString() => string.Join(',', Heights);
}

enum Type
{
    Unknown,
    Key,
    Lock
}
