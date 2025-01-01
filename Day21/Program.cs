﻿using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = 1;
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

var optimizer = new ButtonOptimizer();

var presser1 = new NumericButtonPresser();
var presser2 = new DirectionalButtonPresser();
var presser3 = new DirectionalButtonPresser();

Span<byte> sequence1 = new byte[1024];
Span<byte> sequence2 = new byte[1024];
Span<byte> sequence3 = new byte[1024];

// first attempt: 228938 (too high)
var total1 = 0;
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
        continue;

    if (line.Length != 4)
        throw new InvalidDataException($"Expected code sequence to be 4 bytes: {Encoding.UTF8.GetString(line)}");

    if (!Utf8Parser.TryParse(line, out short value, out _))
        throw new InvalidDataException($"Failed to parse value from: {Encoding.UTF8.GetString(line)}");

    //var length1 = presser1.GetRemoteSequence(line, sequence1);
    //var length2 = presser2.GetRemoteSequence(sequence1[..length1], sequence2);
    //var length3 = presser3.GetRemoteSequence(sequence2[..length2], sequence3);

    var length3 = optimizer.GetOptimalTrajectory(line);

    total1 += length3 * value;

    //Console.WriteLine(Encoding.UTF8.GetString(sequence3[..length3]));
    //Console.WriteLine(Encoding.UTF8.GetString(sequence2[..length2]));
    //Console.WriteLine(Encoding.UTF8.GetString(sequence1[..length1]));
    Console.WriteLine(Encoding.UTF8.GetString(line));
    Console.WriteLine($"{length3} * {value} = {length3 * value}");
    Console.WriteLine();
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

// see https://en.wikipedia.org/wiki/Differential_dynamic_programming
struct ButtonOptimizer
{
    NumericButtonPresser _presser1;
    DirectionalButtonPresser _presser2;
    DirectionalButtonPresser _presser3;
    readonly Stack<KeyNode> _stack;

    public ButtonOptimizer()
    {
        _presser1 = new();
        _presser2 = new();
        _presser3 = new();
        _stack = new Stack<KeyNode>();
    }

    public readonly int GetOptimalTrajectory(ReadOnlySpan<byte> input)
    {
        Span<byte> keyBuffer = stackalloc byte[128];
        using var sequences = new PoolableList<KeyNode>();
        var i = 0;
        var length = 0;
        foreach (var key in input)
        {
            GetNumericSequences(_presser1.Position, key, sequences);

            var leaf = GetOptimalTrajectory(sequences.Span);
            i += GetSequenceKeys(leaf, keyBuffer[i..]);
            length += leaf.Cost;

            sequences.Reset();
        }

        Console.WriteLine(Encoding.UTF8.GetString(keyBuffer[..i]));

        return length;
    }

    private static KeyNode GetOptimalTrajectory(ReadOnlySpan<KeyNode> sequences)
    {
        var best = default(KeyNode?);
        foreach (var node in sequences)
            if (best is null || node.Cost < best.Cost)
                best = node;

        return best!;
    }

    private readonly void GetNumericSequences(
        in KeypadPosition start, ReadOnlySpan<byte> keys, PoolableList<PoolableList<KeyNode>> remoteSequences)
    {
        foreach (var key in keys)
        {
            var sequences = new PoolableList<KeyNode>();
            GetNumericSequences(in start, key, sequences);

            remoteSequences.Add(sequences);
        }
    }

    private readonly void GetNumericSequences(
        in KeypadPosition startPosition, byte numericKey, PoolableList<KeyNode> remoteSequences)
    {
        var start = KeyNode.Start(DirectionalKeypad.Activate, in startPosition);
        _stack.Push(start);

        NumericKeypad.TryGetPosition(numericKey, out var destination);
        if (destination == startPosition)
        {
            remoteSequences.Add(start);
            return;
        }

        while (_stack.TryPop(out var current))
        {
            if (NumericButtonPresser.TryStepVertical(current.Position, numericKey, out var nextPosition))
            {
                if (nextPosition.Position == destination)
                    remoteSequences.Add(nextPosition);
                else
                    _stack.Push(nextPosition);
            }

            if (NumericButtonPresser.TryStepHorizontal(current.Position, numericKey, out nextPosition))
            {
                if (nextPosition.Position == destination)
                    remoteSequences.Add(nextPosition);
                else
                    _stack.Push(nextPosition);
            }
        }

        _stack.Clear();
    }

