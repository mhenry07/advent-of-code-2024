using System.Buffers.Text;
using Core;

var useExample = false;

var exampleBytes = """
3   4
4   3
2   5
1   3
3   9
3   3
"""u8;

var bytes = useExample
    ? exampleBytes
    : File.ReadAllBytes("input.txt").AsSpan();
var index = 0;
var left = new PoolableList<int>();
var right = new PoolableList<int>();
foreach (var range in MemoryExtensions.Split(bytes, "\r\n"u8))
{
    var line = bytes[range];
    if (line.Length == 0)
        continue;

    var separatorStart = line.IndexOf((byte)' ');
    var separatorEnd = line.LastIndexOf((byte)' ');
    if (separatorStart > 0 && separatorEnd > separatorStart
        && Utf8Parser.TryParse(line[..separatorStart], out int l, out _)
        && Utf8Parser.TryParse(line[(separatorEnd + 1)..], out int r, out _))
    {
        left.Add(l);
        right.Add(r);
    }
    index++;
}

var leftSpan = left.Span;
var rightSpan = right.Span;
leftSpan.Sort();
rightSpan.Sort();
var totalDistance = 0;
for (var i = 0; i < left.Length; i++)
    totalDistance += Math.Abs(leftSpan[i] - rightSpan[i]);

Console.WriteLine($"Total distance: {totalDistance}");

var previousLeft = 0;
var previousMatches = 0;
var similarityScore = 0;
for (int i = 0, j = 0; i < left.Length; i++)
{
    int matches;
    var l = leftSpan[i];
    if (l == previousLeft)
    {
        matches = previousMatches;
    }
    else
    {
        matches = 0;
        while (j < right.Length)
        {
            var r = rightSpan[j];
            if (l < r)
                break;
            if (l == r)
                matches++;
            if (l >= r)
                j++;
        }
    }

    previousLeft = l;
    previousMatches = matches;
    similarityScore += l * matches;
}

Console.WriteLine($"Similarity score: {similarityScore}");
