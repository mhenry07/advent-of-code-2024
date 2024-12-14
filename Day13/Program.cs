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

const int ATokens = 3;
const int BTokens = 1;

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
        TryParseButton(line, ATokens, out buttonA);
    }
    else if (line.StartsWith(buttonBXPrefix))
    {
        TryParseButton(line, BTokens, out buttonB);
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
    //Console.WriteLine($"Playing machine {i}");
    if (TryWinPrize(in machine, out var tokens))
    {
        //Console.WriteLine($"  tokens: {tokens}");
        tokens1 += tokens;
    }

    i++;
}

i = 0;
var tokens2 = 0L;
foreach (var machine in machines.Span)
{
    //Console.WriteLine($"Playing machine {i}");
    var machine2 = machine.ToPart2Machine();
    if (TryWinPrize(in machine2, out var tokens))
    {
        //Console.WriteLine($"  tokens: {tokens}");
        tokens2 += tokens;
    }

    i++;
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1: Minimum tokens: {tokens1}");
Console.WriteLine($"Part 2: Minimum tokens: {tokens2}");
Console.WriteLine($"Processed {bytes.Length:N0} input bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool TryWinPrize(in Machine machine, out long tokens)
{
    var buttonA = machine.ButtonA;
    var buttonB = machine.ButtonB;
    var prize = machine.Prize;
    var lineA = new LineSegment(prize.X, prize.Y, prize.X + buttonA.X, prize.Y + buttonA.Y);
    var lineB = new LineSegment(0, 0, buttonB.X, buttonB.Y);

    if (!TryGetIntegerIntersection(in lineA, in lineB, out var intersection))
    {
        // there's still a chance to win if button A and B are parallel
        var straightToPrize = new LineSegment(0, 0, prize.X, prize.Y);
        if (!TryGetIntegerIntersection(in straightToPrize, in lineB, out intersection))
        {
            var button = buttonB.X > 0 && buttonB.X * BTokens <= buttonA.X * ATokens
                || buttonB.Y * ATokens <= buttonB.Y * BTokens
                ? buttonB
                : buttonA;
            if (TryCountButtonPresses(in button, intersection.X, intersection.Y, out var numPresses))
            {
                tokens = numPresses * button.Tokens;
                return true;
            }
        }

        tokens = long.MaxValue;
        return false;
    }

    if (intersection.X > prize.X || intersection.Y > prize.Y)
    {
        tokens = long.MaxValue;
        return false;
    }

    var segment2 = new LineSegment(intersection.X, intersection.Y, prize.X, prize.Y);
    if (!TryCountButtonPresses(in buttonA, segment2.DX, segment2.DY, out var numAPresses)
        || !TryCountButtonPresses(in buttonB, intersection.X, intersection.Y, out var numBPresses))
    {
        tokens = long.MaxValue;
        return false;
    }

    tokens = numAPresses * ATokens + numBPresses * BTokens;
    return true;
}

bool TryParseButton(ReadOnlySpan<byte> line, int tokens, out Button button)
{
    var xIndex = line.IndexOf("X+"u8);
    var yIndex = line.LastIndexOf("Y+"u8);
    if (xIndex >= 0 && Utf8Parser.TryParse(line[(xIndex + 2)..], out int x, out _)
        && yIndex >= 0 && Utf8Parser.TryParse(line[(yIndex + 2)..], out int y, out _))
    {
        button = new(x, y, tokens);
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

static bool TryCountButtonPresses(in Button button, long distanceX, long distanceY, out long count)
{
    long remainder;
    (count, remainder) = button.X > 0
        ? Math.DivRem(distanceX, button.X)
        : Math.DivRem(distanceY, button.Y);

    return remainder == 0;
}

// based on https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection#Given_two_points_on_each_line
static bool TryGetIntegerIntersection(in LineSegment line1, in LineSegment line2, out Point intersection)
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

    var (px, pxRemainder) = Math.DivRem(pxNumerator, denominator);
    var (py, pyRemainder) = Math.DivRem(pyNumerator, denominator);

    if (pxRemainder != 0 || pyRemainder != 0)
    {
        intersection = default;
        return false;
    }

    intersection = new Point(px, py);
    return true;
}

record struct Button(int X, int Y, int Tokens);

record struct Prize(long X, long Y)
{
    public readonly Prize ToPart2Prize() => new(X + 10000000000000L, Y + 10000000000000L);
}

record struct Point(long X, long Y);

record struct LineSegment(long X1, long Y1, long X2, long Y2)
{
    public readonly long DX => X2 - X1;
    public readonly long DY => Y2 - Y1;
}

record struct Machine(Button ButtonA, Button ButtonB, Prize Prize)
{
    public readonly Machine ToPart2Machine() => new(ButtonA, ButtonB, Prize.ToPart2Prize());
}
