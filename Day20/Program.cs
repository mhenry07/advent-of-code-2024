﻿using System.Buffers;
using System.Diagnostics;
using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    ###############
    #...#...#.....#
    #.#.#.#.#.###.#
    #S#...#.#.#...#
    #######.#.#.###
    #######.#.#...#
    #######.#.###.#
    ###..E#...#...#
    ###.#######.###
    #...###...#...#
    #.#####.#.###.#
    #.#...#.#.#...#
    #.#.#.#.#.#.###
    #...#...#...###
    ###############
    """u8;

var exampleBytes10 = """
    ###############
    #...#...#.....#
    #.#.#.#.#.###.#
    #.#...#.#.#...#
    #.#####.#.#.###
    #...S##.#.#...#
    #######.#.###.#
    ###..E#...#...#
    ###.#######.###
    #...###...#...#
    #.#####.#.###.#
    #.#...#.#.#...#
    #.#.#.#.#.#.###
    #...#...#...###
    ###############
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    10 => exampleBytes10,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var mapData = MapData.FromInput(bytes, out _, out var startPosition, out _);
Maze.FromMapData(mapData, out var maze);

var normalScore = maze.GetScore(in startPosition, out var startNode, out var endNode);

Console.WriteLine($"Normal score: {normalScore}");

var cheatDeltas = maze.GetCheatDeltas(startNode!, endNode!);

Console.WriteLine($"Total cheats: {cheatDeltas.Count}");

foreach (var group in cheatDeltas.Values.GroupBy(x => x).OrderByDescending(g => g.Key))
    Console.WriteLine($"{group.Count()} cheats save {Math.Abs(group.Key)} picoseconds");

var total1 = cheatDeltas.Values.Count(d => d <= -100);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1:: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");
Console.ReadLine();

