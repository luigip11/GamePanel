# Conservare e trasferire i dati di GamePanel

Disponibile da alpha.17 / pacchetto Store `1.0.17.0`. I comandi sono in **Backup e ripristino**, anche senza un gioco analizzato.

## Prima di disinstallare o reimpostare l'app

Reset e disinstallazione MSIX possono eliminare libreria e backup interni. Un punto di ripristino presente nell'app non garantisce che sopravviva alla disinstallazione.

1. Apri **Scegli copia esterna**.
2. Scegli uno ZIP in Documenti, su un disco esterno o in un'altra cartella personale reale, fuori da AppData, Temp e dalla cartella dell'app.
3. Attendi il messaggio di copia aggiornata e verificata. Tieni collegato il disco esterno.
4. In alternativa usa **Esporta dati e backup** per creare un archivio indipendente.

La copia esterna configurata viene aggiornata dopo i nuovi backup, dopo applicazione/recupero dei profili gestiti e alla chiusura regolare. La stessa protezione copre i backup automatici creati dai flussi ReShade, Widescreen e asset: se la copia esterna del punto di ripristino fallisce, il flusso viene interrotto e il backup interno resta conservato. L'ultima copia viene sostituita solo dopo che la nuova è completa e verificata. Se il disco manca o l'esportazione fallisce, i dati interni restano conservati e l'app segnala che la copia esterna non è aggiornata. Non disinstallare finché non hai risolto.

Nella versione Store, prima di creare il primo backup è obbligatorio scegliere una posizione esterna; annullando non viene creato il nuovo backup. La libreria richiama la protezione dei dati se manca questa configurazione.

Una chiusura forzata o un arresto di Windows possono impedire l'ultimo aggiornamento: resta valido l'ultimo ZIP completato. GamePanel non può intercettare una disinstallazione/reset avviati direttamente da Windows né proteggere da guasti o cancellazione del disco esterno.

## Recupero dopo reinstallazione

Usa **Importa archivio dati**, scegli il tuo ZIP e conferma. L'app verifica indice e hash prima di pubblicare i file. Non avvia giochi e non modifica le loro cartelle.

File già identici vengono mantenuti; file esistenti diversi bloccano l'importazione senza sovrascrittura. Non è un'unione automatica di due librerie diverse. Per migrare una libreria completa è più semplice partire da dati vuoti. In caso di conflitto conserva ed esporta entrambe le copie: non cancellare l'unica copia valida per forzare l'importazione.

I percorsi dei giochi restano originali: se li hai spostati, ricollegali e rianalizzali. I punti di ripristino restano associati al percorso originale; il trasferimento a un altro percorso non è automatico. Importare un punto di ripristino non ripristina automaticamente i file del gioco: usa poi Esamina/Ripristina, che mantiene i controlli sui conflitti.

## Dalla copia portatile allo Store

Puoi esportare dalla nuova copia portatile e importare lo ZIP nello Store. Se la vecchia copia non si apre, usa **Importa dati della copia portatile** e scegli la cartella dati, normalmente `%LOCALAPPDATA%\\GamePanel`, contenente `Library` e/o `Backups`. Non scegliere la cartella di GamePanel.exe o del gioco.

La sorgente viene letta e conservata: nessun file originale è spostato o eliminato. Dopo l'importazione configura una copia esterna per lo Store. La vecchia preferenza di destinazione non viene trasferita: potrebbe riferirsi a un altro PC/disco.

## Contenuto e riservatezza

Lo ZIP conserva i dati presenti nella cartella dati di GamePanel: libreria, cover, preferenze, punti di ripristino e altri dati locali delle integrazioni. Sono esclusi updater, file temporanei, blocchi di operazione e preferenza macchina della destinazione esterna.

Non è una copia completa dei giochi o di tutti i salvataggi. I punti di ripristino possono contenere EXE e componenti salvati dal backup. Gli ZIP possono contenere percorsi personali e nomi dei giochi: conservali privatamente, non pubblicarli con l'app. Non sono cifrati e non vengono caricati automaticamente online.

Link/junction, percorsi fuori dalla destinazione, nomi ambigui/pericolosi e dati alterati sono bloccati. Limiti: 16 GiB complessivi, 2 GiB per file, 100.000 file, indice di 16 MiB. Trasferimenti incompleti non sono copie valide.

## Verifiche automatiche

I test coprono recupero di libreria/cover/preferenze e backup utilizzabili dopo perdita dei dati interni, importazione portatile non distruttiva, conflitti, archivi alterati, traversal, link, annullamento e conservazione della copia precedente. Il collaudo MSIX disinstalla realmente il pacchetto sintetico e recupera libreria e un backup usato poi per ripristinare una configurazione. Questo non sostituisce il test sul PC dell'utente.