    private readonly void GetDirectionalSequences(
        in KeypadPosition start, byte directionalKey, PoolableList<KeyNode> remoteSequences)
    {
        var root = KeyNode.Start(DirectionalKeypad.Activate, in start);
        var stack = new Stack<KeyNode>([root]);

        DirectionalKeypad.TryGetPosition(directionalKey, out var destination);
        if (destination == start)
        {
            remoteSequences.Add(root);
            return;
        }

        while (stack.TryPop(out var current))
        {
            if (DirectionalButtonPresser.TryStepVertical(current.Position, directionalKey, out var nextPosition))
            {

                if (nextPosition == destination)
                    remoteSequences.Add(nextPosition);
                else
                    stack.Push(nextPosition);
            }

            if (DirectionalButtonPresser.TryStepHorizontal(current.Position, directionalKey, out nextPosition))
            {
                if (nextPosition == destination)
                    remoteSequences.Add(nextPosition);
                else
                    stack.Push(nextPosition);
            }
        }

        _stack.Clear();
    }

    private static PoolableList<KeyNode> GetSequence(KeyNode leafNode)
    {
        KeyNode? node = leafNode;
        var sequence = new PoolableList<KeyNode>(leafNode.Cost);
        while (node is not null)
        {
            sequence.Add(node);
            node = node.Prevous;
        }

        sequence.Span.Reverse();
        return sequence;
    }

    private static int GetSequenceKeys(KeyNode leafNode, Span<byte> keys)
    {
        var i = 0;
        using var sequence = GetSequence(leafNode);
        foreach (var node in sequence.Span)
        {
            keys[i] = node.RemoteKey;
            i++;
        }

        return i;
    }

    private static void Reset<T>(PoolableList<PoolableList<T>> list)
    {
        foreach (var inner in list.Span)
            inner.Reset();

        list.Reset();
    }

    private static void Reset<T>(PoolableList<PoolableList<PoolableList<T>>> list)
    {
        foreach (var inner in list.Span)
            Reset(inner);

        list.Reset();
    }

    private static void Dispose<T>(PoolableList<PoolableList<T>> list)
    {
        foreach (var inner in list.Span)
            inner.Dispose();

        list.Dispose();
    }

    private static void Dispose<T>(PoolableList<PoolableList<PoolableList<T>>> list)
    {
        foreach (var inner in list.Span)
            Dispose(inner);

        list.Dispose();
    }
}

struct NumericButtonPresser
{
    private KeypadPosition _position;

    public readonly KeypadPosition Position => _position;

    public int GetRemoteSequence(ReadOnlySpan<byte> localSequence, Span<byte> remoteSequence)
    {
        //Console.WriteLine($"Calculating sequence for '{Encoding.UTF8.GetString(localSequence)}':");

        Span<byte> buffer1 = stackalloc byte[32];
        Span<byte> buffer2 = stackalloc byte[32];

        var i = 0;
        foreach (var key in localSequence)
        {
            var length1 = GetRemoteSequence(in _position, key, Strategy.VerticalFirst, buffer1, out var position1);
            var length2 = GetRemoteSequence(in _position, key, Strategy.HorizontalFirst, buffer2, out var position2);

            if (length1 <= length2)
            {
                buffer1[..length1].CopyTo(remoteSequence[i..]);
                _position = position1;
                i += length1;
            }
            else
            {
                buffer2[..length2].CopyTo(remoteSequence[i..]);
                _position = position2;
                i += length2;
            }
        }

        //Console.WriteLine();

        return i;
    }

    private static int GetRemoteSequence(
        in KeypadPosition start, byte localKey, Strategy strategy, Span<byte> remoteSequence,
        out KeypadPosition destination)
    {
        if (!NumericKeypad.TryGetPosition(localKey, out destination))
            throw new InvalidOperationException();

        var dx = destination.X - start.X;
        var dy = destination.Y - start.Y;

        var i = 0;
        var (horizontalKey, verticalKey) = DirectionalKeypad.GetDirectionalKeys(dx, dy);

        switch (strategy)
        {
            case Strategy.HorizontalFirst:
                // make sure to avoid gap
                if (NumericKeypad.TryGetKey(new(start.X, destination.Y), out _))
                {
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                }
                else
                {
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                }
                break;

            case Strategy.VerticalFirst:
                // make sure to avoid gap
                if (NumericKeypad.TryGetKey(new(destination.X, start.Y), out _))
                {
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                }
                else
                {
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                }
                break;
        }

        remoteSequence[i++] = DirectionalKeypad.Activate;

        //Console.Write($"{(char)localKey}: {Encoding.UTF8.GetString(remoteSequence[..i])} ({dx},{dy}), ");

        return i;
    }

