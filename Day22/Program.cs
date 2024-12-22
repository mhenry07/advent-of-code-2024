using System.Buffers.Text;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    1
    10
    100
    2024
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

const int NumRounds = 2_000;

var total1 = 0L;
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
        continue;

    Utf8Parser.TryParse(line, out long initialSecretNumber, out _);

    var secretNumber = initialSecretNumber;
    for (var i = 0; i < NumRounds; i++)
        secretNumber = Next(secretNumber);

    total1 += secretNumber;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
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
