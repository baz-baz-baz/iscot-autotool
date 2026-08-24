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
> `renamer_log`), con il **fix del parsing EXCEL** (§6.1-quater: `ExcelFolderParser`), sbloccato
> dai nomi di cartella reali finalmente forniti dal committente — la scoperta #1 dello Sprint 2, rimasta
> aperta per due sprint, è **risolta** — e infine con il **bug di duplicazione VERIFICHE ETR500 +
> reversione del layout a viewport vincolate** (§6.1-quinquies: `RemoveNestedRoots`,
> `VerificheView.xaml` tornata a `ScrollViewer > StackPanel` su richiesta esplicita).
> Segue lo **Sprint 4** (§6.1-sexies), dedicato alle **prestazioni** su hardware datato: lettura SAX
> dei file Excel (intervento 3.4, misurato 4,2× più veloce e 6,1× meno memoria), criticità **E, F e G**
> di §6.4 finalmente risolte, avvio di EXCEL.EXE tolto dal thread UI, I/O OneDrive da 1+N enumerazioni
> a 1. Chiude lo **Sprint 5** (§6.1-septies), audit di **integrità strutturale** del Report Interventi:
> verificato che il percorso di scrittura attuale (Excel Interop) è già non distruttivo — in tutto il
> codice non esiste un solo `SaveAs()` di ClosedXML — più `ReportInterventiWriter` (OpenXML chirurgico,
> non sostituisce Interop) e 30 test che ispezionano il pacchetto OpenXML parte per parte. Chiude lo
> **Sprint 6** (§6.1-octies): fix del percorso Hitachi per ETR1000 I-F ("ETR1000 ITA-FRA" →
> "ETR1000 ITA-FR", il nome reale su disco), segnalato a runtime dal committente — **azione richiesta
> anche sulla macchina reale**, vedi §6.1-octies. Chiude lo **Sprint 7** (§6.1-nonies): risolto il bug
> intermittente "file in uso" su *Riporta report* — causa reale `FileShare.ReadWrite` senza
> `FileShare.Delete`, più guardia anti-ricaricamento estesa, riprova con backoff e diagnostica
> specifica. Chiude lo **Sprint 8** (§6.1-decies), due correzioni mirate su richiesta del
> committente: prefisso `"SR"` ripristinato in "Aggiorna Ticket" (HOME) e dialog di anteprima
> rimosso dalla rinomina PDF (l'overlay e lo storico su `renamer_log` restano invariati). Chiude lo
> **Sprint 9** (§6.1-undecies), tre correzioni sul modulo PASSAGGIO DI CONSEGNE: data odierna forzata
> dopo il caricamento (bug: `CaricaDati()` riportava indietro la data dell'ultimo salvataggio),
> checkbox nascoste durante l'esportazione PDF a favore del solo testo "Sì"/"No", fallback "No" nelle
> ultime 4 colonne della tabella Movimenti limitato alle sole righe già compilate — e da **quattro**
> correzioni successive sullo stesso punto 2, guidate da screenshot del committente: le prime tre
> (percorso `RelativeSource` accorciato, finestra `IsExporting` ristretta fuori dalle chiamate COM)
> erano migliorie legittime ma non la causa; la quarta e risolutiva ha eliminato le proprietà stringa
> derivate dal percorso di visualizzazione (`BoolToSiNoConverter`, legato allo stesso bool di
> `IsChecked` invece che a una proprietà separata con notifica propagata a mano) — vedi §6.1-undecies
> per la sequenza completa e la lezione su come è stata trovata. **Segue lo Sprint 10
> (§6.1-duodecies): il committente ha deciso di riprogettare da zero il modulo PASSAGGIO DI CONSEGNE
> e ha richiesto la rimozione completa di ogni residuo della vecchia implementazione prima di ricevere
> le nuove specifiche.** Rimossi fisicamente tutti i file dedicati (vista, ViewModel, modelli,
> converter, export PDF, servizio email), il pulsante/voce di navigazione in `MainWindow`, la logica
> di seed `EnsurePassaggioConsegneActions` in `DestinatariManager` (e le relative voci di default) e i
> 4 file di test dedicati. **§1, §2.1, §2.2, §2.5 e §2.7 sono stati aggiornati di conseguenza** (il
> modulo non esiste più nel codice corrente); le narrazioni storiche negli sprint precedenti (§4.13,
> §6.1-bis, §6.1-sexies, §6.1-nonies, §6.1-decies, §6.1-undecies) non sono state riscritte e restano
> come registro di ciò che fu fatto finché il codice esisteva — vedi la nota introduttiva di
> §6.1-duodecies prima di fare affidamento su un dettaglio implementativo lì descritto.
> Chiude lo **Sprint 11** (§6.1-terdecies): **igiene del repository**, il debito accumulato in §6.5
> finalmente saldato. Eliminati i progetti scratch `TestClosedXML/` e `scratch/`, i due file C# orfani
> di root (`ep_test.cs`, `test.cs`), i log di build committati e i `.DS_Store`; **669 artefatti di build
> tolti dal tracking Git** (restano su disco). Da 805 a 97 file tracciati. `MatchesTrain` — che
> `TestClosedXML` era l'unico a verificare — è stata portata in xUnit **prima** della cancellazione.
> Chiude infine lo **Sprint 12** (§6.1-quaterdecies): **riscrittura da zero del modulo PASSAGGIO
> CONSEGNE**, sulla base del template Excel reale "rapportino di turno.xlsx" fornito dal committente.
> Tre schede (ETR 500 / ETR 700 / ETR 1000), MVVM con dipendenze iniettate, e soprattutto **PDF
> vettoriale disegnato da uno snapshot immutabile** invece della cattura `RenderTargetBitmap` della
> vista: la scelta che da sola chiude le criticità **C, D, E ed F** di §6.4 e rende il modulo
> testabile: da 0 a **84 test**. Una correzione dopo segnalazione del committente: il nome
> dell'azione dei destinatari doveva essere quello storico `"Passaggio di consegne"`, non uno nuovo —
> vedi il riquadro in §6.1-quaterdecies.
> **Stato build alla chiusura sessione:** `dotnet clean` + `dotnet build` sull'intera `.sln` → **0 errori
> e 0 warning assoluti** (non più "0 salvo i 2 preesistenti in `TestClosedXML`": quel progetto non
> esiste più, e con esso i 2 NU1510 — vedi §6.1-terdecies). `dotnet test` → **286/286 superati**.
> Vista verificata a runtime con un harness WPF usa-e-getta (fuori dal repository): le tre schede si
> rendono senza eccezioni e **senza alcun errore di binding**. **Non verificati in questo ambiente:**
> l'aspetto del PDF a schermo (manca un rasterizzatore) e la bozza Outlook (manca Office) — vedi la
> checklist al punto 29 di §7.1.
> **Da leggere prima di toccare il modulo EXCEL: §5.3-bis**
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
| Compilare ed esportare in PDF il rapportino di turno + email | **PASSAGGIO CONSEGNE** (riscritto da zero nello Sprint 12, §6.1-quaterdecies) |

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
│   │   ├── destinatari_mail/     DestinatariManager (+ seeding azioni, §6.1-quaterdecies)
│   │   ├── passaggio_consegne/   riscritto da zero nello Sprint 12 (§6.1-quaterdecies):
│   │   │                         Models, Snapshot, ViewModel, PdfExporter, EmailService,
│   │   │                         Services (interfacce), View, Assets/logo-isman.png
│   │   └── email/               EmailService, EmailView, dialogs/, trains/
│   └── modules/database/*.db        train_software.db, emails.db (copiati in output)
├── PersonalAutomationTool.Tests/    ← xUnit, ProjectReference verso PersonalAutomationTool.
│                                       Zero dipendenza da WPF: solo classi pure sotto core/ e
│                                       modules/pdf/, più un uso mirato di InternalsVisibleTo per
│                                       EmailService.BuildHtmlBody (golden-file test, §6.1-bis).
│                                       Vedi §6.1/§6.1-bis per cosa copre oggi e §6.2 per il piano
│                                       a 3 livelli.
└── (nient'altro)                    ← §6.1-terdecies: TestClosedXML/ e scratch/ eliminati
```

**Nota (aggiornata nello Sprint 11, §6.1-terdecies).** La soluzione contiene ora **due soli progetti**,
entrambi reali: l'applicazione e la sua suite di test. I residui di sperimentazione che stavano qui —
`TestClosedXML/`, `scratch/`, `ep_test.cs`, `test.cs` — sono stati **eliminati**, e i `bin/obj` non sono
più tracciati da Git (restano su disco, rigenerati dalla build). Il repository è passato da **805 a 97
file tracciati**. Prima di aggiungere un nuovo progetto "usa e getta" alla `.sln`, si consideri che
l'ultimo è sopravvissuto per mesi accumulando 104 MB e 2 warning di build: per una verifica rapida è
quasi sempre preferibile un test in `PersonalAutomationTool.Tests`.

### 2.2 Pattern architetturale
Ibrido, **non uniforme** — è importante saperlo prima di intervenire:

| Modulo | Pattern usato |
|---|---|
| Home, Excel, Verifiche | **MVVM** (`ViewModelBase` + `RelayCommand`, DataContext creato in XAML) |
| PassaggioConsegne | **MVVM con dipendenze iniettate** — unico modulo dell'app in cui PDF, posta e finestre di dialogo passano da interfacce, quindi l'unico il cui flusso completo è verificabile da xUnit (§6.1-quaterdecies) |
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
- espone `static VerificheViewModel? Instance` e l'evento statico `OnVerificheDataUpdated`.
  ⚠️ Il modulo PASSAGGIO CONSEGNE **non si sottoscrive** a questo evento, a differenza della sua prima
  versione: legge le verifiche una volta all'apertura e poi solo su richiesta esplicita del tecnico.
  Il motivo è in §6.1-quaterdecies (un aggiornamento automatico sovrascriverebbe gli orari di
  ingresso/uscita annotati a mano durante il turno) e chiude anche la criticità **D** di §6.4.

### 2.6 Persistenza

| Store | Percorso | Contenuto |
|---|---|---|
| `train_software.db` | `{BaseDirectory}\modules\database\` | tabella `flotte(tipo, treno, loco, software)` — mappa loco → treno e versione SW |
| `emails.db` | idem | `indirizzi_email(id, nome, email, categoria)` — rubrica |
| `destinatari.json` | `{BaseDirectory}\` | destinatari To/Cc per **treno × azione**; auto-generato al primo avvio |
| `shortcuts.json` | `{BaseDirectory}\` | macro-testi "Nulla Riscontrato", "SIM-GIT", … per treno |
| ~~`data\passaggio_consegne.json`~~ | `{BaseDirectory}\data\` | **non più usato da alcun codice.** Il modulo riscritto ha stato **volatile** per richiesta esplicita del committente: riparte vuoto a ogni avvio e non salva nulla su disco (§6.1-quaterdecies). Un file residuo da versioni precedenti è inerte e può essere cancellato |
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
| **Outlook** (`Outlook.Application`) | `EmailService`, `OutlookRapportinoMailService` | crea `MailItem`, forza l'`Inspector` per ottenere la firma, inserisce il corpo HTML **dentro** la firma via `EmailService.ComponiCorpoConFirma` (funzione pura condivisa, §6.1-quaterdecies), allega i PDF, `Display(false)` |
| **Excel** (`Excel.Application`) | `ExcelViewModel.ExecuteScriviReport` | scrive la nuova riga del report **in modo nativo** per non alterare formattazione/struttura; PID del processo tracciato per terminazione forzata di sicurezza se `Quit()` non basta (§6.1-bis, intervento 1.4) |
| **ClosedXML** | Excel, Verifiche | sola **lettura** (intestazioni, data validation, ultima riga compilata, parsing verifiche) |
| **PdfSharp** | PdfView, `PassaggioConsegnePdfExporter` | conteggio pagine PDF; **disegno vettoriale** del rapportino di turno (non più cattura bitmap — §6.1-quaterdecies). Richiede `GlobalFontSettings.UseWindowsFontsUnderWindows = true`: in PDFsharp 6 senza quella riga `XFont` non risolve alcun carattere |

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

> ⚠️ **Rovesciata nello Sprint 3, su richiesta esplicita del committente** (§6.1-quinquies):
> `VerificheView` è tornata a `ScrollViewer > StackPanel`, senza viewport vincolate né
> virtualizzazione. Il resoconto sotto descrive la scelta originale e resta valido per capire *perché*
> esisteva — utile se in futuro i volumi di VERIFICHE dovessero crescere abbastanza da giustificarla
> di nuovo. **`HomeView` non è stata toccata**: la sua virtualizzazione (§3.6 dello Sprint 2) resta
> attiva.

**Problema (a monte della scelta originale).** I tre `DataGrid` stavano dentro `ScrollViewer > StackPanel`. Uno `StackPanel` concede ai
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
| Nome nelle cartelle su disco | `ETR1000` | `ETR1001FH` | `ETR1000IF` (attaccato) |
| **EXCEL** | \| ← **stesso Report Interventi**, voce `ETR1000 / 1000FH` → \| | | `ETR1000 I-F` (report proprio, `maxCol` 24) |
| Voce nel menu ROTABILE del report | `ETR1000` | `ETR1001FH` | *(assente: foglio separato)* |

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

> ✅ **Ora verificata da test, non solo dichiarata** (Sprint 5, §6.1-septies). Misurato: ClosedXML
> **non** elimina il VBA (timore infondato), ma **riscrive l'intero pacchetto** aggiungendo parti che
> il file non conteneva (`docProps/app.xml`, `xl/calcChain.xml`, `xl/theme/theme1.xml`, metadati di
> pacchetto). Un salvataggio ClosedXML sul percorso del report farebbe fallire
> `ReportInterventiWriterTests`. Per scrivere senza Excel esiste `ReportInterventiWriter` (OpenXML
> chirurgico, tocca il solo `<sheetData>`), che **non** sostituisce Interop come percorso predefinito.
>
> ⚠️ **Rischio che Interop non elimina:** scrivendo *oltre* l'ultima riga coperta dagli intervalli,
> convalide (`C2:C100`), formattazione condizionale, `autoFilter` e range di tabella **non si
> estendono da soli** alla riga nuova. Non è corruzione del file, ma la riga resta priva di quelle
> regole — da verificare sul report reale (punto 23 di §7.1).
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

**Le opzioni reali del menu ROTABILE sono state confermate dal committente** (screenshot del foglio):
il Report Interventi ETR1000 espone **esattamente due voci, `ETR1000` e `ETR1001FH`**, senza spazi
interni e senza alcuna voce Italia-Francia (che ha il proprio foglio, `maxCol` 24). Questo rende il fix
**attivo e non inerte su dati reali**: prima entrambe le flotte ottenevano `ETR1000`, perché la
selezione prendeva la prima opzione contenente quella sottostringa. Da notare, verificato da un test
dedicato: `"ETR1001FH"` **non** contiene la sottostringa `"IF"` (non ha alcuna lettera `I`), quindi non
viene scambiata per la variante Italia-Francia; ed è riconosciuta come appartenente alla famiglia
ETR1000 tramite il token `ETR1001`, non `ETR1000`. Per un tipo I-F su questo foglio la funzione
restituisce `null` — meglio nessuna scelta che la voce `ETR1000`, che sarebbe il rotabile sbagliato.

**44 test Tier 1** in `Modules/Excel/ExcelFolderParserTests.cs`, tutti su funzioni pure: i 4 nomi reali
(ticket e loco, via `[Theory]`), tipo e `LOG`/`DUMP` sugli stessi 4, software non confuso con la loco,
esclusione delle forme I-F dall'etichetta non-I-F **e** viceversa, ordinamento dei token per lunghezza
decrescente, non-cattura della loco di una cartella I-F sotto l'etichetta non-I-F, invarianza di
`ETR700` (che già funzionava), nomi fuori grammatica → `null`, le due guardie ticket/loco non numerici,
il riconoscimento della variante FH come tipo distinto da `ETR1000`, i casi ROTABILE nelle due
direzioni (FH non prende `ETR1000`; ETR1000 puro non prende la variante FH) **sulle opzioni reali del
foglio**, e il caso "foglio senza varianti → nessun cambiamento".

**Token FH confermato dal committente:** le cartelle della variante FH usano `ETR1001FH`, lo stesso
valore della colonna `tipo` in `flotte`. I test coprono anche la forma alternativa `1000FH`.

**Build/test:** `dotnet build` → 0 errori, 0 warning (stessi 2 NU1510 preesistenti in `TestClosedXML`).
`dotnet test` → **64/64 superati**.

**Cosa resta da verificare a mano** (non verificabile in questo ambiente, manca l'accesso alle cartelle
`LOG & DUMP` reali): punto 19 della checklist §7.1 — aprire EXCEL su una cartella reale delle due
flotte ETR1000 e confrontare TICKET/LOCO/SN autocompilati con quelli attesi.

### 6.1-quinquies Sprint 3, coda — bug VERIFICHE (duplicazione ETR500) + reversione layout

Il committente ha segnalato uno screenshot del modulo VERIFICHE con due problemi: le verifiche ETR500
comparivano duplicate, e ha chiesto che le tabelle tornino a espandersi per intero invece di scorrere
al proprio interno.

**1. Bug di duplicazione ETR500 — trovato e corretto, non un caso speciale.**
`VerificheViewModel.LoadDataForFleet("500", ...)` cerca in due cartelle radice:
```
Interventi ETR500\Censimento ETR500\Verifiche ETR500      ← percorso base
Interventi ETR500                                          ← aggiunta subito dopo, per fleetIdentifier == "500"
```
La seconda è la cartella **madre** della prima. Il codice scansiona ogni radice **per conto proprio**,
ricorsivamente, cercando il file `.xlsx` più recente in ciascuna. Sulla radice "madre" la scansione
ricorsiva attraversa comunque `Censimento ETR500\Verifiche ETR500` come una delle tante
sottocartelle, trova lì lo stesso identico file già trovato scansionando la radice "figlia" per
conto suo — e, essendo l'unico report della cartella, è anche il file più recente dell'intero
sottoalbero della radice "madre". Risultato: lo stesso file viene passato a `ParseExcelFile` **due
volte**, e ogni riga del foglio finisce due volte in `VerificheList500`.

**Perché non è un problema di "700"/"1000".** Le radici aggiuntive di quelle due flotte
(`ETR1000FH`/`ETR1000 FH`/`ETR1000IF` per "1000", `INTERVENTI ETR700 ELO BL3`/`Interventi ETR700` per
"700") sono cartelle **sorelle**, non annidate l'una nell'altra — verificato con un test dedicato, non
per ispezione visiva del percorso. Solo "500" ha questa relazione genitore/figlio fra le sue radici.

**La correzione (`VerificheViewModel.RemoveNestedRoots`, `internal static`, testabile via
`InternalsVisibleTo`).** Prima di scansionare, scarta dalla lista di radici quelle che sono già
contenute (come sottocartella, a qualunque profondità) in un'altra radice della stessa lista: la
scansione ricorsiva della radice più esterna copre già il contenuto di quella più interna, quindi
tenerle entrambe significa solo trovare due volte lo stesso file. **Non è un fix specifico per "500"**:
è una deduplicazione generale per annidamento, verificata anche contro le radici di "700" e "1000" —
dove, per costruzione, non toglie nulla. Il confronto è per segmenti di percorso (via separatore di
cartella), non per prefisso di stringa: `"...ETR1000"` non è trattata come antenata di
`"...ETR1000FH"` solo perché ne è prefisso testuale.

**9 test Tier 1** in `VerificheViewModelTests.cs`: il caso reale di "500" (la radice annidata viene
scartata), i casi reali di "700" e "1000" (nessuna radice scartata), radici identiche non scartate a
vicenda, annidamento a più livelli di profondità, catena di tre radici annidate (sopravvive solo la
più esterna), nomi con prefisso testuale simile ma non realmente annidati, lista vuota, radice singola,
separatori di percorso misti (`/` e `\`).

**2. Layout: reversione esplicita della virtualizzazione di §4.17.** Il committente ha chiesto che le
tabelle si espandano per intero, senza scorrimento interno — l'opposto della scelta fatta nello Sprint
2 (§4.17), dove `ScrollViewer > StackPanel` era stato sostituito da una `Grid` a viewport vincolate
apposta per abilitare la virtualizzazione, perché lo `StackPanel` originale concede ai figli altezza
infinita e quindi nessuna viewport si stabilisce mai. **Qui è stato fatto l'esatto contrario, di
proposito**: tornato a `ScrollViewer > StackPanel`, rimossi `MaxHeight="260"` dal riquadro di
riepilogo e `MinHeight="120"`/`Height="*"` dalle tre righe delle griglie di flotta, rimossi gli
attributi di virtualizzazione (`EnableRowVirtualization`, `VirtualizingPanel.*`,
`ScrollViewer.CanContentScroll`) dallo stile condiviso e il `RiassuntoListStyle` con il suo
`ScrollViewer` interno (ora un `ItemsControl` semplice, senza wrapper). **Perché è una scelta
corretta e non solo un capriccio estetico:** §4.17 ottimizzava per "migliaia di righe" — il caso
reale di VERIFICHE è invece decine di righe (vedi lo screenshot del committente: singole cifre per
flotta). A quel volume il costo di non virtualizzare è nullo, e la leggibilità di vedere tutto senza
tre riquadri di scorrimento separati vale più dell'ottimizzazione. **Non tocca `HomeView`**, che resta
virtualizzata: quella gestisce le manutenzioni in sospeso, un elenco che può davvero crescere a
centinaia di voci nel tempo, un caso diverso.

**Build/test:** `dotnet build` → 0 errori, 0 warning (stessi 2 NU1510 preesistenti in
`TestClosedXML`). `dotnet test` → **98/98 superati**. Eseguibile avviato manualmente, nessuna
eccezione allo startup.

**Cosa non è stato verificato a schermo** (limite dell'ambiente, non del codice — stesso limite già
dichiarato in §6.1-ter): non è stato possibile confermare visivamente che la duplicazione sia
davvero sparita né che il layout risulti come atteso, in assenza di un tool di automazione UI Windows
e di accesso alle cartelle Hitachi reali. Vedi il punto 21 della checklist §7.1.

### 6.1-sexies Sprint 4 — ottimizzazione prestazionale mirata (SAX Excel, I/O fuori dal dispatcher, LOH)

Il committente ha segnalato **rallentamenti reali nell'uso quotidiano** su macchine datate, in
particolare su operazioni Excel, scansioni di percorsi e caricamento dati. Interventi guidati dalla
misura, non dall'intuizione: dove il guadagno non era dimostrabile, l'ottimizzazione **non** è stata
applicata (vedi "Valutato e scartato").

#### 3.4 — Lettura SAX dei file Excel ⭐ *l'intervento con il guadagno maggiore*

Rimandato per **due sprint** perché mancava un file "Verifiche" reale su cui validare la riscrittura
(§6.1-bis). Sbloccato cambiando la strategia di validazione: la garanzia che serviva non era «funziona
su quel file» ma «**si comporta come l'implementazione precedente**», e quella è verificabile per
**equivalenza differenziale** — si esegue anche il vecchio percorso, sullo stesso file, e si
confrontano gli output riga per riga. Il file reale continua a non essere necessario.

`modules/verifiche/VerificheExcelReader.cs`: lettura in streaming con `OpenXmlReader`
(`DocumentFormat.OpenXml 3.1.1`, **già presente** come dipendenza transitiva di ClosedXML — reso
esplicito nel `.csproj`, nessun pacchetto nuovo scaricato). Sostituisce il caricamento dell'intero DOM
di `XLWorkbook`, che materializzava tutte le celle di tutti i fogli per estrarne **tre colonne**.

**Guadagno misurato** (file di prova da 101 KB, 5.000 righe dati, build Release):

| | Tempo | Memoria allocata |
|---|---|---|
| ClosedXML (prima) | 556 ms | **104 MB** |
| SAX (ora) | 131 ms | 17 MB |
| **Rapporto** | **4,2× più veloce** | **6,1× meno memoria** |

Il dato che spiega i rallentamenti percepiti: **104 MB allocati per un file da 101 KB**, gran parte in
Large Object Heap, e non una volta sola — a ogni ricarica, che è scatenata sia dai `FileSystemWatcher`
sia dal timer di backstop (§2.5). Su una macchina con poca RAM libera questo significa pause del
garbage collector percepibili come blocchi dell'interfaccia.

**Rete di sicurezza:** se la lettura SAX fallisce per qualunque motivo (pacchetto OpenXML malformato,
`.xls` legacy, formato inatteso) si ricade automaticamente sul percorso ClosedXML, rimasto invariato.
Prestazioni peggiori, **nessuna verifica persa**. La normalizzazione dei valori (incluso il lookup del
treno in `flotte` per la sola flotta "1000") è stata estratta in `BuildModel`, condivisa dai due
percorsi, così non possono divergere.

**21 test** in `VerificheExcelReaderTests.cs`, su file `.xlsx` reali generati al volo: ognuno esegue
**entrambe** le implementazioni e ne confronta l'output. Scenari costruiti sulle caratteristiche note
del formato reale: intestazione non sulla prima riga, righe vuote intercalate (che `RowsUsed()` salta
e che quindi **non** devono spostare l'indice dell'intestazione), colonne in posizione arbitraria
(individuate per numero di colonna assoluto), celle vuote dentro le righe, testo multi-riga, più fogli
(va letto il primo), stringhe ripetute (`SharedStringTable`, dove un errore di indice si manifesta con
valori scambiati fra righe), foglio senza intestazione riconoscibile, foglio da 500 righe, e la
conversione dei riferimenti di cella (`A1`→1, `AA1`→27, `BC12`→55).

#### Criticità F — freeze all'apertura di "Passaggio di Consegne"

`PassaggioConsegneViewModel` chiamava `AutoCompilaTreniDaVerifiche()` **sincronamente dal costruttore**.
Quando `VerificheViewModel.Instance` è ancora `null` — cioè aprendo "Passaggio di Consegne" **prima**
di "Verifiche", lo scenario normale al primo utilizzo — quel metodo esegue il caricamento completo di
tre flotte: enumerazione ricorsiva di alberi OneDrive più parsing Excel. Tutto sul dispatcher, con la
finestra bloccata per secondi. Ora la lettura è su `Task.Run` e **solo** l'applicazione dei risultati
(che muta `ObservableCollection` legate alla UI) resta sul dispatcher, via `ConfigureAwait(true)`
esplicito e commentato. Il costruttore non attende più: la vista si apre subito.

#### Criticità G — risoluzione percorsi di rete sul thread UI

`HomeViewModel.OnLogDumpRete` eseguiva `GetLogDumpReteBasePath()` e `ResolveTrainTypePath()` — che
fanno `Directory.Exists`/`GetDirectories` su percorsi OneDrive e di rete — **prima** di entrare nel
`Task.Run`: su rete lenta o disconnessa la finestra si bloccava già al clic del pulsante. Spostate sul
thread pool insieme all'enumerazione degli ZIP, con overlay "Ricerca cartelle di rete..." durante
l'attesa. I tre `MessageBox` di esito restano sul thread UI, con gli stessi testi di prima.

#### Criticità E — `RenderTargetBitmap` non vincolata (Large Object Heap)

`PassaggioConsegnePdfExporter` allocava una bitmap Pbgra32 della dimensione piena del rapportino: 4
byte per pixel in **un unico blocco contiguo**, quindi sempre in LOH — un rapportino 3000×4000 sono
~48 MB in un colpo solo, che su una macchina con poca RAM libera può fallire del tutto. Introdotto un
tetto di 8 milioni di pixel (~32 MB): oltre quella soglia il render avviene a DPI ridotti in modo
proporzionale. **Sotto la soglia — cioè per i rapportini di dimensione ordinaria — nulla cambia:** DPI
resta 96 e l'immagine è identica a prima. La riduzione agisce sui DPI e non sulle dimensioni in pixel,
così l'elemento non viene rimisurato e il layout catturato resta lo stesso; è inoltre esattamente ciò
che `gfx.DrawImage` faceva comunque subito dopo per adattare l'immagine alla pagina PDF.

#### Bonifica COM — avvio di Excel fuori dal thread UI

L'interop di scrittura era **già** dentro `Task.Run`, con rilascio COM protetto singolarmente (§4.6) e
PID-tracking con terminazione forzata (§6.1-bis). Restava però un residuo reale:
`Type.GetTypeFromProgID` e soprattutto **`Activator.CreateInstance`** — che *avvia il processo
EXCEL.EXE*, operazione da secondi su macchina lenta — giravano sul thread UI, prima del `Task.Run`:
la finestra si bloccava prima ancora che comparisse l'overlay. L'intera interazione COM (ProgID,
avvio, scrittura, pulizia) è ora dentro un unico `Task.Run`, che restituisce un messaggio d'errore
(`null` = successo) perché i `MessageBox` restino sul thread UI. È anche più corretto di prima dal
punto di vista COM: creazione e uso avvengono ora nello stesso apartment, mentre prima l'oggetto
veniva creato sul thread STA della UI e usato da un thread del pool.

#### I/O su OneDrive — da 1+N enumerazioni a 1

`VerificheViewModel.LoadDataForFleet` enumerava ricorsivamente **tutte** le sottocartelle dell'albero e
poi apriva una enumerazione di file per **ciascuna** (1 + N chiamate al file system, con N = numero di
sottocartelle). Sostituito da **una sola** `EnumerateFiles(..., SearchOption.AllDirectories)`.
L'esito è identico e non per approssimazione: il filtro che la versione precedente applicava alle
*cartelle* (escludendo quelle con `OLD`/`VECCH`/`ARCHIV` nel percorso) era **ridondante**, perché il
percorso completo di un file include quello della sua cartella, e il filtro sul percorso del file —
rimasto invariato — scarta già quei casi.

#### Allocazioni ripetute

`ExcelFolderParser.BuildLocoRegex` costruiva e compilava un `Regex` a ogni autocompilazione del report.
I pattern possibili sono 8 in tutto (4 etichette × 2 soglie di cifre) e non cambiano durante la
sessione: ora sono in una `ConcurrentDictionary` (thread-safe perché `AutoFillReportFieldsAsync` gira
sul thread pool).

#### Valutato e scartato — con motivo, non per dimenticanza

| Intervento richiesto | Perché non applicato |
|---|---|
| **Debounce/throttle sui watcher** (punto 2.2) | **Già presente**: `AppWatcher` 300 ms (§2.5), `VerificheViewModel.OnFileChanged` 500 ms, più la guardia anti-rientranza `Interlocked` su `CheckForFileUpdates` (§4.1). Nulla da aggiungere: rifarlo sarebbe stato lavoro fittizio. |
| **Scrittura Excel in blocco** invece che cella per cella | Le ~26 celle di una riga costano ~26 round-trip COM (stimati pochi ms in totale), trascurabili rispetto ai **secondi** dell'avvio di EXCEL.EXE che è stato invece corretto. In cambio, scrivere l'intera riga con un solo `Range.Value` sovrascriverebbe anche le celle che oggi vengono **deliberatamente saltate** quando il valore è vuoto — rischio concreto di azzerare formule o formattazione su un file business-critical (§5.4), per un guadagno non misurabile. |
| **`ArrayPool<byte>` su ZIP e PDF** | `ZipFile.CreateFromDirectory` e `PdfSharp` gestiscono i propri buffer internamente e non espongono un punto in cui iniettarne uno riusabile. L'unico buffer davvero grande e sotto il nostro controllo era la `RenderTargetBitmap`, affrontata sopra (criticità E) — che è il vero consumo LOH, non i buffer di streaming. |
| **Allocazioni nel loop di ricerca del tecnico** (`AutoFillReportFieldsAsync`) | Il loop itera sulle opzioni di una combo (~50 voci) una volta per autocompilazione: poche centinaia di stringhe temporanee. Ottimizzarlo sarebbe stata "ottimizzazione cosmetica spacciata per miglioramento", lo stesso errore già evitato per `BitmapScalingMode` (§6.4, punto I). |

**Build/test:** `dotnet build` → 0 errori, 0 warning (stessi 2 NU1510 preesistenti in `TestClosedXML`).
`dotnet test` → **119/119 superati** (98 preesistenti + 21 nuovi di equivalenza SAX): nessuna
regressione, l'output dei dati è identico al 100%. Eseguibile avviato manualmente, nessuna eccezione.

**Non verificato in questo ambiente** (stesso limite dichiarato in §6.1-ter): il guadagno percepito
sulle macchine reali, che dipende dai volumi effettivi dei file Verifiche e dalla latenza di OneDrive.
Vedi il punto 22 della checklist §7.1.

### 6.1-septies Sprint 5 — audit di integrità strutturale del Report Interventi

Il committente ha chiesto di garantire che le scritture sul Report Interventi non alterino la
struttura interna del workbook (macro VBA, convalide dati, formattazione condizionale, filtri,
tabelle, stili, formule, protezioni).

#### Esito dell'audit: **il percorso di scrittura attuale è già non distruttivo**

Ispezionati tutti i punti che toccano un file Excel. Risultato, verificato con ricerca esaustiva su
tutto il codice:

| Punto | Tecnologia | Esito |
|---|---|---|
| `ExecuteScriviReport` — scrittura riga | **Excel Interop** (`workbookInterop.Save()`) | ✅ Sicuro: è Excel stesso a riscrivere il file |
| `ExecuteScriviReport` — ultima riga compilata | ClosedXML, **sola lettura** | ✅ Nessun `Save`, `FileAccess.Read` |
| `LoadExcelFieldsAsync` — intestazioni e convalide | ClosedXML, **sola lettura** | ✅ Nessun `Save`, `FileAccess.Read` |
| `VerificheViewModel` — lettura verifiche | SAX / ClosedXML, **sola lettura** | ✅ `isEditable: false` |
| `ExecuteSpostaReport` | `File.Copy` + `File.Move` | ✅ Copia byte-per-byte, nessuna riscrittura |
| `ExecuteRiportaReport` | `File.Move` | ✅ Preserva anche l'estensione originale (`.xlsm` resta `.xlsm`) |

**In tutto il codice non esiste un solo `SaveAs()` di ClosedXML.** L'unico salvataggio Excel è
`workbookInterop.Save()`, cioè Interop. L'invariante §5.4 è quindi rispettata, e non è stato
necessario alcun refactoring correttivo del percorso di produzione.

#### Rischi residui che Interop **non** elimina — da conoscere

Excel preserva la struttura del pacchetto, ma non estende automaticamente gli intervalli quando si
scrive **oltre** l'ultima riga coperta:
- una convalida dati su `C2:C100` **non** copre la riga 101;
- lo stesso vale per la formattazione condizionale, per l'`autoFilter` e per il range di una tabella
  (ListObject).

Non è corruzione del file — è la nuova riga a restare priva di quelle regole. Verificabile solo sul
report aziendale reale: **è il punto 23 della checklist §7.1**.

#### `ReportInterventiWriter` — scrittura chirurgica OpenXML

`modules/excel/ReportInterventiWriter.cs`, **aggiunto senza sostituire Interop**, che resta il
percorso predefinito (§5.4 invariata). Esiste per due motivi concreti: rende la scrittura
verificabile in modo automatico **senza Excel installato** (impossibile contro un processo COM in un
test headless), e offre un percorso utilizzabile dove Excel manca, dove oggi "Scrivi report" mostra
solo un errore. Modifica il solo `<sheetData>`; scelte prese per non alterare nulla di implicito:
- stringhe come `InlineString`, così `sharedStrings.xml` — parte condivisa da tutti i fogli — non
  viene toccata;
- celle nuove che ereditano lo `StyleIndex` dalla riga precedente: la riga inserita ha l'aspetto
  delle altre (bordi, formato data) **senza** aggiungere stili a `styles.xml`;
- date come seriale OADate, la rappresentazione nativa di Excel;
- colonne assenti dal dizionario **non toccate**, come fa Interop per non sovrascrivere formule.

**Difetto trovato dai test, non dall'ispezione del codice.** La prima versione modificava anche
`xl/workbook.xml`: il semplice accesso a `workbookPart.Workbook` carica il DOM e l'SDK **lo riscrive
alla chiusura**, anche senza averlo modificato. Corretto leggendo `workbook.xml` come XML grezzo per
individuare il primo foglio. È esattamente il tipo di effetto collaterale invisibile che questa suite
esiste per intercettare.

#### Suite di integrità strutturale — 30 test

`ReportTemplateBuilder` costruisce un `.xlsm` che riproduce le caratteristiche del report reale:
progetto VBA binario, convalide dati, formattazione condizionale (con i `<dxfs>` corrispondenti),
`autoFilter`, tabella/ListObject, protezione foglio, formule, stili personalizzati, stringhe
condivise e nomi definiti. Serve perché il file aziendale non è disponibile né versionabile: ciò che i
test devono dimostrare non è «funziona su quel file» ma «**nessuna di queste parti viene toccata**».

`ReportInterventiWriterTests` ispeziona il pacchetto come archivio ZIP, confrontando le parti prima e
dopo — nessuna asserzione si fida di ciò che la libreria dichiara di fare:
- `vbaProject.bin` **identico byte per byte**;
- `styles.xml` e `sharedStrings.xml` identici;
- tabelle, relazioni `.rels` e `[Content_Types].xml` identici;
- nessuna parte aggiunta o rimossa dal pacchetto;
- **l'unica parte modificata è `xl/worksheets/sheet1.xml`**;
- `dataValidations`, `conditionalFormatting`, `autoFilter`, `tableParts`, `sheetProtection` presenti e
  invariati;
- dentro il foglio, le righe preesistenti sono invariate e **l'unica aggiunta è la riga nuova**;
- formula preesistente non alterata, colonna non fornita non creata, stile ereditato, data come
  seriale, testo inline, scritture ripetute che mantengono l'ordine, riscrittura di riga esistente
  senza duplicare celle.

> **Nota sul confronto XML.** `XNode.DeepEquals` tratta le dichiarazioni di namespace come attributi
> posizionali: dopo un salvataggio l'SDK può emettere `xmlns:r` prima di `r:id` anziché dopo,
> producendo XML byte-diverso ma identico per significato. I test usano quindi un confronto
> **semantico** (nome, attributi per nome, figli in ordine) che esclude solo le dichiarazioni di
> namespace: considerarle una modifica strutturale sarebbe un falso positivo.

#### Cosa fa davvero ClosedXML in scrittura — misurato, non presunto

Un test dedicato salva lo stesso file con ClosedXML e ne confronta l'esito con la scrittura
chirurgica. **Il timore più comune si rivela infondato: ClosedXML non elimina il progetto VBA**, che
resta byte-identico. Il rischio reale è un altro: **riscrive l'intero pacchetto**, aggiungendo parti
che il file non conteneva — `docProps/app.xml`, `xl/calcChain.xml`, `xl/theme/theme1.xml` e i metadati
di pacchetto — e ri-serializzando quelle esistenti. Il writer chirurgico ne modifica **una sola**.
L'invariante §5.4 passa così da regola motivata a parole a **regola verificata da un test**, che
fallirebbe se qualcuno introducesse un salvataggio ClosedXML sul percorso del report.

**Build/test:** `dotnet build` → 0 errori, 0 warning (stessi 2 NU1510 preesistenti in
`TestClosedXML`). `dotnet test` → **149/149 superati** (119 preesistenti + 30 di integrità).

### 6.1-octies Sprint 6 — fix percorso Hitachi ETR1000 I-F ("ITA-FRA" → "ITA-FR")

Il committente ha segnalato a runtime l'errore *"Cartella Hitachi non trovata:
C:\Users\peli\Hitachi Group\SSB_SST - Interventi ETR1000\ETR1000 ITA-FRA"*: la cartella reale su
disco si chiama **"ETR1000 ITA-FR"**, senza la "A" finale.

**Causa:** `HitachiPathsManager.CreateDefaultConfig()` (§6.1, intervento 2.3 dello Sprint 1) conteneva
il segmento di percorso sbagliato fin dalla sua introduzione. Non un refactoring che ha rotto qualcosa
di funzionante: il valore era **già** sbagliato quando è stato estratto dal codice duplicato di
`ExecuteSpostaReport`/`ExecuteRiportaReport` — l'estrazione ha copiato fedelmente un errore
preesistente, senza introdurne uno nuovo. Nessun test lo copriva: la build e i 149 test restavano
verdi perché nessuno asseriva sul *contenuto* della configurazione di default, solo sul suo
comportamento strutturale.

**Corretto in due punti**, entrambi necessari:
1. `HitachiPathsManager.cs` — il valore di default usato quando `hitachi_paths.json` non esiste.
2. Il file `hitachi_paths.json` **già generato** in questo ambiente di sviluppo
   (`PersonalAutomationTool/bin/Debug/net10.0-windows/`), che altrimenti avrebbe continuato a
   contenere il valore vecchio: la cache legge dal file se esiste, e lo rigenera dal default **solo**
   se manca. Questo file non è tracciato in git (non risulta né come modificato né come nuovo in
   `git status`): è un artefatto di build locale, non un file distribuito con l'app.

**`ExecuteSpostaReport` ed `ExecuteRiportaReport` non richiedevano modifiche**: entrambi già
costruiscono i propri percorsi (`Path.Combine(hitachiDir, "OLD Report")` per l'archiviazione,
`Path.Combine(hitachiDir, newFileName)` per il ripristino) a partire da `hitachiDir`, senza un
proprio riferimento hardcoded a "ITA-FRA" — la correzione della sola fonte del percorso li corregge
entrambi per costruzione. Verificato leggendo entrambi i metodi, non assunto.

**Deliberatamente non toccato — con motivo, non per svista.** `ExcelViewModel.MatchesTrain`
contiene due controlli `fileName.Contains("ITA-FRA", ...)` (righe ~1574 e ~1583), usati per
riconoscere a quale flotta appartiene un **nome di file di report** (`"Report Interventi*.xls*"`),
non il nome della cartella Hitachi. È un contesto diverso: un nome di file è testo libero scelto da
chi lo salva, non vincolato al nome della cartella che lo contiene, e non c'è alcuna evidenza che
quella stringa sia sbagliata nello stesso modo. La logica è inoltre già in OR con altri marcatori
(`Italia`, `Francia`, `I-F`, `1000IF`): anche se "ITA-FRA" non trovasse mai corrispondenza, gli altri
bastano a riconoscere la flotta — non è un difetto funzionale, solo un'alternativa potenzialmente
inerte. Toccarla senza un file reale su cui verificare sarebbe stato estendere la correzione oltre
l'evidenza disponibile.

> ⚠️ **Da fare sulla macchina reale del tecnico, non copribile da questa sessione.** Se
> `hitachi_paths.json` esiste già nella cartella dell'eseguibile installato (probabile: è così che si
> è manifestato l'errore), la correzione del codice sorgente **non lo corregge retroattivamente** —
> quel file, una volta creato, non viene più rigenerato dal default. Va **cancellato** (si rigenera da
> solo al prossimo avvio con il valore corretto) oppure **modificato a mano**, cambiando
> `"ETR1000 ITA-FRA"` in `"ETR1000 ITA-FR"` nella voce `"Train": "ETR1000 I-F"`.

**4 test** in `HitachiPathsManagerTests.cs` (nuovo — prima non esisteva copertura per questa classe):
regressione mirata sul percorso "ITA-FR" risultante, verifica che la cartella I-F sia annidata sotto
la stessa base di "ETR1000 / 1000FH" (un errore qui manderebbe Sposta/Riporta Report nell'albero
sbagliato), treno non configurato → `null`, presenza di tutte e quattro le voci di default.

**Build/test:** `dotnet build` → 0 errori, 0 warning. `dotnet test` → **153/153 superati**
(149 preesistenti + 4 nuovi).

### 6.1-nonies Sprint 7 — bug intermittente "file in uso" su Riporta report

Il committente ha segnalato un errore **intermittente** cliccando "Riporta report": il file non può
essere caricato perché risulta in uso da un altro processo.

#### Causa individuata: `FileShare.ReadWrite` non concede il diritto di **eliminazione**

`LoadExcelFieldsAsync` (e la lettura dell'ultima riga in `ExecuteScriviReport`) aprivano il report
con `FileShare.ReadWrite`. Quella combinazione consente ad altri processi di **leggere e scrivere** il
file, ma **non** di eliminarlo o rinominarlo — e `File.Move` richiede sul file di origine proprio il
diritto di eliminazione. Finché uno di quegli stream era aperto, lo spostamento falliva con
`ERROR_SHARING_VIOLATION` (HRESULT `0x80070020`).

**Perché intermittente.** Lo stream non è quasi mai aperto: viene aperto solo mentre si (ri)carica il
report. Ma il ricaricamento può partire da solo, per un evento di `AppWatcher` — che scatta su
**qualunque** modifica dentro `LOG & DUMP`, incluse quelle non correlate — e la finestra di
sovrapposizione con il clic dell'utente su "Riporta report" è di poche centinaia di millisecondi.
Da qui il comportamento apparentemente casuale.

**Seconda causa concorrente:** il flag di guardia `_isWritingReport`, che impedisce a
`CheckAndLoadExistingReportAsync` di riaprire il file, copriva **solo** "Scrivi report". "Riporta
report" e "Sposta report" ne erano scoperti — proprio le due operazioni che spostano il file.

#### Correzioni applicate

1. **`FileShare.ReadWrite | FileShare.Delete`** nei tre punti che aprono un file Excel in lettura
   (`LoadExcelFieldsAsync`, `ExecuteScriviReport`, `VerificheExcelReader`). Su Windows l'handle
   segue il file, non il percorso: lo stream resta valido anche se il file viene spostato mentre lo
   si legge — verificato da un test, non assunto.
2. **Guardia estesa**: `_isWritingReport` rinominato `_isReportFileOperationInProgress` e attivato
   anche in `ExecuteSpostaReport` ed `ExecuteRiportaReport`, con rilascio nel `finally`.
3. **`FileOperationRetry`** (`core/FileOperationRetry.cs`): riprova con backoff esponenziale
   (200→400→800→1600 ms, 5 tentativi, ~3 s complessivi) **solo** sulle violazioni di condivisione
   (codici Win32 32 e 33). Un file mancante o un accesso negato per permessi non migliorano
   aspettando: rilanciano subito, senza far perdere secondi all'utente. Applicato a `File.Move` di
   "Riporta report" e a `File.Copy`/`File.Move` di "Sposta report". Durante le riprove l'overlay
   mostra "File temporaneamente in uso, nuovo tentativo N di 5...".
4. **Diagnostica specifica**: se tutti i tentativi falliscono, un `catch` filtrato mostra quale file
   è bloccato, il percorso completo e le tre cause più probabili (Excel aperto, sincronizzazione
   OneDrive in corso, antivirus/indicizzatore) — invece del solo messaggio di sistema, che non indica
   né il file né una via d'uscita.
5. **`DatabaseManager.Dispose` → `SqliteConnection.ClearPool`**: `Microsoft.Data.Sqlite` mantiene un
   pool, quindi `Close()`/`Dispose()` restituiscono la connessione al pool **senza** chiudere
   l'handle nativo sul `.db` né i collaterali `-wal`/`-shm`. Il rilascio è ora deterministico. Costo
   trascurabile: gli accessi al database sono già rari e brevi, perché `FlotteCache` tiene l'intera
   tabella in memoria (§6.1-bis).

#### Sul ".db" citato nella segnalazione — nessuna evidenza che sia la causa

La segnalazione menzionava un blocco "da un file .db". **Non è stata trovata evidenza che il database
sia coinvolto nel lock del report**, e i due file non hanno alcun rapporto: `train_software.db` sta in
`{BaseDirectory}\modules\database\`, il report sta fra `LOG & DUMP` (Desktop) e la cartella Hitachi;
`ExecuteRiportaReport` non esegue alcuna query. In questo ambiente il progetto risiede in
`Documents\GitHub\`, **fuori** da OneDrive (`OneDrive - Iscot Italia S.p.A` è un percorso distinto):
i `.db` non sono quindi sincronizzati. Il `ClearPool` è stato comunque applicato perché è corretto in
sé e chiude un rilascio non deterministico reale, **ma non è la correzione del bug segnalato**: quella
è il punto 1.

> ⚠️ **Da verificare sulla macchina del tecnico**, dove l'installazione potrebbe trovarsi altrove: se
> la cartella dell'eseguibile è dentro un percorso sincronizzato (OneDrive/SharePoint, o `Documenti`
> reindirizzato con Known Folder Move), i file `.db`, `-wal` e `-shm` vengono continuamente toccati
> dal client di sincronizzazione. In quel caso **spostare l'installazione fuori dalla cartella
> sincronizzata** è la soluzione corretta: isolare il solo database (per esempio in `%LOCALAPPDATA%`)
> è possibile ma cambierebbe dove `DatabaseView` legge e scrive e dove finiscono i dati distribuiti
> con l'app — una decisione di prodotto, non un dettaglio tecnico, da prendere esplicitamente.

#### Test — 17 nuovi, con lock reali del file system

`FileOperationRetryTests` (12): il test cardine apre un file con `FileShare.ReadWrite` e verifica che
`File.Move` **fallisca** con violazione di condivisione (riproduce la causa del bug), poi che con
`FileShare.Delete` **riesca** anche a stream aperto, con lo stream ancora leggibile dopo lo
spostamento. Più: riconoscimento delle eccezioni riprovabili e non, riprova che riesce dopo il
rilascio, propagazione dopo l'ultimo tentativo, errore non riprovabile che **non** attende (misurato
con cronometro), backoff che raddoppia davvero, variante sincrona, contenuto del messaggio
diagnostico, e una prova end-to-end con un lock vero rilasciato da un altro thread dopo 250 ms.

`DatabaseManagerLockTests` (5): dopo `Dispose` il `.db` può essere spostato ed eliminato, non restano
`-wal`/`-shm`, dieci aperture consecutive non accumulano handle. Include una **controprova**: con il
`DatabaseManager` ancora aperto lo spostamento deve fallire — senza di essa gli altri test non
proverebbero nulla, perché passerebbero anche se il rilascio non funzionasse.

#### Test instabile individuato e corretto durante la sessione

Una singola esecuzione della suite ha mostrato **1 fallimento non riproducibile** (169/170), poi
scomparso. Un test che fallisce a intermittenza è un difetto del test, non rumore: indagato invece di
ignorato. Il candidato per costruzione era `HitachiPathsManagerTests`, l'unica classe che manipola
**stato globale** — il file `hitachi_paths.json` nella cartella di output condivisa più la cache
statica di `HitachiPathsManager` — mentre xUnit esegue in parallelo classi di collection diverse.
Isolata con `[Collection("SharedBaseDirectoryState")]`, che la serializza rispetto a chiunque altro
dichiari la stessa collection. **Cinque esecuzioni consecutive successive: 170/170 ogni volta.**
Non essendo stato possibile catturare il nome del test fallito prima che il sintomo sparisse, la
correlazione restava la spiegazione più probabile ma non provata in quella sessione.
> **Aggiornamento, Sprint 8 (§6.1-decies): causa reale trovata, non era questa.** Ripetendo la
> suite più volte con log completo per ogni esecuzione, il test che falliva a intermittenza
> (~3 volte su 8) era in realtà `FileOperationRetryTests.ExecuteAsync_SuFileRealmenteBloccato_RiescQuandoIlLockVieneRilasciato`,
> non un effetto di `HitachiPathsManagerTests`. Il test avviava il tentativo di spostamento senza
> attendere che il thread "locker" avesse **davvero** aperto il file: se lo spostamento correva più
> veloce dell'apertura dello stream, riusciva subito (nessun lock esisteva ancora), e il locker
> trovava poi il file già spostato — `FileNotFoundException` o `IOException` a seconda del timing
> esatto, esattamente i due messaggi di errore osservati. Corretto aggiungendo un secondo
> `ManualResetEventSlim` che segnala l'avvenuta apertura dello stream, atteso prima di avviare la
> riprova. **12 esecuzioni consecutive dopo la correzione: 181/181 ogni volta** (era ~5/8 prima).
> L'isolamento di `HitachiPathsManagerTests` in una collection propria resta comunque una buona
> pratica indipendente e non è stato rimosso, ma non era la causa di quell'episodio.

**Build/test:** `dotnet build` → 0 errori, 0 warning. `dotnet test` → **170/170 superati**
(153 preesistenti + 17 nuovi), confermati su 5 esecuzioni consecutive. Eseguibile avviato
manualmente, nessuna eccezione allo startup.

### 6.1-decies Sprint 8 — prefisso "SR" in Aggiorna Ticket + rimozione del dialog di anteprima in PDF

Due modifiche indipendenti richieste dal committente, una per modulo.

#### 1. HOME — "Aggiorna Ticket" perdeva il prefisso "SR"

**Causa.** `HomeViewModel.OnAggiornaTicket` non passa mai da `LogDumpFolderName` (è uno dei 6
chiamanti ancora non migrati all'intervento 1.1, §6.3): sostituisce direttamente il primo token del
nome di cartella (`parts[0]`, che su disco è l'intero `"SR1234567"`) con il valore digitato
nell'omonima casella di testo. L'utente digita però il **solo numero** (`"1234567"`), non l'intero
token — il prefisso `"SR"` spariva quindi dal nome risultante, violando l'invariante §5.1
(`SR{ticket} {LOG|DUMP} {tipo} {loco} {software} {ddMMyy} {utente}`).

**Correzione:** `HomeViewModel.NormalizeTicketPrefix(string?)`, funzione pura applicata a `OldTicket`
e `NewTicket` prima di essere usati come sostituzione. Antepone `"SR"` se assente; se già presente (in
qualunque combinazione di maiuscole/minuscole) non lo duplica, e lo **normalizza in maiuscolo** —
necessario perché il resto della grammatica cerca il marcatore `"SR"` con un regex senza
`IgnoreCase` (`LogDumpFolderName.FolderPrefixRegex`): un `"sr1234567"` digitato dall'utente sarebbe
altrimenti rimasto non analizzabile dal parser condiviso, pur "sembrando" corretto a vista.

**Deliberatamente non fatto:** migrare `OnAggiornaTicket` a `LogDumpFolderName.TryParse`/`Format` per
intero. È il refactoring più ampio già rimandato nella roadmap (1.1, §6.3) — qui serviva una
correzione mirata al sintomo segnalato, non l'occasione per assorbire un intervento più grande e
rischioso senza che fosse stato richiesto.

**11 test** in `HomeViewModelTests.cs` (nuovo — prima questa classe non aveva copertura): i tre casi
del committente (assente → aggiunto, presente → non duplicato) più le varianti di maiuscole/minuscole
(`sr`, `Sr`, `sR` → tutte normalizzate a `SR`), spazi iniziali/finali, valore vuoto o solo spazi →
stringa vuota (non `"SR"` da solo), e la conferma che applicare la normalizzazione due volte di
seguito non produce `"SRSR..."`.

#### 2. PDF — rimosso il dialog di conferma prima della rinomina

Rimossa la sola chiamata `RenamePreviewDialog.Confirm(...)` in `PdfView.BtnRinomina_Click`
(intervento 4.1 dello Sprint 3): la rinomina calcolata da `PdfRenamePlanner` parte ora
immediatamente alla pressione del pulsante. **Tutto il resto del metodo resta invariato**, per
richiesta esplicita: l'overlay di avanzamento (`LoadingOverlay`, intervento 4.2) continua a mostrare
"Rinomina N di M..." durante le due fasi del salvataggio atomico (§5.7), e `RenamerLog.RecordBatch`
continua a scrivere lo storico su `renamer_log` a operazione completata — quindi "Annulla ultima
rinomina" (intervento 4.3) resta pienamente funzionante ed è ora l'unica rete di sicurezza contro un
parsing sbagliato, al posto della conferma preventiva.

`RenamePreviewDialog` **non è stata eliminata** dal codice: resta usata da
`HomeViewModel.OnAggiornaTicket`/`OnAggiornaData`, non toccate da questa richiesta (limitata al solo
modulo PDF). Nessun test esistente referenziava la classe dal lato PDF (è WPF, non testabile da
xUnit per costruzione, §6.5) — non c'era quindi nulla da aggiornare in `PersonalAutomationTool.Tests`
per questa metà dell'intervento.

#### Effetto collaterale: risolto il test instabile lasciato aperto dallo Sprint 7

La verifica di questa sessione ha riprodotto l'instabilità già notata (non risolta) nello Sprint 7:
su 8 esecuzioni consecutive della suite, 3 fallivano con un test diverso ogni volta apparentemente
casuale. Ripetendo con log completo per ogni run, il colpevole si è rivelato sempre lo stesso:
`FileOperationRetryTests.ExecuteAsync_SuFileRealmenteBloccato_RiescQuandoIlLockVieneRilasciato`
(§6.1-nonies) — non `HitachiPathsManagerTests`, come ipotizzato senza prova nello sprint precedente.
**Causa:** una race condition nel test stesso, non nel codice applicativo. Il thread che avrebbe
dovuto bloccare il file veniva avviato con `Task.Run` ma il test tentava lo spostamento subito dopo,
senza attendere che quel thread avesse *davvero* aperto lo stream: se lo spostamento vinceva la
gara, riusciva immediatamente (nessun lock esisteva ancora) e il thread "locker" trovava poi il file
già spostato — da cui `FileNotFoundException` o `IOException`, a seconda di quale istante esatto
vincesse. Corretto con un secondo `ManualResetEventSlim` segnalato subito dopo l'apertura dello
stream, atteso dal thread principale prima di avviare la riprova. **12 esecuzioni consecutive dopo
la correzione: 181/181 ogni volta** (era ~5/8 prima). Vedi §6.1-nonies per la correzione del
resoconto: quella sessione aveva applicato un rimedio (isolare `HitachiPathsManagerTests` in una
collection propria) che non era sbagliato in sé ma non era la causa del sintomo osservato.

**Build/test:** `dotnet build` → 0 errori, 0 warning. `dotnet test` → **181/181 superati**
(170 preesistenti + 11 nuovi), confermati su 12 esecuzioni consecutive. Eseguibile avviato
manualmente, nessuna eccezione allo startup.

### 6.1-undecies Sprint 9 — tre correzioni sul modulo PASSAGGIO DI CONSEGNE

Il committente ha chiesto tre correzioni puntuali su `PassaggioConsegneView`/`PassaggioConsegneViewModel`.
Su un punto (§3, il fallback "No") la richiesta iniziale era ambigua sullo scope esatto: chiesto un
chiarimento invece di indovinare, dato che l'esito finisce in un documento aziendale reale mandato via
email — il committente ha risposto con uno screenshot che ha reso lo scope inequivocabile.

#### 1. Data odierna non aggiornata all'apertura

**Causa.** `RapportinoTurnoModel.Data` è inizializzata correttamente a oggi da un field initializer
(`DateTime.Now.ToString("dd/MM/yyyy")`). Ma il costruttore di `PassaggioConsegneViewModel` chiama
subito dopo `CaricaDati()`, che — se `passaggio_consegne.json` esiste da una sessione precedente —
**sostituisce l'intero oggetto** `RapportinoTurnoModel` con quello deserializzato dal file, portando
con sé anche il valore di `Data` congelato al momento dell'ultimo salvataggio. Riaprendo l'app in un
giorno diverso, la data corretta impostata dal costruttore veniva quindi silenziosamente scartata.

**Correzione:** `PassaggioConsegneViewModel.EnsureDataOdierna(RapportinoTurnoModel)`, metodo puro
(nessuna dipendenza da I/O o stato statico) che imposta `Data` a `DateTime.Today.ToString("dd/MM/yyyy")`
passando dal setter reale della proprietà — quindi la notifica di cambio proprietà scatta
correttamente e il binding `TwoWay` esistente si aggiorna senza bisogno di altre modifiche XAML.
Chiamato per tutti e tre i rapportini subito dopo `CaricaDati()` nel costruttore, incondizionatamente
(sia che un salvataggio precedente sia stato trovato sia che non lo sia stato — nel secondo caso è un
riassegnamento innocuo dello stesso valore già corretto).

**4 test** in `PassaggioConsegneViewModelTests.cs`: sovrascrittura di una data di un giorno diverso,
nessuna alterazione se la data è già odierna, notifica di `PropertyChanged` effettivamente sollevata,
applicazione indipendente a tre rapportini distinti senza interferenze reciproche.
`PassaggioConsegneViewModel` non è testata direttamente (il suo costruttore innesca I/O su disco e
sottoscrizioni a eventi statici) — stesso limite già documentato per `HomeViewModel`/`VerificheViewModel`,
da cui solo funzioni pure estratte sono coperte da test.

#### 2. Checkbox non visibili nel PDF esportato, sostituite da "Sì"/"No"

Le cinque colonne booleane di "Dettaglio interventi" (Compilazione ODL, Chiusura Ticket, Comp.
Report, Email Ingegneria, Aggiornare Verifiche) mostravano già un'etichetta testuale accanto alla
checkbox, ma nella forma `"SI"`/`"NO"` (tutto maiuscolo, senza accento) invece di `"Sì"`/`"No"` come
richiesto — corretto in `DettaglioInterventoRow` (`PassaggioConsegneModels.cs`), 5 proprietà,
getter e setter aggiornati insieme (il setter confrontava `value == "SI"`: lasciarlo con la vecchia
stringa avrebbe rotto silenziosamente la deserializzazione di dati salvati con la nuova forma).

**Il problema principale — il quadratino grafico della checkbox compariva comunque nel PDF —**
richiedeva un meccanismo di "modalità export", dato che `PassaggioConsegnePdfExporter` cattura con
`RenderTargetBitmap` esattamente ciò che è a schermo in quel momento (WYSIWYG, nessun percorso di
rendering alternativo per la stampa). Aggiunta `PassaggioConsegneViewModel.IsExporting` (bool),
`False` di default. In XAML, ogni cella booleana diventa una `Grid` con **due** elementi sovrapposti:
la `CheckBox` originale (invariata: stesso `IsChecked`, stesso `Content`) più un `TextBlock` con lo
stesso testo "Sì"/"No". Due stili condivisi (`ExportAwareCheckBoxStyle`/`ExportAwareSiNoTextStyle`)
usano un `DataTrigger` su `IsExporting` per invertirne la visibilità: normalmente si vede solo la
checkbox (comportamento a schermo identico a prima), durante l'esportazione si vede solo il
`TextBlock`. Il binding risale fino allo `UserControl` (`RelativeSource AncestorType=UserControl`)
perché il `DataContext` del `CellTemplate` di una riga `DataGrid` è la riga stessa
(`DettaglioInterventoRow`), non il ViewModel.

`PassaggioConsegneView.BtnPassaggioConsegne_Click` imposta `IsExporting = true`, esegue l'export
dentro un `try`, lo riporta a `false` in un `finally`. **Nessuno sfarfallio percepibile per
l'utente**, non per una temporizzazione ad hoc ma per una proprietà del modello di binding di WPF:
l'intero ciclo (impostare `IsExporting`, propagazione sincrona del binding/trigger, invalidazione del
layout, `Measure`/`Arrange`/`UpdateLayout` dentro `ExportToPdf`, `RenderTargetBitmap.Render`,
ripristino di `IsExporting`) avviene nello stesso blocco sincrono, senza mai un `await` o un
`Dispatcher.Invoke` di mezzo — quindi WPF non ha mai l'occasione di eseguire un passaggio di
rendering verso lo **schermo** con lo stato "esportazione" attivo, anche se quello stato esiste
realmente (e correttamente) nel momento in cui la bitmap viene catturata.

#### 3. Fallback "No" nelle celle vuote — solo ultime 4 colonne, solo righe compilate

Richiesta chiarita con il committente dopo un primo giro ambiguo (screenshot della tabella
Movimenti): il fallback si applica **solo** a DATA INGRESSO/ORA INGRESSO/DATA USCITA/ORA USCITA (non
a TRENO/LOCO, già popolate correttamente dall'autocompilazione da VERIFICHE) e **solo** sulle righe
già "compilate" — cioè con Treno o Loco presenti. Le righe fra le dieci pre-create ancora
interamente inutilizzate restano vuote anche in quelle 4 colonne.

`RowFilledEmptyToNoConverter` (`PassaggioConsegneConverters.cs`), `IMultiValueConverter` puro: prende
in ingresso `[Treno, Loco, valore del campo]` via `MultiBinding` su ciascuna delle 4 colonne. Se il
campo ha un valore lo restituisce invariato; se è vuoto restituisce `"No"` quando Treno o Loco non
sono vuoti, altrimenti stringa vuota. **Il fallback non viene mai scritto nel modello**: `ConvertBack`
scrive nella sola cella modificata (gli altri due valori del `MultiBinding`, Treno e Loco, restano
inalterati tramite `Binding.DoNothing`) esattamente ciò che il tecnico digita — `passaggio_consegne.json`
continua a contenere stringa vuota per un campo mai compilato, mai la lettera "No": il fallback resta
un fatto di presentazione, non di dati salvati. Essendo un `MultiBinding` su Treno e Loco, la cella si
riaggiorna automaticamente anche quando quei due campi vengono compilati in un momento successivo
dall'autocompilazione da VERIFICHE, non solo quando cambia la cella stessa.

**8 test** in `RowFilledEmptyToNoConverterTests.cs`: campo valorizzato restituito invariato (anche con
spazi interni, per escludere che vengano scambiati per vuoti), fallback "No" con solo Treno presente,
fallback "No" con sola Loco presente, riga interamente vuota → stringa vuota (il caso che distingue
questo converter da un fallback ingenuo "vuoto → No"), Treno/Loco fatti di soli spazi trattati come
assenti, `ConvertBack` che scrive solo il terzo valore lasciando gli altri due a `Binding.DoNothing`,
valore nullo in `ConvertBack` gestito senza eccezioni.

#### Non toccato, deliberatamente

`PassaggioConsegneViewModel.OpzioniSiNo` (`ObservableCollection<string>` con `"SI"`/`"NO"`) non è
referenziata da alcun binding XAML — verificato con una ricerca su tutto il modulo, non per
supposizione: è codice morto preesistente. Lasciata invariata perché normalizzarla non era richiesto
e avrebbe ampliato lo scope oltre quanto chiesto per una collezione che, non essendo usata, non ha
alcun effetto osservabile.

**Build/test:** `dotnet build` → 0 errori, 0 warning. `dotnet test` → **204/204 superati**
(181 preesistenti + 23 nuovi), confermati su 5 esecuzioni consecutive. Eseguibile avviato
manualmente, nessuna eccezione allo startup.

**Cosa non è stato verificato in questo ambiente** (limite dell'ambiente, non del codice — stesso
limite dichiarato più volte in sessioni precedenti): senza un tool di automazione UI Windows non è
stato possibile verificare **a schermo** che il PDF esportato mostri davvero "Sì"/"No" testuale senza
il quadratino, che il fallback "No" compaia correttamente nella tabella Movimenti, e che riaprendo
l'app con un `passaggio_consegne.json` di un giorno precedente la data mostrata sia davvero quella
odierna. Sono stati verificati: build pulita, compilazione XAML senza errori (che valida
staticamente le chiavi delle risorse, i `MultiBinding` e i `RelativeSource`), i 23 test sulla logica
pura, e l'avvio dell'app senza eccezioni. Vedi il punto 28 della checklist §7.1.

#### Correzione successiva, stessa sessione: `RelativeSource AncestorType=UserControl` inaffidabile su righe aggiunte dopo il caricamento

Il committente ha verificato il punto 2 sul campo e segnalato: le righe presenti al caricamento
mostravano correttamente "Sì"/"No" accanto alla checkbox, ma le righe aggiunte con "+ Aggiungi
Intervento" **dopo** mostravano la checkbox senza alcuna etichetta.

Il binding `{Binding DataContext.IsExporting, RelativeSource={RelativeSource AncestorType=UserControl}}`
usato nei due stili `ExportAwareCheckBoxStyle`/`ExportAwareSiNoTextStyle` risaliva un percorso lungo e
attraversato da molti livelli intermedi generati (DataGrid → righe → `Border` → `StackPanel` →
`ScrollViewer` → `UserControl`). Per le righe presenti fin dal primo caricamento, quando l'intero
albero visuale viene costruito in un'unica passata connessa dall'alto verso il basso, questo percorso
si risolve correttamente. Per una riga aggiunta **dopo**, il container della sua cella può essere
generato dal `DataGrid` prima che quella catena lunga sia interamente connessa — un problema noto di
WPF con `RelativeSource`/`AncestorType` dentro `ItemsControl` virtualizzati, non specifico di questa
applicazione.

**Corretto accorciando il percorso di risalita da `AncestorType=UserControl` a
`AncestorType=DataGrid`**: il `DataGrid` immediatamente contenitore esiste per costruzione prima che
una sua cella possa esistere, quindi la risalita a un solo livello è garantita indipendentemente da
quando la riga è stata aggiunta. È il pattern standard e documentato per raggiungere il `DataContext`
di un ViewModel da dentro un `DataGridTemplateColumn.CellTemplate`, più robusto di una risalita a un
antenato distante.

> ⚠️ **Onestà sul livello di certezza.** Non è stato possibile riprodurre il problema in questo
> ambiente (nessun tool di automazione UI Windows disponibile, §6.1-undecies): la diagnosi si basa
> su un meccanismo WPF noto e ben documentato per questa esatta combinazione (RelativeSource +
> ItemsControl virtualizzato + riga aggiunta a runtime), non su un'osservazione diretta del difetto.
> La correzione è comunque **strettamente migliorativa e a rischio nullo** — un percorso di risalita
> più corto e più standard non può peggiorare nulla — ma se il sintomo dovesse persistere dopo questo
> fix, serve sapere: (a) se accade sempre o solo a volte, (b) se scorrere la griglia o cambiare
> scheda e tornare indietro fa ricomparire l'etichetta (indicherebbe un problema di timing alla
> creazione del container, coerente con questa diagnosi) o se resta assente anche dopo (indicherebbe
> una causa diversa, probabilmente nei dati piuttosto che nel rendering).

**Build/test dopo la correzione:** `dotnet build` → 0 errori, 0 warning. `dotnet test` → 204/204
superati (nessun nuovo test: il comportamento è puramente di rendering WPF, non coperto dalla suite
per lo stesso motivo per cui `RenamePreviewDialog`/`ProgressOverlay` non lo sono, §6.5). Eseguibile
avviato manualmente, nessuna eccezione.

#### Terzo giro, stessa sessione: la causa vera non era quella sopra

Il committente ha allegato un secondo screenshot dopo il fix precedente: il sintomo era peggiorato,
non risolto — una riga mostrava **solo testo** "Sì"/"No" **senza alcuna checkbox visibile**, un'altra
non mostrava **né** checkbox **né** testo. Questo esclude direttamente l'ipotesi precedente (un
binding che fallisce a risolversi lascerebbe la checkbox visibile con la sua Content, mai il
contrario) e ha indicato la causa reale: `IsExporting` era **visibilmente `true` a schermo**, non
solo durante la cattura — la modalità "esportazione" (checkbox nascosta, solo testo) stava
comparendo nell'interfaccia normale, non nel solo PDF.

**Causa.** `BtnPassaggioConsegne_Click` teneva `IsExporting = true` per tutta la durata del blocco
`try`, che includeva **anche** `PassaggioConsegneEmailService.OpenDraftEmail` — chiamata che crea un
oggetto COM Outlook (`Activator.CreateInstance`, poi `mailItem.Display`). Una chiamata verso un
server COM STA fuori processo può **pompare internamente la coda messaggi di Windows** durante
l'attesa del marshalling — la stessa coda da cui WPF pesca i propri cicli di rendering — e questo dà
a WPF l'occasione di disegnare un frame con `IsExporting` ancora attivo mentre Outlook si avvia
(operazione da secondi, non istantanea: tempo più che sufficiente perché l'utente veda lo stato
intermedio, altro che "nessuno sfarfallio"). L'assunto scritto nel commento della proprietà —
"tutto resta sincrono quindi nessuno sfarfallio" — era vero **solo** per `ExportToPdf` (I/O e
rendering WPF puri, nessuna chiamata COM, nessun pompaggio messaggi) e falso includendo
`OpenDraftEmail`, che invece ci stava dentro. La seconda riga senza né checkbox né testo era
probabilmente un frame "strappato", catturato a metà di un passaggio di layout/render mentre la UI
restava bloccata in attesa di Outlook — coerente con la stessa causa, non un difetto separato.

**Corretto restringendo la finestra `IsExporting = true` alla sola chiamata a `ExportToPdf`**:
`OpenDraftEmail` parte ora **dopo** che `IsExporting` è già tornato a `false`, fuori dal blocco
`try`/`finally` che lo protegge. La cattura `RenderTargetBitmap` in sé non coinvolge COM né pompaggio
di messaggi, quindi confinare la modalità "esportazione" a quel solo passo elimina la finestra di
tempo in cui poteva diventare visibile.

**Lezione per letture future:** l'ipotesi del giro precedente (`RelativeSource AncestorType`
inaffidabile su righe aggiunte a runtime) resta un miglioramento legittimo e a rischio nullo — non è
stata ritirata — ma **non era la causa di questo sintomo**. La diagnosi corretta è arrivata solo
confrontando **due** screenshot in sequenza (prima: checkbox visibile senza testo; dopo il primo fix:
testo visibile senza checkbox) — un singolo screenshot non l'avrebbe resa distinguibile dall'ipotesi
sbagliata. Se una correzione basata su un meccanismo "noto ma non osservato" non risolve un sintomo
segnalato, il passo successivo è cercare cosa **cambia** fra i due tentativi, non solo se il sintomo
persiste.

**Build/test dopo la correzione:** `dotnet build` → 0 errori, 0 warning. `dotnet test` → 204/204
superati. Eseguibile avviato manualmente, nessuna eccezione.

> ⚠️ **Ancora da confermare sul campo.** Anche questa correzione non è stata verificabile a schermo in
> questo ambiente (stesso limite dichiarato sopra). La spiegazione è però più solida della precedente:
> non si basa su un meccanismo WPF "noto in generale", ma su una lettura diretta e specifica del
> codice che collega esattamente la sequenza osservata (checkbox nascosta + solo testo, a schermo, per
> un tempo percepibile) a un'operazione COM realmente presente nello stesso blocco sincrono.

#### Quarto giro, stessa sessione: causa reale trovata, era strutturale — non di timing

Il committente ha allegato **due** screenshot in sequenza (schermo e PDF generato dallo stesso stato)
dopo il terzo fix: il sintomo era identico a prima, sulla stessa riga, **in entrambe le modalità**.
Questo è il dato decisivo che ha smontato **entrambe** le ipotesi precedenti: se il problema fosse
stato di *timing* (checkbox/testo che si scambiano nel momento sbagliato, come nei tentativi 2 e 3),
i due screenshot — schermo normale e PDF — avrebbero dovuto mostrare esiti **diversi** fra loro. Li
mostravano invece **identici** sulla stessa riga: l'etichetta "Sì"/"No" mancava lì a prescindere da
quale elemento (`CheckBox.Content` o `TextBlock.Text`) fosse quello visibile in quel momento. Il
problema non era MAI stato "quale elemento si vede", ma "cosa mostra l'elemento", in entrambi i casi.

**Causa reale.** Ogni cella booleana aveva due binding **diversi** sulla stessa riga: `IsChecked` legge
`CompilazioneOdlBool` (il campo bool) **direttamente**; `Content`/`Text` leggevano `CompilazioneOdl`
(una proprietà **stringa derivata**, il cui `PropertyChanged` viene sollevato **a mano** dentro il
setter del bool: `OnPropertyChanged(nameof(CompilazioneOdl))`). In tutti gli screenshot, lo stato
spuntato/despuntato della checkbox (`IsChecked`) era **sempre** corretto; solo l'etichetta derivata
falliva, sulla riga che cambiava di prova in prova. L'unica differenza strutturale fra un binding che
funzionava sempre e uno che falliva a intermittenza sulla stessa riga era esattamente quel salto in
più — la notifica propagata manualmente su una proprietà derivata, invece di un binding diretto sulla
sorgente. Non è stato necessario isolare il meccanismo WPF esatto per cui quel salto falliva talvolta
(virtualizzazione/rigenerazione dei container di una riga, con ordine di applicazione dei binding
diverso da quello di `IsChecked` — la spiegazione più plausibile, ma qui non decisiva): bastava
eliminare il salto.

**Corretto eliminando le proprietà stringa derivate dal percorso di visualizzazione.**
`BoolToSiNoConverter` (nuovo, in `PassaggioConsegneConverters.cs`): converter stateless
`bool → "Sì"/"No"`. `Content` e `Text` delle 5 celle ora leggono lo **stesso** campo bool di
`IsChecked` (es. `Content="{Binding CompilazioneOdlBool, Converter={StaticResource BoolToSiNoConverter}}"`),
non più `CompilazioneOdl`. Se `IsChecked` è corretto — e lo era sempre, in ogni screenshot — ora lo è
anche il testo, perché condividono **esattamente lo stesso binding**, non due binding paralleli su
proprietà diverse che potevano disallinearsi. Le proprietà stringa derivate (`CompilazioneOdl`,
`ChiusuraTicket`, `CompReport`, `EmailIngegneria`, `AggiornareVerifiche`) restano nel modello,
corrette, semplicemente non più usate da questa vista.

**8 test** in `BoolToSiNoConverterTests.cs`: mappatura `true`→"Sì"/`false`→"No", valori non booleani
(inclusi `null`) trattati come "No" invece di lanciare, `ConvertBack` che riconosce solo la stringa
esatta "Sì" come vero.

**Lezione, la più concreta delle quattro di questa sessione:** le prime due correzioni erano basate su
meccanismi WPF "noti in generale" applicati per analogia, non su una lettura che spiegasse **perché
proprio quella riga**, ogni volta diversa. La correzione risolutiva è arrivata confrontando cosa
**cambiava** e cosa **restava costante** fra un binding funzionante (`IsChecked`) e uno che falliva
(`Content`/`Text`) sulla stessa identica istanza di riga, nello stesso identico momento — non
cercando un meccanismo WPF plausibile, ma cercando la differenza strutturale fra i due binding.

**Build/test dopo la correzione:** `dotnet build` → 0 errori, 0 warning. `dotnet test` → **212/212
superati** (204 preesistenti + 8 nuovi), confermati su 3 esecuzioni consecutive. Eseguibile avviato
manualmente, nessuna eccezione.

> ⚠️ **Ancora da confermare sul campo**, per lo stesso limite d'ambiente dichiarato sopra — ma qui la
> certezza è più alta delle tre correzioni precedenti: non elimina "un rischio teorico", elimina
> l'unica differenza strutturale osservata fra un binding-controllo positivo (`IsChecked`, sempre
> corretto in ogni screenshot) e il binding difettoso (`Content`/`Text`).

### 6.1-duodecies Sprint 10 — rimozione completa del modulo PASSAGGIO DI CONSEGNE in vista della riscrittura da zero

> ⚠️ **Nota di lettura per chi arriva qui in futuro.** Questa voce documenta una **rimozione**, non
> un'evoluzione: il codice descritto nelle sezioni §4.13, §6.1-bis (scoperta #3), §6.1-sexies
> (criticità E/F), §6.1-nonies, §6.1-decies e §6.1-undecies **non esiste più** nel repository a partire
> da questa sessione. Quelle sezioni **non sono state riscritte al passato** e restano come registro
> storico di cosa fu fatto e perché, utile soprattutto come lezione tecnica per la riscrittura (vedi
> "Cosa resta utile per la riscrittura" più sotto) — ma qualunque riferimento a un file, un metodo o un
> comportamento **attuale** del modulo PASSAGGIO DI CONSEGNE in quelle sezioni è da considerare
> obsoleto. Lo stato corrente è descritto in §1, §2.1, §2.2, §2.5, §2.6, §2.7.

**Richiesta del committente (23/08/2026).** Riprogettare completamente da zero il modulo "Passaggio di
Consegne", a partire da una codebase ripulita da ogni residuo della vecchia implementazione, mantenendo
la soluzione compilabile. Le nuove specifiche dettagliate arriveranno in una sessione successiva: questa
voce copre **solo** la rimozione, non ancora la riscrittura.

**Rimozione fisica dei file dedicati** (`PersonalAutomationTool/modules/passaggio_consegne/`, intera
cartella eliminata):
- `PassaggioConsegneView.xaml` + `PassaggioConsegneView.xaml.cs`
- `PassaggioConsegneViewModel.cs`
- `PassaggioConsegneModels.cs` (`RapportinoTurnoModel`, `MovimentoTrenoRow`, `DettaglioInterventoRow`,
  `InterventoNonSvoltoRow`, `RapportiniDataContainer`)
- `PassaggioConsegneConverters.cs` (`RowFilledEmptyToNoConverter`, `BoolToSiNoConverter`)
- `PassaggioConsegnePdfExporter.cs`
- `PassaggioConsegneEmailService.cs`

**Test dedicati rimossi** (`PersonalAutomationTool.Tests/Modules/PassaggioConsegne/`, intera cartella
eliminata): `PassaggioConsegneViewModelTests.cs`, `PassaggioConsegneModelsTests.cs`,
`RowFilledEmptyToNoConverterTests.cs`, `BoolToSiNoConverterTests.cs` — 31 test in tutto (23 + 8, vedi
§6.1-undecies per la loro origine).

**Disaccoppiamento dal resto dell'app:**
- `MainWindow.xaml` — rimosso il pulsante `BtnPassaggioConsegne` dalla navbar.
- `MainWindow.xaml.cs` — rimossi lo `using PersonalAutomationTool.Modules.PassaggioConsegne;` e il
  metodo `Nav_PassaggioConsegne`.
- `DestinatariManager.cs` (`modules/destinatari_mail/`) — rimosso `EnsurePassaggioConsegneActions`
  (il metodo che, a ogni `LoadConfig()`, inseriva una voce "Passaggio di consegne" mancante in
  `destinatari.json`) insieme alla sua chiamata da `LoadConfig()`, e rimosse le 5 voci
  `ActionName = "Passaggio di consegne"` dalla configurazione di default generata per E404P, ETR700,
  ETR1000, ETR1000IF, ETR1000FH. **`DestinatariManager.GetRecipients(trainType, actionName)` è stato
  mantenuto**: è un'API generica (qualunque coppia treno/azione), non esclusiva del modulo rimosso, e
  resta utilizzabile dalla riscrittura per recuperare i destinatari se la nuova implementazione
  riprenderà l'invio email.
- `HomeView.xaml` — aggiornato un commento che confrontava la virtualizzazione di `HomeView` con
  `PassaggioConsegneView` (la vista non esiste più, il confronto non aveva più senso).
- Nessun'altra istanziazione, `using` o riferimento residuo trovato (verificato con una ricerca
  case-insensitive di "PassaggioConsegne" sull'intero repository, fuori da questo file).

**Dato non toccato.** `PersonalAutomationTool/bin/Debug/net10.0-windows/data/passaggio_consegne.json`
(output di build, contiene gli ultimi rapportini salvati da esecuzioni precedenti) **non è stato
cancellato**: è dato generato a runtime, non codice, e non impedisce la build. Nessun codice lo legge o
scrive più. Vedi §2.6.

**Verifica.** `dotnet clean` sull'intera `.sln`, poi `dotnet build` → **0 errori, 0 warning** su
`PersonalAutomationTool` e `PersonalAutomationTool.Tests` (i 2 warning NU1510 residui sono preesistenti
su `TestClosedXML`, fuori scope, vedi §6.5). `dotnet test` → **181/181 superati** (erano 212 prima
della rimozione dei 31 test dedicati).

**Cosa resta utile per la riscrittura** (lezioni dal codice rimosso, da rileggere quando arriveranno le
nuove specifiche, non da riapplicare automaticamente):
- **Cattura a bitmap per l'export PDF.** `PassaggioConsegnePdfExporter` catturava l'intera vista con
  `RenderTargetBitmap` e imponeva un tetto di 8 milioni di pixel (riducendo i DPI, non le dimensioni
  logiche) per non finire nella Large Object Heap su hardware con poca RAM — vedi §4.13 e §6.1-sexies
  (criticità E). Se la riscrittura userà ancora un export "a schermata", questo vincolo va riproposto.
- **Rilascio COM Outlook.** `PassaggioConsegneEmailService.OpenDraftEmail` seguiva lo stesso pattern di
  `EmailService`: riferimenti COM dichiarati fuori dal `try`, rilasciati in `finally` con una funzione
  che assorbe gli errori del singolo rilascio — vedi §4.4.
- **Autocompilazione da VERIFICHE.** Il vecchio ViewModel si agganciava a
  `VerificheViewModel.OnVerificheDataUpdated` (evento statico, ancora presente e generico, §2.5) per
  popolare la tabella Movimenti; la lettura iniziale era stata spostata su thread pool per non bloccare
  l'apertura del modulo — vedi §6.1-sexies (criticità F).
- **Sottoscrizione statica mai rilasciata.** Era la criticità **D** aperta in §6.4: la lambda su
  `OnVerificheDataUpdated` non aveva mai un `-=` corrispondente. Innocuo solo perché `MainWindow` tiene
  una sola istanza per vista; da non ripetere ciecamente se la riscrittura cambia quel presupposto.
- **Formato dati storico.** `RapportiniDataContainer` (Etr700/Etr1000/Etr500, ciascuno un
  `RapportinoTurnoModel`) serializzato in `data\passaggio_consegne.json`. Se la riscrittura deve
  recuperare i rapportini già salvati dai tecnici, questo è lo schema da leggere per un'eventuale
  migrazione — il file fisico non è stato toccato (vedi sopra).
- **Le tre correzioni di prodotto dello Sprint 9** (§6.1-undecies): data odierna forzata dopo il
  caricamento, checkbox nascoste a favore del testo "Sì"/"No" nell'export, fallback "No" limitato alle
  righe già compilate — tutti requisiti comportamentali confermati dal committente sul vecchio modulo,
  probabilmente da riproporre nella riscrittura salvo indicazione contraria nelle nuove specifiche.

**In attesa delle nuove specifiche del committente.** Nessuna decisione di design per la riscrittura è
stata presa in questa sessione: questa voce si ferma alla rimozione pulita.

### 6.1-terdecies Sprint 11 — igiene del repository: eliminazione dei residui scratch e untracking dei build artifact

**Richiesta del committente (23/08/2026, subito dopo lo Sprint 10).** Ripulire la cartella di lavoro da
file orfani, temporanei e residui di vecchie prove, mantenendo la soluzione funzionante al 100%, con un
audit preventivo da approvare prima di ogni cancellazione.

**Scoperta che ha guidato tutto lo sprint.** Il `.gitignore` era **già corretto e completo** (ignorava
`bin/`, `obj/`, `scratch/`, `build_*.txt`, `.DS_Store` da §6.1-bis). Il problema non era una regola
mancante: quei file erano stati committati **prima** che il `.gitignore` esistesse, e Git non applica le
regole di ignore retroattivamente ai file già tracciati. La correzione non era quindi in `.gitignore` ma
in `git rm --cached`. **Chi in futuro trovasse file ignorati ma ancora tracciati non cerchi il bug nel
`.gitignore`.**

**Audit preventivo (Fase 1).** 805 file tracciati, di cui **669 artefatti di build (83%)**. Verificati
**17 tipi candidati a "classe orfana"** (`InverseBooleanToVisibilityConverter`, `RenamePreviewDialog`,
`ProgressOverlay`, `TrainCardModel`, `FileOperationRetry`, `MouseWheelScrollBehavior`,
`PendingMaintenanceModel`, `ExcelFieldViewModel`, `FlotteCache`, `RenamerLog`, `LogDumpFolderName`,
`ExcelFolderParser`, `PdfRenamePlanner`, `VerificheExcelReader`, `ShortcutsManager`, `TrainViewHelper`,
`ReportInterventiWriter`): **nessuna classe orfana**, ognuna ha un consumatore reale.
`ReportInterventiWriter` è l'unica con 0 riferimenti nell'app, ma è **dead code deliberato** (§5.4,
§6.1-septies) coperto da 30 test: **tenuto**.

**Eliminati:**

| Elemento | Peso | Perché |
|---|---|---|
| `TestClosedXML/` (152 file tracciati) | 104 MB | Harness console **manuale** (129 righe, 3 test): non gira con `dotnet test`, va lanciato a mano. Superato da `PersonalAutomationTool.Tests`. Rimosso prima dalla `.sln` con `dotnet sln remove`, poi da disco |
| `scratch/` (92 file tracciati) | 64 MB | Già in `.gitignore` ma tracciata: `test.zip`, un pacchetto `.xlsx` estratto, `debug_ticket.txt`, un progetto `ExcelTest` mai nella `.sln` |
| `ep_test.cs`, `test.cs` (root) | 2,4 KB | **Fuori da ogni `.csproj` → mai compilati.** UTF-16 su riga singola, path hardcoded a un profilo utente non più esistente. `test.cs` fa `XLWorkbook.Save()`, cioè **l'operazione vietata dall'invariante §5.4**: tenerlo era un rischio di copia-incolla |
| `build_last.txt`, `build_out.txt` | 30 KB | Log di build committati per errore, già in `.gitignore` |
| `.DS_Store` × 3 | 24 KB | Metadati macOS, inutili su Windows, già in `.gitignore` |

**Untracked ma NON cancellati da disco:** i 669 file sotto `bin/` e `obj/`, con
`git rm -r --cached`. Scelta esplicita del committente: l'eseguibile compilato resta utilizzabile e i
file di configurazione già personalizzati nella cartella di output (`destinatari.json`,
`shortcuts.json`) non vengono persi. Verificato dopo `dotnet clean` che `destinatari.json` sopravvive —
`clean` rimuove solo gli output tracciati da MSBuild, non i file generati a runtime.

**La copertura di test NON è stata sacrificata alla pulizia.** `TestClosedXML/Program.cs` conteneva 3
verifiche, e il controllo ha rivelato che **nessuna delle tre aveva un equivalente in xUnit**:
`ExcelViewModel.MatchesTrain`, il parsing di `ChiusuraTicketDialog` e il round-trip di
`DestinatariManager`. Cancellare il progetto senza altro avrebbe quindi *ridotto* la copertura, non solo
la dimensione del repository. Prima della cancellazione:
- `ExcelViewModel.MatchesTrain` è passata da `private static` a **`internal static`** — stesso
  trattamento già usato per `EmailService.BuildHtmlBody` e `HomeViewModel.NormalizeTicketPrefix`,
  sfruttando l'`InternalsVisibleTo` già presente in `AssemblyInfo.cs`. **Nessuna riflessione**: il vecchio
  harness la invocava via `BindingFlags.NonPublic`, il test nuovo la chiama direttamente.
- Creato `ExcelViewModelMatchesTrainTests.cs` (**21 test Tier 1**): i 3 casi ereditati dall'harness più
  l'estensione a tutte le grafie con cui la variante I-F compare sui nomi reali (`1000IF`, `Italia`,
  `Francia`, `ITA-FRA`, `I-F`), verificata in **entrambe le direzioni** come richiede §5.3-bis, più
  case-insensitivity e il caso in cui il *percorso* contiene un token di flotta diverso dal *nome file*.

> ⚠️ **Restano scoperte** le altre 2 verifiche dell'harness: il parsing di `ChiusuraTicketDialog`
> (istanzia un dialog WPF, richiede STA) e il round-trip di `DestinatariManager` (scrive
> `destinatari.json` nella cartella di output). Non sono state portate perché richiedono WPF e I/O reale
> sulla cartella di build, fuori dal perimetro **per costruzione** di `PersonalAutomationTool.Tests`
> (§2.1: zero dipendenza da WPF). Sono ora **debito di test dichiarato**, non copertura persa in
> silenzio: chi vorrà colmarlo dovrà prima decidere se ammettere test WPF nel progetto.

> ⚠️ **Nota su `MatchesTrain`, da non confondere.** Opera sui **nomi dei file "Report Interventi"**, non
> sui nomi di sottocartella LOG/DUMP: **non** delega a `ExcelFolderParser` e i 44 test di
> `ExcelFolderParserTests` **non la coprivano**. Sono due percorsi indipendenti che implementano la
> stessa distinzione di flotta: una modifica all'uno non è coperta dai test dell'altro. Se un giorno si
> unificassero, questa duplicazione sparirebbe — ma sarebbe un intervento di §6.2, non di pulizia.

**Verifica.** `dotnet clean` + `dotnet build` sull'intera `.sln` → **0 errori e 0 warning assoluti**.
È un miglioramento reale rispetto a tutti gli sprint precedenti, che chiudevano con "0 warning *sui
progetti principali*, più 2 preesistenti in `TestClosedXML`": quei 2 `NU1510` venivano da
`System.Drawing.Common` in `TestClosedXML.csproj` — pacchetto che, verificato, **`Program.cs` non usava
nemmeno**, così come non usava `ClosedXML` né `EPPlus` (i 3 `PackageReference` erano tutti orfani).
Eliminato il progetto, il rumore di build è sparito con lui. `dotnet test` → **202/202 superati**.

**Risultato: da 805 a 97 file tracciati** (-88%), ~168 MB liberati, due soli progetti nella `.sln`,
entrambi reali.

**Lezione per le prossime sessioni.** Un progetto "usa e getta" aggiunto alla `.sln` per una verifica
rapida è sopravvissuto per mesi, accumulando 104 MB, 3 dipendenze mai usate e i 2 unici warning della
build — e per giunta era diventato l'unico custode di una verifica che nessun altro test copriva, il che
ha reso la sua rimozione più delicata di quanto sembrasse. Per una verifica rapida, un test in
`PersonalAutomationTool.Tests` costa meno e non lascia sedimenti.

### 6.1-quaterdecies Sprint 12 — riscrittura da zero del modulo PASSAGGIO CONSEGNE

**Contesto.** Lo Sprint 10 (§6.1-duodecies) aveva rimosso il vecchio modulo su richiesta del
committente. Qui viene ricostruito da zero sulla base del **template Excel reale**
("rapportino di turno.xlsx"), fornito in questa sessione e prima mai visto: è la fonte di verità della
struttura, e averlo ha cambiato diverse scelte rispetto alla vecchia implementazione.

#### Struttura letta dal template (fonte di verità)

Il workbook ha 3 fogli operativi — `ETR500`, `ETR700`, `ETR1000` — **identici fra loro tranne il
sottotitolo di riga 4**. Area di stampa `A1:I34`, `fitToPage` al **63%**, A4 **orizzontale**, centrato.

| Blocco | Righe Excel | Struttura colonne (merge) | Righe dati |
|---|---|---|---|
| Intestazione | 1-2 | logo in A; `B1:G1` titolo, `H1` DATA, `I1` valore; riga 2: NOME `B2:C2`, COGNOME `E2:F2`, ORA-INIZIO/FINE `H2`/`I2` | — |
| Attività richieste da ingegneria | 3-15 | `A` \| `B:C` \| `D:E` \| `F` \| `G` \| `H` \| `I` | **10** |
| Dettaglio interventi | 16-22 | `A` \| `B:D` \| `E` \| `F` \| `G` \| `H` \| `I` | **5** |
| Interventi non svolti | 23-34 | `A` \| `B` \| `C:E` \| `F` \| `G` \| `H` \| `I` | **10** |

> ⚠️ **Le etichette sono riprodotte alla lettera dal foglio**, refusi inclusi: `CORRETIVA` (una sola
> "T"), `CHIUSURA TICKET MAXIMO+ EMAIL` (senza spazio prima del "+"), `COMP.REPORT INTERVENTI` (senza
> spazio dopo il punto). Il committente le aveva riscritte in forma "corretta" nella richiesta, ma la
> consegna era *replicare fedelmente* il template: il tecnico deve riconoscere il proprio documento.
> Se un giorno si volessero correggere, vanno corrette **anche nel foglio Excel**, non solo qui.

#### La decisione strutturale: PDF vettoriale al posto della cattura schermo

La vecchia versione fotografava la vista WPF con `RenderTargetBitmap`. Il nuovo esportatore
(`PassaggioConsegnePdfExporter`) **disegna** il rapportino con primitive PdfSharp a partire da un
`RapportinoSnapshot` immutabile. Non è un dettaglio realizzativo: è la scelta da cui discende quasi
tutto il resto.

| Problema della prima versione | Come sparisce |
|---|---|
| **E** (§6.4) — bitmap Pbgra32 da decine di MB nella Large Object Heap | Non esiste alcuna bitmap. Il PDF campione pesa ~110 KB |
| **§6.1-bis scoperta #3** — griglie non virtualizzabili, o il PDF avrebbe troncato le righe fuori viewport | Il PDF non guarda le griglie: la virtualizzazione torna una libera scelta di UI |
| **§6.1-undecies** — flag `IsExporting` che nascondeva le checkbox mutando la UI, con lo sfarfallio inseguito per quattro correzioni | Nessuna proprietà viene alterata per "preparare" la stampa. La regola Si/No è applicata nello snapshot, non a schermo |
| **F** (§6.4) — I/O VERIFICHE sul thread UI | Lo snapshot è catturato sul dispatcher, il disegno gira su thread pool senza corse con l'utente che digita |
| Modulo non testabile (richiedeva una finestra WPF) | L'esportatore non tocca WPF: **PDF generato e riaperto dentro xUnit** |

**Impaginazione.** Contenuto disegnato in coordinate "naturali" (larghezza 900) con le proporzioni
reali delle 9 colonne Excel, poi **una sola trasformazione di scala** lo fa entrare in una pagina,
centrata — cioè quello che fa Excel con `fitToPage`.

> ⚠️ **Trappola nei test, trovata scrivendoli.** Il primo test asseriva `PageCount == 1`: è **vacuo**.
> PDFsharp non impagina da solo, quindi un contenuto troppo alto non produce una seconda pagina, viene
> semplicemente **tagliato al bordo** — il test sarebbe rimasto verde proprio nel caso da intercettare.
> L'invariante vera è che *larghezza e altezza in scala stiano dentro la pagina*: da qui
> `CalcolaScala`/`CalcolaIngombro` esposti come `internal` e verificati da 0 a 60 righe per tabella.

**Font.** PDFsharp 6 non presume più una piattaforma: senza
`GlobalFontSettings.UseWindowsFontsUnderWindows = true` **nessun `XFont` si risolve** e il disegno del
testo fallisce a runtime. È il tipo di guasto che una build pulita non rivela; è coperto dal test che
genera davvero il PDF.

#### Le altre scelte, con il loro perché

- **Stato volatile** (richiesta esplicita): nessuna persistenza su disco. Il difetto della data
  "congelata" all'ultimo salvataggio (§6.1-undecies) non è più possibile *per costruzione*. Entro la
  sessione lo stato sopravvive alla navigazione, perché `MainWindow` tiene una sola istanza per vista.
- **Nessuna sottoscrizione a `OnVerificheDataUpdated`.** La prima versione si agganciava all'evento
  statico e riscriveva la tabella movimenti a ogni cambiamento sui file di flotta: **a metà turno
  questo cancella senza preavviso le date e gli orari di ingresso/uscita appena annotati a mano**.
  Ora la lettura avviene una volta all'apertura e poi solo dal pulsante "Aggiorna da VERIFICHE", con
  conferma esplicita che avverte della sovrascrittura. Chiude anche la criticità **D** di §6.4 (la
  lambda mai rilasciata), che semplicemente non esiste più.
- **Le 4 colonne di ingresso/uscita nascono a `"No"`** sulle sole righe popolate da VERIFICHE; le
  righe inutilizzate restano **completamente vuote**. Nella vecchia versione era un converter di sola
  visualizzazione; ora è un valore reale nel modello, quindi più semplice e testabile.
- **Interfacce iniettate** (`IRapportinoPdfExporter`, `IRapportinoMailService`, `INotificaUtente`) più
  una funzione per le VERIFICHE. È l'unico modulo dell'app costruito così, ed è la ragione per cui il
  flusso "Genera Mail" è verificabile per intero senza aprire Outlook, scrivere su disco o far
  comparire finestre modali. Risponde anche alla richiesta di un `IMailService` e realizza in piccolo
  l'intervento **1.7** di §6.2, finora sempre rimandato.
- **`EmailService.ComponiCorpoConFirma` estratta come funzione pura e condivisa.** La vecchia
  `PassaggioConsegneEmailService` aveva una propria copia della logica e **violava §5.5**: faceva
  `HTMLBody = corpo + firma`, cioè esattamente l'anti-pattern che manda la firma in testa all'email.
  Ora esiste una sola implementazione, usata da entrambi i moduli e coperta da 7 test: l'invariante
  §5.5 è verificabile per la prima volta senza aprire Outlook.
- **Chiavi destinatari**: la scheda "ETR 500" usa la chiave **`E404P`** in `destinatari.json` (§5.3).
  L'azione da cercare è **`"Passaggio di consegne"`** — vedi il riquadro qui sotto.

> ⚠️ **Errore commesso e corretto in questa sessione: il nome dell'azione dei destinatari.**
> La prima stesura del modulo aveva usato `"Passaggio Consegne"` e, non trovandola, aveva aggiunto a
> `DestinatariManager` un innesto (`EnsureAzioniRichieste`) che la creava. Ma la voce **esisteva già**
> in tutti i `destinatari.json` installati, sotto il nome storico `"Passaggio di consegne"`, con gli
> indirizzi reali personalizzati a mano dal tecnico. Risultato osservato dal committente su
> screenshot: **due righe quasi identiche** nella schermata DESTINATARI MAIL, e il modulo che usava
> quella sbagliata.
>
> **Correzione:** costante riportata a `"Passaggio di consegne"`, innesto rimosso del tutto,
> voci ripristinate con il nome storico in `GenerateDefaultConfig` per tutti e 5 i treni.
> Nessun dato perso: l'innesto agiva solo in memoria, quindi il file su disco non era mai stato
> riscritto — verificato dopo la correzione.
>
> **Lezione, valida oltre questo caso:** una configurazione utente già installata è un'interfaccia
> con un contratto, non uno spazio vuoto da popolare. Prima di introdurre una chiave nuova va
> verificato cosa contiene davvero il file sulla macchina di destinazione — qui sarebbe bastato
> guardarlo. Due nomi che differiscono per una sola parola sono inoltre indistinguibili a colpo
> d'occhio nella UI, ed è per questo che il difetto è sopravvissuto fino allo screenshot.
> Protetto ora da `AzioneDestinatariTests` (6 test), che verifica il nome esatto, la risoluzione dei
> destinatari per tutte e 3 le flotte, e **l'assenza di voci quasi-duplicate**.
- **Logo.** `Assets/logo-isman.png`, estratto dal template ed **incorporato nell'assembly** con
  `LogicalName` esplicito (un nome derivato dal percorso si romperebbe in silenzio rinominando la
  cartella). È decorativo: se manca, il rapportino resta valido.

#### Deviazione dichiarata dalla richiesta: destinatari da SQLite

> La richiesta diceva di leggere i destinatari "dal database SQLite (tabella rubrica/destinatari)
> filtrata per la specifica flotta". **Non è realizzabile come scritto, e la verifica lo dimostra:**
> `emails.db` contiene `indirizzi_email(id, nome, email, categoria)` dove `categoria` è
> l'**organizzazione** ("Hitachi Rail", "Iscot", "ADV Service", "Sovel Rail Traction"), non la flotta.
> Non esiste alcuna colonna su cui filtrare per ETR 500/700/1000.
>
> La mappa **flotta × azione → destinatari To/Cc** vive in `destinatari.json`, gestita da
> `DestinatariManager` ed editabile dal tecnico nella schermata DESTINATARI MAIL. È quella che il
> modulo usa. `emails.db` resta la rubrica generica usata da `RubricaDialog`. Se in futuro si volesse
> davvero spostare i destinatari su SQLite, servirebbe prima aggiungere una dimensione "flotta" allo
> schema — è una modifica di dati, non di codice.

#### Copertura di test: da 0 a 78

| Suite | Test | Cosa copre |
|---|---|---|
| `PassaggioConsegneModelsTests` | 26 | orari dei 4 turni, regola Si/No/vuoto, nozione di "riga compilata", struttura iniziale conforme al template |
| `PassaggioConsegneViewModelTests` | 24 | tre schede e loro chiavi, raggruppamento VERIFICHE per treno, flusso "Genera Mail" completo, fallimenti di PDF e Outlook, reset |
| `PassaggioConsegnePdfExporterTests` | 12 | PDF vero su disco, risoluzione dei font, ingombro entro la pagina, celle con testo sproporzionato |
| `ComponiCorpoConFirmaTests` | 7 | invariante §5.5 (corpo dentro la firma) |
| `AzioneDestinatariTests` | 6 | nome esatto dell'azione in `destinatari.json`, risoluzione per le 3 flotte, assenza di voci quasi-duplicate (vedi il riquadro sopra) |
| `PassaggioConsegneSnapshot` (dentro le suite sopra) | 9 | applicazione della regola Si/No al momento della cattura |

**Verifica della vista.** Il XAML è compilato in BAML dalla build, quindi errori di tipo o di sintassi
farebbero fallire la compilazione; ma **istanziazione, `StaticResource` e binding sono errori di
runtime**. Sono stati verificati con un harness WPF usa-e-getta creato **fuori dal repository** (nella
cartella temporanea di sessione, poi cancellato — la lezione di §6.1-terdecies sui progetti scratch che
sedimentano): carica la vista, la rende, cambia tutte e tre le schede e **intercetta il trace del
motore di binding**. Esito: nessuna eccezione, **nessun errore di binding**.

**Non verificato in questo ambiente, resta al committente** (punto 29 di §7.1): l'aspetto del PDF —
manca un rasterizzatore, `convert` su questa macchina è l'utility Windows, non ImageMagick — e la
bozza Outlook, perché Office non è installato qui.

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
| 3.4 | ✅ **Lettura Verifiche con `OpenXmlReader`** (SAX) invece di ClosedXML (Sprint 4 — 4,2× tempo, 6,1× memoria, misurati; §6.1-sexies) | **Alto** | Medio | Medio |
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
> approvate e applicate: vedi §4.16 e §4.17 (quest'ultima poi **rovesciata** nello Sprint 3 su
> richiesta esplicita, §6.1-quinquies). **C** e **H** sono state affrontate nello Sprint 2
> (§6.1-bis); **I** è stata valutata e scartata (non applicabile, non "da fare").
> **E, F e G sono state risolte nello Sprint 4** (§6.1-sexies): `RenderTargetBitmap` con tetto di
> pixel, caricamento verifiche fuori dal costruttore di `PassaggioConsegneViewModel`, risoluzione
> percorsi di rete su thread pool.
> **C, D, E ed F riguardavano tutte il modulo PASSAGGIO CONSEGNE**, rimosso nello Sprint 10 e
> **riscritto da zero nello Sprint 12** (§6.1-quaterdecies) con un'architettura che le rende impossibili
> per costruzione: PDF vettoriale generato da uno snapshot immutabile invece della cattura della vista,
> e nessuna sottoscrizione a eventi statici. **Sono chiuse tutte e quattro** — il testo sotto resta come
> registro di cosa furono e di perché la nuova architettura le elimina.
> **Non resta aperta alcuna criticità di questo elenco.**

**C. `ScrollViewer.CanContentScroll="False"`** su HomeView e sui 3 DataGrid di PassaggioConsegne —
**risolta per HomeView, esclusa per PassaggioConsegneView con motivo tecnico concreto (Sprint 2)**.
HomeView virtualizzata con lo stesso pattern di `VerificheView` (`ScrollUnit` a `Item`, non `Pixel`,
proprio per la ragione di maniglia-scrollbar-instabile ipotizzata qui). PassaggioConsegneView
**non** virtualizzata: la sua griglia veniva catturata per intero via `RenderTargetBitmap` per
l'export PDF del rapportino — virtualizzarla avrebbe rischiato di troncare silenziosamente righe fuori
viewport dal PDF esportato. Vedi §6.1-bis, scoperta #3, per il dettaglio. **Voce sul modulo rimosso
(§6.1-duodecies): la scelta tecnica va rivalutata da capo se la riscrittura userà ancora un export a
bitmap.**

**D. `PassaggioConsegneViewModel` — sottoscrizione statica mai rilasciata — ✅ CHIUSA (§6.1-quaterdecies): il modulo riscritto non si sottoscrive affatto**
```csharp
VerificheViewModel.OnVerificheDataUpdated += () => { … };   // lambda anonima, nessun -=
```
Innocuo **solo** finché `MainWindow` teneva una sola istanza della vista. Non era mai stata corretta
prima della rimozione del modulo: se la riscrittura si aggancia di nuovo a `OnVerificheDataUpdated`, va
prevista una `-=` corrispondente fin dall'inizio. Stesso schema tuttora presente in `HomeViewModel`
(`AppWatcher.OnLogDumpFolderChanged`, mai rimosso; `DispatcherTimer` mai fermato).
`ExcelViewModel` implementa `IDisposable` ma **nessuno chiama `Dispose()`**: è codice morto.

**E. `PassaggioConsegnePdfExporter` — `RenderTargetBitmap` — ✅ CHIUSA definitivamente (§6.1-quaterdecies): il PDF è vettoriale, nessuna bitmap viene più allocata.**
Era: `RenderTargetBitmap` Pbgra32 della dimensione piena del rapportino, 4 byte per pixel in un blocco
contiguo (quindi Large Object Heap) — ~48 MB per un rapportino 3000×4000, con rischio di fallimento su
macchine a poca RAM. Ora un tetto di 8 milioni di pixel riduce i DPI di render in modo proporzionale
**solo oltre la soglia**: per i rapportini ordinari l'immagine è identica a prima. Vedi §6.1-sexies.

**F. `VerificheViewModel.GetVerificheForFleetStatic` — fallback sincrono — risolta (Sprint 4), modulo poi rimosso (§6.1-duodecies).**
Era: aprendo "Passaggio di Consegne" prima di "Verifiche", il costruttore di
`PassaggioConsegneViewModel` eseguiva sul thread UI il caricamento completo di 3 flotte (enumerazione
ricorsiva OneDrive + parsing Excel), con freeze di secondi. Diventata sincrona su `Task.Run`, con solo
l'applicazione dei risultati sul dispatcher (`AutoCompilaTreniDaVerificheAsync`) — vedi §6.1-sexies.
`GetVerificheForFleetStatic` (in `VerificheViewModel`, non rimosso) resta comunque disponibile per
qualunque futuro chiamante che debba leggere le verifiche di una flotta senza I/O sul thread UI.

**G. `HomeViewModel.OnLogDumpRete` — risoluzione percorsi di rete sul thread UI — risolta (Sprint 4).**
`GetLogDumpReteBasePath()` e `ResolveTrainTypePath()` sono ora eseguite su thread pool insieme
all'enumerazione degli ZIP, con overlay di attesa. Vedi §6.1-sexies.

**H. `MainWindow.xaml` — `DropShadowEffect` sulla navbar — risolta (Sprint 2).**
Era un effetto a pixel shader su un `Border` a tutta larghezza (`Opacity="0.05"`, appena percettibile).
Rimosso; il bordo inferiore del `Border` (`BorderThickness="0,0,0,1"`) resta a marcare la navbar.
Non toccati gli altri usi di `DropShadowEffect` nell'app (era presente anche in `PassaggioConsegneView`
— sulla stessa `RapportinoSheetBorder` catturata per il PDF, quindi parte dello stile del documento
esportato, non solo chrome UI, ma la vista è stata rimossa nello Sprint 10, §6.1-duodecies — e nelle
viste email/dialog): non auditati in questa sessione, lasciati come sono.

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
- [x] **`bin/` e `obj/` non più tracciati (Sprint 11, §6.1-terdecies).** Erano 669 file su 805 (83% del
      repository). Tolti dall'indice con `git rm -r --cached`, **senza cancellarli da disco**: la build
      compilata e i file di configurazione già personalizzati nella cartella di output
      (`destinatari.json`, `shortcuts.json`) sono rimasti intatti.
- [x] **`TestClosedXML/`, `scratch/`, `ep_test.cs`, `test.cs` eliminati (Sprint 11, §6.1-terdecies).**
      `TestClosedXML` rimossa prima dalla `.sln` (`dotnet sln remove`), poi da disco. ~168 MB liberati.
      La copertura di `MatchesTrain` che solo quel progetto forniva è stata **portata in xUnit prima**
      della cancellazione — vedi §6.1-terdecies.
- [x] **`build_last.txt` / `build_out.txt` rimossi (Sprint 11, §6.1-terdecies)**, insieme ai 3 `.DS_Store`.
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
      44 Tier 1 (`ExcelFolderParserTests`, sui **nomi di cartella e opzioni di report reali**),
      9 Tier 1 (`VerificheViewModelTests`, deduplicazione radici), 21 Tier 2
      (`VerificheExcelReaderTests`, **equivalenza SAX ↔ ClosedXML** su file .xlsx reali), 30 Tier 2
      (`ReportInterventiWriterTests`, **integrità strutturale** del pacchetto OpenXML su un .xlsm con
      VBA, convalide, filtri e tabelle), 4 Tier 2 (`HitachiPathsManagerTests`, regressione sul
      percorso ETR1000 I-F), 12 Tier 2 (`FileOperationRetryTests`, **lock reali del file system** e
      riprova su violazione di condivisione), 5 Tier 2 (`DatabaseManagerLockTests`, rilascio
      deterministico dell'handle SQLite), 11 Tier 1 (`HomeViewModelTests`, prefisso "SR" in Aggiorna
      Ticket), 21 Tier 1 (`ExcelViewModelMatchesTrainTests`, §6.1-terdecies: match dei **nomi di file
      report** per flotta, con la separazione ETR1000 ↔ ETR1000 I-F di §5.3-bis in entrambe le
      direzioni), **84 per il modulo PASSAGGIO CONSEGNE riscritto** (§6.1-quaterdecies: 26
      `PassaggioConsegneModelsTests`, 24 `PassaggioConsegneViewModelTests` — incluso il flusso
      "Genera Mail" completo con Outlook e disco finti —, 12 `PassaggioConsegnePdfExporterTests` che
      generano e riaprono un PDF vero, 7 `ComponiCorpoConFirmaTests` sull'invariante §5.5, più le
      asserzioni sullo snapshot, 6 `AzioneDestinatariTests`) — **286 in tutto** (212 → 181 dopo la rimozione del vecchio modulo,
      → 202 con la copertura di `MatchesTrain`, → 286 con il modulo riscritto).
      Restano
      da coprire: `ExtractLocosFromFolder`, `BuildSubject`, `AreTrainTypesCompatible` (Tier 1, non
      dipendono da `LogDumpFolderName`, possono procedere in parallelo a §6.3) — **`MatchesTrain` è
      stata coperta nello Sprint 11** (§6.1-terdecies); Tier 3
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

### 7.2 Pulizia del tracking Git — ✅ **ESEGUITA nello Sprint 11 (§6.1-terdecies)**

> Questa sezione descriveva un'operazione **da fare**. È stata eseguita il 23/08/2026: il repository è
> passato da **805 a 97 file tracciati**. Resta qui per documentare *cosa* è stato fatto e perché, e
> come riconoscere il problema se dovesse ripresentarsi.

Il `.gitignore` era già presente e corretto, ma Git **continua a tracciare i file già indicizzati**: le
regole di ignore valgono solo per i file non ancora tracciati. `git rm --cached` rimuove **solo
dall'indice**, i file restano sul disco.

Comandi effettivamente usati (mirati per percorso, invece dell'azzeramento totale dell'indice, così da
non toccare per sbaglio i sorgenti):

```bash
git rm -r --cached --quiet PersonalAutomationTool/bin PersonalAutomationTool/obj
```

Verificato dopo l'operazione che: **0** file restano tracciati sotto `bin/`/`obj/`; **0** file tracciati
risultano assenti dal disco; i `modules/database/*.db` sono ancora tracciati (sono dati sorgente, copiati
in output da `CopyToOutputDirectory` — se comparissero come eliminati, il `.gitignore` conterrebbe una
regola `*.db` di troppo); l'eseguibile compilato e `destinatari.json`/`shortcuts.json` nella cartella di
output sono rimasti intatti.

I file restano comunque nella **storia** del repository (il clone non si alleggerisce
retroattivamente). Per riscrivere anche la storia servirebbe `git filter-repo`, operazione distruttiva
che invalida tutti i cloni esistenti: **non eseguita**, da valutare solo se la dimensione del repository
diventasse un problema concreto.

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
6. PASSAGGIO CONSEGNE → premere "Genera Mail" e verificare che il PDF venga prodotto e che la bozza
   Outlook si apra con l'allegato e i destinatari giusti (modulo riscritto, §6.1-quaterdecies:
   dettagli al punto 29).
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
20. **EXCEL / ROTABILE** (§5.3-bis, §6.1-quater) ⭐ *modifica a comportamento visibile, con effetto
    reale sul report* → il menu ROTABILE ha due voci, `ETR1000` e `ETR1001FH`. Selezionare
    `ETR1000 / 1000FH` e aprire **una cartella FH**: il campo deve ora proporre **`ETR1001FH`** —
    prima proponeva sempre `ETR1000`, cioè il rotabile sbagliato scritto nel report ufficiale. Poi
    aprire una cartella **ETR1000 pura** sotto la stessa voce e verificare che proponga `ETR1000` e
    non la variante FH. È la verifica che dimostra il valore concreto dell'intero intervento.
21. **VERIFICHE** (§6.1-quinquies) ⭐ *corregge un bug di dati segnalato dal committente* → aprire il
    modulo e controllare che ogni riga di `VERIFICHE ETR500` compaia **una sola volta** (prima ogni
    riga era duplicata: stesso ticket/loco/avaria ripetuti due volte in sequenza). Controllare che le
    tre tabelle di flotta e il riepilogo in alto mostrino **tutte** le righe senza barra di
    scorrimento interna — la pagina nel suo complesso scorre se il contenuto supera lo schermo, le
    singole tabelle no. Ripetere il controllo "nessuna duplicazione" anche su ETR700 ed ETR1000 (il
    fix è generale, non specifico a ETR500: se lì si presentasse la stessa duplicazione sarebbe un
    caso nuovo, non coperto da questo intervento).
22. **PRESTAZIONI** (§6.1-sexies) ⭐ *l'obiettivo dello Sprint 4* → sulla macchina d'officina reale,
    verificare: (a) **VERIFICHE** si aggiorna sensibilmente più in fretta e senza pause del garbage
    collector (la lettura Excel usa ora il percorso SAX: 4,2× più veloce e 6,1× meno memoria in
    laboratorio); (b) **PASSAGGIO DI CONSEGNE** si apre **subito** anche quando è il primo modulo
    aperto dopo l'avvio, senza il freeze di secondi di prima; (c) **HOME → "Log Dump in rete"** non
    blocca più la finestra al clic, mostrando "Ricerca cartelle di rete..."; (d) **EXCEL → "Scrivi
    report"** mostra l'overlay immediatamente, senza il blocco iniziale dovuto all'avvio di
    EXCEL.EXE. Se una verifica dovesse ancora risultare lenta, annotare **quale** e con quali volumi
    di dati: è l'informazione che serve per il prossimo giro di misure.
23. **EXCEL / integrità del report** (§6.1-septies) ⭐ *l'unica verifica che richiede il file
    aziendale reale* → dopo uno "Scrivi report" su una copia del Report Interventi vero, aprirlo in
    Excel e controllare che **la riga appena inserita** abbia: il menu a tendina delle convalide
    (es. Tipologia), la formattazione condizionale, e che risulti inclusa nel filtro automatico e
    nell'eventuale tabella. Se una di queste manca **solo sulla riga nuova**, non è un file
    corrotto: è un intervallo (`C2:C100`, il range della tabella, ecc.) che non arriva fino a quella
    riga e va esteso una volta sola nel template aziendale. Verificare inoltre che le macro
    funzionino ancora e che il file resti `.xlsm`.
24. **EXCEL / percorso ETR1000 I-F** (§6.1-octies) ⭐ *richiede un'azione sulla macchina reale, non
    solo la nuova build* → sulla macchina del tecnico, individuare `hitachi_paths.json` accanto
    all'eseguibile: se esiste già, cancellarlo (si rigenera da solo al prossimo avvio) oppure aprirlo
    e cambiare a mano `"ETR1000 ITA-FRA"` in `"ETR1000 ITA-FR"` nella voce `"ETR1000 I-F"` — la nuova
    build da sola **non corregge** un file già generato in precedenza. Poi selezionare `ETR1000 I-F`
    ed eseguire "Sposta Report" e "Riporta Report": entrambi devono risolvere la cartella corretta
    senza più l'errore "Cartella Hitachi non trovata".
25. **EXCEL / "file in uso" su Riporta report** (§6.1-nonies) → ripetere il flusso completo più volte
    di seguito (Sposta → compila → Scrivi → Riporta), possibilmente mentre OneDrive sta
    sincronizzando: l'errore intermittente non deve più comparire. Se ricomparisse, il messaggio ora
    indica **quale file** è bloccato e le cause probabili: annotarlo così com'è, perché dice se il
    blocco viene da Excel, dalla sincronizzazione o da altro — informazione che il messaggio
    precedente non dava. Verificare inoltre che, se il file è davvero aperto in Excel, dopo circa 3
    secondi di tentativi compaia il messaggio diagnostico e non un errore generico.
26. **HOME / prefisso "SR"** (§6.1-decies) → in "Cambio ticket", digitare un ticket **senza** "SR"
    (es. `1234567`) e premere "Aggiorna ticket": i nomi di sottocartella risultanti devono iniziare
    con `SR1234567`, non con `1234567` nudo. Ripetere digitando il ticket **con** "SR" già presente
    (es. `SR1234567`): il risultato deve essere identico, senza `SRSR1234567`.
27. **PDF / rinomina immediata** (§6.1-decies) ⭐ *modifica a comportamento visibile, voluta* →
    premendo "Rinomina" su una cartella madre, l'operazione deve partire **subito**, senza alcun
    dialog di conferma intermedio. Verificare che l'overlay di avanzamento compaia comunque durante
    lo spostamento dei file, e che "Annulla ultima rinomina" continui a funzionare subito dopo — è
    l'unica rete di sicurezza rimasta contro un parsing sbagliato, ora che la conferma preventiva non
    c'è più.
28. ~~**PASSAGGIO DI CONSEGNE** (§6.1-undecies)~~ — **obsoleto: il modulo è stato rimosso nello
    Sprint 10 (§6.1-duodecies)** in vista di una riscrittura da zero: non esiste più alcuna UI su cui
    eseguire queste tre verifiche. Lasciato qui solo perché, se la riscrittura riproporrà gli stessi tre
    comportamenti (data odierna forzata, checkbox nascoste nell'export PDF, fallback "No" limitato alle
    righe compilate — vedi "Cosa resta utile per la riscrittura" in §6.1-duodecies), la checklist
    originale è un buon punto di partenza:
    (a) impostare `Data` a una data passata, chiudere e riaprire l'app: alla riapertura
    "Data" deve mostrare il giorno corrente, non quello lasciato prima della chiusura; (b) in
    "Dettaglio interventi", spuntare qualche checkbox e generare il PDF ("Passaggio di consegne"):
    aprire il PDF e verificare che compaia **solo** il testo "Sì"/"No", senza alcun quadratino di
    checkbox visibile, e che a schermo (prima e dopo l'export) la UI resti quella di sempre, senza
    sfarfallii percepibili durante la generazione; (c) nella tabella Movimenti, verificare che le
    righe con Treno/Loco compilati (es. dall'autocompilazione da VERIFICHE) mostrino "No" nelle
    colonne Data/Ora Ingresso e Data/Ora Uscita finché non vengono compilate a mano, mentre le righe
    ancora del tutto vuote restino vuote anche in quelle colonne.
29. **PASSAGGIO CONSEGNE — modulo riscritto** (§6.1-quaterdecies) ⭐ *le due verifiche che questo
    ambiente non ha potuto fare*. Coperto invece da test automatici, e quindi **da non ri-verificare a
    mano**: struttura delle tre schede, orari dei turni, regola Si/No/vuoto, raggruppamento da
    VERIFICHE, ingombro del PDF nella pagina, oggetto ed destinatari passati a Outlook.
    **(a) Aspetto del PDF** → premere "Genera Mail" su una scheda con qualche riga compilata e aprire
    il PDF: deve stare su **una sola pagina A4 orizzontale**, riprodurre le tre tabelle del template
    Excel con le stesse etichette, mostrare il logo in alto a sinistra, e nella tabella "Dettaglio
    interventi" **non deve comparire alcun quadratino di checkbox** — solo "Si"/"No" sulle righe
    compilate e **celle del tutto vuote** su quelle non compilate. Stessa regola nella tabella
    "Interventi non svolti".
    **(b) Bozza Outlook** → verificare che la bozza si apra **senza inviarsi**, con oggetto
    "Passaggio Consegne IMC AV Milano {flotta}", il PDF allegato, e **la firma in fondo al messaggio,
    non in testa**. I destinatari devono essere quelli della voce **"Passaggio di consegne"** già
    presente in DESTINATARI MAIL: verificare che siano esattamente quelli, e che in quella schermata
    **non sia comparsa una seconda voce** con nome simile.
    **(c) Nota su ETR 500** → i destinatari di quella scheda si leggono sotto la voce **`E404P`**, non
    "ETR500" (§5.3).