    public static bool TryStepHorizontal(KeypadPosition current, byte localKey, out KeypadPosition next)
    {
        if (!NumericKeypad.TryGetPosition(localKey, out var target))
            throw new InvalidOperationException();

        var dx = Math.Sign(target.X - current.X);
        byte remoteKey = dx switch
        {
            > 0 => DirectionalKeypad.Right,
            < 0 => DirectionalKeypad.Left,
            0 => 0
        };

        var destination = current.Move(dx, 0);
        if (remoteKey == 0 || !NumericKeypad.TryGetKey(in destination, out _))
        {
            next = KeypadPosition.Invalid;
            return false;
        }

        next = destination;
        return true;
    }

    public static bool TryStepVertical(KeypadPosition current, byte localKey, out KeypadPosition next)
    {
        if (!NumericKeypad.TryGetPosition(localKey, out var target))
            throw new InvalidOperationException();

        var dy = Math.Sign(target.Y - current.Y);
        byte remoteKey = dy switch
        {
            > 0 => DirectionalKeypad.Down,
            < 0 => DirectionalKeypad.Up,
            0 => 0
        };

        var destination = current.Move(0, dy);
        if (remoteKey == 0 || !NumericKeypad.TryGetKey(in destination, out _))
        {
            next = KeypadPosition.Invalid;
            return false;
        }

        next = destination;
        return true;
    }

    static int AddKeys(byte key, int count, Span<byte> sequence)
    {
        sequence[..count].Fill(key);
        return count;
    }
}

struct DirectionalButtonPresser
{
    private KeypadPosition _position;

    public readonly KeypadPosition Position => _position;

    public int GetRemoteSequence(ReadOnlySpan<byte> localSequence, Span<byte> remoteSequence)
    {
        //Console.WriteLine($"Calculating sequence for '{Encoding.UTF8.GetString(localSequence)}':");

        Span<byte> buffer1 = stackalloc byte[32];
        Span<byte> buffer2 = stackalloc byte[32];

        var i = 0;
        foreach (var key in localSequence)
        {
            var length1 = GetRemoteSequence(in _position, key, Strategy.VerticalFirst, buffer1, out var position1);
            var length2 = GetRemoteSequence(in _position, key, Strategy.HorizontalFirst, buffer2, out var position2);

            if (length1 <= length2)
            {
                buffer1[..length1].CopyTo(remoteSequence[i..]);
                _position = position1;
                i += length1;
            }
            else
            {
                buffer2[..length2].CopyTo(remoteSequence[i..]);
                _position = position2;
                i += length2;
            }
        }

        //Console.WriteLine();

        return i;
    }

    public static int GetRemoteSequence(
        in KeypadPosition start, byte localKey, Strategy strategy, Span<byte> remoteSequence,
        out KeypadPosition destination)
    {
        if (!DirectionalKeypad.TryGetPosition(localKey, out destination))
            throw new InvalidOperationException();

        var dx = destination.X - start.X;
        var dy = destination.Y - start.Y;

        var i = 0;
        var (horizontalKey, verticalKey) = DirectionalKeypad.GetDirectionalKeys(dx, dy);

        switch (strategy)
        {
            case Strategy.HorizontalFirst:
                // make sure to avoid gap
                if (DirectionalKeypad.TryGetKey(new(start.X, destination.Y), out _, out _, out _))
                {
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                }
                else
                {
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                }
                break;

            case Strategy.VerticalFirst:
                // make sure to avoid gap
                if (DirectionalKeypad.TryGetKey(new(destination.X, start.Y), out _, out _, out _))
                {
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                }
                else
                {
                    i += AddKeys(verticalKey, Math.Abs(dy), remoteSequence[i..]);
                    i += AddKeys(horizontalKey, Math.Abs(dx), remoteSequence[i..]);
                }
                break;
        }

        remoteSequence[i++] = DirectionalKeypad.Activate;

        //Console.Write($"{(char)localKey}: {Encoding.UTF8.GetString(remoteSequence[..i])} ({dx},{dy}), ");

        return i;
    }

    public static bool TryStepHorizontal(KeypadPosition current, byte localKey, out KeypadPosition next)
    {
        if (!DirectionalKeypad.TryGetPosition(localKey, out var target))
            throw new InvalidOperationException();

        var dx = Math.Sign(target.X - current.X);
        byte remoteKey = dx switch
        {
            > 0 => DirectionalKeypad.Right,
            < 0 => DirectionalKeypad.Left,
            0 => 0
        };

        var destination = current.Move(dx, 0);
        if (remoteKey == 0 || !DirectionalKeypad.TryGetKey(in destination, out _, out _, out _))
        {
            next = KeypadPosition.Invalid;
            return false;
        }

        next = destination;
        return true;
    }

