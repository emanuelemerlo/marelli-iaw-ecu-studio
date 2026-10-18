# Parametri e registrazione CSV

[← Manuale](README.md) · Precedente: [Cruscotto](dashboard.md) · Successivo: [Errori →](errors.md)

La scheda **Parametri** elenca in una tabella tutti i valori in tempo reale della centralina connessa, raggruppati come nel cruscotto. Qui si sceglie cosa leggere, cosa mettere in grafico, e si avvia una registrazione.

![Parametri](../../screenshots/parameters.png)

## La tabella

| Colonna | Significato |
| --- | --- |
| **Leggi** | Spuntato: il valore viene chiesto alla centralina a ogni ciclo. Non spuntato: viene saltato, e gli altri valori si aggiornano più in fretta. |
| **Grafico** | Spuntato: il valore viene disegnato nella scheda [Grafici](graphs.md). I primi quattro parametri (giri, iniezione, anticipo, pressione collettore sulla maggior parte delle centraline) vengono spuntati alla connessione. |
| Nome | Nome del parametro nella lingua scelta nelle [Opzioni](options.md). Passate con il mouse su un nome tagliato per leggerlo per intero. |
| **Valore** | Ultimo valore letto. **#ERR** in rosso: il valore non è stato decodificato. I valori di stato mostrano **SÌ** / **NO**. |
| **Unità** | giri/min, ms, °, hPa, °C, V, passi… |

**Seleziona tutti** e **Deseleziona tutti** spuntano o tolgono la spunta a tutta la colonna **Leggi**.

Su uno schermo largo i gruppi si dispongono su due o tre colonne; il titolo del gruppo fa anche da intestazione della colonna.

### Quanto spesso si aggiornano i valori?

La centralina risponde un byte per richiesta a 7680 baud, quindi ogni valore costa qualche millisecondo. La frequenza reale è a destra nella barra di stato (per esempio *6,8 aggiornamenti/s*). Per renderla più alta:

- togliete la spunta in **Leggi** a ciò che non serve (spie di stato e autoadattivi cambiano di rado);
- togliete la spunta a **Leggi errori** nella scheda [Errori](errors.md) quando non state guardando gli errori;
- impostate il latency timer FTDI a 1 ms ([Hardware](../../hardware.md)).

Gli ID dei parametri (`PERIODE`, `MT_EAU_L`…) e il significato di ciascun valore sono nel [Riferimento centraline](ecu-reference.md) e nel [Glossario](glossary.md).

## Registrare un log CSV

1. Connettetevi e spuntate in **Leggi** i parametri da registrare.
2. Fate clic su **Registra CSV** (qui o nella scheda Grafici). La barra di stato mostra *Registrazione in …* con il percorso del file.
3. Guidate, o eseguite la prova che volete documentare.
4. Fate clic su **Ferma registrazione**. La barra di stato mostra *Salvato …*.

Note:

- Il file si chiama `iaw_<data>_<ora>.csv` e va nella cartella dei log impostata nelle [Opzioni](options.md#file-e-cartelle) (di default `Documenti/marelli-iaw-ecu-studio/logs`).
- Le colonne sono i parametri spuntati in **Leggi** quando inizia la registrazione. Durante la registrazione continuano a essere letti anche se togliete la spunta, così il file non ripete mai valori vecchi.
- La registrazione si ferma da sola quando vi disconnettete.
- **Registra CSV** è disponibile solo da connessi e con almeno un parametro spuntato.

### Il formato CSV

Il file si apre con Excel, LibreOffice Calc o qualsiasi editor di testo:

```text
# marelli-iaw-ecu-studio log v1
# ecu=IAW-6F/16F
# iso=55D085029440
# columns=PERIODE;MT_EAU_L
# units=rpm;°C
Time [ms];Giri motore [rpm];Temperatura acqua motore [°C]
0;850;88
```

- Le righe che iniziano con `#` dicono da quale centralina e da quale auto viene il log, così si può [riprodurre](log-replay.md) in seguito.
- Le colonne sono separate da `;` come in IES2, e i numeri usano sempre il punto come separatore decimale, qualunque sia la lingua. In un foglio di calcolo, all'importazione scegliete `;` come separatore e `.` come separatore decimale.
- **Time [ms]** conta i millisecondi dall'inizio della registrazione.

Per rivedere una registrazione apritela in [Riproduzione log](log-replay.md).
