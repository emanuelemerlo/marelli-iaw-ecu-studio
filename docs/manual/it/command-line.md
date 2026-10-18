# Riga di comando (`iaw-cli`)

[← Manuale](README.md) · Precedente: [Risoluzione dei problemi](troubleshooting.md) · Successivo: [Riferimento centraline →](ecu-reference.md)

`iaw-cli` fa da terminale quello che fa l'applicazione: identificazione, letture, errori, test. È comodo su un Raspberry Pi senza schermo, per registrazioni lunghe, negli script e per le segnalazioni. Si trova nello stesso archivio dell'applicazione. I messaggi del programma sono in inglese; con `--lang it` i nomi di parametri ed errori sono in italiano.

```sh
iaw-cli <comando> [opzioni]
iaw-cli help
```

Su Windows si avvia come `iaw-cli.exe` dal Prompt dei comandi o da PowerShell nella cartella del programma; su Linux e macOS come `./iaw-cli`.

**Di default è in sola lettura**: test e cancellazione errori richiedono `--allow-active`, e chiedono conferma.

## Comandi

| Comando | Cosa fa |
| --- | --- |
| `ecus` | Elenca le famiglie di centraline conosciute con codici ISO, altri nomi, tecnologia, periodo e veicoli compatibili. |
| `ports` | Elenca le porte seriali: chip USB, nome stabile (`/dev/serial/by-id/…` su Linux), latenza FTDI. |
| `identify` | Si connette e mostra famiglia, codice ISO, CODRIC, veicolo e immobilizzatore. |
| `read` | Legge i parametri in tempo reale, una volta o di continuo, anche su file CSV. |
| `errors` | Legge gli errori di motore e immobilizzatore. |
| `test <nome\|numero>` | Esegue un test attuatore (richiede `--allow-active`). Senza nome né numero elenca i test della centralina connessa. |
| `clear-codes` | Cancella gli errori memorizzati (richiede `--allow-active`). |
| `validate <file\|cartella>` | Controlla i file di definizione delle centraline. |
| `trace <file>` | Riassume un log della linea scritto con `--trace`. |
| `bridge` | Condivide il cavo (o il simulatore) in rete con RFC 2217. |
| `obd <identify\|read\|dtc\|clear>` | OBD-II / EOBD per le auto dal 2001, vedi [sotto](#auto-obd-ii-dal-2001). |

## Opzioni di connessione

| Opzione | Significato |
| --- | --- |
| `--port <nome>` | Porta seriale (`COM3`, `/dev/ttyUSB0`) o bridge di rete (`rfc2217://192.168.4.1:2217`). |
| `--simulate [centralina]` | Usa il simulatore integrato, eventualmente di una famiglia precisa. |
| `--replay <trace>` | Risponde con i byte di un log della linea invece che con un cavo. |
| `--replay-mode <strict\|lookup>` | `strict` (predefinito): stesse richieste nello stesso ordine; `lookup`: qualsiasi richiesta, con i valori registrati. |
| `--ecu <id\|codice\|auto>` | Famiglia o codice sull'etichetta, per esempio `IAW-04K.P8` o `4WF.P8` (predefinito: `auto`). |
| `--compat` | Modalità compatibilità: tempi più lenti per adattatori poco affidabili. |
| `--no-echo` | L'adattatore non restituisce l'eco dei byte inviati. |
| `--no-broadcast` | Non aspetta 10 s il codice ISO all'accensione (chiave già su MAR). |
| `--dtr` | Attiva DTR. |
| `--definitions <cartella>` | File di definizione aggiuntivi, che sostituiscono quelli integrati. |
| `--lang <codice>` | Lingua dei nomi: `en`, `it`, `de`, `es`, `pl`, `pt`, `nl`, `hu`, `hr`, `sr`. |
| `--trace <file>` | Scrive ogni byte sulla linea in un log della linea. |

## Opzioni di `read`

| Opzione | Significato |
| --- | --- |
| `--params <id,id\|all>` | Parametri da leggere, per ID (vedi il [Riferimento centraline](ecu-reference.md)); predefinito `all`. |
| `--count <n>` | Numero di cicli; `0` = fino a Ctrl+C. Predefinito 1. |
| `--csv <file>` | Scrive anche un log CSV (stesso formato dell'applicazione, si apre in [Riproduzione log](log-replay.md)). |
| `--no-reconnect` | Si ferma quando la comunicazione si interrompe invece di riconnettersi. |
| `--reconnect-attempts <n>` | Rinuncia dopo n tentativi (predefinito 0: continua a provare). |
| `--raw` | Stampa anche il valore grezzo di ogni parametro. |

## Opzioni di sicurezza

| Opzione | Significato |
| --- | --- |
| `--allow-active` | Consente test attuatori e cancellazione errori. |
| `--allow-unverified` | Anche per le definizioni non segnate come verificate. |
| `--yes` | Non chiede conferma. |

## Esempi

```sh
# Prova senza auto
iaw-cli read --simulate --count 5
iaw-cli read --simulate IAW-04K.P8 --lang it --count 5

# Trovare il cavo
iaw-cli ports

# Identificazione: avviate il comando, poi girate la chiave su MAR
iaw-cli identify --port /dev/ttyUSB0
# Chiave già su MAR
iaw-cli identify --port COM3 --no-broadcast

# Giri e temperatura acqua fino a Ctrl+C, salvando un CSV
iaw-cli read --port /dev/ttyUSB0 --params PERIODE,MT_EAU_L --count 0 --csv giro.csv --lang it

# Errori, in italiano
iaw-cli errors --port /dev/ttyUSB0 --lang it

# Test attuatori: elencarli con il loro numero, poi eseguirne uno (chiave su MAR, motore spento)
iaw-cli test --port /dev/ttyUSB0
iaw-cli test 1 --port /dev/ttyUSB0 --allow-active
iaw-cli clear-codes --port /dev/ttyUSB0 --allow-active
```

## Log della linea

Aggiungete `--trace file.txt` a qualsiasi comando per registrare la sessione byte per byte. Poi:

```sh
iaw-cli trace punto.trace.txt
```

stampa richieste, timeout, eco errate e la cronologia degli eventi: la prima cosa da guardare quando la comunicazione non va.

### Riprodurre un log della linea

Un log della linea può prendere il posto dell'auto:

```sh
iaw-cli read --replay punto.trace.txt --count 20
iaw-cli read --replay punto.trace.txt --replay-mode lookup --params all
```

In modalità `strict` il programma deve inviare esattamente le richieste registrate, e si ferma dicendo dove ha divergito: un test di regressione. In modalità `lookup` risponde a qualsiasi richiesta presente nel log, per provare altri parametri con i valori di un'auto vera.

## Bridge di rete

```sh
# Sul computer con il cavo (per esempio un Raspberry Pi in auto)
iaw-cli bridge --port /dev/ttyUSB0 --listen 0.0.0.0:2217
# Provare il collegamento con il simulatore
iaw-cli bridge --simulate --listen 0.0.0.0:2217

# Sull'altro computer
iaw-cli read --port rfc2217://192.168.4.1:2217
```

`--listen` vale di default `127.0.0.1:2217` (solo questo computer). Usate `0.0.0.0` solo su una rete privata.

## Auto OBD-II (dal 2001)

Dal 2001 circa le FIAT, Lancia e Alfa Romeo a benzina vendute in Europa hanno l'EOBD sulla presa a 16 pin. Funziona lo stesso cavo KKL, **senza** l'adattatore 3 pin. Per ora è disponibile solo da riga di comando.

```sh
iaw-cli obd identify --port /dev/ttyUSB0      # protocollo, centraline, PID supportati, VIN
iaw-cli obd read --port /dev/ttyUSB0 --params RPM,ECT,MAP,STFT1 --count 0
iaw-cli obd dtc --port /dev/ttyUSB0           # spia MIL, errori memorizzati e in attesa
iaw-cli obd clear --port /dev/ttyUSB0 --allow-active
iaw-cli obd read --simulate kwp-fast          # senza auto
```

| Opzione | Significato |
| --- | --- |
| `--protocol <auto\|iso9141\|kwp-slow\|kwp-fast>` | Protocollo sulla K-Line; `auto` prova l'init a 5 baud, poi il fast init KWP2000. |
| `--simulate [iso9141\|kwp-slow\|kwp-fast]` | Auto OBD simulata. |
| `--params <id,id\|all>` | PID per nome (`RPM`, `ECT`, `MAP`) o numero (`0C`, `05`); predefinito tutti quelli supportati. |
| `--count <n>` | Numero di cicli, `0` = fino a Ctrl+C. |

Sono i valori EOBD standard; i dati del costruttore (autoadattivi, test attuatori di ogni centralina) non sono compresi. Le auto CAN (dal 2008 circa) richiedono un altro adattatore e non sono supportate.
