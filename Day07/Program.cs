using System.Buffers.Text;
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

using var numbersList = new PoolableList<int>();
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
        if (!Utf8Parser.TryParse(slice[numberRange], out int number, out _))
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

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Part 2 answer: {total2}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool Calculate(long testValue, long accumulated, ReadOnlySpan<int> numbers)
{
    if (numbers.IsEmpty)
        return accumulated == testValue;

    if (accumulated > testValue)
        return false;

    return Calculate(testValue, accumulated + numbers[0], numbers[1..])
        || Calculate(testValue, accumulated * numbers[0], numbers[1..]);
}

static bool Calculate2(long testValue, long accumulated, ReadOnlySpan<int> numbers)
{
    if (numbers.IsEmpty)
        return accumulated == testValue;

    if (accumulated > testValue)
        return false;

    return Calculate2(testValue, accumulated + numbers[0], numbers[1..])
        || Calculate2(testValue, accumulated * numbers[0], numbers[1..])
        || (TryConcatenate(accumulated, numbers[0], out long concatenated)
            && Calculate2(testValue, concatenated, numbers[1..]));
}

static bool TryConcatenate(long left, long right, out long result)
{
    Span<byte> buffer = stackalloc byte[64];
    result = 0L;
    return Utf8Formatter.TryFormat(left, buffer, out int leftLength)
        && Utf8Formatter.TryFormat(right, buffer[leftLength..], out int rightLength)
        && Utf8Parser.TryParse(buffer[..(leftLength + rightLength)], out result, out _);
}
