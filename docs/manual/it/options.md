# Opzioni

[← Manuale](README.md) · Precedente: [Riproduzione log](log-replay.md) · Successivo: [Risoluzione dei problemi →](troubleshooting.md)

Fate clic su **Opzioni** in alto a destra. Ogni modifica viene salvata subito e mantenuta al prossimo avvio. Le opzioni che riguardano la connessione valgono dal prossimo **Connetti**.

| Opzione | Predefinito | Cosa fa |
| --- | --- | --- |
| **Lingua** | English | Lingua dell'interfaccia e dei nomi di parametri, errori e messaggi: English, Deutsch, Español, Hrvatski, Magyar, Italiano, Nederlands, Polski, Português, Srpski. Menu e pulsanti esistono in italiano e inglese; nelle altre lingue restano in inglese e sono tradotti solo i nomi che vengono dalla centralina. Il cambio vale subito, anche da connessi. |
| **Tema** | Come il sistema | *Come il sistema*, *Chiaro* o *Scuro*. Anche dal pulsante del tema accanto a Opzioni. |
| **Attendi il codice ISO quando porti la chiave su MAR** | sì | Prima di inizializzare, ascolta fino a 10 secondi il codice ISO che alcune centraline inviano da sole quando si gira la chiave (come faceva IES2). Fate clic su Connetti, poi girate la chiave. Togliete la spunta quando la chiave è già su MAR, per saltare l'attesa. |
| **Modalità compatibilità (adattatori lenti)** | no | Tempi più lenti per adattatori o linee che perdono byte (come in IES2). Provatela quando vedete *l'adattatore restituisce un byte diverso* o perdite frequenti. Le letture diventano più lente. |
| **Attiva DTR** | no | Attiva la linea DTR della porta seriale. Alcuni cavi alimentano da lì il loro chip K-Line: provatela se un cavo non risponde mai, nemmeno con l'eco. |
| **Abilita comandi attivi (test, cancellazione errori, regolazioni)** | sì | Senza spunta i pulsanti Esegui, Applica e Cancella errori mostrano solo un messaggio. Ogni comando chiede comunque conferma. |
| **Riconnetti in automatico se la comunicazione si interrompe** | sì | Dopo una perdita (chiave girata, connettore che balla) il programma riprova da solo, dopo 0,5, 1 e 2 secondi e poi ogni 5 secondi, e controlla che risponda la stessa centralina. Senza spunta si ferma alla prima perdita come faceva IES2, e si riconnette con **Riconnetti** (F12). |
| **Salva il log della linea K a ogni connessione (da allegare alle segnalazioni)** | no | Scrive ogni byte inviato e ricevuto sulla linea K in un file di testo, `iaw_trace_<data>_<ora>.txt` nella cartella dei log. Il percorso compare nella barra di stato. Allegatelo a ogni segnalazione sulla comunicazione. |
| **Bridge di rete (RFC 2217), indirizzo:porta** | vuoto | Indirizzo di un bridge di rete, per esempio `192.168.4.1:2217`. Quando è valido compare in cima all'elenco **Interfaccia**. Vedi [Bridge di rete](#bridge-di-rete). |
| **Cartella dei log** | `Documenti/marelli-iaw-ecu-studio/logs` | Dove vanno le registrazioni CSV e i log della linea; da qui parte anche **Apri log...**. Viene creata quando serve. |

## Bridge di rete

Il cavo può restare in auto, collegato a un Raspberry Pi (o a un altro computer), mentre il programma gira su un portatile nella stessa rete Wi-Fi o Ethernet. Sul computer con il cavo:

```sh
iaw-cli bridge --port /dev/ttyUSB0 --listen 0.0.0.0:2217
```

Poi scrivete `<indirizzo di quel computer>:2217` in **Bridge di rete** e sceglietelo in **Interfaccia**. Dovrebbero funzionare anche ser2net ed esp-link. Usatelo solo su una rete privata: chi raggiunge l'indirizzo raggiunge l'auto. Dettagli: [Hardware](../../hardware.md#network-bridge-wi-fi-ethernet) (in inglese).

Un normale adattatore seriale Bluetooth o Wi-Fi non funziona: non può cambiare la velocità della linea, che serve a queste centraline.

## File e cartelle

| Cosa | Windows | Linux, Raspberry Pi, macOS |
| --- | --- | --- |
| Impostazioni (`settings.json`) | `%APPDATA%\marelli-iaw-ecu-studio\` | `~/.config/marelli-iaw-ecu-studio/` |
| Definizioni di centraline aggiunte o corrette | `%APPDATA%\marelli-iaw-ecu-studio\definitions\` | `~/.config/marelli-iaw-ecu-studio/definitions/` |
| Log CSV e log della linea (predefinito) | `Documenti\marelli-iaw-ecu-studio\logs\` | `~/Documents/marelli-iaw-ecu-studio/logs/` |

Per ripartire con le impostazioni predefinite chiudete il programma e cancellate `settings.json`.

Un file nella cartella delle definizioni con lo stesso `id` di una definizione integrata la sostituisce: così si prova una definizione corretta o nuova senza una nuova versione del programma. Vedi [ECU definition files](../../definitions.md) (in inglese).

Il programma ricorda anche l'interfaccia, la centralina scelta, se la finestra era massimizzata e la sua dimensione, la scelta delle scale separate e la finestra dei grafici.
