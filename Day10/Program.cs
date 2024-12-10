using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
0123
1234
8765
9876
"""u8.ToArray();

var exampleBytes2 = """
...0...
...1...
...2...
6543456
7.....7
8.....8
9.....9
"""u8.ToArray();

var exampleBytes3 = """
..90..9
...1.98
...2..7
6543456
765.987
876....
987....
"""u8.ToArray();

var exampleBytes4 = """
10..9..
2...8..
3...7..
4567654
...8..3
...9..2
.....01
"""u8.ToArray();

var exampleBytes5 = """
89010123
78121874
87430965
96549874
45678903
32019012
01329801
10456732
"""u8.ToArray();

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    3 => exampleBytes3,
    4 => exampleBytes4,
    5 => exampleBytes5,
    _ => File.ReadAllBytes("input.txt")
};

var mapData = MapData.FromInput(bytes, out var trailheadCollection, out var numPeaks);

var reachablePeaks = new HashSet<Position>(numPeaks);
var total1 = 0;
var trailheads = trailheadCollection.Span;
foreach (var trailhead in trailheads)
{
    total1 += GetTrailScore(mapData, in trailhead, reachablePeaks);
    reachablePeaks.Clear();
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static int GetTrailScore(MapData mapData, in TopoPosition position, HashSet<Position> reachablePeaks)
{
    if (position.Elevation == MapData.Peak)
        return reachablePeaks.Add(position.Position) ? 1 : 0;

    var score = 0;
    var nextElevation = (byte)(position.Elevation + 1);

    var north = position.Y - 1;
    if (mapData.TryGetElevation(position.X, north, out var checkElevation) && checkElevation == nextElevation)
        score += GetTrailScore(mapData, new TopoPosition(position.X, (sbyte)north, nextElevation), reachablePeaks);

    var south = position.Y + 1;
    if (mapData.TryGetElevation(position.X, south, out checkElevation) && checkElevation == nextElevation)
        score += GetTrailScore(mapData, new TopoPosition(position.X, (sbyte)south, nextElevation), reachablePeaks);

    var east = position.X + 1;
    if (mapData.TryGetElevation(east, position.Y, out checkElevation) && checkElevation == nextElevation)
        score += GetTrailScore(mapData, new TopoPosition((sbyte)east, position.Y, nextElevation), reachablePeaks);

    var west = position.X - 1;
    if (mapData.TryGetElevation(west, position.Y, out checkElevation) && checkElevation == nextElevation)
        score += GetTrailScore(mapData, new TopoPosition((sbyte)west, position.Y, nextElevation), reachablePeaks);

    return score;
}

class MapData
{
    public const byte Peak = (byte)'9';
    public const byte Trailhead = (byte)'0';

    public static MapData FromInput(
        ReadOnlySpan<byte> source, out PoolableList<TopoPosition> trailheads, out int numPeaks)
    {
        trailheads = new();
        numPeaks = 0;

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
                switch (value)
                {
                    case Trailhead:
                        trailheads.Add(new((sbyte)x, (sbyte)y, value));
                        break;
                    case Peak:
                        numPeaks++;
                        break;
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

    public bool TryGetElevation(int x, int y, out byte elevation)
    {
        elevation = default;
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return false;

        elevation = RowOrder[Width * y + x];
        return true;
    }
}

record struct Position(sbyte X, sbyte Y);

record struct TopoPosition(sbyte X, sbyte Y, byte Elevation)
{
    public readonly Position Position => new(X, Y);
}
