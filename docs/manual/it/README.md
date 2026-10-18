# Marelli IAW ECU Studio — Manuale utente

[Read in English](../en/README.md)

Marelli IAW ECU Studio legge le centraline motore Magneti Marelli IAW delle FIAT, Lancia e Alfa Romeo degli anni '90 attraverso la presa diagnosi FIAT a 3 pin: riconosce la centralina e l'auto, mostra i dati del motore in tempo reale, legge e cancella gli errori, esegue test attuatori e regolazioni, registra e riproduce i log.

Questo manuale è per chi usa il programma, a casa o in officina. Descrive l'applicazione desktop e lo strumento a riga di comando `iaw-cli`. Per compilare dai sorgenti e per gli sviluppatori vedi il [README](../../../README.it.md).

> **Il programma non è ancora stato provato su un'auto vera.** Funziona tutto con il simulatore integrato ed è verificato contro il codice originale di IES2. Partite dalle sole letture e segnalate i risultati (vedi [Risoluzione dei problemi](troubleshooting.md#segnalare-un-problema)).

![Cruscotto](../../screenshots/cruscotto-04k.png)

## Indice

**Per iniziare**

1. [Installazione e prima connessione](getting-started.md) — scaricare, avviare, provare il simulatore, collegarsi all'auto
2. [La finestra principale](main-window.md) — barra di connessione, barra centralina, schede, barra di stato, scorciatoie

**Uso del programma**

3. [Cruscotto](dashboard.md) — scheda veicolo, strumenti, barre e spie
4. [Parametri e registrazione CSV](parameters.md) — la tabella in tempo reale, cosa leggere, cosa registrare
5. [Errori](errors.md) — lettura degli errori, significato di [A], [V], [S], cancellazione
6. [Test attuatori e regolazioni](tests-and-adjustments.md) — comandare i componenti, azzerare gli autoadattivi, trimmer CO
7. [Grafici](graphs.md) — grafici in tempo reale, zoom, scale, finestra temporale
8. [Riproduzione log](log-replay.md) — rivedere i log CSV, anche quelli di IES2
9. [Opzioni](options.md) — ogni impostazione, e dove vengono salvati i file

**Quando qualcosa non va**

10. [Risoluzione dei problemi](troubleshooting.md) — messaggi, comunicazione persa, log della linea K, segnalazioni

**Riferimento**

11. [Riga di comando (`iaw-cli`)](command-line.md) — tutti i comandi e le opzioni
12. [Riferimento centraline](ecu-reference.md) — parametri, errori, test e regolazioni di ogni famiglia
13. [Glossario](glossary.md) — codice ISO, CODRIC, autoadattivi, stepper, linea K e altri termini
14. [Hardware](../../hardware.md) (in inglese) — cavo, adattatore, driver, latenza, bridge di rete, OBD-II

## Prima di tutto, la sicurezza

Test attuatori, cancellazione errori e regolazioni comandano componenti reali: pompa benzina, iniettori, bobine, relè. Prima di ognuno il programma chiede conferma e indica la condizione richiesta (di solito chiave su MAR e motore spento). Lavorate con l'auto ferma, in un luogo ventilato, lontano da vapori di benzina, e non eseguite mai il test di iniettori o pompa con le tubazioni del carburante aperte. I comandi attivi si possono disabilitare del tutto nelle [Opzioni](options.md). L'uso è a vostro rischio: vedi l'esclusione di garanzia nella [licenza](../../../LICENSE).
