using System.Buffers.Text;
using System.Text;

var start = TimeProvider.System.GetTimestamp();

const int numBlinks = 25;
int? useExample = null;
var exampleBytes1 = "0 1 10 99 999"u8.ToArray();
var exampleBytes2 = "125 17"u8.ToArray();

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    _ => File.ReadAllBytes("input.txt")
};

var stones = new LinkedList<Stone>();
foreach (var stoneRange in MemoryExtensions.Split(bytes, (byte)' '))
{
    var stoneSpan = bytes.AsSpan(stoneRange);
    if (Utf8Parser.TryParse(stoneSpan, out int number, out int length))
    {
        var stone = new Stone(number, length);
        stones.AddLast(stone);
    }
}

//var stringBuilder = new StringBuilder();
//Format(stringBuilder, stones);
//Console.WriteLine(stringBuilder);
for (var blink = 0; blink < numBlinks; blink++)
{
    var node = stones.First;
    while (node != null)
    {
        if (node.Value.Number == 0)
        {
            node.ValueRef.Number = 1;
        }
        else if (node.Value.Digits.IsEven())
        {
            node.Value.Split(out var left, out var right);
            stones.AddBefore(node, left);
            node.ValueRef = right;
        }
        else
        {
            node.ValueRef = node.Value.Multiply2024();
        }

        node = node.Next;
    }

    //Format(stringBuilder, stones);
    //Console.WriteLine(stringBuilder);
}

var numStones = stones.Count;

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Number of stones: {numStones}");
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
