using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Modules.Pdf;
using PersonalAutomationTool.Modules.Pdf.Models;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Pdf
{
    /// <summary>
    /// Tier 2: alberi di cartelle reali su disco (un <see cref="Directory.CreateTempSubdirectory"/>
    /// per test, ripulito a fine test), esattamente lo scenario che <c>PdfView.BtnRinomina_Click</c>
    /// affronta in produzione — a differenza di <c>LogDumpFolderNameTests</c> (Tier 1, funzioni pure
    /// su stringhe), qui il conflitto con un file di destinazione già esistente (una delle uscite
    /// del planner) viene verificato con un vero <c>File.Exists</c> su disco, non simulato.
    /// I file PDF sono file vuoti con estensione .pdf: <see cref="PdfRenamePlanner.CreatePlan"/> non
    /// apre mai i PDF, il conteggio pagine è iniettato (vedi <see cref="FakePageCounts"/>).
    /// </summary>
    public sealed class PdfRenamePlannerTests : IDisposable
    {
        private static readonly string[] RealKnownTypes = ["ETR1000 I-F", "ETR1001FH", "ETR1000", "E404P", "ETR700"];

        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("PdfRenamePlannerTests_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private string CreateCardFolder(string name)
        {
            string path = Path.Combine(_root.FullName, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static void CreateLogSubfolder(string cardPath, string folderName) =>
            Directory.CreateDirectory(Path.Combine(cardPath, folderName));

        private static string CreateEmptyFile(string cardPath, string fileName)
        {
            string path = Path.Combine(cardPath, fileName);
            File.WriteAllBytes(path, []);
            return path;
        }

        /// <summary>Scansiona una cartella madre reale in un <see cref="TrainCardModel"/>, come fa <c>PdfView.LoadFolders</c> in produzione.</summary>
        private static TrainCardModel ScanCard(string cardPath, bool isNd = false)
        {
            var card = new TrainCardModel { Title = Path.GetFileName(cardPath), FullPath = cardPath, IsND = isNd };

            foreach (var sub in Directory.GetDirectories(cardPath))
            {
                card.Children.Add(new FolderItemModel { Name = Path.GetFileName(sub), FullPath = sub, IsDirectory = true });
            }
            foreach (var file in Directory.GetFiles(cardPath))
            {
                card.Children.Add(new FolderItemModel
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false,
                    Extension = Path.GetExtension(file).ToLower()
                });
            }

            return card;
        }

        private static Func<string, int> FakePageCounts(Dictionary<string, int> pages) => path =>
            pages.TryGetValue(path, out var count) ? count : 1;

        [Fact]
        public void CreatePlan_SinglePdf_ProducesFlName()
        {
            string cardPath = CreateCardFolder("ETR700 12");
            CreateLogSubfolder(cardPath, "SR1247654 LOG ETR700 117 04.02HR 300526 Todde");
            string pdf = CreateEmptyFile(cardPath, "input.pdf");

            var card = ScanCard(cardPath);
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Ready, plan.Outcome);
            var op = Assert.Single(plan.MoveOperations);
            Assert.Equal(pdf, op.OldPath);
            Assert.Equal(Path.Combine(cardPath, "FL SR1247654 ETR700 117 IMC AV Milano 300526 Todde.pdf"), op.NewPath);
        }

        [Fact]
        public void CreatePlan_SinglePdf_NdPrefix_WhenCardIsNd()
        {
            string cardPath = CreateCardFolder("ETR700 12 ND");
            CreateLogSubfolder(cardPath, "SR1247654 LOG ETR700 117 04.02HR 300526 Todde");
            CreateEmptyFile(cardPath, "input.pdf");

            var card = ScanCard(cardPath, isNd: true);
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Ready, plan.Outcome);
            var op = Assert.Single(plan.MoveOperations);
            Assert.StartsWith("ND FL ", Path.GetFileName(op.NewPath));
        }

        [Fact]
        public void CreatePlan_TwoUncheckedPdfs_SmallerBecomesNdL_LargerBecomesFl()
        {
            string cardPath = CreateCardFolder("ETR1000 I-F 30");
            CreateLogSubfolder(cardPath, "SR2233445 LOG ETR1000 I-F 302 02.02.0004_ELO_BL3 220626 Gialli");
            string bigPdf = CreateEmptyFile(cardPath, "big.pdf");
            string smallPdf = CreateEmptyFile(cardPath, "small.pdf");

            var card = ScanCard(cardPath);
            var pageCounts = FakePageCounts(new Dictionary<string, int> { [bigPdf] = 12, [smallPdf] = 2 });
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, pageCounts);

            Assert.Equal(PdfRenameOutcome.Ready, plan.Outcome);
            Assert.Equal(2, plan.MoveOperations.Count);

            var bigOp = plan.MoveOperations.Single(o => o.OldPath == bigPdf);
            var smallOp = plan.MoveOperations.Single(o => o.OldPath == smallPdf);

            Assert.StartsWith("FL ", Path.GetFileName(bigOp.NewPath));
            Assert.StartsWith("NdL ", Path.GetFileName(smallOp.NewPath));
        }

        [Fact]
        public void CreatePlan_TwoUncheckedPdfsWithTxtFile_SmallerNamedFromChecklist()
        {
            string cardPath = CreateCardFolder("ETR700 40");
            CreateLogSubfolder(cardPath, "SR9988776 LOG ETR700 210 04.02HR 010726 Bianchi");
            string bigPdf = CreateEmptyFile(cardPath, "big.pdf");
            string smallPdf = CreateEmptyFile(cardPath, "small.pdf");
            CreateEmptyFile(cardPath, "V.I. Semestrale.txt");

            var card = ScanCard(cardPath);
            var pageCounts = FakePageCounts(new Dictionary<string, int> { [bigPdf] = 12, [smallPdf] = 2 });
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, pageCounts);

            Assert.Equal(PdfRenameOutcome.Ready, plan.Outcome);
            var smallOp = plan.MoveOperations.Single(o => o.OldPath == smallPdf);
            Assert.Equal("Checklist V.I. Semestrale ETR700 210 IMC AV Milano 010726 Bianchi.pdf", Path.GetFileName(smallOp.NewPath));
        }

        [Fact]
        public void CreatePlan_MultipleNcFiles_IncrementsTicketForEachAfterTheFirst()
        {
            string cardPath = CreateCardFolder("E404P 60");
            CreateLogSubfolder(cardPath, "SR1000000 LOG E404P 601 04.02HR 020726 Verdi");
            CreateEmptyFile(cardPath, "nc1.pdf");
            CreateEmptyFile(cardPath, "nc2.pdf");

            var card = ScanCard(cardPath);
            // ScanCard non conosce IsNC (proprietà solo UI, valorizzata dalla CheckBox in PdfView):
            // qui viene impostata a mano sugli item corrispondenti, come farebbe il binding reale.
            foreach (var child in card.Children.Where(c => !c.IsDirectory))
            {
                child.IsNC = true;
            }

            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Ready, plan.Outcome);
            Assert.Equal(2, plan.MoveOperations.Count);

            var names = plan.MoveOperations.Select(o => Path.GetFileName(o.NewPath)).OrderBy(n => n).ToList();
            Assert.Contains(names, n => n.StartsWith("NC SR1000000 "));
            Assert.Contains(names, n => n.StartsWith("NC SR1000001 ")); // ticket incrementato di 1 per il secondo NC
        }

        [Fact]
        public void CreatePlan_Error_WhenNoPdfFiles()
        {
            string cardPath = CreateCardFolder("ETR700 70");
            CreateLogSubfolder(cardPath, "SR1111111 LOG ETR700 100 04.02HR 030726 Neri");

            var card = ScanCard(cardPath);
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Error, plan.Outcome);
            Assert.Equal(PdfRenameErrorSeverity.Warning, plan.Severity);
        }

        [Fact]
        public void CreatePlan_Error_WhenMoreThanTwoUncheckedPdfs()
        {
            string cardPath = CreateCardFolder("ETR700 80");
            CreateLogSubfolder(cardPath, "SR2222222 LOG ETR700 100 04.02HR 040726 Neri");
            CreateEmptyFile(cardPath, "a.pdf");
            CreateEmptyFile(cardPath, "b.pdf");
            CreateEmptyFile(cardPath, "c.pdf");

            var card = ScanCard(cardPath);
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Error, plan.Outcome);
            Assert.Equal(PdfRenameErrorSeverity.Warning, plan.Severity);
        }

        [Fact]
        public void CreatePlan_Error_WhenNoLogSubfolder()
        {
            string cardPath = CreateCardFolder("ETR700 90");
            CreateEmptyFile(cardPath, "a.pdf");

            var card = ScanCard(cardPath);
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Error, plan.Outcome);
            Assert.Equal(PdfRenameErrorSeverity.Warning, plan.Severity);
        }

        [Fact]
        public void CreatePlan_Error_WhenComputedDestinationAlreadyExistsOnDisk()
        {
            // Scenario costruito a mano (non con ScanCard): in una scansione reale via
            // PdfView.LoadFolders OGNI .pdf della cartella diventa per costruzione uno degli
            // ingressi del piano (checked o unchecked), quindi il suo percorso finisce sempre
            // fra gli "OldPath" delle operazioni pianificate — rendendo questo ramo
            // (conflitto con un file che NON fa parte del piano corrente) irraggiungibile con
            // una scansione fresca. Resta comunque una protezione reale contro una TrainCards
            // non più allineata al disco (es. un aggiornamento di AppWatcher non ancora arrivato
            // quando l'utente preme "Rinomina"): qui lo si riproduce dichiarando nel card un solo
            // file (quello da rinominare) mentre sul disco reale esiste ANCHE, non dichiarato,
            // un file già occupante il nome di destinazione calcolato.
            string cardPath = CreateCardFolder("ETR700 95");
            string logFolder = "SR3333333 LOG ETR700 100 04.02HR 050726 Neri";
            CreateLogSubfolder(cardPath, logFolder);
            string inputPdf = CreateEmptyFile(cardPath, "input.pdf");
            CreateEmptyFile(cardPath, "FL SR3333333 ETR700 100 IMC AV Milano 050726 Neri.pdf"); // destinazione già occupata, non dichiarata nel card

            var card = new TrainCardModel { Title = Path.GetFileName(cardPath), FullPath = cardPath, IsND = false };
            card.Children.Add(new FolderItemModel { Name = logFolder, FullPath = Path.Combine(cardPath, logFolder), IsDirectory = true });
            card.Children.Add(new FolderItemModel { Name = "input.pdf", FullPath = inputPdf, IsDirectory = false, Extension = ".pdf" });

            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Error, plan.Outcome);
            Assert.Equal(PdfRenameErrorSeverity.Warning, plan.Severity);
        }

        [Fact]
        public void CreatePlan_NothingToDo_WhenFileAlreadyHasTheComputedName()
        {
            string cardPath = CreateCardFolder("ETR700 96");
            CreateLogSubfolder(cardPath, "SR4444444 LOG ETR700 100 04.02HR 060726 Neri");
            CreateEmptyFile(cardPath, "FL SR4444444 ETR700 100 IMC AV Milano 060726 Neri.pdf");

            var card = ScanCard(cardPath);
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.NothingToDo, plan.Outcome);
            Assert.Empty(plan.MoveOperations);
        }

        [Fact]
        public void CreatePlan_Error_WhenLogFolderNameIsUnparsable()
        {
            string cardPath = CreateCardFolder("ETR700 97");
            CreateLogSubfolder(cardPath, "cartella LOG senza il formato atteso");
            CreateEmptyFile(cardPath, "input.pdf");

            var card = ScanCard(cardPath);
            var plan = PdfRenamePlanner.CreatePlan(card, RealKnownTypes, FakePageCounts([]));

            Assert.Equal(PdfRenameOutcome.Error, plan.Outcome);
            Assert.Equal(PdfRenameErrorSeverity.Error, plan.Severity); // non Warning: qui è un problema nei dati, non nell'input dell'utente
        }
    }
}
