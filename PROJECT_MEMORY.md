# PROJECT_MEMORY.md — Personal Automation Tool (iscot-autotool)

> **Scopo di questo file.** Documento di passaggio di consegne verso qualsiasi sessione futura (umana o AI).
> Contiene tutto ciò che serve per lavorare sul progetto senza doverlo ri-esplorare da zero: architettura,
> vincoli, invarianti da non rompere e stato del lavoro svolto.
>
> **Ultimo aggiornamento:** 22 agosto 2026 — sessione di audit e ottimizzazione per hardware Windows datato,
> più secondo giro con le modifiche a comportamento visibile approvate dal committente (§4-bis).
> **Stato build alla chiusura sessione:** `dotnet build` → 0 errori, 0 warning.

---

## 1. Panoramica & Scopo

### Cos'è
Applicazione desktop **WPF (.NET 10, Windows-only)** a uso interno di un tecnico manutentore SSB
(Sotto Sistema di Bordo) che opera su flotte ferroviarie Hitachi Rail / Trenitalia presso l'**IMC AV Milano**.

Automatizza il lavoro burocratico ripetitivo che ruota attorno agli interventi di manutenzione:
creazione di cartelle con nomenclatura standard, rinomina di PDF, compilazione di report Excel,
generazione di email Outlook precompilate e produzione del rapportino di turno.

### Requisiti funzionali soddisfatti
| Necessità operativa | Modulo che la copre |
|---|---|
| Creare le cartelle LOG/DUMP con nome standard `SR{ticket} LOG {tipo} {loco} {sw} {data} {utente}` | **CARTELLE** |
| Vedere le manutenzioni in sospeso, rinominare ticket/date in blocco, zippare, spostare in rete, eliminare | **HOME** |
| Rinominare i PDF di intervento (FL / NdL / NC / ND) secondo convenzione | **PDF** |
| Generare email Outlook di chiusura ticket, log/dump e scadenze, per tipo treno | **EMAIL** (+ sotto-viste per treno) |
| Compilare e archiviare il "Report Interventi" Excel aziendale | **EXCEL** |
| Gestire l'anagrafica destinatari per treno/azione, con rubrica | **DESTINATARI MAIL** |
| Consultare/editare i database SQLite locali | **DATABASE** |
| Vedere le verifiche aperte estratte dai file Excel di flotta | **VERIFICHE** |
| Compilare ed esportare in PDF il rapportino di turno + email | **PASSAGGIO DI CONSEGNE** |

### Flotte gestite
`E404P` (ETR500), `ETR700`, `ETR1000`, `ETR1000 I-F` (Italia-Francia / bi-standard KVB),
`ETR1000FH` / `1000FH` (alias `ETR1001`), `ETR421`, `ETR521`, `ETR522`.

> ⚠️ La stessa flotta compare con **nomi diversi** a seconda del contesto (UI, nome cartella, chiave DB,
> chiave destinatari). Vedi §5 "Invarianti".

---

## 2. Architettura & Flusso Dati

### 2.1 Struttura fisica

```
iscot-autotool.sln
├── PersonalAutomationTool/          ← APPLICAZIONE (l'unica che conta)
│   ├── main/                        App.xaml(.cs), MainWindow.xaml(.cs)
│   ├── core/                        AppConfig, AppWatcher, RelayCommand,
│   │                                ViewModelBase, MouseWheelScrollBehavior, Converters/
│   ├── modules/                     un sottoalbero per modulo funzionale
│   │   ├── home/  cartelle/  pdf/  excel/  verifiche/  database/
│   │   ├── destinatari_mail/  passaggio_consegne/
│   │   └── email/               EmailService, EmailView, dialogs/, trains/
│   └── modules/database/*.db        train_software.db, emails.db (copiati in output)
├── TestClosedXML/                   ← scratch, NON parte dell'app
└── scratch/                         ← scratch, NON parte dell'app
```

**Nota:** `TestClosedXML/`, `scratch/`, `ep_test.cs`, `test.cs` sono residui di sperimentazione.
`TestClosedXML` è però referenziato nella `.sln` e i suoi `bin/obj` sono **committati nel repository**.

### 2.2 Pattern architetturale
Ibrido, **non uniforme** — è importante saperlo prima di intervenire:

| Modulo | Pattern usato |
|---|---|
| Home, Excel, Verifiche, PassaggioConsegne | **MVVM** (`ViewModelBase` + `RelayCommand`, DataContext creato in XAML) |
| Cartelle, PDF, Database, DestinatariMail, RubricaDialog, ChiusuraTicketDialog | **Code-behind** diretto, accesso ai controlli per nome |
| Email/trains (E404P, ETR700, ETR1000…) | Code-behind sottile che delega a `TrainViewHelper` |

`ExcelView` è l'unico che istanzia il proprio VM nel costruttore C# invece che in XAML.

### 2.3 Navigazione
`MainWindow` ha una navbar orizzontale e un `ContentControl` (`MainContentControl`, `x:FieldModifier="public"`).

- `MainWindow.NavigateTo<T>()` mantiene una **cache di istanze** (`Dictionary<Type, UserControl>`):
  ogni vista di primo livello viene costruita **una sola volta** e ne conserva lo stato.
- Le sotto-viste treno (`EmailView` → `E404PView`, `ETR700View`, …) e il ritorno indietro
  (`TrainViewHelper.NavigateBack()`) **bypassano la cache** e istanziano ogni volta un oggetto nuovo,
  scrivendo direttamente su `MainWindow.MainContentControl.Content`.

