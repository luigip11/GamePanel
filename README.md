# GamePanel — giochi, grafica e configurazioni

GamePanel è un’app Windows per importare i giochi dal loro EXE, organizzarli in libreria e gestire le configurazioni grafiche compatibili. Analisi locale, backup, ripristino e varianti Originale/Remix seguono la copia selezionata.

Le funzioni vengono proposte in base al gioco, al renderer e all'hardware rilevati. GamePanel non aggiunge DLSS o HDR a un titolo che non li supporta.

## Download

**[Scarica GamePanel alpha.17 per Windows x64](https://github.com/luigip11/GamePanel/actions/runs/44167723312/artifacts/14365599354)** · [Build e controlli](https://github.com/luigip11/GamePanel/actions/runs/44167723312).

[Pacchetto MSIX 1.0.17.0 per Partner Center](https://github.com/luigip11/GamePanel/actions/runs/44167723550/artifacts/14365439632): da caricare nel portale per nuova certificazione, non da installare localmente. Gli artifact richiedono accesso a GitHub e hanno una scadenza indicata nella pagina di download.

Per usare una build portabile, estrarre tutto lo ZIP in una cartella nuova e aprire **GamePanel.exe**. Runtime dell’app incluso; guide in **Docs**, licenze in **Licenses**, materiali in **Assets**. Giochi e pacchetti Remix non sono inclusi.

La versione Store alpha.17 usa un pacchetto MSIX da sottoporre a nuova certificazione; la certificazione di `1.0.16.0` non si estende al codice aggiornato. [Dettagli MSIX](docs/MSIX.md).

## Changelog alpha.17

- Esportazione e importazione ZIP verificate di libreria, copertine, preferenze e punti di ripristino.
- Importazione esplicita dalla cartella dati della copia portatile, con sorgente conservata e blocco dei conflitti.
- Copia esterna configurabile, aggiornata dopo i nuovi backup e alla chiusura regolare; scelta richiesta prima del primo backup Store.
- Conservazione dell'ultimo archivio valido se esportazione, disco esterno o annullamento impediscono l'aggiornamento.
- Avvisi su disinstallazione/reset MSIX, chiusura forzata, archivi privati e percorsi dei giochi da ricollegare.
- Pacchetto Store `VirtualLP.GamePanel` versione `1.0.17.0`, lingua italiana e aggiornamenti tramite canale Store.
- Collaudo Windows esteso a disinstallazione effettiva e recupero di libreria e backup da una copia esterna con dati sintetici.

[Guida a conservazione, migrazione e recupero dei dati](docs/DATA-PRESERVATION.md).

## Schermate

[Schermate della build alpha.17, acquisite dall'app con dati dimostrativi](https://github.com/luigip11/GamePanel/actions/runs/44167723312/artifacts/14366322203). Le immagini statiche sotto documentano la precedente alpha.16.

Le anteprime mostrano la UI finale di alpha.16: Libreria, Gioco e hardware, Grafica, DLSS, Schermo, HDR e Backup.

Catture della vera app WPF alpha.16 su Windows, con libreria dimostrativa isolata e copertine illustrative locali. L'hardware mostrato nella pagina Gioco appartiene al runner Windows; non rappresenta il PC dell'utente. Nessun gioco viene eseguito per produrre queste immagini.

![Libreria GamePanel](docs/images/alpha16-library-nfs.png)

<details>
<summary>Azioni delle schede della libreria</summary>

![Azioni con icone](docs/images/alpha16-library-actions.png)

</details>

<details>
<summary>Gioco e specifiche hardware</summary>

![Gioco e copertina](docs/images/alpha16-game.png)

![CPU, GPU e RAM rilevate](docs/images/alpha16-hardware.png)

</details>

<details>
<summary>Grafica del gioco</summary>

![Scorciatoie grafiche e filtri](docs/images/alpha16-graphics.png)

</details>

<details>
<summary>DLSS: configurazione e parametri</summary>

![Salva e ripristina affiancati](docs/images/alpha16-dlss-profile.png)

![Parametri DLSS](docs/images/alpha16-dlss-parameters.png)

</details>

<details>
<summary>Schermo e configurazione avanzata</summary>

![Monitor selezionato e risoluzione](docs/images/alpha16-screen.png)

![Configurazione avanzata del gioco](docs/images/alpha16-screen-advanced.png)

</details>

<details>
<summary>HDR e schermo</summary>

![Stato colore avanzato e impostazioni HDR](docs/images/alpha16-hdr.png)

</details>

<details>
<summary>Backup e punti di ripristino</summary>

![Backup con Esamina ed Elimina](docs/images/alpha16-backups.png)

</details>

Verifica: **273/273 controlli core Windows superati**, controlli UI, avvio, updater e launcher Explorer riusciti. Le copie Remix generate richiedono un collaudo nel gioco sul PC dell'utente.
