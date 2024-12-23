using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    kh-tc
    qp-kh
    de-cg
    ka-co
    yn-aq
    qp-ub
    cg-tb
    vc-aq
    tb-ka
    wh-tc
    yn-cg
    kh-ub
    ta-co
    de-co
    tc-td
    tb-wq
    wh-td
    ta-ka
    td-qp
    aq-cg
    wq-ub
    ub-vc
    de-ta
    wq-aq
    wq-vc
    wh-yn
    ka-de
    kh-ta
    co-tc
    wh-qp
    tb-vc
    td-yn
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var computers = new Dictionary<string, Computer>(new ComputerNameEqualityComparer());
var lookup = computers.GetAlternateLookup<ReadOnlySpan<byte>>();
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
        continue;

    if (line.Length != 5 || line.IndexOf((byte)'-') != 2)
        throw new InvalidDataException();

    var nameA = line[..2];
    if (!lookup.TryGetValue(nameA, out var computerA))
    {
        computerA = new Computer(Encoding.UTF8.GetString(nameA));
        lookup.TryAdd(nameA, computerA);
    }

    var nameB = line[^2..];
    if (!lookup.TryGetValue(nameB, out var computerB))
    {
        computerB = new Computer(Encoding.UTF8.GetString(nameB));
        lookup.TryAdd(nameB, computerB);
    }

    computerA.Connections.Add(computerB);
    computerB.Connections.Add(computerA);
}

Span<string?> nameBuffer = new string[3];
Span<string?> sortBuffer = new string[3];
var setsOfThree = new HashSet<string>();
var setsOfThreeT = new HashSet<string>();
foreach (var computer in computers.Values)
{
    nameBuffer[0] = computer.Name;
    foreach (var second in computer.Connections)
    {
        if (nameBuffer[..1].Contains(second.Name))
            continue;

        nameBuffer[1] = second.Name;
        foreach (var third in second.Connections)
        {
            if (nameBuffer[..2].Contains(third.Name))
                continue;

            nameBuffer[2] = third.Name;
            foreach (var last in third.Connections)
            {
                if (last.Name == computer.Name)
                {
                    nameBuffer.CopyTo(sortBuffer);
                    sortBuffer.Sort();

                    var set = string.Join(',', sortBuffer);
                    setsOfThree.Add(set);

                    if (AnyStartsWithT(sortBuffer))
                        setsOfThreeT.Add(set);
                }
            }
        }
    }
}

//Console.WriteLine("Sets of three:");
//foreach (var set in setsOfThree.Order())
//    Console.WriteLine(set);

//Console.WriteLine();
//Console.WriteLine("Sets of three containing 't':");
//foreach (var tSet in setsOfThreeT.Order())
//    Console.WriteLine(tSet);

var total1 = setsOfThreeT.Count;
var passwordLength = GetPassword([.. computers.Values], out var password);

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Part 2: {password} ({passwordLength})");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool AnyStartsWithT(ReadOnlySpan<string?> names)
{
    foreach (var name in names)
        if (name?.StartsWith('t') is true)
            return true;

    return false;
}

static int GetPassword(Span<Computer> computers, out string password)
{
    computers.Sort((x, y) =>
    {
        var connectionsComparison = -x.Connections.Count.CompareTo(y.Connections.Count);
        return connectionsComparison != 0
            ? connectionsComparison
            : x.Name.CompareTo(y.Name);
    });

    var bestCandidates = new HashSet<string>();
    var bestLength = 0;
    var array = ArrayPool<string>.Shared.Rent(computers.Length);
    var candidates = array.AsSpan();
    var connections = new HashSet<string>();
    foreach (var first in computers)
    {
        var i = 0;
        candidates[i++] = first.Name;

        foreach (var second in first.Connections)
        {
            connections.Clear();
            foreach (var connection in second.Connections)
                connections.Add(connection.Name);

            var isConnected = true;
            foreach (var candidate in candidates[..i])
                isConnected &= connections.Contains(candidate);

            if (isConnected)
                candidates[i++] = second.Name;
        }

        if (i > bestLength)
        {
            bestCandidates.Clear();
            foreach (var candidate in candidates[..i])
                bestCandidates.Add(candidate);

            bestLength = i;
        }

        candidates.Clear();
    }

    ArrayPool<string>.Shared.Return(array);

    password = string.Join(',', bestCandidates.Order());
    return bestLength;
}

[DebuggerDisplay("{Name} [{Connections.Count}]")]
class Computer(string name)
{
    public List<Computer> Connections { get; } = [];
    public string Name { get; } = name;
}

class ComputerNameEqualityComparer : IAlternateEqualityComparer<ReadOnlySpan<byte>, string>, IEqualityComparer<string>
{
    public string Create(ReadOnlySpan<byte> alternate) => Encoding.UTF8.GetString(alternate);

    public bool Equals(ReadOnlySpan<byte> alternate, string other)
        => alternate.Length == 2 && other.Length == 2 && alternate[0] == other[0] && alternate[1] == other[1];

    public bool Equals(string? x, string? y)
    {
        if (x is null || y is null)
            return x is null && y is null;

        return Equals(x, y);
    }

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        var hashCode = new HashCode();
        hashCode.AddBytes(alternate);
        return hashCode.ToHashCode();
    }

    public int GetHashCode([DisallowNull] string obj) => obj.GetHashCode();
}