### 2.4 Il perno del sistema: la cartella `LOG & DUMP`

```
%USERPROFILE%\Desktop\LOG & DUMP\        ← AppConfig.LogAndDumpFolder (creata all'avvio)
└── {TIPO} {TRENO}\                      ← "cartella madre" (es. "ETR700 12")
    ├── SR1247654 LOG ETR700 117 04.02HR 300526 Todde\
    ├── SR1247654 DUMP ETR700 117 04.02HR 300526 Todde\
    ├── info_ticket.json                 ← cache dei dati inseriti nel dialog Chiusura Ticket
    ├── FL SR... .pdf / NdL ... .pdf / NC ... .pdf
    └── {scadenzaFrancia}.txt            ← opzionale, per le scadenze francesi
```

**Questa struttura è il database de-facto dell'applicazione.** Quasi ogni modulo la legge e ne fa
il *parsing dei nomi* per estrarre ticket, tipo treno, locomotore, versione SW, data (`ddMMyy`) e utente.

Il **flusso operativo tipico** è:

```
CARTELLE (crea)  →  [tecnico copia i log dal treno]  →  PDF (rinomina)
      ↓                                                       ↓
    HOME (zip, sposta in rete, aggiorna ticket/data)    EMAIL (chiusura ticket)
      ↓                                                       ↓
    EXCEL (report interventi)                          info_ticket.json
                                                              ↓
                                            riletto da EXCEL per l'autocompilazione
```

### 2.5 Propagazione degli aggiornamenti

**`Core.AppWatcher`** — singolo `FileSystemWatcher` statico su `LOG & DUMP`, `IncludeSubdirectories = true`,
debounce 300 ms, poi `Dispatcher.InvokeAsync` dell'evento statico `OnLogDumpFolderChanged`.

Sottoscrittori: `HomeViewModel`, `ExcelViewModel`, `CartelleView` (Loaded/Unloaded), `PdfView` (Loaded/Unloaded).

**`VerificheViewModel`** — canale separato e indipendente:
- 3 `FileSystemWatcher` sulle cartelle Hitachi (`%USERPROFILE%\Hitachi Group\…`), debounce 500 ms;
- **più** un `DispatcherTimer` da 5 s che riscandaglia 5 alberi di cartelle confrontando le date di modifica;
- espone `static VerificheViewModel? Instance` e l'evento statico `OnVerificheDataUpdated`,
  a cui si aggancia `PassaggioConsegneViewModel` per auto-compilare la tabella movimenti.

### 2.6 Persistenza

