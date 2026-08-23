using System.Collections.Generic;
using System.Linq;
using PersonalAutomationTool.Modules.Verifiche;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Verifiche
{
    /// <summary>
    /// Tier 1 (funzioni pure sui percorsi, nessun file system reale): copre
    /// <see cref="VerificheViewModel.RemoveNestedRoots"/>, la correzione del bug segnalato dal
    /// committente ("le verifiche ETR500 sono replicate due volte") — vedi PROJECT_MEMORY.md
    /// §6.1-quinquies per il meccanismo completo. I percorsi usati qui rispecchiano esattamente
    /// quelli reali costruiti da <c>LoadDataForFleet</c> per le tre flotte.
    /// </summary>
    public sealed class VerificheViewModelTests
    {
        [Fact]
        public void RemoveNestedRoots_CasoEtr500_ScartaLaRadiceCheContieneLAltra()
        {
            // Riproduce esattamente le due radici costruite per la flotta "500": la seconda è la
            // cartella madre della prima. Prima della correzione entrambe venivano scansionate
            // separatamente, e lo stesso file di report veniva trovato (e quindi le sue righe
            // aggiunte) due volte.
            string[] roots =
            [
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR500\Censimento ETR500\Verifiche ETR500",
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR500"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal([@"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR500"], result);
        }

        [Fact]
        public void RemoveNestedRoots_CasoEtr1000_NessunaRadiceENidificataNellAltra_NessunaRimozione()
        {
            // Le quattro radici di "1000" sono cartelle sorelle (stesso genitore "Hitachi Group"):
            // nessuna contiene l'altra, quindi la correzione non deve toccare questo caso.
            string[] roots =
            [
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR1000",
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR1000FH",
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR1000 FH",
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR1000IF"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal(roots.OrderBy(r => r), result.OrderBy(r => r));
        }

        [Fact]
        public void RemoveNestedRoots_CasoEtr700_NessunaRadiceENidificataNellAltra_NessunaRimozione()
        {
            string[] roots =
            [
                @"C:\Users\utente\Hitachi Group\SSB_SST - INTERVENTI ETR700 ELO BL3",
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR700"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal(roots.OrderBy(r => r), result.OrderBy(r => r));
        }

        [Fact]
        public void RemoveNestedRoots_RadiciDuplicateIdentiche_NonVengonoScartatePerErroreComeAnnidate()
        {
            // Due percorsi identici non sono "l'uno annidato nell'altro": la guardia sull'uguaglianza
            // (String.Equals) impedisce che uno scarti l'altro a vicenda, lasciando entrambi (la
            // successiva .Distinct() nel chiamante reale elimina comunque il duplicato letterale).
            string[] roots =
            [
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR700",
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR700"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void RemoveNestedRoots_NidificazioneAProfonditaMaggiore_VieneRiconosciuta()
        {
            // Non solo genitore diretto: qualunque profondità di annidamento deve essere riconosciuta.
            string[] roots =
            [
                @"C:\Radice\A\B\C\File",
                @"C:\Radice\A"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal([@"C:\Radice\A"], result);
        }

        [Fact]
        public void RemoveNestedRoots_CartellaConNomePrefissoSimile_NonVieneConfusaPerAnnidata()
        {
            // "Interventi ETR1000" non è antenata di "Interventi ETR1000FH": è un confronto per
            // segmenti di percorso (via separatore di directory), non per prefisso di stringa — un
            // confronto ingenuo per StartsWith senza separatore le tratterebbe erroneamente come
            // annidate, dato che "...ETR1000" è prefisso testuale di "...ETR1000FH".
            string[] roots =
            [
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR1000",
                @"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR1000FH"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void RemoveNestedRoots_ListaVuota_RestituisceListaVuota()
        {
            Assert.Empty(VerificheViewModel.RemoveNestedRoots([]));
        }

        [Fact]
        public void RemoveNestedRoots_UnaSolaRadice_RestituisceQuellaRadice()
        {
            string[] roots = [@"C:\Users\utente\Hitachi Group\SSB_SST - Interventi ETR500"];

            Assert.Equal(roots, VerificheViewModel.RemoveNestedRoots(roots));
        }

        [Fact]
        public void RemoveNestedRoots_TreRadiciConCatenaDiAnnidamento_TieneSoloLaPiuEsterna()
        {
            // A contiene B, B contiene C: deve sopravvivere solo A (la più esterna), perché la sua
            // scansione ricorsiva copre già sia B sia C.
            string[] roots =
            [
                @"C:\Radice\A\B\C",
                @"C:\Radice\A\B",
                @"C:\Radice\A"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal([@"C:\Radice\A"], result);
        }

        [Fact]
        public void RemoveNestedRoots_SeparatoriMisti_VengonoNormalizzatiPrimaDelConfronto()
        {
            string[] roots =
            [
                "C:/Radice/A/B",
                @"C:\Radice\A"
            ];

            var result = VerificheViewModel.RemoveNestedRoots(roots);

            Assert.Equal([@"C:\Radice\A"], result);
        }
    }
}
