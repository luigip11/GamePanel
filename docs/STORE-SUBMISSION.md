# GamePanel — bozza candidatura Microsoft Store

Aggiornata il 17 novembre 2026. Questi testi accompagnano la prova MSIX della PR #8; non attestano una certificazione e non autorizzano l'invio o la pubblicazione. L'identità del pacchetto deve corrispondere ai valori riservati in Partner Center.

## Dati necessari dal portale

In **Product management → Product identity** copiare esattamente:

- `Package/Identity/Name`
- `Package/Identity/Publisher`
- `Package/Properties/PublisherDisplayName`

Non occorrono password, documenti personali, chiavi private o token. L'eventuale verifica dell'identità dell'account è separata dalla candidatura dell'app: i documenti richiesti da Microsoft vanno inviati esclusivamente nel portale ufficiale.

### Identità riservata ricevuta dal titolare

I valori pubblici sono fissati in `packaging/msix/StoreIdentity.json`: nome tecnico `VirtualLP.GamePanel`, publisher `CN=33AE9F23-7E92-4890-8DF6-5BEE6CEA6EDB`, nome editore `VirtualLP`, PFN `VirtualLP.GamePanel_z68edq0c3m1tg`, Store ID `9NP833B30WVB`. La riga del nome tradotta dal browser non va copiata nel manifest: il nome tecnico è ricavabile dal PFN, prima dell'ultimo underscore.

Su Windows, dopo la pubblicazione standard, eseguire `./scripts/build-store-msix.ps1 -PackageDirectory artifacts/GamePanel-win-x64`. La Action MSIX prepara lo stesso artifact separato, non firmato, con un'unica applicazione e senza CompatibilityProbe. Il pacchetto alpha.17 è un aggiornamento del codice: la certificazione del precedente `1.0.16.0` non copre `1.0.17.0`. Prima dell'invio controllare il riepilogo della nuova candidatura. La prenotazione del nome e la compilazione non dimostrano che la pagina pubblica Store sia già disponibile.

## Proprietà e disponibilità

- Nome riservato: **GamePanel**.
- Categoria proposta: **Utilities & tools**, se disponibile nel menu. È un'utilità desktop per giochi posseduti dall'utente, non un gioco distribuito dal progetto.
- Sito: https://github.com/luigip11/GamePanel
- Supporto: https://github.com/luigip11/GamePanel/issues
- Prezzo proposto: gratuito, da confermare dal titolare nel portale.
- Famiglia dispositivi: PC Windows x64. Il manifest della prova richiede Windows 11 (build 22000 o successiva); allineare i requisiti dichiarati al manifest definitivo.
- Lingua della scheda iniziale: italiano. La UI attuale è italiana; il manifest dichiara solo `it-IT`. Non presentarla come tradotta in inglese.
- Privacy: revisionare `PRIVACY-DRAFT.md` e pubblicare una versione definitiva a un URL pubblico stabile prima di inserirlo nel portale. Nessun URL non ancora pubblicato va dichiarato come disponibile.
- Age ratings: compilare il questionario IARC in base alle funzioni e ai contenuti effettivamente offerti. Non copiare la classificazione di Need for Speed e non inventare un'età assegnata.
- Submission options: selezionare **Don't publish this submission until I select Publish now**, così la certificazione non causa automaticamente la pubblicazione.

## Completare le tre sezioni rimaste nel portale

### Pacchetti

Il pacchetto Store usa `1.0.17.0`: major non zero e quarta componente riservata allo Store impostata a zero. L'app rimane alpha.17; il numero del pacchetto non annuncia una release stabile. Le vecchie identità di collaudo possono conservare `0.1.16.0` e non vanno caricate nel prodotto riservato.

Scaricare l'artifact Partner Center dell'ultima Action riuscita, estrarre lo ZIP e caricare **solo** `VirtualLP.GamePanel-1.0.17.0-x64-unsigned.msix` nella sezione Pacchetti della candidatura **Draft**. Non caricare lo ZIP dell'artifact, il pacchetto CompatibilityTest o lo ZIP portatile; non aprire l'MSIX con doppio clic per installarlo sul PC.

