using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    ###############
    #.......#....E#
    #.#.###.#.###.#
    #.....#.#...#.#
    #.###.#####.#.#
    #.#.#.......#.#
    #.#.#####.###.#
    #...........#.#
    ###.#.#####.#.#
    #...#.....#.#.#
    #.#.#.###.#.#.#
    #.....#...#.#.#
    #.###.#.#.#.#.#
    #S..#.....#...#
    ###############
    """u8;

var exampleBytes2 = """
    #################
    #...#...#...#..E#
    #.#.#.#.#.#.#.#.#
    #.#.#.#...#...#.#
    #.#.#.#.###.#.#.#
    #...#.#.#.....#.#
    #.#.#.#.#.#####.#
    #.#...#.#.#.....#
    #.#.#####.#.###.#
    #.#.#.......#...#
    #.#.###.#####.###
    #.#.#...#.....#.#
    #.#.#.#####.###.#
    #.#.#.........#.#
    #.#.#.#########.#
    #S#.............#
    #################
    """u8;

// from https://www.reddit.com/r/adventofcode/comments/1hfhgl1/2024_day_16_part_1_alternate_test_case/
// expected: right zig-zagging path, 21148
// wrong: left diagonal path, 46048
var exampleBytes10 = """
    ###########################
    #######################..E#
    ######################..#.#
    #####################..##.#
    ####################..###.#
    ###################..##...#
    ##################..###.###
    #################..####...#
    ################..#######.#
    ###############..##.......#
    ##############..###.#######
    #############..####.......#
    ############..###########.#
    ###########..##...........#
    ##########..###.###########
    #########..####...........#
    ########..###############.#
    #######..##...............#
    ######..###.###############
    #####..####...............#
    ####..###################.#
    ###..##...................#
    ##..###.###################
    #..####...................#
    #.#######################.#
    #S........................#
    ###########################
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    10 => exampleBytes10,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var mapData = MapData.FromInput(bytes, out _, out var startPosition, out _);
Maze.FromMapData(mapData, out var maze);
var node = MoveNode.CreateRoot((byte)startPosition.X, (byte)startPosition.Y, Direction.East);
if (maze.TryGetBestPath(node, out var bestPath))
    maze.PrintMaze(bestPath, clear: false);

var tiles = maze.CountBestPathsTiles();

Console.WriteLine($"Attempts: {maze.Attempts:N0}");

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Best Score: {bestPath?.Score}");
Console.WriteLine($"Part 2: Tiles: {tiles}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");
Console.ReadLine();

