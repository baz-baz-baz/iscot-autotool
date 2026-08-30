using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Categoria dell'operazione che ha generato un batch di <c>renamer_log</c>. Determina anche se
    /// <see cref="RenamerLog.UndoLastBatch"/> deve invertire l'operazione con <c>File.Move</c>
    /// (<see cref="PdfRename"/>, che rinomina file) o <c>Directory.Move</c> (le due variabili di HOME,
    /// che rinominano le sottocartelle LOG/DUMP).
    /// </summary>
    public enum RenameBatchKind
    {
        PdfRename,
        HomeTicket,
        HomeData
    }

    public sealed record RenameLogOperation(long Id, string OldPath, string NewPath);

    public sealed record RenameLogBatch(string Ts, RenameBatchKind Kind, IReadOnlyList<RenameLogOperation> Operations);

    public sealed record RenameUndoResult(bool BatchFound, int Restored, IReadOnlyList<string> Errors);

    /// <summary>
    /// Storico inverso delle rinomine (intervento 4.3, Sprint 3), su <c>renamer_log</c> di
    /// <c>train_software.db</c> — tabella preesistente nel database ma mai scritta da alcun modulo
    /// prima di questa sessione (residuo di una funzione di rinomina automatica mai completata, vedi
    /// §6.6 di PROJECT_MEMORY.md). Lo schema reale (verificato sul <c>.db</c> spedito con l'app) è
    /// <c>id, ts, file_sig, old_path, new_path, template, result</c> — nessuna colonna "batch": tutte
    /// le righe scritte da una singola chiamata a <see cref="RecordBatch"/> condividono lo stesso
    /// <c>ts</c> (timestamp al decimo di microsecondo, generato una sola volta per batch), che funge
    /// da chiave di raggruppamento. <c>template</c> porta invece il tipo di operazione
    /// (<see cref="RenameBatchKind"/>), per poter filtrare l'ultimo batch di un tipo specifico: PDF
    /// deve poter annullare solo l'ultima rinomina PDF, non un'eventuale rinomina più recente fatta
    /// da HOME, e viceversa.
    /// <para>
    /// Ogni metodo accetta un <c>dbPath</c> opzionale (default <c>null</c> → percorso reale
    /// dell'applicazione): permette ai test di puntare a un file SQLite temporaneo invece del
    /// database di produzione, stesso principio dei test Tier 2 già in uso per
    /// <c>PdfRenamePlanner</c> e <c>DatabaseManager</c>. I chiamanti applicativi (PdfView,
    /// HomeViewModel) non lo passano mai: usano sempre il percorso reale.
    /// </para>
    /// </summary>
    public static class RenamerLog
    {
        private static string DefaultDbPath => AppPaths.DatabaseFile("train_software.db");

        /// <summary>Registra un batch. Le operazioni "no-op" (percorso invariato) vanno filtrate dal chiamante prima di invocare questo metodo.</summary>
        public static void RecordBatch(RenameBatchKind kind, IReadOnlyList<(string OldPath, string NewPath)> operations, string? dbPath = null)
        {
            string path = dbPath ?? DefaultDbPath;
            if (operations.Count == 0 || !File.Exists(path)) return;

            string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
            try
            {
                using var db = new PersonalAutomationTool.Modules.Database.DatabaseManager(path);
                foreach (var (oldPath, newPath) in operations)
                {
                    db.ExecuteNonQuery(
                        "INSERT INTO renamer_log (ts, file_sig, old_path, new_path, template, result) VALUES (@ts, NULL, @old, @new, @template, 'ok')",
                        new Dictionary<string, object?> { ["@ts"] = ts, ["@old"] = oldPath, ["@new"] = newPath, ["@template"] = kind.ToString() });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore scrittura renamer_log: {ex.Message}");
            }
        }

        /// <summary>L'ultimo batch fra le categorie indicate (per <c>ts</c> decrescente), o <c>null</c> se non ce n'è nessuno.</summary>
        public static RenameLogBatch? GetLastBatch(RenameBatchKind[] kinds, string? dbPath = null)
        {
            string path = dbPath ?? DefaultDbPath;
            if (kinds.Length == 0 || !File.Exists(path)) return null;

            try
            {
                using var db = new PersonalAutomationTool.Modules.Database.DatabaseManager(path);

                var kindParams = new Dictionary<string, object?>();
                var placeholders = new List<string>();
                for (int i = 0; i < kinds.Length; i++)
                {
                    string name = $"@k{i}";
                    placeholders.Add(name);
                    kindParams[name] = kinds[i].ToString();
                }

                var lastTs = db.Query(
                    $"SELECT ts FROM renamer_log WHERE template IN ({string.Join(",", placeholders)}) ORDER BY ts DESC LIMIT 1",
                    static r => r.GetString(0),
                    kindParams);

                if (lastTs.Count == 0) return null;
                string ts = lastTs[0];

                var rows = db.Query(
                    "SELECT id, old_path, new_path, template FROM renamer_log WHERE ts = @ts ORDER BY id",
                    static r => (Id: r.GetInt64(0), Old: r.GetString(1), New: r.GetString(2), Kind: r.GetString(3)),
                    new Dictionary<string, object?> { ["@ts"] = ts });

                if (rows.Count == 0 || !Enum.TryParse<RenameBatchKind>(rows[0].Kind, out var kind)) return null;

                var ops = rows.Select(r => new RenameLogOperation(r.Id, r.Old, r.New)).ToList();
                return new RenameLogBatch(ts, kind, ops);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore lettura renamer_log: {ex.Message}");
                return null;
            }
        }

        /// <summary>Overload comoda per il caso comune di un'unica categoria (o poche, elencate direttamente) — vedi <see cref="GetLastBatch(RenameBatchKind[], string?)"/>.</summary>
        public static RenameLogBatch? GetLastBatch(params RenameBatchKind[] kinds) => GetLastBatch(kinds, dbPath: null);

        public static void DeleteBatch(string ts, string? dbPath = null)
        {
            string path = dbPath ?? DefaultDbPath;
            if (!File.Exists(path)) return;
            try
            {
                using var db = new PersonalAutomationTool.Modules.Database.DatabaseManager(path);
                db.ExecuteNonQuery("DELETE FROM renamer_log WHERE ts = @ts", new Dictionary<string, object?> { ["@ts"] = ts });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore pulizia renamer_log: {ex.Message}");
            }
        }

        /// <summary>
        /// Annulla l'ultimo batch fra <paramref name="kinds"/>: sposta ogni elemento da
        /// <c>new_path</c> a <c>old_path</c> (inverso dell'operazione originale). Il batch viene
        /// ripulito dal log **solo** se tutte le operazioni sono state ripristinate senza errori — se
        /// anche una sola fallisce (percorso spostato o modificato nel frattempo da fuori
        /// dall'applicazione), il log resta intatto e l'errore è riportato al chiamante, così un
        /// nuovo tentativo dopo una correzione manuale può ripartire dallo stesso batch.
        /// </summary>
        public static RenameUndoResult UndoLastBatch(RenameBatchKind[] kinds, string? dbPath = null)
        {
            var batch = GetLastBatch(kinds, dbPath);
            if (batch == null) return new RenameUndoResult(false, 0, []);

            bool isDirectory = batch.Kind != RenameBatchKind.PdfRename;
            int restored = 0;
            var errors = new List<string>();

            foreach (var op in batch.Operations)
            {
                try
                {
                    bool newExists = isDirectory ? Directory.Exists(op.NewPath) : File.Exists(op.NewPath);
                    bool oldExists = isDirectory ? Directory.Exists(op.OldPath) : File.Exists(op.OldPath);

                    if (!newExists)
                    {
                        errors.Add($"{Path.GetFileName(op.NewPath)}: non trovato, forse già spostato o rinominato altrove.");
                        continue;
                    }
                    if (oldExists)
                    {
                        errors.Add($"{Path.GetFileName(op.OldPath)}: esiste già un elemento con il nome originale.");
                        continue;
                    }

                    if (isDirectory) Directory.Move(op.NewPath, op.OldPath);
                    else File.Move(op.NewPath, op.OldPath);

                    restored++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(op.NewPath)}: {ex.Message}");
                }
            }

            if (errors.Count == 0)
            {
                DeleteBatch(batch.Ts, dbPath);
            }

            return new RenameUndoResult(true, restored, errors);
        }

        /// <summary>Overload comoda — vedi <see cref="UndoLastBatch(RenameBatchKind[], string?)"/>.</summary>
        public static RenameUndoResult UndoLastBatch(params RenameBatchKind[] kinds) => UndoLastBatch(kinds, dbPath: null);
    }
}
