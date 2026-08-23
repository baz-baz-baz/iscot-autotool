using System;
using System.Collections.Generic;
using System.IO;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.Database;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2: un vero file SQLite temporaneo per test (<see cref="Directory.CreateTempSubdirectory"/>,
    /// ripulito a fine test) con lo schema reale di <c>renamer_log</c> verificato sul <c>.db</c>
    /// spedito con l'app (<c>id, ts, file_sig, old_path, new_path, template, result</c>) — non il
    /// database di produzione, tramite l'overload <c>dbPath</c> di <see cref="RenamerLog"/>.
    /// Le operazioni di annullamento (<see cref="RenamerLog.UndoLastBatch(RenameBatchKind[], string?)"/>)
    /// muovono file/cartelle reali dentro <see cref="_root"/>, esattamente come farebbe in produzione.
    /// </summary>
    public sealed class RenamerLogTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("RenamerLogTests_");
        private readonly string _dbPath;

        public RenamerLogTests()
        {
            _dbPath = Path.Combine(_root.FullName, "test.db");
            using var db = new DatabaseManager(_dbPath);
            db.ExecuteNonQuery(@"CREATE TABLE renamer_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ts TEXT NOT NULL,
                file_sig TEXT,
                old_path TEXT NOT NULL,
                new_path TEXT NOT NULL,
                template TEXT NOT NULL,
                result TEXT NOT NULL)");
        }

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private string CreateFile(string name)
        {
            string path = Path.Combine(_root.FullName, name);
            File.WriteAllBytes(path, []);
            return path;
        }

        private string CreateDirectory(string name)
        {
            string path = Path.Combine(_root.FullName, name);
            Directory.CreateDirectory(path);
            return path;
        }

        [Fact]
        public void RecordBatch_PoiGetLastBatch_RestituisceLeStesseOperazioni()
        {
            var oldPath = CreateFile("FL SR1 ETR700 117 IMC AV Milano 010126 Rossi.pdf");
            string newPath = Path.Combine(_root.FullName, "FL SR2 ETR700 117 IMC AV Milano 010126 Rossi.pdf");
            var ops = new List<(string OldPath, string NewPath)> { (oldPath, newPath) };

            RenamerLog.RecordBatch(RenameBatchKind.PdfRename, ops, _dbPath);

            var batch = RenamerLog.GetLastBatch([RenameBatchKind.PdfRename], _dbPath);

            Assert.NotNull(batch);
            Assert.Equal(RenameBatchKind.PdfRename, batch!.Kind);
            var op = Assert.Single(batch.Operations);
            Assert.Equal(oldPath, op.OldPath);
            Assert.Equal(newPath, op.NewPath);
        }

        [Fact]
        public void GetLastBatch_FiltraPerCategoria_IgnoraBatchDiAltraCategoria()
        {
            RenamerLog.RecordBatch(RenameBatchKind.HomeTicket, [("a", "b")], _dbPath);

            var batch = RenamerLog.GetLastBatch([RenameBatchKind.PdfRename], _dbPath);

            Assert.Null(batch);
        }

        [Fact]
        public void GetLastBatch_ConPiuCategorie_RestituisceIlPiuRecenteFraQuelleRichieste()
        {
            RenamerLog.RecordBatch(RenameBatchKind.HomeTicket, [("a", "b")], _dbPath);
            System.Threading.Thread.Sleep(5); // garantisce un `ts` strettamente successivo
            RenamerLog.RecordBatch(RenameBatchKind.HomeData, [("c", "d")], _dbPath);

            var batch = RenamerLog.GetLastBatch([RenameBatchKind.HomeTicket, RenameBatchKind.HomeData], _dbPath);

            Assert.NotNull(batch);
            Assert.Equal(RenameBatchKind.HomeData, batch!.Kind);
        }

        [Fact]
        public void UndoLastBatch_File_RipristinaIlNomeOriginaleERipulisceIlLog()
        {
            string oldPath = CreateFile("originale.pdf");
            string newPath = Path.Combine(_root.FullName, "rinominato.pdf");
            File.Move(oldPath, newPath);
            RenamerLog.RecordBatch(RenameBatchKind.PdfRename, [(oldPath, newPath)], _dbPath);

            var result = RenamerLog.UndoLastBatch([RenameBatchKind.PdfRename], _dbPath);

            Assert.True(result.BatchFound);
            Assert.Equal(1, result.Restored);
            Assert.Empty(result.Errors);
            Assert.True(File.Exists(oldPath));
            Assert.False(File.Exists(newPath));
            Assert.Null(RenamerLog.GetLastBatch([RenameBatchKind.PdfRename], _dbPath));
        }

        [Fact]
        public void UndoLastBatch_Cartella_UsaDirectoryMoveNonFileMove()
        {
            string oldPath = CreateDirectory("SR1 LOG ETR700 117 04.02HR 010126 Rossi");
            string newPath = Path.Combine(_root.FullName, "SR2 LOG ETR700 117 04.02HR 010126 Rossi");
            Directory.Move(oldPath, newPath);
            RenamerLog.RecordBatch(RenameBatchKind.HomeTicket, [(oldPath, newPath)], _dbPath);

            var result = RenamerLog.UndoLastBatch([RenameBatchKind.HomeTicket], _dbPath);

            Assert.Equal(1, result.Restored);
            Assert.True(Directory.Exists(oldPath));
            Assert.False(Directory.Exists(newPath));
        }

        [Fact]
        public void UndoLastBatch_DestinazioneMancante_NonRipulisceIlLogEDescriveLErrore()
        {
            // Il file "rinominato" non esiste più su disco (spostato o cancellato fuori dall'app):
            // l'annulla deve segnalarlo, non lanciare, e lasciare il batch nel log per un nuovo tentativo.
            string oldPath = Path.Combine(_root.FullName, "originale.pdf");
            string newPath = Path.Combine(_root.FullName, "rinominato.pdf"); // mai creato
            RenamerLog.RecordBatch(RenameBatchKind.PdfRename, [(oldPath, newPath)], _dbPath);

            var result = RenamerLog.UndoLastBatch([RenameBatchKind.PdfRename], _dbPath);

            Assert.True(result.BatchFound);
            Assert.Equal(0, result.Restored);
            Assert.Single(result.Errors);
            // Il batch non deve essere stato ripulito: un nuovo tentativo deve poterlo ritrovare.
            Assert.NotNull(RenamerLog.GetLastBatch([RenameBatchKind.PdfRename], _dbPath));
        }

        [Fact]
        public void UndoLastBatch_NessunBatch_NonTrovatoSenzaErrori()
        {
            var result = RenamerLog.UndoLastBatch([RenameBatchKind.PdfRename], _dbPath);

            Assert.False(result.BatchFound);
            Assert.Equal(0, result.Restored);
            Assert.Empty(result.Errors);
        }
    }
}
