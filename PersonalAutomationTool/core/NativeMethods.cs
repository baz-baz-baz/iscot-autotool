using System;
using System.Runtime.InteropServices;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Funzioni Win32 P/Invoke per il pattern istanza singola (§6.1-vicies-bis di PROJECT_MEMORY.md):
    /// riportare in primo piano, ripristinandola se ridotta a icona, la finestra dell'istanza già in
    /// esecuzione quando l'utente riavvia l'eseguibile.
    ///
    /// <para>
    /// Nome della classe per convenzione .NET (CA1060/CA5392): un metodo P/Invoke va dichiarato in
    /// una classe chiamata <c>NativeMethods</c>, <c>SafeNativeMethods</c> o <c>UnsafeNativeMethods</c>,
    /// non sparso nelle classi che lo usano — rende visibile a colpo d'occhio, e agli analyzer di
    /// sicurezza, dove finisce ogni superficie non gestita dell'applicazione. Solo dichiarazioni pure
    /// qui dentro: nessuna logica applicativa, nessuna gestione d'errore — quella vive in
    /// <see cref="SingleInstanceGuard"/>, l'unico chiamante.
    /// </para>
    ///
    /// <para>
    /// <b>Compatibilità con la distribuzione stand-alone (§6.1-duodevicies).</b> Tutte le funzioni
    /// sono in <c>user32.dll</c>, componente di sistema presente su ogni installazione Windows: non
    /// viene estratta dal bundle <c>PublishSingleFile</c> né toccata da <c>PublishReadyToRun</c>, e
    /// non introduce alcuna dipendenza aggiuntiva da distribuire insieme all'eseguibile.
    /// </para>
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// Valore di <c>nCmdShow</c> per <see cref="ShowWindowAsync"/>: ripristina la finestra alla
        /// dimensione e posizione originali se ridotta a icona o massimizzata. Sicuro da passare anche
        /// a una finestra già in stato normale — <c>SW_RESTORE</c> su una finestra non minimizzata non
        /// fa nulla, quindi non serve interrogare prima lo stato con <c>IsIconic</c>.
        /// </summary>
        internal const int SW_RESTORE = 9;

        /// <summary>Lampeggia sia il bordo della finestra sia il pulsante in barra delle applicazioni.</summary>
        internal const uint FLASHW_ALL = 0x00000003;

        /// <summary>Continua a lampeggiare finché la finestra non ottiene il focus, invece di un numero fisso di volte.</summary>
        internal const uint FLASHW_TIMERNOFG = 0x0000000C;

        /// <summary>
        /// Invia <c>WM_SYSCOMMAND</c> alla finestra in modo <b>asincrono</b>: non attende che il
        /// thread proprietario elabori il messaggio. Il chiamante è un processo diverso da quello
        /// della finestra target — la variante sincrona (<c>ShowWindow</c>) potrebbe restare bloccata
        /// se quel thread è occupato in quel momento.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Porta la finestra in primo piano e le assegna il focus di tastiera. Windows può rifiutare
        /// la richiesta (regole anti-furto-focus, attive quando il processo chiamante non ha ricevuto
        /// input recente dall'utente): il chiamante deve trattare un esito <see langword="false"/>
        /// come "prova con un altro segnale", non come un errore da segnalare.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>Porta la finestra in cima all'ordine di sovrapposizione (z-order) senza attivarla.</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BringWindowToTop(IntPtr hWnd);

        /// <summary>
        /// Ripiego per quando <see cref="SetForegroundWindow"/> viene rifiutata: fa lampeggiare la
        /// finestra in barra delle applicazioni, un segnale visibile che Windows non nega mai.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        internal struct FLASHWINFO
        {
            internal uint cbSize;
            internal IntPtr hwnd;
            internal uint dwFlags;
            internal uint uCount;
            internal uint dwTimeout;
        }
    }
}
