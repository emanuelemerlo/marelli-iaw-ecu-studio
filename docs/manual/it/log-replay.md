# Riproduzione log

[← Manuale](README.md) · Precedente: [Grafici](graphs.md) · Successivo: [Opzioni →](options.md)

La scheda **Riproduzione log** rivede un log CSV come se stesse succedendo di nuovo, con i valori e il grafico che avanzano nel tempo. Funziona senza auto e senza connettersi: potete guardare un giro a casa, o un log che qualcuno vi ha mandato.

![Riproduzione log](../../screenshots/replay.png)

## Aprire un log

Fate clic su **Apri log...** e scegliete un file. La finestra si apre nella cartella dei log impostata nelle [Opzioni](options.md#file-e-cartelle). Si possono aprire due tipi di file:

- i log registrati con questo programma (**Registra CSV**, oppure `iaw-cli read --csv`);
- i log CSV esportati da **IES2**, il programma originale: vengono riconosciuti da soli e indicati come *Esportazione IES2*.

Sotto la barra una riga di riepilogo mostra centralina, veicolo, numero di campioni e durata, per esempio *IAW-6F/16F · Punto 55 1.1 SPI 5M/6M ECE F2 · 4210 campioni · 10:02.4*. Se il file non si può leggere, un messaggio dice perché.

## Riproduzione

| Comando | Cosa fa |
| --- | --- |
| **Riproduci** / **Pausa** | Avvia e ferma la riproduzione. Alla fine si ferma da sola. |
| **Velocità** | 1x (tempo reale), 2x, 5x o 10x. |
| Cursore della posizione | Trascinatelo per saltare a qualsiasi momento; a destra c'è il tempo. |
| **Scale separate** | Un asse verticale per segnale, come nei [Grafici](graphs.md). |

A sinistra c'è l'elenco delle colonne del log, ciascuna con il suo valore nella posizione attuale e la sua unità. Spuntate una colonna per disegnarla nel grafico a destra. Nel grafico la rotella ingrandisce, il trascinamento sposta e il doppio clic ripristina la vista.

## A cosa serve

- Rivedere con calma un giro dopo averlo registrato su strada.
- Confrontare due sessioni, per esempio prima e dopo la sostituzione di un sensore.
- Leggere vecchi log di IES2 su Linux o macOS.
- Allegare un log a una [segnalazione](troubleshooting.md#segnalare-un-problema) quando un valore non torna.

Per riprodurre i byte di un'intera sessione (non solo i valori), in modo che il programma parli con la registrazione come se fosse l'auto, si usa un log della linea con `iaw-cli read --replay`: vedi [Riga di comando](command-line.md#riprodurre-un-log-della-linea).