Attendere la validazione del portale e controllare i dettagli: identità `VirtualLP.GamePanel`, editore `VirtualLP`, architettura x64 e lingua italiano (`it-IT`). La famiglia dispositivi è Windows Desktop; lasciare abilitata l'offerta per PC e non dichiarare supporto a famiglie non provate. Completare e salvare anche le eventuali scelte obbligatorie della sezione: il solo stato "Validated" di un file non garantisce che tutta la sezione sia completa.

L'upload nella bozza serve a verificare i metadati. È distinto da **Submit for certification**, che richiede una nuova certificazione per il codice aggiornato. In caso di rifiuto annotare il testo esatto del portale; la validazione locale MakeAppx non sostituisce quella di Partner Center.

### Elenchi dello Store / Store listings

Aprire o aggiungere la scheda **Italiano (Italia)**. "Elenchi dello Store" indica le schede localizzate del prodotto, non un elenco di negozi. Incollare la descrizione italiana riportata sotto, aggiungere le funzionalità e almeno una schermata reale dell'app conforme ai requisiti mostrati dal portale; controllare eventuali campi grafici obbligatori e salvare la scheda. Per ora non dichiarare una UI inglese.

### Opzioni di invio

Impostare **Don't publish this submission until I select Publish now**. Nella richiesta per `runFullTrust` usare la motivazione riportata sotto; nelle note per la certificazione usare la bozza solo dopo aver completato i passaggi di prova mancanti. Salvare la sezione. Il questionario capability e la certificazione restano soggetti alla valutazione Microsoft; salvare la bozza non è un'approvazione.

## Descrizione italiana da incollare

GamePanel organizza i tuoi giochi PC e ti aiuta a gestire le configurazioni grafiche compatibili da un'unica app Windows.

Importa il file EXE di un gioco già installato, consulta le informazioni rilevate, organizza la libreria e avvia la copia selezionata. Puoi scegliere copertine locali oppure cercarle online, consultare il tuo hardware e raggiungere le impostazioni di schermo e GPU di Windows.

Per i giochi e i profili supportati, GamePanel propone configurazioni di risoluzione, backup e ripristino dei file gestiti e strumenti per copie Originale/Remix. Le opzioni DLSS, ReShade e HDR dipendono dal renderer, dai componenti disponibili e dall'hardware. Alcune integrazioni richiedono il download o l'importazione separata di componenti di terze parti, presentati nel relativo flusso.

Giochi e pacchetti Remix non sono inclusi. GamePanel non aggiunge automaticamente DLSS o HDR a qualsiasi gioco e non garantisce incrementi di FPS. Non è un prodotto ufficiale di NVIDIA, Microsoft o degli editori dei giochi.

## Funzionalità della scheda

- Libreria di giochi importati dal loro EXE, con copertine e avvio della copia selezionata.
- Analisi locale delle informazioni del gioco e dell'hardware disponibile.
- Gestione della risoluzione per configurazioni e titoli supportati.
- Backup e ripristino dei file di configurazione gestiti.
- Controlli grafici e DLSS disponibili in base al profilo e al renderer rilevati.
- Gestione di varianti Originale/Remix per integrazioni compatibili.

Parole chiave proposte: `libreria giochi`, `configurazioni`, `backup`, `risoluzione`, `grafica`.

## Immagini

Preparare almeno una schermata dell'interfaccia realmente eseguita; Microsoft ne raccomanda quattro o più. Le immagini in `docs/images/alpha16-*.png` sono riferimenti dell'interfaccia, da confrontare con la build definitiva. Se usano dati dimostrativi dichiararlo; non presentare benchmark o risultati hardware inventati.

Viste proposte: Libreria, Gioco/hardware, Schermo e risoluzione, Backup. Per DLSS chiarire che la disponibilità dipende dal gioco. Prima dell'upload controllare leggibilità, dimensioni richieste nel portale, assenza di percorsi personali e diritti sulle copertine o immagini di terzi. Non usare mockup come prova di funzioni già disponibili.

## Restricted capability: runFullTrust (testo inglese proposto)

GamePanel is an existing WPF desktop utility packaged as a classic desktop application. It needs runFullTrust to inspect executable files explicitly selected by the user, read and manage supported configuration files in user-selected game folders, create and restore configuration backups, detect local hardware and displays, and launch the selected installed game. Operations run at the current user's permissions; the package does not request elevation, install drivers or Windows services, or change Windows security policies. The MSIX build blocks the portable self-updater and uses the package distribution channel for app updates.

## Notes for certification — aggiornamento 1.0.17.0

