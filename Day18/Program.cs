using System.Buffers.Text;
using Core;

var startTimestamp = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    5,4
    4,2
    4,5
    3,0
    2,1
    6,3
    2,4
    1,5
    0,6
    3,3
    2,6
    5,1
    1,2
    5,5
    2,5
    6,5
    1,4
    0,4
    6,4
    1,1
    6,1
    1,0
    0,5
    1,6
    2,0
    """u8.ToArray();

var input = useExample switch
{
    1 => (Bytes: exampleBytes1, Height: 7, Width: 7, SimulationBytes: 12),
    _ => (Bytes: File.ReadAllBytes("input.txt"), Height: 71, Width: 71, SimulationBytes: 1024)
};

var start = new Position(0, 0);
var goal = new Position((byte)(input.Width - 1), (byte)(input.Height - 1));

using var fallingBytes = new PoolableList<Position>(input.Bytes.Length / 5);
var inputBytes = input.Bytes.AsSpan();
foreach (var range in MemoryExtensions.Split(inputBytes, "\r\n"u8))
{
    var line = inputBytes[range];
    if (line.IsEmpty)
        continue;

    var separatorIndex = line.IndexOf((byte)',');
    if (separatorIndex >= 0
        && Utf8Parser.TryParse(line[..separatorIndex], out byte x, out _)
        && Utf8Parser.TryParse(line[(separatorIndex + 1)..], out byte y, out _))
    {
        fallingBytes.Add(new Position(x, y));
    }
}

Span<byte> rows1 = new byte[input.Width * input.Height];
rows1.Fill((byte)'.');
var rowOrder1 = new RowOrderSpan<byte>(rows1, input.Width, input.Height);
var fallingSpan = fallingBytes.Span;
for (var i = 0; i < input.SimulationBytes; i++)
{
    var position = fallingSpan[i];
    rowOrder1.GetRef(position.X, position.Y) = (byte)'#';
}

var total1 = AStar(in rowOrder1, in start, in goal);

var blockPosition2 = default(Position);
Span<byte> rows2 = rows1.ToArray();
var rowOrder2 = new RowOrderSpan<byte>(rows2, input.Width, input.Height);
for (var i = input.SimulationBytes; i < fallingSpan.Length; i++)
{
    var position = fallingSpan[i];
    rowOrder2.GetRef(position.X, position.Y) = (byte)'#';

    var steps = AStar(in rowOrder2, in start, in goal);
    if (!steps.HasValue)
    {
        Console.WriteLine($"blocked by fallingSpan[{i}] = {position.X},{position.Y}");
        blockPosition2 = position;
        break;
    }
}

var elapsed = TimeProvider.System.GetElapsedTime(startTimestamp);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Part 2: {blockPosition2.X},{blockPosition2.Y}");
Console.WriteLine($"Processed {input.Bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

// adapted from https://en.wikipedia.org/wiki/A*_search_algorithm
static int? AStar(in RowOrderSpan<byte> rowOrder, in Position start, in Position goal)
{
    var width = rowOrder.Width;
    var height = rowOrder.Height;
    var fStart = Heuristic(in start, in goal);
    var startNode = MoveNode.Start(start.X, start.Y);
    rowOrder.TryGetIndex(start.X, start.Y, out var startIndex);
    var open = new PriorityQueue<MoveNode, int>([(startNode, fStart)]);
    var openSet = new HashSet<Position>([start]);

    // cost of cheapest known path from start to n
    var gScores = new RowOrderSpan<int>(new int[width * height], width, height);
    var gSpan = gScores.Span;
    gSpan.Fill(int.MaxValue);
    gSpan[startIndex] = 0;

    // currently known cheapest path from start to end through n
    var fScores = new RowOrderSpan<int>(new int[width * height], width, height);
    var fSpan = fScores.Span;
    fSpan.Fill(int.MaxValue);
    fSpan[startIndex] = fStart;

    Span<MoveNode> neighbors = new MoveNode[4];
    while (open.TryDequeue(out var current, out _))
    {
        openSet.Remove(current.Position);
        if (current.Position == goal)
        {
            fScores.TryGet(current.X, current.Y, out int fScore);
            //Console.WriteLine($"fScores@({current.X},{current.Y}): {fScore}");
            return fScore;
        }

        gScores.TryGet(current.X, current.Y, out var gCurrent);

        var nLength = current.GetNeighbors(in rowOrder, neighbors);
        foreach (var neighbor in neighbors[..nLength])
        {
            rowOrder.TryGetIndex(neighbor.X, neighbor.Y, out var nIndex);
            var gTentative = gCurrent + 1;
            if (gTentative < gSpan[nIndex])
            {
                var h = Heuristic(neighbor.Position, in goal);
                gSpan[nIndex] = gTentative;
                fSpan[nIndex] = gTentative + h;

                if (openSet.Add(neighbor.Position))
                    open.Enqueue(neighbor, gTentative + h);
            }
        }
    }

    return null;
}

// using taxi cab distance
static int Heuristic(in Position position, in Position goal)
    => Math.Abs(goal.X - position.X) + Math.Abs(goal.Y - position.Y);

record MoveNode(byte X, byte Y, MoveNode? Previous)
{
    const byte Obstacle = (byte)'#';
    static readonly (sbyte dx, sbyte dy)[] Neighbors = [(0, -1), (0, 1), (-1, 0), (1, 0)];

    public static MoveNode Start(byte x, byte y)
        => new(x, y, Previous: null);

    public MoveNode Step(sbyte dx, sbyte dy)
        => new((byte)(X + dx), (byte)(Y + dy), Previous: this);

    public Position Position => new(X, Y);

    public int GetNeighbors(in RowOrderSpan<byte> rowOrder, Span<MoveNode> neighbors)
    {
        var length = 0;
        foreach (var (dx, dy) in Neighbors)
        {
            var x = X + dx;
            var y = Y + dy;
            if (rowOrder.TryGet(x, y, out var cell) && cell != Obstacle)
                neighbors[length++] = Step(dx, dy);
        }

        return length;
    }
}

record struct Position(byte X, byte Y);
