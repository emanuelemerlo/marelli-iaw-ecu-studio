# marelli-iaw-ecu-studio

Diagnosi per le centraline motore Magneti Marelli IAW delle FIAT, Lancia e Alfa Romeo degli anni '90 (OBD-I, connettore FIAT a 3 pin). Evoluzione multipiattaforma di [IES2 (IAW Scan 2)](https://github.com/TzOk83/IES2) di Tomasz Orczyk, la cui ultima versione (0.86) risale al 2020.

[Read in English](README.md)

> **Stato: sviluppo iniziale.** Il nucleo del protocollo, le definizioni delle centraline, il simulatore e lo strumento a riga di comando funzionano e sono verificati contro il codice originale di IES2. L'interfaccia grafica (Avalonia) non è ancora scritta; l'applicazione Windows originale è in `legacy/IES2`.

## Centraline supportate

| Famiglia | Vetture (esempi) |
| --- | --- |
| IAW-6F/16F SPI | Cinquecento, Seicento, Panda, Punto 55/60, Tipo/Tempra 1.6, Lancia Y 1.2 |
| IAW-8F/18F MPI | Punto 75 1.2, Palio 1.2, Delta/Dedra 1.8, Alfa 145/146 1.3 Boxer, Tipo/Tempra 1.8 |
| IAW-8F.68 MPI | Alfa 33 1.4 |
| IAW-18FD/1G7 MPI | Punto 1.2 16V, Siena 1.4, Palio 1.0 |
| Weber-Marelli IAW-04K.P8 MPI | Coupé 2.0, Delta Evoluzione, Dedra/Tempra 2.0, Tipo 2000 16V, Alfa 155 2.0 16V |
| Immobilizzatore FIAT CODE | tutte le precedenti, se presente |

`iaw-cli ecus` mostra l'elenco completo con i codici ISO. Le definizioni sono file JSON in [`src/MarelliIawEcuStudio.Core/Definitions`](src/MarelliIawEcuStudio.Core/Definitions); per correggerne o aggiungerne una vedi [docs/definitions.md](docs/definitions.md).

## Hardware

Un cavo USB VAG-COM "KKL 409.1" con chip FTDI originale, più un adattatore da OBD2 a FIAT 3 pin. Dettagli, permessi su Linux e latenza: [docs/hardware.md](docs/hardware.md).

Queste centraline **non** usano ISO 9141-2 né KWP2000, ma un protocollo FIAT con init a 1200 baud e richieste di un byte a 7680 baud. Gli adattatori ELM327 non possono gestirlo.

## Avvio rapido

Serve il [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build
dotnet test

# Prova senza auto: il simulatore integrato
dotnet run --project src/MarelliIawEcuStudio.Cli -- read --simulate --count 5 --lang it

# Con il cavo (esempio Linux; su Windows --port COM3)
dotnet run --project src/MarelliIawEcuStudio.Cli -- ports
dotnet run --project src/MarelliIawEcuStudio.Cli -- identify --port /dev/ttyUSB0
dotnet run --project src/MarelliIawEcuStudio.Cli -- read --port /dev/ttyUSB0 --params PERIODE,MT_EAU_L --count 0 --csv giro.csv
dotnet run --project src/MarelliIawEcuStudio.Cli -- errors --port /dev/ttyUSB0
```

Come IES2, lo strumento ascolta per 10 secondi il codice ISO che la centralina può inviare all'accensione: avvialo, poi gira la chiave. `--no-broadcast` salta l'attesa.

## Sicurezza

Test attuatori, cancellazione errori e regolazioni comandano componenti reali (pompa, iniettori, bobine). Lo strumento a riga di comando è **in sola lettura** senza `--allow-active`, e chiede conferma prima di ogni comando. Le definizioni non marcate `verified` non eseguono comandi attivi salvo autorizzazione esplicita. Uso a proprio rischio; vedi l'esclusione di garanzia nella licenza.

## Origine e licenza

Il progetto prosegue IES2 di Tomasz Orczyk e ne conserva tutta la storia. Il codice è rilasciato sotto [GNU GPL v3](LICENSE). Le parti derivate da IES2 restano anche sotto la sua licenza BSD 3-clausole ([LICENSE-IES2](LICENSE-IES2)); attribuzioni, traduttori e marchi in [NOTICE](NOTICE). Progetto non affiliato a Marelli, FIAT o ai loro successori.
