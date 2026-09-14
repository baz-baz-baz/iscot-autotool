using System;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Modules.Home;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Home
{
    /// <summary>
    /// Tier 2 (cartelle reali su disco, come <c>PdfRenamePlannerTests</c>): copre il bug segnalato dal
    /// committente su "Aggiorna ticket" per i treni a doppia motrice (E404P/ETR500). La vecchia logica
    /// raggruppava le sottocartelle per il ticket <b>attualmente scritto nel nome</b>: quando tutte e
    /// quattro condividevano lo stesso ticket segnaposto ("SRX"), quel raggruppamento produceva un solo
    /// gruppo e il secondo campo ticket veniva silenziosamente ignorato. Questi test usano la loco
    /// (estratta da <see cref="PersonalAutomationTool.Core.Naming.LogDumpFolderName"/>), non il ticket
    /// corrente, per decidere quale ticket assegnare a quale cartella.
    /// </summary>
    public sealed class HomeTicketRenamePlannerTests : IDisposable
    {
        private static readonly string[] RealKnownTypes = ["ETR1000 I-F", "ETR1001FH", "ETR1000", "E404P", "ETR700"];

        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("HomeTicketRenamePlannerTests_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private string CreateParentFolder(string name)
        {
            string path = Path.Combine(_root.FullName, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static void CreateSubfolder(string parentPath, string name) =>
            Directory.CreateDirectory(Path.Combine(parentPath, name));

        /// <summary>
        /// Lo scenario esatto del bug report: E404P 34, due motrici (634/635), quattro cartelle
        /// (LOG+DUMP per ciascuna) tutte con lo stesso ticket segnaposto "SRX". Le cartelle della loco
        /// 634 devono ricevere Ticket 1, quelle della loco 635 Ticket 2.
        /// </summary>
        [Fact]
        public void TrenoADoppiaMotrice_ConEntrambiITicketCompilati_AssegnaPerLocoNonPerTicketAttuale()
        {
            string parent = CreateParentFolder("E404P 34");
            CreateSubfolder(parent, "SRX DUMP E404P 634 04.02HR 140924 Rossi");
            CreateSubfolder(parent, "SRX DUMP E404P 635 04.02HR 140924 Rossi");
            CreateSubfolder(parent, "SRX LOG E404P 634 04.02HR 140924 Rossi");
            CreateSubfolder(parent, "SRX LOG E404P 635 04.02HR 140924 Rossi");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "SR1234568", RealKnownTypes);

            Assert.Equal(4, plan.Count);

            var byOldName = plan.ToDictionary(p => Path.GetFileName(p.OldPath), p => Path.GetFileName(p.NewPath));
            Assert.Equal("SR1234567 DUMP E404P 634 04.02HR 140924 Rossi", byOldName["SRX DUMP E404P 634 04.02HR 140924 Rossi"]);
            Assert.Equal("SR1234568 DUMP E404P 635 04.02HR 140924 Rossi", byOldName["SRX DUMP E404P 635 04.02HR 140924 Rossi"]);
            Assert.Equal("SR1234567 LOG E404P 634 04.02HR 140924 Rossi", byOldName["SRX LOG E404P 634 04.02HR 140924 Rossi"]);
            Assert.Equal("SR1234568 LOG E404P 635 04.02HR 140924 Rossi", byOldName["SRX LOG E404P 635 04.02HR 140924 Rossi"]);

            // Nessuna duplicazione del prefisso "SR".
            Assert.All(byOldName.Values, name => Assert.DoesNotContain("SRSR", name));
        }

        [Fact]
        public void CassaSingola_ConEntrambiITicketCompilati_ApplicaSoloIlPrimoATutte()
        {
            // ETR700: una sola loco, come da specifica esplicita del committente.
            string parent = CreateParentFolder("ETR700 12");
            CreateSubfolder(parent, "SRX DUMP ETR700 117 IF01 140924 Rossi");
            CreateSubfolder(parent, "SRX LOG ETR700 117 IF01 140924 Rossi");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "SR1234568", RealKnownTypes);

            Assert.Equal(2, plan.Count);
            Assert.All(plan, p => Assert.StartsWith("SR1234567 ", Path.GetFileName(p.NewPath)));
        }

        [Fact]
        public void TrenoADoppiaMotrice_ConSoloIlPrimoTicketCompilato_ApplicaATutteLeCartelle()
        {
            string parent = CreateParentFolder("E404P 34");
            CreateSubfolder(parent, "SRX DUMP E404P 634 04.02HR 140924 Rossi");
            CreateSubfolder(parent, "SRX DUMP E404P 635 04.02HR 140924 Rossi");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "", RealKnownTypes);

            Assert.Equal(2, plan.Count);
            Assert.All(plan, p => Assert.StartsWith("SR1234567 ", Path.GetFileName(p.NewPath)));
        }

        [Fact]
        public void EntrambiITicketVuoti_NonProduceAlcunaOperazione()
        {
            string parent = CreateParentFolder("E404P 34");
            CreateSubfolder(parent, "SRX DUMP E404P 634 04.02HR 140924 Rossi");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "", "  ", RealKnownTypes);

            Assert.Empty(plan);
        }

        [Fact]
        public void CartellaGiaConIlTicketCorretto_NonVieneInclusaNelPiano()
        {
            // Idempotenza: rilanciare "Aggiorna ticket" con lo stesso valore non deve generare
            // un'operazione "vecchio == nuovo".
            string parent = CreateParentFolder("E404P 34");
            CreateSubfolder(parent, "SR1234567 DUMP E404P 634 04.02HR 140924 Rossi");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "", RealKnownTypes);

            Assert.Empty(plan);
        }

        [Fact]
        public void OrdineLocoENumericoNonAlfabetico()
        {
            // "150" precede "99" in ordine alfabetico ma non in ordine numerico: la loco più bassa
            // (99, "Loco 1") deve comunque ricevere Ticket 1.
            string parent = CreateParentFolder("E404P 99");
            CreateSubfolder(parent, "SRX DUMP E404P 150 04.02HR 140924 Rossi");
            CreateSubfolder(parent, "SRX DUMP E404P 99 04.02HR 140924 Rossi");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "SR1111111", "SR2222222", RealKnownTypes);

            var byOldName = plan.ToDictionary(p => Path.GetFileName(p.OldPath), p => Path.GetFileName(p.NewPath));
            Assert.StartsWith("SR1111111 ", byOldName["SRX DUMP E404P 99 04.02HR 140924 Rossi"]);
            Assert.StartsWith("SR2222222 ", byOldName["SRX DUMP E404P 150 04.02HR 140924 Rossi"]);
        }

        [Fact]
        public void UnaCartellaConNomeNonConformeAllaGrammatica_VieneEsclusaDalPiano()
        {
            string parent = CreateParentFolder("E404P 34");
            CreateSubfolder(parent, "SRX DUMP E404P 634 04.02HR 140924 Rossi");
            CreateSubfolder(parent, "Cartella a caso senza grammatica");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "", RealKnownTypes);

            Assert.Single(plan);
            Assert.Equal("SRX DUMP E404P 634 04.02HR 140924 Rossi", Path.GetFileName(plan[0].OldPath));
        }

        [Fact]
        public void CartellaMadreInesistente_RestituisceUnPianoVuoto()
        {
            string parent = Path.Combine(_root.FullName, "non esiste");

            var plan = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "SR1234568", RealKnownTypes);

            Assert.Empty(plan);
        }

        [Fact]
        public void ChiamateRipetuteConLoStessoInput_ProduconoLoStessoPiano()
        {
            // Garanzia usata da HomeViewModel: l'anteprima e l'esecuzione chiamano CreatePlan una
            // sola volta ciascuna, ma sullo stesso identico input devono coincidere sempre.
            string parent = CreateParentFolder("E404P 34");
            CreateSubfolder(parent, "SRX DUMP E404P 634 04.02HR 140924 Rossi");
            CreateSubfolder(parent, "SRX DUMP E404P 635 04.02HR 140924 Rossi");

            var plan1 = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "SR1234568", RealKnownTypes);
            var plan2 = HomeTicketRenamePlanner.CreatePlan(parent, "SR1234567", "SR1234568", RealKnownTypes);

            Assert.Equal(plan1, plan2);
        }
    }
}
