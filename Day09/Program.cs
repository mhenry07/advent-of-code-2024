using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = "2333133121414131402"u8;
var exampleBytes2 = "12345"u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var diskMap = bytes;

using var disk = new PoolableList<short?>(bytes.Length * 4);
for (int i = 0, id = 0; i < diskMap.Length; i += 2, id++)
{
    // file
    var fileLength = GetMapValue(diskMap[i]);
    for (var j = 0; j < fileLength; j++)
        disk.Add((short)id);

    // free space
    if (i + 1 < diskMap.Length)
    {
        var freeLength = GetMapValue(diskMap[i + 1]);
        for (var j = 0; j < freeLength; j++)
            disk.Add(null);
    }
}

var diskSpan = disk.Span;
var contiguousLength = CompactFiles(diskSpan);
var checksum1 = CalculateChecksumContiguous(diskSpan);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 checksum: {checksum1}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static void FormatDisk(StringBuilder builder, ReadOnlySpan<short?> disk)
{
    for (var i = 0; i < disk.Length; i++)
    {
        var item = disk[i];
        builder.Append(item.HasValue ? (char)((item.Value % 10) + (byte)'0') : '.');
    }
}

static long CalculateChecksumContiguous(Span<short?> disk)
{
    var checksum = 0L;
    for (var i = 0; i < disk.Length; i++)
    {
        var id = disk[i];
        if (!id.HasValue)
            break;

        checksum += i * id.Value;
    }

    return checksum;
}

static int CompactFiles(Span<short?> disk)
{
    var builder = new StringBuilder(disk.Length);
    var i = 0;
    var j = disk.Length - 1;
    while (i < j)
    {
        if (!IsFree(disk[i]))
        {
            i++;
            continue;
        }

        var block = disk[j];
        if (IsFree(block))
        {
            j--;
            continue;
        }

        disk[i++] = block;
        disk[j--] = null;

        //FormatDisk(builder, disk);
        //Console.WriteLine(builder);
        //builder.Clear();
    }

    return j + 1;
}

static byte GetMapValue(byte utf8Byte) => (byte)(utf8Byte - (byte)'0');

static bool IsFree(short? block) => !block.HasValue;
