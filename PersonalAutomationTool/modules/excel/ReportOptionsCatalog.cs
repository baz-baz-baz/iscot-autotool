using System;
using System.Collections.Generic;

namespace PersonalAutomationTool.Modules.Excel
{
    /// <summary>
    /// Le opzioni ammesse nelle ComboBox del Report Interventi, per flotta e per campo.
    ///
    /// <para>
    /// <b>Perché questa classe esiste.</b> Le opzioni venivano ricavate dalle convalide dati del file
    /// Excel unendo <b>tutte</b> quelle che toccano una colonna. Su un report reale quelle convalide
    /// sono decine, stratificate negli anni su intervalli di righe diversi: la colonna "Materiale
    /// Fornito" del report ETR1000 ne ha cinque — <c>"ASTS,HITACHI"</c>, <c>"ASTS,ANSALDOBREDA,HITACHI"</c>,
    /// <c>"HITACHIRail,HR-STS"</c>, <c>"HR-STS, HR"</c> e quella in uso oggi, <c>"HR, STS, TI"</c> —
    /// e l'unione produceva un menu con otto voci di cui cinque non più valide. Stessa cosa per
    /// "Scarico Dati Locale" (<c>"Si,No"</c> storica + <c>"SI, NO"</c> attuale, che un confronto
    /// sensibile alle maiuscole teneva entrambe) e per "Versione SW Presente".
    /// </para>
    ///
    /// <para>
    /// <b>I valori non sono inventati qui.</b> Ognuno è stato verificato contro la convalida dati
    /// <b>realmente applicata alle righe in uso</b> del Report Interventi ETR1000 aziendale, colonna
    /// per colonna (vedi PROJECT_MEMORY.md §6.1-vicies-quinquies per il confronto completo, con gli
    /// intervalli di riga di ciascuna convalida). È questo che soddisfa il vincolo di scrivere solo
    /// valori che Excel accetta: se un valore qui dentro divergesse da quello del foglio, la cella
    /// scritta risulterebbe non valida all'apertura del report.
    /// </para>
    ///
    /// <para>
    /// <b>ETR1000 ed ETR1001FH condividono queste liste</b>, perché condividono lo stesso Report
    /// Interventi e la stessa voce di ComboBox <c>"ETR1000 / 1000FH"</c> (§5.3-bis). Non esiste quindi
    /// un filtro che scatti passando da un rotabile all'altro: i due rotabili sono due voci della
    /// lista "Rotabile", non due contesti con opzioni diverse.
    /// </para>
    /// </summary>
    public static class ReportOptionsCatalog
    {
        /// <summary>
        /// L'etichetta della ComboBox di flotta che seleziona il Report Interventi condiviso da
        /// ETR1000 ed ETR1001FH. È l'etichetta, non il tipo treno: i due rotabili stanno dentro
        /// questo report (§5.3-bis).
        /// </summary>
        public const string Etr1000Report = "ETR1000 / 1000FH";

        private static readonly string[] Siti =
            ["Pistoia", "Napoli Gianturco", "Milano Martesana", "Roma S.Lorenzo", "Piacenza", "Firenze"];

        private static readonly string[] Clienti = ["Hitachi", "Trenitalia"];

        private static readonly string[] Rotabili = ["ETR1000", "ETR1001FH"];

        private static readonly string[] TipologieIntervento =
        [
            "Mis", "Extragaranzia", "Upgrade", "Man Programmata", "Man Predittiva",
            "Controlli Remoto", "Nulla Riscontrato", "Correttiva con sostit", "Correttiva senza sostit"
        ];

        private static readonly string[] CategorieAvaria =
        [
            "Oscuram Monitor", "Verifica", "Catena Radio", "Catena Vigilante", "Catena RSDD",
            "JRU DIS", "Data Logger", "RIML", "Odometria", "Perdita Rid. SSB", "Altro"
        ];

        private static readonly string[] ScaricoDatiLocale = ["SI", "NO"];

        private static readonly string[] MaterialeFornito = ["HR", "STS", "TI"];

        private static readonly string[] VersioniSoftware =
            ["04.01 HR", "04.03 HR", "01.01.0000 Elo BL3", "02.02.0006 Elo BL3", "02.02.0007 Elo BL3"];

