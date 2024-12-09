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

var disk1 = disk.Span.ToArray().AsSpan();
var contiguousLength1 = CompactFilesFragmenting(disk1);
var checksum1 = CalculateChecksum(disk1);

var disk2 = disk.Span.ToArray().AsSpan();
CompactFilesNonFragmenting(disk2);
var checksum2 = CalculateChecksum(disk2);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 checksum: {checksum1}");
Console.WriteLine($"Part 2 checksum: {checksum2}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static long CalculateChecksum(Span<short?> disk)
{
    var checksum = 0L;
    for (var i = 0; i < disk.Length; i++)
    {
        var id = disk[i];
        if (!id.HasValue)
            continue;

        checksum += i * id.Value;
    }

    return checksum;
}

static int CompactFilesFragmenting(Span<short?> disk)
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

        var fileId = disk[j];
        if (IsFree(fileId))
        {
            j--;
            continue;
        }

        disk[i++] = fileId;
        disk[j--] = null;

        //FormatDisk(builder, disk);
        //Console.WriteLine(builder);
        //builder.Clear();
    }

    return j + 1;
}

static void CompactFilesNonFragmenting(Span<short?> disk)
{
    var builder = new StringBuilder(disk.Length);
    var i = 0;
    var j = disk.Length - 1;
    while (i < j)
    {
        if (!TryGetNextFreeBlock(disk, i, j, out i, out _))
            break;

        var fileId = disk[j];
        if (IsFree(fileId))
        {
            j--;
            continue;
        }

        var fileLength = GetFileLength(disk, j);
        var fileStart = j - fileLength + 1;
        j = fileStart - 1;
        if (TryGetFreeBlock(disk, i, fileStart, fileLength, out var freeStart, out _))
            MoveFile(disk, fileStart, fileLength, freeStart);
        else
            continue;

        //FormatDisk(builder, disk);
        //Console.WriteLine(builder);
        //builder.Clear();
    }
}

static int GetFileLength(ReadOnlySpan<short?> disk, int end)
{
    var id = disk[end];
    if (IsFree(id))
        return 0;

    var length = 1;
    for (var i = end - 1; i >= 0; i--)
    {
        if (disk[i] != id)
            break;

        length++;
    }

    return length;
}

static byte GetMapValue(byte utf8Byte) => (byte)(utf8Byte - (byte)'0');

static bool IsFree(short? block) => !block.HasValue;

static void MoveFile(Span<short?> disk, int fileStart, int fileLength, int freeStart)
{
    var originalFile = disk.Slice(fileStart, fileLength);
    originalFile.CopyTo(disk[freeStart..]);

    for (var i = 0; i < fileLength; i++)
        originalFile[i] = null;
}

static bool TryGetFreeBlock(
    ReadOnlySpan<short?> disk, int start, int end, int minSize, out int freeStart, out int freeLength)
{
    while (TryGetNextFreeBlock(disk, start, end, out freeStart, out freeLength))
    {
        if (freeLength >= minSize)
            return true;

        start = freeStart + freeLength;
    }

    freeStart = -1;
    freeLength = 0;
    return false;
}

static bool TryGetNextFreeBlock(ReadOnlySpan<short?> disk, int start, int end, out int freeStart, out int freeLength)
{
    freeStart = -1;
    freeLength = 0;
    for (var i = start; i < end; i++)
    {
        var id = disk[i];
        if (freeStart == -1 && IsFree(id))
            freeStart = i;
        if (IsFree(id))
            freeLength++;
        else if (freeStart != -1)
            break;
    }

    return freeStart != -1;
}
