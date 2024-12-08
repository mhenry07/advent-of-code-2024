using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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

var degreeOfParallelism = Environment.ProcessorCount - 1;
var mapData = MapData.FromInput(bytes, out var guard, out var numObstructions);
var guardMovements = new BlockingCollection<GuardMovement>(mapData.Width * mapData.Height);

// part 2 (parallelized)
var executor2 = new ExecutorPart2(mapData, guardMovements, numObstructions);
var tasks = new Task[degreeOfParallelism];
for (var i = 0; i < degreeOfParallelism; i++)
    tasks[i] = Task.Run(executor2.Execute);

// part 1
var guardHash = new HashSet<Position>(bytes.Length);
var guardPrevious = guard;
var guardStart = guard;
var map = new Map(mapData);
map.MarkPath(guard.X, guard.Y);
guardHash.Add(guard.Position);
var total1 = 1;
while (map.TryMove(ref guard))
{
    map.MarkPath(guard.X, guard.Y);
    if (guardHash.Add(guard.Position))
    {
        guardMovements.Add(new(guardPrevious, guard.Position));
        total1++;
    }

    guardPrevious = guard;
}

guardMovements.CompleteAdding();
var elapsed1 = TimeProvider.System.GetElapsedTime(start);

Task.WaitAll(tasks);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Part 2 answer: {executor2.Total}");
Console.WriteLine($"Part 2 attempted moves: {executor2.AttemptedMoves:N0}, turns: {executor2.AttemptedTurns:N0}");
Console.WriteLine($"Part 1 elapsed: {elapsed1.TotalMilliseconds:N3} ms");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

class ExecutorPart2(MapData mapData, BlockingCollection<GuardMovement> guardMovements, int numObstructions)
{
    int _attemptedMoves;
    int _attemptedTurns;
    long _total;
    readonly BlockingCollection<GuardMovement> _guardMovements = guardMovements;
    readonly MapData _mapData = mapData;

    // it's theoretically possible to hit an obstruction from all 4 sides
    // but with the given input, loops can be detected correctly even with `maxTurns = (numObstructions + 1) / 4`
    readonly int _maxTurns = (numObstructions + 1) * 4;

    public int AttemptedMoves => _attemptedMoves;
    public int AttemptedTurns => _attemptedTurns;
    public long Total => _total;

    public void Execute()
    {
        var attemptedMoves = 0;
        var attemptedTurns = 0;
        var guardMovements = _guardMovements;
        var loopHashCapacity = _maxTurns / 16; // use a more practical initial capacity
        var loopHash = new HashSet<Guard>(loopHashCapacity);
        var map = new ReadOnlyMap(_mapData);
        var total = 0;
        while (!guardMovements.IsCompleted)
        {
            if (guardMovements.TryTake(out var guardMovement)
                && TestMovementsWithNewObstruction(
                    in map, in guardMovement, loopHash, ref attemptedMoves, ref attemptedTurns))
            {
                total++;
            }
        }

        Interlocked.Add(ref _attemptedMoves, attemptedMoves);
        Interlocked.Add(ref _attemptedTurns, attemptedTurns);
        Interlocked.Add(ref _total, total);
    }

    static bool TestMovementsWithNewObstruction(
        in ReadOnlyMap map, in GuardMovement start, HashSet<Guard> loopHash, ref int attemptedMoves,
        ref int attemptedTurns)
    {
        //var builder = new StringBuilder($"Thread {Environment.CurrentManagedThreadId}:\r\n");
        var newObstruction = start.Next;

        //builder.AppendLine($"Testing obstruction at {newObstruction.X}, {newObstruction.Y}");

        var guard = start.Guard;
        //builder.AppendLine($"Guard position: {guard.X}, {guard.Y}, {guard.Direction}");

        var isLoop = false;
        var lastDirection = guard.Direction;
        var numTurns = 0;
        while (map.TryMoveFast(ref guard, in newObstruction))
        {
            Debug.Assert(guard.X >= 0 && guard.Y >= 0);
            attemptedMoves++;

            //builder.AppendLine($"- Moved to {guard.X}, {guard.Y}");
            if (guard.Direction != lastDirection)
            {
                //builder.AppendLine($"- Turned at {guard.X}, {guard.Y}, {guard.Direction} (turn # {numTurns + 1})");
                lastDirection = guard.Direction;
                attemptedTurns++;
                numTurns++;
            }

            if (!loopHash.Add(guard))
            {
                isLoop = true;
                //builder.AppendLine($"Loop found at {newObstruction.X}, {newObstruction.Y}");
                break;
            }
        }

        loopHash.Clear();

        //Console.WriteLine(builder);

        return isLoop;
    }
}

class MapData
{
    const byte GuardUp = (byte)'^';
    const byte Obstruction = Map.Obstruction;

    public static MapData FromInput(ReadOnlySpan<byte> source, out Guard guard, out int numObstructions)
    {
        guard = default;
        numObstructions = 0;

        byte[] columnOrder = [];
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
                columnOrder = new byte[width * height];
            }

            line.CopyTo(rowOrder.AsSpan(y * width));
            for (var x = 0; x < width; x++)
            {
                var value = line[x];
                columnOrder[x * height + y] = value;
                switch (value)
                {
                    case GuardUp:
                        guard = new Guard((short)x, (short)y, Direction.Up);
                        break;
                    case Obstruction:
                        numObstructions++;
                        break;
                }
            }

