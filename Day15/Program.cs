using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
##########
#..O..O.O#
#......O.#
#.OO..O.O#
#..O@..O.#
#O#..O...#
#O..O..O.#
#.OO.O.OO#
#....O...#
##########

<vv>^<v^>v>^vv^v>v<>v^v<v<^vv<<<^><<><>>v<vvv<>^v^>^<<<><<v<<<v^vv^v>^
vvv<<^>^v^^><<>>><>^<<><^vv^^<>vvv<>><^^v>^>vv<>v<<<<v<^v>^<^^>>>^<v<v
><>vv>v^v^<>><>>>><^^>vv>v<^^^>>v^v^<^^>v^^>v^<^v>v<>>v^v^<v>v^^<^^vv<
<<v<^>>^^^^>>>v^<>vvv^><v<<<>^^^vv^<vvv>^>v<^^^^v<>^>vvvv><>>v^<<^^^^^
^><^><>>><>^^<<^^v>>><^<v>^<vv>>v>>>^v><>^v><<<<v>>v<v<v>vvv>^<><<>^><
^>><>^v<><^vvv<^^<><v<<<<<><^v<<<><<<^^<v<^^^><^>>^<v^><<<^>>^v<v^v<v^
>^>>^v>vv>^<<^v<>><<><<v<<v><>v<^vv<<<>^^v^>^^>>><<^v>>v^v><^^>>^<>vv^
<><^^>^^^<><vvvvv^v<v<<>^v<v>v<<^><<><<><<<^^<<<^<<>><<><^^^>^^<>^>v<>
^^>vv<^v^v<vv>^<><v<^v>^^^>>>^^vvv^>vvv<>>>^<^>>>>>^<<^v>^vvv<>^<><<v>
v^^>>><<^^<>>^v^<v^vv<>v^<<>^<^v^v><^<<<><<^<v><v<>vv>>v><v^<vv<>v^<<^
"""u8;

var exampleBytes2 = """
########
#..O.O.#
##@.O..#
#...O..#
#.#.O..#
#...O..#
#......#
########

<^^>>>vv<v>>v<<
"""u8;

const byte MoveUp = (byte)'^';
const byte MoveDown = (byte)'v';
const byte MoveLeft = (byte)'<';
const byte MoveRight = (byte)'>';

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var mapData = MapData.FromInput(bytes, out var robot, out var mapNumBytes);
var rowOrderSpan = new RowOrderSpan<byte>(mapData.RowOrder, mapData.Width, mapData.Height);

var lineBuffer = new char[mapData.Width];
var moves = bytes[mapNumBytes..];
var warehouse = new Warehouse(in rowOrderSpan, in robot);
foreach (var move in moves)
{
    if (!TryParseMove(move, out var dx, out var dy))
        continue;

    warehouse.TryMove(dx, dy);
    warehouse.Print(lineBuffer, move);
    //Thread.Sleep(1);
}

var total1 = warehouse.SumBoxGpsCoordinates();

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Total 1: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool TryParseMove(byte source, out int dx, out int dy)
{
    (dx, dy) = source switch
    {
        MoveUp => (0, -1),
        MoveDown => (0, 1),
        MoveLeft => (-1, 0),
        MoveRight => (1, 0),
        _ => (0, 0)
    };

    return (dx, dy) != (0, 0);
}

class MapData
{
    const byte Robot = (byte)'@';

    public static MapData FromInput(ReadOnlySpan<byte> source, out Position robot, out int bytesConsumed)
    {
        robot = default;
        bytesConsumed = 0;

        byte[] rowOrder = [];
        var height = 0;
        var width = 0;
        var y = 0;
        foreach (var range in source.Split("\r\n"u8))
        {
            var line = source[range];
            if (line.IsEmpty)
            {
                bytesConsumed = range.End.GetOffset(source.Length);
                break;
            }

            if (y == 0)
            {
                width = line.Length;
                var mapLength = source.IndexOf("\r\n\r\n"u8);
                height = (int)Math.Ceiling(mapLength / (width + 2.0));
                rowOrder = new byte[width * height];
            }

            line.CopyTo(rowOrder.AsSpan(y * width));

            var robotIndex = line.IndexOf(Robot);
            if (robotIndex >= 0)
                robot = new Position(robotIndex, y);

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
}

record struct Position(int X, int Y);

ref struct Warehouse(in RowOrderSpan<byte> rowOrder, in Position robot)
{
    const byte Box = (byte)'O';
    const byte Empty = (byte)'.';
    const byte Robot = (byte)'@';
    const byte Wall = (byte)'#';

    private Position _robot = robot;
    private readonly RowOrderSpan<byte> _rowOrder = rowOrder;

    public int Height { get; } = rowOrder.Height;
    public int Width { get; } = rowOrder.Width;

    public static int GetGpsCoordinate(int x, int y) => 100 * y + x;

    public readonly void Print(char[] lineBuffer, byte move)
    {
        var index = 0;
        Console.Clear();
        Console.WriteLine($"Move {(char)move}:");

        for (var y = 0; y < Height; y++)
        {
            Encoding.UTF8.GetChars(_rowOrder.Span.Slice(index, Width), lineBuffer);
            Console.WriteLine(lineBuffer);

            index += Width;
        }
    }

    public readonly long SumBoxGpsCoordinates()
    {
        var index = 0;
        var span = _rowOrder.Span;
        var sum = 0L;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (span[index] == Box)
                    sum += GetGpsCoordinate(x, y);

                index++;
            }
        }

        return sum;
    }

    public bool TryMove(int dx, int dy)
    {
        var numBoxes = 0;
        var x = _robot.X + dx;
        var y = _robot.Y + dy;
        while (_rowOrder.TryGet(x, y, out var value))
        {
            switch (value)
            {
                case Box:
                    numBoxes++;
                    break;
                case Wall:
                    return false;
                default:
                    var robot = new Position(_robot.X + dx, _robot.Y + dy);
                    _rowOrder.GetRef(_robot.X, _robot.Y) = Empty;
                    _rowOrder.GetRef(robot.X, robot.Y) = Robot;
                    _robot = robot;
                    if (numBoxes > 0)
                        _rowOrder.GetRef(x, y) = Box;

                    return true;
            }

            x += dx;
            y += dy;
        }

        return false;
    }
}
