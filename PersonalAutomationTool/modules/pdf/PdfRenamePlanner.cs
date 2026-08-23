using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PersonalAutomationTool.Core.Naming;
using PersonalAutomationTool.Modules.Pdf.Models;

namespace PersonalAutomationTool.Modules.Pdf
{
    public enum PdfRenameOutcome
    {
        /// <summary>Un vincolo di business non è soddisfatto (numero di file, cartelle mancanti, conflitti su disco...).</summary>
        Error,

        /// <summary>Il piano è valido ma non contiene alcuno spostamento: i file hanno già i nomi corretti.</summary>
        NothingToDo,

        /// <summary>Il piano è pronto: <see cref="PdfRenamePlan.MoveOperations"/> contiene gli spostamenti da eseguire.</summary>
        Ready
    }

    /// <summary>
    /// Rispecchia i due <c>MessageBoxImage</c> usati dal codice originale per gli errori di
    /// pianificazione: <see cref="Warning"/> per i vincoli di input più "normali" (numero di file,
    /// cartelle mancanti, conflitto con un file già presente), <see cref="Error"/> per gli esiti che
    /// segnalano un problema nei dati stessi (nomi non analizzabili, piano internamente inconsistente).
    /// Preservata per non perdere la distinzione visiva (icona/titolo del MessageBox) che
    /// l'utente vedeva prima di questa estrazione.
    /// </summary>
    public enum PdfRenameErrorSeverity
    {
        Warning,
        Error
    }

    public sealed record PdfRenamePlan
    {
        public required PdfRenameOutcome Outcome { get; init; }
        public string? ErrorMessage { get; init; }
        public PdfRenameErrorSeverity Severity { get; init; } = PdfRenameErrorSeverity.Error;
        public IReadOnlyList<(string OldPath, string NewPath)> MoveOperations { get; init; } = [];

        public static PdfRenamePlan Error(string message, PdfRenameErrorSeverity severity = PdfRenameErrorSeverity.Error) =>
            new() { Outcome = PdfRenameOutcome.Error, ErrorMessage = message, Severity = severity };
        public static PdfRenamePlan NothingToDo() => new() { Outcome = PdfRenameOutcome.NothingToDo };
        public static PdfRenamePlan Ready(IReadOnlyList<(string OldPath, string NewPath)> ops) => new() { Outcome = PdfRenameOutcome.Ready, MoveOperations = ops };
    }

    /// <summary>
    /// Logica di decisione, estratta da <c>PdfView.BtnRinomina_Click</c> (intervento 2.1 della
    /// roadmap: separare "decidere" da "eseguire"). Nessuna dipendenza da WPF — <see cref="TrainCardModel"/>
    /// e <see cref="FolderItemModel"/> non ne hanno a loro volta, quindi questa classe è testabile
    /// senza avviare un'applicazione WPF.
    /// <para>
    /// <see cref="CreatePlan"/> fa la sola eccezione di I/O di sola lettura consentita a un
    /// "pianificatore": <c>File.Exists</c> per rilevare conflitti su disco, indispensabile per
    /// decidere correttamente il piano. Non esegue mai scritture (<c>File.Move</c>/<c>File.Delete</c>):
    /// quelle restano a carico del chiamante, che è anche l'unico punto che deve gestire eventuali
    /// fallimenti a metà operazione.
    /// </para>
    /// </summary>
    public static partial class PdfRenamePlanner
    {
        [GeneratedRegex(@"SR(\d+)")]
        private static partial Regex SrTicketRegex();

