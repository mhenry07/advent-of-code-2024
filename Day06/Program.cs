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
int numObstructions = 0;
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

    for (var j = 0; j < line.Length; j++)
        if (line[j] == Map.Obstruction)
            numObstructions++;

    i++;
}

var guardStart = guard;
var map = new Map(bytes, lineRanges);
map.MarkPath(guard.X, guard.Y);
while (map.TryMove(ref guard))
{
    map.MarkPath(guard.X, guard.Y);
}

var total1 = map.CountVisitedPositions();

Span<byte> bytes2 = new byte[bytes.Length];
var maxTurns = (numObstructions + 1) * 4;
var total2 = 0;
for (i = 0; i < map.Height; i++)
{
    for (var j = 0; j < map.Width; j++)
    {
        if (j == guardStart.X && i == guardStart.Y)
            continue;

        if (!map.ShouldAddObstruction(j, i))
            continue;

        //Console.WriteLine($"Attempting to add obstruction at {j}, {i}");
        bytes.CopyTo(bytes2);
        var map2 = new Map(bytes2, lineRanges);
        map2.AddObstruction(j, i);

        var guard2 = guardStart;
        var lastDirection = guard2.Direction;
        var numTurns = 0;
        var isLoop = false;
        while (map2.TryMove(ref guard2))
        {
            //Console.WriteLine($"Moved to {guard2.X}, {guard2.Y}");
            if (guard2.Direction != lastDirection)
            {
                //Console.WriteLine($"Turned at {guard2.X}, {guard2.Y}, {guard2.Direction} (turn # {numTurns + 1})");
                lastDirection = guard2.Direction;
                numTurns++;
            }

            if (numTurns > maxTurns)
            {
                isLoop = true;
                break;
            }
        }

        if (isLoop)
            total2++;
    }
}


var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Part 2 answer: {total2}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

ref struct Map
{
    public const byte Obstruction = (byte)'#';
    const byte NewObstruction = (byte)'O';
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

    public void AddObstruction(int x, int y)
        => _bytes[_lineRanges[y]][x] = NewObstruction;

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

    // it's only relevant to add new obstructions to previously visited positions
    public readonly bool ShouldAddObstruction(int x, int y)
        => TryGet(x, y, out var result) && result == Visited;

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

        if (result == Obstruction || result == NewObstruction)
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
