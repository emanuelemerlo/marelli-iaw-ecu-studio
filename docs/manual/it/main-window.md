# La finestra principale

[← Manuale](README.md) · Precedente: [Installazione](getting-started.md) · Successivo: [Cruscotto →](dashboard.md)

![Finestra principale](../../screenshots/parameters.png)

*Alcune schermate sono in inglese o di una versione precedente; fanno fede i nomi usati in questo manuale.*

Dall'alto in basso la finestra contiene:

1. la **barra di connessione**: interfaccia, centralina, Connetti, tema, Opzioni;
2. una riga gialla di **avviso**, solo quando c'è qualcosa da dire sulla porta (per esempio una latenza FTDI troppo alta);
3. la **barra centralina**, solo da connessi: con che cosa sta parlando il programma;
4. le **schede**: Cruscotto, Parametri, Errori, Test attuatori, Regolazioni, Grafici, Riproduzione log;
5. la **barra di stato**: cosa sta succedendo, il file di registrazione o del log della linea, la frequenza di aggiornamento.

Il layout si adatta alla finestra: cruscotto e tabella dei parametri si ingrandiscono o si riducono per stare nello schermo senza scroll, fino a una dimensione che resta leggibile, e su uno schermo largo i parametri si dispongono su più colonne.

## Barra di connessione

| Comando | Cosa fa |
| --- | --- |
| **Interfaccia** | Dove si trova la centralina: una porta seriale (il cavo KKL), un bridge di rete impostato nelle [Opzioni](options.md#bridge-di-rete), oppure il **Simulatore**. Il programma ricorda l'ultima scelta. |
| **Aggiorna** | Cerca di nuovo le porte, per esempio dopo aver collegato il cavo. |
| **Centralina** (casella di ricerca) | Restringe l'elenco delle centraline: scrivete il codice sull'etichetta, un'auto o una marca. |
| **Centralina** (elenco) | **Riconoscimento automatico** (riconosce la famiglia dal codice ISO), oppure una famiglia precisa. Ogni voce ha una seconda riga con gli altri nomi della centralina e alcune vetture. |
| **Connetti** / **Annulla** / **Riconnetti** / **Disconnetti** | Un solo pulsante che cambia con lo stato della connessione, vedi sotto. |
| **Tema** | Mostra il tema attuale; il menu sceglie *Come il sistema*, *Chiaro* o *Scuro*. |
| **Opzioni** | Lingua, tema e impostazioni di connessione: vedi [Opzioni](options.md). |

Interfaccia e centralina si possono cambiare solo da disconnessi.

### Scegliere la centralina

**Riconoscimento automatico** è quasi sempre la scelta giusta: il programma legge il codice ISO e sceglie la famiglia corrispondente. La centralina connessa compare poi nella barra centralina.

Scegliete la famiglia a mano quando:

- il programma dice *Centralina non riconosciuta*;
- la centralina è una IAW-04K.P8 (anche IAW 4WF.P8) e la chiave è già su MAR: questa famiglia viene riconosciuta in automatico solo dal codice ISO che invia quando si gira la chiave, quindi lasciate spuntato **Attendi il codice ISO quando porti la chiave su MAR** e girate la chiave dopo aver fatto clic su Connetti, oppure sceglietela a mano;
- volete simulare una famiglia precisa.

La casella di ricerca cerca nel nome della famiglia, negli altri nomi, nelle auto e nei motori compatibili. Ogni parola scritta deve trovarsi da qualche parte: `4wf p8`, `coupe turbo`, `alfa 33`, `punto 1.2 16v` funzionano tutte. Scrivendo un codice viene selezionata la prima famiglia che corrisponde. Le famiglie non segnate come verificate mostrano **(NON VERIFICATA)**: i loro comandi attivi restano disabilitati.

### Stati della connessione

| Stato | Pulsante | Tastiera | Significato |
| --- | --- | --- | --- |
| Non connesso | **Connetti** | F10 | Pronto a partire. |
| Connessione | **Annulla** | Esc | In attesa della chiave su MAR, inizializzazione della centralina. |
| Connesso | **Disconnetti** | — | Lettura dei dati in corso. |
| Comunicazione persa | **Riconnetti** e **Disconnetti** | F12 | La centralina ha smesso di rispondere. Con la riconnessione automatica attiva il programma riprova da solo; **Riconnetti** riprova subito. Vedi [Risoluzione dei problemi](troubleshooting.md#comunicazione-persa). |

## Barra centralina

Visibile da connessi:

| Campo | Significato |
| --- | --- |
| **Centralina** | Famiglia della centralina connessa, per esempio *Magneti-Marelli IAW-8F/18F MPI*. Il suffisso **ECOL** (come in IES2) indica una centralina senza immobilizzatore. |
| **Codice ISO** | Codice che identifica software e calibrazione, per esempio `55-31-80-02-94-1C`. Determina il veicolo. |
| **CODRIC** | Codice hardware della centralina. Il programma lo controlla quando si riconnette, così una centralina diversa non viene mai scambiata per quella connessa. |
| **Veicolo** | Auto e motore riconosciuti dal codice ISO, oppure *Sconosciuto*. Passateci sopra con il mouse per leggerlo per intero. |
| **Immobilizzatore** | *presente* se risponde un immobilizzatore FIAT CODE, altrimenti *assente*. |
| **Simulazione** | Badge mostrato quando i dati arrivano dal simulatore. |

## Schede

| Scheda | Contenuto | Pagina |
| --- | --- | --- |
| **Cruscotto** | Scheda veicolo e dati in tempo reale come strumenti, barre e spie, raggruppati per impianto. | [Cruscotto](dashboard.md) |
| **Parametri** | Tutti i valori in tempo reale in una tabella: cosa leggere, cosa mettere in grafico, registrazione CSV. | [Parametri](parameters.md) |
| **Errori** | Errori motore e immobilizzatore, cancellazione errori. | [Errori](errors.md) |
| **Test attuatori** | Comandare un componente (pompa, iniettore, bobina…) per qualche secondo. | [Test e regolazioni](tests-and-adjustments.md) |
| **Regolazioni** | Abilitare, disabilitare o azzerare gli autoadattivi; impostare trimmer CO e stepper. | [Test e regolazioni](tests-and-adjustments.md#regolazioni) |
| **Grafici** | Grafici in tempo reale dei parametri scelti. | [Grafici](graphs.md) |
| **Riproduzione log** | Rivedere un log CSV, anche senza auto. | [Riproduzione log](log-replay.md) |

Prima di connettersi il Cruscotto mostra la scheda della famiglia scelta in **Centralina**, e la **Riproduzione log** è disponibile; le altre schede restano grigie finché la centralina non risponde.

## Barra di stato

- **A sinistra**: lo stato (*Non connesso*, *Porta la chiave su MAR...*, *Connesso: …*, *Comando in corso...*), l'esito dell'ultimo comando, o il motivo della perdita di comunicazione.
- **Al centro**: il file in registrazione (*Registrazione in …*), l'ultimo file salvato (*Salvato …*) o il log della linea (*Log della linea salvato in …*).
- **A destra**: la frequenza di aggiornamento reale, in aggiornamenti al secondo. Meno parametri leggete, più è alta.

## Scorciatoie da tastiera

| Tasto | Azione |
| --- | --- |
| **F10** | Connetti |
| **Esc** | Annulla il tentativo di connessione |
| **F12** | Riconnetti dopo una perdita di comunicazione (come in IES2) |

Nei grafici: rotella per ingrandire, trascinare per spostarsi, doppio clic per ripristinare la vista.
