using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace TestClosedXML
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            Console.WriteLine("Iniziando i test...");
            
            try
            {
                TestMatchesTrain();
                TestChiusuraTicketDialogParsing();
                TestDestinatariManager();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore: {ex}");
            }
            
            Console.WriteLine("Test completati.");
        }

        static void TestMatchesTrain()
        {
            Console.WriteLine("--- Test MatchesTrain ---");
            var assembly = typeof(PersonalAutomationTool.Modules.Excel.ExcelViewModel).Assembly;
            var type = assembly.GetType("PersonalAutomationTool.Modules.Excel.ExcelViewModel");
            var method = type?.GetMethod("MatchesTrain", BindingFlags.NonPublic | BindingFlags.Static);
            
            bool matchesTrain(string name, string trainType) => method?.Invoke(null, [name, trainType]) is bool b && b;


            AssertTrue(matchesTrain("Report Interventi ETR500 230726 04_04.xlsx", "E404P"), "E404P");
            AssertTrue(matchesTrain("Report 1001.xlsx", "ETR1000 / 1000FH"), "ETR1000");
            AssertFalse(matchesTrain("Report 1000IF.xlsx", "ETR1000 / 1000FH"), "ETR1000 excludes 1000IF");
            Console.WriteLine("Test MatchesTrain completato con successo.\n");
        }

        static void TestChiusuraTicketDialogParsing()
        {
            Console.WriteLine("--- Test ChiusuraTicketDialog Parsing ---");
            
            string testDir = Path.Combine(Path.GetTempPath(), "TestLogDump");
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);
            
            var appConfigType = typeof(PersonalAutomationTool.Core.AppConfig);
            var logAndDumpProp = appConfigType.GetProperty("LogAndDumpFolder", BindingFlags.Public | BindingFlags.Static);
            logAndDumpProp?.SetValue(null, testDir);

            string cartella = "ETR700 101 230726";
            string fullPath = Path.Combine(testDir, cartella);
            Directory.CreateDirectory(fullPath);
            Directory.CreateDirectory(Path.Combine(fullPath, "Ticket 1234567 LOG 230726 ETR700 101 04_04"));
            Directory.CreateDirectory(Path.Combine(fullPath, "Ticket 1234568 LOG 230726 ETR700 101-102 BISTANDARD 04_04"));
            
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "ETR700", false, "Log Dump");
            
            var locos = dialog.LocoGroups.Select(l => l.GroupLocoName).ToList();
            Console.WriteLine($"Trovati locos: {string.Join(", ", locos)}");
            AssertTrue(locos.Contains("101"), "Loco 101 trovato");
            AssertTrue(locos.Contains("102"), "Loco 102 trovato");
            
            Directory.Delete(testDir, true);
            Console.WriteLine("Test ChiusuraTicketDialog Parsing completato con successo.\n");
        }

        static void TestDestinatariManager()
        {
            Console.WriteLine("--- Test DestinatariManager ---");
            
            string destFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "destinatari.json");
            string backupFile = destFile + ".bak";
            if (File.Exists(destFile))
            {
                File.Copy(destFile, backupFile, true);
                File.Delete(destFile);
            }
            
            try 
            {
                var config = PersonalAutomationTool.Modules.DestinatariMail.DestinatariManager.LoadConfig();
                
                var action = PersonalAutomationTool.Modules.DestinatariMail.DestinatariManager.GetRecipients("ETR700", "Chiusura Ticket");
                AssertTrue(action != null, "Recipients per ETR700 Chiusura Ticket caricati.");
                
                if (action != null)
                {
                    action.ToRecipients = "test@test.com";
                    PersonalAutomationTool.Modules.DestinatariMail.DestinatariManager.SaveConfig(config);
                    
                    var newAction = PersonalAutomationTool.Modules.DestinatariMail.DestinatariManager.GetRecipients("ETR700", "Chiusura Ticket");
                    AssertTrue(newAction?.ToRecipients == "test@test.com", "Destinatario modificato e salvato correttamente.");
                }
            }
            finally
            {
                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, destFile, true);
                    File.Delete(backupFile);
                }
            }
            
            Console.WriteLine("Test DestinatariManager completato con successo.\n");
        }

        static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception($"Assertion failed: {message}");
            Console.WriteLine($"[PASS] {message}");
        }

        static void AssertFalse(bool condition, string message)
        {
            if (condition) throw new Exception($"Assertion failed: {message}");
            Console.WriteLine($"[PASS] {message}");
        }
    }
}
