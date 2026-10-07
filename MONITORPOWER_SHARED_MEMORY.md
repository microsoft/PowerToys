# Display Profiles — memoria condivisa tra agenti

## Funzione del documento

Questo documento è il punto di coordinamento condiviso per gli agenti che lavorano su Display Profiles (`MonitorPower`). Serve a registrare decisioni, responsabilità, stato delle attività, file in lavorazione e verifiche eseguite, così da evitare modifiche concorrenti, regressioni o rimozioni accidentali. Prima di iniziare un intervento, ogni agente deve leggere questo file; durante il lavoro deve dichiarare la propria area e, al termine, aggiornare stato, verifiche e problemi aperti.

## Regole di coordinamento

1. Non modificare file assegnati all'altro agente senza coordinarsi.
2. Non rimuovere modifiche non proprie solo perché non ancora completate.
3. Prima di modificare un'area condivisa, controllare `git status` e questo documento.
4. Conservare `Display Profiles` come nome visibile all'utente e `MonitorPower` come identificatore tecnico.
5. Settings è l'esperienza principale; Command Palette è un accesso complementare standalone.
6. Entrambe le esperienze devono riutilizzare `MonitorPower.Core`; non duplicare topologia, persistenza o applicazione dei profili.
7. Non eseguire commit o push finché le modifiche di entrambi gli agenti non sono state revisionate e validate insieme.
8. Non sovrascrivere o ripristinare l'intera working tree: sono presenti modifiche concorrenti intenzionali.

## Decisioni architetturali

- La funzionalità principale vive nella pagina Settings dedicata a Display Profiles.
- `MonitorPower.Core` contiene la logica condivisa per display, topologie, profili e applicazione.
- `PowerToys.MonitorPower.Runtime` gestisce il selettore rapido, shortcut e controller.
- `MonitorPowerExtension` viene mantenuta come estensione standalone di Command Palette.
- L'estensione permette di usare Display Profiles anche quando è in esecuzione la Command Palette ufficiale e la versione pubblicata di PowerToys non include ancora il nuovo modulo.
- L'estensione deve leggere gli stessi profili da `%LOCALAPPDATA%\MonitorPower\profiles`.
- L'estensione non deve registrare una seconda gestione globale di shortcut o chord Xbox.

## Ripartizione del lavoro

### Agente Settings / modulo principale

Area assegnata:

- `src/modules/monitorpower/MonitorPower.Core/Commands/`
- `src/modules/monitorpower/MonitorPower.Runtime/`
- `src/settings-ui/`
- asset Display Profiles
- localizzazione Settings, runtime e core
- integrazione runner e comportamento del modulo principale

Stato riportato:

- Migrato il flusso principale da Command Palette a Settings.
- Implementati topologia, profili, runtime selector, shortcut tastiera e chord Xbox.
- Migliorata la UI con controlli Settings standard.
- Localizzati Settings, runtime e core.
- Corretti UI thread ed errori localizzati.
- Creati asset dedicati a Display Profiles.
- Build completa Debug x64 riuscita con 0 errori prima delle modifiche CmdPal concorrenti.

### Agente Command Palette

Area assegnata:

- `src/modules/cmdpal/ext/MonitorPowerExtension/`
- riferimenti dell'estensione in `PowerToys.slnx`
- riferimenti dell'estensione in `src/modules/cmdpal/CommandPalette.slnf`
- script di build/registrazione strettamente relativi a CmdPal
- minima esposizione necessaria di `MonitorPower.Core` all'estensione

Stato corrente:

- Recuperata l'interfaccia CmdPal storica senza recuperare la vecchia copia di `DisplayHelpers`.
- Collegata l'estensione all'attuale `MonitorPower.Core`.
- Unificato lo storage su `%LOCALAPPDATA%\MonitorPower`.
- Aggiunti profili predefiniti `All displays` e `Primary display only`.
- Rimossa dall'estensione la registrazione del chord Xbox per evitare duplicazioni col runtime.
- Aggiornata l'applicazione asincrona dei profili e la classificazione errori tramite Core.
- Convertito il progetto in estensione standalone MSIX con COM server e app extension CmdPal.
- Build Debug x64 del progetto riuscita; rimane un warning relativo a `mspdbcmf.exe` per il pacchetto simboli.
- La registrazione MSIX deve essere completata con firma/certificato di sviluppo coerente.

## File condivisi sensibili

I seguenti file richiedono coordinamento esplicito:

- `PowerToys.slnx`
- `src/modules/cmdpal/CommandPalette.slnf`
- `src/modules/monitorpower/MonitorPower.Core/MonitorPower.Core.csproj`
- `register-dev-cmdpal.ps1`
- `.gitignore`

Modifica CmdPal attualmente necessaria nel Core:

- `MonitorPower.Core.csproj` include `InternalsVisibleTo` per `MonitorPowerExtension`.

Questa soluzione è intenzionalmente minima. Non rendere pubblico l'intero `DisplayHelpers` e non duplicarne il codice.

## Avvio locale

- Il modulo Command Palette ufficiale di PowerToys è stato disabilitato nelle impostazioni locali per evitare conflitti.
- Esiste un collegamento Startup per:
  `x64\Debug\WinUI3Apps\CmdPal\Microsoft.CmdPal.UI.exe`.
- La build Debug di Command Palette è registrata come `Microsoft.CommandPalette.Dev`.
- L'estensione standalone deve poter essere registrata anche per la Command Palette ufficiale.

## Verifiche da completare