ref struct Maze
{
    const byte Start = (byte)'S';
    const byte End = (byte)'E';
    const byte Wall = (byte)'#';

    int _bestScore;
    Dictionary<Cheat, int> _cheatDeltas;
    Dictionary<Position, int> _positionScores;
    RowOrderSpan<byte> _tiles;
    ReadOnlySpan<MazeVertex> _vertices;

    public readonly Dictionary<Cheat, int> CheatDeltas => _cheatDeltas;
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

                    if (true)
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
                    var north = initial.North;
                    var south = initial.South;
                    var east = initial.East;
                    var west = initial.West;

                    if (true)
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

        var elapsed = TimeProvider.System.GetElapsedTime(startTimestamp);

        Console.WriteLine($"# Vertices: {numVertices}, Elapsed: {elapsed.TotalMilliseconds:N3} ms");

        maze = new Maze
        {
            _bestScore = int.MaxValue,
            _cheatDeltas = [],
            _positionScores = [],
            _tiles = rowOrderTiles,
            _vertices = vertices
        };
    }

    private static int GetEastDistance(ReadOnlySpan<MazeVertex> row, int x)
    {
        var slice = row[(x + 1)..];
        if (slice[0].IsVertex)
            return 1;

        return 0;
    }

    private static int GetWestDistance(ReadOnlySpan<MazeVertex> row, int x)
    {
        if (row[x - 1].IsVertex)
            return 1;

        return 0;
    }

    private static int GetNorthSouthDistance(
        RowOrderSpan<byte> tiles, ReadOnlySpan<MazeVertex> vertices, int x, int y, int dy)
    {
        byte distance = 1;
        if (tiles.TryGetIndex(x, y + distance * dy, out var index))
        {
            if (vertices[index].IsVertex)
                return distance;

            distance++;
        }

        return 0;
    }

    public int GetScore(in Position startPosition, out MoveNode? start, out MoveNode? end)
    {
        if (!_tiles.TryGetIndex(startPosition.X, startPosition.Y, out var index))
            throw new InvalidOperationException();

        var direction = Direction.North;
        var vertex = _vertices[index];
        while (!vertex.TryGetDistance(direction, out _))
            direction = direction.TurnLeft();

        var node = start = MoveNode.CreateRoot((byte)startPosition.X, (byte)startPosition.Y, direction);
        var tilesSpan = _tiles.Span;
        while (true)
        {
            if (!_tiles.TryGetIndex(node.X, node.Y, out index))
                throw new InvalidOperationException();

            var position = node.Position;
            _positionScores[position] = node.Score;

            if (tilesSpan[index] == End)
                break;

            direction = node.Direction;
            vertex = _vertices[index];
            if (vertex.TryGetDistance(direction, out var distance))
            {
                node = node.CreateChild(position.Move(direction, distance), direction);
                continue;
            }

            var leftDirection = direction.TurnLeft();
            if (vertex.TryGetDistance(leftDirection, out distance))
            {
                node = node.CreateChild(position.Move(leftDirection, distance), leftDirection);
                continue;
            }

            var rightDirection = direction.TurnRight();
            if (vertex.TryGetDistance(rightDirection, out distance))
            {
                node = node.CreateChild(position.Move(rightDirection, distance), rightDirection);
                continue;
            }

            throw new InvalidOperationException();
        }

        end = node;
        _bestScore = end.Score;
        return end.Score;
    }

    public readonly Dictionary<Cheat, int> GetCheatDeltas(MoveNode start, MoveNode end)
    {
        if (!_tiles.TryGet(start.X, start.Y, out _))
            throw new InvalidOperationException();

        using var mainPath = GetPath(end);
        foreach (var node in mainPath.Span[..^1])
        {
            if (!_tiles.TryGetIndex(node.X, node.Y, out var index))
                throw new InvalidOperationException();

            var direction = node.Direction;
            var position = node.Position;
            var cheatStart = position.Move(direction, 1);
            var cheatEnd = position.Move(direction, 2);
            if (_tiles.TryGet(cheatStart.X, cheatStart.Y, out var s) && s == Wall
                && _tiles.TryGet(cheatEnd.X, cheatEnd.Y, out var e) && e != Wall)
            {
                var cheat = new Cheat(cheatStart, cheatEnd);
                var cheatScore = node.Score + 2;
                var delta = cheatScore - _positionScores[cheatEnd];
                if (delta < 0)
                    _cheatDeltas.Add(cheat, delta);
            }

            var leftDirection = direction.TurnLeft();
            cheatStart = position.Move(leftDirection, 1);
            cheatEnd = position.Move(leftDirection, 2);
            if (_tiles.TryGet(cheatStart.X, cheatStart.Y, out s) && s == Wall
                && _tiles.TryGet(cheatEnd.X, cheatEnd.Y, out e) && e != Wall)
            {
                var cheat = new Cheat(cheatStart, cheatEnd);
                var cheatScore = node.Score + 2;
                var delta = cheatScore - _positionScores[cheatEnd];
                if (delta < 0)
                    _cheatDeltas.Add(cheat, delta);
            }

            var rightDirection = direction.TurnRight();
            cheatStart = position.Move(rightDirection, 1);
            cheatEnd = position.Move(rightDirection, 2);
            if (_tiles.TryGet(cheatStart.X, cheatStart.Y, out s) && s == Wall
                && _tiles.TryGet(cheatEnd.X, cheatEnd.Y, out e) && e != Wall)
            {
                var cheat = new Cheat(cheatStart, cheatEnd);
                var cheatScore = node.Score + 2;
                var delta = cheatScore - _positionScores[cheatEnd];
                if (delta < 0)
                    _cheatDeltas.Add(cheat, delta);
            }

            if (ReferenceEquals(node, start))
            {
                var reverse = direction.TurnRight().TurnRight();
                cheatStart = position.Move(reverse, 1);
                cheatEnd = position.Move(reverse, 2);
                if (_tiles.TryGet(cheatStart.X, cheatStart.Y, out s) && s == Wall
                    && _tiles.TryGet(cheatEnd.X, cheatEnd.Y, out e) && e != Wall)
                {
                    var cheat = new Cheat(cheatStart, cheatEnd);
                    var cheatScore = node.Score + 2;
                    var delta = cheatScore - _positionScores[cheatEnd];
                    if (delta < 0)
                        _cheatDeltas.Add(cheat, delta);
                }
            }
        }

        return _cheatDeltas;
    }

    public static PoolableList<MoveNode> GetPath(MoveNode end)
    {
        var node = end;
        var path = new PoolableList<MoveNode>();
        while (node != null)
        {
            path.Add(node);
            node = node.Parent;
        }

        path.Span.Reverse();
        return path;
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

[DebuggerDisplay("Position = ({X}, {Y}), Direction = {Direction}, HasCheat = {HasCheat}, Score = {Score}")]
record MoveNode(MoveNode? Parent, byte X, byte Y, Direction Direction, bool HasCheat, int Score)
{
    public Position Position => new(X, Y);

    public static MoveNode CreateRoot(byte x, byte y, Direction direction)
        => new(Parent: null, x, y, direction, HasCheat: false, Score: 0);

    public MoveNode CreateChild(in Position position, Direction direction)
    {
        var distance = position.X == X ? Math.Abs(position.Y - Y) : Math.Abs(position.X - X);
        return new(this, (byte)position.X, (byte)position.Y, direction, HasCheat, Score + distance);
    }

    public MoveNode CreateCheat(in Position position, Direction direction)
    {
        if (HasCheat)
            throw new ArgumentException("Already cheated");

        var distance = position.X == X ? Math.Abs(position.Y - Y) : Math.Abs(position.X - X);
        return new(this, (byte)position.X, (byte)position.Y, direction, HasCheat: true, Score + distance);
    }

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

record struct Cheat(Position Start, Position End);

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
