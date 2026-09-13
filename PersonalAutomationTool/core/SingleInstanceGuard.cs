using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Pattern istanza singola: impedisce che l'applicazione si apra due volte, riportando in primo
    /// piano la finestra già aperta quando l'utente riavvia l'eseguibile per errore (§6.1-vicies-bis
    /// di PROJECT_MEMORY.md).
    ///
    /// <para>
    /// <b>Un <c>Mutex</c> con nome, non un file di lock o una named pipe.</b> È l'unico dei tre
    /// meccanismi comuni per cui il sistema operativo stesso garantisce l'atomicità "chi arriva prima
    /// vince": un file di lock lascia una finestra fra "controlla se esiste" e "crealo" in cui due
    /// istanze potrebbero passare entrambe, una named pipe richiederebbe un server sempre in ascolto.
    /// Il nome è fisso — un GUID generato una volta sola e scritto qui, non
    /// <c>Guid.NewGuid()</c> a runtime, che darebbe un nome diverso a ogni avvio e renderebbe
    /// impossibile per una seconda istanza trovare la prima.
    /// </para>
    ///
    /// <para>
    /// <b><c>Local\</c>, non <c>Global\</c>.</b> <c>Global\</c> renderebbe il mutex visibile a tutte le
    /// sessioni della macchina (utile solo su un server Terminal Services con più utenti connessi
    /// contemporaneamente) e in ambienti con criteri di sicurezza più stretti può sollevare
    /// <c>UnauthorizedAccessException</c> se un'istanza gira elevata e l'altra no. Questa applicazione
    /// gira su workstation d'officina con un tecnico per macchina, una sessione desktop alla volta
    /// (§3 di PROJECT_MEMORY.md): <c>Local\</c> — l'ambito è comunque quello dell'intera sessione di
    /// accesso, non del solo processo — copre esattamente il caso reale senza il rischio di
    /// <c>Global\</c>.
    /// </para>
    /// </summary>
    public sealed class SingleInstanceGuard : IDisposable
    {
        private const string MutexName =
            @"Local\PersonalAutomationTool_SingleInstance_435588cb-eb87-4942-8d6a-5bb8a8f7672c";

        private Mutex? _mutex;

        /// <summary>
        /// Vero se questo processo ha ottenuto il controllo esclusivo del mutex, cioè è la prima (e
        /// per ora unica) istanza. Se falso, un'altra istanza è già in esecuzione: il chiamante deve
        /// attivarla con <see cref="ActivateExistingInstance"/> e terminare senza proseguire l'avvio.
        /// </summary>
        public bool IsPrimaryInstance { get; }

        /// <summary>Costruttore di produzione: usa il nome fisso reale dell'applicazione.</summary>
        public SingleInstanceGuard() : this(MutexName)
        {
        }

        /// <summary>
        /// Accetta un nome di mutex esplicito solo per essere testabile senza collidere con
        /// un'istanza reale dell'applicazione eventualmente in esecuzione sulla stessa macchina —
        /// stesso trattamento già riservato a <c>RenamerLog</c> per lo stesso motivo. Nessun altro
        /// cambiamento di comportamento rispetto al costruttore di produzione.
        /// </summary>
        internal SingleInstanceGuard(string mutexName)
        {
            try
            {
                _mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
                IsPrimaryInstance = createdNew;
            }
            catch (AbandonedMutexException)
            {
                // L'istanza precedente deteneva il mutex ed è terminata senza rilasciarlo (crash,
                // terminazione da Task Manager, mancanza di corrente...). L'eccezione è solo un
                // avviso: concede comunque a questo processo la proprietà del mutex, quindi si
                // procede esattamente come se lo avesse creato lui — è di fatto l'unica istanza viva,
                // e la finestra dell'istanza precedente non esiste più da attivare.
                IsPrimaryInstance = true;
            }
        }

        /// <summary>
        /// Riporta in primo piano, ripristinandola se ridotta a icona, la finestra principale
        /// dell'istanza già in esecuzione. Da chiamare solo quando <see cref="IsPrimaryInstance"/> è
        /// falso, <b>prima</b> di terminare questo processo.
        /// </summary>
        public static void ActivateExistingInstance()
        {
            IntPtr handle = FindExistingInstanceWindowHandle();
            if (handle == IntPtr.Zero) return;

            NativeMethods.ShowWindowAsync(handle, NativeMethods.SW_RESTORE);
            NativeMethods.BringWindowToTop(handle);

            if (!NativeMethods.SetForegroundWindow(handle))
            {
                var flashInfo = new NativeMethods.FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.FLASHWINFO>(),
                    hwnd = handle,
                    dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMERNOFG,
                    uCount = uint.MaxValue,
                    dwTimeout = 0
                };
                NativeMethods.FlashWindowEx(ref flashInfo);
            }
        }

        /// <summary>
        /// Cerca, fra i processi con lo stesso nome di questo, il primo con una finestra reale
        /// (<c>MainWindowHandle</c> diverso da zero) diverso dal processo corrente.
        ///
        /// <para>
        /// <b>Il filtro sulla finestra non è ridondante.</b> In sviluppo (<c>dotnet run</c>, vedi
        /// <c>Avvia.bat</c>) il nome di processo è "dotnet" per **qualunque** eseguibile .NET in corso
        /// sulla macchina — un <c>dotnet build</c> o <c>dotnet test</c> in background comparirebbe
        /// nell'elenco. Un processo del genere non ha mai una finestra, quindi il filtro lo esclude
        /// senza bisogno di distinguere esplicitamente sviluppo da produzione. In produzione (incluso
        /// il pacchetto <c>PublishSingleFile</c> dello Sprint 16) il nome è invece univoco
        /// (<c>PersonalAutomationTool</c>), e il filtro è puramente difensivo.
        /// </para>
        /// </summary>
        private static IntPtr FindExistingInstanceWindowHandle()
        {
            using Process processoCorrente = Process.GetCurrentProcess();
            string nomeProcesso = processoCorrente.ProcessName;
            int idCorrente = processoCorrente.Id;

            IntPtr handle = IntPtr.Zero;
            foreach (Process candidato in Process.GetProcessesByName(nomeProcesso))
            {
                try
                {
                    if (handle == IntPtr.Zero && candidato.Id != idCorrente && candidato.MainWindowHandle != IntPtr.Zero)
                    {
                        handle = candidato.MainWindowHandle;
                    }
                }
                finally
                {
                    candidato.Dispose();
                }
            }

            return handle;
        }

        /// <summary>
        /// Rilascia il mutex se questa istanza lo possiede, poi lo elimina. Da chiamare in
        /// <c>App.OnExit</c>: senza un rilascio esplicito il mutex si libera comunque quando il
        /// processo termina, ma solo dopo che il sistema operativo se ne accorge — con
        /// <c>ReleaseMutex</c> esplicito la finestra in cui una nuova istanza potrebbe trovarlo ancora
        /// occupato per un'uscita in corso si riduce a zero.
        /// </summary>
        public void Dispose()
        {
            if (_mutex == null) return;

            try
            {
                if (IsPrimaryInstance) _mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rilascio del mutex di istanza singola non riuscito: {ex.Message}");
            }
            finally
            {
                _mutex.Dispose();
                _mutex = null;
            }
        }
    }
}
