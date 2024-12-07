using System.Buffers;
using System.Collections.Concurrent;

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
"""u8.ToArray();

var bytes = useExample switch
{
    true => exampleBytes,
    _ => File.ReadAllBytes("input.txt")
};

const byte GuardUp = (byte)'^';

Guard guard = default;
short i = 0;
Range[] lineRanges = [];
int numObstructions = 0;
var bytesSpan = bytes.AsSpan();
foreach (var lineRange in MemoryExtensions.Split(bytesSpan, "\r\n"u8))
{
    var line = bytesSpan[lineRange];
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

var attemptedMoves2 = 0L;
var attemptedTurns2 = 0L;
var degreeOfParallelism = Environment.ProcessorCount - 1;
var guardMovements = new BlockingCollection<GuardMovement>(bytes.Length);
// it's theoretically possible to hit an obstruction from all 4 sides
// but with the given input, loops can be detected correctly even with `maxTurns = (numObstructions + 1) / 4`
var maxTurns = (numObstructions + 1) * 4;
var total2 = 0;
var tasks = Enumerable.Range(0, degreeOfParallelism).Select(_ => Task.Run(() =>
{
    var attemptedMoves = 0L;
    var attemptedTurns = 0L;
    var bytesCopy = bytes.ToArray();
    var loopHash = new HashSet<Guard>(maxTurns / 16); // use a more practical initial capacity
    var map = new Map(bytesCopy, lineRanges);
    var total = 0;
    while (!guardMovements.IsCompleted)
    {
        if (guardMovements.TryTake(out var guardMovement)
            && TestMovementsWithNewObstacle(in map, in guardMovement, loopHash, ref attemptedMoves, ref attemptedTurns))
            total++;
    }

    Interlocked.Add(ref attemptedMoves2, attemptedMoves);
    Interlocked.Add(ref attemptedTurns2, attemptedTurns);
    Interlocked.Add(ref total2, total);
})).ToArray();

var guardHash = new HashSet<Position>(bytes.Length);
var guardPositions = new Stack<Guard>(bytes.Length);
var guardPrevious = guard;
var guardStart = guard;
var map = new Map(bytes, lineRanges);
map.MarkPath(guard.X, guard.Y);
guardHash.Add(guard.Position);
guardPositions.Push(guard);
while (map.TryMove(ref guard))
{
    map.MarkPath(guard.X, guard.Y);
    if (guardHash.Add(guard.Position))
    {
        guardMovements.Add(new(guardPrevious, guard.Position));
        guardPositions.Push(guard);
    }
    guardPrevious = guard;
}

guardMovements.CompleteAdding();
var total1 = guardPositions.Count;
var elapsed1 = TimeProvider.System.GetElapsedTime(start);


Task.WaitAll(tasks);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Part 2 answer: {total2}");
Console.WriteLine($"Part 2 attempted moves: {attemptedMoves2:N0}, turns: {attemptedTurns2:N0}");
Console.WriteLine($"Part 1 elapsed: {elapsed1.TotalMilliseconds:N3} ms");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool TestMovementsWithNewObstacle(
    in Map map, in GuardMovement start, HashSet<Guard> loopHash, ref long attemptedMoves, ref long attemptedTurns)
{
    var position = start.Next;
    if (!map.TryAddObstruction2(position.X, position.Y, out var previous))
        return false;

    //Console.WriteLine($"Attempting to add obstruction at {position.X}, {position.Y}");

    var guard = start.Guard;
    var isLoop = false;
    var lastDirection = guard.Direction;
    while (map.TryMoveFast(ref guard))
    {
        attemptedMoves++;

        //Console.WriteLine($"Moved to {guard.X}, {guard.Y}");
        if (guard.Direction != lastDirection)
        {
            //Console.WriteLine($"Turned at {guard.X}, {guard.Y}, {guard.Direction} (turn # {numTurns + 1})");
            lastDirection = guard.Direction;
            attemptedTurns++;
        }

        if (!loopHash.Add(guard))
        {
            isLoop = true;
            //Console.WriteLine($"Loop found at {position.X}, {position.Y}");
            break;
        }
    }

    loopHash.Clear();
    map.Set(position.X, position.Y, previous);

    return isLoop;
}

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

    public bool TryAddObstruction2(int x, int y, out byte previous)
    {
        // it's only relevant to add new obstructions to previously visited positions
        if (TryGet(x, y, out _)) // when parallelizing, board state is stale so we don't have all visited positions
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

record struct GuardMovement(Guard Guard, Position Next);

record struct Position(short X, short Y);
