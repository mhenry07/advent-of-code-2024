var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes0 = """
..X...
.SAMX.
.A..A.
XMAS.S
.X....
"""u8;
var exampleBytes1 = """
MMMSXXMASM
MSAMXMSMSA
AMXSXMAAMM
MSAMASMSMX
XMASAMXAMM
XXAMMXXAMA
SMSMSASXSS
SAXAMASAAA
MAMMMXMMMM
MXMXAXMASX
"""u8;

var bytes = useExample switch
{
    0 => exampleBytes0,
    1 => exampleBytes1,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

const byte X = (byte)'X';
const byte M = (byte)'M';
const byte A = (byte)'A';
const byte S = (byte)'S';
const int XmasLength = 4;
var Xmas = "XMAS"u8;
var XmasReversed = "SAMX"u8;
var NorthernDirections = new[] { Direction.Northwest, Direction.North, Direction.Northeast }.AsSpan();
var SouthernDirections = new[] { Direction.Southwest, Direction.South, Direction.Southeast }.AsSpan();

var i = 0;
var lineLength = 0;
Span<Range> lineRanges = default;
foreach (var lineRange in MemoryExtensions.Split(bytes, "\r\n"u8))
{
    var (_, length) = lineRange.GetOffsetAndLength(bytes.Length);
    if (i == 0)
    {
        // assume fixed line length
        lineLength = length;
        if (lineLength == 0)
            throw new InvalidOperationException("Expected non-zero line length");
        lineRanges = new Range[(int)Math.Ceiling(bytes.Length / (lineLength + 2.0)) + 1].AsSpan();
    }

    if (length > 0)
        lineRanges[i++] = lineRange;
}

var numLines = i;
lineRanges = lineRanges[..numLines];

ReadOnlySpan<byte> line = default;
ReadOnlySpan<byte> nextLine = default;
var total1 = 0;
for (i = 0; i < numLines; i++)
{
    var prevLine = i > 0 ? line : default;
    line = i == 0 ? bytes[lineRanges[i]] : nextLine;
    nextLine = i < numLines - 1 ? bytes[lineRanges[i + 1]] : default;

    for (var j = 0; j < line.Length; j++)
    {
        if (line[j] != X)
            continue;

        var searchRange = new Range(Math.Max(0, j - 1), Math.Min(lineLength, j + 2));
        if (i >= XmasLength - 1 && prevLine[searchRange].Contains(M))
            foreach (var direction in NorthernDirections)
                if (IsMatch(bytes, lineRanges, Xmas, i, j, direction))
                    total1++;

        if (i <= numLines - XmasLength && nextLine[searchRange].Contains(M))
            foreach (var direction in SouthernDirections)
                if (IsMatch(bytes, lineRanges, Xmas, i, j, direction))
                    total1++;

        if (line[..(j+1)].EndsWith(XmasReversed))
        {
            //Console.WriteLine($"Found at: {j},{i} ({Direction.West})");
            total1++;
        }

        if (line[j..].StartsWith(Xmas))
        {
            //Console.WriteLine($"Found at: {j},{i} ({Direction.East})");
            total1++;
            j += XmasLength - 1;
        }
    }
}

var total2 = 0;
for (i = 0; i < numLines - 1; i++)
{
    var prevLine = i > 0 ? line : default;
    line = i > 0 ? nextLine : bytes[lineRanges[i]];
    nextLine = bytes[lineRanges[i + 1]];

    if (i == 0)
        continue;

    for (var j = 1; j < line.Length - 1; j++)
    {
        if (line[j] != A)
            continue;

        var nw = prevLine[j - 1];
        var ne = prevLine[j + 1];
        var sw = nextLine[j - 1];
        var se = nextLine[j + 1];

        if ((nw == M && se == S || nw == S && se == M)
            && (ne == M && sw == S || ne == S && sw == M))
        {
            //Console.WriteLine($"Found \"X\"-MAS at: {j},{i})");
            total2++;
        }
    }
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"XMAS appearances: {total1}");
Console.WriteLine($"\"X\"-MAS appearances: {total2}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool IsMatch(
    ReadOnlySpan<byte> bytes, ReadOnlySpan<Range> lineRanges, ReadOnlySpan<byte> word, int row, int column,
    Direction direction)
{
    var colIncrement = direction switch
    {
        _ when (direction & Direction.East) != 0 => 1,
        _ when (direction & Direction.West) != 0 => -1,
        _ => 0
    };
    var rowIncrement = direction switch
    {
        _ when (direction & Direction.South) != 0 => 1,
        _ when (direction & Direction.North) != 0 => -1,
        _ => 0
    };

    var endRow = row + rowIncrement * (word.Length - 1);
    if (endRow < 0 || endRow >= lineRanges.Length)
        return false;

    var i = row;
    var j = column;
    for (var k = 0; k < word.Length; k++)
    {
        var line = bytes[lineRanges[i]];
        if (j < 0 || j >= line.Length)
            return false;

        if (line[j] != word[k])
            return false;

        i += rowIncrement;
        j += colIncrement;
    }

    //Console.WriteLine($"Found at: {column},{row} ({direction})");
    return true;
}

[Flags]
enum Direction
{
    None = 0,
    North = 1 << 0,
    South = 1 << 1,
    East = 1 << 2,
    West = 1 << 3,

    Northeast = North | East,
    Northwest = North | West,
    Southeast = South | East,
    Southwest = South | West
}
