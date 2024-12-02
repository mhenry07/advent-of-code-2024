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
var dampenerSafeReports = 0;
foreach (var lineRange in MemoryExtensions.Split(bytes, "\r\n"u8))
{
    var line = bytes[lineRange];
    if (line.IsEmpty)
        continue;

    if (IsSafe(line) is true)
    {
        safeReports++;
        dampenerSafeReports++;
    }
    else if (IsSafeWithDampener(line) is true)
    {
        dampenerSafeReports++;
    }
}

Console.WriteLine($"Safe reports: {safeReports}");
Console.WriteLine($"Safe reports with Problem Dampener: {dampenerSafeReports}");

enum Direction
{
    Decreasing = -1,
    Unknown = 0,
    Increasing = 1
}

static partial class Program
{
    static bool? IsSafe(ReadOnlySpan<byte> line)
    {
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

        return isSafe;
    }

    // naive implementation: O(N^2)
    static bool IsSafeWithDampener(ReadOnlySpan<byte> line)
    {
        var length = 0;
        for (var skip = 0; skip == 0 || skip < length; skip++)
        {
            var direction = Direction.Unknown;
            var i = 0;
            var isSafe = default(bool?);
            var lastLevel = default(int?);
            foreach (var levelRange in MemoryExtensions.Split(line, (byte)' '))
            {
                if (i++ == skip)
                    continue;

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
                return true;

            if (skip == 0)
                length = i;
        }

        return false;
    }

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
