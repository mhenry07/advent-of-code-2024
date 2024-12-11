using System.Buffers.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

const int numBlinks1 = 25;
const int numBlinks2 = 75;
int? useExample = null;
var exampleBytes1 = "0 1 10 99 999"u8.ToArray();
var exampleBytes2 = "125 17"u8.ToArray();

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    _ => File.ReadAllBytes("input.txt")
};

var stones = new PoolableList<Stone>();
foreach (var stoneRange in MemoryExtensions.Split(bytes, (byte)' '))
{
    var stoneSpan = bytes.AsSpan(stoneRange);
    if (Utf8Parser.TryParse(stoneSpan, out int number, out int length))
    {
        var stone = new Stone(number, length);
        stones.Add(stone);
    }
}

//var stringBuilder = new StringBuilder();
//Format(stringBuilder, stones);
//Console.WriteLine(stringBuilder);
var numStones1 = 0L;
foreach (var stone in stones.Span)
    numStones1 += CountStones(in stone, numBlinks1);

var numStones2 = 0L;
foreach (var stone in stones.Span)
{
    var stoneStart = TimeProvider.System.GetTimestamp();
    numStones2 += CountStones(in stone, numBlinks2);
    Console.WriteLine($"Starting stone: {stone.Number}, Stones: {numStones2:N0}, Elapsed: {TimeProvider.System.GetElapsedTime(stoneStart)}");
}

//for (var blink = 1; blink <= 6; blink++)
//{
//    var blinkStart = TimeProvider.System.GetTimestamp();
//    var stonesByBlink = 0;
//    foreach (var stone in stones.Span)
//    {
//        stonesByBlink += CountStones(in stone, blink);
//    }

//    Console.WriteLine($"Blink: {blink}, Stones: {stonesByBlink:N0}, Elapsed: {TimeProvider.System.GetElapsedTime(blinkStart)}");
//}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Number of stones: {numStones1} for {numBlinks1} blinks");
Console.WriteLine($"Part 2: Number of stones: {numStones2} for {numBlinks2} blinks");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static int CountStones(in Stone stone, int numBlinks)
{
    if (numBlinks == 0)
        return 1;

    var count = 0;
    if (stone.Number == 0)
    {
        count += CountStones(new(1, 1), numBlinks - 1);
    }
    else if (stone.Digits.IsEven())
    {
        stone.Split(out var left, out var right);
        count += CountStones(in left, numBlinks - 1);
        count += CountStones(in right, numBlinks - 1);
    }
    else
    {
        count += CountStones(stone.Multiply2024(), numBlinks - 1);
    }

    return count;
}

record struct Stone(long Number, int Digits)
{
    public readonly Stone Multiply2024()
    {
        var number = Number * 2024L;
        var digits = number >= (Digits + 3).Pow10()
            ? Digits + 4
            : Digits + 3;

        //Debug.Assert(number >= 0L);
        //Debug.Assert(digits == $"{number}".Length);
        return new(number, digits);
    }

    public readonly void Split(out Stone left, out Stone right)
    {
        var digits = Digits / 2;
        var divisor = digits.Pow10();
        left = new Stone(Number / divisor, digits);

        var rightNumber = Number % divisor;
        if (rightNumber < 10)
        {
            right = new Stone(rightNumber, 1);
            return;
        }

        var rightDigits = digits;
        while (rightNumber < (rightDigits - 1).Pow10())
            rightDigits--;

        right = new Stone(rightNumber, rightDigits);
    }
}

static class IntegerExtensions
{
    static readonly long[] PowersOfTen =
        [
            1,
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

    public static bool IsEven(this int value) => value == (value >> 1) << 1;

    public static long Pow10(this int exponent) => PowersOfTen[exponent];
}
