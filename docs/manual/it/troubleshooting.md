# Risoluzione dei problemi

[← Manuale](README.md) · Precedente: [Opzioni](options.md) · Successivo: [Riga di comando →](command-line.md)

## Prima di tutto

- Provate il **Simulatore**: se funziona, il programma è a posto e il problema è nel cavo, nell'adattatore o nell'auto.
- Eseguite `iaw-cli ports`: elenca le porte seriali con il chip USB e il nome stabile, e avvisa quando la latenza FTDI è troppo alta.
- Controllate l'adattatore 3 pin con un tester: pin 16 dell'OBD2 = +12 V, pin 4/5 = massa, pin 7 = K-Line ([Hardware](../../hardware.md)).
- La centralina parla solo con la chiave **su MAR**.

## Messaggi

| Messaggio | Causa probabile e cosa fare |
| --- | --- |
| **Il cavo non è nell'elenco Interfaccia** | Fate clic su **Aggiorna**. Windows: installate il driver FTDI. Linux: controllate con `ls /dev/ttyUSB*`. |
| *Errore nell'apertura della porta …* | La porta è usata da un altro programma (chiudete IES2, altri programmi di diagnosi, monitor seriali), è stata scollegata, oppure mancano i permessi (Linux: `sudo usermod -aG dialout $USER`, poi uscite e rientrate). |
| Avviso giallo *FTDI latency timer is 16 ms…* | Funziona, ma lentamente. Impostate la latenza a 1 ms ([Hardware](../../hardware.md)). |
| Resta su *Porta la chiave su MAR...* | Il programma sta aspettando il codice ISO: girate la chiave adesso. Se era già su MAR, aspettate 10 secondi o togliete la spunta a **Attendi il codice ISO quando porti la chiave su MAR** nelle [Opzioni](options.md). |
| *Centralina non riconosciuta, riporta la chiave su STOP...* (ISO …) | La centralina ha risposto con un codice ISO che nessuna definizione conosce. Riportate la chiave su STOP, scegliete la famiglia a mano in **Centralina** (cercate il codice sull'etichetta) e riconnettetevi. Segnalateci il codice ISO mostrato. |
| **Il codice ISO … non è nella tabella veicoli** | Non è un errore: la centralina è riconosciuta, manca solo il nome dell'auto. Segnalare auto e codice aiuta a completare la tabella. |
| *Nessuna risposta dalla centralina — dall'adattatore non torna nulla, nemmeno l'eco…* | Cavo scollegato, porta USB, o un adattatore senza eco. Controllate il collegamento USB; provate **Attiva DTR**. |
| *Nessuna risposta dalla centralina — l'adattatore restituisce un byte diverso…* | Disturbi sulla linea o velocità errata. Provate la **Modalità compatibilità**; controllate il cablaggio dell'adattatore; provate un'altra porta USB o una prolunga USB più corta. |
| *Nessuna risposta dalla centralina — l'adattatore funziona ma la centralina non risponde…* | Chiave su STOP, connettore 3 pin allentato o cablato male, filo K-Line interrotto. |
| *Risponde una centralina diversa (CODRIC cambiato)* | Dopo la riconnessione ha risposto un'altra centralina (un'altra auto, o una centralina sostituita). Disconnettetevi e connettetevi di nuovo. |
| *I comandi attivi sono disabilitati nelle opzioni.* | Spuntate **Abilita comandi attivi** nelle [Opzioni](options.md). |
| Un test mostra **INIZIALIZZAZIONE NON RIUSCITA** | La centralina ha rifiutato il modo attivo. Controllate la condizione (chiave su MAR, motore spento), poi disconnettetevi, girate la chiave su STOP e di nuovo su MAR, e riprovate. |
| Un valore mostra **#ERR** | La centralina ha inviato un valore che non si può decodificare. Registrate un log CSV e un log della linea e segnalatelo. |
| *Impossibile leggere il file* (Riproduzione log) | Non è un log CSV di questo programma o di IES2, oppure il file è danneggiato. |
| macOS: *impossibile aprire perché lo sviluppatore non può essere verificato* | Clic destro → *Apri*, oppure `xattr -d com.apple.quarantine marelli-iaw-ecu-studio`. |

## Comunicazione persa

Quando più di una richiesta di fila non riceve una risposta valida, la barra di stato mostra *Nessuna risposta dalla centralina* con la causa probabile (vedi la tabella qui sopra) e compare il pulsante **Riconnetti**.

Con **Riconnetti in automatico** attivo (impostazione predefinita) il programma ripete l'inizializzazione dopo 0,5, 1 e 2 secondi e poi ogni 5 secondi, mostrando *riconnessione, tentativo n...*. Ogni volta controlla il CODRIC, così una centralina diversa non viene mai scambiata per quella connessa. Quando la centralina risponde di nuovo la barra di stato dice *Connesso: … (riconnesso)* e le letture riprendono, registrazione compresa.

Se non torna: girate la chiave su STOP e di nuovo su MAR, controllate il connettore, oppure premete **Riconnetti** (F12) per riprovare subito. **Disconnetti** rinuncia.

Cause tipiche: chiave riportata su STOP, il motorino di avviamento che fa calare la tensione della batteria durante l'avviamento, un connettore che balla, il computer che va in sospensione.

## Segnalare un problema

La cosa più utile è un **log della linea**: registra ogni byte sulla linea K e mostra subito se il problema è nel cavo, nella linea o nella centralina.

1. Nelle [Opzioni](options.md) spuntate **Salva il log della linea K a ogni connessione**.
2. Connettetevi e riproducete il problema.
3. Disconnettetevi. La barra di stato mostra il percorso di `iaw_trace_<data>_<ora>.txt`.
4. Aprite un [real car report](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/issues/new?template=car_report.yml) e allegate il log della linea, più il log CSV se un valore non torna. Indicate auto, motore, codice sull'etichetta della centralina, cavo, sistema operativo e cosa è successo.

Le segnalazioni sono benvenute anche quando funziona tutto, e si possono scrivere in italiano. Per l'assistenza in generale vedi [SUPPORT.md](../../../SUPPORT.md).

`iaw-cli trace <file>` stampa un riepilogo di un log della linea: richieste, timeout, eco errate e la cronologia.
