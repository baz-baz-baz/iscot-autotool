using System;
using System.Data;

class Program
{
    static void Main()
    {
        string dbPath = @"c:\Users\Bassetto Alessio\Documents\GitHub\iscot-autotool\PersonalAutomationTool\modules\database\emails.db";
        using var _connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};");
        _connection.Open();
        using var command = new Microsoft.Data.Sqlite.SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';", _connection);
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            string table = reader.GetString(0);
            Console.WriteLine($"\nTable: {table}");
            using var c2 = new Microsoft.Data.Sqlite.SqliteCommand($"PRAGMA table_info({table});", _connection);
            using var r2 = c2.ExecuteReader();
            while (r2.Read()) {
                Console.WriteLine($"  {r2["name"]} - {r2["type"]}");
            }
        }
    }
}
