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

var numStones1 = 0L;
foreach (var stone in stones.Span)
    numStones1 += InfiniteCorridors.CountStones(in stone, numBlinks1);

var numStones2 = 0L;
foreach (var stone in stones.Span)
{
    var stoneStart = TimeProvider.System.GetTimestamp();
    numStones2 += InfiniteCorridors.CountStones(in stone, numBlinks2);
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Number of stones: {numStones1} for {numBlinks1} blinks");
Console.WriteLine($"Part 2: Number of stones: {numStones2} for {numBlinks2} blinks");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static class InfiniteCorridors
{
    private static readonly Dictionary<StoneBlinks, long> Memoized = [];

    public static long CountStones(in Stone stone, int numBlinks)
    {
        if (numBlinks == 0)
            return 1;

        var nextBlinks = numBlinks - 1;
        var subTotal = 0L;
        if (stone.Number == 0)
        {
            var one = new Stone(1, 1);
            subTotal += CountStonesMemoized(in one, nextBlinks);
        }
        else if (stone.Digits.IsEven())
        {
            stone.Split(out var left, out var right);
            subTotal += CountStonesMemoized(in left, nextBlinks);
            subTotal += CountStonesMemoized(in right, nextBlinks);
        }
        else
        {
            subTotal += CountStonesMemoized(stone.Multiply2024(), nextBlinks);
        }

        return subTotal;
    }

    private static long CountStonesMemoized(in Stone stone, int numBlinks)
    {
        var key = new StoneBlinks(stone.Number, numBlinks);
        if (!Memoized.TryGetValue(key, out var count))
        {
            count = CountStones(in stone, numBlinks);
            Memoized.Add(key, count);
        }

        return count;
    }

    private record struct StoneBlinks(long Number, int Blinks);
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
