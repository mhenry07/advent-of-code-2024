﻿using System.Buffers.Text;
using System.Diagnostics;
using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

bool useExample = false;
var exampleBytes = """
190: 10 19
3267: 81 40 27
83: 17 5
156: 15 6
7290: 6 8 6 15
161011: 16 10 13
192: 17 8 14
21037: 9 7 18 13
292: 11 6 16 20
"""u8;

var bytes = useExample switch
{
    true => exampleBytes,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var (total1, total2) = ProcessInput(bytes);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Part 2 answer: {total2}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static (long Total1, long Total2) ProcessInput(ReadOnlySpan<byte> bytes)
{
    var numbersList = new PoolableList<Number>();
    var total1 = 0L;
    var total2 = 0L;
    foreach (var lineRange in bytes.Split("\r\n"u8))
    {
        var line = bytes[lineRange];
        if (line.IsEmpty)
            continue;

        var colonIndex = line.IndexOf(": "u8);
        if (colonIndex == -1)
            throw new InvalidOperationException($"Expected colon in line: {Encoding.UTF8.GetString(line)}");

        if (!Utf8Parser.TryParse(line[..colonIndex], out long testValue, out _))
            throw new InvalidOperationException($"Expected text value to be a valid number: {Encoding.UTF8.GetString(line)}");

        numbersList.Clear();
        var slice = line[(colonIndex + 2)..];
        foreach (var numberRange in slice.Split((byte)' '))
        {
            if (!Number.TryParse(slice[numberRange], out var number))
                throw new InvalidOperationException($"Expected number: {Encoding.UTF8.GetString(slice[numberRange])}");

            numbersList.Add(number);
        }

        var numbers = numbersList.Span;
        var isValid1 = Calculate(testValue, numbers[0], numbers[1..]);
        if (isValid1)
            total1 += testValue;

        if (isValid1 || Calculate2(testValue, numbers[0], numbers[1..]))
            total2 += testValue;
    }

    return (total1, total2);
}

static bool Calculate(long testValue, Number accumulated, ReadOnlySpan<Number> numbers)
{
    if (numbers.IsEmpty)
        return accumulated.Value == testValue;

    if (accumulated.Value > testValue)
        return false;

    return Calculate(testValue, accumulated + numbers[0], numbers[1..])
        || Calculate(testValue, accumulated * numbers[0], numbers[1..]);
}

static bool Calculate2(long testValue, Number accumulated, ReadOnlySpan<Number> numbers)
{
    if (numbers.IsEmpty)
        return accumulated.Value == testValue;

    if (accumulated.Value > testValue)
        return false;

    return Calculate2(testValue, accumulated + numbers[0], numbers[1..])
        || Calculate2(testValue, accumulated * numbers[0], numbers[1..])
        || Calculate2(testValue, accumulated.Concatenate(numbers[0]), numbers[1..]);
}

record struct Number(long Value, byte Digits)
{
    // used to multiply by 10 * 10^(# digits)
    private static readonly long[] Multipliers =
        [
            long.MaxValue, // 0 digits is invalid
            10,
            100,
            1_000,
            10_000,
            100_000,
            1_000_000,
            10_000_000,
            100_000_000,
            1_000_000_000,
            10_000_000_000,
            100_000_000_000,
            1_000_000_000_000,
            10_000_000_000_000,
            100_000_000_000_000,
            1_000_000_000_000_000,
            10_000_000_000_000_000,
            100_000_000_000_000_000,
            1_000_000_000_000_000_000
        ];

    public static Number operator +(Number left, Number right)
        => new(left.Value + right.Value, default);

    public static Number operator *(Number left, Number right)
        => new(left.Value * right.Value, default);

    public static bool TryParse(ReadOnlySpan<byte> source, out Number number)
    {
        if (Utf8Parser.TryParse(source, out long value, out int length))
        {
            number = new Number(value, (byte)length);
            return true;
        }

        number = default;
        return false;
    }

    public readonly Number Concatenate(Number right)
    {
        Debug.Assert(right.Digits > 0);
        return new(Value * Multipliers[right.Digits] + right.Value, default);
    }
}
