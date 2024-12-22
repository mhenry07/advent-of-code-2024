using System.Buffers.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    1
    10
    100
    2024
    """u8;

var exampleBytes2 = """
    1
    2
    3
    2024
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

const int NumRounds = 2_000;

var j = 0;
var total1 = 0L;
using var monkeyPrices = new PoolableList<int[]>();
using var monkeyPriceChanges = new PoolableList<int[]>();
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
        continue;

    Utf8Parser.TryParse(line, out long initialSecretNumber, out _);

    var prices = new int[2_001];
    var priceChanges = new int[2_001];
    prices[0] = (int)(initialSecretNumber % 10);
    priceChanges[0] = 0;
    var secretNumber = initialSecretNumber;
    for (var i = 0; i < NumRounds; i++)
    {
        secretNumber = Next(secretNumber);
        var price = prices[i + 1] = (int)(secretNumber % 10);
        priceChanges[i + 1] = price - prices[i];
    }

    monkeyPrices.Add(prices);
    monkeyPriceChanges.Add(priceChanges);

    total1 += secretNumber;
    j++;
}

Console.WriteLine($"Part 1 elapsed: {TimeProvider.System.GetElapsedTime(start)}");

Span<int> bestSequence = [];
var checkedSequences = new Dictionary<ChangeSequence, long>();
var totalBananas = 0L;
var monkeyPricesSpan = monkeyPrices.Span;
var monkeyPriceChangesSpan = monkeyPriceChanges.Span;

// the best sequence may not exist in the first monkey's price changes,
// but if a sequence is missing from too many monkeys, it will be highly unlikely to yield the most bananas
for (var l = 0; l < monkeyPricesSpan.Length / 2; l++)
{
    // check all possible sequences for each monkey
    for (var k = 0; k <= NumRounds - 3; k++)
    {
        var bananas = 0L;
        var sequence = monkeyPriceChanges[l].AsSpan(k, 4);
        var key = ChangeSequence.FromSpan(sequence);
        if (checkedSequences.ContainsKey(key))
            continue;

        // count bananas for the sequence across all monkeys
        for (var i = 0; i < monkeyPricesSpan.Length; i++)
        {
            var prices = monkeyPricesSpan[i].AsSpan();
            var priceChanges = monkeyPriceChangesSpan[i].AsSpan();

            var changeIndex = priceChanges.IndexOf(sequence);
            if (changeIndex >= 0 && changeIndex <= NumRounds - 3)
            {
                bananas += prices[changeIndex + 3];
            }
        }

        checkedSequences.Add(key, bananas);
        if (bananas > totalBananas)
        {
            totalBananas = bananas;
            bestSequence = sequence;
        }
    }

    Console.WriteLine($"Best bananas after monkey {l}: bananas: {totalBananas}, sequence: {string.Join(',', bestSequence.ToArray())}, elapsed: {TimeProvider.System.GetElapsedTime(start).TotalMilliseconds} ms");
}

Console.WriteLine($"Best sequence: {string.Join(',', bestSequence.ToArray())}, total bananas: {totalBananas}");

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Part 2: {totalBananas}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static long Next(long secretNumber)
{
    secretNumber = Prune(Mix(
        secretNumber << 6, // multiply by 64
        secretNumber));

    secretNumber = Prune(Mix(
        secretNumber >> 5, // divide by 32
        secretNumber));

    secretNumber = Prune(Mix(
        secretNumber << 11, // multiply by 2048
        secretNumber));

    return secretNumber;
}

static long Mix(long value, long secretNumber)
    => value ^ secretNumber;

static long Prune(long secretNumber)
    => secretNumber % 16777216;

record struct ChangeSequence(sbyte A, sbyte B, sbyte C, sbyte D)
{
    public static ChangeSequence FromSpan(ReadOnlySpan<int> source)
        => new((sbyte)source[0], (sbyte)source[1], (sbyte)source[2], (sbyte)source[3]);

    public readonly bool Equals(ReadOnlySpan<int> other)
        => A == other[0] && B == other[1] && C == other[2] && D == other[3];
}
