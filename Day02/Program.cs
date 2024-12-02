using System.Buffers.Text;
using System.Text;

var useExample = false;
var exampleBytes = """
7 6 4 2 1
1 2 7 8 9
9 7 6 2 1
1 3 2 4 5
8 6 4 4 1
1 3 6 7 9
"""u8;

var bytes = useExample
    ? exampleBytes
    : File.ReadAllBytes("input.txt").AsSpan();
var safeReports = 0;
foreach (var lineRange in MemoryExtensions.Split(bytes, "\r\n"u8))
{
    var line = bytes[lineRange];
    if (line.IsEmpty)
        continue;

    var direction = Direction.Unknown;
    var isSafe = default(bool?);
    var lastLevel = default(int?);
    foreach (var levelRange in MemoryExtensions.Split(line, (byte)' '))
    {
        if (!Utf8Parser.TryParse(line[levelRange], out int level, out _))
            throw new InvalidOperationException($"Failed to parse level from {Encoding.UTF8.GetString(line[levelRange])}");

        if (!lastLevel.HasValue)
        {
            lastLevel = level;
            continue;
        }

        isSafe = IsSafe(level, lastLevel, ref direction);
        lastLevel = level;
        if (isSafe is false)
            break;
    }

    if (isSafe is true)
        safeReports++;
}

Console.WriteLine($"Safe reports: {safeReports}");

enum Direction
{
    Decreasing = -1,
    Unknown = 0,
    Increasing = 1
}

static partial class Program
{
    static bool IsSafe(int level, int? lastLevel, ref Direction reportDirection)
    {
        if (!lastLevel.HasValue)
            return true;

        var diff = level - lastLevel.Value;
        var absDiff = Math.Abs(diff);
        if (absDiff == 0 || absDiff > 3)
            return false;

        var adjacentDirection = (Direction)(diff / absDiff);
        if (reportDirection == Direction.Unknown)
        {
            reportDirection = adjacentDirection;
            return true;
        }

        return adjacentDirection == reportDirection;
    }
}
