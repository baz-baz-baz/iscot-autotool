using System;
using System.IO;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// <see cref="HitachiPathResolver"/>: risoluzione dinamica della cartella radice
    /// <c>"Hitachi Group"</c> (§6.1-quadragies di PROJECT_MEMORY.md — il caso reale che l'ha motivato:
    /// un tecnico con SharePoint sincronizzato sul Desktop invece che direttamente sotto
    /// <c>%USERPROFILE%</c>, con "Verifica Percorsi Hitachi" che segnalava ERRORE ovunque).
    ///
    /// <para>
    /// Tre livelli, come il resto della suite: <see cref="HitachiPathResolver.ComponiRadiciCandidate"/>
    /// è pura (Tier 1, input sintetici, nessun disco); <see cref="HitachiPathResolver.TrovaRadiceFraCandidate"/>
    /// e <see cref="HitachiPathResolver.CombinaSottoPercorso"/> toccano/riflettono il file system vero
    /// (Tier 2, cartelle temporanee proprie come <c>AppPathsTests</c>); <see cref="HitachiPathResolver.ResolveRoot"/>
    /// stesso resta <b>non testato direttamente</b> per lo stesso motivo di
    /// <c>HomeViewModel.GetLogDumpReteBasePath</c> (mai stato testato prima di questo intervento): legge
    /// il Desktop e il profilo <i>reali</i> della macchina che esegue i test, che qui non sono
    /// controllabili in modo deterministico.
    /// </para>
    /// </summary>
    public sealed class HitachiPathResolverTests
    {
        [Fact]
        public void ComponiRadiciCandidate_OrdineDesktopPrimaDelProfiloUtente()
        {
            var radici = HitachiPathResolver.ComponiRadiciCandidate(
                profiloUtente: @"C:\Users\tecnico",
                desktop: @"C:\Users\tecnico\Desktop",
                radiciOneDrive: [],
                radiciOneDriveWildcard: []);

            Assert.Equal(@"C:\Users\tecnico\Desktop", radici[0]);
            Assert.Contains(@"C:\Users\tecnico", radici);
        }

        [Fact]
        public void ComponiRadiciCandidate_IncludeUserProfileDesktopEsplicito()
        {
            // Caso distinto dal Desktop speciale: un Desktop reindirizzato da criteri aziendali non
            // coincide più con "%USERPROFILE%\Desktop", ma quest'ultimo può comunque contenere la
            // cartella sincronizzata se il reindirizzamento è avvenuto dopo la sincronizzazione.
            var radici = HitachiPathResolver.ComponiRadiciCandidate(
                profiloUtente: @"C:\Users\tecnico",
                desktop: @"D:\DesktopReindirizzato",
                radiciOneDrive: [],
                radiciOneDriveWildcard: []);

            Assert.Contains(@"C:\Users\tecnico\Desktop", radici);
            Assert.Contains(@"D:\DesktopReindirizzato", radici);
        }

        [Fact]
        public void ComponiRadiciCandidate_IncludeLeVariantiOneDrive()
        {
            var radici = HitachiPathResolver.ComponiRadiciCandidate(
                profiloUtente: @"C:\Users\tecnico",
                desktop: @"C:\Users\tecnico\Desktop",
                radiciOneDrive: [@"C:\Users\tecnico\OneDrive - Hitachi Rail"],
                radiciOneDriveWildcard: [@"C:\Users\tecnico\OneDrive - Iscot"]);

            Assert.Contains(@"C:\Users\tecnico\OneDrive - Hitachi Rail", radici);
            Assert.Contains(@"C:\Users\tecnico\OneDrive - Iscot", radici);
        }

        [Fact]
        public void ComponiRadiciCandidate_ScartaPercorsiNonQualificati()
        {
            // Il caso reale da cui protegge: Environment.GetFolderPath restituisce stringa vuota per
            // una cartella speciale non risolvibile (Sprint 29, PROJECT_MEMORY.md §6.1-tricies-semel).
            // Senza questo filtro diventerebbe un candidato relativo alla cartella corrente.
            var radici = HitachiPathResolver.ComponiRadiciCandidate(
                profiloUtente: "",
                desktop: "",
                radiciOneDrive: ["relativo\\percorso", ""],
                radiciOneDriveWildcard: []);

            Assert.Empty(radici);
        }

        [Fact]
        public void ComponiRadiciCandidate_DeduplicaPercorsiRipetuti()
        {
            // Il Desktop speciale coincide quasi sempre con "%USERPROFILE%\Desktop": senza dedup
            // comparirebbe due volte, e il chiamante lo scansionerebbe inutilmente due volte.
            var radici = HitachiPathResolver.ComponiRadiciCandidate(
                profiloUtente: @"C:\Users\tecnico",
                desktop: @"C:\Users\tecnico\Desktop",
                radiciOneDrive: [],
                radiciOneDriveWildcard: []);

            Assert.Equal(radici.Count, new System.Collections.Generic.HashSet<string>(radici, StringComparer.OrdinalIgnoreCase).Count);
        }

        [Fact]
        public void CombinaSottoPercorso_UsaLaRadiceRisoltaQuandoIlPrimoSegmentoENomeCartellaRadice()
        {
            string risultato = HitachiPathResolver.CombinaSottoPercorso(
                userProfile: @"C:\Users\tecnico",
                radiceRisolta: @"C:\Users\tecnico\Desktop\Hitachi Group",
                segmentiRelativi: ["Hitachi Group", "SSB_SST - Interventi ETR500"]);

            Assert.Equal(Path.Combine(@"C:\Users\tecnico\Desktop\Hitachi Group", "SSB_SST - Interventi ETR500"), risultato);
        }

        [Fact]
        public void CombinaSottoPercorso_RestituisceLaRadiceStessaQuandoNonRestaAltroSegmento()
        {
            string risultato = HitachiPathResolver.CombinaSottoPercorso(
                userProfile: @"C:\Users\tecnico",
                radiceRisolta: @"C:\Users\tecnico\Desktop\Hitachi Group",
                segmentiRelativi: ["Hitachi Group"]);

            Assert.Equal(@"C:\Users\tecnico\Desktop\Hitachi Group", risultato);
        }

        [Fact]
        public void CombinaSottoPercorso_TornaAlComportamentoPrecedenteSeLaRadiceNonERisolta()
        {
            // Nessuna posizione candidata esisteva: stesso identico esito di prima di questo
            // intervento, "%USERPROFILE%\Hitachi Group\...", non null — così l'health-check continua a
            // mostrare un percorso plausibile invece di "non configurato".
            string risultato = HitachiPathResolver.CombinaSottoPercorso(
                userProfile: @"C:\Users\tecnico",
                radiceRisolta: null,
                segmentiRelativi: ["Hitachi Group", "SSB_SST - Interventi ETR500"]);

            Assert.Equal(Path.Combine(@"C:\Users\tecnico", "Hitachi Group", "SSB_SST - Interventi ETR500"), risultato);
        }

        [Fact]
        public void CombinaSottoPercorso_TornaAlComportamentoPrecedenteSeIlPrimoSegmentoNonELaCartellaRadice()
        {
            // Configurazione non standard (nessuna ne esiste oggi, ma il contratto pubblico di
            // hitachi_paths.json/verifiche_paths.json non lo vieta): la radice risolta non si applica,
            // il fallback resta quello di sempre anche se una radice era stata trovata.
            string risultato = HitachiPathResolver.CombinaSottoPercorso(
                userProfile: @"C:\Users\tecnico",
                radiceRisolta: @"C:\Users\tecnico\Desktop\Hitachi Group",
                segmentiRelativi: ["Un'altra Cartella", "Sottocartella"]);

            Assert.Equal(Path.Combine(@"C:\Users\tecnico", "Un'altra Cartella", "Sottocartella"), risultato);
        }

        [Fact]
        public void TrovaRadiceFraCandidate_RispettaLOrdineDiPriorita()
        {
            string radice = Path.Combine(Path.GetTempPath(), "PatTests_HitachiResolver_" + Guid.NewGuid().ToString("N"));
            string baseDesktop = Path.Combine(radice, "desktop");
            string baseProfilo = Path.Combine(radice, "profilo");

            try
            {
                // Entrambe le basi hanno una "Hitachi Group" reale: deve vincere la prima nell'ordine
                // dato, non la prima creata né la prima in ordine alfabetico.
                Directory.CreateDirectory(Path.Combine(baseDesktop, HitachiPathResolver.NomeCartellaRadice));
                Directory.CreateDirectory(Path.Combine(baseProfilo, HitachiPathResolver.NomeCartellaRadice));

                string? trovata = HitachiPathResolver.TrovaRadiceFraCandidate([baseDesktop, baseProfilo]);

                Assert.Equal(Path.Combine(baseDesktop, HitachiPathResolver.NomeCartellaRadice), trovata);
            }
            finally
            {
                Directory.Delete(radice, true);
            }
        }

        [Fact]
        public void TrovaRadiceFraCandidate_SaltaLeBasiSenzaLaCartellaESceglieLaProssima()
        {
            string radice = Path.Combine(Path.GetTempPath(), "PatTests_HitachiResolver_" + Guid.NewGuid().ToString("N"));
            string baseDesktop = Path.Combine(radice, "desktop");
            string baseProfilo = Path.Combine(radice, "profilo");

            try
            {
                Directory.CreateDirectory(baseDesktop);
                Directory.CreateDirectory(Path.Combine(baseProfilo, HitachiPathResolver.NomeCartellaRadice));

                // baseDesktop esiste ma NON contiene "Hitachi Group": è il caso reale della macchina
                // che ha motivato l'intervento, prima che si scoprisse che la sincronizzazione era sul
                // Desktop — qui verifichiamo il caso simmetrico, un Desktop che invece non ce l'ha.
                string? trovata = HitachiPathResolver.TrovaRadiceFraCandidate([baseDesktop, baseProfilo]);

                Assert.Equal(Path.Combine(baseProfilo, HitachiPathResolver.NomeCartellaRadice), trovata);
            }
            finally
            {
                Directory.Delete(radice, true);
            }
        }

        [Fact]
        public void TrovaRadiceFraCandidate_RestituisceNullSeNessunaBaseHaLaCartella()
        {
            string radice = Path.Combine(Path.GetTempPath(), "PatTests_HitachiResolver_" + Guid.NewGuid().ToString("N"));
            string baseDesktop = Path.Combine(radice, "desktop");
            Directory.CreateDirectory(baseDesktop);

            try
            {
                string? trovata = HitachiPathResolver.TrovaRadiceFraCandidate([baseDesktop, Path.Combine(radice, "inesistente")]);

                Assert.Null(trovata);
            }
            finally
            {
                Directory.Delete(radice, true);
            }
        }

        [Fact]
        public void TrovaRadiceFraCandidate_NonCreaMaiLaCartellaCercata()
        {
            // Vincolo esplicito della richiesta: sola lettura. Un candidato inesistente deve restare
            // tale dopo la chiamata, non essere materializzato come effetto collaterale della verifica.
            string radice = Path.Combine(Path.GetTempPath(), "PatTests_HitachiResolver_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(radice);

            try
            {
                string? trovata = HitachiPathResolver.TrovaRadiceFraCandidate([radice]);

                Assert.Null(trovata);
                Assert.False(Directory.Exists(Path.Combine(radice, HitachiPathResolver.NomeCartellaRadice)));
            }
            finally
            {
                Directory.Delete(radice, true);
            }
        }
    }

    /// <summary>
    /// <see cref="HitachiPathResolver.LeggiOverrideManuale"/> tocca il file reale
    /// <c>paths_config.json</c> sotto <c>AppPaths.DataFolder</c> — stessa cartella di
    /// <c>hitachi_paths.json</c> — quindi nella stessa collection di <c>HitachiPathsManagerTests</c> per
    /// lo stesso motivo lì documentato: serializzare l'accesso allo stato condiviso di
    /// <c>%LOCALAPPDATA%\iscot-autotool</c> fra classi di test eseguite in parallelo.
    /// </summary>
    [Collection("SharedAppDataState")]
    public sealed class HitachiPathResolverOverrideTests : IDisposable
    {
        private readonly string _configPath;
        private readonly string? _preexistingContent;

        public HitachiPathResolverOverrideTests()
        {
            AppPaths.Initialize();
            _configPath = AppPaths.DataFile("paths_config.json");

            if (File.Exists(_configPath))
            {
                _preexistingContent = File.ReadAllText(_configPath);
                File.Delete(_configPath);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_preexistingContent != null) File.WriteAllText(_configPath, _preexistingContent);
                else if (File.Exists(_configPath)) File.Delete(_configPath);
            }
            catch { /* pulizia best-effort, come nelle altre suite sullo stato condiviso */ }
        }

        [Fact]
        public void LeggiOverrideManuale_RestituisceNullSeIlFileNonEsiste()
        {
            Assert.Null(HitachiPathResolver.LeggiOverrideManuale());
        }

        [Fact]
        public void LeggiOverrideManuale_LeggeIlValoreConfigurato()
        {
            File.WriteAllText(_configPath, """{ "HitachiGroupRootOverride": "D:\\Percorsi\\Hitachi Group" }""");

            Assert.Equal(@"D:\Percorsi\Hitachi Group", HitachiPathResolver.LeggiOverrideManuale());
        }

        [Fact]
        public void LeggiOverrideManuale_RestituisceNullSeIlCampoEVuotoOAssente()
        {
            File.WriteAllText(_configPath, "{}");
            Assert.Null(HitachiPathResolver.LeggiOverrideManuale());

            File.WriteAllText(_configPath, """{ "HitachiGroupRootOverride": "" }""");
            Assert.Null(HitachiPathResolver.LeggiOverrideManuale());
        }

        [Fact]
        public void LeggiOverrideManuale_RestituisceNullSeIlJsonENonValido()
        {
            // Non deve mai propagare un'eccezione: un file corrotto a mano non deve impedire
            // l'avvio dei moduli che dipendono dalla risoluzione dei percorsi.
            File.WriteAllText(_configPath, "{ questo non è JSON");

            Assert.Null(HitachiPathResolver.LeggiOverrideManuale());
        }

        [Fact]
        public void ResolveRoot_UsaLOverrideSoloSeLaCartellaEsisteDavvero()
        {
            string radiceInesistente = Path.Combine(Path.GetTempPath(), "PatTests_OverrideInesistente_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(_configPath, $$"""{ "HitachiGroupRootOverride": "{{radiceInesistente.Replace("\\", "\\\\")}}" }""");

            // Un override che punta a una cartella non più esistente non deve mai bloccare la
            // risoluzione sulle posizioni note: deve essere ignorato, non propagato come "trovato".
            string? risultato = HitachiPathResolver.ResolveRoot();

            Assert.NotEqual(radiceInesistente, risultato);
        }
    }
}
