using System;
using System.Threading;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Debounce "in coda" (trailing): una raffica di <see cref="Segnala"/> ravvicinati produce una sola
    /// esecuzione dell'azione, trascorso il ritardo dall'<b>ultimo</b> segnale.
    ///
    /// <para>
    /// Estratto da <see cref="AppWatcher"/> nello Sprint 29 (§6.1-tricies-semel di PROJECT_MEMORY.md) per
    /// due motivi. <b>Costo:</b> la versione precedente distruggeva e ricreava un
    /// <c>System.Threading.Timer</c> per <b>ogni</b> evento del file system — migliaia durante la copia
    /// di una cartella di log — mentre qui si riarma sempre lo stesso con
    /// <see cref="Timer.Change(TimeSpan, TimeSpan)"/>. <b>Sicurezza:</b> il callback gira su un thread del
    /// pool, dove un'eccezione non intercettata chiude l'intero processo senza passare dal dispatcher
    /// WPF; qui non può uscirne, e finisce a <c>onErrore</c>.
    /// </para>
    /// </summary>
    public sealed class Debouncer : IDisposable
    {
        private readonly Timer _timer;
        private readonly TimeSpan _ritardo;
        private readonly Action _azione;
        private readonly Action<Exception>? _onErrore;
        private int _disposto;

        /// <param name="ritardo">Silenzio richiesto dopo l'ultimo segnale prima di eseguire l'azione.</param>
        /// <param name="azione">Eseguita su un thread del pool.</param>
        /// <param name="onErrore">Riceve le eccezioni lanciate da <paramref name="azione"/>.</param>
        public Debouncer(TimeSpan ritardo, Action azione, Action<Exception>? onErrore = null)
        {
            _ritardo = ritardo;
            _azione = azione ?? throw new ArgumentNullException(nameof(azione));
            _onErrore = onErrore;
            _timer = new Timer(_ => Esegui(), null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>(Ri)arma il timer. Thread-safe: chiamabile da qualunque thread, anche in raffica.</summary>
        public void Segnala()
        {
            if (Volatile.Read(ref _disposto) != 0) return;

            try
            {
                _timer.Change(_ritardo, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // Dispose concorrente: il segnale non serve più a nessuno.
            }
        }

        private void Esegui()
        {
            if (Volatile.Read(ref _disposto) != 0) return;

            try
            {
                _azione();
            }
            catch (Exception ex)
            {
                try { _onErrore?.Invoke(ex); }
                catch { /* nemmeno il gestore d'errore deve poter chiudere il processo da un thread del pool */ }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposto, 1) == 0) _timer.Dispose();
        }
    }
}
