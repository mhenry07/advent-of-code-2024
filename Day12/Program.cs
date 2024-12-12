var start = TimeProvider.System.GetTimestamp();

int? useExample = 3;
var exampleBytes1 = """
AAAA
BBCD
BBCC
EEEC
"""u8.ToArray();

var exampleBytes2 = """
OOOOO
OXOXO
OOOOO
OXOXO
OOOOO
"""u8.ToArray();

var exampleBytes3 = """
RRRRIICCFF
RRRRIICCCF
VVRRRCCFFF
VVRCCCJFFF
VVVVCJJCFE
VVIVCCJJEE
VVIIICJJEE
MIIIIIJJEE
MIIISIJEEE
MMMISSJEEE
"""u8.ToArray();

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    3 => exampleBytes3,
    _ => File.ReadAllBytes("input.txt")
};

var mapData = MapData.FromInput(bytes);
var farm = new Farm(mapData);

var totalPrice1 = farm.CalculateTotalFencePrice();


var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Total price: {totalPrice1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

class Farm(MapData mapData)
{
    public MapData MapData { get; } = mapData;
    public PlotPosition[] PlotPositions { get; } = new PlotPosition[mapData.Width * mapData.Height];
    public int Width { get; } = mapData.Width;
    public int Height { get; } = mapData.Height;

    public long CalculateTotalFencePrice()
    {
        var index = 0;
        var totalPrice = 0L;
        var rowOrder = MapData.RowOrder.AsSpan();
        var plotPositions = PlotPositions.AsSpan();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (plotPositions[index].RegionId == 0)
                    totalPrice += CalculateFencePrice(rowOrder, plotPositions, index + 1, x, y);

                index++;
            }
        }

        return totalPrice;
    }

    int CalculateFencePrice(ReadOnlySpan<byte> rowOrder, Span<PlotPosition> plotPositions, int regionId, int x, int y)
    {
        var area = 1;
        var direction = Direction.Right;
        var position = new Position((byte)x, (byte)y);
        var index = GetIndex(x, y);
        var plant = rowOrder[index];
        var perimiter = GetPlotPerimiter(rowOrder, plant, x, y);
        plotPositions[index] = new PlotPosition(plant, regionId, (byte)x, (byte)y);
        while (TryMoveLeftHandRule(rowOrder, plotPositions, plant, ref direction, ref position))
        {
            index = GetIndex(position.X, position.Y);
            plotPositions[index] = new PlotPosition(plant, regionId, position.X, position.Y);
            area++;
            perimiter += GetPlotPerimiter(rowOrder, plant, position.X, position.Y);
        }

        return area * perimiter;
    }

    int GetPlotPerimiter(ReadOnlySpan<byte> rowOrder, byte plant, int x, int y)
    {
        var perimiter = 0;
        perimiter += GetPerimiter(rowOrder, plant, x - 1, y, perimiter);
        perimiter += GetPerimiter(rowOrder, plant, x + 1, y, perimiter);
        perimiter += GetPerimiter(rowOrder, plant, x, y - 1, perimiter);
        perimiter += GetPerimiter(rowOrder, plant, x, y + 1, perimiter);

        return perimiter;

        int GetPerimiter(ReadOnlySpan<byte> rowOrder, byte plant, int x1, int y1, int perimiter)
        {
            return !IsInBounds(x1, y1) || rowOrder[GetIndex(x1, y1)] != plant
                ? 1
                : 0;
        }
    }

    // this has an issue where it will exit early if there's a 1-wide peninsula or 1-wide corridor
    bool TryMoveLeftHandRule(
        ReadOnlySpan<byte> rowOrder, ReadOnlySpan<PlotPosition> plotPositions, byte plant,
        ref Direction direction, ref Position position)
    {
        var leftTurn = TurnLeft(direction);
        if (TryMove(rowOrder, plotPositions, plant, leftTurn, ref position))
        {
            direction = leftTurn;
            return true;
        }

        if (TryMove(rowOrder, plotPositions, plant, direction, ref position))
            return true;

        var rightTurn = TurnRight(direction);
        if (TryMove(rowOrder, plotPositions, plant, rightTurn, ref position))
        {
            direction = rightTurn;
            return true;
        }

        var back = TurnAround(direction);
        if (TryMove(rowOrder, plotPositions, plant, back, ref position))
        {
            direction = back;
            return true;
        }

        return false;
    }

    bool TryMove(
        ReadOnlySpan<byte> rowOrder, ReadOnlySpan<PlotPosition> plotPositions, byte plant, Direction direction,
        ref Position position)
    {
        var (x, y) = MoveOne(in position, direction);
        if (!IsInBounds(x, y))
            return false;

        var index = GetIndex(x, y);
        if (rowOrder[index] != plant)
            return false;

        if (plotPositions[index].RegionId == 0)
        {
            position = new((byte)x, (byte)y);
            return true;
        }

        return false;
    }

    int GetIndex(int x, int y) => y * Width + x;

    bool IsInBounds(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height;

    static (int X, int Y) MoveOne(in Position start, Direction direction) => direction switch
    {
        Direction.Up => (start.X, start.Y - 1),
        Direction.Down => (start.X, start.Y + 1),
        Direction.Left => (start.X - 1, start.Y),
        Direction.Right => (start.X + 1, start.Y),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    static Direction TurnAround(Direction direction) => direction switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    static Direction TurnLeft(Direction direction) => direction switch
    {
        Direction.Up => Direction.Left,
        Direction.Left => Direction.Down,
        Direction.Down => Direction.Right,
        Direction.Right => Direction.Up,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    static Direction TurnRight(Direction direction) => direction switch
    {
        Direction.Up => Direction.Right,
        Direction.Right => Direction.Down,
        Direction.Down => Direction.Left,
        Direction.Left => Direction.Up,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}

class MapData
{
    public static MapData FromInput(ReadOnlySpan<byte> source)
    {
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

enum Direction : byte
{
    None,
    Up,
    Down,
    Left,
    Right
}

record struct PlotPosition(byte Plant, int RegionId, byte X, byte Y);

record struct Position(byte X, byte Y);
