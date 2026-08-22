# PROJECT_MEMORY.md — Personal Automation Tool (iscot-autotool)

> **Scopo di questo file.** Documento di passaggio di consegne verso qualsiasi sessione futura (umana o AI).
> Contiene tutto ciò che serve per lavorare sul progetto senza doverlo ri-esplorare da zero: architettura,
> vincoli, invarianti da non rompere e stato del lavoro svolto.
>
> **Ultimo aggiornamento:** 22 agosto 2026 — sessione di audit e ottimizzazione per hardware Windows datato,
> secondo giro con le modifiche a comportamento visibile approvate dal committente (§4-bis), e terzo giro
> con l'avvio dello Sprint 1 della roadmap strategica: primo tipo condiviso testato (`LogDumpFolderName`),
> primo progetto di test automatico della soluzione, prima estrazione di percorsi hardcoded in config (§6.1).
> **Stato build alla chiusura sessione:** `dotnet build` sull'intera `.sln` → 0 errori, 0 warning su
> `PersonalAutomationTool` e `PersonalAutomationTool.Tests` (i 2 warning residui sono preesistenti nello
> scratch `TestClosedXML`, fuori scope — vedi §6.5). `dotnet test` → 19/19 superati.

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
│   │                                ViewModelBase, MouseWheelScrollBehavior, Converters/
│   │   └── core/Naming/             LogDumpFolderName — parser condiviso e testato dei nomi
│   │                                di sottocartella LOG/DUMP (vedi §6.1, §6.2 intervento 1.1)
│   ├── modules/                     un sottoalbero per modulo funzionale
│   │   ├── home/  cartelle/  pdf/  excel/  verifiche/  database/
│   │   ├── destinatari_mail/  passaggio_consegne/
│   │   └── email/               EmailService, EmailView, dialogs/, trains/
│   └── modules/database/*.db        train_software.db, emails.db (copiati in output)
├── PersonalAutomationTool.Tests/    ← xUnit, ProjectReference verso PersonalAutomationTool.
│                                       Zero dipendenza da WPF: solo classi pure sotto core/.
│                                       Vedi §6.1 per cosa copre oggi e §6.2 per il piano a 3 livelli.
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

### 6.2 Le 4 macro-aree della roadmap strategica

Elaborata come risposta alla domanda "se fossi il Lead Architect, cosa faresti dopo l'audit
prestazionale". Ogni intervento è marcato **Impatto** (Alto/Medio/Basso) · **Sforzo**
(Rapido/Medio/Ristrutturazione) · **Rischio di regressione** (Basso/Medio/Alto). Le voci con ✅
sono quelle avviate nello Sprint 1 (§6.1); tutte le altre sono **non applicate**, invariate.

#### 2.1-A Stabilità & Resilienza Operativa

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 1.1 | ✅ **Parser/formatter unico** per i nomi `LOG & DUMP` (Sprint 1, 1/8 chiamanti migrato) | **Alto** | Medio | Basso |
| 1.2 | **Validazione preventiva alla creazione** (in CARTELLE) | **Alto** | Rapido | Basso |
| 1.3 | **Confidenza di parsing + avviso visibile** invece di `catch {}` | **Alto** | Medio | Basso |
| 1.4 | **Isolamento dell'interop Excel in processo figlio** | **Alto** | Medio | Medio |
| 1.5 | Sostituzione interop Excel con OpenXML diretto | Alto | Ristrutturazione | **Alto** |
| 1.6 | **Health-check dei percorsi** all'avvio | Medio | Rapido | Basso |
| 1.7 | Wrapper tipizzato sopra Outlook (`dynamic` → interfaccia) | Basso | Medio | Basso |

Il **keystone è 1.1**: oggi l'app scrive quei nomi in un punto (`CartelleView.BtnCrea_Click`) e li
rilegge in almeno otto altri, con logiche indipendenti; scrittura e lettura possono divergere senza
che nessuno se ne accorga. Un tipo unico con `TryParse`/`Format` rende la divergenza
strutturalmente impossibile. **1.2 vale più di 1.1**: validare al momento della creazione costa una
frazione del tollerare in lettura — CARTELLE ha già il pattern giusto (anteprima live del nome),
basta estenderlo con controlli su formato ticket, cartella già esistente, loco presente in `flotte`.
**1.4 prima di 1.5**: eseguire l'interop Excel in un processo figlio a vita breve è molto più
economico di una riscrittura OpenXML diretta (che richiederebbe estendere a mano i range di
convalida dati e formattazione condizionale, oggi limitati, es. `B2:B500`) — un blocco o un leak
muoiono col processo figlio, il padre resta pulito.

#### 2.1-B Architettura & Debito Tecnico

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 2.1 | **Separare "decidere" da "eseguire"** (estrazione logica pura) | **Alto** | Medio | Basso |
| 2.2 | ✅ **Progetto di test + Tier 1** (Sprint 1, avviato) | **Alto** | Rapido | **Nullo** |
| 2.3 | ✅ **Percorsi Hitachi hardcoded → config** (Sprint 1, `hitachiDir` di `ExcelViewModel`) | **Alto** | Rapido | Basso |
| 2.4 | Test Tier 2 su workspace temporaneo reale | Alto | Medio | Nullo |
| 2.5 | Golden-file test sul corpo HTML delle email | **Alto** | Rapido | Nullo |
| 2.6 | Completamento MVVM sui moduli code-behind | Basso | Ristrutturazione | **Alto** |
| 2.7 | `DatabaseManager`: `DataTable` → record tipizzati, lock per istanza | Medio | Medio | Medio |

**2.6 è deliberatamente scartato come obiettivo in sé:** convertire Cartelle, PDF, Database e i
dialog a MVVM è un big-bang senza beneficio visibile all'utente, su codice che oggi funziona. Al suo
posto, **2.1**: estrarre la *logica* lasciando il code-behind come guscio sottile (esempio concreto,
non ancora fatto: `PdfView.BtnRinomina_Click` contiene un algoritmo non banale — due fasi con nomi
temporanei GUID, rilevamento collisioni, incremento ticket per gli NC — annegato in un event
handler; estratto in un `PdfRenamePlanner` che restituisce un *piano* di operazioni, diventerebbe
testabile senza WPF). **2.5 ha il miglior rapporto valore/sforzo dei test non ancora scritti**:
`EmailService.BuildHtmlBody` produce ciò che arriva al cliente; congelare l'output per una decina di
input rappresentativi intercetta esattamente le regressioni che fanno più danno.

**Strategia di test a 3 livelli** (di cui Tier 1 è l'unico avviato finora):
- **Tier 1 — funzioni pure su stringhe** (`BuildSubject`, `ParseLogFolderName`/`LogDumpFolderName`,
  `MatchesTrain`, `AreTrainTypesCompatible`, `ExtractLocosFromFolder`): nessuna astrazione, basta
  spostarle fuori dalle classi WPF. È dove vivono i bug ed è dove i test costano meno.
- **Tier 2 — file system**: per questa app una directory temporanea reale è meglio di un mock,
  perché la logica riguarda semantica di percorsi veri; serve una fixture che costruisca un finto
  albero `LOG & DUMP`.
- **Tier 3 — COM**: non si testa, si isola dietro `IReportWriter`/`IMailComposer` e si verifica
  tutto fino al confine.

#### 2.1-C Performance & Efficienza Legacy

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 3.1 | ✅ **Polling Verifiche: 5s → 60s** (Sprint 1) | **Alto** | Rapido | Basso |
| 3.2 | **`flotte` in dizionario in memoria** all'avvio | Medio | Rapido | Basso |
| 3.3 | Indici SQLite su `(tipo, loco)` e `loco` | Medio | Rapido | Basso |
| 3.4 | **Lettura Verifiche con `OpenXmlReader`** (SAX) invece di ClosedXML | **Alto** | Medio | Medio |
| 3.5 | Pubblicazione **ReadyToRun** | Medio | Rapido | Basso |
| 3.6 | Virtualizzazione griglie residue (Home, PassaggioConsegne) | Basso | Rapido | Basso |
| 3.7 | `DropShadowEffect` e `BitmapScalingMode` | Basso | Rapido | Basso |

**3.4** resta il residuo più grosso non affrontato sul percorso Verifiche: ClosedXML carica l'intero
workbook in un object model per estrarre tre colonne da un foglio; una lettura SAX con
`OpenXmlReader` taglierebbe la memoria di un ordine di grandezza. **3.5**: valutare anche il
deployment self-contained (elimina la dipendenza dal runtime installato su macchine d'ufficio con
permessi ristretti); **non** aggiungere il trimming — XAML e `dynamic` usano riflessione, il
trimmer romperebbe cose in modo difficile da diagnosticare.

#### 2.1-D UX & Nuove Feature

| # | Intervento | Impatto | Sforzo | Rischio |
|---|---|---|---|---|
| 4.1 | **Anteprima "cosa cambierà"** prima delle rinomine massive | **Alto** | Medio | Basso |
| 4.2 | **Feedback di avanzamento** su zip / sposta in rete / elimina | **Alto** | Rapido | Basso |
| 4.3 | **Annulla rinomina** (riusando la tabella `renamer_log`, già presente e inutilizzata — vedi §6.6) | Medio | Medio | Basso |
| 4.4 | Pannello health-check percorsi (vedi 1.6) | Medio | Rapido | Basso |
| 4.5 | Flusso da tastiera: acceleratori, tab order, Invio per confermare | Medio | Rapido | Basso |
| 4.6 | Ricerca globale su `LOG & DUMP` | Medio | Medio | Basso |

**4.1** è la difesa più efficace contro il rischio descritto nella premessa di §6, e non richiede di
rendere i parser perfetti: oggi "Rinomina" in PDF e "Aggiorna ticket" in HOME rinominano in blocco
senza mostrare nulla prima; un dialog "vecchio → nuovo" intercetta un parsing sbagliato prima che
diventi un'email o un file mal nominato. **4.2**: ExcelView ha già l'overlay giusto (`IsLoading` +
`ProgressBar`, quest'ultima ora legata correttamente in §4.15), va solo generalizzato con un
conteggio ("3 di 12").

### 6.3 Da dove ripartire nel prossimo sprint

Il prossimo passo naturale dello Sprint 1 (non fatto in questa sessione: lo scope era "un solo
chiamante pilota") è migrare **un altro** degli 7 chiamanti rimasti a `LogDumpFolderName`, verificare,
poi passare al successivo. Ordine suggerito (dal più isolato al più intrecciato):
`TrainViewHelper.ExtractLocosFromFolder` → `ChiusuraTicketDialog.PopulateLocos` →
`EmailService.GetLogAndDumpFolders`/`BuildSubject` → `ExcelViewModel.AutoFillReportFieldsAsync`.
In parallelo, **2.5** (golden-file test su `BuildHtmlBody`) è pronto per essere iniziato in
qualunque momento: non dipende da nessuna migrazione ulteriore.

### 6.4 Criticità note **non** risolte (richiedono una scelta esplicita, perché cambierebbero il comportamento visibile)

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
- [~] **Test automatici — avviati, non completi.** `PersonalAutomationTool.Tests` esiste ora
      (§6.1) con 19 test Tier 1 su `LogDumpFolderName`. Restano da coprire allo stesso modo:
      `ExtractLocosFromFolder`, `BuildSubject`, `AreTrainTypesCompatible`, `MatchesTrain` — nessuno
      di questi dipende da `LogDumpFolderName`, possono procedere in parallelo alla migrazione dei
      chiamanti (§6.3).

### 6.6 Idee funzionali emerse dal codice (non richieste, solo annotate)
- Le tabelle `renamer_config` / `renamer_queue` / `renamer_log` esistono in `train_software.db` e sono
  gestite da `DatabaseView`, ma **nessun modulo dell'app le usa**: residuo di una funzione di rinomina
  automatica mai completata (o rimossa).
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
