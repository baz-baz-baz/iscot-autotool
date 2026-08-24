using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// Disegna il rapportino di turno in un PDF vettoriale che riproduce il template Excel
    /// "rapportino di turno.xlsx" (fogli ETR500 / ETR700 / ETR1000, area di stampa <c>A1:I34</c>).
    ///
    /// <para>
    /// <b>Grafica vettoriale, non una fotografia della finestra.</b> La prima versione del modulo
    /// catturava la vista WPF con <c>RenderTargetBitmap</c>; qui il PDF è composto da rettangoli e
    /// testo a partire da un <see cref="RapportinoSnapshot"/>. Le conseguenze pratiche: nessuna
    /// bitmap nella Large Object Heap (criticità E di PROJECT_MEMORY.md §6.4), nessun vincolo sulla
    /// virtualizzazione delle griglie, nessuna modifica alla UI durante l'esportazione — e quindi
    /// nessuna possibilità di riprodurre lo sfarfallio inseguito in §6.1-undecies. In più il file
    /// pesa pochi KB invece di alcuni MB, ed è nitido a qualunque ingrandimento.
    /// </para>
    ///
    /// <para>
    /// <b>Impaginazione.</b> Il template Excel stampa con <c>fitToPage</c> al 63% su A4 orizzontale,
    /// centrato: qui si fa lo stesso: il contenuto è disegnato in coordinate "naturali" e una singola
    /// trasformazione di scala lo fa entrare in <b>una sola pagina</b>, centrata. Le proporzioni delle
    /// 9 colonne sono quelle reali del foglio Excel (<see cref="LarghezzeColonneExcel"/>).
    /// </para>
    /// </summary>
    public static class PassaggioConsegnePdfExporter
    {
        /// <summary>
        /// Larghezze delle colonne A..I lette dal template Excel. Sono in "unità larghezza Excel":
        /// conta solo il loro rapporto, che viene riscalato su <see cref="LarghezzaNaturale"/>.
        /// </summary>
        private static readonly double[] LarghezzeColonneExcel =
        [
            16.109375,      // A — N°
            16.109375,      // B
            20.5546875,     // C
            20.5546875,     // D
            20.5546875,     // E
            20.5546875,     // F
            18.77734375,    // G
            17.6640625,     // H
            20.77734375     // I
        ];

        // Coordinate "naturali": il disegno avviene qui dentro, poi una sola ScaleTransform porta
        // tutto dentro la pagina. Cambiare questi valori cambia le proporzioni, non la leggibilità:
        // la scala finale si adatta di conseguenza.
        private const double LarghezzaNaturale = 900;

        private const double HRigaTitolo = 34;      // riga 1 Excel: logo + RAPPORTINO TURNO + DATA
        private const double HRigaOperatore = 20;   // riga 2 Excel: NOME / COGNOME / ORA-INIZIO-FINE
        private const double HTitoloSezione = 26;   // righe 3, 16, 23 Excel
        private const double HSottotitolo = 20;     // riga 4 Excel
        private const double HIntestazione = 32;    // righe 5, 17, 24 Excel
        private const double HRigaDati = 16;        // righe dati
        private const double SpazioFraSezioni = 10;

        private const double MargineOrizzontale = 20;
        private const double MargineVerticale = 24;

        private const string NomeFont = "Arial";
        private const string LogoResourceName = "PassaggioConsegne.Logo.png";

        private static readonly XSolidBrush BrushTesto = new(XColors.Black);
        private static readonly XSolidBrush BrushSfondoSezione = new(XColor.FromArgb(0xD9, 0xD9, 0xD9));
        private static readonly XSolidBrush BrushSfondoIntestazione = new(XColor.FromArgb(0xF2, 0xF2, 0xF2));
        private static readonly XPen PennaBordo = new(XColors.Black, 0.6);

        private static bool _fontInizializzati;

        /// <summary>
        /// Genera il PDF del rapportino e ne restituisce il percorso.
        ///
        /// <para>
        /// Non tocca alcun oggetto WPF: riceve solo lo snapshot, quindi può essere invocato da un
        /// thread di background mentre l'utente continua a lavorare sulla finestra.
        /// </para>
        /// </summary>
        /// <param name="rapportino">Fotografia del rapportino da stampare.</param>
        /// <param name="cartellaDestinazione">
        /// Cartella di destinazione; se <c>null</c> si usa una sottocartella della cartella temporanea
        /// dell'utente, come faceva la versione precedente del modulo.
        /// </param>
        public static string ExportToPdf(RapportinoSnapshot rapportino, string? cartellaDestinazione = null)
        {
            ArgumentNullException.ThrowIfNull(rapportino);
            InizializzaFont();

            string cartella = cartellaDestinazione
                ?? Path.Combine(Path.GetTempPath(), "PersonalAutomationTool", "PassaggioConsegne");
            Directory.CreateDirectory(cartella);

            string percorso = Path.Combine(cartella, ComponiNomeFile(rapportino));

            // Il logo è letto in memoria e tenuto aperto fino a document.Save(): PdfSharp legge i byte
            // dell'immagine al momento del salvataggio, non a quello di DrawImage (stessa ragione per
            // cui il MemoryStream della vecchia versione andava chiuso solo nel finally, §4.13).
            MemoryStream? streamLogo = CaricaLogoInMemoria();
            try
            {
                using var document = new PdfDocument();
                document.Info.Title = $"Passaggio Consegne {rapportino.TipoTreno} - {rapportino.Data}";
                document.Info.Subject = rapportino.Sottotitolo;

                PdfPage page = document.AddPage();
                page.Size = PageSize.A4;
                page.Orientation = PageOrientation.Landscape;

                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                {
                    double altezzaNaturale = CalcolaAltezzaNaturale(rapportino);
                    double scala = CalcolaScala(altezzaNaturale, page.Width.Point, page.Height.Point);

                    double larghezzaFinale = LarghezzaNaturale * scala;
                    double altezzaFinale = altezzaNaturale * scala;

                    gfx.TranslateTransform(
                        (page.Width.Point - larghezzaFinale) / 2,   // centrato in orizzontale
                        Math.Max(MargineVerticale, (page.Height.Point - altezzaFinale) / 2));
                    gfx.ScaleTransform(scala, scala);

                    Disegna(gfx, rapportino, streamLogo);
                }

                document.Save(percorso);
                return percorso;
            }
            finally
            {
                streamLogo?.Dispose();
            }
        }

        /// <summary>
        /// Abilita in PDFsharp la risoluzione dei font dal catalogo di Windows. Senza questa riga
        /// <c>XFont</c> non trova alcun tipo di carattere e il disegno del testo fallisce: PDFsharp 6
        /// non presume più una piattaforma. L'applicazione è Windows-only (WPF), quindi non serve un
        /// <c>IFontResolver</c> personalizzato.
        /// </summary>
        private static void InizializzaFont()
        {
            if (_fontInizializzati) return;
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            _fontInizializzati = true;
        }

        private static string ComponiNomeFile(RapportinoSnapshot r)
        {
            string grezzo = $"Passaggio Consegne IMC AV Milano {r.TipoTreno} {r.Data.Replace('/', '-')}.pdf";
            return string.Concat(grezzo.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        }

        /// <summary>
        /// Altezza del rapportino in coordinate naturali, prima della riduzione in scala. Cresce con
        /// il numero di righe delle tre tabelle. <c>internal</c> per essere verificabile insieme a
        /// <see cref="CalcolaScala"/>.
        /// </summary>
        internal static double CalcolaAltezzaNaturale(RapportinoSnapshot r) =>
            HRigaTitolo + HRigaOperatore + SpazioFraSezioni
            + HTitoloSezione + HSottotitolo + HIntestazione + (r.Movimenti.Count * HRigaDati) + SpazioFraSezioni
            + HTitoloSezione + HIntestazione + (r.Interventi.Count * HRigaDati) + SpazioFraSezioni
            + HTitoloSezione + HIntestazione + (r.InterventiNonSvolti.Count * HRigaDati);

        /// <summary>
        /// Fattore di riduzione che fa entrare il rapportino in una sola pagina, come fa Excel con
        /// <c>fitToPage</c>. Non supera mai 1: un rapportino corto resta alla dimensione naturale
        /// invece di essere ingrandito fino a riempire il foglio.
        ///
        /// <para>
        /// <b>È qui che si decide se il contenuto sta nella pagina, e per questo è verificabile.</b>
        /// PDFsharp non impagina da solo: disegnare oltre il bordo non produce una seconda pagina, il
        /// contenuto viene semplicemente tagliato. Contare le pagine del PDF non direbbe quindi nulla
        /// sulla correttezza dell'impaginazione — l'invariante vera è che larghezza e altezza in scala
        /// stiano entro i margini, ed è quella che i test asseriscono.
        /// </para>
        /// </summary>
        internal static double CalcolaScala(double altezzaNaturale, double larghezzaPagina, double altezzaPagina)
        {
            double larghezzaDisponibile = larghezzaPagina - (2 * MargineOrizzontale);
            double altezzaDisponibile = altezzaPagina - (2 * MargineVerticale);

            return Math.Min(
                Math.Min(larghezzaDisponibile / LarghezzaNaturale, altezzaDisponibile / altezzaNaturale),
                1.0);
        }

        /// <summary>Ingombro del rapportino in punti, una volta ridotto in scala per la pagina data.</summary>
        internal static (double Larghezza, double Altezza) CalcolaIngombro(
            RapportinoSnapshot r, double larghezzaPagina, double altezzaPagina)
        {
            double altezzaNaturale = CalcolaAltezzaNaturale(r);
            double scala = CalcolaScala(altezzaNaturale, larghezzaPagina, altezzaPagina);
            return (LarghezzaNaturale * scala, altezzaNaturale * scala);
        }

        // ------------------------------------------------------------------
        // Disegno
        // ------------------------------------------------------------------

        private static void Disegna(XGraphics gfx, RapportinoSnapshot r, Stream? logo)
        {
            var fontTitolo = new XFont(NomeFont, 17, XFontStyleEx.Bold);
            var fontSezione = new XFont(NomeFont, 11, XFontStyleEx.Bold);
            var fontSottotitolo = new XFont(NomeFont, 9, XFontStyleEx.BoldItalic);
            var fontIntestazione = new XFont(NomeFont, 8.5, XFontStyleEx.Bold);
            var fontEtichetta = new XFont(NomeFont, 9, XFontStyleEx.Bold);
            var fontDati = new XFont(NomeFont, 9, XFontStyleEx.Regular);

            double y = 0;

            // --- Riga 1: logo (col. A), "RAPPORTINO TURNO" (B:G), "DATA" (H), valore (I) ---
            if (logo != null) DisegnaLogo(gfx, logo, new XRect(X(0), y + 3, W(0, 0), HRigaTitolo - 6));

            Cella(gfx, Rect(1, 6, y, HRigaTitolo), "RAPPORTINO TURNO", fontTitolo, XStringAlignment.Center, bordo: false);
            Cella(gfx, Rect(7, 7, y, HRigaTitolo), "DATA", fontEtichetta, XStringAlignment.Center, sfondo: BrushSfondoIntestazione);
            Cella(gfx, Rect(8, 8, y, HRigaTitolo), r.Data, fontDati, XStringAlignment.Center);
            y += HRigaTitolo;

            // --- Riga 2: NOME (A) | valore (B:C) | COGNOME (D) | valore (E:F) | ORA-INIZIO/FINE (G) | inizio (H) | fine (I) ---
            Cella(gfx, Rect(0, 0, y, HRigaOperatore), "NOME", fontEtichetta, XStringAlignment.Center, sfondo: BrushSfondoIntestazione);
            Cella(gfx, Rect(1, 2, y, HRigaOperatore), r.Nome, fontDati, XStringAlignment.Near);
            Cella(gfx, Rect(3, 3, y, HRigaOperatore), "COGNOME", fontEtichetta, XStringAlignment.Center, sfondo: BrushSfondoIntestazione);
            Cella(gfx, Rect(4, 5, y, HRigaOperatore), r.Cognome, fontDati, XStringAlignment.Near);
            Cella(gfx, Rect(6, 6, y, HRigaOperatore), "ORA-INIZIO/FINE", fontIntestazione, XStringAlignment.Center, sfondo: BrushSfondoIntestazione);
            Cella(gfx, Rect(7, 7, y, HRigaOperatore), r.OraInizio, fontDati, XStringAlignment.Center);
            Cella(gfx, Rect(8, 8, y, HRigaOperatore), r.OraFine, fontDati, XStringAlignment.Center);
            y += HRigaOperatore + SpazioFraSezioni;

            // --- Tabella 1: attività richieste da ingegneria (movimenti treni) ---
            Cella(gfx, Rect(0, 8, y, HTitoloSezione), "ATTIVITA' RICHIESTE DA INGEGNERIA HR STS",
                fontSezione, XStringAlignment.Center, sfondo: BrushSfondoSezione);
            y += HTitoloSezione;

            Cella(gfx, Rect(0, 8, y, HSottotitolo), r.Sottotitolo, fontSottotitolo, XStringAlignment.Center);
            y += HSottotitolo;

            // A | B:C | D:E | F | G | H | I
            int[][] colonneMovimenti = [[0, 0], [1, 2], [3, 4], [5, 5], [6, 6], [7, 7], [8, 8]];
            string[] intestazioniMovimenti =
                ["N°", "TRENO", "LOCO", "DATA INGRESSO", "ORA INGRESSO", "DATA USCITA", "ORA USCITA"];
            RigaIntestazione(gfx, colonneMovimenti, intestazioniMovimenti, y, fontIntestazione);
            y += HIntestazione;

            foreach (var m in r.Movimenti)
            {
                string[] valori =
                    [m.Numero.ToString(), m.Treno, m.Loco, m.DataIngresso, m.OraIngresso, m.DataUscita, m.OraUscita];
                RigaDati(gfx, colonneMovimenti, valori, y, fontDati, colonnaTestoASinistra: 1);
                y += HRigaDati;
            }
            y += SpazioFraSezioni;

            // --- Tabella 2: dettaglio interventi ---
            Cella(gfx, Rect(0, 8, y, HTitoloSezione), "DETTAGLIO INTERVENTI (CORRETIVA, PREVENTIVA, RICHIESTE INGEGNERIA)",
                fontSezione, XStringAlignment.Center, sfondo: BrushSfondoSezione);
            y += HTitoloSezione;

            // A | B:D | E | F | G | H | I
            int[][] colonneInterventi = [[0, 0], [1, 3], [4, 4], [5, 5], [6, 6], [7, 7], [8, 8]];
            string[] intestazioniInterventi =
            [
                "TRENO-LOCO", "DESCRIZIONE", "COMPILAZIONE ODL", "CHIUSURA TICKET MAXIMO+ EMAIL",
                "COMP.REPORT INTERVENTI", "EMAIL AD INGEGNERIA", "AGGIORNARE FILE VERIFICHE"
            ];
            RigaIntestazione(gfx, colonneInterventi, intestazioniInterventi, y, fontIntestazione);
            y += HIntestazione;

            foreach (var i in r.Interventi)
            {
                string[] valori =
                [
                    i.TrenoLoco, i.Descrizione, i.CompilazioneOdl, i.ChiusuraTicket,
                    i.CompReport, i.EmailIngegneria, i.AggiornareVerifiche
                ];
                RigaDati(gfx, colonneInterventi, valori, y, fontDati, colonnaTestoASinistra: 1);
                y += HRigaDati;
            }
            y += SpazioFraSezioni;

            // --- Tabella 3: interventi non svolti ---
            Cella(gfx, Rect(0, 8, y, HTitoloSezione), "INTERVENTI RICHIESTI DA INGEGNERIA NON SVOLTI SU TRENI IN CANTIERE",
                fontSezione, XStringAlignment.Center, sfondo: BrushSfondoSezione);
            y += HTitoloSezione;

            // A | B | C:E | F | G | H | I
            int[][] colonneNonSvolti = [[0, 0], [1, 1], [2, 4], [5, 5], [6, 6], [7, 7], [8, 8]];
            string[] intestazioniNonSvolti =
            [
                "N°", "TRENO-LOCO", "MOTIVAZIONE", "ORA RICHIESTA", "REFERENTE TRENITALIA o HR",
                "INVIATA EMAIL AD INGEGNERIA", "PASSAGGIO DI CONSEGNA"
            ];
            RigaIntestazione(gfx, colonneNonSvolti, intestazioniNonSvolti, y, fontIntestazione);
            y += HIntestazione;

            foreach (var n in r.InterventiNonSvolti)
            {
                string[] valori =
                [
                    n.Numero.ToString(), n.TrenoLoco, n.Motivazione, n.OraRichiesta,
                    n.Referente, n.InviataEmail, n.PassaggioConsegna
                ];
                RigaDati(gfx, colonneNonSvolti, valori, y, fontDati, colonnaTestoASinistra: 2);
                y += HRigaDati;
            }
        }

        private static void DisegnaLogo(XGraphics gfx, Stream logo, XRect area)
        {
            try
            {
                logo.Position = 0;
                using XImage immagine = XImage.FromStream(logo);

                // Mantiene le proporzioni originali dentro l'area disponibile, allineato a sinistra.
                double scala = Math.Min(area.Width / immagine.PixelWidth, area.Height / immagine.PixelHeight);
                double larghezza = immagine.PixelWidth * scala;
                double altezza = immagine.PixelHeight * scala;
                gfx.DrawImage(immagine, area.Left + 4, area.Top + ((area.Height - altezza) / 2), larghezza, altezza);
            }
            catch (Exception ex)
            {
                // Il logo è decorativo: se manca o non è leggibile il rapportino resta valido.
                System.Diagnostics.Debug.WriteLine($"Logo non disegnato: {ex.Message}");
            }
        }

        private static void RigaIntestazione(XGraphics gfx, int[][] colonne, string[] testi, double y, XFont font)
        {
            for (int i = 0; i < colonne.Length; i++)
            {
                Cella(gfx, Rect(colonne[i][0], colonne[i][1], y, HIntestazione), testi[i], font,
                    XStringAlignment.Center, sfondo: BrushSfondoIntestazione);
            }
        }

        /// <param name="colonnaTestoASinistra">
        /// Indice della colonna (nell'array <paramref name="colonne"/>) da allineare a sinistra perché
        /// contiene testo libero e non un valore breve; le altre restano centrate come nel template.
        /// </param>
        private static void RigaDati(XGraphics gfx, int[][] colonne, string[] valori, double y, XFont font, int colonnaTestoASinistra)
        {
            for (int i = 0; i < colonne.Length; i++)
            {
                var allineamento = i == colonnaTestoASinistra ? XStringAlignment.Near : XStringAlignment.Center;
                Cella(gfx, Rect(colonne[i][0], colonne[i][1], y, HRigaDati), valori[i], font, allineamento);
            }
        }

        // ------------------------------------------------------------------
        // Primitive di disegno e geometria
        // ------------------------------------------------------------------

        /// <summary>Ascissa naturale del bordo sinistro della colonna <paramref name="colonna"/> (0 = A).</summary>
        private static double X(int colonna)
        {
            double totale = LarghezzeColonneExcel.Sum();
            double somma = 0;
            for (int i = 0; i < colonna; i++) somma += LarghezzeColonneExcel[i];
            return somma / totale * LarghezzaNaturale;
        }

        /// <summary>Larghezza naturale dell'intervallo di colonne [<paramref name="da"/>..<paramref name="a"/>].</summary>
        private static double W(int da, int a) => X(a + 1) - X(da);

        private static XRect Rect(int colonnaDa, int colonnaA, double y, double altezza) =>
            new(X(colonnaDa), y, W(colonnaDa, colonnaA), altezza);

        private static void Cella(XGraphics gfx, XRect rect, string? testo, XFont font,
            XStringAlignment allineamento, XBrush? sfondo = null, bool bordo = true)
        {
            if (sfondo != null) gfx.DrawRectangle(sfondo, rect);
            if (bordo) gfx.DrawRectangle(PennaBordo, rect);
            if (!string.IsNullOrWhiteSpace(testo)) DisegnaTesto(gfx, testo, font, rect, allineamento);
        }

        /// <summary>
        /// Scrive il testo dentro la cella mandandolo a capo sulle parole e, se ancora non entra,
        /// riducendo progressivamente il corpo del carattere. Serve soprattutto alle intestazioni
        /// lunghe del template ("CHIUSURA TICKET MAXIMO+ EMAIL", "REFERENTE TRENITALIA o HR"), che in
        /// Excel occupano tre righe in una cella alta 34 punti.
        /// </summary>
        private static void DisegnaTesto(XGraphics gfx, string testo, XFont font, XRect rect, XStringAlignment allineamento)
        {
            const double padding = 2;
            double larghezzaUtile = rect.Width - (2 * padding);
            if (larghezzaUtile <= 0) return;

            XFont fontCorrente = font;
            List<string> righe = SpezzaInRighe(gfx, testo, fontCorrente, larghezzaUtile);

            // Al massimo 3 riduzioni: sotto il 70% del corpo originale il testo non sarebbe comunque
            // leggibile, e a quel punto è preferibile lasciarlo debordare che renderlo invisibile.
            for (int tentativo = 0; tentativo < 3; tentativo++)
            {
                if (righe.Count * fontCorrente.GetHeight() <= rect.Height) break;
                fontCorrente = new XFont(NomeFont, fontCorrente.Size * 0.88, font.Style);
                righe = SpezzaInRighe(gfx, testo, fontCorrente, larghezzaUtile);
            }

            double altezzaRiga = fontCorrente.GetHeight();
            double altezzaTotale = righe.Count * altezzaRiga;
            double yCorrente = rect.Top + Math.Max(0, (rect.Height - altezzaTotale) / 2);

            var formato = new XStringFormat { Alignment = allineamento, LineAlignment = XLineAlignment.Center };
            foreach (string riga in righe)
            {
                gfx.DrawString(riga, fontCorrente, BrushTesto,
                    new XRect(rect.Left + padding, yCorrente, larghezzaUtile, altezzaRiga), formato);
                yCorrente += altezzaRiga;
            }
        }

        private static List<string> SpezzaInRighe(XGraphics gfx, string testo, XFont font, double larghezzaMassima)
        {
            var righe = new List<string>();
            foreach (string paragrafo in testo.Split('\n'))
            {
                string[] parole = paragrafo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parole.Length == 0)
                {
                    righe.Add(string.Empty);
                    continue;
                }

                string corrente = parole[0];
                for (int i = 1; i < parole.Length; i++)
                {
                    string candidata = corrente + " " + parole[i];
                    if (gfx.MeasureString(candidata, font).Width <= larghezzaMassima)
                    {
                        corrente = candidata;
                    }
                    else
                    {
                        righe.Add(corrente);
                        corrente = parole[i];
                    }
                }
                righe.Add(corrente);
            }
            return righe;
        }

        private static MemoryStream? CaricaLogoInMemoria()
        {
            try
            {
                using Stream? risorsa = typeof(PassaggioConsegnePdfExporter).Assembly
                    .GetManifestResourceStream(LogoResourceName);
                if (risorsa == null) return null;

                var memoria = new MemoryStream();
                risorsa.CopyTo(memoria);
                memoria.Position = 0;
                return memoria;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logo non caricato: {ex.Message}");
                return null;
            }
        }
    }
}