Testo inglese da copiare nelle Informazioni aggiuntive sui test, dopo aver verificato che la candidatura contenga questo pacchetto:

GamePanel 1.0.17.0 (alpha.17) replaces 1.0.16.0. This update adds local data export/import, explicit migration from the portable data directory, and an external recovery archive to preserve library metadata and configuration backups across MSIX reset/uninstall.

The UI is Italian and no login is required. Games are not included. The new data controls are available without a selected game: expand Avanzate, open Backup e ripristino, choose Scegli copia esterna and select a ZIP in Documents or another user folder outside AppData and Temp. Confirm that the archive completes. Esporta dati e backup creates an independent ZIP; Importa archivio dati verifies and imports it without overwriting different existing files. An empty library can also be exported/imported. For a nonempty library, import an installed game already available in the test environment; no commercial game download or execution is needed to inspect the EXE and library metadata.

Store operations that create backups or apply/recover managed profiles require the external location to be selected first. Backup data is preserved before continuing when a baseline is created; the archive is refreshed after managed profile changes and on normal application close. If the external destination is unavailable, the app retains internal data and reports the failure. A forced process termination cannot refresh the archive. Windows reset/uninstall can still remove internal LocalState; users must retain the completed external ZIP.

This update does not introduce new online services or additional restricted capabilities. runFullTrust remains necessary for user-selected desktop file inspection, supported configuration management, backups and game launch. No drivers/services, elevation request or Windows security policy changes are introduced. The package disables the portable updater; Store/package updates remain separate from optional third-party graphics components.

Optional graphics functions still require compatible games/renderers/hardware and may download clearly presented third-party components. DLSS/HDR support is not universal. Automated tests use synthetic fixtures: Windows CI verified data transfer, actual WPF profile/close preservation and recovery of library plus a usable configuration backup after actually uninstalling the test MSIX. They do not claim gameplay or graphics-driver stability testing.

## Pacchetto sostitutivo e risultati

- [Scarica lo ZIP per Partner Center](https://github.com/luigip11/GamePanel/actions/runs/44167723550/artifacts/14365439632) e carica soltanto `VirtualLP.GamePanel-1.0.17.0-x64-unsigned.msix` contenuto al suo interno.
- [Verifica MSIX Windows](https://github.com/luigip11/GamePanel/actions/runs/44167723550): 281/281 controlli, prova sulla finestra reale, disinstallazione e recupero completati.
- [Build portatile e verifiche desktop](https://github.com/luigip11/GamePanel/actions/runs/44167723312): completate.
- La certificazione di `1.0.16.0` non copre questa build. Caricare il pacchetto in un nuovo invio/aggiornamento e richiedere una nuova certificazione, mantenendo la pubblicazione manuale. Non pubblicare la vecchia build per sbloccare il portale.
- Se l'invio già certificato non è modificabile, controllare i comandi disponibili nella panoramica/nel menu dell'invio. I comandi dipendono dallo stato: non eliminare il prodotto o il nome riservato. [Controlli degli invii Microsoft](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-submission-control).

## Prima di inviare

1. Verificare [esportazione, migrazione e conservazione](DATA-PRESERVATION.md) sul PC di destinazione e conservare un archivio esterno completato. La build alpha.17 include queste funzioni; disinstallazione/reset può comunque eliminare LocalState.
2. Usare l'identità Partner Center effettiva e un pacchetto senza CompatibilityProbe, con versione a quattro componenti e revisione `.0`.
3. Verificare licenze e policy delle integrazioni scaricabili; verificare eventuali obblighi per piattaforme mod e contenuti di terzi.
4. Revisionare e pubblicare la privacy; allinearla alle richieste di rete e ai dati della build finale.
5. Eseguire WACK e prove sul pacchetto definitivo, incluse disinstallazione/aggiornamento e conservazione dei backup.
6. Conservare e verificare scheda, IARC, immagini e motivazione capability del prodotto; aggiornare le note di test e le novità per alpha.17.
7. Esaminare il riepilogo con il titolare prima di premere Submit for certification. Tenere la pubblicazione manuale.

## Fonti Microsoft

- [Checklist candidatura MSIX](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/create-app-submission)
- [Pubblicazione manuale, capability e note per certificazione](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/manage-submission-options)
- [Processo di certificazione MSIX](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-certification-process)
- [Policy Store](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)
