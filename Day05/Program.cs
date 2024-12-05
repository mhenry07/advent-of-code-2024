﻿using System.Buffers.Text;
using System.Text;

var start = TimeProvider.System.GetTimestamp();

bool useExample = false;
var exampleBytes = """
47|53
97|13
97|61
97|47
75|29
61|13
75|53
29|13
97|29
53|29
61|53
97|53
61|29
47|13
75|47
97|75
47|61
75|61
47|29
75|13
53|13

75,47,61,53,29
97,61,53,29,13
75,29,13
75,97,47,61,53
61,13,29
97,13,75,29,47
"""u8;

var bytes = useExample switch
{
    true => exampleBytes,
    _ => File.ReadAllBytes("input.txt").AsSpan()
};

const int PageOrderingRulesSection = 1;
const int PageUpdatesSection = 2;

var rules = new HashSet<Rule>();
var section = 1;
var total1 = 0;
foreach (var lineRange in bytes.Split("\r\n"u8))
{
    var line = bytes[lineRange];
    if (line.IsEmpty)
    {
        section++;
        continue;
    }

    if (section == PageOrderingRulesSection)
    {
        if (!TryParseRule(line, out var rule))
            throw new InvalidOperationException($"Failed to parse rule from line: {Encoding.UTF8.GetString(line)}");

        rules.Add(rule);
    }
    else if (section == PageUpdatesSection)
    {
        if (IsCorrectlyOrdered(rules, line, out var middlePageNumber))
            total1 += middlePageNumber;
    }
}

var elapsed = TimeProvider.System.GetElapsedTime(start);

Console.WriteLine($"Part 1 answer: {total1}");
Console.WriteLine($"Processed {bytes.Length:N0} bytes in: {elapsed.TotalMilliseconds:N3} ms");

static bool TryParseRule(ReadOnlySpan<byte> line, out Rule rule)
{
    if (Utf8Parser.TryParse(line, out byte x, out int xLength)
        && line.Length > xLength && line[xLength] == (byte)'|'
        && Utf8Parser.TryParse(line[(xLength + 1)..], out byte y, out int _))
    {
        rule = new Rule(x, y);
        return true;
    }

    rule = default;
    return false;
}

static bool IsCorrectlyOrdered(HashSet<Rule> rules, ReadOnlySpan<byte> line, out byte middlePageNumber)
{
    var count = (line.Length + 1) / 3; // assumes 2-digit page numbers separated by commas
    var i = 0;
    Span<byte> pageNumbers = stackalloc byte[count];
    foreach (var numberRange in line.Split((byte)','))
    {
        if (!Utf8Parser.TryParse(line[numberRange], out byte pageNumber, out _))
            throw new InvalidOperationException($"Failed to parse page number from {Encoding.UTF8.GetString(line[numberRange])}");

        pageNumbers[i++] = pageNumber;
    }

    middlePageNumber = 0;
    for (i = 0; i < count - 1; i++)
    {
        for (var j = i; j < count; j++)
        {
            var check = new Rule(pageNumbers[j], pageNumbers[i]);
            if (rules.Contains(check))
                return false;
        }
    }

    middlePageNumber = pageNumbers[count / 2];
    return true;
}

record struct Rule(int X, int Y);
