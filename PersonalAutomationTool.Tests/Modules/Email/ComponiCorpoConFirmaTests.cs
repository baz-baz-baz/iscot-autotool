using PersonalAutomationTool.Modules.Email;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Email
{
    /// <summary>
    /// Tier 1: <see cref="EmailService.ComponiCorpoConFirma"/>, cioè l'invariante §5.5 di
    /// PROJECT_MEMORY.md — <b>il corpo del messaggio va inserito dentro l'HTML della firma, non
    /// concatenato prima</b>, altrimenti la firma finisce in testa all'email.
    ///
    /// <para>
    /// Era una regola dichiarata a parole e verificabile solo aprendo Outlook. È diventata
    /// verificabile qui quando la logica è stata estratta da <c>MergeBodyWithSignature</c> per essere
    /// condivisa con il modulo PASSAGGIO CONSEGNE — la cui prima versione aveva una propria copia che
    /// sbagliava esattamente questo punto, facendo <c>HTMLBody = corpo + firma</c>.
    /// </para>
    /// </summary>
    public sealed class ComponiCorpoConFirmaTests
    {
        private const string Corpo = "<p>Buongiorno,</p>";

        [Fact]
        public void IlCorpoVieneInseritoSubitoDopoIlTagBodyDellaFirma()
        {
            string firma = "<html><head></head><body lang=IT><p>Mario Rossi</p></body></html>";

            string risultato = EmailService.ComponiCorpoConFirma(Corpo, firma);

            Assert.Equal("<html><head></head><body lang=IT>" + Corpo + "<p>Mario Rossi</p></body></html>", risultato);
        }

        [Fact]
        public void LaFirmaRestaDopoIlCorpo_NonInTesta()
        {
            string firma = "<html><body><p>Mario Rossi</p></body></html>";

            string risultato = EmailService.ComponiCorpoConFirma(Corpo, firma);

            Assert.True(risultato.IndexOf(Corpo, System.StringComparison.Ordinal)
                        < risultato.IndexOf("Mario Rossi", System.StringComparison.Ordinal),
                "il corpo deve precedere la firma");
        }

        [Fact]
        public void TagBodyConAttributi_VieneRiconosciutoCorrettamente()
        {
            string firma = "<html><body style=\"margin:0\" class=\"x\"><p>Firma</p></body></html>";

            string risultato = EmailService.ComponiCorpoConFirma(Corpo, firma);

            Assert.Contains("class=\"x\">" + Corpo, risultato);
        }

        [Fact]
        public void TagBodyInMaiuscolo_VieneRiconosciuto()
        {
            string firma = "<HTML><BODY><p>Firma</p></BODY></HTML>";

            string risultato = EmailService.ComponiCorpoConFirma(Corpo, firma);

            Assert.Contains("<BODY>" + Corpo, risultato);
        }

        [Fact]
        public void FirmaVuota_ProduceUnDocumentoHtmlMinimo()
        {
            string risultato = EmailService.ComponiCorpoConFirma(Corpo, string.Empty);

            Assert.Equal("<html><body>" + Corpo + "</body></html>", risultato);
        }

        [Fact]
        public void FirmaSenzaTagBody_IlCorpoVaComunquePrima()
        {
            string firma = "<p>Firma senza body</p>";

            string risultato = EmailService.ComponiCorpoConFirma(Corpo, firma);

            Assert.Equal(Corpo + firma, risultato);
        }

        [Fact]
        public void TagBodyNonChiuso_RicadeSullaConcatenazione()
        {
            // Firma malformata: meglio un'email con la firma in coda che un'eccezione a fine turno.
            string firma = "<html><body";

            string risultato = EmailService.ComponiCorpoConFirma(Corpo, firma);

            Assert.Equal(Corpo + firma, risultato);
        }
    }
}
