using PersonalAutomationTool.Modules.DestinatariMail;
using PersonalAutomationTool.Modules.PassaggioConsegne;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.PassaggioConsegne
{
    /// <summary>
    /// Tier 2: il nome dell'azione con cui il modulo PASSAGGIO CONSEGNE cerca i propri destinatari
    /// deve corrispondere <b>esattamente</b> a quello registrato in <c>destinatari.json</c>.
    ///
    /// <para>
    /// <b>Perché questa suite esiste.</b> Alla prima stesura del modulo l'azione era stata chiamata
    /// "Passaggio Consegne", mentre la voce reale — presente da sempre nei file installati sulle
    /// macchine dei tecnici, con gli indirizzi corretti e personalizzati a mano — si chiama
    /// "Passaggio di consegne". Il difetto non produce un errore: <c>GetRecipients</c> semplicemente
    /// non trova nulla, e nella schermata DESTINATARI MAIL compare una <b>seconda</b> riga con i
    /// valori di default, lasciando inutilizzata quella buona. Segnalato dal committente con uno
    /// screenshot. Questi test rendono impossibile ripetere l'errore in silenzio.
    /// </para>
    ///
    /// <para>
    /// Nota: <c>GetRecipients</c> passa da <c>LoadConfig</c>, che se non trova
    /// <c>destinatari.json</c> nella cartella di output genera e salva la configurazione di default —
    /// che è proprio ciò che si vuole verificare qui.
    /// </para>
    /// </summary>
    public sealed class AzioneDestinatariTests
    {
        [Fact]
        public void IlNomeDellAzioneEQuelloStoricoDelFileDestinatari()
        {
            // Scritto per esteso e non derivato da una costante: se qualcuno cambia la costante,
            // questo test deve fallire, non seguirla.
            Assert.Equal("Passaggio di consegne", OutlookRapportinoMailService.AzioneDestinatari);
        }

        [Theory]
        [InlineData("E404P")]    // scheda "ETR 500"
        [InlineData("ETR700")]
        [InlineData("ETR1000")]
        public void OgniFlottaDelModuloRisolveIPropriDestinatari(string destinatariKey)
        {
            var destinatari = DestinatariManager.GetRecipients(
                destinatariKey, OutlookRapportinoMailService.AzioneDestinatari);

            Assert.NotNull(destinatari);
            Assert.False(string.IsNullOrWhiteSpace(destinatari!.ToRecipients),
                $"destinatari 'A:' mancanti per {destinatariKey}");
        }

        [Fact]
        public void LaSchedaEtr500SiRisolveTramiteLaChiaveE404P()
        {
            // La flotta si chiama "ETR 500" nell'interfaccia ma "E404P" in destinatari.json (§5.3).
            // GetRecipients accetta entrambe grazie alla regola 500/404, ma il modulo passa "E404P".
            var perEtichetta = DestinatariManager.GetRecipients("ETR 500", OutlookRapportinoMailService.AzioneDestinatari);
            var perChiave = DestinatariManager.GetRecipients("E404P", OutlookRapportinoMailService.AzioneDestinatari);

            Assert.NotNull(perEtichetta);
            Assert.NotNull(perChiave);
            Assert.Equal(perChiave!.ToRecipients, perEtichetta!.ToRecipients);
        }

        [Fact]
        public void LaConfigurazioneDiDefaultNonContieneNomiDiAzioneQuasiUguali()
        {
            // Due voci che differiscono solo per una parola ("Passaggio Consegne" contro "Passaggio di
            // consegne") sono indistinguibili a colpo d'occhio nella schermata DESTINATARI MAIL, ed è
            // così che il difetto originale è passato inosservato fino allo screenshot del committente.
            foreach (var treno in DestinatariManager.LoadConfig())
            {
                var azioniPassaggio = treno.Actions
                    .Where(a => a.ActionName.Replace(" ", string.Empty)
                        .Contains("assaggio", System.StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.ActionName)
                    .ToList();

                Assert.True(azioniPassaggio.Count <= 1,
                    $"il treno {treno.TrainName} ha più voci di passaggio consegne: {string.Join(" | ", azioniPassaggio)}");
            }
        }
    }
}
