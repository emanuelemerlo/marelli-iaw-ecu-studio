# Cruscotto

[← Manuale](README.md) · Precedente: [La finestra principale](main-window.md) · Successivo: [Parametri →](parameters.md)

Il cruscotto mostra a colpo d'occhio lo stato del motore, con i parametri in tempo reale raggruppati per impianto e disegnati come strumenti.

![Cruscotto IAW-04K.P8](../../screenshots/cruscotto-04k.png)

## Scheda Veicolo e centralina

La scheda in alto a sinistra descrive la famiglia della centralina e, da connessi, l'auto.

| Riga | Significato |
| --- | --- |
| **Veicolo riconosciuto** | Auto e motore riconosciuti dal codice ISO, dalle tabelle di IES2. Se il codice non è in tabella: *Il codice ISO … non è nella tabella veicoli* (il programma funziona lo stesso: manca solo il nome dell'auto). |
| Nome della centralina | Per esempio *Weber-Marelli IAW-04K.P8 MPI*. |
| **Tecnologia** | Che sistema è, per esempio iniezione single-point per motori di piccola cilindrata. |
| **Nota anche come** | Altri nomi della stessa centralina, come stampati sulla sua etichetta (per esempio *IAW 4WF.P8*). |
| **Periodo** | Anni di produzione delle auto che la montano. |
| **Veicoli compatibili** | Apritelo per vedere, marca per marca, i modelli e i motori che usano questa famiglia. Indicato come *indicativo, da verificare* quando l'elenco non è stato confermato. Sotto, **Riconosciuti dal codice ISO** elenca le auto che il programma sa riconoscere da solo. |

Prima di connettersi la scheda mostra la famiglia scelta nell'elenco **Centralina**: un modo rapido per sapere quale famiglia monta un'auto, senza l'auto.

![Scheda veicolo](../../screenshots/vehicles.png)

## Gruppi

Le altre schede raggruppano i parametri per impianto. Ogni famiglia di centraline ha i suoi parametri (vedi il [Riferimento centraline](ecu-reference.md)); i gruppi senza niente da mostrare non compaiono.

| Gruppo | Contenuto tipico |
| --- | --- |
| **Motore** | Giri motore, tempo di iniezione, anticipo, pressione collettore, fase di iniezione |
| **Temperature** | Temperatura acqua motore e aria aspirata |
| **Impianto elettrico** | Tensione batteria |
| **Miscela e sonda lambda** | Correzione lambda, tensione sonda lambda |
| **Farfalla e minimo** | Angolo farfalla, posizione e guadagni dello stepper, regime minimo obiettivo, duty cycle VAE |
| **Autoadattivi** | Trimmer CO, autoadattivi di miscela e di minimo |
| **Stato** | Spie di stato: motore in moto, lambda in anello chiuso, clima inserito, marcia inserita… |
| **Altro** | Tutto il resto che la definizione prevede |

## Strumenti

- **Strumenti circolari** per i valori principali (giri, pressione collettore, temperatura acqua, tensione batteria). Gli archi colorati sono le zone di attenzione (giallo) e di pericolo (rosso), per esempio fuorigiri o tensione batteria troppo bassa o troppo alta.
- **Barre** con valore e unità a destra. Le barre delle correzioni che possono andare nei due versi (correzione lambda, trimmer CO, offset degli autoadattivi, e lo stepper sulla IAW-04K.P8) si riempiono dal centro: verso destra per una correzione positiva, verso sinistra per una negativa.
- **Valori**: piccoli riquadri con numero e unità, per i parametri senza una scala naturale (guadagni, offset, regime minimo obiettivo).
- **Spie di stato**: grigie se spente; **verdi** se accese; **blu** se accese per uno stato solo informativo (per esempio clima inserito, marcia inserita); **arancioni** per uno stato da tenere d'occhio (anticipo ridotto per battito).

Un valore mostrato come **#ERR** non è stato decodificato nell'ultima lettura.

## Da sapere

- Il cruscotto mostra solo i parametri spuntati nella colonna **Leggi** di [Parametri](parameters.md). Di default sono spuntati tutti; togliere la spunta a quelli che non servono fa aggiornare più in fretta gli altri.
- Gli strumenti si adattano alla finestra, tra l'80% e il 150% della loro dimensione, così tutto sta nello schermo senza scroll. Su uno schermo piccolo invece la finestra scorre.
- I nomi seguono la lingua scelta nelle [Opzioni](options.md), e così i separatori decimali.
- Il tema scuro è comodo in un garage poco illuminato: sceglietelo con il pulsante del tema accanto a Opzioni.

![Tema scuro](../../screenshots/dashboard-dark.png)