1. Completare firma e registrazione locale del pacchetto `MonitorPowerExtension`.
2. Verificare che l'estensione venga rilevata dalla Command Palette ufficiale e da quella Debug.
3. Verificare elenco e applicazione dei profili salvati condivisi con Settings.
4. Verificare i profili predefiniti e gli errori mostrati da CmdPal.
5. Aggiungere o aggiornare i test CmdPal pertinenti.
6. Revisionare visivamente gli asset Display Profiles.
7. Completare tutti i riferimenti utente da Monitor Power a Display Profiles senza cambiare identificatori tecnici.
8. Eseguire build completa Debug x64, test, `git diff --check`, spell-check e controlli CI pertinenti.
9. Revisionare insieme il diff prima di commit e push.
10. Collegare il lavoro all'issue upstream `#48286` e preparare la PR solo dopo la validazione congiunta.

## Protocollo di aggiornamento

Ogni agente aggiunge una voce in fondo dopo una sessione significativa usando questo formato:

```text
### AAAA-MM-GG HH:MM — Agente / area
- File modificati:
- Attività completate:
- Verifiche eseguite:
- Problemi o decisioni richieste:
- Prossimo passo:
```

## Registro

### 2026-10-07 — Coordinamento iniziale

- Stabilito che Settings resta l'esperienza principale.
- Stabilito che l'estensione standalone CmdPal viene mantenuta come accesso complementare.
- Separate le aree di responsabilità per evitare conflitti.
- Vietata la rimozione delle modifiche CmdPal in corso senza coordinamento.

### 2026-10-07 — Agente Command Palette

- File modificati: `src/modules/cmdpal/ext/MonitorPowerExtension/`, `src/modules/cmdpal/CommandPalette.slnf`, `PowerToys.slnx`, `register-dev-cmdpal.ps1`, `src/modules/monitorpower/MonitorPower.Core/MonitorPower.Core.csproj`.
- Attività completate: ripristinata l'estensione come processo standalone; collegata a `MonitorPower.Core`; aggiunti profili predefiniti; unificato lo storage; rimossa la registrazione Xbox duplicata; aggiunti manifest COM/AppExtension e asset Display Profiles; corretti soluzione e script di registrazione.
- Verifiche eseguite: build Debug x64 riuscita; registrazione loose tramite `Add-AppxPackage -Register` riuscita; Command Palette Debug avviata e processo `MonitorPowerExtension.exe -RegisterProcessAsComServer -Embedding` attivato correttamente.
- Problemi o decisioni richieste: il vecchio smoke test del provider non è adatto al processo standalone perché VSTest non inizializza il contesto Windows App SDK; non è stato mantenuto. La build MSIX segnala soltanto l'assenza di `mspdbcmf.exe` per il pacchetto simboli.
- Prossimo passo: controlli finali di build/formattazione, prova manuale delle azioni profilo e revisione congiunta del diff.

### 2026-10-07 — Correzione nome e condivisione profili

- Nome visibile dell'estensione, provider, pagina e manifest aggiornato a `Display Profiles`; `MonitorPower` rimane soltanto identificatore tecnico per compatibilità.
- CmdPal e Settings usano entrambi le API di `MonitorPower.Core` e la stessa directory `%LOCALAPPDATA%\MonitorPower\profiles`.
- Verificata la directory condivisa con due profili esistenti: `Centrale + Laterali.json` e `Solo TV.json`.
- Build Debug x64 riuscita; estensione registrata nuovamente e caricata dalla Command Palette ufficiale.
- `Create profile` è stato rimosso dalla lista principale ed esposto nell'area comandi del footer/command bar tramite `MoreCommands` su ogni profilo visualizzato.
- `Save profile` è stato rimosso dalla lista della pagina di creazione/modifica ed esposto nello stesso footer/command bar tramite `MoreCommands` sui monitor visualizzati.
- Comandi footer completati con label e shortcut: `Ctrl+N` crea profilo, `Ctrl+E` modifica profilo, `Delete` elimina profilo, `Space` include/esclude il monitor selezionato, `Ctrl+S` salva il profilo.
- Il toggle con `Space` usa un refresh incrementale (`TotalItems = -2`) per conservare la selezione sul monitor corrente.
- Flusso di creazione/modifica completato con riepilogo stato e layout, conferma finale, feedback e ritorno alla lista.
- Condivisione e regole garantite dal medesimo `MonitorPower.Core`: CmdPal e Settings leggono/scrivono `%LOCALAPPDATA%\MonitorPower\profiles` e chiamano `GetSavedProfileConflict`, che impedisce nomi duplicati e combinazioni di monitor duplicate.
- Il toggle `Space` ora modifica l'elemento esistente senza rigenerare la lista, preservando stabilmente il focus; il footer della creazione espone Toggle, Save/Review e Cancel.

### 2026-10-07 — Agente Settings / branding e localizzazione

- File modificati: asset `DisplayProfiles.png`, `DisplayProfilesPage.xaml`, `ShellPage.xaml`, `ModuleHelper.cs`, risorse Settings/runtime/core, ViewModel e code-behind Display Profiles.
- Attività completate: adottato `Display Profiles` come nome visibile mantenendo `MonitorPower` come identificatore tecnico; sostituiti gli asset Power Display condivisi con icona 36×36 e immagine modulo 400×266 dedicate; completata la localizzazione dei testi visibili e rimossi commenti non inglesi.
- Verifiche eseguite: asset verificati per dimensioni/formato; risorse XML valide senza chiavi duplicate; `git diff --check` riuscito; build Debug x64 di `PowerToys.Settings.csproj` riuscita con 0 errori e 0 warning.
- Problemi o decisioni richieste: gli asset generati sono funzionali e distinti da Power Display, ma richiedono revisione visiva congiunta prima della PR upstream.
- Prossimo passo: attendere il completamento CmdPal, quindi eseguire build completa, test e revisione congiunta del diff senza rimuovere modifiche concorrenti.
