using System;
using Xunit;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 1: la logica pura di confronto versioni dietro l'auto-update zero-click
    /// (<c>AutoUpdateService.VerificaEAggiornaAsync</c>). Non copre il controllo di rete, il download
    /// né lo script di hot-swap: quella parte parla con GitHub e con il file system in un modo che, per
    /// essere testato senza rendere la suite dipendente dalla rete, richiederebbe un seam (HttpClient
    /// iniettabile) che il resto del progetto non usa altrove per gli stessi motivi — stessa categoria
    /// di COM Excel/Outlook già esclusa dai test (Tier 3, PROJECT_MEMORY.md §6.2). Qui si verifica solo
    /// ciò che è deterministico e a rischio di bug sottile: il parsing del tag e il confronto SemVer.
    /// </summary>
    public sealed class AutoUpdateServiceTests
    {
        [Theory]
        [InlineData("v1.2.1", "1.2.0", true)]
        [InlineData("1.2.1", "1.2.0", true)]
        [InlineData("v1.2.0", "1.2.0", false)]
        [InlineData("v1.1.9", "1.2.0", false)]
        [InlineData("v2.0.0", "1.9.9", true)]
        [InlineData("v2.0.0-beta", "1.9.9", true)]
        [InlineData("v1.0.0", "1.0.0", false)]
        public void IsRemoteVersionNewer_ConfrontaVersioniValide(string tagRemoto, string versioneLocaleStr, bool attesoAggiornamento)
        {
            Version versioneLocale = Version.Parse(versioneLocaleStr);

            bool risultato = AutoUpdateService.IsRemoteVersionNewer(tagRemoto, versioneLocale);

            Assert.Equal(attesoAggiornamento, risultato);
        }

        /// <summary>
        /// Regressione mirata: <see cref="Version"/> tratta i componenti non specificati come -1, non
        /// come 0. Senza normalizzare a quattro componenti prima del confronto, un tag a tre componenti
        /// come "v1.2.0" risulterebbe sempre "più vecchio" di una versione locale a quattro componenti
        /// "1.2.0.0" (il default di build per un assembly .NET) anche quando sono la stessa versione —
        /// un aggiornamento fantasma ripetuto a ogni avvio.
        /// </summary>
        [Theory]
        [InlineData("v1.2.0", "1.2.0.0")]
        [InlineData("1.2.0", "1.2.0.0")]
        public void IsRemoteVersionNewer_VersioniEquivalentiAComponentiDiversi_NonSegnalaAggiornamento(string tagRemoto, string versioneLocaleStr)
        {
            Version versioneLocale = Version.Parse(versioneLocaleStr);

            bool risultato = AutoUpdateService.IsRemoteVersionNewer(tagRemoto, versioneLocale);

            Assert.False(risultato);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("non-una-versione")]
        [InlineData("v")]
        [InlineData("1")]
        [InlineData("uno.due.tre")]
        public void IsRemoteVersionNewer_FormatiErrati_NonSegnalaAggiornamento(string? tagRemoto)
        {
            bool risultato = AutoUpdateService.IsRemoteVersionNewer(tagRemoto, new Version(1, 0, 0));

            Assert.False(risultato);
        }

        [Theory]
        [InlineData("v1.2.0", 1, 2, 0)]
        [InlineData("1.2.0", 1, 2, 0)]
        [InlineData("V1.2.3", 1, 2, 3)]
        [InlineData("1.2.3.4", 1, 2, 3)]
        [InlineData("v1.2.3-beta.1", 1, 2, 3)]
        [InlineData("v1.2.3+build5", 1, 2, 3)]
        [InlineData("  v1.2.3  ", 1, 2, 3)]
        public void TryParseVersion_TagValidi_EstraeLaVersioneNumerica(string tag, int major, int minor, int build)
        {
            bool riuscito = AutoUpdateService.TryParseVersion(tag, out Version? versione);

            Assert.True(riuscito);
            Assert.NotNull(versione);
            Assert.Equal(major, versione!.Major);
            Assert.Equal(minor, versione.Minor);
            Assert.Equal(build, versione.Build);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("v")]
        [InlineData("1")]
        [InlineData("abc")]
        public void TryParseVersion_TagNonValidi_Fallisce(string? tag)
        {
            bool riuscito = AutoUpdateService.TryParseVersion(tag, out Version? versione);

            Assert.False(riuscito);
            Assert.Null(versione);
        }
    }
}