        /// <summary>
        /// I componenti LRU ammessi per "Descrizione LRU" (colonne R e U, che condividono la stessa
        /// convalida). Estratti dall'intervallo <c>$AE$4:$AE$106</c> del report reale — non da una
        /// formula CSV in linea come gli altri campi, ma da un intervallo di celle, la stessa forma
        /// già gestita da <c>ExcelViewModel</c> per "Rotabile" e "Versione SW Presente".
        ///
        /// <para>
        /// <b>Gli spazi iniziali e doppi qui dentro sono reali, non refusi di trascrizione.</b> Sette
        /// voci (es. <c>" CPUE ALM N B61C.0100003"</c>, <c>" Edor 109A.0101709"</c>) hanno uno spazio
        /// iniziale nel file, marcato esplicitamente con <c>xml:space="preserve"</c> nell'XML — la
        /// prova che Excel stesso lo considera significativo, non normalizzabile. Altre (es.
        /// <c>"SUONERIA CAB.A  B64A.000002"</c>) hanno uno spazio doppio interno. L'intera lista è
        /// stata generata da uno script che legge <c>$AE$4:$AE$106</c> del file reale ed emette il
        /// letterale C#, non trascritta a mano: il rischio di un errore di battitura su 103 voci,
        /// ciascuna delle quali deve corrispondere byte per byte alla convalida per essere accettata
        /// da Excel, non valeva la pena di correre.
        /// </para>
        /// </summary>
        private static readonly string[] ComponentiLru =
        [
            @"ARMADIO ALA B61A.000014",
            @"BUS ALA B61D.000001",
            @" CPUE ALM N B61C.0100003",
            @" CPUE ALM R B61C.0100004",
            @" CPU2 TMM N FM9136201500",
            @"CPU2 TMM R FM9136201501",
            @" CPU2 RIM N FM9136202600",
            @" CPU2 RIM R FM9136202601",
            @"RIME N B61B.000011",
            @"RIME R B61B.000011",
            @"RIML N B61D.000023",
            @"RIML R B61D.000023",
            @"CPUE EVC N B61C.000003",
            @"CPUE EVC R B61C.000004",
            @" WDRS N B61D.000005",
            @" WDRS R B61D.000005",
            @" MULE N B61D.B16003",
            @" MULE R B61D.B16003",
            @"MULU TMM N B61D.000004",
            @" MULU TMM R B61D.000004",
            @"WDOU TMM N FM9136200550",
            @" WDOU TMM R FM9136200550",
            @"BACE N B61D.000006",
            @"BACE R B61D.000006",
            @"MVBV N 166A.A25002",
            @"MVBV R 166A.A25002",
            @"TACU TMM N FM9136202351",
            @"TACU TMM R FM9136202351",
            @"IDVI TMM N FM9136200650",
            @"IDVI TMM R FM9136200650",
            @"AL5V N B61B.000005",
            @"AL5V R B61B.000005",
            @"AL24V N B61B.000007",
            @"AL24V R B61B.000007",
            @"AUXT N FM9136202250",
            @"AUXT R FM9136202250",
            @"GRUPPO VENTOLE B64B.000001",
            @"BTM N B62A.000002",
            @"BTM R B62A.000002",
            @"SUONERIA CAB.A  B64A.000002",
            @"ANTENNA_RSDD  N FM9136300101",
            @"ANTENNA_RSDD  R FM9136300101",
            @"CAPTATORE CAB.A SX   2/561866",
            @"CAPTATORE CAB.A DX   2/561866",
            @"QUADRO DISTRIB. 24V  B64A.000033",
            @"DC/DC CONVERTER B64A.000023",
            @"GENERATORE TACHIM. 1",
            @"GENERATORE TACHIM. 2",
            @"GENERATORE TACHIM. 3",
            @"GENERATORE TACHIM. 4",
            @"DMI CAB. A-N B65A.000028",
            @"DMI CAB. A-R B65A.000028",
            @"PIASTRA PNEUMATICA N 2/563062",
            @"PIASTRA PNEUMATICA R 2/563062",
            @"GSM R  MOBILE TERMINAL  120I.000005",
            @"ANTENNA GSMR N 3238.000016",
            @"ANTENNA GSMR R 3238.000016",
            @"AMPLIFICATORE  CAB. A N B65A.000023",
            @"AMPLIFICATORE CAB. A R  B65A.000023",
            @"Data Logger 166A.000042",
            @"Bridge 3220.000564",
            @"Antenna rsdd con supporto B63A.000009",
            @"Duagon B60A.0100680",
            @"Deuta Elo N 3238.100072",
            @"Deuta Elo R 3238.100072",
            @"Elo ETCS B61A.0100034",
            @"Elo STPS N B67C.0100001",
            @"Elo STPS R B67C.0100001",
            @"Elo AUVH N B61D.0100035",
            @"Elo AUVH R B61D.0100035",
            @"Elo LCMD B61B.0100064",
            @"Elo SMIO 2 N B67C.0100006",
            @"Elo SMIO 2 R B67C.0100006",
            @"Elo SMIO 1 N B67C.0100006",
            @"Elo SMIO 1 R B67C.0100006",
            @"Elo TACU N B61C.0100014",
            @"Elo TACU R B61C.0100014",
            @"Elo BACE N B61C.0100015",
            @"Elo BACE R B61C.0100015",
            @"Elo CPUN N B61B.0100065",
            @"Elo CPUN R B61B.0100065",
            @"Elo Rack B61B.0100049",
            @"Elo Switch 1 3220.0100670",
            @"Elo Switch 2 3220.0100670",
            @" Edor 109A.0101709",
            @"Elo QD B64A.0100010",
            @"Elo suoneria DMI N B64A.000002",
            @"Elo suoneria DMI R B64A.000002",
            @"Elo  LTE\GPS 3238.0100120",
            @"Elo DC/DC CONVERTER 3209.0100214",
            @"Elo EPF Filter 3238.0100092",
            @"Elo BTM 2G REDUNDANDB62A.0100028",
            @"Elo RACK 3U-84TE BTM2G B62B.0100024",
            @"Elo BLPS BTM2G-N POWER SUPPLY 138D.0100001",
            @"Elo BLTX BTM2G-N BOARD ASSEMBLY B62A.0100011",
            @"Elo BLRX BTM2G-N MAIN MODULE B62A.0100003",
            @"Elo BLPS BTM2G-R POWER SUPPLY 138D.0100001",
            @"Elo BLTX BTM2G-R BOARD ASSEMBLY B62A.0100011",
            @"Elo BLRX BTM2G-R MAIN MODULE B62A.0100012",
            @"Elo ANTENNA_RSDD_N B63A.000012",
            @"Elo ANTENNA_RSDD_R B63A.000012",
            @"Elo SWITCH CS1 9001.0101301",
            @"Elo SWITCH CS2 9001.0101301",
        ];

