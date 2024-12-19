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

var exampleBytes2 = """
    Register A: 2024
    Register B: 0
    Register C: 0

    Program: 0,3,5,4,3,0
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
    2 => exampleBytes2,
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
if (!TrySolvePart2(ref computer, out var registerA2, out var attempts))
    Console.WriteLine($"Failed to find part 2 solution after {attempts:N0} attempts");

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: {output1}");
Console.WriteLine($"Part 2: {registerA2}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

// target: 16 outputs
// capture the right 3 bits (a & 0b111) for each matched output,
// then try those combinations + the last 7 digits at the very end
static bool TrySolvePart2(ref ChronospatialComputer computer, out long registerA, out long attempt)
{
    const int ChunkSize = 4;

    var part2Start = TimeProvider.System.GetTimestamp();
    attempt = 0;
    Span<HashSet<long>> candidatePartSets = new HashSet<long>[computer.Program.Length / ChunkSize];
    for (var i = 0; i < computer.Program.Length; i += ChunkSize)
    {
        var expectedValues = computer.Program.Slice(i, ChunkSize);
        var candidates = candidatePartSets[i / ChunkSize] = [];
        var bits = 7 + 3 * ChunkSize + 1;
        var mask = 0;
        for (var m = 0; m < ChunkSize; m++)
            mask |= 0b111 << (3 * m);

        for (var a = 0L; a < 1L << bits; a++)
        {
            if (computer.TryExecutePartialCheck(a, expectedValues))
            {
                var maskedA = a & mask;
                candidates.Add(maskedA);
            }
        }

        Console.WriteLine($"Found {candidates.Count} candidates for {string.Join(',', expectedValues.ToArray())} (i: {i}..{i + ChunkSize})");
    }

    Span<long[]> candidateParts = new long[candidatePartSets.Length][];
    for (var i = 0; i < candidatePartSets.Length; i++)
    {
        var candidates = candidatePartSets[i].ToArray();
        candidates.AsSpan().Sort();
        candidateParts[i] = candidates;
    }

    // order of loops is important to find the smallest solution first:
    // low order bits in inner loops, high order bits in outer loops
    // these loops are hardcoded for ChunkSize: 4
    attempt = 0L;
    for (var c5 = 0L; c5 < 1L << (7 + 1); c5++)
    {
        var a5 = c5 << (4 * 3 * ChunkSize);
        foreach (var c4 in candidatePartSets[3])
        {
            var a4 = c4 << (3 * 3 * ChunkSize);
            foreach (var c3 in candidatePartSets[2])
            {
                var a3 = c3 << (2 * 3 * ChunkSize);
                foreach (var c2 in candidatePartSets[1])
                {
                    var a2 = c2 << (1 * 3 * ChunkSize);
                    foreach (var a1 in candidatePartSets[0])
                    {
                        var a = a5 | a4 | a3 | a2 | a1;
                        if (computer.TryExecuteFullCheck(a, out _))
                        {
                            //var part2Elapsed = TimeProvider.System.GetElapsedTime(part2Start);
                            //Console.WriteLine($"Part 2: {a} (0x{a:X} / 0b{a:B}) in {part2Elapsed.TotalMilliseconds:N3} ms and {attempt} attempts");
                            //Console.WriteLine($"C1: {a1}, C2: {c2}, C3: {c3}, C4: {c4}, C5: {c5}");
                            //Console.ReadLine();
                            registerA = a;
                            return true;
                        }

                        if (attempt % 1_000_000 == 0)
                            Console.WriteLine($"Attempt: {attempt:N0}");

                        attempt++;
                    }
                }
            }
        }
    }

    registerA = 0;
    return false;
}

ref struct ChronospatialComputer(long registerA, long registerB, long registerC, ReadOnlySpan<byte> program)
{
    readonly ReadOnlySpan<byte> ComboOpcodes = [Opcode.Adv, Opcode.Bst, Opcode.Out, Opcode.Bdv, Opcode.Cdv];
    readonly ReadOnlySpan<string> InstructionNames = new[] { "adv", "bxl", "bst", "jnz", "bxc", "out", "bdv", "cdv" };

    readonly long _initialB = registerB;
    readonly long _initialC = registerC;
    readonly PoolableList<byte> _output = new();
    readonly ReadOnlySpan<byte> _program = program;
    int _instructionPointer;

    long _registerA = registerA;
    long _registerB = registerB;
    long _registerC = registerC;

    public readonly ReadOnlySpan<byte> Output => _output.Span;
    public readonly ReadOnlySpan<byte> Program => _program;

    public static void FromInput(ReadOnlySpan<byte> source, out ChronospatialComputer computer)
    {
        Console.WriteLine(Encoding.UTF8.GetString(source));
        Console.WriteLine();

        var section = 0;
        var registerA = 0L;
        var registerB = 0L;
        var registerC = 0L;
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
                    if (!Utf8Parser.TryParse(right, out long registerValue, out _))
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

    public void ResetWithRegisterA(long registerA)
    {
        _instructionPointer = 0;
        _output.Reset();
        _registerA = registerA;
        _registerB = _initialB;
        _registerC = _initialC;
    }

    public void Execute()
    {
        Console.WriteLine($"Registers: A: {_registerA}, B: {_registerB}, C: {_registerC}, instruction ptr: {_instructionPointer}");

        while (TryReadInstruction(out var opcode, out var operand))
        {
            var formattedInstruction = FormatInstruction(opcode, operand);
            ExecuteInstruction(opcode, operand);

            Console.WriteLine($"{formattedInstruction}, Registers: A: {_registerA}, B: {_registerB}, C: {_registerC}, instruction ptr: {_instructionPointer}, output: {string.Join(',', Output.ToArray())}");
        }
    }

    // input program
    // bst: A & 0b111 => B
    // bxl: B XOR 2 => B
    // cdv: A >> B => C
    // bxl: B XOR 3 => B
    // bxc: B XOR C => B
    // out: B & 0b111 => output[0] # expected: 2
    // adv: A >> 3 => A' # loop while A / 8 > 0
    // jnz: A' != 0 ?=> ptr = 0
    // ... loop
    // bst: A' & 0b111 => B
    // bxl: B XOR 2 => B
    // cdv: A' >> B => C
    // bxl: B XOR 3 => B
    // bxc: B XOR C => B
    // out: B & 0b111 => output[0] # expected: 4
    // adv: A' >> 3 => A''
    // jnz: A'' != 0 ?=> ptr = 0
    // ...
    // # loops needed: 16 => A > 2 ^ (16 * 3) = 281,474,976,710,656
    // 1,818,934,772
    public bool TryExecuteFullCheck(long registerA, out int matchingLength, bool verbose = false)
    {
        ResetWithRegisterA(registerA);

        //Console.WriteLine($"Registers: A: {_registerA}, B: {_registerB}, C: {_registerC}, instruction ptr: {_instructionPointer}");

        while (TryReadInstruction(out var opcode, out var operand))
        {
            var formattedInstruction = verbose ? FormatInstruction(opcode, operand) : string.Empty;

            ExecuteInstruction(opcode, operand);

            if (verbose)
                Console.WriteLine($"{formattedInstruction}, Registers: A: {_registerA}, B: {_registerB}, C: {_registerC}, instruction ptr: {_instructionPointer}, output: {string.Join(',', Output.ToArray())}");

            if (_output.Length > _program.Length || !_program.StartsWith(_output.Span))
            {
                if (verbose) Console.WriteLine();

                matchingLength = _program.CommonPrefixLength(_output.Span);
                return false;
            }
        }

        if (verbose) Console.WriteLine();

        matchingLength = _program.CommonPrefixLength(_output.Span);
        return _output.Span.SequenceEqual(_program);
    }

    public bool TryExecutePartialCheck(long registerA, ReadOnlySpan<byte> expected)
    {
        ResetWithRegisterA(registerA);

        //Console.WriteLine($"Registers: A: {_registerA}, B: {_registerB}, C: {_registerC}, instruction ptr: {_instructionPointer}");

        while (TryReadInstruction(out var opcode, out var operand))
            ExecuteInstruction(opcode, operand);

        return _output.Span.StartsWith(expected);
    }

    void ExecuteInstruction(byte opcode, byte operand)
    {
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

    readonly long GetComboValue(byte operand)
    {
        return operand switch
        {
            <= 3 => operand,
            4 => _registerA,
            5 => _registerB,
            6 => _registerC,
            7 => throw new InvalidOperationException("reserved"),
            _ => throw new ArgumentOutOfRangeException(nameof(operand))
        };
    }

    // division (store in A)
    void Adv(byte operand)
    {
        var numerator = _registerA;
        var exponent = GetComboValue(operand);
        _registerA = DivideByPow2(numerator, exponent);
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
        var exponent = GetComboValue(operand);
        _registerB = DivideByPow2(numerator, exponent);
    }

    // division (store in C)
    void Cdv(byte operand)
    {
        var numerator = _registerA;
        var exponent = GetComboValue(operand);
        _registerC = DivideByPow2(numerator, exponent);
    }

    static long DivideByPow2(long numerator, long exponent) => numerator >> (int)exponent;

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
