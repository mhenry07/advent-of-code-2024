using System.Buffers.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = 1;
var exampleBytes1 = """
Button A: X+94, Y+34
Button B: X+22, Y+67
Prize: X=8400, Y=5400

Button A: X+26, Y+66
Button B: X+67, Y+21
Prize: X=12748, Y=12176

Button A: X+17, Y+86
Button B: X+84, Y+37
Prize: X=7870, Y=6450

Button A: X+69, Y+23
Button B: X+27, Y+71
Prize: X=18641, Y=10279
"""u8;

var bytes = useExample switch
{
    1 => exampleBytes1,
    _ => File.ReadAllBytes("input.txt")
};

var buttonAXPrefix = "Button A:"u8;
var buttonBXPrefix = "Button B:"u8;
var prizePrefix = "Prize:"u8;

var buttonA = default(Button);
var buttonB = default(Button);
var machines = new PoolableList<Machine>();
foreach (var range in bytes.Split("\r\n"u8))
{
    var line = bytes[range];
    if (line.IsEmpty)
        continue;

    if (line.StartsWith(buttonAXPrefix))
        TryParseButton(line, out buttonA);
    else if (line.StartsWith(buttonBXPrefix))
        TryParseButton(line, out buttonB);
    else if (line.StartsWith(prizePrefix))
    {
        TryParsePrize(line, out var prize);
        var machine = new Machine(buttonA, buttonB, prize);
        machines.Add(machine);
    }
}

var i = 0;
var tokens1 = 0L;
foreach (var machine in machines.Span)
{
    Console.WriteLine($"Playing machine {i}");
    if (TryWinPrize(in machine, out var minimumTokens))
    {
        Console.WriteLine($"  tokens: {minimumTokens}");
        tokens1 += minimumTokens;
    }

    i++;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Minimum tokens: {tokens1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool TryWinPrize(in Machine machine, out int minimumTokens)
{
    var machineB = machine;
    var statusB = machineB.PressB();
    if (statusB == Status.Won)
    {
        minimumTokens = machineB.Tokens;
        return true;
    }

    var machineA = machine;
    var statusA = machineA.PressA();
    if (statusA == Status.Won)
    {
        minimumTokens = machineA.Tokens;
        return true;
    }

    var distanceA = machineA.GetDistance();
    var distanceB = machineB.GetDistance();
    switch (statusA, statusB)
    {
        case (Status.Lost, Status.Lost):
            minimumTokens = int.MaxValue;
            return false;
        case (Status.Lost, _):
            return TryWinPrize(in machineB, out minimumTokens);
        case (_, Status.Lost):
            return TryWinPrize(in machineA, out minimumTokens);
        default:
            return distanceB <= distanceA
                ? TryWinPrize(in machineB, out minimumTokens)
                : TryWinPrize(in machineA, out minimumTokens);
    }
}

bool TryParseButton(ReadOnlySpan<byte> line, out Button button)
{
    var xIndex = line.IndexOf("X+"u8);
    var yIndex = line.LastIndexOf("Y+"u8);
    if (xIndex >= 0 && Utf8Parser.TryParse(line[(xIndex + 2)..], out int x, out _)
        && yIndex >= 0 && Utf8Parser.TryParse(line[(yIndex + 2)..], out int y, out _))
    {
        button = new(x, y);
        return true;
    }

    button = default;
    return false;
}

bool TryParsePrize(ReadOnlySpan<byte> line, out Prize prize)
{
    var xIndex = line.IndexOf("X="u8);
    var yIndex = line.LastIndexOf("Y="u8);
    if (xIndex >= 0 && Utf8Parser.TryParse(line[(xIndex + 2)..], out int x, out _)
        && yIndex >= 0 && Utf8Parser.TryParse(line[(yIndex + 2)..], out int y, out _))
    {
        prize = new(x, y);
        return true;
    }

    prize = default;
    return false;
}

record struct Claw(int X, int Y)
{
    public void Move(in Button button)
    {
        X += button.X;
        Y += button.Y;
    }
}

record struct Button(int X, int Y);
record struct Prize(int X, int Y);
record struct Vector(int X, int Y);

struct Machine(Button buttonA, Button buttonB, Prize prize)
{
    private Claw _claw;

    public Button ButtonA { get; } = buttonA;
    public Button ButtonB { get; } = buttonB;
    public readonly Claw Claw => _claw;
    public int Tokens { get; private set; }

    public readonly double GetDistance() => GetDistance(prize.X - Claw.X, prize.Y - Claw.Y);
    public Status PressA() => PressButton(ButtonA, 3);
    public Status PressB() => PressButton(ButtonB, 1);

    private Status PressButton(in Button button, int tokens)
    {
        Tokens += tokens;

        _claw.Move(in button);
        if (Claw.X == prize.X && Claw.Y == prize.Y)
            return Status.Won;

        if (Claw.X > prize.X || Claw.Y > prize.Y)
            return Status.Lost;

        return Status.Moved;
    }

    public void Reset()
    {
        _claw = new(0, 0);
        Tokens = 0;
    }

    static double GetDistance(int x, int y) => Math.Sqrt(x * x + y * y);
}

enum Status
{
    None,
    Moved,
    Won,
    Lost
}