ref struct Maze
{
    const byte Start = (byte)'S';
    const byte End = (byte)'E';
    const byte Wall = (byte)'#';

    long _attempts;
    List<MoveNode> _bestPaths;
    int _bestScore;
    Dictionary<MazeVertexKey, int> _bestScores;
    RowOrderSpan<byte> _tiles;
    ReadOnlySpan<MazeVertex> _vertices;

    public readonly long Attempts => _attempts;
    public readonly int Width => _tiles.Width;
    public readonly int Height => _tiles.Height;

    public static void FromMapData(MapData mapData, out Maze maze)
    {
        var startTimestamp = TimeProvider.System.GetTimestamp();

        var width = mapData.Width;
        var height = mapData.Height;
        var rowOrderTiles = new RowOrderSpan<byte>(mapData.RowOrder, width, height);
        var start = default(MazeVertex);
        Span<MazeVertex> firstPass = new MazeVertex[width * height];

        // first pass: identify vertices
        var index = 0;
        var numVertices = 0;
        var tiles = rowOrderTiles.Span;
        for (byte y = 0; y < height; y++)
        {
            for (byte x = 0; x < width; x++)
            {
                var tile = tiles[index];
                if (tile != Wall)
                {
                    var north = rowOrderTiles.TryGet(x, y - 1, out var n) && n != Wall ? 1 : 0;
                    var south = rowOrderTiles.TryGet(x, y + 1, out var s) && s != Wall ? 1 : 0;
                    var east = rowOrderTiles.TryGet(x + 1, y, out var e) && e != Wall ? 1 : 0;
                    var west = rowOrderTiles.TryGet(x - 1, y, out var w) && w != Wall ? 1 : 0;

                    var isHorizontal = east > 0 && west > 0 && north == 0 && south == 0;
                    var isVertical = north > 0 && south > 0 && east == 0 && west == 0;
                    var isEdge = isHorizontal || isVertical;
                    if (tile == Start || tile == End || !isEdge)
                    {
                        firstPass[index] = new MazeVertex(tile, x, y, (byte)north, (byte)south, (byte)east, (byte)west);
                        numVertices++;
                    }
                }

                index++;
            }
        }

        // second pass: build vertices with edges
        Span<MazeVertex> vertices = new MazeVertex[width * height];
        index = 0;
        for (byte y = 0; y < height; y++)
        {
            var row = firstPass.Slice(index, width);
            for (byte x = 0; x < width; x++)
            {
                var tile = tiles[index];
                if (tile != Wall)
                {
                    var initial = firstPass[index];
                    var north = initial.North > 0 ? GetNorthSouthDistance(rowOrderTiles, firstPass, x, y, dy: -1) : 0;
                    var south = initial.South > 0 ? GetNorthSouthDistance(rowOrderTiles, firstPass, x, y, dy: 1) : 0;
                    var east = initial.East > 0 ? GetEastDistance(row, x) : 0;
                    var west = initial.West > 0 ? GetWestDistance(row, x) : 0;

                    var isHorizontal = east > 0 && west > 0 && north == 0 && south == 0;
                    var isVertical = north > 0 && south > 0 && east == 0 && west == 0;
                    var isEdge = isHorizontal || isVertical;
                    if (tile == Start || tile == End || !isEdge)
                    {
                        var vertex = new MazeVertex(tile, x, y, (byte)north, (byte)south, (byte)east, (byte)west);
                        if (tile == Start)
                            start = vertex;

                        vertices[index] = vertex;
                    }
                }

                index++;
            }
        }

        var bestScores = new Dictionary<MazeVertexKey, int>(numVertices)
        {
            { start.GetKey(Direction.East), 0 }
        };

        var elapsed = TimeProvider.System.GetElapsedTime(startTimestamp);

        // main input has 3,497 vertices
        Console.WriteLine($"# Vertices: {numVertices}, Elapsed: {elapsed.TotalMilliseconds:N3} ms");

        maze = new Maze
        {
            _bestPaths = [],
            _bestScore = int.MaxValue,
            _bestScores = bestScores,
            _tiles = rowOrderTiles,
            _vertices = vertices
        };
    }

    private static int GetEastDistance(ReadOnlySpan<MazeVertex> row, int x)
    {
        var slice = row[(x + 1)..];
        for (var i = 0; i < slice.Length; i++)
            if (slice[i].IsVertex)
                return i + 1;

        return 0;
    }

    private static int GetWestDistance(ReadOnlySpan<MazeVertex> row, int x)
    {
        var slice = row[..x];
        for (var i = x - 1; i >= 0; i--)
            if (slice[i].IsVertex)
                return x - i;

        return 0;
    }

    private static int GetNorthSouthDistance(
        RowOrderSpan<byte> tiles, ReadOnlySpan<MazeVertex> vertices, int x, int y, int dy)
    {
        byte distance = 1;
        while (tiles.TryGetIndex(x, y + distance * dy, out var index))
        {
            if (vertices[index].IsVertex)
                return distance;

            distance++;
        }

        return 0;
    }

    public bool TryGetBestPath(MoveNode node, [NotNullWhen(true)] out MoveNode? bestPath)
    {
        bestPath = null;
        if (!_tiles.TryGetIndex(node.X, node.Y, out var index))
            throw new InvalidOperationException();

        var vertex = _vertices[index];

        // success
        if (vertex.IsEnd)
        {
            //PrintMaze(node);

            _attempts++;
            if (node.Score < _bestScore)
            {
                _bestPaths.Clear();
                _bestScore = node.Score;
            }

            if (node.Score <= _bestScore)
                _bestPaths.Add(node);

            if (_attempts % 100_000 == 0)
                Console.WriteLine($"Attempts: {_attempts:N0}, Best Score: {_bestScore:N0}");

            bestPath = node;
            return true;
        }

        // dead end
        if (vertex.Degree == 1 && !vertex.IsStart)
        {
            //PrintMaze(node);

            _attempts++;
            if (_attempts % 100_000 == 0)
                Console.WriteLine($"Attempts: {_attempts:N0}, Best Score: {_bestScore:N0}");

            return false;
        }

        // straight
        if (vertex.TryGetDistance(node.Direction, out var straightDistance))
        {
            var straightPosition = node.Position.Move(node.Direction, straightDistance);
            if (!node.IsRepeated(in straightPosition))
            {
                var key = new MazeVertexKey((byte)straightPosition.X, (byte)straightPosition.Y, node.Direction);
                var straightScore = node.Score + straightDistance;
                if (!_bestScores.TryGetValue(key, out var s) || straightScore <= s)
                {
                    _bestScores[key] = straightScore;
                    var straight = new MoveNode(
                        node, (byte)straightPosition.X, (byte)straightPosition.Y, node.Direction, straightScore);
                    if (TryGetBestPath(straight, out var straightBestPath))
                        bestPath = straightBestPath;
                }
            }
        }

        // limit turns from a vertex
        if (node.Parent is null || !node.Parent.IsRepeated(node.Position))
        {
            // left
            var leftDirection = node.Direction.TurnLeft();
            if (vertex.TryGetDistance(leftDirection, out _))
            {
                var key = vertex.GetKey(leftDirection);
                var leftScore = node.Score + 1_000;
                if (!_bestScores.TryGetValue(key, out var s) || leftScore <= s)
                {
                    _bestScores[key] = leftScore;
                    var left = new MoveNode(node, node.X, node.Y, leftDirection, leftScore);
                    if (TryGetBestPath(left, out var leftBestPath) && (bestPath is null || leftBestPath.Score < bestPath.Score))
                        bestPath = leftBestPath;
                }
            }

            // right
            var rightDirection = node.Direction.TurnRight();
            if (vertex.TryGetDistance(rightDirection, out _))
            {
                var key = vertex.GetKey(rightDirection);
                var rightScore = node.Score + 1_000;
                if (!_bestScores.TryGetValue(key, out var s) || rightScore <= s)
                {
                    _bestScores[key] = rightScore;
                    var right = new MoveNode(node, node.X, node.Y, rightDirection, rightScore);
                    if (TryGetBestPath(right, out var rightBestPath) && (bestPath is null || rightBestPath.Score < bestPath.Score))
                        bestPath = rightBestPath;
                }
            }
        }

        return bestPath is not null;
    }

    public int CountBestPathsTiles()
    {
        var tiles = new HashSet<Position>(2 * (Width + Height));
        foreach (var path in _bestPaths)
        {
            var node = path;
            var parent = node.Parent;
            while (node is not null)
            {
                tiles.Add(node.Position);
                if (parent is not null)
                {
                    var dx = int.Sign(node.X - parent.X);
                    if (dx != 0)
                    {
                        for (var x = parent.X + dx; x != node.X; x += dx)
                            tiles.Add(new((short)x, node.Y));
                    }

                    var dy = int.Sign(node.Y - parent.Y);
                    if (dy != 0)
                    {
                        for (var y = parent.Y + dy; y != node.Y; y += dy)
                            tiles.Add(new(node.X, (short)y));
                    }
                }

                node = parent;
                parent = parent?.Parent;
            }
        }

        return tiles.Count;
    }

    public readonly void PrintMaze(MoveNode end, bool clear = true)
    {
        var rented = ArrayPool<byte>.Shared.Rent(_tiles.Span.Length);
        var span = rented.AsSpan(0, _tiles.Span.Length);
        _tiles.Span.CopyTo(span);
        var node = end;
        while (node is not null)
        {
            if (_tiles.TryGetIndex(node.X, node.Y, out var index))
            {
                var tile = span[index];
                if (tile == (byte)'.')
                    span[index] = node.Direction switch
                    {
                        Direction.North => (byte)'^',
                        Direction.South => (byte)'v',
                        Direction.East => (byte)'>',
                        Direction.West => (byte)'<',
                        _ => throw new InvalidOperationException()
                    };
            }

            node = node.Parent;
        }

        var isFinish = _tiles.TryGet(end.X, end.Y, out var t) && t == End;
        var lineBuffer = new char[Width];
        if (clear)
            Console.Clear();
        if (isFinish)
            Console.WriteLine($"Score: {end.Score:N0}");
        else
            Console.WriteLine();

        for (var y = 0; y < Height; y++)
        {
            Encoding.UTF8.GetChars(span.Slice(y * Width, Width), lineBuffer);
            Console.WriteLine(lineBuffer);
        }

        Console.WriteLine();

        ArrayPool<byte>.Shared.Return(rented);

        var delay = isFinish ? 100 : 1;
        Thread.Sleep(delay);
    }
}

