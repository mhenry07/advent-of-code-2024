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
        var totalPrice = 0L;
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
                    CalculateFenceDimensions(plants, plotPositions, regionId, x, y, ref area, ref perimiter);
                    totalPrice += area * perimiter;
                }

                regionId++;
            }
        }

        return totalPrice;
    }

    void CalculateFenceDimensions(
        RowOrderSpan<byte> plants, RowOrderSpan<PlotPosition> plotPositions, int regionId, int x, int y, ref int area, ref int perimiter)
    {
        area++;
        var plant = plants.Get(x, y);
        plotPositions.GetRef(x, y) = new PlotPosition(plant, regionId, (byte)x, (byte)y);

        GetNeighbor(plants, plotPositions, x - 1, y, out var neighbor);
        if (neighbor.IsPerimiter(plant))
            perimiter++;
        if (neighbor.ShouldVisit(plant))
            CalculateFenceDimensions(plants, plotPositions, regionId, neighbor.X, neighbor.Y, ref area, ref perimiter);

        GetNeighbor(plants, plotPositions, x + 1, y, out neighbor);
        if (neighbor.IsPerimiter(plant))
            perimiter++;
        if (neighbor.ShouldVisit(plant))
            CalculateFenceDimensions(plants, plotPositions, regionId, neighbor.X, neighbor.Y, ref area, ref perimiter);

        GetNeighbor(plants, plotPositions, x, y - 1, out neighbor);
        if (neighbor.IsPerimiter(plant))
            perimiter++;
        if (neighbor.ShouldVisit(plant))
            CalculateFenceDimensions(plants, plotPositions, regionId, neighbor.X, neighbor.Y, ref area, ref perimiter);

        GetNeighbor(plants, plotPositions, x, y + 1, out neighbor);
        if (neighbor.IsPerimiter(plant))
            perimiter++;
        if (neighbor.ShouldVisit(plant))
            CalculateFenceDimensions(plants, plotPositions, regionId, neighbor.X, neighbor.Y, ref area, ref perimiter);
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

record struct Neighbor(byte Plant, short X, short Y, bool OutOfBounds, bool Visited)
{
    public readonly bool IsPerimiter(byte plant) => OutOfBounds || plant != Plant;
    public readonly bool ShouldVisit(byte plant) => plant == Plant && !Visited;
}

record struct PlotPosition(byte Plant, int RegionId, byte X, byte Y);