            y++;
        }

        return new MapData
        {
            ColumnOrder = columnOrder,
            RowOrder = rowOrder,
            Height = height,
            Width = width
        };
    }

    public required byte[] ColumnOrder { get; init; }
    public required byte[] RowOrder { get; init; }
    public int Height { get; init; }
    public int Width { get; init; }
}

struct Map(MapData mapData)
{
    public const byte Obstruction = (byte)'#';
    const byte NewObstruction = (byte)'O';
    const byte Visited = (byte)'X';

    // copy source array to avoid mutating shared data
    private readonly byte[] _rowOrder = mapData.RowOrder.ToArray();

    public int Height { get; } = mapData.Height;
    public int Width { get; } = mapData.Width;

    // optimization of x >= 0 && x < width && y >= 0 && y < height
    public readonly bool IsInBounds(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public void MarkPath(int x, int y)
        => Set(x, y, Visited);

    public byte Set(int x, int y, byte value)
    {
        var rowIndex = GetRowIndex(x, y);
        var previous = _rowOrder[rowIndex];
        _rowOrder[rowIndex] = value;
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

        var rowIndex = GetRowIndex(x, y);
        result = _rowOrder[rowIndex];
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly int GetRowIndex(int x, int y) => y * Width + x;

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

struct ReadOnlyMap(MapData mapData)
{
    const byte Obstruction = Map.Obstruction;
    private readonly byte[] _columnOrder = mapData.ColumnOrder;
    private readonly byte[] _rowOrder = mapData.RowOrder;

    public int Height { get; } = mapData.Height;
    public int Width { get; } = mapData.Width;

    // this can be used when we don't need to mark the path
    public readonly bool TryMoveFast(ref Guard guard, in Position altObstruction)
    {
        int index, altIndex;
        switch (guard.Direction)
        {
            case Direction.Up:
                index = GetColumnEndAtSpan(guard.X, guard.Y).LastIndexOf(Obstruction);
                altIndex = guard.X == altObstruction.X && guard.Y > altObstruction.Y
                    ? altObstruction.Y
                    : -1;

                return TryMoveUp(index, altIndex, ref guard);

            case Direction.Down:
                index = GetColumnStartAtSpan(guard.X, guard.Y).IndexOf(Obstruction);
                altIndex = guard.X == altObstruction.X && guard.Y < altObstruction.Y
                    ? altObstruction.Y
                    : -1;

                return TryMoveDown(index, altIndex, ref guard);

            case Direction.Left:
                index = GetRowEndAtSpan(guard.X, guard.Y).LastIndexOf(Obstruction);
                altIndex = guard.Y == altObstruction.Y && guard.X > altObstruction.X
                    ? altObstruction.X
                    : -1;

                return TryMoveLeft(index, altIndex, ref guard);

            case Direction.Right:
                index = GetRowStartAtSpan(guard.X, guard.Y).IndexOf(Obstruction);
                altIndex = guard.Y == altObstruction.Y && guard.X < altObstruction.X
                    ? altObstruction.X
                    : -1;

                return TryMoveRight(index, altIndex, ref guard);

            default:
                throw new InvalidOperationException();
        }
    }

    private static bool TryMoveUp(int obstructionIndex, int altIndex, ref Guard guard)
    {
        var index = Math.Max(obstructionIndex, altIndex);
        if (index == -1)
            return false;

        guard.Direction = Direction.Right;
        guard.Y = (short)(index + 1);
        return true;
    }

    private static bool TryMoveDown(int obstructionIndex, int altIndex, ref Guard guard)
    {
        var index = (obstructionIndex, altIndex) switch
        {
            (-1, _) => altIndex,
            (_, -1) => guard.Y + obstructionIndex,
            (_, _) => Math.Min(guard.Y + obstructionIndex, altIndex)
        };
        if (index == -1)
            return false;

        guard.Direction = Direction.Left;
        guard.Y = (short)(index - 1);
        return true;
    }

    private static bool TryMoveLeft(int obstructionIndex, int altIndex, ref Guard guard)
    {
        var index = Math.Max(obstructionIndex, altIndex);
        if (index == -1)
            return false;

        guard.Direction = Direction.Up;
        guard.X = (short)(index + 1);
        return true;
    }

    private static bool TryMoveRight(int obstructionIndex, int altIndex, ref Guard guard)
    {
        var index = (obstructionIndex, altIndex) switch
        {
            (-1, _) => altIndex,
            (_, -1) => guard.X + obstructionIndex,
            (_, _) => Math.Min(guard.X + obstructionIndex, altIndex)
        };
        if (index == -1)
            return false;

        guard.Direction = Direction.Down;
        guard.X = (short)(index - 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ReadOnlySpan<byte> GetColumnEndAtSpan(int x, int y)
        => _columnOrder.AsSpan(x * Height, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ReadOnlySpan<byte> GetColumnStartAtSpan(int x, int y)
        => _columnOrder.AsSpan(x * Height + y, Height - y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ReadOnlySpan<byte> GetRowEndAtSpan(int x, int y)
        => _rowOrder.AsSpan(y * Width, x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ReadOnlySpan<byte> GetRowStartAtSpan(int x, int y)
        => _rowOrder.AsSpan(y * Width + x, Width - x);
}

enum Direction : byte
{
    None,
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
