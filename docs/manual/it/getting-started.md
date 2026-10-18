# Installazione e prima connessione

[← Manuale](README.md) · Successivo: [La finestra principale →](main-window.md)

## Cosa serve

- Un computer con Windows (x64), Linux (x64, oppure arm64 come un Raspberry Pi) o macOS (Apple silicon).
- Per parlare con l'auto: un **cavo USB KKL 409.1** con chip FTDI originale e un **adattatore OBD2 → FIAT 3 pin**. Gli adattatori ELM327 non possono funzionare con queste centraline. Tutti i dettagli in [Hardware](../../hardware.md) (in inglese).
- Per provare il programma non serve niente: il **simulatore** integrato si comporta come un'auto.

## Installazione

1. Aprite la [pagina delle release](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/releases) e scaricate l'archivio per il vostro sistema:

   | Sistema | File |
   | --- | --- |
   | Windows x64 | `…-win-x64.zip` |
   | Linux x64 | `…-linux-x64.tar.gz` |
   | Raspberry Pi / Linux arm64 | `…-linux-arm64.tar.gz` |
   | macOS Apple silicon | `…-osx-arm64.tar.gz` |

2. Estraetelo in una cartella a scelta. Contiene due programmi, che non richiedono installazione né .NET:
   - `marelli-iaw-ecu-studio` — l'applicazione desktop descritta in questo manuale;
   - `iaw-cli` — lo [strumento a riga di comando](command-line.md).
3. Avviate `marelli-iaw-ecu-studio`.

I programmi non sono firmati, quindi la prima volta il sistema avvisa:

- **Windows**: SmartScreen mostra "PC protetto da Windows": fate clic su *Ulteriori informazioni* → *Esegui comunque*.
- **macOS**: clic destro sul programma → *Apri*, oppure da terminale `xattr -d com.apple.quarantine marelli-iaw-ecu-studio iaw-cli`.

Per controllare il download confrontate il checksum con `SHA256SUMS.txt` nella pagina della release.

### Prima della prima connessione con il cavo

- **Windows**: installate il driver FTDI VCP se Windows non lo fa da solo. Il cavo compare come porta `COM`.
- **Linux**: aggiungetevi al gruppo `dialout` (`sudo usermod -aG dialout $USER`), poi uscite e rientrate nella sessione.
- **Ovunque**: impostate il latency timer FTDI a 1 ms per letture più veloci. Il programma lo ricorda con un avviso giallo sotto la barra quando la latenza è troppo alta. Come fare: [Hardware](../../hardware.md).

## Primo avvio: provate il simulatore

La prima volta la finestra si apre massimizzata; poi si riapre come l'avete lasciata. Per avere l'interfaccia in italiano scegliete **Italiano** in **Options → Language**.

1. In **Interfaccia** scegliete **Simulatore — demo, senza cavo**.
2. In **Centralina** lasciate **Riconoscimento automatico**, oppure scegliete una famiglia da simulare (per esempio *Weber-Marelli IAW-04K.P8 MPI* per vedere un'auto turbo).
3. Fate clic su **Connetti** (o premete **F10**). Dopo circa tre secondi la centralina viene riconosciuta, nella barra centralina compare il badge **Simulazione** e il cruscotto si anima.

Nel simulatore funziona tutto, compresi errori, test attuatori e registrazione: è il posto più sicuro per imparare a usare il programma. Quando avete finito fate clic su **Disconnetti**.

## Connessione all'auto

1. A **chiave su STOP**, collegate l'adattatore 3 pin alla presa diagnosi FIAT (la posizione cambia da modello a modello: spesso è nel vano motore vicino alla centralina, oppure dietro la plancia) e il cavo KKL al computer.
2. In **Interfaccia** scegliete la porta del cavo (`COM3`, `/dev/ttyUSB0`…). Se non c'è, fate clic su **Aggiorna**.
3. In **Centralina** lasciate **Riconoscimento automatico**. Se conoscete il codice sull'etichetta della centralina (per esempio *IAW 4WF.P8*) potete scriverlo nella casella di ricerca per scegliere direttamente la famiglia; vedi [La finestra principale](main-window.md#scegliere-la-centralina).
4. Fate clic su **Connetti**. La barra di stato dice *Porta la chiave su MAR...*
5. **Ora portate la chiave su MAR.** Non avviate ancora il motore.

Come IES2, il programma prima ascolta fino a 10 secondi il codice ISO che alcune centraline inviano da sole quando si porta la chiave su MAR; se non arriva nulla inizializza la centralina e le chiede il codice. Quando la centralina risponde:

- la barra centralina mostra nome della centralina, codice ISO, CODRIC, veicolo e immobilizzatore;
- il [Cruscotto](dashboard.md) e le altre schede si riempiono di dati in tempo reale;
- la barra di stato mostra *Connesso* e la frequenza di aggiornamento (per esempio *6,8 aggiornamenti/s*).

Se la chiave è già su MAR quando fate clic su Connetti, togliete la spunta a **Attendi il codice ISO quando porti la chiave su MAR** nelle [Opzioni](options.md) per saltare l'attesa di 10 secondi.

Per finire fate clic su **Disconnetti** e solo dopo riportate la chiave su STOP.

### Se non si connette

- **Annulla** (o **Esc**) interrompe il tentativo di connessione.
- *Centralina non riconosciuta* significa che la centralina ha risposto ma il suo codice ISO non è in nessuna definizione: scegliete la famiglia a mano in **Centralina** e riprovate.
- *Errore nell'apertura della porta* significa che la porta è in uso, non c'è più, oppure mancano i permessi (Linux: gruppo `dialout`).

Altro in [Risoluzione dei problemi](troubleshooting.md).

## E poi

- Leggete i dati del motore nel [Cruscotto](dashboard.md) e in [Parametri](parameters.md).
- Controllate gli [Errori](errors.md).
- Prima di qualsiasi test attuatore, leggete [Test attuatori e regolazioni](tests-and-adjustments.md).
