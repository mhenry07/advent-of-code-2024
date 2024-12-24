using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    x00: 1
    x01: 1
    x02: 1
    y00: 0
    y01: 1
    y02: 0

    x00 AND y00 -> z00
    x01 XOR y01 -> z01
    x02 OR y02 -> z02
    """u8;

var exampleBytes2 = """
    x00: 1
    x01: 0
    x02: 1
    x03: 1
    x04: 0
    y00: 1
    y01: 1
    y02: 1
    y03: 1
    y04: 1

    ntg XOR fgs -> mjb
    y02 OR x01 -> tnw
    kwq OR kpj -> z05
    x00 OR x03 -> fst
    tgd XOR rvg -> z01
    vdt OR tnw -> bfw
    bfw AND frj -> z10
    ffh OR nrd -> bqk
    y00 AND y03 -> djm
    y03 OR y00 -> psh
    bqk OR frj -> z08
    tnw OR fst -> frj
    gnj AND tgd -> z11
    bfw XOR mjb -> z00
    x03 OR x00 -> vdt
    gnj AND wpb -> z02
    x04 AND y00 -> kjc
    djm OR pbm -> qhw
    nrd AND vdt -> hwm
    kjc AND fst -> rvg
    y04 OR y02 -> fgs
    y01 AND x02 -> pbm
    ntg OR kjc -> kwq
    psh XOR fgs -> tgd
    qhw XOR tgd -> z09
    pbm OR djm -> kpj
    x03 XOR y03 -> ffh
    x00 XOR y04 -> ntg
    bfw OR bqk -> z06
    nrd XOR fgs -> wpb
    frj XOR qhw -> z04
    bqk OR frj -> z07
    y03 OR x01 -> nrd
    hwm AND bqk -> z03
    tgd XOR rvg -> z12
    tnw OR pbm -> gnj
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    2 => exampleBytes2,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

var And = "AND"u8;
var Or = "OR"u8;
var OutputSeparator = " -> "u8;
var Xor = "XOR"u8;
var WireValueSeparator = ": "u8;

var gates = new Queue<Gate>();
var values = new Dictionary<string, byte>(new Utf8ByteStringEqualityComparer());
var lookup = values.GetAlternateLookup<ReadOnlySpan<byte>>();
var section = 0;
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
    {
        section++;
        continue;
    }

    switch (section)
    {
        case 0:
            var wireValueIndex = line.IndexOf(WireValueSeparator);
            var name = line[..wireValueIndex];
            var value = byte.Parse(line[(wireValueIndex + WireValueSeparator.Length)..]);
            lookup.TryAdd(name, value);

            break;

        case 1:
            var outputIndex = line.IndexOf(OutputSeparator);
            var inputs = line[..outputIndex];
            var input1 = "";
            var input2 = "";
            var @operator = Operator.Unknown;
            var i = 0;
            foreach (var inputRange in inputs.Split((byte)' '))
            {
                var input = inputs[inputRange];
                switch (i)
                {
                    case 0:
                        input1 = Encoding.UTF8.GetString(input);
                        break;

                    case 1:
                        if (input.SequenceEqual("AND"u8))
                            @operator = Operator.And;
                        else if (input.SequenceEqual("OR"u8))
                            @operator = Operator.Or;
                        else if (input.SequenceEqual("XOR"u8))
                            @operator = Operator.Xor;
                        else
                            throw new InvalidDataException($"'{Encoding.UTF8.GetString(input)}'");
                        break;

                    case 2:
                        input2 = Encoding.UTF8.GetString(input);
                        break;
                }

                i++;
            }

            var output = Encoding.UTF8.GetString(line[(outputIndex + OutputSeparator.Length)..]);

            var gate = new Gate(input1, @operator, input2, output);
            if (!gate.TryProduceOutput(values, out _))
                gates.Enqueue(gate);

            break;
    }
}

while (gates.TryDequeue(out var gate))
{
    if (!gate.TryProduceOutput(values, out _))
        gates.Enqueue(gate);
}

ulong total1 = 0UL;
foreach (var (name, value) in values)
{
    if (!name.StartsWith('z'))
        continue;

    var index = int.Parse(name.AsSpan(1));
    if (index >= sizeof(long) * 8)
        throw new InvalidDataException();

    total1 |= (ulong)value << index;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

record struct WireValue(string Name, byte Value);

record struct Gate(string Input1, Operator Operator, string Input2, string Output)
{
    public readonly bool TryProduceOutput(Dictionary<string, byte> values, out byte output)
    {
        if (values.TryGetValue(Input1, out var input1) && values.TryGetValue(Input2, out var input2))
        {
            output = ComputeValue(input1, input2);
            values.Add(Output, output);
            return true;
        }

        output = default;
        return false;
    }

    public readonly byte ComputeValue(byte input1, byte input2)
        => (byte)(Operator switch
        {
            Operator.And => input1 & input2,
            Operator.Or => input1 | input2,
            Operator.Xor => input1 ^ input2,
            _ => throw new InvalidOperationException()
        });
}

enum Operator
{
    Unknown,
    And,
    Or,
    Xor
}