        /// <summary>
        /// Le opzioni canoniche per il campo indicato, oppure <see langword="null"/> se per questa
        /// flotta e questo campo non esiste una lista vincolata — nel qual caso il chiamante lascia
        /// le opzioni che ha già ricavato dal file.
        /// </summary>
        /// <param name="selectedTrain">L'etichetta di flotta selezionata nel modulo EXCEL.</param>
        /// <param name="fieldName">L'intestazione di colonna, così come letta dalla riga 1 del report.</param>
        public static IReadOnlyList<string>? GetOptions(string? selectedTrain, string? fieldName)
        {
            if (!string.Equals(selectedTrain, Etr1000Report, StringComparison.Ordinal)) return null;
            if (string.IsNullOrWhiteSpace(fieldName)) return null;

            // "TECNICO Cliente" contiene "Cliente" ma non è il campo Cliente: va escluso prima di
            // qualunque altro confronto, altrimenti erediterebbe la lista dei clienti.
            if (Contains(fieldName, "TECNICO")) return null;

            if (Contains(fieldName, "Sito")) return Siti;
            if (Contains(fieldName, "Cliente")) return Clienti;
            if (Contains(fieldName, "Rotabile")) return Rotabili;
            if (Contains(fieldName, "Tipologia")) return TipologieIntervento;
            if (Contains(fieldName, "Categoria") && Contains(fieldName, "Avaria")) return CategorieAvaria;
            if (Contains(fieldName, "Scarico Dati")) return ScaricoDatiLocale;
            if (Contains(fieldName, "Materiale")) return MaterialeFornito;
            if (Contains(fieldName, "Versione SW")) return VersioniSoftware;
            if (Contains(fieldName, "Descrizione LRU")) return ComponentiLru;

            return null;
        }

        /// <summary>
        /// Vero per i campi il cui catalogo è un elenco lungo di componenti (oggi solo "Descrizione
        /// LRU", 103 voci) invece di una lista breve e chiusa come le altre otto di questa classe.
        /// Il chiamante lo usa per decidere se la ComboBox deve restare digitabile con ricerca
        /// testuale: renderle digitabili <b>tutte</b> vanificherebbe il vincolo "solo queste voci,
        /// senza opzioni extra" appena introdotto per Sito/Cliente/Rotabile/eccetera. "Descrizione
        /// LRU" è diversa per natura — un catalogo di ricambi, non una regola di business — ed è
        /// l'unico caso in cui la digitazione libera con autocompletamento ha senso.
        /// </summary>
        public static bool IsSearchableComponentField(string? fieldName) =>
            !string.IsNullOrWhiteSpace(fieldName) && Contains(fieldName, "Descrizione LRU");

        private static bool Contains(string text, string value) =>
            text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
