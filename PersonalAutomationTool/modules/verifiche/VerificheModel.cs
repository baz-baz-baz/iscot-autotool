namespace PersonalAutomationTool.Modules.Verifiche
{
    public class VerificheModel
    {
        public string Treno { get; set; } = string.Empty;
        public string Loco { get; set; } = string.Empty;
        public string Avaria { get; set; } = string.Empty;

        /// <summary>
        /// File <c>.xlsx</c> da cui la riga è stata letta. Necessario perché una flotta può essere
        /// alimentata da <b>più cartelle</b> (per "1000" anche ETR1000FH e ETR1000IF, §2.5): senza
        /// questo dato "Verifica Eseguita" non saprebbe quale workbook aggiornare.
        /// </summary>
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Numero di riga Excel (1-based) da cui la riga proviene, nel foglio principale del file.
        ///
        /// <para>
        /// <b>È l'unico identificatore affidabile della riga.</b> Treno, Loco e Avaria non bastano:
        /// nei file reali esistono righe con stesso treno e stessa loco (ETR1000 31/831 compare due
        /// volte, con richieste diverse). Vale 0 se la riga arriva dal percorso di ripiego ClosedXML,
        /// e in quel caso l'archiviazione viene rifiutata invece di procedere alla cieca.
        /// </para>
        /// </summary>
        public int SourceRowNumber { get; set; }

        /// <summary>
        /// Valore <b>grezzo</b> della colonna TRENO così com'è scritto nel foglio, prima di qualunque
        /// normalizzazione per la visualizzazione.
        ///
        /// <para>
        /// <b>Non coincide sempre con <see cref="Treno"/>, ed è il motivo per cui esiste.</b> Per la
        /// flotta 1000 il ViewModel sostituisce un "ETR1000" generico con il numero di treno reale
        /// risolto dal database tramite la loco (es. il file contiene "ETR1000" e a video compare
        /// "31"). La guardia che, prima di scrivere, verifica che la riga nel file sia ancora quella
        /// selezionata deve confrontare grezzo con grezzo: confrontare il valore mostrato a video con
        /// quello del foglio faceva fallire ogni archiviazione ETR1000 con un falso "la riga non
        /// corrisponde più".
        /// </para>
        /// </summary>
        public string SourceTreno { get; set; } = string.Empty;

        /// <summary>Valore grezzo della colonna LOCO nel foglio. Vedi <see cref="SourceTreno"/>.</summary>
        public string SourceLoco { get; set; } = string.Empty;

        /// <summary>
        /// Identificatore di flotta ("500", "700", "1000") a cui la riga appartiene: seleziona la
        /// configurazione di percorsi e il prefisso del nome file in fase di archiviazione.
        /// </summary>
        public string FleetIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Vero quando la riga ha tutto ciò che serve per essere archiviata. Le righe lette dal
        /// percorso di ripiego non lo sono: meglio un pulsante che rifiuta con un messaggio chiaro
        /// che un'archiviazione applicata alla riga sbagliata.
        /// </summary>
        public bool PuoEssereArchiviata =>
            !string.IsNullOrWhiteSpace(SourceFilePath) && SourceRowNumber > 0;
    }
}
