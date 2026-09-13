using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 1 (solo timer, nessun file): <see cref="Debouncer"/>, il raggruppamento degli eventi di
    /// <see cref="AppWatcher"/>. I ritardi sono volutamente larghi rispetto agli intervalli fra i segnali:
    /// su una macchina carica durante l'intera suite i tempi si allungano, e un test di temporizzazione
    /// fragile è peggio di nessun test.
    /// </summary>
    public sealed class DebouncerTests
    {
        private static readonly TimeSpan Ritardo = TimeSpan.FromMilliseconds(300);

        [Fact]
        public async Task UnaRafficaDiSegnaliProduceUnaSolaEsecuzione()
        {
            int esecuzioni = 0;
            using var debouncer = new Debouncer(Ritardo, () => Interlocked.Increment(ref esecuzioni));

            for (int i = 0; i < 30; i++)
            {
                debouncer.Segnala();
                await Task.Delay(10);
            }

            await Task.Delay(Ritardo * 5);

            Assert.Equal(1, esecuzioni);
        }

        [Fact]
        public async Task SegnaliSeparatiDaUnSilenzioProduconoEsecuzioniSeparate()
        {
            int esecuzioni = 0;
            using var debouncer = new Debouncer(Ritardo, () => Interlocked.Increment(ref esecuzioni));

            debouncer.Segnala();
            Assert.True(await AttendiFinoA(() => esecuzioni == 1, TimeSpan.FromSeconds(5)));

            debouncer.Segnala();
            Assert.True(await AttendiFinoA(() => esecuzioni == 2, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task LAzioneParteSoloDopoIlRitardo()
        {
            var cronometro = Stopwatch.StartNew();
            long millisecondi = -1;
            using var debouncer = new Debouncer(Ritardo,
                () => Interlocked.CompareExchange(ref millisecondi, cronometro.ElapsedMilliseconds, -1));

            debouncer.Segnala();

            Assert.True(await AttendiFinoA(() => Interlocked.Read(ref millisecondi) >= 0, TimeSpan.FromSeconds(5)));
            Assert.True(millisecondi >= Ritardo.TotalMilliseconds - 30, $"eseguita dopo {millisecondi} ms");
        }

        [Fact]
        public async Task UnEccezioneDellAzioneFinisceAOnErroreENonFermaISegnaliSuccessivi()
        {
            // Senza questa garanzia l'eccezione, lanciata su un thread del pool, chiuderebbe il processo.
            int tentativi = 0;
            Exception? ricevuta = null;
            using var debouncer = new Debouncer(Ritardo,
                () =>
                {
                    if (Interlocked.Increment(ref tentativi) == 1) throw new IOException("file bloccato");
                },
                ex => ricevuta = ex);

            debouncer.Segnala();
            Assert.True(await AttendiFinoA(() => ricevuta != null, TimeSpan.FromSeconds(5)));

            debouncer.Segnala();
            Assert.True(await AttendiFinoA(() => tentativi == 2, TimeSpan.FromSeconds(5)));

            Assert.IsType<IOException>(ricevuta);
        }

        [Fact]
        public async Task DopoDisposeNessunaEsecuzione()
        {
            int esecuzioni = 0;
            var debouncer = new Debouncer(Ritardo, () => Interlocked.Increment(ref esecuzioni));

            debouncer.Segnala();
            debouncer.Dispose();
            debouncer.Segnala();
            await Task.Delay(Ritardo * 4);

            Assert.Equal(0, esecuzioni);
        }

        private static async Task<bool> AttendiFinoA(Func<bool> condizione, TimeSpan limite)
        {
            var cronometro = Stopwatch.StartNew();
            while (cronometro.Elapsed < limite)
            {
                if (condizione()) return true;
                await Task.Delay(20);
            }
            return condizione();
        }
    }
}
