using System.Buffers.Text;
using Core;

var start = TimeProvider.System.GetTimestamp();

int? useExample = null;
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
    {
        TryParseButton(line, out buttonA);
    }
    else if (line.StartsWith(buttonBXPrefix))
    {
        TryParseButton(line, out buttonB);
    }
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
    if (TryWinPrize1(in machine, out var minimumTokens))
    {
        Console.WriteLine($"  tokens: {minimumTokens}");
        tokens1 += minimumTokens;
    }

    i++;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Minimum tokens: {tokens1}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool TryWinPrize1(in Machine machine, out int tokens)
{
    const int aTokens = 3;
    const int bTokens = 1;

    var buttonA = machine.ButtonA;
    var buttonB = machine.ButtonB;
    var prize = machine.Prize;
    var lineA = new LineSegment(prize.X, prize.Y, prize.X + buttonA.X, prize.Y + buttonA.Y);
    var lineB = new LineSegment(0, 0, buttonB.X, buttonB.Y);

    if (!TryGetIntersection(in lineA, in lineB, out var intersection)
        || intersection.X > prize.X || intersection.Y > prize.Y
        || !double.IsInteger(intersection.X) || !double.IsInteger(intersection.Y))
    {
        tokens = int.MaxValue;
        return false;
    }

    var segment2 = new LineSegment((int)intersection.X, (int)intersection.Y, prize.X, prize.Y);
    var numAPresses = segment2.DX > 0
        ? segment2.DX / buttonA.X
        : segment2.DY / buttonA.Y;
    var numBPresses = intersection.X > 0
        ? intersection.X / buttonB.X
        : intersection.Y / buttonB.Y;

    if (!double.IsInteger(numAPresses) || !double.IsInteger(numBPresses))
    {
        tokens = int.MaxValue;
        return false;
    }

    tokens = (int)numAPresses * aTokens + (int)numBPresses * bTokens;
    return true;
}

// I think we need to find the intersection of the line B intersecting (0, 0) and line A intersecting the prize
// first find where line B from (0, 0) intersects with prize.Y and get line segment
// second, find where line A from prize intersects with Y = 0 and get line segment
// third, get intersection (if it exists)
// fourth, use intersection to determine B presses and A presses (if integer numbers)
static bool TryWinPrize(in Machine machine, out int tokens)
{
    var machineB = machine;
    var statusB = machineB.PressB();
    if (statusB == Status.Won)
    {
        tokens = machineB.Tokens;
        return true;
    }

    var machineA = machine;
    var statusA = machineA.PressA();
    if (statusA == Status.Won)
    {
        tokens = machineA.Tokens;
        return true;
    }

    var distanceA = machineA.GetDistance();
    var distanceB = machineB.GetDistance();
    switch (statusA, statusB)
    {
        case (Status.Lost, Status.Lost):
            tokens = int.MaxValue;
            return false;
        case (Status.Lost, _):
            return TryWinPrize(in machineB, out tokens);
        case (_, Status.Lost):
            return TryWinPrize(in machineA, out tokens);
        default:
            return distanceB <= distanceA
                ? TryWinPrize(in machineB, out tokens)
                : TryWinPrize(in machineA, out tokens);
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

// based on https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection#Given_two_points_on_each_line
static bool TryGetIntersection(in LineSegment line1, in LineSegment line2, out Point intersection)
{
    line1.Deconstruct(out var x1, out var y1, out var x2, out var y2);
    line2.Deconstruct(out var x3, out var y3, out var x4, out var y4);

    var denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
    if (denominator == 0)
    {
        intersection = default;
        return false;
    }

    var pxNumerator = (x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4);
    var pyNumerator = (x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4);

    var px = pxNumerator * 1.0 / denominator;
    var py = pyNumerator * 1.0 / denominator;

    intersection = new Point(px, py);
    return true;
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
record struct Point(double X, double Y);

record struct LineSegment(int X1, int Y1, int X2, int Y2)
{
    public readonly int DX => X2 - X1;
    public readonly int DY => Y2 - Y1;
}

struct Machine(Button buttonA, Button buttonB, Prize prize)
{
    private Claw _claw;

    public Button ButtonA { get; } = buttonA;
    public Button ButtonB { get; } = buttonB;
    public readonly Claw Claw => _claw;
    public readonly Prize Prize { get; } = prize;
    public int Tokens { get; private set; }

    public readonly double GetDistance() => GetDistance(Prize.X - Claw.X, Prize.Y - Claw.Y);
    public Status PressA() => PressButton(ButtonA, 3);
    public Status PressB() => PressButton(ButtonB, 1);

    private Status PressButton(in Button button, int tokens)
    {
        Tokens += tokens;

        _claw.Move(in button);
        if (Claw.X == Prize.X && Claw.Y == Prize.Y)
            return Status.Won;

        if (Claw.X > Prize.X || Claw.Y > Prize.Y)
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
