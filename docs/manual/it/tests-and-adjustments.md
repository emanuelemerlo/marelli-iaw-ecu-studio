# Test attuatori e regolazioni

[← Manuale](README.md) · Precedente: [Errori](errors.md) · Successivo: [Grafici →](graphs.md)

Queste due schede inviano **comandi attivi**: la centralina comanda un componente reale, o cambia un valore nella sua memoria. Leggete questa pagina prima di usarle.

## Sicurezza

- Lavorate con l'auto **ferma**, freno a mano tirato, cambio in folle, in un luogo ventilato, lontano da fiamme e vapori di benzina.
- Tenete **chiave su MAR e motore spento**, salvo diversa indicazione nella colonna *Condizione*. Tutti i test delle famiglie integrate si fanno a motore spento.
- **I test di pompa benzina e iniettori erogano carburante.** Non eseguiteli mai con tubazioni, iniettori o flauto staccati. **I test delle bobine producono alta tensione**: state lontani dai cavi candela.
- Ogni comando chiede conferma. Per disabilitarli del tutto togliete la spunta a **Abilita comandi attivi** nelle [Opzioni](options.md); i pulsanti mostrano allora solo un messaggio.
- Le famiglie di centraline non segnate come verificate non eseguono mai comandi attivi.
- Il programma non è ancora stato provato su un'auto vera: lasciate i comandi attivi per ultimi, dopo aver visto che lettura dei parametri ed errori funzionano.

Durante un comando attivo la barra di stato dice *Comando in corso...*, le letture in tempo reale si fermano e gli altri pulsanti dei comandi sono disabilitati.

## Test attuatori

Ogni riga è un componente che la centralina può comandare:

| Colonna | Significato |
| --- | --- |
| **Nome** | Il componente: relè pompa benzina, iniettori (tutti o uno alla volta), bobine, valvola minimo (VAE), elettrovalvola canister, relè compressore clima, relè principale, spia avaria motore (MIL), valvola EGR, contagiri, econometro, overboost… |
| **Condizione** | Come deve essere l'auto: *chiave su MAR, motore spento* oppure *motore in moto*. |
| **Durata massima** | Per quanto tempo al massimo la centralina comanda il componente, per esempio 30 s per la pompa, 5 s per un iniettore, 2 s per il contagiri. |
| **Esito** | Il risultato dell'ultima esecuzione. |

Per eseguire un test:

1. Rispettate la condizione (chiave su MAR, motore spento).
2. Fate clic su **Esegui** nella riga.
3. La conferma dice cosa verrà comandato, per quanto tempo e con quale condizione. Confermate.
4. Ascoltate o guardate: la pompa ronza, un iniettore ticchetta, la lancetta del contagiri si muove, la spia MIL lampeggia…
5. La colonna **Esito** mostra il risultato.

| Esito | Significato |
| --- | --- |
| **OK** | La centralina ha eseguito il test e ha segnalato che è andato a buon fine. |
| **NON RIUSCITO** | La centralina ha eseguito il test e ha segnalato un guasto, oppure non ha accettato il comando. |
| **TIMEOUT** | La centralina non ha segnalato la fine del test entro la durata massima. |
| **INIZIALIZZAZIONE NON RIUSCITA** | La centralina non è entrata in modo attivo, oppure non ha restituito il comando. |
| **ANNULLATO** | Il test è stato interrotto (per esempio disconnettendosi). |

I test disponibili dipendono dalla centralina: vedi il [Riferimento centraline](ecu-reference.md). La IAW-04K.P8, per esempio, comanda ogni iniettore e la valvola di overboost; la IAW-18FD/1G7 la valvola EGR.

## Regolazioni

Le regolazioni cambiano impostazioni memorizzate nella centralina. La colonna **Tipo** dice di che genere è ciascuna:

| Tipo | Cosa succede facendo clic su **Applica** |
| --- | --- |
| **abilita/disabilita** | Dopo una conferma la centralina inserisce o disinserisce la funzione, oppure esegue l'azzeramento. |
| **nuovo valore** | Una finestra mostra il valore attuale con un cursore e un campo numerico (0–255): scegliete il nuovo valore e fate clic su **Applica**, oppure su **Annulla**. |
| **codice** | Invia un codice fisso, dopo una conferma. |

Le regolazioni delle famiglie integrate:

- **Abilita/disabilita autoadattivo miscela** / **Abilita/disabilita autoadattivo minimo** — inseriscono o disinseriscono l'autoadattivo. Le spie della sezione **Stato** nel cruscotto (*Autoadattivo miscela abilitato*, *Autoadattivo minimo abilitato*) mostrano lo stato attuale. Disinseritelo per regolare un'auto a mano senza che la centralina vi corregga; reinseritelo dopo.
- **Azzeramento autoadattivi miscela** / **Azzeramento autoadattivi minimo** — cancellano quello che la centralina ha imparato. Utile dopo aver sostituito sonda lambda, iniettore, stepper o corpo farfallato, o dopo una pulizia. Dopo l'azzeramento lasciate il motore al minimo finché non è caldo, poi guidate normalmente per qualche chilometro perché la centralina impari di nuovo.
- **Imposta trimmer CO (TRIMRAM)** e **Imposta correzione stepper (ALFAFTR)** — solo sulla IAW-6F/16F: un nuovo valore da 0 a 255. Annotate il valore attuale prima di cambiarlo, per poter tornare indietro. Sui motori single-point il trimmer CO regola la miscela al minimo, e va controllato con l'analizzatore dei gas di scarico.

La colonna **Esito** mostra il risultato come per i test.

I nomi esatti e l'elenco per ogni centralina sono nel [Riferimento centraline](ecu-reference.md); cosa significano *autoadattivo*, *trimmer CO* e *stepper* è nel [Glossario](glossary.md).
