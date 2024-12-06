using System.Buffers;

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
short i = 0;
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
        guard = new Guard((short)index, i, Direction.Up);

    for (var j = 0; j < line.Length; j++)
        if (line[j] == Map.Obstruction)
            numObstructions++;

    i++;
}

var guardHash = new HashSet<Position>(bytes.Length);
var guardPositions = new Stack<Guard>(bytes.Length);
var guardStart = guard;
var map = new Map(bytes, lineRanges);
map.MarkPath(guard.X, guard.Y);
guardHash.Add(guard.Position);
guardPositions.Push(guard);
while (map.TryMove(ref guard))
{
    map.MarkPath(guard.X, guard.Y);
    if (guardHash.Add(guard.Position))
        guardPositions.Push(guard);
}

var total1 = guardPositions.Count;

var attemptedMoves2 = 0L;
var attemptedTurns2 = 0L;
Span<byte> bytes2 = new byte[bytes.Length];
bytes.CopyTo(bytes2);
var map2 = new Map(bytes2, lineRanges);
// it's theoretically possible to hit an obstruction from all 4 sides
// but with the given input, I still get the correct answer even with `maxTurns = (numObstructions + 1) / 4`
var maxTurns = (numObstructions + 1) * 4;
var loopHash = new HashSet<Guard>(maxTurns);
var total2 = 0;
while (guardPositions.TryPop(out var guardPosition) && guardPositions.TryPeek(out var guard2))
{
    var position = guardPosition.Position;
    if (position.X == guardStart.X && position.Y == guardStart.Y)
        continue;

    if (!map2.TryAddObstruction(position.X, position.Y, out var previous))
        continue;

    //Console.WriteLine($"Attempting to add obstruction at {position.X}, {position.Y}");

    //guard2 = guardStart; // testing
    var isLoop = false;
    var lastDirection = guard2.Direction;
    var numTurns = 0;
    while (map2.TryMoveFast(ref guard2))
    {
        attemptedMoves2++;

        //Console.WriteLine($"Moved to {guard2.X}, {guard2.Y}");
        if (guard2.Direction != lastDirection)
        {
            //Console.WriteLine($"Turned at {guard2.X}, {guard2.Y}, {guard2.Direction} (turn # {numTurns + 1})");
            lastDirection = guard2.Direction;
            numTurns++;
            attemptedTurns2++;
        }

        if (!loopHash.Add(guard2))
        {
            isLoop = true;
            //Console.WriteLine($"Loop found at {position.X}, {position.Y}");
            break;
        }
    }

    if (isLoop)
        total2++;

    loopHash.Clear();
    map2.Set(position.X, position.Y, previous);
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

var expected = useExample ? 6 : 2162;
if (total2 != expected)
    Console.WriteLine($"Wrong answer. Expected: {expected}, Actual: {total2}");

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Part 2 answer: {total2}");
Console.WriteLine($"Part 2 attempted moves: {attemptedMoves2:N0}, turns: {attemptedTurns2:N0}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

ref struct Map
{
    public const byte Obstruction = (byte)'#';
    const byte NewObstruction = (byte)'O';
    const byte Visited = (byte)'X';
    private static readonly SearchValues<byte> ObstacleSearchValues = SearchValues.Create("#O"u8);
    private readonly Span<byte> _bytes;
    private readonly Span<byte[]> _columnBytes;
    private readonly ReadOnlySpan<Range> _lineRanges;

    public Map(Span<byte> bytes, ReadOnlySpan<Range> lineRanges)
    {
        _bytes = bytes;
        _lineRanges = lineRanges;
        Height = lineRanges.Length;
        Width = bytes[lineRanges[0]].Length;

        _columnBytes = new byte[Width][];
        for (var i = 0; i < _columnBytes.Length; i++)
        {
            Span<byte> column = _columnBytes[i] = new byte[Height];
            for (var j = 0; j < column.Length; j++)
                column[j] = bytes[lineRanges[j]][i];
        }
    }

    public int Height { get; }
    public int Width { get; }

    public readonly bool IsInBounds(int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height;

    public void MarkPath(int x, int y)
        => Set(x, y, Visited);

    public byte Set(int x, int y, byte value)
    {
        var row = _bytes[_lineRanges[y]];
        var previous = row[x];
        row[x] = value;
        _columnBytes[x][y] = value;
        return previous;
    }

    public bool TryAddObstruction(int x, int y, out byte previous)
    {
        // it's only relevant to add new obstructions to previously visited positions
        if (TryGet(x, y, out var result) && result == Visited)
        {
            previous = Set(x, y, NewObstruction);
            return true;
        }

        previous = default;
        return false;
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
            guard.Direction = TurnRight(guard.Direction);
            return true;
        }

        guard.X = (short)nextX;
        guard.Y = (short)nextY;
        return true;
    }

    // this can be used when we don't need to mark the path
    public readonly bool TryMoveFast(ref Guard guard)
    {
        Span<byte> column;
        Span<byte> row;
        int index;
        switch (guard.Direction)
        {
            case Direction.Up:
                column = _columnBytes[guard.X];
                index = column[..guard.Y].LastIndexOfAny(ObstacleSearchValues);
                if (index == -1)
                    return false;

                guard.Direction = Direction.Right;
                guard.Y = (short)(index + 1);
                return true;

            case Direction.Down:
                column = _columnBytes[guard.X];
                index = column[guard.Y..].IndexOfAny(ObstacleSearchValues);
                if (index == -1)
                    return false;

                guard.Direction = Direction.Left;
                guard.Y += (short)(index - 1);
                return true;

            case Direction.Left:
                row = _bytes[_lineRanges[guard.Y]];
                index = row[..guard.X].LastIndexOfAny(ObstacleSearchValues);
                if (index == -1)
                    return false;

                guard.Direction = Direction.Up;
                guard.X = (short)(index + 1);
                return true;

            case Direction.Right:
                row = _bytes[_lineRanges[guard.Y]];
                index = row[guard.X..].IndexOfAny(ObstacleSearchValues);
                if (index == -1)
                    return false;

                guard.Direction = Direction.Down;
                guard.X += (short)(index - 1);
                return true;

            default:
                throw new InvalidOperationException();
        }
    }

    public static Direction TurnRight(Direction direction)
        => direction switch
        {
            Direction.Up => Direction.Right,
            Direction.Right => Direction.Down,
            Direction.Down => Direction.Left,
            Direction.Left => Direction.Up,
            _ => throw new InvalidOperationException("Unexpected direction")
        };
}

enum Direction : byte
{
    Up,
    Down,
    Left,
    Right
}

record struct Guard(short X, short Y, Direction Direction)
{
    public readonly Position Position => new(X, Y);
}

record struct Position(short X, short Y);
