using PersonalAutomationTool.Modules.Home;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Home
{
    /// <summary>
    /// Tier 1 (funzione pura su stringhe): <see cref="HomeViewModel.NormalizeTicketPrefix"/>,
    /// correzione del bug segnalato dal committente — "Aggiorna ticket" scriveva il numero senza il
    /// prefisso "SR" richiesto dalla grammatica dei nomi di cartella (invariante §5.1 di
    /// PROJECT_MEMORY.md), perché il valore digitato dall'utente (es. "1234567") sostituiva
    /// direttamente l'intero primo token del nome esistente, che su disco è "SR1234567".
    /// </summary>
    public sealed class HomeViewModelTests
    {
        [Theory]
        [InlineData("1234567", "SR1234567")]
        [InlineData("SR1234567", "SR1234567")]
        [InlineData("sr1234567", "SR1234567")]
        [InlineData("Sr1234567", "SR1234567")]
        [InlineData("sR1234567", "SR1234567")]
        public void NormalizeTicketPrefix_GarantisceUnSoloPrefissoSRInMaiuscolo(string input, string expected) =>
            Assert.Equal(expected, HomeViewModel.NormalizeTicketPrefix(input));

        [Theory]
        [InlineData("  1234567  ", "SR1234567")]
        [InlineData("  SR1234567  ", "SR1234567")]
        public void NormalizeTicketPrefix_IgnoraSpaziIniziliEFinali(string input, string expected) =>
            Assert.Equal(expected, HomeViewModel.NormalizeTicketPrefix(input));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizeTicketPrefix_ValoreVuotoOSoloSpazi_RestituisceStringaVuota(string? input) =>
            Assert.Equal(string.Empty, HomeViewModel.NormalizeTicketPrefix(input));

        [Fact]
        public void NormalizeTicketPrefix_NonDuplicaIlPrefissoSuInputGiaCorretto()
        {
            // Applicare la normalizzazione due volte di seguito (come farebbe il codice se il
            // ViewModel venisse toccato più volte) non deve produrre "SRSR...".
            string once = HomeViewModel.NormalizeTicketPrefix("1234567");
            string twice = HomeViewModel.NormalizeTicketPrefix(once);

            Assert.Equal("SR1234567", once);
            Assert.Equal("SR1234567", twice);
        }
    }
}