| Store | Percorso | Contenuto |
|---|---|---|
| `train_software.db` | `{BaseDirectory}\modules\database\` | tabella `flotte(tipo, treno, loco, software)` — mappa loco → treno e versione SW |
| `emails.db` | idem | `indirizzi_email(id, nome, email, categoria)` — rubrica |
| `destinatari.json` | `{BaseDirectory}\` | destinatari To/Cc per **treno × azione**; auto-generato al primo avvio |
| `shortcuts.json` | `{BaseDirectory}\` | macro-testi "Nulla Riscontrato", "SIM-GIT", … per treno |
| `data\passaggio_consegne.json` | `{BaseDirectory}\data\` | i 3 rapportini di turno (ETR700/1000/500) |
| `info_ticket.json` | dentro ogni cartella madre di `LOG & DUMP` | avvisi/avarie/interventi per locomotore |

`DatabaseManager` incapsula SQLite (`Microsoft.Data.Sqlite`), restituisce `DataTable`, e serializza
**tutti** gli accessi con un `lock` **statico** condiviso fra tutte le istanze.

### 2.7 Integrazioni esterne (COM, late-bound via `dynamic`)

| Integrazione | Dove | Uso |
|---|---|---|
| **Outlook** (`Outlook.Application`) | `EmailService`, `PassaggioConsegneEmailService` | crea `MailItem`, forza l'`Inspector` per ottenere la firma, inserisce il corpo HTML **prima** della firma, allega i PDF, `Display(false)` |
| **Excel** (`Excel.Application`) | `ExcelViewModel.ExecuteScriviReport` | scrive la nuova riga del report **in modo nativo** per non alterare formattazione/struttura |
| **ClosedXML** | Excel, Verifiche | sola **lettura** (intestazioni, data validation, ultima riga compilata, parsing verifiche) |
| **PdfSharp** | PdfView, PassaggioConsegnePdfExporter | conteggio pagine PDF; render del rapportino in PDF |

> Excel è usato **due volte con due tecnologie diverse nello stesso comando**: ClosedXML per *leggere*
> l'ultima riga (file rilasciato subito), poi Interop per *scrivere*. È voluto: ClosedXML in scrittura
> riscriverebbe il file perdendo formattazione e convalide.

### 2.8 Dipendenze NuGet
`ClosedXML 0.105.0` · `EPPlus 4.5.3.3` (referenziato, **non usato** nell'app) · `MaterialDesignThemes 4.9.0`
· `Microsoft.Data.Sqlite 10.0.3` · `PDFsharp 6.2.4` · `SQLitePCLRaw.bundle_e_sqlite3 3.0.3`

---

## 3. Requisiti di Sistema & Vincoli Legacy

### Ambiente di destinazione
- **Windows** (svilupp. su Windows 11; il target reale include macchine d'officina datate).
- **.NET 10 Desktop Runtime** con supporto WPF. SDK di sviluppo verificato: **10.0.301**.
- **Microsoft Office (Outlook + Excel) installato**: senza Excel il comando "Scrivi report" mostra un
  errore esplicito; senza Outlook la generazione email si interrompe con avviso.
- **OneDrive / Hitachi Group** sincronizzato sotto `%USERPROFILE%\Hitachi Group\…`.
- Avvio: `Avvia.bat` (esegue `dotnet run`), oppure l'eseguibile compilato.

### Vincoli imposti dall'hardware datato — da tenere sempre presenti
1. **Nessun I/O sincrono sul thread UI.** Le cartelle di lavoro sono su OneDrive: una `Directory.Exists`
   su un percorso non sincronizzato può bloccare per secondi. Ogni scansione ricorsiva va su `Task.Run`.
2. **La RAM è la risorsa scarsa.** Processi COM orfani (EXCEL.EXE ≈ 100-200 MB l'uno), bitmap in
   Large Object Heap e righe DataGrid non virtualizzate sono i tre modi più rapidi per saturarla.
3. **Il render thread WPF è spesso in software rendering.** Effetti (`DropShadowEffect`), animazioni
   continue e `BitmapScalingMode="HighQuality"` costano molto più che su una macchina moderna.
4. **Il disco è lento.** Ogni `File.ReadAllText` ripetuto in un percorso caldo si sente.
5. **Le connessioni SQLite non sono gratis.** Aprirne una per riga di un foglio Excel è il classico
   errore che passa inosservato in sviluppo e paralizza in produzione.

---

## 4. Registro Modifiche Effettuate (sessione 22/08/2026)

Tutte le modifiche sono **a comportamento invariato**, salvo l'unica eccezione dichiarata al punto §4.11.
Nessuna feature aggiunta, nessuna rimossa. Build finale: 0 errori, 0 warning.

### 4.1 `VerificheViewModel` — I/O ricorsivo bloccante sul thread UI ⭐ *criticità principale*
**Prima:** il `DispatcherTimer` da 5 s eseguiva `CheckForFileUpdates()` **in linea sul thread UI**:
`Directory.GetFiles(..., SearchOption.AllDirectories)` su **5 alberi OneDrive** più una
`File.GetLastWriteTime` per ogni file trovato. L'interfaccia si bloccava a ogni tick, per sempre.
**Ora:** la scansione è delegata al thread pool, con guardia anti-rientranza (`Interlocked`) perché
le scansioni lente non si accavallino. Estratta in `ScanForFileUpdates()`, che ritorna `bool`.
Enumerazione con `EnumerateFiles` invece di `GetFiles`. Array dei percorsi promossi a `static readonly`
(prima riallocati a ogni tick).

### 4.2 `VerificheViewModel` — una connessione SQLite per riga di foglio Excel
**Prima:** `GetTrenoFromDatabase(loco)` faceva `new DatabaseManager(dbPath)` (apertura + chiusura
connessione) **per ogni riga** della flotta 1000. Un foglio da 200 righe = 200 cicli open/close.
**Ora:** connessione aperta al massimo una volta per file, in modo pigro, più una cache
`loco → treno` case-insensitive che elimina anche le query duplicate.

### 4.3 `VerificheViewModel.LoadDataForFleet` — ordinamento con syscall per confronto
**Prima:** `candidateFiles.OrderByDescending(f => File.GetLastWriteTime(f)).FirstOrDefault()` →
O(n log n) chiamate al file system per trovare un solo massimo.
**Ora:** singola passata. Il confronto usa `>` (non `>=`) per preservare l'ordinamento stabile
originale: a parità di data vince il primo file incontrato.

### 4.4 `EmailService` — riferimenti COM Outlook non rilasciati in caso di errore
**Prima:** le tre `Marshal.ReleaseComObject` erano le ultime istruzioni del blocco `try`. Qualunque
eccezione (allegato mancante, `Display` fallito, destinatari non risolti…) le saltava, lasciando
riferimenti pendenti che impediscono a OUTLOOK.EXE di chiudersi.
**Ora:** riferimenti dichiarati fuori dal `try`, rilasciati in `finally` tramite `ReleaseCom()`, che
assorbe gli errori del singolo rilascio così che i successivi vengano comunque eseguiti.
Stessa correzione applicata a **`PassaggioConsegneEmailService.OpenDraftEmail`**.

### 4.5 `EmailService` — una connessione SQLite per riga di tabella HTML
**Prima:** `ResolveTrainAndSoftware` apriva e chiudeva `train_software.db` per ogni locomotiva.
**Ora:** `OpenTrainSoftwareDatabase()` apre una connessione sola per email, passata come parametro.

### 4.6 `ExcelViewModel` — processo EXCEL.EXE orfano ⭐ *criticità principale*
**Prima:** nel `finally` dell'interop, `ExecuteComWithRetry(() => excelApp.Quit())` e i tre
`ReleaseComObject` erano istruzioni consecutive: un'eccezione su `Quit()` saltava tutti i rilasci
successivi, lasciando un processo Excel invisibile e vivo in memoria. Su una macchina con poca RAM
bastavano pochi salvataggi falliti per saturarla.
**Ora:** ogni passo di pulizia è isolato in `TryComCleanup(...)`.

### 4.7 `ExcelViewModel.ExecuteScriviReport` — celle fantasma ClosedXML
**Prima:** ogni cella veniva recuperata **due volte** (`IsEmpty()` + `GetString()`), e il look-ahead
di 20 righe interrogava celle **oltre il RangeUsed**, costringendo ClosedXML a materializzarle in
memoria (fino a 60 celle inutili per ogni riga scansionata).
**Ora:** helper locale `HasValue(sheet, row, col)` che recupera la cella una sola volta, e stop del
look-ahead a `scanLimit` (le righe oltre sono per definizione vuote → risultato identico).

### 4.8 `ExcelViewModel.LoadExcelFieldsAsync` — lavoro ripetuto per colonna
**Prima:** `worksheet.DataValidations` rienumerata da capo per ognuna delle ~26 colonne;
l'array `importantKeywords` (21 stringhe) riallocato dentro il ciclo.
**Ora:** convalide di tipo lista raccolte una volta prima del ciclo (ordine sorgente preservato →
opzioni identiche); `ImportantKeywords` promosso a `static readonly`.

### 4.9 `ExcelViewModel` — allocazioni in ciclo
- Regex del locomotore costruita **fuori** dal ciclo sulle sottocartelle (il pattern dipende solo da
  `SelectedTrain`, costante).
- `new DirectoryInfo(d).Name` → `Path.GetFileName(d)` (in un punto ne venivano creati **due** per
  cartella, uno per ciascun `Contains`).
- Ricerca log/dump: `Directory.EnumerateFiles(...).Any()` invece di materializzare l'array completo
  dei file solo per leggerne `Length`.

### 4.10 `HomeViewModel` — orologio ed eliminazione
- **Orologio:** `new CultureInfo("it-IT")` era costruita **a ogni secondo**, e `CurrentDate`
  riformattata a ogni tick pur cambiando solo a mezzanotte. Cultura promossa a `static readonly`,
  data ricalcolata solo al cambio giorno. Output visibile identico.
- **Eliminazione cartella:** la scansione ricorsiva degli attributi + `Directory.Delete(path, true)`
  giravano **sul thread UI**; su una cartella con migliaia di file Windows marcava la finestra come
  "Non risponde". Ora su `Task.Run` (le eccezioni risalgono all'`await`, stesso `catch` di prima),
  con `EnumerateFiles`/`EnumerateDirectories` invece di `GetFiles`/`GetDirectories`.
- `DateTime.Now` letto una volta invece che a ogni cartella; `ToUpper()` per sottocartella sostituito
  da confronto `OrdinalIgnoreCase`.

### 4.11 `ExcelViewModel.UpdateFolders` — doppia riapertura del workbook ⚠️ *unica deviazione dichiarata*
**Prima:** `AvailableFolders.Clear()` svuotava l'`ItemsSource` della ComboBox, il che azzerava
`SelectedFolder` e faceva partire `CheckAndLoadExistingReportAsync` una prima volta con `null`;
subito dopo la riselezione ne faceva partire una seconda. Risultato: **due aperture complete del
workbook Excel per ogni evento del FileSystemWatcher**, che scatta anche per modifiche nelle
sottocartelle, del tutto irrilevanti per l'elenco di primo livello.
**Ora:** la nuova lista è calcolata a parte; se è identica a quella corrente **e** la selezione è già
quella che verrebbe riapplicata, il ciclo Clear/riempi/riseleziona viene saltato per intero.

> **Deviazione da segnalare:** nel caso saltato, i valori eventualmente digitati a mano nel form
> **non vengono più azzerati** da una modifica non correlata in `LOG & DUMP`. Lo stato finale
> dell'elenco e della selezione è identico; cambia solo che il form non viene ricaricato a vuoto.
> Se questo azzeramento fosse un comportamento voluto, è sufficiente rimuovere il blocco
> `if (FoldersUnchanged(...) && ...) return;` in `UpdateFolders()` per tornare esattamente a prima.

### 4.12 `MouseWheelScrollBehavior` — visita ricorsiva dell'albero visuale
`FindVisualChild<T>` era ricorsiva e viene invocata **a ogni scatto di rotellina, per ogni elemento
della route** (class handler su `UIElement` con `handledEventsToo: true`).
**Ora:** stack esplicito con **identico ordine di visita pre-order** (figli inseriti in ordine inverso),
quindi risultato garantito identico, senza catena di stack frame. Aggiunta uscita immediata su
`e.Delta == 0`. *(Vedi §6.1: resta un problema strutturale non risolto in questo punto.)*

### 4.13 `PassaggioConsegnePdfExporter` — MemoryStream mai chiuso
Il `MemoryStream` contiene il PNG dell'intero rapportino (facilmente vari MB → Large Object Heap) e
non veniva mai rilasciato. Ora è chiuso in `finally`, **dopo** `document.Save()` — PdfSharp legge i
byte dell'immagine al momento del salvataggio, quindi non può essere chiuso prima.

### 4.14 `DestinatariManager` / `ShortcutsManager` — lettura da disco a ogni chiamata
**Prima:** `LoadConfig()` faceva `File.ReadAllText` + deserializzazione **a ogni invocazione**
(cioè a ogni email generata e a ogni popolamento di shortcut). In più
`EnsurePassaggioConsegneActions` rigenerava l'**intera** configurazione di default (5 treni × fino a
8 azioni con stringhe lunghe di destinatari) anche quando non serviva.
**Ora:**
- cache del **solo testo** del file, validata su data di modifica + dimensione, invalidata al salvataggio;
- la **deserializzazione resta per chiamata**, così ogni chiamante continua a ricevere un grafo di
  oggetti indipendente (scelta deliberata: le modifiche non salvate nella schermata "Destinatari Mail"
  restano invisibili alla generazione email, esattamente come prima);
- configurazione di default costruita in modo pigro, solo se manca davvero un'azione.

### 4.15 `ExcelView.xaml` — animazione indeterminata
`IsIndeterminate` del `ProgressBar` di caricamento era hard-coded a `True`. Ora è legato a `IsLoading`,
così l'animazione continua sul render thread è fermata in modo esplicito e non solo nascosta.

---

## 4-bis. Modifiche a comportamento visibile — approvate dal committente

Le due voci seguenti erano state deliberatamente escluse dal primo giro perché alterano il
comportamento percepito. Sono state **autorizzate esplicitamente** e applicate.

### 4.16 `MouseWheelScrollBehavior` — scorrimento moltiplicato ⭐
**Problema.** `PreviewMouseWheelEvent` è un evento in *tunneling*: la route va dalla finestra fino
all'elemento sotto il mouse. Il class handler registrato su `typeof(UIElement)` veniva quindi invocato
**per ogni elemento attraversato**, e ognuna di quelle invocazioni poteva trovare lo stesso
`ScrollViewer` e chiamare `ScrollToVerticalOffset(offset - delta/3)`. Un solo scatto di rotellina
scorreva quindi N volte, con N = profondità dell'elemento puntato: la **velocità di scorrimento
dipendeva da quanto era annidata la UI**. In più, partendo dalla finestra, la ricerca
`FindVisualChild<ScrollViewer>` restituiva il primo ScrollViewer in ordine di visita, che non è
necessariamente quello sotto il mouse.

**Correzione.**
1. La logica viene eseguita **una sola volta per evento**, sull'elemento più interno della route
   (`ReferenceEquals(sender, GetDeepestUIElement(e))`). Girando alla fine del tunnel, gli handler di
   istanza degli antenati — in particolare lo scorrimento orizzontale di `ChiusuraTicketDialog` —
   mantengono la precedenza.
2. La ricerca del contenitore non **scende** più nel sottoalbero (visita completa, ripetuta per ogni
   livello) ma **risale** l'albero visuale dal punto puntato: da O(dimensione sottoalbero × profondità)
   a O(profondità), una volta sola.
3. Registrazione con `handledEventsToo: false`: se un handler più specifico ha già gestito lo scatto,
   non interveniamo.
4. Semantica finale, quella che il codice cercava di ottenere fin dall'inizio: *se il ScrollViewer
   interno può ancora scorrere non facciamo nulla e lascia fare a WPF; solo quando è a fondo corsa
   inoltriamo lo scatto al ScrollViewer padre.*
5. `GetDeepestUIElement` gestisce anche il caso in cui l'hit-test restituisca un `ContentElement`
   (per esempio un `Run` dentro un `TextBlock`), che non comparirebbe mai come `sender`.

`FindVisualChild<T>` non è più sulla strada critica; è mantenuta come utility pubblica.

### 4.17 `VerificheView.xaml` — layout riorganizzato per abilitare la virtualizzazione ⭐
**Problema.** I tre `DataGrid` stavano dentro `ScrollViewer > StackPanel`. Uno `StackPanel` concede ai
figli **altezza infinita**: nessuna griglia riceveva una viewport vincolata, quindi la virtualizzazione
WPF **non poteva attivarsi a prescindere dalle proprietà impostate**. Ogni riga di ogni griglia veniva
materializzata come `DataGridRow` completa di celle e `TextBlock`, e lo stesso valeva per i tre
`ItemsControl` del riepilogo.

**Correzione.**
- `ScrollViewer` + `StackPanel` esterni sostituiti da una `Grid` con righe `Auto / Auto / *`.
- Le tre sezioni di flotta stanno in una `Grid` interna a righe `Auto` (intestazione) + `*` (griglia,
  `MinHeight="120"`): ogni griglia ha ora una viewport propria e scorre per conto suo.
- Nuovo stile condiviso `VerificheGridStyle` con `ScrollViewer.CanContentScroll="True"`
  (era `False`, ed era quello a disattivare la virtualizzazione), `EnableRowVirtualization="True"`,
  `VirtualizingPanel.IsVirtualizing="True"`, `VirtualizationMode="Recycling"`.
  `ScrollUnit` resta il default **`Item`**: con la colonna "Avaria" a testo mandato a capo le righe
  hanno altezza variabile, e lo scorrimento per riga mantiene stabile la maniglia della barra.
- I tre elenchi del riepilogo usano `RiassuntoListStyle`, che fornisce loro via `ControlTemplate` uno
  `ScrollViewer` con `CanContentScroll="True"` e un `VirtualizingStackPanel` (un `ItemsControl` nudo
  non ha viewport e quindi non può virtualizzare). Il contenitore ha `MaxHeight="260"`: sotto quella
  soglia si dimensiona sul contenuto **esattamente come prima**, oltre fornisce la viewport vincolata.
- Definizioni di colonna e stili delle celle sono invariati; la duplicazione delle proprietà comuni
  delle tre griglie è stata fattorizzata in un unico stile.

**Impatto visivo accettato:** la pagina non è più un unico documento scorrevole ma tre aree con barra
di scorrimento propria; lo scorrimento delle griglie è per riga anziché a pixel.

---

## 5. Invarianti & Regole Critiche

> **Rompere una di queste significa rompere il lavoro quotidiano del tecnico.**

### 5.1 Nomenclatura — è un contratto, non una convenzione
```
Cartella LOG/DUMP :  SR{ticket} {LOG|DUMP} {tipo} {loco} {sw} {ddMMyy} {utente}
Cartella madre    :  {tipo} {treno}       (fallback: {tipo} {loco})
PDF principale    :  [ND ]FL SR{t1}[ - SR{t2}] {tipo} {loco} IMC AV Milano {ddMMyy} {utente}.pdf
PDF checklist     :  NdL … .pdf   oppure   Checklist {nomeTxt} {tipo} {loco} IMC AV Milano {data} {utente}.pdf
PDF non conformità:  NC … .pdf    (ticket incrementato di +1 per ogni NC successivo)
Oggetto email     :  CHIUSURA TICKET {tickets} {tipo} {locos} IMC AV Milano {data} {utente}
```
- La **data è sempre `ddMMyy`** (6 cifre) ed è il perno di quasi tutti i parser: molti algoritmi
  localizzano la data con `\b\d{6}\b` e poi ricavano loco e utente per **posizione relativa**.
- I separatori sono **spazi singoli**. `EmailService` normalizza gli spazi multipli nell'oggetto,
  ma i parser sui nomi di cartella no.
- Il marcatore `" LOG "` / `" DUMP "` è cercato **con gli spazi**: senza, il parsing fallisce.

### 5.2 Lo scostamento `I-F` / `FH`
Ricorre in **almeno 8 punti indipendenti** (`EmailService`, `TrainViewHelper`, `ChiusuraTicketDialog`, …):
```csharp
int locoStartIndex = 3;                       // o 1, a seconda del contesto
if (parts[3] è "I-F" oppure "FH") locoStartIndex = 4;   // il token in più sposta tutto
```
Qualunque modifica al parsing va replicata in **tutti** i punti, o le flotte I-F/FH si rompono in silenzio.

### 5.3 Alias dei nomi treno — non unificarli senza una mappa esplicita
| Contesto | Valore |
|---|---|
| ComboBox modulo Excel | `E404P`, `ETR700`, `ETR1000 / 1000FH`, `ETR1000 I-F` |
| Chiave `destinatari.json` | `E404P`, `ETR700`, `ETR1000`, `ETR1000IF`, `ETR1000FH` |
| Colonna `tipo` in `flotte` | come sopra, ma `ETR1000IF` → `ETR1000 I-F` e `ETR1000FH` → `ETR1001FH` |
| Cartelle di rete | `ETR1001`, `ETR1000`, `1000FH`, `ETR1000_1001`, `E404P`, `ETR500`, `E404`, … |

La normalizzazione vive in `HomeViewModel.AreTrainTypesCompatible` / `ResolveTrainTypePath` e in
`ExcelViewModel.MatchesTrain`. **`ETR1000 / 1000FH` deve escludere** Italia/Francia/ITA-FRA/1000IF/I-F.

### 5.4 Excel — mai riscrivere il file con ClosedXML
Il "Report Interventi" aziendale contiene formattazione condizionale, convalide dati e named range.
**La scrittura deve restare su Excel Interop.** ClosedXML è ammesso **solo in lettura**.
Le colonne del form partono da **B (indice 2)**; l'intestazione è sulla **riga 1**.
`maxCol` = 24 per `ETR1000 I-F`, 27 per gli altri.

### 5.5 Outlook — il corpo va inserito *dentro* la firma
`mailItem.GetInspector` **deve** essere invocato per forzare Outlook a generare la firma in `HTMLBody`.
Il corpo generato va poi inserito subito dopo il tag `<body …>` della firma
(`MergeBodyWithSignature`), **non** concatenato prima: altrimenti la firma finisce in testa all'email.
`Display(false)` apre la bozza **senza inviarla** — l'invio resta sempre un'azione manuale dell'utente.

### 5.6 `LOG & DUMP in rete` — non creare mai cartelle
`HomeViewModel.OnLogDumpRete` risolve **solo cartelle esistenti** in rete
(`FindExistingLocoFolder`, `FindExistingTargetSubfolder`). Se non le trova, **salta il file e lo
segnala**. Non deve mai creare struttura sul percorso di rete condiviso.

### 5.7 Rinomina PDF — deve restare atomica
`PdfView.BtnRinomina_Click` esegue la rinomina in **due fasi** (tutti i file → nome temporaneo con GUID,
poi temporaneo → nome finale) per gestire gli scambi di nome. Prima verifica che le destinazioni
calcolate non contengano duplicati. **Non semplificare in una passata sola.**

### 5.8 Cache e stato
- `MainWindow` mantiene **una sola istanza** per vista di primo livello: i ViewModel vivono per tutta
  la sessione e i loro timer/sottoscrizioni non vengono mai rilasciati (accettabile solo grazie a questo).
- `info_ticket.json` è il ponte fra il modulo EMAIL e l'autocompilazione del modulo EXCEL:
  è scritto sia da `ChiusuraTicketDialog.SaveCache()` sia da `EmailService.SaveCacheJson()`.
- `AppConfig.Initialize()` **deve** essere chiamata prima di ogni altra cosa in `App.OnStartup`.

### 5.9 Regole di lavoro sul codice
- Il codice, i commenti e i messaggi utente sono **in italiano**. Mantenere la lingua.
- I `MessageBox` con testo esatto fanno parte del comportamento atteso: non riformularli.
- Diversi `catch { }` sono **silenziosi di proposito** (parsing best-effort su nomi di cartella
  imprevedibili). Non convertirli in errori visibili senza motivo.
- `Nullable` e `ImplicitUsings` sono abilitati. La build deve restare a **0 warning**.

---

## 6. Roadmap per le Prossime Sessioni

### 6.1 Criticità note **non** risolte (richiedono una scelta esplicita, perché cambierebbero il comportamento visibile)

> Le ex-voci **A** (scorrimento moltiplicato) e **B** (virtualizzazione `VerificheView`) sono state
> approvate e applicate: vedi §4.16 e §4.17. Restano aperte le 7 seguenti.

**C. `ScrollViewer.CanContentScroll="False"`** su HomeView e sui 3 DataGrid di PassaggioConsegne.
Disabilita la virtualizzazione anche dove il contenitore è vincolato. Il rimedio a comportamento
quasi-identico è `CanContentScroll="True"` + `VirtualizingPanel.ScrollUnit="Pixel"`.
Non applicato perché con righe ad altezza variabile (HomeView usa `RowDetailsTemplate`) la maniglia
della scrollbar può cambiare dimensione mentre si scorre. Impatto attuale basso: le collezioni sono piccole.

**D. `PassaggioConsegneViewModel` — sottoscrizione statica mai rilasciata**
```csharp
VerificheViewModel.OnVerificheDataUpdated += () => { … };   // lambda anonima, nessun -=
```
Innocuo **solo** finché `MainWindow` tiene una sola istanza della vista. Se un giorno la vista venisse
ricreata, ogni istanza resterebbe viva per sempre. Stesso schema in `HomeViewModel`
(`AppWatcher.OnLogDumpFolderChanged`, mai rimosso; `DispatcherTimer` mai fermato).
`ExcelViewModel` implementa `IDisposable` ma **nessuno chiama `Dispose()`**: è codice morto.

**E. `PassaggioConsegnePdfExporter` — `RenderTargetBitmap` non vincolata**
`element.Measure(new Size(PositiveInfinity, PositiveInfinity))` seguito da un `RenderTargetBitmap`
Pbgra32 della dimensione risultante: 4 byte per pixel in Large Object Heap. Con un rapportino
3000×4000 sono ~48 MB in un colpo solo. Su una macchina con 4 GB può fallire.
*Correzione proposta:* limitare la dimensione di render (o rendere a tile). Cambia la risoluzione del PDF.

**F. `VerificheViewModel.GetVerificheForFleetStatic` — fallback sincrono**
Se `Instance == null` (cioè si apre "Passaggio di Consegne" **prima** di "Verifiche"), il costruttore
di `PassaggioConsegneViewModel` esegue sul thread UI il caricamento completo di 3 flotte: enumerazione
ricorsiva OneDrive + parsing ClosedXML. Freeze di parecchi secondi all'apertura del modulo.
Il costo per riga è già stato abbattuto (§4.2/4.3), ma la chiamata resta bloccante.

**G. `HomeViewModel.OnLogDumpRete` — risoluzione percorsi di rete sul thread UI**
`GetLogDumpReteBasePath()` e `ResolveTrainTypePath()` fanno `Directory.Exists` su percorsi OneDrive/rete
prima di entrare nel `Task.Run`. Su rete lenta o disconnessa possono bloccare per secondi.

**H. `MainWindow.xaml` — `DropShadowEffect` sulla navbar**
È un effetto a pixel shader su un `Border` a tutta larghezza. Su macchine senza accelerazione WPF adeguata
viene renderizzato via software. Opzioni: rimuoverlo (ombra appena percettibile: `Opacity="0.05"`) oppure
aggiungere `CacheMode="BitmapCache"`. Entrambe toccano la resa visiva → serve conferma.

**I. `RenderOptions.BitmapScalingMode="HighQuality"`** su `MainWindow` è ereditato da tutto l'albero.
`LowQuality`/`NearestNeighbor` sarebbe più adatto a hardware datato, ma cambierebbe la resa.

### 6.2 Debito tecnico / igiene del repository

- [x] **`.gitignore` creato** (regole .NET/WPF + VS/Rider/VS Code + NuGet + OS). Attenzione: **non**
      aggiungere una regola `*.db` globale — i database sotto `modules/database/` sono dati sorgente
      e devono restare versionati.
- [x] **`EPPlus 4.5.3.3` rimosso** dal `.csproj`. Verificato: zero `using OfficeOpenXml` / `ExcelPackage`
      nel codice, zero occorrenze residue in `project.assets.json`, `EPPlus.dll` assente dall'output.
- [ ] **`bin/` e `obj/` ancora tracciati** (~480 file su 800). Il `.gitignore` da solo non basta:
      serve `git rm -r --cached` (comandi in §7.2).
- [ ] **`TestClosedXML/`, `scratch/`, `ep_test.cs`, `test.cs`** sono scratch. `TestClosedXML` è però
      referenziato nella `.sln`: rimuoverla dalla soluzione prima di cancellare.
- [ ] **`build_last.txt` / `build_out.txt`** sono log di build committati: da rimuovere dal tracking.
- [ ] **`DatabaseManager._dbLock` è statico**: serializza ogni accesso al database dell'intero processo,
   anche fra file `.db` diversi. Valutare un lock per istanza.
- [ ] **`DatabaseView`** compone SQL per interpolazione su nome tabella e colonne
      (`$"SELECT * FROM {tableName}"`, `UPDATE {tableName} SET {col} = @p`). I valori provengono dallo
      schema SQLite locale, quindi il rischio pratico è nullo, ma usare identificatori quotati è più corretto.
- [ ] **`ExcelViewModel.AutoFillReportFieldsAsync` muta i ViewModel da un thread di background.**
      Funziona perché WPF marshalla automaticamente i `PropertyChanged` scalari sul dispatcher, ma è
      fragile: basta introdurre una mutazione di `ObservableCollection` per far esplodere tutto.
      Da consolidare portando le assegnazioni sul dispatcher.
- [ ] **`TrainViewHelper.NavigateBack()`** crea `new EmailView()` invece di riusare la cache di
      `MainWindow`: la cache resta popolata con un'istanza ormai orfana.
- [ ] **`AppWatcher`** non espone alcun modo per fermarsi o rilasciare il `FileSystemWatcher`.
      Valutare anche `InternalBufferSize` (il default di 8 KB può perdere eventi in caso di raffiche).
- [ ] **Nessun test automatico.** I parser dei nomi di cartella (§5.1, §5.2) sono la parte più fragile
      e più facilmente testabile del sistema: sono funzioni pure su stringhe. Un progetto di unit test
      su `ExtractLocosFromFolder`, `BuildSubject`, `ParseLogFolderName`, `AreTrainTypesCompatible` e
      `MatchesTrain` darebbe la rete di sicurezza che oggi manca del tutto.

### 6.3 Idee funzionali emerse dal codice (non richieste, solo annotate)
- Le tabelle `renamer_config` / `renamer_queue` / `renamer_log` esistono in `train_software.db` e sono
  gestite da `DatabaseView`, ma **nessun modulo dell'app le usa**: residuo di una funzione di rinomina
  automatica mai completata (o rimossa).
- `ExcelViewModel.Trains` è una lista hard-coded di 4 flotte, mentre gli altri moduli ne gestiscono 8:
  ETR421/521/522 non hanno modulo Excel.

---

## 7. Come riprendere il lavoro

### 7.1 Build ed esecuzione

```bash
dotnet build PersonalAutomationTool/PersonalAutomationTool.csproj
```
```bash
dotnet run --project PersonalAutomationTool/PersonalAutomationTool.csproj
```

### 7.2 Pulizia del tracking Git (da eseguire una volta sola)

Il `.gitignore` è già presente, ma Git **continua a tracciare i file già indicizzati**: le regole di
ignore valgono solo per i file non ancora tracciati. Serve quindi svuotare l'indice e ricostruirlo.
`git rm --cached` rimuove **solo dall'indice**, i file restano sul disco.

```bash
git rm -r --cached . --quiet && git add . && git status --short | head -40
```

Il comando sopra è il modo più sicuro: azzera l'indice e lo ricostruisce applicando il `.gitignore`.
Verificare in `git status` che compaiano **solo** righe `D` per `bin/`, `obj/` e `scratch/`, e che
**nessun** file sorgente o `.db` risulti eliminato. Poi:

```bash
git commit -m "chore: smetti di tracciare gli artefatti di build e aggiungi .gitignore"
```

> ⚠️ **Da verificare prima del commit:** i file `PersonalAutomationTool/modules/database/*.db` devono
> restare tracciati (sono dati sorgente, copiati in output da `CopyToOutputDirectory`). Se compaiono
> come eliminati, il `.gitignore` contiene una regola `*.db` di troppo.

I file resteranno comunque nella **storia** del repository (il clone non si alleggerisce
retroattivamente). Per riscrivere anche la storia servirebbe `git filter-repo`, operazione distruttiva
che invalida tutti i cloni esistenti: da valutare solo se la dimensione del repository diventa un
problema concreto.

**Prima di toccare qualsiasi cosa:** leggere §5 (Invarianti). La maggior parte della logica di questa
applicazione non è nei tipi, ma nelle **convenzioni sui nomi di file e cartella** — e il compilatore
non può proteggerle. Ogni modifica al parsing va verificata su casi reali presi da `LOG & DUMP`.

**Verifica manuale minima consigliata dopo modifiche strutturali:**
1. CARTELLE → creare una coppia LOG/DUMP con 2 ticket e verificare i nomi generati.
2. HOME → la riga compare, l'espansione mostra le sottocartelle, "Aggiorna Data" riscrive le 6 cifre.
3. PDF → rinomina con 1 e con 2 PDF non spuntati, più almeno un NC.
4. EMAIL → chiusura ticket su una flotta I-F: verificare oggetto, destinatari, firma **sotto** il corpo.
5. EXCEL → Sposta Report, verifica autocompilazione, Scrivi report, e **controllare in Gestione
   attività che non resti un processo EXCEL.EXE** (regressione §4.6).
6. PASSAGGIO CONSEGNE → esportare il PDF e verificare che la bozza Outlook si apra con l'allegato.
7. **Rotellina** (regressione §4.16) → verificare che: lo scorrimento abbia la stessa velocità in
   HOME, in EXCEL e dentro il dialog Chiusura Ticket (prima dipendeva dalla profondità della UI);
   arrivati a fondo di un elenco interno, il contenitore esterno riprenda a scorrere; nel dialog
   Chiusura Ticket lo scorrimento **orizzontale** fra gli avvisi continui a funzionare.
8. **VERIFICHE** (regressione §4.17) → verificare che le tre griglie mostrino tutti i dati, che
   ognuna scorra per conto proprio e che il riquadro di riepilogo in alto resti leggibile
   (compare una barra di scorrimento solo se le liste superano i 260 px).
