using System;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2 (file veri su disco): il trasferimento dello stato applicativo verso <c>%APPDATA%</c>
    /// eseguito da <see cref="AppPaths"/> al primo avvio.
    ///
    /// <para>
    /// <b>Perché questa suite conta più di quanto sembri.</b> Copre la riga che separa "l'applicazione
    /// conserva le personalizzazioni dei tecnici" da "le perde a ogni aggiornamento". Il difetto che ha
    /// motivato <see cref="AppPaths"/> non era teorico: pubblicando in single-file, i database finivano
    /// nella cartella temporanea di estrazione del bundle, il cui nome dipende dall'hash
    /// dell'eseguibile — quindi cambiava a ogni release, portandosi via <c>destinatari.json</c> con gli
    /// indirizzi reali compilati a mano. La regola verificata qui — <b>copia solo ciò che manca, non
    /// sovrascrivere mai</b> — è ciò che rende l'operazione ripetibile a ogni avvio senza distruggere
    /// nulla.
    /// </para>
    /// </summary>
    public sealed class AppPathsTests : IDisposable
    {
        private readonly string _origine;
        private readonly string _destinazione;

        public AppPathsTests()
        {
            string radice = Path.Combine(Path.GetTempPath(), "PatTests_AppPaths_" + Guid.NewGuid().ToString("N"));
            _origine = Path.Combine(radice, "installazione");
            _destinazione = Path.Combine(radice, "dati");
            Directory.CreateDirectory(_origine);
            Directory.CreateDirectory(_destinazione);
        }

        public void Dispose()
        {
            try
            {
                string radice = Path.GetDirectoryName(_origine)!;
                if (Directory.Exists(radice)) Directory.Delete(radice, true);
            }
            catch { /* pulizia best-effort: un file agganciato non deve far fallire la suite */ }
        }

        [Fact]
        public void CopiaIFileCheNellaDestinazioneNonEsistono()
        {
            ScriviOrigine("destinatari.json", "{ \"reali\": true }");

            int copiati = AppPaths.TrasferisciFileMancanti(_origine, _destinazione, ["destinatari.json"]);

            Assert.Equal(1, copiati);
            Assert.Equal("{ \"reali\": true }", LeggiDestinazione("destinatari.json"));
        }

        [Fact]
        public void NonSovrascriveUnFileGiaPresenteNellaDestinazione()
        {
            // È l'invariante che protegge il lavoro dell'utente: un aggiornamento dell'applicazione
            // porta con sé un destinatari.json di default, che non deve rimpiazzare quello curato a mano.
            ScriviOrigine("destinatari.json", "DEFAULT DELLA NUOVA RELEASE");
            ScriviDestinazione("destinatari.json", "INDIRIZZI VERI DEL TECNICO");

            int copiati = AppPaths.TrasferisciFileMancanti(_origine, _destinazione, ["destinatari.json"]);

            Assert.Equal(0, copiati);
            Assert.Equal("INDIRIZZI VERI DEL TECNICO", LeggiDestinazione("destinatari.json"));
        }

        [Fact]
        public void EseguitoDueVolteNonCambiaNullaLaSecondaVolta()
        {
            // Gira a ogni avvio: deve essere idempotente.
            ScriviOrigine("shortcuts.json", "contenuto");

            int prima = AppPaths.TrasferisciFileMancanti(_origine, _destinazione, ["shortcuts.json"]);
            int seconda = AppPaths.TrasferisciFileMancanti(_origine, _destinazione, ["shortcuts.json"]);

            Assert.Equal(1, prima);
            Assert.Equal(0, seconda);
        }

        [Fact]
        public void CreaLeSottocartelleMancantiNellaDestinazione()
        {
            // I database stanno sotto modules\database: la cartella non esiste al primo avvio.
            ScriviOrigine(@"modules\database\train_software.db", "sqlite");

            int copiati = AppPaths.TrasferisciFileMancanti(_origine, _destinazione, [@"modules\database\train_software.db"]);

            Assert.Equal(1, copiati);
            Assert.True(File.Exists(Path.Combine(_destinazione, @"modules\database\train_software.db")));
        }

        [Fact]
        public void UnFileAssenteNellOrigineVieneSemplicementeSaltato()
        {
            int copiati = AppPaths.TrasferisciFileMancanti(_origine, _destinazione, ["mai_esistito.json"]);

            Assert.Equal(0, copiati);
        }

        [Fact]
        public void UnFileNonCopiabileNonImpedisceIlTrasferimentoDegliAltri()
        {
            // Un percorso non valido simula il fallimento del singolo file: l'avvio deve proseguire.
            ScriviOrigine("shortcuts.json", "buono");

            int copiati = AppPaths.TrasferisciFileMancanti(
                _origine, _destinazione, ["nome|non>valido.json", "shortcuts.json"]);

            Assert.Equal(1, copiati);
            Assert.Equal("buono", LeggiDestinazione("shortcuts.json"));
        }

        [Fact]
        public void TrasferisceTuttiIFileDiStatoInsieme()
        {
            foreach (string relativo in AppPaths.FileDiStatoGestiti) ScriviOrigine(relativo, "x");

            int copiati = AppPaths.TrasferisciFileMancanti(_origine, _destinazione, AppPaths.FileDiStatoGestiti);

            Assert.Equal(AppPaths.FileDiStatoGestiti.Count, copiati);
        }

        [Theory]
        [InlineData("destinatari.json")]
        [InlineData("shortcuts.json")]
        [InlineData("hitachi_paths.json")]
        [InlineData("verifiche_paths.json")]
        [InlineData(@"modules\database\train_software.db")]
        [InlineData(@"modules\database\emails.db")]
        public void LElencoDeiFileDiStatoCopreTuttoCioCheLApplicazioneScrive(string atteso) =>
            Assert.Contains(atteso, AppPaths.FileDiStatoGestiti);

        [Fact]
        public void IDatabaseVivonoSottoLaCartellaDati()
        {
            AppPaths.Initialize();

            Assert.StartsWith(AppPaths.DataFolder, AppPaths.DatabaseFolder, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                Path.Combine(AppPaths.DatabaseFolder, "train_software.db"),
                AppPaths.DatabaseFile("train_software.db"));
        }

        [Fact]
        public void LaCartellaDatiStaSottoAppDataENonAccantoAllEseguibile()
        {
            // Il punto dell'intero intervento: lo stato scrivibile non deve dipendere da dove si trova
            // l'eseguibile né da come è stato pubblicato.
            AppPaths.Initialize();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            Assert.StartsWith(appData, AppPaths.DataFolder, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(AppPaths.DataFolder));
        }

        // ------------------------------------------------------------------
        // Supporto
        // ------------------------------------------------------------------

        private void ScriviOrigine(string relativo, string contenuto)
        {
            string percorso = Path.Combine(_origine, relativo);
            Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
            File.WriteAllText(percorso, contenuto);
        }

        private void ScriviDestinazione(string relativo, string contenuto)
        {
            string percorso = Path.Combine(_destinazione, relativo);
            Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
            File.WriteAllText(percorso, contenuto);
        }

        private string LeggiDestinazione(string relativo) =>
            File.ReadAllText(Path.Combine(_destinazione, relativo));
    }
}
