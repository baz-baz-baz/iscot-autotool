using System;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Modules.Verifiche;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Verifiche
{
    /// <summary>
    /// Tier 1: composizione del nuovo nome file e individuazione del foglio storico dell'anno
    /// corrente, le due regole "di nomenclatura" dell'azione "Verifica Eseguita".
    ///
    /// <para>
    /// <b>I nomi dei fogli qui sotto sono quelli reali</b>, letti dai tre file forniti dal
    /// committente: <c>STORICO 2026</c> (ETR700), <c>STORICO 22-24-25-26</c> (ETR1000) e
    /// <c>STORICO '22-'23-'24-'25-'26</c> (ETR500). Tre convenzioni diverse per la stessa cosa, il
    /// che è esattamente il motivo per cui questa logica esiste come funzione a sé e non come un
    /// confronto di stringhe sepolto nel servizio.
    /// </para>
    /// </summary>
    public sealed class VerificheArchivioNamingTests
    {
        // ------------------------------------------------------------------
        // Nome del file
        // ------------------------------------------------------------------

        /// <summary>
        /// La data è <c>ddMMyy</c>, non <c>AAMMGG</c> come la chiamava la specifica: il 24 agosto
        /// 2026 si scrive <c>240826</c>, esattamente come nei nomi dei file reali forniti dal
        /// committente e come vuole l'invariante §5.1 di PROJECT_MEMORY.md. Con l'interpretazione
        /// letterale dell'etichetta si otterrebbe <c>260824</c>, che un tecnico leggerebbe come
        /// 26 agosto 2024.
        /// </summary>
        [Fact]
        public void ComponiNomeFile_LaDataEGiornoMeseAnno_NonAnnoMeseGiorno()
        {
            string nome = VerificheArchivioNaming.ComponiNomeFile(
                "Verifiche ETR500", new DateTime(2026, 8, 24, 21, 36, 0), "Rossi");

            Assert.Equal("Verifiche ETR500 240826 21_36 Rossi.xlsx", nome);
            Assert.DoesNotContain("260824", nome);
        }

        [Fact]
        public void ComponiNomeFile_RiproduceINomiDeiFileReali()
        {
            // I tre nomi effettivamente presenti nelle cartelle Hitachi al momento della stesura.
            Assert.Equal("Verifiche ETR500 240826 14_31 Franzese.xlsx",
                VerificheArchivioNaming.ComponiNomeFile("Verifiche ETR500", new DateTime(2026, 8, 24, 14, 31, 0), "Franzese"));
            Assert.Equal("Verifiche ETR700 240826 17_30 Ruffini.xlsx",
                VerificheArchivioNaming.ComponiNomeFile("Verifiche ETR700", new DateTime(2026, 8, 24, 17, 30, 0), "Ruffini"));
            Assert.Equal("Verifiche ETR1000 240826 10_13 Del Prete.xlsx",
                VerificheArchivioNaming.ComponiNomeFile("Verifiche ETR1000", new DateTime(2026, 8, 24, 10, 13, 0), "Del Prete"));
        }

        [Theory]
        [InlineData("Verifiche ETR500", "Verifiche ETR500 240826 21_36 Rossi.xlsx")]
        [InlineData("Verifiche ETR700", "Verifiche ETR700 240826 21_36 Rossi.xlsx")]
        [InlineData("Verifiche ETR1000", "Verifiche ETR1000 240826 21_36 Rossi.xlsx")]
        public void ComponiNomeFile_UnPrefissoPerFlotta(string prefisso, string atteso) =>
            Assert.Equal(atteso, VerificheArchivioNaming.ComponiNomeFile(
                prefisso, new DateTime(2026, 8, 24, 21, 36, 0), "Rossi"));

        [Fact]
        public void ComponiNomeFile_LOraUsaIlTrattinoBassoNonIDuePunti()
        {
            // I due punti non sono ammessi nei nomi file Windows: il pattern richiesto è HH_mm.
            string nome = VerificheArchivioNaming.ComponiNomeFile(
                "Verifiche ETR700", new DateTime(2026, 1, 5, 9, 7, 0), "Bianchi");

            Assert.Equal("Verifiche ETR700 050126 09_07 Bianchi.xlsx", nome);
            Assert.DoesNotContain(":", nome);
        }

        [Fact]
        public void ComponiNomeFile_AccettaCognomiComposti()
        {
            // "Del Prete" è uno dei cognomi che compaiono nei file reali forniti.
            string nome = VerificheArchivioNaming.ComponiNomeFile(
                "Verifiche ETR1000", new DateTime(2026, 8, 24, 10, 13, 0), "Del Prete");

            Assert.Equal("Verifiche ETR1000 240826 10_13 Del Prete.xlsx", nome);
        }

        [Fact]
        public void ComponiNomeFile_NeutralizzaICaratteriVietati()
        {
            // Il cognome è digitato a mano: una barra bloccherebbe il salvataggio a fine turno.
            string nome = VerificheArchivioNaming.ComponiNomeFile(
                "Verifiche ETR500", new DateTime(2026, 8, 24, 21, 36, 0), "Ros/si:1");

            Assert.DoesNotContain(Path.GetInvalidFileNameChars(), nome.Contains);
            Assert.EndsWith(".xlsx", nome);
        }

        [Fact]
        public void ComponiNomeFile_RimuoveSpaziSuperflui() =>
            Assert.Equal("Verifiche ETR500 240826 21_36 Rossi.xlsx",
                VerificheArchivioNaming.ComponiNomeFile(
                    "  Verifiche ETR500  ", new DateTime(2026, 8, 24, 21, 36, 0), "  Rossi  "));

        // ------------------------------------------------------------------
        // Anni citati nel nome di un foglio
        // ------------------------------------------------------------------

        [Fact]
        public void EstraiAnni_AnnoSingoloAQuattroCifre() =>
            Assert.Equal([2026], VerificheArchivioNaming.EstraiAnni("STORICO 2026").OrderBy(a => a));

        [Fact]
        public void EstraiAnni_ElencoADueCifreConApostrofo_FileRealeEtr500() =>
            Assert.Equal([2022, 2023, 2024, 2025, 2026],
                VerificheArchivioNaming.EstraiAnni("STORICO '22-'23-'24-'25-'26").OrderBy(a => a));

        [Fact]
        public void EstraiAnni_ElencoADueCifreSenzaApostrofo_FileRealeEtr1000() =>
            Assert.Equal([2022, 2024, 2025, 2026],
                VerificheArchivioNaming.EstraiAnni("STORICO 22-24-25-26").OrderBy(a => a));

        [Fact]
        public void EstraiAnni_IntervalloAQuattroCifre() =>
            Assert.Equal([2019, 2020], VerificheArchivioNaming.EstraiAnni("STORICO 2019-2020").OrderBy(a => a));

        [Fact]
        public void EstraiAnni_NomeSenzaCifre_ElencoVuoto() =>
            Assert.Empty(VerificheArchivioNaming.EstraiAnni("non cancellare"));

        // ------------------------------------------------------------------
        // Scelta del foglio storico
        // ------------------------------------------------------------------

        /// <summary>Nomi dei fogli letti dal file ETR700 reale.</summary>
        private static readonly string[] FogliEtr700 =
        [
            "verifiche", "STORICO 2026", "STORICO 2025", "Foglio1", "STORICO 2024",
            "STORICO 2023", "STORICO 2022", "STORICO 2021", "STORICO 2020", "STORICO 2019", "non cancellare"
        ];

        /// <summary>Nomi dei fogli letti dal file ETR500 reale.</summary>
        private static readonly string[] FogliEtr500 =
        [
            "VERIFICHE ETR500", "STORICO '22-'23-'24-'25-'26", "STORICO 2021",
            "STORICO 2019", "STORICO 2020", "STORICO 2018", "Foglio1"
        ];

        /// <summary>Nomi dei fogli letti dal file ETR1000 reale.</summary>
        private static readonly string[] FogliEtr1000 =
        [
            "verifiche", "STORICO 22-24-25-26", "STORICO 2019-2020", "STORICO 2021-2022",
            "STORICO 2020-2021", "STORICO 2018", "STORICO 2017", "STORICO 2016", "STORICO 2015",
            "note", "non cancellare"
        ];

        [Fact]
        public void TrovaFoglioStorico_Etr700_AnnoEsplicito() =>
            Assert.Equal("STORICO 2026", VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr700, 2026));

        [Fact]
        public void TrovaFoglioStorico_Etr500_FoglioMultiAnnoConApostrofi() =>
            Assert.Equal("STORICO '22-'23-'24-'25-'26",
                VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr500, 2026));

        [Fact]
        public void TrovaFoglioStorico_Etr1000_FoglioMultiAnnoSenzaApostrofi() =>
            Assert.Equal("STORICO 22-24-25-26",
                VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr1000, 2026));

        [Fact]
        public void TrovaFoglioStorico_AnnoPrecedente_SceglieIlFoglioGiusto() =>
            Assert.Equal("STORICO 2023", VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr700, 2023));

        [Fact]
        public void TrovaFoglioStorico_AnnoNonCoperto_RestituisceNull()
        {
            // 2027 non esiste in nessuno dei tre file: il servizio deve fermarsi con un messaggio,
            // non inventarsi un foglio.
            Assert.Null(VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr700, 2027));
            Assert.Null(VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr500, 2027));
            Assert.Null(VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr1000, 2027));
        }

        [Fact]
        public void TrovaFoglioStorico_IgnoraIFogliNonStorici()
        {
            // "verifiche", "note", "Foglio1" e "non cancellare" non devono mai essere scelti.
            string? scelto = VerificheArchivioNaming.TrovaFoglioStorico(FogliEtr1000, 2026);

            Assert.StartsWith("STORICO", scelto);
        }

        [Fact]
        public void TrovaFoglioStorico_APariCopertura_PreferisceIlFoglioPiuSpecifico()
        {
            // Se un domani venisse creato "STORICO 2026" accanto al foglio multi-anno, il nuovo
            // record deve finire in quello dedicato.
            string[] fogli = ["verifiche", "STORICO 22-24-25-26", "STORICO 2026"];

            Assert.Equal("STORICO 2026", VerificheArchivioNaming.TrovaFoglioStorico(fogli, 2026));
        }

        [Fact]
        public void TrovaFoglioStorico_ElencoVuoto_RestituisceNull() =>
            Assert.Null(VerificheArchivioNaming.TrovaFoglioStorico([], 2026));
    }
}
