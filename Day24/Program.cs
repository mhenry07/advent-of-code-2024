using System.Buffers.Text;
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

var OutputSeparator = " -> "u8;
var WireValueSeparator = ": "u8;

var gates1 = new Queue<Gate>();
var gates = new PoolableList<Gate>();
var values1 = new Dictionary<string, byte>(new Utf8ByteStringEqualityComparer());
var lookup1 = values1.GetAlternateLookup<ReadOnlySpan<byte>>();
var values2 = new Dictionary<string, byte>(new Utf8ByteStringEqualityComparer());
var lookup2 = values2.GetAlternateLookup<ReadOnlySpan<byte>>();
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
            lookup1.TryAdd(name, value);
            lookup2.TryAdd(name, value);

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
            gates.Add(gate);
            if (!gate.TryProduceOutput(values1, out _))
                gates1.Enqueue(gate);

            break;
    }
}

while (gates1.TryDequeue(out var gate))
{
    if (!gate.TryProduceOutput(values1, out _))
        gates1.Enqueue(gate);
}

ulong total1 = 0UL;
foreach (var (name, value) in values1)
{
    if (!name.StartsWith('z'))
        continue;

    var index = int.Parse(name.AsSpan(1));
    if (index >= sizeof(long) * 8)
        throw new InvalidDataException();

    total1 |= (ulong)value << index;
}

byte carry = 0;
var x = 0UL;
var y = 0UL;
var z = 0UL;
Span<byte> xName = stackalloc byte[3];
Span<byte> yName = stackalloc byte[3];
Span<byte> zName = stackalloc byte[3];
for (var i = 0; i < 64; i++)
{
    Format('x', i, xName);
    Format('y', i, yName);
    Format('z', i, zName);

    if (!lookup2.TryGetValue(xName, out var xBit) || !lookup2.TryGetValue(yName, out var yBit))
        break;

    x |= (ulong)xBit << i;
    y |= (ulong)yBit << i;

    var zBit = AddBitsWithCarry(xBit, yBit, carry, out carry);
    z |= (ulong)zBit << i;

    if (lookup1.TryGetValue(zName, out var zBad) && zBad != zBit)
        Console.WriteLine($"{Encoding.UTF8.GetString(zName)}: Expected: {zBit}, Actual: {zBad}");
}

