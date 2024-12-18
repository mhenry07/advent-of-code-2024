using System.Buffers.Text;
using System.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
var exampleBytes1 = """
    Register A: 729
    Register B: 0
    Register C: 0

    Program: 0,1,5,4,3,0
    """u8;

var exampleBytes10 = """
    Register C: 9

    Program: 2,6
    """u8;

var exampleBytes11 = """
    Register A: 10

    Program: 5,0,5,1,5,4
    """u8;

var exampleBytes12 = """
    Register A: 2024

    Program: 0,1,5,4,3,0
    """u8;

var exampleBytes13 = """
    Register B: 29

    Program: 1,7
    """u8;

var exampleBytes14 = """
    Register B: 2024
    Register C: 43690

    Program: 4,0
    """u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    10 => exampleBytes10,
    11 => exampleBytes11,
    12 => exampleBytes12,
    13 => exampleBytes13,
    14 => exampleBytes14,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

ChronospatialComputer.FromInput(bytes, out var computer);
computer.Execute();

var output1 = string.Join(',', computer.Output.ToArray());

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {output1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

ref struct ChronospatialComputer(int registerA, int registerB, int registerC, ReadOnlySpan<byte> program)
{
    readonly ReadOnlySpan<byte> ComboOpcodes = [Opcode.Adv, Opcode.Bst, Opcode.Out, Opcode.Bdv, Opcode.Cdv];
    readonly ReadOnlySpan<string> InstructionNames = new[] { "adv", "bxl", "bst", "jnz", "bxc", "out", "bdv", "cdv" };

    readonly PoolableList<byte> _output = new();
    readonly ReadOnlySpan<byte> _program = program;
    int _instructionPointer;

    int _registerA = registerA;
    int _registerB = registerB;
    int _registerC = registerC;

    public readonly ReadOnlySpan<byte> Output => _output.Span;

    public static void FromInput(ReadOnlySpan<byte> source, out ChronospatialComputer computer)
    {
        Console.WriteLine(Encoding.UTF8.GetString(source));
        Console.WriteLine();

        var section = 0;
        var registerA = 0;
        var registerB = 0;
        var registerC = 0;
        using var program = new PoolableList<byte>();
        foreach (var range in source.Split("\r\n"u8))
        {
            var line = source[range];
            if (line.IsEmpty)
            {
                section++;
                continue;
            }

            var separator = ": "u8;
            var separatorIndex = line.IndexOf(separator);
            if (separatorIndex < 0)
                throw new InvalidDataException();

            var left = line[..separatorIndex];
            var right = line[(separatorIndex + separator.Length)..];
            switch (section)
            {
                // registers
                case 0:
                    var registerPrefix = "Register "u8;
                    if (!left.StartsWith(registerPrefix))
                        throw new InvalidDataException();

                    var label = left[^1];
                    if (!Utf8Parser.TryParse(right, out int registerValue, out _))
                        throw new InvalidDataException();

                    switch (label)
                    {
                        case (byte)'A':
                            registerA = registerValue;
                            break;
                        case (byte)'B':
                            registerB = registerValue;
                            break;
                        case (byte)'C':
                            registerC = registerValue;
                            break;
                        default:
                            throw new InvalidDataException();
                    }

                    break;

                // program
                case 1:
                    var programPrefix = "Program"u8;
                    if (!left.SequenceEqual(programPrefix))
                        throw new InvalidDataException();

                    foreach (var instructionRange in right.Split((byte)','))
                    {
                        var instructionText = right[instructionRange];
                        if (!Utf8Parser.TryParse(instructionText, out int instructionPart, out _))
                            throw new InvalidDataException();

                        program.Add((byte)instructionPart);
                    }

                    break;
            }
        }

        computer = new ChronospatialComputer(registerA, registerB, registerC, program.Span.ToArray());
    }

    public void Execute()
    {
        _instructionPointer = 0;
        _output.Clear();

        Console.WriteLine($"Registers: A: {_registerA}, B: {_registerB}, C: {_registerC}, instruction ptr: {_instructionPointer}");

        while (TryReadInstruction(out var opcode, out var operand))
        {
            var formattedInstruction = FormatInstruction(opcode, operand);

            switch (opcode)
            {
                case Opcode.Adv:
                    Adv(operand);
                    break;
                case Opcode.Bxl:
                    Bxl(operand);
                    break;
                case Opcode.Bst:
                    Bst(operand);
                    break;
                case Opcode.Jnz:
                    Jnz(operand);
                    break;
                case Opcode.Bxc:
                    Bxc(operand);
                    break;
                case Opcode.Out:
                    Out(operand);
                    break;
                case Opcode.Bdv:
                    Bdv(operand);
                    break;
                case Opcode.Cdv:
                    Cdv(operand);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            Console.WriteLine($"{formattedInstruction}, Registers: A: {_registerA}, B: {_registerB}, C: {_registerC}, instruction ptr: {_instructionPointer}, output: {string.Join(',', Output.ToArray())}");
        }
    }

    bool TryReadInstruction(out byte opcode, out byte operand)
    {
        if (_instructionPointer >= _program.Length)
        {
            opcode = 0;
            operand = 0;
            return false;
        }

        opcode = _program[_instructionPointer++];
        operand = _program[_instructionPointer++];
        return true;
    }

    readonly int GetComboValue(byte operand)
    {
        return operand switch
        {
            <= 3 => operand,
            4 => _registerA,
            5 => _registerB,
            6 => _registerC,
            7 => throw new InvalidOperationException("reserved"),
            _ => throw new InvalidDataException()
        };
    }

    // division (store in A)
    void Adv(byte operand)
    {
        var numerator = _registerA;
        var denominator = Pow2(GetComboValue(operand));
        _registerA = numerator / denominator;
    }

    // bitwise XOR (B and literal operand)
    void Bxl(byte operand) => _registerB ^= operand;

    // modulo 8
    void Bst(byte operand) => _registerB = GetComboValue(operand) & 0b111;

    // jump if not zero
    void Jnz(byte operand)
    {
        if (_registerA != 0)
            _instructionPointer = operand;
    }

    // bitwise XOR (B and C)
    void Bxc(byte _) => _registerB ^= _registerC;

    // output combo modulo 8
    void Out(byte operand) => _output.Add((byte)(GetComboValue(operand) & 0b111));

    // division (store in B)
    void Bdv(byte operand)
    {
        var numerator = _registerA;
        var denominator = Pow2(GetComboValue(operand));
        _registerB = numerator / denominator;
    }

    // division (store in C)
    void Cdv(byte operand)
    {
        var numerator = _registerA;
        var denominator = Pow2(GetComboValue(operand));
        _registerC = numerator / denominator;
    }

    static int Pow2(int power) => 1 << power;

    // note: combo value must be read before executing instruction
    readonly string FormatInstruction(byte opcode, byte operand)
    {
        var instruction = InstructionNames[opcode];
        var comboLiteral = ComboOpcodes.Contains(opcode)
            ? GetComboValue(operand)
            : operand;

        return $"{instruction}: opcode: {opcode}, operand: {operand} => {comboLiteral}";
    }
}

static class Opcode
{
    public const byte Adv = 0;
    public const byte Bxl = 1;
    public const byte Bst = 2;
    public const byte Jnz = 3;
    public const byte Bxc = 4;
    public const byte Out = 5;
    public const byte Bdv = 6;
    public const byte Cdv = 7;
}
