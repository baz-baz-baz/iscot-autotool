using System;
using Xunit;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2: <see cref="SingleInstanceGuard"/>, il mutex con nome dietro il pattern istanza singola
    /// (§6.1-vicies-bis di PROJECT_MEMORY.md).
    ///
    /// <para>
    /// <b>Nome di mutex univoco per test, mai quello reale di produzione.</b> Riusare il nome che
    /// <c>App.xaml.cs</c> usa davvero farebbe collidere questi test con un'istanza reale
    /// dell'applicazione eventualmente in esecuzione sulla stessa macchina — un fallimento
    /// intermittente e dipendente dall'ambiente, esattamente la classe di instabilità che il
    /// costruttore <c>internal</c> con nome esplicito esiste per evitare (stesso principio già
    /// applicato a <c>RenamerLog</c>). Ogni test genera il proprio nome con un GUID, così anche
    /// l'esecuzione parallela di più test non collide.
    /// </para>
    ///
    /// <para>
    /// <b>Cosa non è testato qui.</b> <see cref="SingleInstanceGuard.ActivateExistingInstance"/> e la
    /// ricerca del processo gemello parlano con Win32 e con l'elenco processi reale del sistema
    /// operativo: stessa categoria della COM Excel/Outlook già esclusa dai test in questo progetto
    /// (Tier 3, non affrontato — vedi PROJECT_MEMORY.md §6.2).
    /// </para>
    /// </summary>
    public sealed class SingleInstanceGuardTests
    {
        private static string NuovoNomeMutex() => @"Local\PatTests_SingleInstance_" + Guid.NewGuid().ToString("N");

        [Fact]
        public void PrimaIstanza_OttieneIlControlloDelMutex()
        {
            using var guard = new SingleInstanceGuard(NuovoNomeMutex());

            Assert.True(guard.IsPrimaryInstance);
        }

        [Fact]
        public void SecondaIstanza_NonOttieneIlControlloDelMutexGiaPreso()
        {
            string nome = NuovoNomeMutex();
            using var prima = new SingleInstanceGuard(nome);
            using var seconda = new SingleInstanceGuard(nome);

            Assert.True(prima.IsPrimaryInstance);
            Assert.False(seconda.IsPrimaryInstance);
        }

        [Fact]
        public void DopoIlDisposeDellaPrima_UnaNuovaIstanzaPuoDiventarePrimaria()
        {
            string nome = NuovoNomeMutex();
            var prima = new SingleInstanceGuard(nome);
            Assert.True(prima.IsPrimaryInstance);

            prima.Dispose();

            using var seconda = new SingleInstanceGuard(nome);
            Assert.True(seconda.IsPrimaryInstance);
        }

        [Fact]
        public void NomiDiMutexDiversi_SonoIndipendenti()
        {
            using var a = new SingleInstanceGuard(NuovoNomeMutex());
            using var b = new SingleInstanceGuard(NuovoNomeMutex());

            Assert.True(a.IsPrimaryInstance);
            Assert.True(b.IsPrimaryInstance);
        }

        [Fact]
        public void Dispose_ChiamatoPiuVolte_NonSollevaEccezioni()
        {
            var guard = new SingleInstanceGuard(NuovoNomeMutex());

            guard.Dispose();
            var eccezione = Record.Exception(guard.Dispose);

            Assert.Null(eccezione);
        }

        [Fact]
        public void Dispose_SullIstanzaSecondaria_NonSollevaEccezioni()
        {
            // La secondaria non possiede il mutex: Dispose deve limitarsi a rilasciare le risorse
            // senza tentare ReleaseMutex, che solleverebbe se chiamato da chi non lo possiede.
            string nome = NuovoNomeMutex();
            using var prima = new SingleInstanceGuard(nome);
            var seconda = new SingleInstanceGuard(nome);

            var eccezione = Record.Exception(seconda.Dispose);

            Assert.Null(eccezione);
        }
    }
}
