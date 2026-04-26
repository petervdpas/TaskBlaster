// sqlite-demo.csx
// Stage some data in SQLite using SqliteBlast — a typical "scratch DB"
// pattern for scripts that need to keep state between steps.

using SqliteBlast;

// 1) In-memory store. Note: Roslyn .csx doesn't support `using var x = ...`
//    declarations, so we Dispose at the end instead.
var db = SqliteBlastFactory.InMemory();

db.Execute("""
    CREATE TABLE notes (
        id    INTEGER PRIMARY KEY AUTOINCREMENT,
        body  TEXT    NOT NULL,
        seen  INTEGER NOT NULL DEFAULT 0
    );
    """);

// 2) Parameter binding from a POCO — properties map to @Body / @Seen.
db.Execute("INSERT INTO notes(body, seen) VALUES (@Body, @Seen)", new { Body = "first note", Seen = false });
db.Execute("INSERT INTO notes(body, seen) VALUES (@Body, @Seen)", new { Body = "second note", Seen = true  });

// 3) Transaction with safe-by-default rollback. Dispose without Commit() reverts.
using (var tx = db.BeginTransaction())
{
    db.Execute("UPDATE notes SET seen = 1 WHERE id = @Id", new { Id = 1 });
    tx.Commit();
}

// 4) Typed Query<T> — column names match public settable properties (case-insensitive).
foreach (var note in db.Query<Note>("SELECT id, body, seen FROM notes ORDER BY id"))
    Console.WriteLine($"#{note.Id}  {note.Body}  (seen={note.Seen})");

// 5) Scalar
Console.WriteLine($"Total notes: {db.ExecuteScalar<long>("SELECT COUNT(*) FROM notes")}");

// 6) Vault-backed path — uncomment after adding a vault entry under
//    category "scratch-db" with key "path" pointing at a real .db file.
// var persistent = new SqliteStore();
// await persistent.SetupAsync(Secrets.Resolver, "scratch-db");
// persistent.Open();
// // ... use it ...
// persistent.Dispose();

db.Dispose();

record Note { public long Id { get; set; } public string Body { get; set; } = ""; public bool Seen { get; set; } }
