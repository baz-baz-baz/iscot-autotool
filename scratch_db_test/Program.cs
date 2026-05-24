using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        string dbPath = @"c:\Users\peli\Documents\GitHub\iscot-autotool\PersonalAutomationTool\modules\database\train_software.db";
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT tipo FROM flotte";
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine($"tipo: '{reader.GetString(0)}'");
        }
    }
}