        /// <summary>
        /// Calcola il piano di rinomina per una cartella madre (<paramref name="card"/>), secondo le
        /// stesse regole del codice originale: FL (1 PDF), FL+NdL (2 PDF, il più corto per pagine
        /// diventa NdL, o "Checklist {nome file txt}" se presente un file .txt), NC in coda con
        /// ticket incrementato per ogni file successivo al primo.
        /// </summary>
        /// <param name="card">La cartella madre con i suoi figli (sottocartelle e file).</param>
        /// <param name="knownTypes">Elenco dei "tipo" noti, ordinato per lunghezza decrescente (vedi <see cref="LogDumpFolderName.TryParse"/>).</param>
        /// <param name="getPdfPageCount">Conteggio pagine di un PDF dato il percorso completo. Iniettato per non far dipendere questa classe da PdfSharp.</param>
        public static PdfRenamePlan CreatePlan(TrainCardModel card, IReadOnlyList<string> knownTypes, Func<string, int> getPdfPageCount)
        {
            var pdfFiles = card.Children.Where(c => !c.IsDirectory && c.Extension == ".pdf").ToList();
            if (pdfFiles.Count == 0)
            {
                return PdfRenamePlan.Error("È richiesto almeno 1 file PDF nella cartella per questa operazione.", PdfRenameErrorSeverity.Warning);
            }

            var uncheckedFiles = pdfFiles.Where(p => !p.IsNC).ToList();
            var checkedFiles = pdfFiles.Where(p => p.IsNC).ToList();

            if (uncheckedFiles.Count > 2)
            {
                return PdfRenamePlan.Error("Sono permessi al massimo 2 file PDF non spuntati (normali) per questa operazione.", PdfRenameErrorSeverity.Warning);
            }

            var logFolders = card.Children.Where(c => c.IsDirectory && c.Name.Contains(" LOG ")).ToList();
            if (logFolders.Count == 0)
            {
                return PdfRenamePlan.Error("Nessuna cartella LOG trovata per estrarre le informazioni.", PdfRenameErrorSeverity.Warning);
            }

            var parsedInfos = new List<LogDumpFolderName>();
            foreach (var logDir in logFolders)
            {
                if (LogDumpFolderName.TryParse(logDir.Name, knownTypes, out var info))
                {
                    parsedInfos.Add(info!);
                }
            }

            if (parsedInfos.Count == 0)
            {
                return PdfRenamePlan.Error("Impossibile analizzare i nomi delle cartelle LOG.");
            }

            var first = parsedInfos.First();
            string prefix = card.IsND ? "ND FL" : "FL";
            string newName;

            if (parsedInfos.Count == 1)
            {
                newName = $"{prefix} SR{first.Ticket} {first.Tipo} {first.Loco} IMC AV Milano {first.Data} {first.Utente}.pdf";
            }
            else
            {
                var second = parsedInfos[1];
                string locoPart = first.Loco == second.Loco ? first.Loco : $"{first.Loco} - {second.Loco}";
                newName = $"{prefix} SR{first.Ticket} - SR{second.Ticket} {first.Tipo} {locoPart} IMC AV Milano {first.Data} {first.Utente}.pdf";
            }

            var moveOperations = new List<(string OldPath, string NewPath)>();

            if (uncheckedFiles.Count == 1)
            {
                moveOperations.Add((uncheckedFiles[0].FullPath, Path.Combine(card.FullPath, newName)));
            }
            else if (uncheckedFiles.Count == 2)
            {
                int pages1 = getPdfPageCount(uncheckedFiles[0].FullPath);
                int pages2 = getPdfPageCount(uncheckedFiles[1].FullPath);

                var smallerPdf = pages1 <= pages2 ? uncheckedFiles[0] : uncheckedFiles[1];
                var largerPdf = pages1 <= pages2 ? uncheckedFiles[1] : uncheckedFiles[0];

                string newNameNdL;
                var txtFiles = card.Children.Where(c => !c.IsDirectory && c.Extension == ".txt").ToList();
                if (txtFiles.Count > 0)
                {
                    string txtNameBase = Path.GetFileNameWithoutExtension(txtFiles[0].Name);
                    string locoStr = parsedInfos.Count == 1 ? parsedInfos[0].Loco :
                                     (parsedInfos[0].Loco == parsedInfos[1].Loco ? parsedInfos[0].Loco : $"{parsedInfos[0].Loco} - {parsedInfos[1].Loco}");
                    newNameNdL = $"Checklist {txtNameBase} {parsedInfos[0].Tipo} {locoStr} IMC AV Milano {parsedInfos[0].Data} {parsedInfos[0].Utente}.pdf";
                }
                else
                {
                    newNameNdL = newName.Replace("ND FL ", "NdL ").Replace("FL ", "NdL ");
                }

                moveOperations.Add((largerPdf.FullPath, Path.Combine(card.FullPath, newName)));
                moveOperations.Add((smallerPdf.FullPath, Path.Combine(card.FullPath, newNameNdL)));
            }

            string baseNcName = newName.Replace("ND FL ", "NC ").Replace("FL ", "NC ");
            for (int i = 0; i < checkedFiles.Count; i++)
            {
                string currentNcName = baseNcName;
                if (i > 0)
                {
                    currentNcName = SrTicketRegex().Replace(currentNcName, m =>
                    {
                        if (long.TryParse(m.Groups[1].Value, out long tic))
                        {
                            return "SR" + (tic + i).ToString();
                        }
                        return m.Value;
                    });
                }
                moveOperations.Add((checkedFiles[i].FullPath, Path.Combine(card.FullPath, currentNcName)));
            }

            var dests = moveOperations.Select(m => m.NewPath).ToList();
            if (dests.Distinct(StringComparer.OrdinalIgnoreCase).Count() != dests.Count)
            {
                return PdfRenamePlan.Error("Errore: la rinomina calcolata genererebbe file di destinazione duplicati.");
            }

            foreach (var (oldPath, newPath) in moveOperations)
            {
                if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
                {
                    bool isOneOfOriginals = moveOperations.Any(m => string.Equals(m.OldPath, newPath, StringComparison.OrdinalIgnoreCase));
                    if (!isOneOfOriginals)
                    {
                        return PdfRenamePlan.Error("Esiste già un altro file di destinazione nel percorso:\n" + Path.GetFileName(newPath), PdfRenameErrorSeverity.Warning);
                    }
                }
            }

            var actualMoves = moveOperations
                .Where(m => !string.Equals(m.OldPath, m.NewPath, StringComparison.Ordinal))
                .ToList();

            return actualMoves.Count == 0 ? PdfRenamePlan.NothingToDo() : PdfRenamePlan.Ready(actualMoves);
        }
    }
}
