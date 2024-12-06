var start = TimeProvider.System.GetTimestamp();

bool useExample = false;
var exampleBytes = """
....#.....
.........#
..........
..#.......
.......#..
..........
.#..^.....
........#.
#.........
......#...
"""u8.ToArray().AsSpan();

var bytes = useExample switch
{
    true => exampleBytes,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

const byte GuardUp = (byte)'^';

Guard guard = default;
var i = 0;
Span<Range> lineRanges = default;
foreach (var lineRange in MemoryExtensions.Split(bytes, "\r\n"u8))
{
    var line = bytes[lineRange];
    if (line.IsEmpty)
        break;

    if (i == 0)
    {
        var width = line.Length;
        var height = (int)Math.Ceiling(bytes.Length / (width + 2.0));
        lineRanges = new Range[height];
    }

    lineRanges[i] = lineRange;

    var index = line.IndexOf(GuardUp);
    if (index != -1)
        guard = new Guard(index, i, Direction.Up);

    i++;
}

var map = new Map(bytes, lineRanges);
map.MarkPath(guard.X, guard.Y);
while (map.TryMove(ref guard))
{
    map.MarkPath(guard.X, guard.Y);
}

var total1 = map.CountVisitedPositions();

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

ref struct Map
{
    const byte Obstacle = (byte)'#';
    const byte Visited = (byte)'X';
    private readonly Span<byte> _bytes;
    private readonly ReadOnlySpan<Range> _lineRanges;

    public Map(Span<byte> bytes, ReadOnlySpan<Range> lineRanges)
    {
        _bytes = bytes;
        _lineRanges = lineRanges;
        Height = lineRanges.Length;
        Width = bytes[lineRanges[0]].Length;
    }

    public int Height { get; }
    public int Width { get; }

    public readonly int CountVisitedPositions()
    {
        var total = 0;
        foreach (var b in _bytes)
            if (b == Visited)
                total++;

        return total;
    }

    public readonly bool IsInBounds(int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height;

    public void MarkPath(int x, int y)
    {
        var line = _bytes[_lineRanges[y]];
        line[x] = Visited;
    }

    public readonly bool TryMove(ref Guard guard)
    {
        var nextX = guard.X + guard.Direction switch
        {
            Direction.Left => -1,
            Direction.Right => 1,
            _ => 0
        };
        var nextY = guard.Y + guard.Direction switch
        {
            Direction.Up => -1,
            Direction.Down => 1,
            _ => 0
        };

        if (!TryGet(nextX, nextY, out var result))
            return false;

        if (result == Obstacle)
        {
            guard.Direction = guard.Direction switch
            {
                Direction.Up => Direction.Right,
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => throw new InvalidOperationException("Unexpected direction")
            };
            return true;
        }

        guard.X = nextX;
        guard.Y = nextY;
        return true;
    }

    public readonly bool TryGet(int x, int y, out byte result)
    {
        result = default;
        if (!IsInBounds(x, y))
            return false;

        var line = _bytes[_lineRanges[y]];
        result = line[x];
        return true;
    }
}

enum Direction
{
    Up,
    Down,
    Left,
    Right
}

record struct Guard(int X, int Y, Direction Direction);
