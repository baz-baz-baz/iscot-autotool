using System;

namespace PersonalAutomationTool.Modules.Excel
{
    /// <summary>
    /// Nome esatto del foglio "Interventi" per ciascuna flotta, così come appare nella scheda del
    /// Report Interventi reale — verificato sui quattro file aziendali correnti, non ipotizzato:
    /// <list type="bullet">
    /// <item>ETR700 → "Interventi ETR700"</item>
    /// <item>E404P (ETR500) → "Interventi ETR500"</item>
    /// <item>ETR1000 / 1000FH → "Interventi ETR1000"</item>
    /// <item>ETR1000 I-F → "Interventi ETR1000 FR"</item>
    /// </list>
    /// In tutti e quattro i file il foglio "istruzioni" convive con uno o più fogli non di lavoro
    /// (grafico, "Foglio1") e la scheda "Interventi" **non è sempre la prima**: da qui il bug ETR500
    /// (e, identico, ETR1000 I-F) corretto risolvendo <see cref="ReportInterventiWriter"/> per nome
    /// invece che per posizione.
    /// </summary>
    public static class ReportSheetNames
    {
        /// <param name="selectedTrain">Il valore di <c>ExcelViewModel.SelectedTrain</c> per la flotta corrente.</param>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="selectedTrain"/> non è una delle quattro flotte gestite dal modulo EXCEL:
        /// esplicita invece di far scrivere il report su un foglio indovinato.
        /// </exception>
        public static string GetInterventiSheetName(string? selectedTrain) => selectedTrain switch
        {
            "ETR700" => "Interventi ETR700",
            "E404P" => "Interventi ETR500",
            ReportOptionsCatalog.Etr1000Report => "Interventi ETR1000",
            "ETR1000 I-F" => "Interventi ETR1000 FR",
            _ => throw new InvalidOperationException(
                $"Nessun foglio Interventi configurato per la flotta '{selectedTrain}'.")
        };
    }
}
