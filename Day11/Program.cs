using System.Buffers.Text;
using System.Text;
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
var numStones1 = 0;
var numStones2 = 0;
var nextStones = new PoolableList<Stone>();
for (var blink = 1; blink <= Math.Max(numBlinks1, numBlinks2); blink++)
{
    var blinkStart = TimeProvider.System.GetTimestamp();
    nextStones.Reset();
    var stonesSpan = stones.Span;
    foreach (var stone in stonesSpan)
    {
        if (stone.Number == 0)
        {
            nextStones.Add(new(1, 1));
        }
        else if (stone.Digits.IsEven())
        {
            stone.Split(out var left, out var right);
            nextStones.Add(left);
            nextStones.Add(right);
        }
        else
        {
            nextStones.Add(stone.Multiply2024());
        }
    }

    (nextStones, stones) = (stones, nextStones);

    var numStones = stones.Length;
    Console.WriteLine($"Blink: {blink}, Stones: {numStones:N0}, Elapsed: {TimeProvider.System.GetElapsedTime(blinkStart)}");

    if (blink == numBlinks1)
        numStones1 = numStones;

    if (blink == numBlinks2)
        numStones2 = numStones;

    //Format(stringBuilder, stones);
    //Console.WriteLine(stringBuilder);

}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Number of stones: {numStones1}");
Console.WriteLine($"Part 2: Number of stones: {numStones2}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static void Format(StringBuilder builder, LinkedList<Stone> stones)
{
    builder.Clear();
    var i = 0;
    foreach (var stone in stones)
    {
        if (i > 0)
            builder.Append(' ');

        if (builder.Length >= 60)
        {
            builder.Append("...");
            break;
        }

        builder.Append(stone.Number);

        i++;
    }

    builder.Append("   <== ");
    builder.Append(stones.Count);
    builder.Append(" stones");
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
