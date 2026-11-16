# Verifica MSIX / Microsoft Store

Il percorso MSIX è una prova tecnica per distribuire GamePanel tramite Microsoft Store. Non equivale a un'approvazione dello Store o a una soluzione già firmata per il blocco Smart App Control dell'alpha portatile.

## Architettura

- Applicazione desktop WPF x64 `packagedClassicApp`, `mediumIL`, capability `runFullTrust`; non un'AppContainer e non un'app eseguita come amministratore.
- I dialoghi WPF selezionano gli stessi percorsi reali dei giochi. Le operazioni rimangono soggette ai permessi Windows dell'utente: una cartella non scrivibile non diventa scrivibile grazie al pacchetto.
- Il broker Explorer rimane il percorso per UG1 Remix. Il test usa esclusivamente un EXE x86 sintetico compilato nel job; non esegue giochi, crack, mod o EXE caricati dall'utente.
- L'identità di pacchetto viene letta tramite `GetCurrentPackageFamilyName`. Quando presente, il controllo aggiornamenti apre la Libreria di Microsoft Store; il metodo di installazione dell'updater portatile rifiuta l'esecuzione prima di scaricare o sostituire file.
- Il pacchetto MSIX esclude `GamePanel.Updater*`. L'updater continua a esistere nell'artifact portatile, senza modifiche al relativo percorso di distribuzione.
- Nessuna capability `allowElevation`, `unvirtualizedResources`, servizio Windows, driver o modifica della policy di sicurezza.

## Dati e migrazione

La copia portatile continua a usare `%LOCALAPPDATA%\GamePanel`. La copia con identità di pacchetto usa esplicitamente `%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\GamePanel`, fuori dall'installazione in WindowsApps. Il report della prova Windows registra il percorso effettivo.

`GAMEPANEL_DATA_ROOT` rimane disponibile come override diagnostico assoluto. La prova MSIX rifiuta un override presente, così non può passare usando accidentalmente la cartella di un test portatile.

Non viene eseguita una migrazione automatica della libreria, dei backup o dei pacchetti scaricati dell'alpha portatile. L'app di prova parte con dati separati; i dati della copia portatile restano conservati. Prima di una distribuzione pubblica servono una procedura esplicita di migrazione/esportazione e messaggi di conservazione dei backup.

**Disinstallare o reimpostare un'app MSIX può eliminare i dati LocalState, inclusi i backup lì conservati.** Non usare questa prova come unico deposito di ripristino di giochi reali. La prova automatica usa solo dati sintetici e rimuove il proprio pacchetto alla fine. Le modifiche già applicate a cartelle di giochi esterne non vengono annullate automaticamente disinstallando l'app.

## Compilazione del pacchetto da esaminare

Su Windows con SDK Windows e SDK .NET fissato da `global.json`, dopo la pubblicazione standard:

```powershell
./scripts/build-msix.ps1 -PackageDirectory ./artifacts/GamePanel-win-x64
```

Il comando crea manifest, icone derivate dall'icona dell'app e MSIX **non firmato**, validato da MakeAppx. Conserva l'artifact portatile originale. Non genera certificati e non installa il pacchetto.

L'identità predefinita `GamePanel.Compatibility`, il publisher `CN=GamePanelCompatibility` e la versione `0.1.16.0` sono identità di prova, non dati di Partner Center. Per un futuro pacchetto di candidatura devono essere sostituiti con i valori esatti riservati nel proprio account:

```powershell
./scripts/build-msix.ps1 -PackageDirectory ./artifacts/GamePanel-win-x64 `
    -IdentityName '<Package/Identity/Name da Partner Center>' `
    -Publisher '<Package/Identity/Publisher da Partner Center>' `
    -PublisherDisplayName '<nome editore registrato>' `
    -PackageVersion '0.1.16.0'
```

La revisione della versione deve terminare in `.0` per l'invio Store. Non inviare il pacchetto di prova con `CompatibilityProbe`, né descriverlo come certificato o approvato.

## Collaudo Windows isolato

Il workflow `Windows MSIX compatibility`:

1. Esegue i controlli core e pubblica l'app senza avviare componenti grafici esterni.
2. Crea un pacchetto da esaminare senza probe e un secondo pacchetto `GamePanel.CompatibilityTest` con un'app di test aggiuntiva.
3. Sul runner CI usa un certificato effimero per firmare **soltanto il secondo pacchetto** e aggiunge temporaneamente la relativa fiducia al runner. Il certificato non è un certificato di produzione, non viene consegnato e non viene installato sul PC dell'utente. La prova non disattiva Smart App Control o altre policy.
4. Registra e attiva il pacchetto attraverso l'API Windows, verifica identità e LocalState, analisi/libreria di un EXE esterno sintetico, backup e ripristino di una configurazione esterna, rifiuto dell'updater portatile e broker Explorer.
5. Attiva la vera finestra GamePanel dall'identità registrata e verifica la presenza dell'azione aggiornamenti nella UI.
6. Rimuove pacchetto e certificato di prova dal runner. Gli artifact includono evidenza JSON e un pacchetto **non firmato** da esaminare, non il pacchetto firmato con la chiave effimera.

Una prova fallita è un blocco reale da investigare, non un esito positivo. Anche una prova riuscita non dimostra compatibilità con giochi reali, hardware grafico specifico, tutte le cartelle protette o Smart App Control in enforcement.

## Prima della pubblicazione

- Riservare identità e nome prodotto in Partner Center; verificare l'idoneità dell'account.
- Completare migrazione/esportazione dei dati e collaudo di aggiornamento e disinstallazione con backup conservati fuori dal pacchetto.
- Richiedere e giustificare `runFullTrust`; eseguire Windows App Certification Kit sul pacchetto definitivo.
- Verificare con le policy Store le funzioni di download/installazione di componenti esterni, gli asset, le licenze e l'eventuale aggiornamento del codice aggiuntivo. Non si presume che la disponibilità di un file su GitHub ne autorizzi la distribuzione o ne garantisca l'accettazione nello Store.
- Eseguire sul pacchetto definitivo i test con i giochi dell'utente: percorso selezionato, permessi, broker Explorer, risoluzione, backup e recupero. I test dei renderer rimangono separati dalla prova MSIX.
- Pubblicare solo dopo certificazione. Microsoft firma il pacchetto MSIX accettato: questa firma non rende attendibile o firmato lo ZIP portatile su GitHub e non firma automaticamente i componenti scaricati separatamente.

## Riferimenti

- [Conversione manuale di un'app desktop](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-manual-conversion)
- [Comportamento delle app desktop con pacchetto](https://learn.microsoft.com/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)
- [Accesso ai dati dell'app](https://learn.microsoft.com/windows/msix/msix-troubleshooting-guide)
- [Distribuzione e firma Store/MSIX](https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path)
