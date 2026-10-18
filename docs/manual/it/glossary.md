# Glossario

[← Manuale](README.md) · Precedente: [Riferimento centraline](ecu-reference.md)

| Termine | Significato |
| --- | --- |
| **Anello chiuso** | La centralina corregge la miscela in base alla sonda lambda. In anello aperto (motore freddo, pieno carico) usa solo le sue mappe. |
| **Attuatore** | Un componente comandato dalla centralina: iniettore, bobina, relè pompa benzina, stepper del minimo, elettrovalvola canister… |
| **Autoadattivo** | Quello che la centralina impara col tempo per compensare usura e tolleranze: offset della miscela per zona di carico, posizione del minimo. Si può disinserire e azzerare nelle [Regolazioni](tests-and-adjustments.md#regolazioni). In IES2 si chiamava *autocalibration* (AC). |
| **Battito (detonazione)** | Combustione anomala nei cilindri. Quando la rileva, la centralina riduce l'anticipo (spia arancione nel cruscotto). |
| **Bridge di rete (RFC 2217)** | Protocollo di rete che trasporta una porta seriale, compresi i cambi di velocità, su TCP. Permette di lasciare il cavo in auto collegato a un Raspberry Pi. |
| **Canister (EVAP)** | Filtro a carboni attivi che trattiene i vapori di benzina del serbatoio. La centralina lo spurga nell'aspirazione tramite l'elettrovalvola canister; alcuni autoadattivi cambiano con il canister chiuso o aperto. |
| **Cavo KKL** | Cavo USB–K-Line ("VAG-COM KKL 409.1"), l'interfaccia che serve a questo programma. |
| **Chiave su MAR** | Quadro acceso (posizione di marcia). *STOP* è spento, *AVV* è l'avviamento. |
| **Codice ISO** | Codice che identifica software e calibrazione della centralina (sei byte, mostrati come `55-D0-85-02-94-40`). Determina famiglia e veicolo. Alcune centraline lo inviano da sole quando si porta la chiave su MAR. |
| **CODRIC** | Codice hardware della centralina. Serve a riconoscere la famiglia e a controllare che dopo una riconnessione risponda la stessa centralina. |
| **Comando attivo** | Una richiesta che fa fare qualcosa alla centralina (test attuatore, cancellazione errori, regolazione) invece di farle solo restituire un valore. La centralina lo accetta solo in modo attivo. |
| **Duty cycle** | Percentuale di tempo in cui una valvola è aperta, per esempio la valvola minimo VAE. |
| **ECOL** | Suffisso mostrato dopo il nome della centralina quando l'auto non ha immobilizzatore, come in IES2. |
| **Eco** | Il cavo KKL restituisce ogni byte che trasmette. Il programma usa l'eco per distinguere un cavo guasto da una centralina che non risponde. |
| **EGR** | Valvola di ricircolo dei gas di scarico. |
| **EOBD / OBD-II** | Diagnosi di bordo standard delle auto dal 2001 circa, sulla presa a 16 pin. Le centraline IAW degli anni '90 sono precedenti (OBD-I). |
| **FIAT CODE** | L'immobilizzatore di FIAT, Lancia e Alfa Romeo: la centralina avvia il motore solo dopo aver riconosciuto il codice della chiave. |
| **IAW** | *Iniezione Accensione Weber*: la famiglia di centraline di iniezione e accensione Magneti Marelli (ex Weber). |
| **IES2** | *IAW Scan 2*, il programma Windows originale di Tomasz Orczyk che questo programma continua. |
| **K-Line (linea K)** | Il filo su cui centralina e cavo si parlano. |
| **Latency timer FTDI** | Quanto aspetta il chip USB del cavo prima di passare i dati al computer. Con 1 ms le letture sono molto più veloci che con i 16 ms predefiniti. |
| **Log della linea** | File di testo con ogni byte inviato e ricevuto sulla linea K, con i tempi. Indispensabile per segnalare problemi di comunicazione. |
| **MAP** | Pressione assoluta nel collettore di aspirazione, in hPa: il carico del motore. Circa 1000 hPa a motore spento, molto meno al minimo. |
| **MIL** | Spia avaria motore. |
| **Modalità compatibilità** | Tempi più lenti per adattatori che perdono byte, ereditata da IES2. |
| **MPI / SPI** | Iniezione multipoint (un iniettore per cilindro) / single-point (un solo iniettore sul corpo farfallato). |
| **Presa diagnosi FIAT 3 pin** | Connettore di diagnosi di queste auto: +12 V, massa, K-Line. |
| **Regime minimo obiettivo** | Il regime di minimo a cui punta la centralina (sale a motore freddo o con il clima inserito). |
| **Sonda lambda** | Sensore di ossigeno nello scarico. La *correzione lambda* è quanto la centralina modifica l'iniezione per tenere giusta la miscela (1 = nessuna correzione). |
| **Stepper** | Motorino passo-passo del minimo: apre in passi un bypass attorno alla farfalla per tenere il regime minimo obiettivo. |
| **Trimmer CO (TRIMRAM)** | Regolazione della miscela al minimo (CO) memorizzata nella centralina, in passi. Sulle auto più vecchie era una vite sul debimetro. |
| **VAE** | Valvola del minimo della IAW-04K.P8, comandata in duty cycle. |
| **Verificata** | Definizione segnata come controllata. Solo le definizioni verificate eseguono comandi attivi. |
