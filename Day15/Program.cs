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

var exampleBytes3 = """
    #######
    #...#.#
    #.....#
    #..OO@#
    #..O..#
    #.....#
    #######

    <vv<<^^<<^^
    """u8;

const byte MoveUp = (byte)'^';
const byte MoveDown = (byte)'v';
const byte MoveLeft = (byte)'<';
const byte MoveRight = (byte)'>';

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    3 => exampleBytes3,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var mapData = MapData.FromInput(bytes, out var robot, out var mapNumBytes);
var rowOrderSpan = new RowOrderSpan<byte>(mapData.RowOrder, mapData.Width, mapData.Height);

var lineBuffer = new char[mapData.Width];
var moves = bytes[mapNumBytes..];
var lastMove = moves[moves.LastIndexOfAny("^v<>"u8)];
var warehouse = new Warehouse(rowOrderSpan, robot);
warehouse.ToWideWarehouse(out var wideWarehouse);
foreach (var move in moves)
{
    if (!TryParseMove(move, out var dx, out var dy))
        continue;

    warehouse.TryMove(dx, dy);
    //warehouse.Print(lineBuffer, move);
    //Thread.Sleep(1);
}

var total1 = warehouse.SumBoxGpsCoordinates();

var wideLineBuffer = new char[wideWarehouse.Width];
foreach (var move in moves)
{
    if (!TryParseMove(move, out var dx, out var dy))
        continue;

    wideWarehouse.TryMove(dx, dy);
    //wideWarehouse.Print(wideLineBuffer, move);
    //Thread.Sleep(1);
}

var total2 = wideWarehouse.SumBoxGpsCoordinates();

var elapsed = TimeProvider.System.GetElapsedTime(start);

warehouse.Print(lineBuffer, lastMove, clear: false);
wideWarehouse.Print(wideLineBuffer, lastMove, clear: false);

Console.WriteLine($"Total 1: {total1}");
Console.WriteLine($"Total 2: {total2}");
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

record struct Position(int X, int Y);

record struct WideBox(int LeftX, int RightX, int Y)
{
    public static WideBox FromLeft(int x, int y) => new(x, x + 1, y);
    public static WideBox FromRight(int x, int y) => new(x - 1, x, y);
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

ref struct Warehouse(RowOrderSpan<byte> rowOrder, Position robot)
{
    const byte Box = (byte)'O';
    const byte BoxL = (byte)'[';
    const byte BoxR = (byte)']';
    const byte Empty = (byte)'.';
    const byte Robot = (byte)'@';
    const byte Wall = (byte)'#';

    private bool _isWide;
    private Position _robot = robot;
    private readonly RowOrderSpan<byte> _rowOrder = rowOrder;

    public int Height { get; } = rowOrder.Height;
    public int Width { get; } = rowOrder.Width;

    public static int GetGpsCoordinate(int x, int y) => 100 * y + x;

    public readonly void Print(char[] lineBuffer, byte move, bool clear = true)
    {
        if (clear)
            Console.Clear();

        Console.WriteLine($"Move {(char)move}:");

        var index = 0;
        for (var y = 0; y < Height; y++)
        {
            Encoding.UTF8.GetChars(_rowOrder.Span.Slice(index, Width), lineBuffer);
            Console.WriteLine(lineBuffer);

            index += Width;
        }
    }

    public readonly long SumBoxGpsCoordinates()
    {
        var boxByte = _isWide ? BoxL : Box;
        var index = 0;
        var span = _rowOrder.Span;
        var sum = 0L;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (span[index] == boxByte)
                    sum += GetGpsCoordinate(x, y);

                index++;
            }
        }

        return sum;
    }

    public void ToWideWarehouse(out Warehouse warehouse)
    {
        var source = _rowOrder.Span;
        var wideBytes = new byte[source.Length * 2];
        var destination = wideBytes.AsSpan();
        for (int i = 0, j = 0; i < source.Length; i++, j += 2)
        {
            var tile = source[i];
            switch (tile)
            {
                case Empty:
                case Wall:
                    destination[j] = tile;
                    destination[j + 1] = tile;
                    break;
                case Box:
                    destination[j] = BoxL;
                    destination[j + 1] = BoxR;
                    break;
                case Robot:
                    destination[j] = Robot;
                    destination[j + 1] = Empty;
                    break;
            }
        }

        var rowOrder = new RowOrderSpan<byte>(wideBytes, 2 * Width, Height);
        var robot = _robot with { X = 2 * _robot.X };
        warehouse = new Warehouse(rowOrder, robot) { _isWide = true };
    }

    public bool TryMove(int dx, int dy)
    {
        if (_isWide)
            return TryMoveWide(dx, dy);

        var numBoxes = 0;
        var x = _robot.X + dx;
        var y = _robot.Y + dy;
        while (_rowOrder.TryGet(x, y, out var tile))
        {
            switch (tile)
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

    private bool TryMoveWide(int dx, int dy)
    {
        using var boxes = new PoolableList<WideBox>();
        if (!CanMoveWideDfs(_robot.X + dx, _robot.Y + dy, dx, dy, boxes))
            return false;

        if (boxes.Length > 0)
            MoveBoxes(boxes.Span, dx, dy);
        MoveRobot(dx, dy);
        return true;
    }

    private readonly bool CanMoveWideDfs(int x, int y, int dx, int dy, PoolableList<WideBox> boxes)
    {
        if (!_rowOrder.TryGet(x, y, out var tile))
            return false;

        switch (tile)
        {
            case BoxL:
                boxes.Add(WideBox.FromLeft(x, y));
                if (dx == 0)
                {
                    return CanMoveWideDfs(x, y + dy, dx, dy, boxes)
                        && CanMoveWideDfs(x + 1, y + dy, dx, dy, boxes);
                }
                else
                {
                    return CanMoveWideDfs(x + 2 * dx, y + dy, dx, dy, boxes);
                }

            case BoxR:
                boxes.Add(WideBox.FromRight(x, y));
                if (dx == 0)
                {
                    return CanMoveWideDfs(x - 1, y + dy, dx, dy, boxes)
                        && CanMoveWideDfs(x, y + dy, dx, dy, boxes);
                }
                else
                {
                    return CanMoveWideDfs(x + 2 * dx, y + dy, dx, dy, boxes);
                }

            case Wall:
                return false;

            default:
                return true;
        }
    }

    private void MoveBoxes(ReadOnlySpan<WideBox> boxes, int dx, int dy)
    {
        using var newBoxes = new PoolableList<WideBox>(boxes.Length);
        foreach (var box in boxes)
            newBoxes.Add(WideBox.FromLeft(box.LeftX + dx, box.Y + dy));

        foreach (var box in boxes)
        {
            _rowOrder.GetRef(box.LeftX, box.Y) = Empty;
            _rowOrder.GetRef(box.RightX, box.Y) = Empty;
        }

        foreach (var box in newBoxes.Span)
        {
            _rowOrder.GetRef(box.LeftX, box.Y) = BoxL;
            _rowOrder.GetRef(box.RightX, box.Y) = BoxR;
        }
    }

    private void MoveRobot(int dx, int dy)
    {
        var robot = new Position(_robot.X + dx, _robot.Y + dy);
        _rowOrder.GetRef(_robot.X, _robot.Y) = Empty;
        _rowOrder.GetRef(robot.X, robot.Y) = Robot;
        _robot = robot;
    }
}
