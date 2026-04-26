// blast-display-demo.csx
// UtilBlast 1.2 "Blast" display DSL — emits structured display messages
// (one self-describing JSON line per call) that a host terminal can render
// as real widgets when it understands the $blast discriminator, and falls
// back to plain JSON output in any other terminal.

using UtilBlast.Tabular;

var people = new[]
{
    new Person(1, "Alice", 95, Active: true),
    new Person(2, "Bob",   72, Active: false),
    new Person(3, "Cara",  88, Active: true),
};

// 1) Section heading.
Blast.WriteHeading("Demo report");

// 2) Status line — info / ok / warn / error.
Blast.WriteStatus($"Loaded {people.Length} rows", BlastLevel.Ok);

// 3) Table — any IEnumerable<T> can become a TabularResult, and
//    Blast.WriteTable serialises that as a $blast=table line.
Blast.WriteTable(people.ToTabular(), "People");

// 4) Key/value snapshot of a single object (POCO, anonymous, or IDictionary).
Blast.WriteKv(people[0], "First row");

// 5) For a quick human-readable text fallback you can also do:
Console.WriteLine();
Console.WriteLine(people.ToTabular().ToTextTable("People (text)"));

record Person(int Id, string Name, int Score, bool Active);
