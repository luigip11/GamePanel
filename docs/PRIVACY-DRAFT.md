# GamePanel — informativa privacy da revisionare

**Bozza tecnica del 16 novembre 2026, non ancora approvata dal titolare per la scheda Store.** Descrive il codice attuale; verificare di nuovo le funzioni della build definitiva prima della pubblicazione. Non inserire nel portale un URL che non è stato pubblicato e verificato.

## Dati locali

GamePanel legge i file e i percorsi dei giochi selezionati dall'utente per ricavare informazioni tecniche e gestire le configurazioni supportate. Può conservare sul PC la libreria, le copertine, le preferenze, i risultati dell'analisi, i backup e i componenti scaricati. Rileva inoltre informazioni locali su processore, memoria, GPU e schermi per mostrare opzioni compatibili.

Non è richiesto un account GamePanel. Nel codice attuale non sono presenti servizi di pubblicità, analytics o invio automatico della libreria e dei file EXE a un server GamePanel. Il progetto non gestisce un servizio di sincronizzazione cloud dei dati dell'app.

## Funzioni online e destinatari

- La ricerca online di copertine invia a Wikipedia il testo cercato, precompilato con il titolo del gioco e modificabile dall'utente. Il download dell'immagine contatta il relativo servizio Wikipedia/Wikimedia. Non vengono inviati l'EXE o il suo percorso attraverso questa ricerca.
- Il download di componenti o materiali opzionali contatta le fonti previste dall'integrazione, ad esempio GitHub, reshade.me e nfsmods.xyz. Le fonti applicabili sono indicate nei flussi e nella documentazione dei componenti.
- La versione portatile controlla gli aggiornamenti tramite GitHub quando richiesto. La versione MSIX apre il canale Store e non usa l'updater portatile per sostituire l'app.
- I collegamenti esterni si aprono nelle applicazioni appropriate di Windows, ad esempio il browser o Microsoft Store.

Come per ogni richiesta internet, il servizio destinatario può ricevere indirizzo IP, dettagli della richiesta e informazioni del client. Trattamento e conservazione da parte dei servizi esterni dipendono dalle loro informative. Non dichiarare che una funzione online non trasmette alcun dato.

Riferimenti: [Wikimedia](https://foundation.wikimedia.org/wiki/Policy:Privacy_policy), [GitHub](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement), [Microsoft](https://privacy.microsoft.com/privacystatement). Per gli altri siti consultare l'informativa pubblicata dalla fonte interessata.

## Esportazioni e assistenza

I report e gli archivi esportati dall'utente possono contenere percorsi locali, nomi di giochi e dati diagnostici. L'esportazione non implica un invio automatico al progetto. Prima di condividerli, rivedere il contenuto e rimuovere le informazioni personali non necessarie.

Il supporto del progetto è disponibile su [GitHub Issues](https://github.com/luigip11/GamePanel/issues). Le segnalazioni pubbliche e gli allegati pubblicati lì sono visibili ad altri utenti e soggetti alle condizioni di GitHub. Non inserire password, token o documenti di identità nelle segnalazioni.

## Esportazione, importazione e copia esterna (alpha.17)

La pagina Backup e ripristino può esportare i dati in uno ZIP locale e importarli dopo una reinstallazione o dalla vecchia cartella dati portatile. È possibile scegliere una destinazione esterna: l'app aggiorna l'archivio dopo i nuovi backup e alla chiusura regolare. La preferenza del percorso rimane locale e non viene trasferita tra PC.

Gli ZIP possono contenere percorsi personali, copertine e copie di EXE o componenti salvati dai punti di ripristino. Non sono cifrati e non vengono inviati automaticamente online: conservali privatamente in una posizione sotto il tuo controllo. La copia esterna non viene rimossa automaticamente disinstallando GamePanel. Per rimuoverla cancellala esplicitamente dalla cartella scelta, dopo aver verificato che non sia l'unica copia necessaria.

## Conservazione e cancellazione

I dati locali restano sul PC finché vengono rimossi tramite le funzioni disponibili nell'app o dall'utente. La versione portatile usa normalmente `%LOCALAPPDATA%\GamePanel`; la prova MSIX usa `%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\GamePanel`. Un override diagnostico può impostare un percorso diverso.

Reset o disinstallazione della versione MSIX possono eliminare i dati LocalState, inclusi i backup. Rimuovere l'app non annulla automaticamente le modifiche già applicate alle cartelle dei giochi esterne. Conservare una copia indipendente dei backup necessari prima di rimuovere o reimpostare l'app.

## Da completare dal titolare prima di pubblicare

Confermare identità e recapito del responsabile del progetto da indicare nell'informativa, eventuali requisiti applicabili al trattamento dei dati delle richieste di assistenza, servizi effettivamente presenti nella build finale e data di entrata in vigore. Rimuovere questo paragrafo e lo stato di bozza solo dopo la revisione.
