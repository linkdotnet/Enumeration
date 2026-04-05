using LinkDotNet.Enumeration;

var all = DatabaseType.All;
Console.WriteLine("All database types:");
foreach (var db in all)
{
    Console.WriteLine($"- {db.Key}");
}   
var r = DatabaseType.Sqlite;

r.Match(
    onSqlServer: () => Console.WriteLine("SQL Server selected"),
    onSqlite: () => Console.WriteLine("SQLite selected"),
    onFile: () => Console.WriteLine("File selected")
);

[Enumeration("SqlServer", "Sqlite", "File")]
public sealed partial record DatabaseType;