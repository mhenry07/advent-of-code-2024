using System.Buffers;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
............
........0...
.....0......
.......0....
....0.......
......A.....
............
............
........A...
.........A..
............
............
"""u8.ToArray();

var exampleBytes2 = """
..........
..........
..........
....a.....
..........
.....a....
..........
..........
..........
..........
"""u8.ToArray();

var exampleBytes3 = """
..........
..........
..........
....a.....
........a.
.....a....
..........
..........
..........
..........
"""u8.ToArray();

var exampleBytes4 = """
..........
..........
..........
....a.....
........a.
.....a....
..........
......A...
..........
..........
"""u8.ToArray();

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    3 => exampleBytes3,
    4 => exampleBytes4,
    _ => File.ReadAllBytes("input.txt")
};

var mapData = MapData.FromInput(bytes, out var antennaFrequencies);
var antinodes = new HashSet<Position>(antennaFrequencies.Length);

foreach (var frequency in antennaFrequencies.AsSpan())
{
    if (frequency is null)
        continue;

    var span = frequency.Span;
    for (var i = 0; i < span.Length - 1; i++)
    {
        var a = span[i];
        for (var j = i + 1; j < span.Length; j++)
        {
            var b = span[j];
            a.GetAntinodes(in b, out var antinode1, out var antinode2);
            if (mapData.IsInBounds(in antinode1))
                antinodes.Add(antinode1);

            if (mapData.IsInBounds(in antinode2))
                antinodes.Add(antinode2);
        }
    }
}

var total1 = antinodes.Count;
var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

class MapData
{
    static readonly SearchValues<byte> AntennaSearchValues =
        SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    public static MapData FromInput(ReadOnlySpan<byte> source, out PoolableList<Antenna>[] antennaFrequencies)
    {
        antennaFrequencies = new PoolableList<Antenna>[128];
        byte[] rowOrder = [];
        var height = 0;
        var width = 0;
        var y = 0;
        foreach (var range in source.Split("\r\n"u8))
        {
            var line = source[range];
            if (line.IsEmpty)
                break;

            if (y == 0)
            {
                width = line.Length;
                height = (int)Math.Ceiling(source.Length / (width + 2.0));
                rowOrder = new byte[width * height];
            }

            line.CopyTo(rowOrder.AsSpan(y * width));
            for (var x = 0; x < width; x++)
            {
                var value = line[x];
                if (AntennaSearchValues.Contains(value))
                {
                    ref PoolableList<Antenna> group = ref antennaFrequencies[value];
                    if (group is null)
                        group = new PoolableList<Antenna>();

                    group.Add(new(value, new((sbyte)x, (sbyte)y)));
                }
            }

            y++;
        }

        return new MapData
        {
            RowOrder = rowOrder,
            Height = height,
            Width = width
        };
    }

    public required byte[] RowOrder { get; init; }
    public int Height { get; init; }
    public int Width { get; init; }

    public bool IsInBounds(in Position position)
        => (uint)position.X < (uint)Width && (uint)position.Y < (uint)Height;
}

record struct Antenna(byte Frequency, Position Position)
{
    public readonly void GetAntinodes(in Antenna other, out Position antinode1, out Position antinode2)
    {
        var otherPosition = other.Position;
        Position.GetSegment(in otherPosition, out var dx, out var dy);
        antinode1 = new((sbyte)(otherPosition.X + dx), (sbyte)(otherPosition.Y + dy));
        antinode2 = new((sbyte)(Position.X - dx), (sbyte)(Position.Y - dy));
    }
}

record struct Position(sbyte X, sbyte Y)
{
    public readonly void GetSegment(in Position other, out int x, out int y)
    {
        x = other.X - X;
        y = other.Y - Y;
    }
}