    public static bool TryStepVertical(KeypadPosition current, byte localKey, out KeypadPosition next)
    {
        if (!DirectionalKeypad.TryGetPosition(localKey, out var target))
            throw new InvalidOperationException();

        var dy = Math.Sign(target.Y - current.Y);
        byte remoteKey = dy switch
        {
            > 0 => DirectionalKeypad.Down,
            < 0 => DirectionalKeypad.Up,
            0 => 0
        };

        var destination = current.Move(0, dy);
        if (remoteKey == 0 || !DirectionalKeypad.TryGetKey(in destination, out _, out _, out _))
        {
            next = KeypadPosition.Invalid;
            return false;
        }

        next = destination;
        return true;
    }

    static int AddKeys(byte key, int count, Span<byte> sequence)
    {
        sequence[..count].Fill(key);
        return count;
    }
}

struct DirectionalKeypad
{
    public const byte Activate = (byte)'A';
    public const byte Up = (byte)'^';
    public const byte Down = (byte)'v';
    public const byte Left = (byte)'<';
    public const byte Right = (byte)'>';

    public static bool TryGetKey(in KeypadPosition position, out byte key, out sbyte dx, out sbyte dy)
    {
        key = (position.X, position.Y) switch
        {
            (0, 0) => Activate,
            (-1, 0) => Up,
            (-2, 1) => Left,
            (-1, 1) => Down,
            (0, 1) => Right,
            _ => 0
        };

        (dx, dy) = ((sbyte, sbyte))(key switch
        {
            Activate => (0, 0),
            Up => (0, -1),
            Left => (-1, 0),
            Down => (0, 1),
            Right => (1, 0),
            _ => (0, 0)
        });

        return key != 0;
    }

    public static bool TryGetPosition(byte key, out KeypadPosition position)
    {
        position = key switch
        {
            Activate => new(0, 0),
            Up => new(-1, 0),
            Left => new(-2, 1),
            Down => new(-1, 1),
            Right => new(0, 1),
            _ => KeypadPosition.Invalid
        };

        return position != KeypadPosition.Invalid;
    }

    public static (byte Horizontal, byte Vertical) GetDirectionalKeys(int dx, int dy)
    {
        byte horizontal = dx switch
        {
            > 0 => Right,
            < 0 => Left,
            0 => 0
        };

        byte vertical = dy switch
        {
            > 0 => Down,
            < 0 => Up,
            0 => 0
        };

        return (horizontal, vertical);
    }
}

struct NumericKeypad
{
    public const byte Activate = (byte)'A';

    public static bool TryGetKey(in KeypadPosition position, out byte key)
    {
        key = (position.X, position.Y) switch
        {
            (0, 0) => Activate,
            (-1, 0) => (byte)'0',
            (-2, -1) => (byte)'1',
            (-1, -1) => (byte)'2',
            (0, -1) => (byte)'3',
            (-2, -2) => (byte)'4',
            (-1, -2) => (byte)'5',
            (0, -2) => (byte)'6',
            (-2, -3) => (byte)'7',
            (-1, -3) => (byte)'8',
            (0, -3) => (byte)'9',
            _ => 0
        };

        return key != 0;
    }

    public static bool TryGetPosition(byte key, out KeypadPosition position)
    {
        position = key switch
        {
            Activate => new(0, 0),
            (byte)'0' => new(-1, 0),
            (byte)'1' => new(-2, -1),
            (byte)'2' => new(-1, -1),
            (byte)'3' => new(0, -1),
            (byte)'4' => new(-2, -2),
            (byte)'5' => new(-1, -2),
            (byte)'6' => new(0, -2),
            (byte)'7' => new(-2, -3),
            (byte)'8' => new(-1, -3),
            (byte)'9' => new(0, -3),
            _ => KeypadPosition.Invalid
        };

        return position != KeypadPosition.Invalid;
    }
}

record struct KeypadPosition(sbyte X, sbyte Y)
{
    public static KeypadPosition Invalid { get; } = new(sbyte.MinValue, sbyte.MinValue);

    public readonly KeypadPosition Move(int dx, int dy) => new((sbyte)(X + dx), (sbyte)(Y + dy));
}

record KeyNode(byte RemoteKey, KeypadPosition Position, int Cost, KeyNode? Prevous)
{
    public static KeyNode Start(byte key, in KeypadPosition position)
        => new(key, position, Cost: 0, Prevous: null);

    public KeyNode Next(byte key, in KeypadPosition position, int cost)
        => new(key, position, Cost + cost, Prevous: this);
}

enum DirectionCode
{
    Activate,
    Up,
    Down,
    Left,
    Right
}

enum Strategy
{
    HorizontalFirst,
    VerticalFirst,
    Alternate
}
