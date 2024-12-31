using System.Buffers.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    029A
    980A
    179A
    456A
    379A
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var numericKeypad = new Keypad("789456123 0A"u8, 3, 4);
var directionalKeypad = new Keypad(" ^A<v>"u8, 3, 2);

Span<byte> robot2Sequence = new byte[1024];
Span<byte> robot3Sequence = new byte[1024];
Span<byte> humanSequence = new byte[1024];

var total1 = 0;

foreach (var range in bytes.Split("\r\n"u8))
{
    var code = bytes[range];
    if (code.IsEmpty)
        continue;

    var length2 = GetRemoteSequence(code, in numericKeypad, robot2Sequence);
    var length3 = GetRemoteSequence(robot2Sequence[..length2], in directionalKeypad, robot3Sequence);
    var length4 = GetRemoteSequence(robot3Sequence[..length3], in directionalKeypad, humanSequence);

    Utf8Parser.TryParse(code, out int numericValue, out _);
    total1 += length4 * numericValue;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static int GetRemoteSequence(ReadOnlySpan<byte> input, in Keypad keypad, Span<byte> remoteSequence)
{
    var i = 0;
    Span<byte> path = stackalloc byte[32];
    var previous = Keys.Activate;
    foreach (var key in input)
    {
        var length = GetShortestPath(previous, key, in keypad, path);
        path[..length].CopyTo(remoteSequence[i..]);
        i += length;

        previous = key;
    }

    return i;
}

// based on a Reddit comment, the shortest path will always be:
// - move left first if not passing over gap
// - move vertical first if not passing over gap
// - otherwise, move right first
static int GetShortestPath(byte key1, byte key2, in Keypad keypad, Span<byte> path)
{
    var i = 0;
    var position1 = keypad.GetPosition(key1);
    var position2 = keypad.GetPosition(key2);
    var dx = position2.X - position1.X;
    var dy = position2.Y - position1.Y;

    // move left first if not passing over gap
    if (dx < 0 && keypad.TryGetKey(position2.X, position1.Y, out _))
    {
        i += AddHorizontalKeys(dx, path);
        i += AddVerticalKeys(dy, path[i..]);
        path[i++] = Keys.Activate;
        return i;
    }

    // move vertical first if not passing over gap
    if (dy != 0 && keypad.TryGetKey(position1.X, position2.Y, out _))
    {
        i += AddVerticalKeys(dy, path);
        i += AddHorizontalKeys(dx, path[i..]);
        path[i++] = Keys.Activate;
        return i;
    }

    // otherwise, move right first
    if (dx >= 0)
    {
        i += AddHorizontalKeys(dx, path);
        i += AddVerticalKeys(dy, path[i..]);
        path[i++] = Keys.Activate;
        return i;
    }

    throw new InvalidOperationException();
}

static int AddHorizontalKeys(int dx, Span<byte> path)
{
    if (dx == 0)
        return 0;

    byte key = dx switch
    {
        > 0 => Keys.Right,
        < 0 => Keys.Left,
        _ => 0
    };

    var length = Math.Abs(dx);
    path[..length].Fill(key);
    return length;
}

static int AddVerticalKeys(int dy, Span<byte> path)
{
    if (dy == 0)
        return 0;

    byte key = dy switch
    {
        > 0 => Keys.Down,
        < 0 => Keys.Up,
        _ => 0
    };

    var length = Math.Abs(dy);
    path[..length].Fill(key);
    return length;
}

static class Keys
{
    public const byte Activate = (byte)'A';
    public const byte Up = (byte)'^';
    public const byte Down = (byte)'v';
    public const byte Left = (byte)'<';
    public const byte Right = (byte)'>';
}

readonly ref struct Keypad
{
    const byte Gap = (byte)' ';

    readonly RowOrderSpan<byte> _keys;
    readonly Dictionary<byte, Position> _lookup = [];

    public Keypad(ReadOnlySpan<byte> keys, int width, int height)
    {
        if (keys.Length != width * height)
            throw new ArgumentException("Inconsistent arguments");

        _keys = new(keys.ToArray(), width, height);
        for (byte y = 0; y < height; y++)
            for (byte x = 0; x < width; x++)
                if (_keys.TryGet(x, y, out var key))
                    _lookup.Add(key, new(x, y));
    }

    public readonly bool TryGetKey(int x, int y, out byte key) => _keys.TryGet(x, y, out key) && key != Gap;
    public readonly Position GetPosition(byte key) => _lookup[key];
}

record struct Position(byte X, byte Y);
