# PROJECT_MEMORY.md — Personal Automation Tool (iscot-autotool)

> **Scopo di questo file.** Documento di passaggio di consegne verso qualsiasi sessione futura (umana o AI).
> Contiene tutto ciò che serve per lavorare sul progetto senza doverlo ri-esplorare da zero: architettura,
> vincoli, invarianti da non rompere e stato del lavoro svolto.
>
> **Ultimo aggiornamento:** 23 agosto 2026 — sessione di audit e ottimizzazione per hardware Windows datato,
> secondo giro con le modifiche a comportamento visibile approvate dal committente (§4-bis), terzo giro
> con l'avvio dello Sprint 1 della roadmap strategica (§6.1: `LogDumpFolderName`, primo progetto di test,
> prima estrazione di percorsi hardcoded), quarto giro con lo **Sprint 2** (§6.1-bis: cache `flotte` in
> memoria, `PdfRenamePlanner` estratto e testato, golden-file test sulle email, PID-tracking Excel,
> indici SQLite, virtualizzazione HomeView, pulizia navbar, lock `DatabaseManager` per istanza), quinto
> giro con lo **Sprint 3** (§6.1-ter: lettura tipizzata `DatabaseManager.Query<T>`, dialog di anteprima
> rinomine in PDF e HOME, overlay di progresso riutilizzabile, storico/annulla rinomina su
> `renamer_log`), e infine con il **fix del parsing EXCEL** (§6.1-quater: `ExcelFolderParser`), sbloccato
> dai nomi di cartella reali finalmente forniti dal committente — la scoperta #1 dello Sprint 2, rimasta
> aperta per due sprint, è **risolta**.
> **Stato build alla chiusura sessione:** `dotnet build` sull'intera `.sln` → 0 errori, 0 warning su
> `PersonalAutomationTool` e `PersonalAutomationTool.Tests` (i 2 warning residui sono preesistenti nello
> scratch `TestClosedXML`, fuori scope — vedi §6.5). `dotnet test` → **64/64 superati**. L'eseguibile è
> stato avviato manualmente per verificare l'assenza di eccezioni allo startup — vedi la nota su cosa
> **non** è stato verificato in §6.1-ter. **Da leggere prima di toccare il modulo EXCEL: §5.3-bis**
> (ETR1000 / ETR1000FH / ETR1000IF sono tre treni distinti; solo in EXCEL i primi due condividono il
> report).

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
│   ├── core/                        AppConfig, AppWatcher, RelayCommand, HitachiPathsManager,
│   │                                FlotteCache (§6.1-bis), ViewModelBase, MouseWheelScrollBehavior,
│   │                                Converters/
│   │   └── core/Naming/             LogDumpFolderName — parser condiviso e testato dei nomi
│   │                                di sottocartella LOG/DUMP (vedi §6.1, §6.2 intervento 1.1)
│   ├── modules/                     un sottoalbero per modulo funzionale
│   │   ├── home/  cartelle/  excel/  verifiche/  database/
│   │   ├── pdf/                  PdfView, PdfRenamePlanner (§6.1-bis, intervento 2.1)
│   │   ├── destinatari_mail/  passaggio_consegne/
│   │   └── email/               EmailService, EmailView, dialogs/, trains/
│   └── modules/database/*.db        train_software.db, emails.db (copiati in output)
├── PersonalAutomationTool.Tests/    ← xUnit, ProjectReference verso PersonalAutomationTool.
│                                       Zero dipendenza da WPF: solo classi pure sotto core/ e
│                                       modules/pdf/, più un uso mirato di InternalsVisibleTo per
│                                       EmailService.BuildHtmlBody (golden-file test, §6.1-bis).
│                                       Vedi §6.1/§6.1-bis per cosa copre oggi e §6.2 per il piano
│                                       a 3 livelli.
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
- **più** un `DispatcherTimer` di backstop (60 s dal §6.1 di questa sessione, prima 5 s — vedi §4.1
  per il motivo per cui questa scansione è comunque su thread pool) che riscandaglia 5 alberi di
  cartelle confrontando le date di modifica, come rete di sicurezza per eventi persi dai watcher;
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
| `hitachi_paths.json` | `{BaseDirectory}\` | cartella Hitachi base per treno, usata da `ExcelViewModel` (Sposta/Riporta Report); auto-generato al primo avvio — vedi §6.1 |

`DatabaseManager` incapsula SQLite (`Microsoft.Data.Sqlite`) e serializza gli accessi con un `lock`
**per istanza** (era statico e condiviso fra tutte le istanze fino al §6.1-bis: si veda lì per il
motivo). Espone due API di lettura: `ExecuteQuery` (restituisce `DataTable`, per `DatabaseView` — un
browser SQLite a schema arbitrario che ne ha davvero bisogno) e `Query<T>` (proiezione tipizzata via
`SqliteDataReader`, senza `DataTable` intermedio — aggiunta nello Sprint 3, §6.1-ter, intervento 2.7,
usata da `FlotteCache`, `RubricaDialog` e `RenamerLog`).

**`FlotteCache`** (`core/FlotteCache.cs`, §6.1-bis) tiene in memoria l'intera tabella `flotte` di
`train_software.db`, invalidata su mtime+dimensione del file come i JSON sopra. **Non** è usata da
`DatabaseView`, che deve continuare a interrogare SQLite direttamente per mostrare dati sempre
aggiornati (supporta inserimento/modifica/eliminazione righe dal vivo).

### 2.7 Integrazioni esterne (COM, late-bound via `dynamic`)

| Integrazione | Dove | Uso |
|---|---|---|
| **Outlook** (`Outlook.Application`) | `EmailService`, `PassaggioConsegneEmailService` | crea `MailItem`, forza l'`Inspector` per ottenere la firma, inserisce il corpo HTML **prima** della firma, allega i PDF, `Display(false)` |
| **Excel** (`Excel.Application`) | `ExcelViewModel.ExecuteScriviReport` | scrive la nuova riga del report **in modo nativo** per non alterare formattazione/struttura; PID del processo tracciato per terminazione forzata di sicurezza se `Quit()` non basta (§6.1-bis, intervento 1.4) |
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
`e.Delta == 0`. *(Il problema strutturale di scorrimento moltiplicato non era ancora risolto a
questo punto della sessione: risolto poi in §4.16.)*

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
| Colonna `tipo` in `flotte` | `E404P`, `ETR700`, `ETR1000`, `ETR1000 I-F`, `ETR1001FH` (**verificato sul `.db` reale**) |
| **Nomi di sottocartella su disco** | `ETR1000`, **`ETR1000IF`** (attaccato, ≠ dal `tipo` in `flotte`), `ETR700`, `E404P` — vedi §6.1-quater per i nomi reali completi |
| Cartelle di rete | `ETR1001`, `ETR1000`, `1000FH`, `ETR1000_1001`, `E404P`, `ETR500`, `E404`, … |

> ⚠️ **Le etichette della ComboBox di Excel non compaiono mai come tali sui nomi di cartella.** Due
> delle quattro (`ETR1000 / 1000FH` e `ETR1000 I-F`) non corrispondono ad alcun token su disco: usarle
> come token letterale in un pattern è il difetto corretto nello Sprint 3 (§6.1-quater). La traduzione
> etichetta → token reali vive in **`ExcelFolderParser.GetDiskTokens`**: ogni nuovo codice che debba
> cercare un treno dentro un nome di cartella deve passare da lì, non interpolare `SelectedTrain`.

#### 5.3-bis `ETR1000`, `ETR1000FH` ed `ETR1000IF` sono TRE TRENI DISTINTI

Confermato dal committente (Sprint 3). Non è una sfumatura terminologica: quasi tutta l'applicazione
li tratta già come flotte separate, ed **è il solo modulo EXCEL a raggrupparne due** — per una ragione
precisa, non per svista.

| Modulo | ETR1000 | ETR1000FH | ETR1000IF |
|---|---|---|---|
| EMAIL (viste) | `ETR1000View` | `ETR1000FHView` | `ETR1000IFView` |
| `destinatari.json` | `ETR1000` | `ETR1000FH` | `ETR1000IF` |
| `shortcuts.json` | `ETR1000` | `ETR1000FH` | `ETR1000IF` |
| VERIFICHE (cartelle Hitachi) | `Interventi ETR1000` | `Interventi ETR1000FH` | `Interventi ETR1000IF` |
| `flotte`, colonna `tipo` | `ETR1000` (116 righe) | `ETR1001FH` (2 righe) | `ETR1000 I-F` (18 righe) |
| **EXCEL** | \| ← **stesso Report Interventi**, voce `ETR1000 / 1000FH` → \| | | `ETR1000 I-F` (report proprio, `maxCol` 24) |

**Regola operativa.** In EXCEL, ETR1000 e FH condividono report, cartella Hitachi
(`SSB_SST - Interventi ETR1000`) e opzioni del form: la voce unica di ComboBox è **corretta**, non va
divisa. La I-F resta separata. **Ma condividere il report non significa essere lo stesso rotabile:**
il campo `ROTABILE` deve riportare il treno reale della cartella, non l'etichetta — vedi §6.1-quater,
punto "ROTABILE".

La normalizzazione vive in `HomeViewModel.AreTrainTypesCompatible` / `ResolveTrainTypePath`, in
`ExcelViewModel.MatchesTrain` e in `ExcelFolderParser.GetDiskTokens`.
**`ETR1000 / 1000FH` deve escludere** Italia/Francia/ITA-FRA/1000IF/I-F: le due voci portano a report
Excel con `maxCol` diverso (24 contro 27, §5.4) e a cartelle Hitachi diverse, quindi confonderle
significa scrivere nel report sbagliato. Coperto da test (`ExcelFolderParserTests`) in **entrambe** le
direzioni.

**Preferire `LogDumpFolderName.TryParse` a qualunque nuovo regex** per estrarre campi da un nome di
sottocartella: localizza per posizione e non dipende da alias né etichette. Chiamanti migrati finora:
`PdfView` (Sprint 1) ed `ExcelViewModel` via `ExcelFolderParser` (Sprint 3).

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

> **Premessa che guida le priorità (invariata dalla sessione precedente):** il rischio principale di
> questa applicazione non è il crash, è l'**output silenziosamente sbagliato**. Se un parser
> posizionale sbaglia a estrarre il locomotore da un nome di cartella, non esplode niente: parte
> un'email a Hitachi con la loco sbagliata, o si scrive una riga errata nel Report Interventi
> aziendale. I numerosi `catch { }` best-effort rendono questo scenario invisibile. È un rischio di
> processo, non di software, e detta l'ordine di tutto quello che segue.

### 6.1 Sprint 1 — Core Parsing & Test Suite — **avviato in questa sessione**

Primo sprint della roadmap in §6.2 (interventi 1.1 + 2.2, più due quick win a rischio pressoché
nullo). Obiettivo: creare l'infrastruttura di test che oggi manca del tutto, e cominciare a
sostituire — un chiamante alla volta, verificando ogni volta prima di procedere — gli otto punti
indipendenti che ridecodificano la stessa grammatica di nomi cartella.

- [x] **Progetto `PersonalAutomationTool.Tests`** aggiunto alla soluzione (xUnit,
      `Microsoft.NET.Test.Sdk`, `TargetFramework=net10.0-windows` senza `UseWPF`: zero dipendenza
      da WPF, per costruzione — dimostra che la logica estratta è davvero indipendente dalla UI).
      `ProjectReference` verso `PersonalAutomationTool.csproj`.
- [x] **`LogDumpFolderName`** (`core/Naming/LogDumpFolderName.cs`) — record immutabile con
      `TryParse(folderName, knownTypes, out result)` e `Format()`, per il formato di sottocartella
      `SR{ticket} {LOG|DUMP} {tipo} {loco} {software} {ddMMyy} {utente}`. Copre **solo** questo
      formato (non la "cartella madre" `{tipo} {treno}`, non il nome dei file ZIP spostati in
      rete: sono grammatiche diverse, vedi il commento XML del tipo). La gestione dello scostamento
      `I-F`/`FH` non richiede più codice ad hoc con indici posizionali: basta passare l'elenco dei
      `tipo` noti (da `flotte`) **ordinato per lunghezza decrescente**, e il confronto
      `StartsWith(tipo + " ")` distingue correttamente `"ETR1000 I-F"` da `"ETR1000"` — verificato
      nella DB reale (`SELECT DISTINCT tipo FROM flotte` → `E404P`, `ETR700`, `ETR1000`,
      `ETR1000 I-F`, `ETR1001FH`: quest'ultimo, si noti, **non** è `"ETR1000FH"`).
- [x] **19 test Tier 1** in `LogDumpFolderNameTests.cs`, tutti su funzioni pure (nessun file
      system): casi standard per ognuna delle 5 flotte reali, il caso critico che distingue
      `"ETR1000"` da `"ETR1000 I-F"` quando la loco è puramente numerica, software multi-parola
      (`"04.01 HR"`, valore reale delle opzioni combo in `ExcelViewModel`), software vuoto che
      produce uno spazio doppio nel nome (artefatto reale, non di laboratorio — vedi sotto), tipo
      sconosciuto (fallback identico all'originale), elenco `knownTypes` vuoto o **in ordine
      sbagliato** (test che documenta la landmine invece di nasconderla), prefisso mancante, kind
      mancante o minuscolo, data non a 6 cifre, round-trip `Format()` → `TryParse()`.
      **Risultato:** 19/19 superati al primo colpo dopo la scrittura.
      **Scoperta della sessione, non ovvia prima di scrivere i test:** se il campo "Utente" viene
      lasciato vuoto in CARTELLE, `.Trim()` sull'intera stringa interpolata rimuove lo spazio
      separatore che la grammatica richiede dopo la data, e la cartella risultante **non è più
      analizzabile da nessun parser**, né quello originale né questo — non un bug introdotto qui, un
      limite preesistente del formato su disco, ora candidato esplicito per l'intervento 1.2.
- [x] **Migrazione pilota:** `PdfView.ParseLogFolderName` (con la sua classe di supporto
      `ParsedFolderInfo` e i due `[GeneratedRegex]` `LogFolderRegex`/`LogDateRegex`, usati solo lì)
      **rimossi**, sostituiti dalla chiamata a `LogDumpFolderName.TryParse`. Verificato con build
      pulita (0 warning) che il resto del file (`SrTicketRegex`, usata per l'incremento dei ticket
      sui file NC, non toccata) resta identico.
      **Gli altri 7 chiamanti restano invariati**: `HomeViewModel` (nome cartella madre — formato
      diverso, fuori scope), `EmailService.BuildSubject`/`GetLogAndDumpFolders`,
      `TrainViewHelper.ExtractLocosFromFolder` (3 varianti interne), `ChiusuraTicketDialog.PopulateLocos`,
      `ExcelViewModel` (diversi punti di `AutoFillReportFieldsAsync`). Vanno migrati **uno alla
      volta**, verificando sul campo prima di procedere col successivo — non in blocco.
- [x] **Quick win 2.3 (percorsi Hitachi in config), applicato in forma ridotta e mirata:**
      `core/HitachiPathsManager.cs` (stesso pattern di cache "testo validato su mtime+dimensione"
      già usato da `DestinatariManager`/`ShortcutsManager`) legge `hitachi_paths.json` e sostituisce
      la cartella Hitachi base per treno, prima **duplicata identica** in
      `ExcelViewModel.ExecuteSpostaReport` **ed** `ExecuteRiportaReport`. Verificato manualmente,
      riga per riga, che i valori di `hitachiDir` prodotti per le 4 flotte (ETR700, E404P,
      ETR1000/1000FH, ETR1000 I-F) siano testualmente identici a quelli hardcoded prima.
      **Deliberatamente NON estratta** la costruzione di `targetFolder`/`trainPrefix`: ha forme non
      uniformi da treno a treno (con/senza anno nel percorso, profondità diversa, maiuscole diverse
      — "OLD REPORT" vs "OLD Report"), e forzarla in un unico schema JSON avrebbe rischiato di
      introdurre una differenza sottile senza un beneficio proporzionato. Resta inline in C#, ora
      però a partire da un `hitachiDir` centralizzato.
      **Non toccati** (fuori scope di questo intervento): i percorsi di `VerificheViewModel`
      (`PollingRelativePaths`/`WatcherRelativePaths`) e la risoluzione della cartella di rete in
      `HomeViewModel.GetLogDumpReteBasePath`.
- [x] **Quick win 3.1 (polling Verifiche):** `VerificheViewModel._refreshTimer.Interval` da 5 s a
      60 s. I `FileSystemWatcher` (§2.5) restano il canale primario; il timer è solo un backstop
      per eventi persi — a 60 s svolge la stessa funzione con un dodicesimo dell'I/O su disco.

**Build/test alla chiusura dello sprint:** `dotnet build` sull'intera `.sln` → 0 errori; 0 warning
su `PersonalAutomationTool` e `PersonalAutomationTool.Tests` (i 2 warning NU1510 residui sono
preesistenti in `TestClosedXML`, scratch fuori scope). `dotnet test` → **19/19 superati**.

### 6.1-bis Sprint 2 — esecuzione della roadmap strategica, con scoping esplicito

Il committente ha chiesto l'esecuzione **completa e sistematica** di tutte le ~20 voci rimaste in
§6.2 (le 4 macro-aree), suddivisa in 4 fasi. Prima di eseguire alla cieca, è stato investigato a
fondo l'item dichiarato a più alta priorità (1.1, migrare i restanti 7 chiamanti di
`LogDumpFolderName`): l'investigazione ha rivelato che **non** è la continuazione meccanica dello
Sprint 1 che sembrava (vedi "Scoperte" più sotto). Su queste basi lo sprint è stato **rifocalizzato**
sul sottoinsieme eseguibile con lo stesso rigore dello Sprint 1 (verifica per blocco, build+test a
0 errori/0 warning), rimandando esplicitamente — con motivazione, non per pigrizia — tutto ciò che
richiede dati reali di validazione non disponibili in questo ambiente o decisioni di prodotto/UX
che spettano al committente. Vedi il ledger "Fatto / Rimandato" in fondo a questa sezione.

**Interventi completati:**

- [x] **1.4 — PID tracking Excel.** Reinterpretato in modo più stretto e più sicuro di "isolare in un
      processo separato" (che avrebbe richiesto una vera e propria architettura a due processi):
      snapshot di `Process.GetProcessesByName("EXCEL")` prima di `Activator.CreateInstance`, PID del
      nuovo processo ricavato per differenza (Excel non espone il proprio PID quando è invisibile).
      Nel `finally`, dopo i tentativi di `Quit()`/`ReleaseComObject` già presenti dalla sessione
      precedente, rete di sicurezza finale: se il processo tracciato è ancora vivo dopo un margine di
      3 secondi, viene terminato forzatamente (`Process.Kill()`).
- [x] **3.2 — `FlotteCache` (`core/FlotteCache.cs`).** Tabella `flotte` intera in memoria, invalidata
      su mtime+dimensione del file `.db` (stesso pattern dei JSON di configurazione). Migrati **tutti
      e 6** i punti che interrogavano `flotte` via SQL: `CartelleView` (×3: elenco tipi, treno da
      tipo+loco, software da tipo+loco), `VerificheViewModel.GetTrenoFromDatabase`,
      `PdfView.GetTipiFromDbAsync`, `EmailService.ResolveTrainAndSoftware` (×2, incluso il fallback
      con loco troncata). Ogni metodo di ricerca replica **esattamente** la query che sostituisce,
      comprese le differenze fra chiamanti — **solo** `FindTrenoByLoco` (che sostituisce la query di
      `VerificheViewModel`) applica il fallback numerico su `loco`, perché era l'unica delle sei query
      originali ad averlo; gli altri restano a confronto testuale puro. `CartelleView` non ha più
      bisogno di un proprio `DatabaseManager`: il campo e la sua inizializzazione sono stati rimossi.
- [x] **3.3 — Indici SQLite.** `CREATE INDEX IF NOT EXISTS` su `flotte(tipo, loco)` e `flotte(loco)`,
      idempotente, eseguito in `App.OnStartup` via `Task.Run` (fire-and-forget, non deve ritardare
      l'apertura della finestra). Con `FlotteCache` questi indici non servono più alle ricerche di
      questa sessione, ma restano utili a `DatabaseView`, che continua a interrogare SQLite dal vivo.
- [x] **2.1 — `PdfRenamePlanner` (`modules/pdf/PdfRenamePlanner.cs`).** Logica di
      `PdfView.BtnRinomina_Click` estratta in una classe statica pura: `CreatePlan(TrainCardModel,
      knownTypes, Func<string,int> getPdfPageCount) → PdfRenamePlan`. Zero dipendenza da WPF
      (`TrainCardModel`/`FolderItemModel` non ne hanno a loro volta). Concede a sé stessa **una sola**
      eccezione di I/O: `File.Exists` in lettura per rilevare conflitti su disco — indispensabile per
      decidere correttamente il piano — ma **non esegue mai scritture**: `File.Move`/`File.Delete`
      restano nell'esecutore (`PdfView`), unico punto che deve gestire fallimenti a metà operazione.
      `PdfRenamePlan` distingue tre esiti (`Error` con `Severity` Warning/Error — per non perdere la
      distinzione di icona/titolo del `MessageBox` originale —, `NothingToDo`, `Ready` con la lista di
      `(OldPath, NewPath)`), preservando **1:1** tutti i messaggi di errore originali.
      `PdfView.BtnRinomina_Click` è ora un esecutore sottile: chiama il planner, poi esegue il piano
      con lo stesso spostamento a due fasi (nome temporaneo → nome finale) di prima.
- [x] **2.4 — 11 test Tier 2 per `PdfRenamePlanner`** (`PdfRenamePlannerTests.cs`), su alberi di
      cartelle reali (`Directory.CreateTempSubdirectory`, ripulita a fine test): FL singolo, FL+NdL
      per pagine, FL+Checklist da file .txt, NC multipli con incremento ticket, i quattro casi di
      errore (nessun PDF, troppi non spuntati, nessuna cartella LOG, nome non analizzabile), il caso
      "nulla da fare" (nome già corretto), e il conflitto con un file di destinazione già presente su
      disco (vedi "Scoperte" per come quest'ultimo ha richiesto un `TrainCardModel` costruito a mano,
      non una scansione reale).
- [x] **2.5 — Golden-file test su `BuildHtmlBody`** (`EmailServiceHtmlGoldenTests.cs`).
      `EmailService.BuildHtmlBody` reso `internal` (era `private`) con `InternalsVisibleTo` verso
      `PersonalAutomationTool.Tests` in `AssemblyInfo.cs`: nessun altro cambiamento di superficie
      dell'assembly. Il valore atteso è stato **catturato dall'esecuzione reale** (non derivato a mano
      leggendo il codice), verificato manualmente, poi congelato. Due accorgimenti per evitare falsi
      positivi: il saluto (`DetermineSaluto`, dipende dall'ora corrente) è normalizzato prima del
      confronto; le locomotive di test usano matricole chiaramente inesistenti (99999, 88888) apposta
      — con matricole reali, un aggiornamento legittimo di `train_software.db` avrebbe fatto fallire
      il test per un motivo estraneo al suo scopo (vedi "Scoperte").
- [x] **3.6 — Virtualizzazione HomeView** (solo, **non** PassaggioConsegneView — vedi "Scoperte").
      Stesso pattern di `VerificheView` (§4.17): `CanContentScroll="True"`,
      `VirtualizingPanel.IsVirtualizing="True"`, `VirtualizationMode="Recycling"`, `ScrollUnit` a
      `Item` (default) per via del `RowDetailsTemplate` a righe di altezza variabile.
- [x] **3.7 — `DropShadowEffect` rimosso dalla navbar** (`MainWindow.xaml`, era item H, `Opacity=0.05`
      quasi impercettibile). **`BitmapScalingMode` deliberatamente non toccato**: l'intera app non ha
      **nessun** elemento `<Image>` (verificato via ricerca su tutti gli `.xaml`) — MaterialDesign
      usa icone vettoriali (`PackIcon`), PdfView usa emoji come testo. Cambiare quella proprietà
      sarebbe stato un no-op cosmetico spacciato per ottimizzazione: non applicato, non un intervento
      rimandato ma **valutato e scartato** perché non avrebbe fatto nulla.
- [x] **3.5 — Configurazione `PublishReadyToRun`** nel `.csproj`, dentro
      `<PropertyGroup Condition="'$(RuntimeIdentifier)' != ''">`: si attiva **solo** quando si
      pubblica specificando un RID esplicito (`dotnet publish -r win-x64`); `dotnet build`/`run`/`test`
      non passano mai un RID, quindi il blocco è verificato inerte per il lavoro quotidiano.
      `SelfContained` lasciato a `false` di default (framework-dependent): passare a `true` è una
      scelta operativa (come vengono aggiornate le macchine dei tecnici), lasciata esplicita a chi
      pubblica. Trimming **non** abilitato (XAML e `dynamic` verso Excel/Outlook usano riflessione).
      Non è stata eseguita una pubblicazione reale di verifica in questa sessione: da fare prima del
      primo rollout.
- [x] **2.7 (parziale) — `DatabaseManager._dbLock`** da statico a per-istanza (vedi §2.6/§2.7 sopra).
      **Non fatta** la parte più grossa dell'intervento (`DataTable` → record tipizzati): tocca
      trasversalmente una decina di punti (`CartelleView`, `DatabaseView`, `EmailService`,
      `VerificheViewModel`, `PdfView`...), è un refactor a sé, non un quick win — vedi "Rimandato".

**Scoperte della sessione** (emerse investigando/testando, non richieste esplicitamente):

1. **Bug latente in `ExcelViewModel.AutoFillReportFieldsAsync`, non corretto.** Il regex che estrae
   ticket/loco dalle sottocartelle (`new Regex($@"{SelectedTrain}\s*[-_]?\s*(\d{{3,4}})")`) usa
   **l'etichetta UI composita** `SelectedTrain` (uno fra 4 valori: `"E404P"`, `"ETR700"`,
   `"ETR1000 / 1000FH"`, `"ETR1000 I-F"`) come token letterale del pattern. Per
   `SelectedTrain == "ETR1000 / 1000FH"` questo non può **mai** trovare corrispondenza: nessuna
   sottocartella reale contiene la stringa letterale `"ETR1000 / 1000FH "` (le sottocartelle usano il
   `tipo` del DB — `"ETR1000"` o `"ETR1001FH"`, mai l'etichetta composita con lo slash). L'estrazione
   ricade quindi silenziosamente sui pattern di fallback generici (`StandaloneTicketRegex`/
   `StandaloneLocoRegex`), più deboli. **Non corretto in questa sessione**: sistemarlo bene
   richiederebbe provare i singoli `tipo` reali dietro l'etichetta composita, e non è stato possibile
   validare la correzione contro cartelle `LOG & DUMP` reali in questo ambiente. Candidato per una
   sessione futura con accesso a dati reali — vedi §6.3.
2. **Il ramo "conflitto con un file di destinazione già esistente" di `PdfRenamePlanner` (ed era già
   così nel `PdfView.BtnRinomina_Click` originale) è irraggiungibile con una scansione reale.** In
   `PdfView.LoadFolders`, **ogni** `.pdf` della cartella diventa per costruzione uno degli ingressi
   del piano (checked o unchecked): il suo percorso finisce quindi sempre fra gli `OldPath` delle
   operazioni pianificate, e il controllo `isOneOfOriginals` lo esclude sempre dal conflitto. Il test
   Tier 2 corrispondente (§6.1-bis, intervento 2.4) ha dovuto costruire il `TrainCardModel` a mano
   (non con una scansione reale) per esercitare questo ramo — dichiarando nel card un solo file
   mentre sul disco reale ne esiste anche un secondo, non dichiarato. Resta comunque una protezione
   reale contro una `TrainCards` non più allineata al disco (es. un aggiornamento di `AppWatcher` non
   ancora arrivato quando l'utente preme "Rinomina"): **non è codice morto da rimuovere**, è
   difensivo per uno scenario diverso da quello con cui era stato presumibilmente concepito.
3. **`PassaggioConsegneView` non può essere virtualizzata come `HomeView`/`VerificheView`.**
   `PassaggioConsegneView.xaml` (`RapportinoSheetBorder`) viene catturata **per intero** via
   `RenderTargetBitmap` in `PassaggioConsegnePdfExporter.ExportToPdf` per produrre il PDF del
   rapportino di turno. `ScrollViewer.CanContentScroll="False"` sui suoi 3 `DataGrid` non è quindi un
   controllo mancato: è ciò che oggi **garantisce** che tutte le righe (comprese quelle fuori
   viewport, incluse quelle aggiunte dall'utente — la tabella "Interventi" ha
   `CanUserAddRows="True"`) restino materializzate nell'albero visuale al momento dello scatto.
   Abilitare la virtualizzazione lì **tronca silenziosamente il PDF esportato** alle sole righe
   visibili a schermo in quel momento — l'esatto rischio di "output sbagliato in modo silenzioso" che
   guida le priorità di questo progetto. **Non toccata, con questa motivazione tecnica concreta**, non
   per genericamente "impatto basso" come nella valutazione precedente (item C, §6.4).

**Rimandato, con motivazione esplicita** (nessuno di questi è stato "dimenticato"):

| Voce | Perché rimandata |
|---|---|
| **1.1**, restanti 6 chiamanti di `LogDumpFolderName` | Vedi scoperta #1 e la nota su `TrainViewHelper` sotto: alcuni chiamanti hanno logica bespoke che già gestisce I-F/FH correttamente senza DB; altri (Excel) usano un'etichetta UI che non corrisponde a un `tipo` reale. Migrarli alla cieca senza nomi di cartella reali su cui validare è il rischio che questo progetto vuole evitare. |
| **1.2** — validazione preventiva in CARTELLE | Richiede decisioni di UX (quali controlli bloccano la creazione, che messaggio, dove appare) che spettano al committente, non un'implementazione unilaterale. |
| **1.6 / 4.4** — health-check percorsi all'avvio | Nuova superficie UI (dove appare, blocca l'avvio o solo avvisa) non specificata: stesso motivo di 1.2. |
| **1.7** — wrapper tipizzato su Outlook | Impatto già valutato **Basso** in §6.2; tocca `EmailService` **e** `PassaggioConsegneEmailService`; la parte più delicata (forzare Outlook a generare la firma via `Inspector`) è intrinsecamente legata a `dynamic`/COM e si presta male a un'interfaccia pulita senza perdere quel comportamento. Rapporto costo/beneficio sfavorevole rispetto al resto dello sprint. |
| **3.4** — `OpenXmlReader` SAX per Verifiche | Riscrittura del parsing di un percorso business-critical (alimenta l'autocompilazione di Passaggio di Consegne) senza un file Excel "Verifiche" reale su cui validare la nuova logica contro l'attuale (che gestisce ricerca euristica dell'header, merge di celle, ecc.). Rischio Medio già dichiarato in §6.2: non affrontabile alla cieca. |
| **Fase 3 per intero** (4.1 anteprima rinomine, 4.2 progress overlay, 4.3 annulla rinomina, 4.5 tastiera, 4.6 ricerca) | Sono **funzionalità nuove**, non ottimizzazioni: richiedono decisioni di prodotto/UX (layout del dialog di anteprima, semantica di annullamento, dove va la barra di ricerca) che questa sessione non ha. In particolare **4.3** ripropone le tabelle `renamer_config`/`renamer_queue`/`renamer_log`, residuo di una funzione mai completata di cui non è noto il progetto originale (§6.6): riusarle per un significato nuovo senza conoscerne l'intento è speculativo. |
| **2.7**, parte `DataTable` → record tipizzati | Tocca trasversalmente ~10 punti del codice (vedi sopra): un refactor a sé, non un quick win da infilare in coda a uno sprint già ampio. |
| Griglie di `PassaggioConsegneView` (3.6) | Vedi scoperta #3: motivo tecnico concreto (troncamento del PDF esportato), non solo "impatto basso". |
| `BitmapScalingMode` (3.7) | Vedi sopra: valutato e scartato, non rimandato — l'app non ha elementi `<Image>`, il cambiamento sarebbe un no-op. |

**Build/test alla chiusura dello sprint:** `dotnet build` sull'intera `.sln` → 0 errori; 0 warning su
`PersonalAutomationTool` e `PersonalAutomationTool.Tests` (i 2 warning NU1510 restano preesistenti in
`TestClosedXML`). `dotnet test` → **32/32 superati** (19 Tier 1 `LogDumpFolderName` + 11 Tier 2
`PdfRenamePlanner` + 2 golden-file `EmailService`).

### 6.1-ter Sprint 3 — debito dati residuo + Fase 3 (UX), con scelte di prodotto ricevute dal committente

Il committente ha aperto lo Sprint 3 chiedendo tre blocchi: 1) fix del regex `ETR1000 / 1000FH` in
`ExcelViewModel` (scoperta #1, §6.1-bis), 2) completamento di 2.7 (`DataTable` → record tipizzati),
3) l'intera Fase 3 (UX), questa volta con le decisioni di prodotto che in §6.1-bis mancavano — layout
del dialog di anteprima, semantica di "annulla", uso delle tabelle `renamer_*` — fornite direttamente
nella richiesta. **Blocco 1 non eseguito**: il messaggio di apertura sprint conteneva un placeholder
(`[INSERISCI QUI 2-3 ESEMPI REALI DI NOMI CARTELLA...]`) mai compilato. Indovinare il formato reale
della flotta ETR1000/1000FH sarebbe esattamente il rischio di "output silenziosamente sbagliato" che
guida le priorità di questo progetto (vedi premessa di §6): il fix resta bloccato, invariato da
§6.1-bis/§6.3. Blocci 2 e 3 completati.

**Blocco 2 — lettura tipizzata (intervento 2.7, completamento):**

- [x] **`DatabaseManager.Query<T>(string, Func<SqliteDataReader,T>, Dictionary?)`** — legge via
      `SqliteDataReader` e proietta ogni riga con `map`, senza mai materializzare un `DataTable`
      intermedio. Stessa politica di errore non distruttiva di `ExecuteQuery` (un errore SQL produce
      lista vuota, loggata su `Debug`, mai un'eccezione verso il chiamante).
- [x] **Scoperta preliminare che ha ridimensionato lo scope:** la stima "~10 punti" di §6.1-bis/§2.6
      era già superata da `FlotteCache` (§6.1-bis, intervento 3.2), che aveva consolidato 6 dei
      chiamanti diretti di `DatabaseManager.ExecuteQuery` (CartelleView ×3, VerificheViewModel,
      PdfView, EmailService ×2). Verificato con una ricerca su tutto il codice: i soli punti che
      ancora consumavano `DataTable` erano `FlotteCache.GetAll`, `RubricaDialog.LoadContactsFromDatabase`
      e `DatabaseManager.GetTableNames` — tutti e tre migrati a `Query<T>`.
- [x] **`DatabaseView` deliberatamente esclusa, con motivo tecnico** (stesso principio delle
      esclusioni già in §6.1-bis/§6.4, es. `BitmapScalingMode`): è un browser SQLite generico a schema
      arbitrario — l'utente sceglie la tabella da un ComboBox popolato a runtime, il `DataGrid` mostra
      ed edita colonne non note a compile time, `GetChanges`/`AcceptChanges` di `DataTable` è ciò che
      rende possibile l'editing in-place. Un record C# richiede una forma nota a compile time: forzarla
      qui eliminerebbe la funzione stessa della schermata (sfogliare *qualunque* tabella, comprese
      quelle aggiunte in futuro), non la migliorerebbe. `ExecuteQuery`/`DataTable` restano l'API
      corretta per questo solo punto, non un residuo dimenticato.
- [x] **Scoperta emersa scrivendo i test, non richiesta esplicitamente:** `flotte.treno` e
      `flotte.loco` sono colonne SQLite con storage class **INTEGER** (verificato con
      `PRAGMA table_info` sul `.db` reale spedito con l'app), non TEXT come si potrebbe assumere dai
      valori che contengono di solito. `SqliteDataReader.GetString()` lancerebbe un'eccezione su
      quelle colonne; il mapping usa `GetValue(i)?.ToString()`, che replica esattamente il
      comportamento (permissivo, senza assunzioni di tipo) di `DataRow["col"]?.ToString()` usato dal
      codice `DataTable` che questo intervento sostituisce. Coperto da un test dedicato
      (`DatabaseManagerTests.Query_ColonnaIntegerLetta_NonLanciaEProduceStringa`) apposta per non
      perdere questo vincolo in una futura modifica.
- [x] **5 test Tier 2** in `DatabaseManagerTests.cs`, su un vero file SQLite temporaneo
      (`Directory.CreateTempSubdirectory`, non il database di produzione): proiezione multi-riga,
      colonna INTEGER letta senza eccezioni, tabella inesistente → lista vuota (non eccezione),
      nessuna riga → lista vuota, `GetTableNames` sul nuovo `Query<T>`.

**Blocco 3 — Fase 3 UX (interventi 4.1, 4.2, 4.3), con le scelte di prodotto ricevute:**

- [x] **4.1 — Dialog di anteprima rinomine** (`core/Dialogs/RenamePreviewDialog.xaml(.cs)`). `Window`
      modale essenziale: `DataGrid` in sola lettura a due colonne ("Nome Attuale"/"Nuovo Nome",
      popolate con `Path.GetFileName` sui percorsi del piano già calcolato — il dialog non decide
      nulla, mostra solo l'esito di un planner esistente), pulsanti Conferma/Annulla. Helper statico
      `RenamePreviewDialog.Confirm(owner, operations, subtitle?)` per non duplicare la costruzione
      degli item nei 3 punti che lo richiamano. **Richiamato prima dell'esecuzione in:**
      `PdfView.BtnRinomina_Click` (dopo `PdfRenamePlanner.CreatePlan`, prima delle due fasi di
      `File.Move`) e in **entrambe** le operazioni di rinomina di HOME,
      `HomeViewModel.OnAggiornaTicket`/`OnAggiornaData` (non solo un sottoinsieme: sono le uniche due
      operazioni di HOME che rinominano cartelle). Per questo, la logica di calcolo di
      `OnAggiornaTicket`/`OnAggiornaData` è stata separata dall'esecuzione (stesso principio
      dell'intervento 2.1 dello Sprint 2 per `PdfRenamePlanner`): un primo `Task.Run` calcola la lista
      `(OldPath, NewPath)` senza scrivere nulla, il dialog la mostra, un secondo `Task.Run` esegue solo
      se confermato. **Comportamento invariato quando l'utente conferma**: stessi identici
      `Directory.Move`, nello stesso ordine, con lo stesso identico calcolo di "quali cartelle
      rinominare" del codice originale — l'unica differenza osservabile è il passaggio di conferma in
      più. `HomeViewModel` non ha una `Window` propria: usa pragmaticamente
      `Application.Current?.MainWindow` come owner, coerente con lo stile già presente nella stessa
      classe (i `MessageBox.Show` in `OnZip`/`OnElimina` sono già chiamati direttamente dal
      ViewModel, senza servizio di dialog — vedi §2.2, pattern "ibrido, non uniforme").
- [x] **4.2 — Overlay di progresso riutilizzabile** (`core/Controls/ProgressOverlay.xaml(.cs)`).
      Estratto dall'overlay originale di `ExcelView` (stessa grafica: `ProgressBar` circolare
      MaterialDesign + messaggio sotto), ora `UserControl` con due `DependencyProperty` (`IsBusy`,
      `Message`) invece di essere legato al `DataContext` di una singola view — funziona sia da
      binding MVVM (`IsBusy="{Binding IsLoading}"`, usato da `ExcelView`/`HomeView`) sia da
      assegnazione diretta in code-behind puro (`PdfView`, che non ha un ViewModel).
      `Report(current, total, verbo)` centralizza il formato "Elaborazione 3 di 12...". Applicato a:
      - `ExcelView` — sostituito l'overlay inline con il nuovo controllo, **zero cambi di
        comportamento visibile** (stessi colori, stessa struttura, ancora legato a
        `IsLoading`/`LoadingMessage` di `ExcelViewModel`, non toccati).
      - `HomeViewModel` — nuove proprietà `IsLoading`/`LoadingMessage` (stessa forma di
        `ExcelViewModel`, per coerenza); applicate a `OnZip` (conteggio sulle sottocartelle
        archiviate) e `OnLogDumpRete` (conteggio sui file ZIP processati) via `IProgress<(int,int)>`
        — cattura il `SynchronizationContext` della UI alla costruzione, quindi `Report()` dal thread
        pool marshalla automaticamente sul dispatcher, senza bisogno di `Dispatcher.Invoke` espliciti
        (vedi il debito tecnico su questo stesso rischio in §6.5). `OnElimina` usa l'overlay **senza
        conteggio** (solo messaggio "Eliminazione in corso..."): `Directory.Delete(path, true)` è una
        singola chiamata indivisibile, ed enumerare prima i file solo per calcolare un totale
        raddoppierebbe l'I/O senza un beneficio reale — scelta deliberata, non un conteggio dimenticato.
      - `PdfView.BtnRinomina_Click` — overlay guidato da code-behind (`LoadingOverlay.IsBusy`/`.Report(...)`),
        conteggio sui due passaggi del rinomina atomica (temporaneo, poi finale — §5.7, non semplificato).
- [x] **4.3 — Annulla rinomina** (`core/RenamerLog.cs`). Le tabelle `renamer_config`/`renamer_queue`/
      `renamer_log` esistevano nel `.db` ma non erano mai scritte da nessun modulo (residuo annotato in
      §6.6): usata solo `renamer_log`, con lo schema reale verificato via `PRAGMA table_info`
      (`id, ts, file_sig, old_path, new_path, template, result` — **nessuna colonna "batch"**). Design:
      tutte le righe scritte da una singola rinomina condividono lo stesso `ts` (timestamp al decimo
      di microsecondo, generato una volta per batch), che funge da chiave di raggruppamento;
      `template` porta la categoria (`PdfRename`/`HomeTicket`/`HomeData`), per poter filtrare l'ultimo
      batch di un tipo specifico — l'annulla di PDF non deve poter annullare per sbaglio l'ultima
      rinomina di HOME, e viceversa. `UndoLastBatch` inverte `new_path → old_path` (`File.Move` per
      `PdfRename`, `Directory.Move` per le due varianti di HOME) e ripulisce il batch dal log **solo
      se tutte** le operazioni sono state ripristinate senza errori — se anche una fallisce (percorso
      spostato o modificato fuori dall'app nel frattempo), il log resta intatto per un nuovo tentativo
      dopo correzione manuale, e l'errore è riportato per nome file. Pulsante "Annulla ultima
      rinomina" aggiunto sia in `PdfView` (annulla solo `PdfRename`) sia in `HomeView`
      (`AnnullaRinominaCommand`, annulla l'ultimo fra `HomeTicket`/`HomeData`). Ogni metodo pubblico
      accetta un `dbPath` opzionale (`null` → percorso reale) per permettere ai test di puntare a un
      file temporaneo invece del database di produzione — i chiamanti applicativi non lo passano mai.
- [x] **7 test Tier 2** in `RenamerLogTests.cs`: registrazione e rilettura di un batch, filtro per
      categoria (un batch `HomeTicket` non deve comparire cercando `PdfRename`), scelta del più
      recente fra più categorie richieste, annulla di un file (`File.Move` inverso + pulizia log),
      annulla di una cartella (`Directory.Move`, non `File.Move` — verificato esplicitamente),
      destinazione mancante (percorso rinominato già spostato altrove: errore descrittivo, log **non**
      ripulito), nessun batch da annullare (esito "non trovato", non un'eccezione).

**Cosa non è stato verificato in questa sessione (limite dell'ambiente, non del codice):** l'eseguibile
è stato avviato e resta in esecuzione senza eccezioni per alcuni secondi (verifica di startup), ma
senza un tool di automazione UI Windows non è stato possibile navigare a PDF/HOME/EXCEL e verificare a
schermo il dialog di anteprima, l'overlay con conteggio e il pulsante di annulla. **La checklist
manuale in §7.1 è stata estesa con i punti 16-18 per coprire esattamente questo** prima di considerare
lo sprint chiuso sul serio.

**Build/test alla chiusura dello sprint:** `dotnet build` sull'intera `.sln` → 0 errori, 0 warning
(stessi 2 NU1510 preesistenti in `TestClosedXML`). `dotnet test` → **44/44 superati** (32 di Sprint 1+2
+ 5 `DatabaseManagerTests` + 7 `RenamerLogTests`).

### 6.1-quater Sprint 3, coda — fix del parsing EXCEL (scoperta #1, **RISOLTA**)

Il committente ha fornito i nomi di cartella reali che mancavano da due sprint, sbloccando l'intervento
con priorità più alta della §6.3. **Non è stato necessario indovinare nulla:** i quattro esempi sono
stati usati direttamente come casi di test.

**Nomi reali di riferimento** (da conservare: sono l'unica fonte verificata del formato su disco per
questa flotta):
```
SR1234567 LOG  ETR1000   119 02.02CR3    230826 Carlomagno
SR1234568 DUMP ETR1000   119 02.02CR3HR  230826 Carlomagno
SR1234567 LOG  ETR1000IF 128 BISTANDARD  230826 Carlomagno
SR1234567 DUMP ETR1000IF 128 BISTANDARD  230826 Carlomagno
```

**Il difetto era più ampio di quanto documentato.** La scoperta #1 (§6.1-bis) citava solo
`"ETR1000 / 1000FH"`. Verificando contro i nomi reali risulta che **due** delle quattro etichette della
ComboBox erano rotte, non una:

| Etichetta ComboBox | Token cercato dal pattern originale | Forma reale su disco | Esito |
|---|---|---|---|
| `E404P` | `E404P` | `E404P` | funzionava |
| `ETR700` | `ETR700` | `ETR700` | funzionava |
| `ETR1000 / 1000FH` | `ETR1000 / 1000FH` (con lo slash) | `ETR1000` | **mai un match** |
| `ETR1000 I-F` | `ETR1000 I-F` (con spazio e trattino) | `ETR1000IF` (attaccato) | **mai un match** |

In entrambi i casi rotti l'estrazione ricadeva sui fallback generici `\b\d{7,8}\b` (ticket) e
`\b\d{2,4}\b` (loco), che prendono il **primo numero della lunghezza giusta ovunque si trovi nel
nome** — compresi i numeri dentro la versione software (`02.02CR3`) o la data. Su una cartella reale
`ETR1000IF 128 BISTANDARD` il fallback loco `\b\d{2,4}\b` restituiva `128` solo per fortuna
posizionale; su `ETR1000 119 02.02CR3` poteva restituire `02` — un numero di locomotore inesistente
scritto nel Report Interventi ufficiale, in silenzio.

**La correzione (`modules/excel/ExcelFolderParser.cs`, classe pura, zero WPF).** L'estrazione non parte
più dall'etichetta UI: usa **`LogDumpFolderName.TryParse`**, il parser condiviso già adottato da
`PdfView` nello Sprint 1 — questo è **l'ottavo chiamante migrato dell'intervento 1.1**, e il primo dopo
il pilota. Il parser localizza i campi per *posizione* nella grammatica
`SR{ticket} {LOG|DUMP} {tipo} {loco} {software} {ddMMyy} {utente}` e non ha quindi bisogno di sapere
quale voce di ComboBox è selezionata: il difetto di classe "etichetta UI usata come token di ricerca"
è eliminato alla radice, non aggirato.

- **`ETR1000IF` non è in `flotte`** (la tabella registra `"ETR1000 I-F"`, con spazio e trattino:
  verificato con una query sul `.db` reale, insieme agli altri quattro tipi — `E404P`, `ETR1000`,
  `ETR1001FH`, `ETR700`). Non è un problema e **non è stato necessario modificare il database**: per un
  tipo assente dall'elenco, `TryParse` ricade sul primo token, che in questa grammatica *è* esattamente
  il tipo cercato. Verificato dai test su entrambe le forme.
- **Due guardie sulla forma dei campi estratti** (`TryExtractTicketAndLoco` restituisce `null` se il
  ticket non è di sole cifre o la loco non è 2-4 cifre). Non sono ridondanti: `LogDumpFolderName` è più
  permissivo dei regex che sostituisce (accetta un ticket `\S+`), quindi senza guardie un nome anomalo
  produrrebbe ora un valore che prima veniva scartato — un cambiamento di comportamento silenzioso su
  dati imprevisti, cioè lo stesso genere di rischio che l'intervento vuole eliminare. Con le guardie il
  risultato è **identico a prima ovunque il vecchio codice funzionasse**, e corretto dove cadeva sul
  fallback.
- **Percorso di riserva conservato.** Per i nomi fuori grammatica la logica a regex preesistente resta
  invariata, ma il pattern del treno è ora costruito da `BuildLocoRegex` sui **token reali su disco**
  (con `Regex.Escape` su ciascuno — l'etichetta veniva prima interpolata grezza in un pattern) invece
  che sull'etichetta. Applicato ai **due** punti che avevano lo stesso difetto: il ciclo sulle
  sottocartelle (`minDigits: 3`) e la ricerca del campo "SN" (`minDigits: 2`). Le due soglie diverse
  dei chiamanti originali sono preservate, non uniformate.

**Tensione con la richiesta, risolta a favore dell'invariante — da sapere.** La richiesta affermava che
l'etichetta `ETR1000 / 1000FH` «corrisponde nei percorsi disco reali a `ETR1000` o `ETR1000IF`». Presa
alla lettera unirebbe due flotte che l'applicazione tiene **deliberatamente separate**: `MatchesTrain`
esclude già oggi `1000IF`/`I-F` da quell'etichetta e li assegna alla voce distinta `ETR1000 I-F`
(§5.3), le due voci portano a report Excel con un numero di colonne diverso (`maxCol` 24 contro 27,
§5.4) e a cartelle Hitachi diverse. Unirle farebbe scrivere righe nel report sbagliato — l'esatto
"output silenziosamente sbagliato" della premessa di §6. **La separazione è stata quindi preservata** e
ogni etichetta gestisce i propri token (`ETR1000 I-F` → `ETR1000IF`, che prima non funzionava: la
richiesta è soddisfatta nella sostanza, entrambe le forme sono ora riconosciute, ciascuna sotto la
propria voce). Un test dedicato blocca la regressione in entrambe le direzioni. **Se l'intenzione fosse
davvero unificare le due voci di ComboBox, è una decisione di prodotto separata** che tocca anche
`MatchesTrain`, `maxCol` e i percorsi Hitachi: va affrontata come tale, non come effetto collaterale di
un fix di parsing.

**ROTABILE — seconda correzione, emersa dal chiarimento sulle tre flotte (§5.3-bis).** Il campo
`ROTABILE` del report veniva compilato **dall'etichetta della ComboBox invece che dal treno reale**
della cartella: sotto la voce `ETR1000 / 1000FH` una cartella `ETR1001FH` otteneva il rotabile
`ETR1000` — valore sbagliato scritto nel Report Interventi ufficiale, in silenzio. Il ramo campo-libero
(non ComboBox) era peggio: scriveva letteralmente `"ETR1000 / 1000FH"`, slash incluso, che non è il
nome di alcun rotabile. Corretto con `ResolveActualTrainType` (il `tipo` realmente presente nei nomi
delle sottocartelle, via `LogDumpFolderName.TryParse`) e `SelectRotabileOption`, che sceglie l'opzione
della convalida dati corrispondente alla variante reale. Il confronto è per *marcatori di variante*
(FH / IF) e non per sottostringa, perché `"ETR 1000 FH"` contiene `"ETR 1000"`: senza questa
distinzione l'errore si verificava anche al contrario, assegnando la variante FH a una cartella
ETR1000 pura. **Nessuna regressione possibile:** se il foglio non offre un'opzione distinta per la
variante (report che elenca solo `"ETR 1000"`), la funzione restituisce `null` e valgono esattamente
i criteri di selezione preesistenti.

**39 test Tier 1** in `Modules/Excel/ExcelFolderParserTests.cs`, tutti su funzioni pure: i 4 nomi reali
(ticket e loco, via `[Theory]`), tipo e `LOG`/`DUMP` sugli stessi 4, software non confuso con la loco,
esclusione delle forme I-F dall'etichetta non-I-F **e** viceversa, ordinamento dei token per lunghezza
decrescente, non-cattura della loco di una cartella I-F sotto l'etichetta non-I-F, invarianza di
`ETR700` (che già funzionava), nomi fuori grammatica → `null`, le due guardie ticket/loco non numerici,
il riconoscimento della variante FH come tipo distinto da `ETR1000`, e i casi ROTABILE nelle due
direzioni (FH non prende `ETR 1000`; ETR1000 puro non prende `ETR 1000 FH`) più il caso "foglio senza
varianti → nessun cambiamento".

> ⚠️ **Limite noto sui nomi FH.** Il committente ha fornito nomi di cartella reali solo per `ETR1000`
> ed `ETR1000IF`. Per la variante FH i test usano `ETR1001FH` — che **non è inventato**, è il valore
> reale della colonna `tipo` in `flotte` — e la forma alternativa `1000FH`. Se le cartelle FH reali
> usassero un token diverso, quei test vanno aggiornati con quello: **portare un nome di cartella FH
> reale** è il tassello mancante per chiudere del tutto questa verifica.

**Build/test:** `dotnet build` → 0 errori, 0 warning (stessi 2 NU1510 preesistenti in `TestClosedXML`).
`dotnet test` → **64/64 superati**.

**Cosa resta da verificare a mano** (non verificabile in questo ambiente, manca l'accesso alle cartelle
`LOG & DUMP` reali): punto 19 della checklist §7.1 — aprire EXCEL su una cartella reale delle due
flotte ETR1000 e confrontare TICKET/LOCO/SN autocompilati con quelli attesi.

### 6.2 Le 4 macro-aree della roadmap strategica

Elaborata come risposta alla domanda "se fossi il Lead Architect, cosa faresti dopo l'audit
prestazionale". Ogni intervento è marcato **Impatto** (Alto/Medio/Basso) · **Sforzo**
(Rapido/Medio/Ristrutturazione) · **Rischio di regressione** (Basso/Medio/Alto). ✅ = completato
(Sprint 1, §6.1, o Sprint 2, §6.1-bis); 🟡 = completato **parzialmente**, con il resto rimandato
per un motivo esplicito (vedi la tabella "Rimandato" in §6.1-bis); senza segno = non applicato.

#### 2.1-A Stabilità & Resilienza Operativa

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 1.1 | 🟡 **Parser/formatter unico** per i nomi `LOG & DUMP` (2/8 chiamanti migrati: `PdfView` Sprint 1, `ExcelViewModel` Sprint 3 via `ExcelFolderParser`; restano 5, caso per caso) | **Alto** | Medio | Basso |
| 1.2 | **Validazione preventiva alla creazione** (in CARTELLE) | **Alto** | Rapido | Basso |
| 1.3 | **Confidenza di parsing + avviso visibile** invece di `catch {}` | **Alto** | Medio | Basso |
| 1.4 | ✅ **PID-tracking Excel con terminazione forzata di sicurezza** (Sprint 2 — versione più stretta di "isolamento in processo figlio", vedi §6.1-bis) | **Alto** | Medio | Medio |
| 1.5 | Sostituzione interop Excel con OpenXML diretto | Alto | Ristrutturazione | **Alto** |
| 1.6 | **Health-check dei percorsi** all'avvio | Medio | Rapido | Basso |
| 1.7 | Wrapper tipizzato sopra Outlook (`dynamic` → interfaccia) | Basso | Medio | Basso |

Il **keystone è 1.1**: oggi l'app scrive quei nomi in un punto (`CartelleView.BtnCrea_Click`) e li
rilegge in almeno otto altri, con logiche indipendenti; scrittura e lettura possono divergere senza
che nessuno se ne accorga. Un tipo unico con `TryParse`/`Format` rende la divergenza
strutturalmente impossibile — **ma** lo Sprint 2 ha scoperto che completarlo non è la semplice
ripetizione del pilota: vedi le "Scoperte" in §6.1-bis. **1.2 vale più di 1.1**: validare al momento
della creazione costa una frazione del tollerare in lettura — CARTELLE ha già il pattern giusto
(anteprima live del nome), basta estenderlo con controlli su formato ticket, cartella già esistente,
loco presente in `flotte`. **1.4** è stato applicato in versione più stretta di quanto originariamente
proposto (PID-tracking con kill forzato, non un vero processo separato): più semplice, stesso
beneficio pratico (nessun EXCEL.EXE orfano), senza il costo di una vera architettura a due processi.

#### 2.1-B Architettura & Debito Tecnico

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 2.1 | ✅ **Separare "decidere" da "eseguire"** (Sprint 2 — `PdfRenamePlanner`, vedi §6.1-bis) | **Alto** | Medio | Basso |
| 2.2 | ✅ **Progetto di test + Tier 1** (Sprint 1) | **Alto** | Rapido | **Nullo** |
| 2.3 | ✅ **Percorsi Hitachi hardcoded → config** (Sprint 1, `hitachiDir` di `ExcelViewModel`) | **Alto** | Rapido | Basso |
| 2.4 | ✅ **Test Tier 2 su workspace temporaneo reale** (Sprint 2 — 11 test su `PdfRenamePlanner`) | Alto | Medio | Nullo |
| 2.5 | ✅ **Golden-file test sul corpo HTML delle email** (Sprint 2) | **Alto** | Rapido | Nullo |
| 2.6 | Completamento MVVM sui moduli code-behind | Basso | Ristrutturazione | **Alto** |
| 2.7 | ✅🟡 `DatabaseManager`: lock per istanza (Sprint 2) + `Query<T>` tipizzato (Sprint 3); `DatabaseView` esclusa con motivo tecnico (browser a schema arbitrario) | Medio | Medio | Medio |

**2.6 è deliberatamente scartato come obiettivo in sé:** convertire Cartelle, PDF, Database e i
dialog a MVVM è un big-bang senza beneficio visibile all'utente, su codice che oggi funziona. Al suo
posto, **2.1**: estrarre la *logica* lasciando il code-behind come guscio sottile — fatto nello
Sprint 2 per `PdfView.BtnRinomina_Click` (un algoritmo non banale: due fasi con nomi temporanei GUID,
rilevamento collisioni, incremento ticket per gli NC, ora in `PdfRenamePlanner`, testabile senza WPF
e coperto da 11 test Tier 2). **2.5**, fatto nello stesso sprint, aveva il miglior rapporto
valore/sforzo dei test non ancora scritti: `EmailService.BuildHtmlBody` produce ciò che arriva al
cliente, e il golden-file test ora congela quell'output.

**Strategia di test a 3 livelli** (Tier 1 e Tier 2 avviati, Tier 3 ancora da affrontare):
- **Tier 1 — funzioni pure su stringhe** (`LogDumpFolderName` fatto; restano `BuildSubject`,
  `MatchesTrain`, `AreTrainTypesCompatible`, `ExtractLocosFromFolder`): nessuna astrazione, basta
  spostarle fuori dalle classi WPF. È dove vivono i bug ed è dove i test costano meno.
- **Tier 2 — file system**: `PdfRenamePlanner` fatto (Sprint 2, alberi di cartelle reali via
  `Directory.CreateTempSubdirectory`). Lo stesso approccio si applica al resto della logica di
  parsing/scansione di `LOG & DUMP` non ancora estratta.
- **Tier 3 — COM**: non si testa, si isola dietro `IReportWriter`/`IMailComposer` e si verifica
  tutto fino al confine. Non affrontato: dipende da 1.7 (wrapper Outlook), rimandato.

#### 2.1-C Performance & Efficienza Legacy

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 3.1 | ✅ **Polling Verifiche: 5s → 60s** (Sprint 1) | **Alto** | Rapido | Basso |
| 3.2 | ✅ **`flotte` in dizionario in memoria** all'avvio (Sprint 2 — `FlotteCache`) | Medio | Rapido | Basso |
| 3.3 | ✅ **Indici SQLite** su `(tipo, loco)` e `loco` (Sprint 2) | Medio | Rapido | Basso |
| 3.4 | **Lettura Verifiche con `OpenXmlReader`** (SAX) invece di ClosedXML | **Alto** | Medio | Medio |
| 3.5 | ✅🟡 **Configurazione ReadyToRun** (Sprint 2 — solo la config nel `.csproj`, nessuna pubblicazione reale ancora eseguita) | Medio | Rapido | Basso |
| 3.6 | 🟡 Virtualizzazione griglie residue — **fatta solo HomeView**, PassaggioConsegneView esclusa con motivo tecnico (Sprint 2, vedi §6.1-bis) | Basso | Rapido | Basso |
| 3.7 | 🟡 `DropShadowEffect` navbar rimosso (Sprint 2); `BitmapScalingMode` valutato e scartato (nessun `<Image>` nell'app) | Basso | Rapido | Basso |

**3.4** resta il residuo più grosso non affrontato sul percorso Verifiche: ClosedXML carica l'intero
workbook in un object model per estrarre tre colonne da un foglio; una lettura SAX con
`OpenXmlReader` taglierebbe la memoria di un ordine di grandezza. Non affrontato in Sprint 2 per
assenza di un file "Verifiche" reale su cui validare la riscrittura (vedi §6.1-bis). **3.5**: la
config c'è, ma valutare il deployment self-contained (elimina la dipendenza dal runtime installato
su macchine d'ufficio con permessi ristretti) resta una decisione operativa aperta, da prendere
prima del primo rollout reale — non aggiungere il trimming, XAML e `dynamic` usano riflessione.

#### 2.1-D UX & Nuove Feature

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 4.1 | ✅ **Anteprima "cosa cambierà"** prima delle rinomine massive (Sprint 3 — PDF e HOME) | **Alto** | Medio | Basso |
| 4.2 | ✅ **Feedback di avanzamento** su zip / sposta in rete / elimina (Sprint 3) | **Alto** | Rapido | Basso |
| 4.3 | ✅ **Annulla rinomina** (Sprint 3 — `renamer_log`, prima presente e inutilizzata, vedi §6.6) | Medio | Medio | Basso |
| 4.4 | Pannello health-check percorsi (vedi 1.6) | Medio | Rapido | Basso |
| 4.5 | Flusso da tastiera: acceleratori, tab order, Invio per confermare | Medio | Rapido | Basso |
| 4.6 | Ricerca globale su `LOG & DUMP` | Medio | Medio | Basso |

**4.1, 4.2 e 4.3 sono stati implementati nello Sprint 3** (§6.1-ter), con le decisioni di prodotto
(layout del dialog, uso di `renamer_log`) fornite direttamente dal committente in apertura di sprint —
il paragrafo seguente descrive il ragionamento originale che ha guidato l'implementazione. **4.1** era
la difesa più efficace contro il rischio descritto nella premessa di §6, e non ha richiesto di rendere
i parser perfetti: "Rinomina" in PDF e "Aggiorna ticket"/"Aggiorna Data" in HOME rinominavano in blocco
senza mostrare nulla prima; il dialog "vecchio → nuovo" ora intercetta un parsing sbagliato prima che
diventi un'email o un file mal nominato. **4.2**: `ExcelView` aveva già l'overlay giusto (`IsLoading` +
`ProgressBar`, legata correttamente in §4.15), è stato estratto in componente riutilizzabile e
generalizzato con un conteggio ("3 di 12") sulle altre operazioni pesanti di HOME.

### 6.3 Da dove ripartire nel prossimo sprint

Priorità aggiornata dopo lo Sprint 3 (vedi §6.1-ter per il perché di questo ordine):

1. ~~**Bug latente `ExcelViewModel` (scoperta #1, §6.1-bis)**~~ — **RISOLTO nello Sprint 3**
   (§6.1-quater), sbloccato dai nomi di cartella reali forniti dal committente. Verificato che il
   difetto riguardava **due** etichette e non una sola. Resta da eseguire la sola verifica sul campo
   (punto 19 di §7.1): aprire EXCEL su una cartella reale delle due flotte ETR1000 e confrontare
   TICKET/LOCO/SN autocompilati con quelli attesi.
2. **1.1, i restanti 5 chiamanti di `LogDumpFolderName`** (erano 6: `ExcelViewModel` è stato migrato
   nello Sprint 3 via `ExcelFolderParser`) — ma non "uno qualsiasi al prossimo turno": vanno valutati
   **caso per caso**, non in blocco. `EmailService.BuildSubject` è il candidato più pulito (nessuna
   etichetta UI composita coinvolta, solo parsing posizionale con lo stesso schema I-F/FH di
   `LogDumpFolderName`). `TrainViewHelper` invece **non è un miglioramento ovvio**: la sua logica
   bespoke (`ExtractLocosAdvanced`) già gestisce I-F/FH correttamente senza bisogno del DB; migrarla a
   `LogDumpFolderName.TryParse` richiederebbe aggiungere una dipendenza dal DB (per `knownTypes`) a un
   helper oggi puramente file-system, per un beneficio di manutenibilità non di correttezza — vale la
   pena, ma è una scelta deliberata, non un drop-in.
   **Lezione dallo Sprint 3, da riusare:** la migrazione di `ExcelViewModel` è riuscita perché i nomi
   reali erano disponibili come casi di test. Prima di migrare i prossimi chiamanti, procurarsi i nomi
   reali che *quel* chiamante incontra.
3. **3.4 (SAX Verifiche)** — stesso principio: portare un file "Verifiche" reale (anche anonimizzato)
   sblocca la riscrittura in sicurezza.
4. **Verifica manuale del Blocco 3 dello Sprint 3** (§6.1-ter, punti 16-18 di §7.1): dialog di
   anteprima, overlay con conteggio e annulla rinomina sono stati verificati solo per compilazione,
   test automatici e avvio senza eccezioni — non a schermo, per assenza di un tool di automazione UI
   in questo ambiente. Prima di considerare la Fase 3 davvero chiusa, eseguire a mano i tre scenari.
5. **4.5 (flusso da tastiera) e 4.6 (ricerca globale)** — uniche voci rimaste della Fase 3 UX, non
   ancora richieste: 4.1/4.2/4.3 sono state completate nello Sprint 3.

### 6.4 Criticità note **non** risolte (richiedono una scelta esplicita, perché cambierebbero il comportamento visibile)

> Le ex-voci **A** (scorrimento moltiplicato) e **B** (virtualizzazione `VerificheView`) sono state
> approvate e applicate: vedi §4.16 e §4.17. **C** e **H** sono state affrontate nello Sprint 2
> (§6.1-bis); **I** è stata valutata e scartata (non applicabile, non "da fare"). Restano aperte le
> 4 seguenti: **D, E, F, G**.

**C. `ScrollViewer.CanContentScroll="False"`** su HomeView e sui 3 DataGrid di PassaggioConsegne —
**risolta per HomeView, esclusa per PassaggioConsegneView con motivo tecnico concreto (Sprint 2)**.
HomeView virtualizzata con lo stesso pattern di `VerificheView` (`ScrollUnit` a `Item`, non `Pixel`,
proprio per la ragione di maniglia-scrollbar-instabile ipotizzata qui). PassaggioConsegneView
**non** virtualizzata: la sua griglia viene catturata per intero via `RenderTargetBitmap` per
l'export PDF del rapportino — virtualizzarla rischierebbe di troncare silenziosamente righe fuori
viewport dal PDF esportato. Vedi §6.1-bis, scoperta #3, per il dettaglio.

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

**H. `MainWindow.xaml` — `DropShadowEffect` sulla navbar — risolta (Sprint 2).**
Era un effetto a pixel shader su un `Border` a tutta larghezza (`Opacity="0.05"`, appena percettibile).
Rimosso; il bordo inferiore del `Border` (`BorderThickness="0,0,0,1"`) resta a marcare la navbar.
Non toccati gli altri usi di `DropShadowEffect` nell'app (`PassaggioConsegneView` — sulla stessa
`RapportinoSheetBorder` catturata per il PDF, quindi parte dello stile del documento esportato, non
solo chrome UI — e le viste email/dialog): non auditati in questa sessione, lasciati come sono.

**I. `RenderOptions.BitmapScalingMode="HighQuality"` — valutata e scartata, non "da fare" (Sprint 2).**
Verificato (ricerca su tutti gli `.xaml`): l'app **non ha alcun elemento `<Image>`**. MaterialDesign
usa icone vettoriali (`PackIcon`, non bitmap), `PdfView` usa emoji come testo. Questa proprietà
influenza solo lo scaling di contenuto bitmap: senza `<Image>` da scalare, cambiarla non avrebbe
alcun effetto visivo né prestazionale misurabile. Se in futuro venissero aggiunte immagini reali
all'interfaccia, rivalutare a quel punto.

### 6.5 Debito tecnico / igiene del repository

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
- [x] **`DatabaseManager._dbLock` da statico a per istanza (Sprint 2).** Prima serializzava ogni
      accesso al database dell'intero processo, anche fra file `.db` diversi, senza motivo.
- [x] **`DatabaseManager.Query<T>` tipizzato, senza `DataTable` intermedio (Sprint 3).** Vedi §6.1-ter
      e §2.6. `DatabaseView` resta su `ExecuteQuery`/`DataTable`, con motivo tecnico esplicito
      (browser a schema arbitrario), non per pigrizia.
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
- [~] **Test automatici — avviati su tre livelli, non completi.** `PersonalAutomationTool.Tests`
      (§6.1/§6.1-bis/§6.1-ter/§6.1-quater): 19 test Tier 1 (`LogDumpFolderName`), 11 Tier 2
      (`PdfRenamePlanner`, alberi di cartelle reali), 2 golden-file (`EmailService.BuildHtmlBody`),
      5 Tier 2 (`DatabaseManagerTests`, SQLite temporaneo reale), 7 Tier 2 (`RenamerLogTests`, idem),
      39 Tier 1 (`ExcelFolderParserTests`, sui **nomi di cartella reali**) — **83 in tutto**. Restano
      da coprire: `ExtractLocosFromFolder`, `BuildSubject`, `AreTrainTypesCompatible`, `MatchesTrain`
      (Tier 1, non dipendono da `LogDumpFolderName`, possono procedere in parallelo a §6.3); Tier 3
      (COM) non affrontato, dipende da 1.7 (wrapper Outlook), rimandato. **Non testata da xUnit** (per costruzione, sono WPF): `RenamePreviewDialog` e
      `ProgressOverlay` — gusci sottili senza logica di decisione, stessa categoria di `PdfView` prima
      dell'estrazione di `PdfRenamePlanner`; verificarli richiede la checklist manuale (§7.1, punti
      16-18).

### 6.6 Idee funzionali emerse dal codice (non richieste, solo annotate)
- ~~Le tabelle `renamer_config` / `renamer_queue` / `renamer_log` esistono in `train_software.db` ma
  nessun modulo dell'app le usa~~ — **`renamer_log` è ora scritta e letta da `core/RenamerLog.cs`**
  (Sprint 3, §6.1-ter, intervento 4.3). `renamer_config` (1 riga preesistente, mai letta da nessun
  modulo) e `renamer_queue` (vuota) restano residui non usati: la loro funzione originale resta
  sconosciuta, non riutilizzata per non essere speculativa.
- `ExcelViewModel.Trains` è una lista hard-coded di 4 flotte, mentre gli altri moduli ne gestiscono 8:
  ETR421/521/522 non hanno modulo Excel.

---

## 7. Come riprendere il lavoro

### 7.1 Build, test ed esecuzione

```bash
dotnet build PersonalAutomationTool/PersonalAutomationTool.csproj
```
```bash
dotnet test PersonalAutomationTool.Tests/PersonalAutomationTool.Tests.csproj
```
```bash
dotnet run --project PersonalAutomationTool/PersonalAutomationTool.csproj
```

Il progetto di test (§6.1) non richiede Windows in senso stretto per compilare, ma
`TargetFramework=net10.0-windows` (per la `ProjectReference` verso l'app WPF) sì: va eseguito nello
stesso ambiente della build principale.

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
9. **PDF** (migrazione pilota §6.1) → rinominare una cartella con tipo a due parole reale
   (`ETR1000 I-F`) e verificare che il nome generato sia identico a quello che produceva la
   versione precedente (tipo, loco, data, utente nella posizione corretta).
10. **EXCEL** (quick win §6.1) → "Sposta Report" su ognuna delle 4 flotte con pulsante attivo
    (ETR700, E404P, ETR1000/1000FH, ETR1000 I-F): verificare che la cartella Hitachi individuata
    sia quella corretta per ciascuna, e che al primo avvio compaia `hitachi_paths.json` accanto
    all'eseguibile con i 4 percorsi attesi.
11. **PDF** (estrazione `PdfRenamePlanner`, §6.1-bis) → ripetere il punto 3 e in più: un caso a 2 PDF
    non spuntati con un file `.txt` presente (verificare il nome "Checklist ..."), un caso con più
    file NC (verificare l'incremento del ticket), un caso senza alcuna cartella LOG (messaggio di
    attenzione, non di errore).
12. **CARTELLE / EXCEL / EMAIL / VERIFICHE** (`FlotteCache`, §6.1-bis) → verificare che
    treno/software si autocompilino ancora correttamente in tutti i punti che li usavano; poi,
    da `DATABASE`, modificare una riga di `flotte` e verificare che la modifica si rifletta (il
    file `.db` cambia mtime, la cache si invalida automaticamente al prossimo utilizzo).
13. **EXCEL** (PID-tracking, §6.1-bis) → alcuni salvataggi consecutivi con "Scrivi report", poi
    controllare in Gestione attività che non resti né un processo EXCEL.EXE visibile né uno
    "fantasma" (verificabile forzando un errore, es. chiudendo manualmente la finestra Excel
    invisibile da Gestione attività a metà operazione, e controllando che comunque non resti nulla
    dopo il margine di 3 secondi).
14. **HOME** (virtualizzazione, §6.1-bis) → con un numero cospicuo di manutenzioni in sospeso,
    verificare che lo scorrimento della tabella resti fluido e che l'espansione delle sottocartelle
    (`RowDetailsTemplate`) continui a funzionare correttamente riga per riga.
15. **PASSAGGIO DI CONSEGNE** → **non dovrebbe essere cambiato nulla** (deliberatamente esclusa
    dalla virtualizzazione, §6.1-bis): verificare comunque che il PDF esportato contenga ancora
    tutte le righe delle tre tabelle, incluse quelle aggiunte manualmente in "Interventi".
16. **PDF / HOME** (dialog di anteprima, §6.1-ter) → in PDF, premere "Rinomina" su una cartella madre
    e verificare che compaia il dialog "Nome Attuale"/"Nuovo Nome" **prima** di qualunque modifica su
    disco; premere "Annulla" e controllare che nessun file sia stato toccato; ripetere e premere
    "Conferma", verificando che l'esito sia identico a prima dell'intervento. Stessa verifica in HOME
    su "Aggiorna ticket" e "Aggiorna Data".
17. **HOME** (overlay con conteggio, §6.1-ter) → su "Zip" e "Log Dump in rete" con più sottocartelle/
    file ZIP, verificare che l'overlay mostri "Elaborazione N di M..."/"Spostamento N di M..." con il
    conteggio che avanza; su "Elimina", verificare che compaia comunque l'overlay (senza conteggio).
    Verificare anche in EXCEL che l'overlay (ora componente condiviso) abbia lo stesso aspetto di prima.
18. **PDF / HOME** (annulla rinomina, §6.1-ter) → dopo una rinomina PDF, premere "Annulla ultima
    rinomina" e verificare che i file tornino al nome precedente; ripetere in HOME dopo un "Aggiorna
    ticket" o "Aggiorna Data". Verificare che il pulsante di PDF non annulli una rinomina fatta da
    HOME (e viceversa) se quest'ultima è più recente — devono restare due storici indipendenti.
19. **EXCEL** (fix parsing, §6.1-quater) ⭐ *la verifica sul campo più importante rimasta* → selezionare
    `ETR1000 / 1000FH` e aprire una cartella reale di quella flotta: i campi **TICKET**, **LOCO** e
    **SN** devono ora autocompilarsi con i valori corretti (prima venivano da un fallback che poteva
    prendere un numero qualsiasi, es. `02` da `02.02CR3`). Ripetere selezionando `ETR1000 I-F` su una
    cartella `ETR1000IF`: anche questa etichetta era rotta e ora deve funzionare. Controllare infine
    che una cartella I-F **non** compaia nell'elenco sotto l'etichetta non-I-F e viceversa (le due
    flotte scrivono su report con numero di colonne diverso, §5.4). Verificare che `ETR700` ed `E404P`,
    che già funzionavano, diano esattamente gli stessi valori di prima.
20. **EXCEL / ROTABILE** (§5.3-bis, §6.1-quater) ⭐ *modifica a comportamento visibile* → selezionare
    `ETR1000 / 1000FH` e aprire **una cartella FH**: il campo `ROTABILE` deve ora proporre il rotabile
    FH e **non più** `ETR 1000`. Poi aprire una cartella **ETR1000 pura** sotto la stessa voce e
    verificare che proponga `ETR 1000` e non la variante FH. Se il menu a tendina ROTABILE del report
    non ha una voce FH distinta, il valore deve restare identico a prima (nessuna regressione):
    annotare quali opzioni contiene davvero quel menu, perché è l'informazione che manca per chiudere
    la verifica.
