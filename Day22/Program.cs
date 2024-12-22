using System.Buffers.Text;

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
var bestSequence = default(ChangeSequence);
var monkeySeenSequences = new HashSet<ChangeSequence>(NumRounds);
Span<int> priceChanges = new int[NumRounds];
var sequenceBananas = new Dictionary<ChangeSequence, long>(NumRounds);
var total1 = 0L;
var totalBananas = 0L;
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
        continue;

    Utf8Parser.TryParse(line, out long initialSecretNumber, out _);

    var previousPrice = (int)(initialSecretNumber % 10);
    var secretNumber = initialSecretNumber;
    for (var i = 0; i < NumRounds; i++)
    {
        secretNumber = Next(secretNumber);
        var price = (int)(secretNumber % 10);
        priceChanges[i] = price - previousPrice;
        previousPrice = price;

        if (i >= 3)
        {
            var sequence = ChangeSequence.FromSpan(priceChanges.Slice(i - 3, 4));
            if (!monkeySeenSequences.Add(sequence))
                continue;

            if (sequenceBananas.TryGetValue(sequence, out var bananas))
            {
                sequenceBananas[sequence] = bananas + price;
                if (bananas + price > totalBananas)
                {
                    bestSequence = sequence;
                    totalBananas = bananas + price;
                }
            }
            else
            {
                sequenceBananas.Add(sequence, price);
            }
        }
    }

    monkeySeenSequences.Clear();
    priceChanges.Clear();

    total1 += secretNumber;
    j++;
}

Console.WriteLine($"Best sequence: {bestSequence}, total bananas: {totalBananas}");

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

    public readonly override string ToString() => $"[{A},{B},{C},{D}]";
}