class MapData
{
    public static MapData FromInput(ReadOnlySpan<byte> source, out int bytesConsumed, out Position start, out Position end)
    {
        bytesConsumed = 0;
        start = default;
        end = default;

        byte[] columnOrder = [];
        byte[] rowOrder = [];
        var height = 0;
        var width = 0;
        short y = 0;
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
                height = (int)Math.Ceiling(source.Length / (width + 2.0));
                rowOrder = new byte[width * height];
                columnOrder = new byte[width * height];
            }

            line.CopyTo(rowOrder.AsSpan(y * width));
            for (short x = 0; x < width; x++)
            {
                var value = line[x];
                columnOrder[x * height + y] = value;
                switch (value)
                {
                    case (byte)'S':
                        start = new Position(x, y);
                        break;
                    case (byte)'E':
                        end = new Position(x, y);
                        break;
                }
            }

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

record struct Position(short X, short Y);

record struct MazeVertex(byte Tile, byte X, byte Y, byte North, byte South, byte East, byte West)
{
    public readonly byte Degree => CalculateDegree(North, South, East, West);
    public readonly bool IsEnd => Tile == (byte)'E';
    public readonly bool IsStart => Tile == (byte)'S';
    public readonly bool IsVertex => North + South + East + West > 0;

    public readonly MazeVertexKey GetKey(Direction direction) => new(X, Y, direction);

