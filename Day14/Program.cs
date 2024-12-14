using System.Buffers.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
p=0,4 v=3,-3
p=6,3 v=-1,-3
p=10,3 v=-1,2
p=2,0 v=2,-1
p=0,0 v=1,3
p=3,0 v=-2,-2
p=7,6 v=-1,-3
p=3,0 v=-1,-2
p=9,3 v=2,3
p=7,3 v=-1,2
p=2,4 v=2,-3
p=9,5 v=-3,-3
"""u8.ToArray();

var (bytesArray, area) = useExample switch
{
    1 => (exampleBytes1, new Area(11, 7)),
    _ => (File.ReadAllBytes("input.txt"), new Area(101, 103))
};

using var robots = new PoolableList<Robot>();
var bytes = bytesArray.AsSpan();
foreach (var range in MemoryExtensions.Split(bytes, "\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
        continue;

    if (!TryParsePair(line, "p="u8, out var px, out var py, out var pLength)
        || !TryParsePair(line[pLength..], " v="u8, out var vx, out var vy, out _))
        throw new FormatException();

    var position = new Position(px, py);
    var velocity = new Velocity(vx, vy);
    var robot = new Robot(position, velocity);

    robots.Add(robot);
}

const int numSeconds = 100;
var ul = 0;
var ur = 0;
var ll = 0;
var lr = 0;
var i = 0;
foreach (var robot in robots.Span)
{
    Console.WriteLine($"Moving robot {i}");
    var quadrant = robot.Move(in area, numSeconds);
    Console.WriteLine($"  X: {robot.Position.X}, Y: {robot.Position.Y}, Quadrant: {quadrant}");
    switch (quadrant)
    {
        case Quadrant.UpperLeft:
            ul++;
            break;
        case Quadrant.UpperRight:
            ur++;
            break;
        case Quadrant.LowerLeft:
            ll++;
            break;
        case Quadrant.LowerRight:
            lr++;
            break;
    }

    i++;
}

var total1 = ul * ur * ll * lr;

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Safety factor: {total1}");
//Console.WriteLine($"Part 2: Safety factor: {total2}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool TryParsePair(
    ReadOnlySpan<byte> source, ReadOnlySpan<byte> prefix, out int x, out int y, out int bytesConsumed)
{
    x = 0;
    y = 0;
    bytesConsumed = 0;

    var i = 0;
    if (!source.StartsWith(prefix))
        return false;

    i += prefix.Length;
    if (!Utf8Parser.TryParse(source[i..], out x, out int xLength))
        return false;

    i += xLength;
    if (!source[i..].StartsWith((byte)','))
        return false;

    i++;
    if (!Utf8Parser.TryParse(source[i..], out y, out int yLength))
        return false;

    i += yLength;
    bytesConsumed = i;
    return true;
}

record struct Area(int Width, int Height)
{
    private readonly int _midX = Width / 2;
    private readonly int _midY = Height / 2;

    public readonly Quadrant GetQuadrant(in Position position)
    {
        if (position.X == _midX || position.Y == _midY)
            return Quadrant.None;

        var left = position.X < _midX;
        var upper = position.Y < _midY;
        return (left, upper) switch
        {
            (true, true) => Quadrant.UpperLeft,
            (true, false) => Quadrant.LowerLeft,
            (false, true) => Quadrant.UpperRight,
            (false, false) => Quadrant.LowerRight
        };
    }
}

record struct Position(int X, int Y);
record struct Velocity(int X, int Y);

struct Robot(Position position, Velocity velocity)
{
    private Position _position = position;

    public readonly Position Position => _position;
    public readonly Velocity Velocity { get; } = velocity;

    public Quadrant Move(in Area area, int seconds)
    {
        var x = (int)((_position.X + (long)Velocity.X * seconds) % area.Width);
        if (x < 0)
            x += area.Width;

        var y = (int)((_position.Y + (long)Velocity.Y * seconds) % area.Height);
        if (y < 0)
            y += area.Height;

        _position = new(x, y);
        return area.GetQuadrant(in _position);
    }
}

enum Quadrant
{
    None,
    UpperLeft,
    UpperRight,
    LowerLeft,
    LowerRight
}
