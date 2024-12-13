using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
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

var exampleBytes4 = """
EEEEE
EXXXX
EEEEE
EXXXX
EEEEE
"""u8.ToArray();

var exampleBytes5 = """
AAAAAA
AAABBA
AAABBA
ABBAAA
ABBAAA
AAAAAA
"""u8.ToArray();

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    3 => exampleBytes3,
    4 => exampleBytes4,
    5 => exampleBytes5,
    _ => File.ReadAllBytes("input.txt")
};

var mapData = MapData.FromInput(bytes);
var farm = new Farm(mapData);

farm.CalculateTotalFencePrices(out var totalPrice1, out var totalPrice2);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Total price: {totalPrice1}");
Console.WriteLine($"Part 2: Total price: {totalPrice2}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

class Farm(MapData mapData)
{
    public MapData MapData { get; } = mapData;
    public PlotPosition[] PlotPositions { get; } = new PlotPosition[mapData.Width * mapData.Height];
    public int Width { get; } = mapData.Width;
    public int Height { get; } = mapData.Height;

    public void CalculateTotalFencePrices(out long totalPrice1, out long totalPrice2)
    {
        totalPrice1 = 0L;
        totalPrice2 = 0L;
        var perimiterSegments = new PoolableList<LineSegment>();
        var plants = new RowOrderSpan<byte>(MapData.RowOrder.AsSpan(), Width, Height);
        var plotPositions = new RowOrderSpan<PlotPosition>(PlotPositions.AsSpan(), Width, Height);
        var regionId = 1;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (plotPositions.Get(x, y).RegionId == 0)
                {
                    var area = 0;
                    var perimiter = 0;
                    perimiterSegments.Reset();
                    CalculateFenceDimensions(plants, plotPositions, perimiterSegments, regionId, x, y, ref area, ref perimiter);
                    var sides = CalculateSides(perimiterSegments.Span);

                    totalPrice1 += area * perimiter;
                    totalPrice2 += area * sides;
                }

                regionId++;
            }
        }
    }

    void CalculateFenceDimensions(
        RowOrderSpan<byte> plants, RowOrderSpan<PlotPosition> plotPositions, PoolableList<LineSegment> perimiterSegments,
        int regionId, int x, int y, ref int area, ref int perimiter)
    {
        area++;
        var plant = plants.Get(x, y);
        plotPositions.GetRef(x, y) = new PlotPosition(plant, regionId, (byte)x, (byte)y);

        GetNeighbor(plants, plotPositions, x - 1, y, out var left);
        var isLeftPerimiter = left.IsPerimiter(plant);
        if (left.ShouldVisit(plant))
            CalculateFenceDimensions(
                plants, plotPositions, perimiterSegments, regionId, left.X, left.Y, ref area, ref perimiter);

        GetNeighbor(plants, plotPositions, x + 1, y, out var right);
        var isRightPerimiter = right.IsPerimiter(plant);
        if (right.ShouldVisit(plant))
            CalculateFenceDimensions(
                plants, plotPositions, perimiterSegments, regionId, right.X, right.Y, ref area, ref perimiter);

        GetNeighbor(plants, plotPositions, x, y - 1, out var up);
        var isUpPerimiter = up.IsPerimiter(plant);
        if (up.ShouldVisit(plant))
            CalculateFenceDimensions(
                plants, plotPositions, perimiterSegments, regionId, up.X, up.Y, ref area, ref perimiter);

        GetNeighbor(plants, plotPositions, x, y + 1, out var down);
        var isDownPerimiter = down.IsPerimiter(plant);
        if (down.ShouldVisit(plant))
            CalculateFenceDimensions(
                plants, plotPositions, perimiterSegments, regionId, down.X, down.Y, ref area, ref perimiter);

        var isUpperLeftCorner = isUpPerimiter && isLeftPerimiter;
        var isUpperRightCorner = isUpPerimiter && isRightPerimiter;
        var isLowerLeftCorner = isDownPerimiter && isLeftPerimiter;
        var isLowerRightCorner = isDownPerimiter && isRightPerimiter;

        if (isLeftPerimiter)
        {
            perimiter++;
            perimiterSegments.Add(LineSegment.Vertical(x, y, y + 1, isUpperLeftCorner, isLowerLeftCorner));
        }

        if (isRightPerimiter)
        {
            perimiter++;
            perimiterSegments.Add(LineSegment.Vertical(x + 1, y, y + 1, isUpperRightCorner, isLowerRightCorner));
        }

        if (up.IsPerimiter(plant))
        {
            perimiter++;
            perimiterSegments.Add(LineSegment.Horizontal(x, x + 1, y, isUpperLeftCorner, isUpperRightCorner));
        }

        if (down.IsPerimiter(plant))
        {
            perimiter++;
            perimiterSegments.Add(LineSegment.Horizontal(x, x + 1, y + 1, isLowerLeftCorner, isLowerRightCorner));
        }
    }

    static int CalculateSides(Span<LineSegment> perimiterSegments)
    {
        perimiterSegments.Sort((a, b) =>
        {
            if (a.Orientation != b.Orientation)
                return a.Orientation.CompareTo(b.Orientation);

            if (a.IsHorizontal && b.IsHorizontal)
            {
                if (a.Y1 < b.Y1)
                    return -1;

                if (a.Y1 > b.Y1)
                    return 1;

                return a.X1.CompareTo(b.X1);
            }

            if (a.IsVertical && b.IsVertical)
            {
                if (a.X1 < b.X1)
                    return -1;

                if (a.X1 > b.X1)
                    return 1;

                return a.Y1.CompareTo(b.Y1);
            }

            return 0;
        });

        var count = 0;
        var previous = new LineSegment(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        for (var i = 0; i < perimiterSegments.Length; i++)
        {
            var segment = perimiterSegments[i];
            if (segment.IsHorizontal && previous.IsHorizontal && segment.Y1 == previous.Y1 && segment.X1 == previous.X2
                && !segment.IsCorner1)
            {
                previous = segment;
                continue;
            }

            if (segment.IsVertical && previous.IsVertical && segment.X1 == previous.X1 && segment.Y1 == previous.Y2
                && !segment.IsCorner1)
            {
                previous = segment;
                continue;
            }

            previous = segment;
            count++;
        }

        return count;
    }

    void GetNeighbor(
        RowOrderSpan<byte> plants, RowOrderSpan<PlotPosition> plotPositions, int x, int y, out Neighbor neighbor)
    {
        if (!IsInBounds(x, y))
        {
            neighbor = new Neighbor(default, (short)x, (short)y, OutOfBounds: true, Visited: false);
            return;
        }

        var plant = plants.Get(x, y);
        var visited = plotPositions.Get(x, y).RegionId != 0;
        neighbor = new Neighbor(plant, (short)x, (short)y, OutOfBounds: false, visited);
    }

    bool IsInBounds(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height;
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

readonly ref struct RowOrderSpan<T>
{
    public RowOrderSpan(Span<T> span, int width, int height)
    {
        if (span.Length != width * height)
            throw new ArgumentException("Span length must be width * height");

        Span = span;
        Width = width;
        Height = height;
    }

    public Span<T> Span { get; }
    public int Width { get; }
    public int Height { get; }

    public T Get(int x, int y) => Span[GetIndex(x, y)];
    public int GetIndex(int x, int y) => y * Width + x;
    public ref T GetRef(int x, int y) => ref Span[GetIndex(x, y)];
}

enum Direction : byte
{
    None,
    Up,
    Down,
    Left,
    Right
}

enum Orientation : byte
{
    Unknown,
    Horizontal,
    Vertical
}

record struct LineSegment(byte X1, byte Y1, byte X2, byte Y2, bool IsCorner1 = false, bool IsCorner2 = false)
{
    public static LineSegment Horizontal(int x1, int x2, int y, bool isCorner1 = false, bool isCorner2 = false)
        => new((byte)x1, (byte)y, (byte)x2, (byte)y, isCorner1, isCorner2);

    public static LineSegment Vertical(int x, int y1, int y2, bool isCorner1 = false, bool isCorner2 = false)
        => new((byte)x, (byte)y1, (byte)x, (byte)y2, isCorner1, isCorner2);

    public readonly bool IsHorizontal => Y1 == Y2;
    public readonly bool IsVertical => X1 == X2;
    public readonly Orientation Orientation
        => X1 == X2 ? Orientation.Vertical : (Y1 == Y2 ? Orientation.Horizontal : Orientation.Unknown);
}

record struct Neighbor(byte Plant, short X, short Y, bool OutOfBounds, bool Visited)
{
    public readonly bool IsPerimiter(byte plant) => OutOfBounds || plant != Plant;
    public readonly bool ShouldVisit(byte plant) => plant == Plant && !Visited;
}

record struct PlotPosition(byte Plant, int RegionId, byte X, byte Y);