    public readonly bool TryGetDistance(Direction direction, out byte distance)
    {
        distance = direction switch
        {
            Direction.North => North,
            Direction.South => South,
            Direction.East => East,
            Direction.West => West,
            _ => throw new ArgumentException(null, nameof(direction))
        };

        return distance > 0;
    }

    private static byte CalculateDegree(byte north, byte south, byte east, byte west)
    {
        var n = north > 0 ? 1 : 0;
        var s = south > 0 ? 1 : 0;
        var e = east > 0 ? 1 : 0;
        var w = west > 0 ? 1 : 0;

        return (byte)(n + s + e + w);
    }
}

record struct MazeVertexKey(byte X, byte Y, Direction Direction);

record MoveNode(MoveNode? Parent, byte X, byte Y, Direction Direction, int Score)
{
    public Position Position => new(X, Y);

    public static MoveNode CreateRoot(byte X, byte Y, Direction Direction) 
        => new(Parent: null, X, Y, Direction, Score: 0);

    public bool IsRepeated(in Position position)
    {
        var node = this;
        while (node is not null)
        {
            if (position == node.Position)
                return true;

            node = node.Parent;
        }

        return false;
    }
}

enum Direction : byte
{
    North,
    South,
    East,
    West
}

static class Extensions
{
    public static Position Move(this in Position position, Direction direction, short distance)
    {
        var (dx, dy) = direction switch
        {
            Direction.North => (0, -distance),
            Direction.South => (0, distance),
            Direction.East => (distance, 0),
            Direction.West => (-distance, 0),
            _ => throw new ArgumentException(null, nameof(direction))
        };

        return new((short)(position.X + dx), (short)(position.Y + dy));
    }

    public static Direction TurnLeft(this Direction direction)
        => direction switch
        {
            Direction.North => Direction.West,
            Direction.South => Direction.East,
            Direction.East => Direction.North,
            Direction.West => Direction.South,
            _ => throw new ArgumentException(null, nameof(direction))
        };

    public static Direction TurnRight(this Direction direction)
        => direction switch
        {
            Direction.North => Direction.East,
            Direction.South => Direction.West,
            Direction.East => Direction.South,
            Direction.West => Direction.North,
            _ => throw new ArgumentException(null, nameof(direction))
        };
}