var gatesArray = gates.Span.ToArray();
var gatesByInput1 = gatesArray.ToLookup(g => g.Input1);
var gatesByInput2 = gatesArray.ToLookup(g => g.Input2);
var gatesByOutput = gatesArray.ToDictionary(g => g.Output);
var fullAdders = new PoolableList<FullAdder>();
for (var i = 1; i < 45; i++)
{
    Format('x', i, xName);
    Format('y', i, yName);
    Format('z', i, zName);
    var xGates = gatesByInput1[Encoding.UTF8.GetString(xName)];
    var yGates = gatesByInput1[Encoding.UTF8.GetString(yName)];
    var gate1 = xGates.Union(yGates).FirstOrDefault(g => g.Operator == Operator.Xor);
    var next1Gates = gatesByInput1[gate1.Output].Union(gatesByInput2[gate1.Output]);
    var gate2 = next1Gates.FirstOrDefault(g => g.Operator == Operator.Xor);
    var gate3 = next1Gates.FirstOrDefault(g => g.Operator == Operator.And);
    var gate4 = xGates.Union(yGates).FirstOrDefault(g => g.Operator == Operator.And);
    var gate5 = gatesByInput1[gate3.Output].Union(gatesByInput1[gate4.Output]).FirstOrDefault();

    var fullAdder = new FullAdder(gate1, gate2, gate3, gate4, gate5);
    if (!fullAdder.IsValid(out var message))
        Console.WriteLine($"Invalid adder {i}:\r\n  {message}\r\n{fullAdder}\r\n");

    fullAdders.Add(fullAdder);
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static void Format(char prefix, int value, Span<byte> utf8Bytes)
{
    utf8Bytes[0] = (byte)prefix;
    if (value >= 10)
    {
        Utf8Formatter.TryFormat(value, utf8Bytes[1..], out _);
        return;
    }

    utf8Bytes[1] = (byte)'0';
    utf8Bytes[2] = (byte)('0' + value);
}

// full adder: https://medium.com/@op.oliverprice/how-do-computers-add-numbers-94589ceead6a
static byte AddBitsWithCarry(byte a, byte b, byte carryIn, out byte carryOut)
{
    var x1 = a ^ b;
    var sum = x1 ^ carryIn;
    var x3 = x1 & carryIn;
    var x4 = a & b;
    carryOut = (byte)(x3 | x4);
    return (byte)sum;
}

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

record struct FullAdder(Gate Xor1, Gate Xor2, Gate And3, Gate And4, Gate Or5)
{
    public readonly string CarryIn
        => Xor2.Input1 != Xor1.Output ? Xor2.Input1 : Xor2.Input2;

    public readonly string CarryOut => Or5.Output;

    public readonly bool IsValid(out string message)
    {
        var gate1IsValid = Xor1.Operator == Operator.Xor
            && InputStartsWith(Xor1, 'x') && InputStartsWith(Xor1, 'y');
        var gate2IsValid = Xor2.Operator == Operator.Xor && Xor2.Output.StartsWith('z')
            && HasInput(Xor2, Xor1.Output);
        var gate3IsValid = And3.Operator == Operator.And && HasInput(And3, Xor1.Output)
            && !InputStartsWith(And3, 'x') && !InputStartsWith(And3, 'y');
        var gate4IsValid = And4.Operator == Operator.And
            && InputStartsWith(And4, 'x') && InputStartsWith(And4, 'y');
        var gate5IsValid = Or5.Operator == Operator.Or
            && HasInput(Or5, And3.Output) && HasInput(Or5, And4.Output)
            && !Or5.Output.StartsWith('z');

        var messages = new List<string>();
        if (!gate1IsValid)
            messages.Add($"XOR 1 is invalid: {Xor1}");
        if (!gate2IsValid)
            messages.Add($"XOR 2 is invalid: {Xor2}");
        if (!gate3IsValid)
            messages.Add($"AND 3 is invalid: {And3}");
        if (!gate4IsValid)
            messages.Add($"AND 4 is invalid: {And4}");
        if (!gate5IsValid)
            messages.Add($"OR 5 is invalid: {Or5}");

        message = string.Join("\r\n  ", messages);

        return gate1IsValid && gate2IsValid && gate3IsValid && gate4IsValid && gate5IsValid;
    }

    private static bool HasInput(in Gate gate, string name)
        => gate.Input1 == name || gate.Input2 == name;

    private static bool InputStartsWith(in Gate gate, char c)
        => gate.Input1.StartsWith(c) || gate.Input2.StartsWith(c);

    public readonly override string ToString()
    {
        string i11 = Xor1.Input1, i12 = Xor1.Input2, ou1 = Xor1.Output;
        var op1 = Xor1.Operator;
        string i21 = Xor2.Input1, i22 = Xor2.Input2, ou2 = Xor2.Output;
        var op2 = Xor2.Operator;
        string i31 = And3.Input1, i32 = And3.Input2, ou3 = And3.Output;
        var op3 = And3.Operator;
        string i41 = And4.Input1, i42 = And4.Input2, ou4 = And4.Output;
        var op4 = And4.Operator;
        string i51 = Or5.Input1, i52 = Or5.Input2, o5 = Or5.Output;
        var op5 = Or5.Operator;
        string cin = CarryIn, cou = CarryOut;

        // note that this diagram usually lines up better in real output because the braces affect alignment
        return $"""
              A:{i11}-\_{op1}---------+--{i21}-\_{op2}--{ou2}--SUM
              B:{i12}-/   ({ou1})     |  {i22}-/
              C:{cin}---------------------+              ({ou3})
                                  |      \---{i31}-\_{op3}--{i51}-\
                                  \----------{i32}-/           |{op5}--{cou}
                                           A:{i41}-\_{op4}--{i52}-/    ({o5})
                                           B:{i42}-/     ({ou4})
            """;
    }
}

enum Operator
{
    Unknown,
    And,
    Or,
    Xor
}
