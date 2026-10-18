# Riferimento centraline

[← Manuale](README.md) · Precedente: [Riga di comando](command-line.md) · Successivo: [Glossario →](glossary.md)

Cosa offre ogni famiglia di centraline supportata: parametri in tempo reale, errori, test attuatori e regolazioni, con i nomi che mostra il programma. Questa pagina è generata dai file di definizione con `tools/generate-manual-reference/generate_ecu_reference.py`; non modificarla a mano.

## Famiglie

- [Magneti-Marelli IAW-6F/16F SPI](#magneti-marelli-iaw-6f16f-spi)
- [Magneti-Marelli IAW-8F/18F MPI](#magneti-marelli-iaw-8f18f-mpi)
- [Magneti-Marelli IAW-8F.68 MPI (Alfa 33)](#magneti-marelli-iaw-8f68-mpi-alfa-33)
- [Magneti-Marelli IAW-18FD/1G7 MPI](#magneti-marelli-iaw-18fd1g7-mpi)
- [Weber-Marelli IAW-04K.P8 MPI](#weber-marelli-iaw-04kp8-mpi)
- [FIAT CODE Immobiliser](#fiat-code-immobiliser)

## Magneti-Marelli IAW-6F/16F SPI

- **Tecnologia**: Iniezione single-point (monoiniettore sul corpo farfallato) per motori di piccola cilindrata
- **Periodo**: 1993–2000
- **Veicoli riconosciuti dal codice ISO**: 18

### Parametri in tempo reale

| ID | Nome | Gruppo del cruscotto | Mostrato come |
| --- | --- | --- | --- |
| `PERIODE` | Giri motore | Motore | strumento circolare |
| `T_INJ_AP` | Tempo di iniezione | Motore | barra |
| `AVANCE` | Anticipo accensione | Motore | barra |
| `MP2_MP8` | Pressione assoluta collettore | Motore | strumento circolare |
| `MT_AIR_L` | Temperatura aria aspirata | Temperature | barra |
| `MT_EAU_L` | Temperatura acqua motore | Temperature | strumento circolare |
| `ANG_PAP0` | Angolo farfalla | Farfalla e minimo | barra |
| `M_VBATT` | Tensione batteria | Impianto elettrico | strumento circolare |
| `K_O2` | Correzione lambda | Miscela e sonda lambda | barra |
| `ALFAR` | Posizione stepper minimo | Farfalla e minimo | barra |
| `INTEGR` | Guadagno integrale stepper | Farfalla e minimo | valore |
| `PROP` | Guadagno proporzionale stepper | Farfalla e minimo | valore |
| `TRIMRAM` | Trimmer CO | Autoadattivi | barra |
| `ALFAU` | Autoadattivo minimo | Autoadattivi | valore |
| `ALFAUC` | Autoadattivo minimo con clima inserito | Autoadattivi | valore |
| `CONS_REG` | Regime minimo obiettivo | Farfalla e minimo | valore |
| `OFNNTR` | Offset regime minimo | Farfalla e minimo | valore |
| `DELREG` | Correzione stepper (delta) | Farfalla e minimo | valore |
| `ALFAFTR` | Correzione stepper da FLT | Farfalla e minimo | valore |
| `ADOFFSET` | Autoadattivo miscela: offset al minimo, canister chiuso | Autoadattivi | valore |
| `ADOFFPURG` | Autoadattivo miscela: offset al minimo, canister aperto | Autoadattivi | valore |
| `ADOFFPL` | Autoadattivo miscela: offset ai carichi parziali | Autoadattivi | valore |
| `ADGAIN` | Autoadattivo miscela: guadagno a carico medio | Autoadattivi | valore |
| `S2` | Motore in moto | Stato | spia |
| `S3` | Segnali sensori OK | Stato | spia |
| `S4` | Farfalla a fine corsa (min/max) | Stato | spia |
| `S5` | Lambda in anello chiuso | Stato | spia |
| `S6` | Clima inserito | Stato | spia |
| `S7` | Autoadattivo miscela abilitato | Stato | spia |
| `S23` | Autoadattivo minimo abilitato | Stato | spia |
| `S27` | Power latch OK | Stato | spia |

### Errori

- **Motore** (24): Potenziometro farfalla, Sensore pressione assoluta (MAP), Sonda lambda, Sensore temperatura acqua, Sensore temperatura aria, Tensione batteria, Regolazione minimo, Errore ingresso sconosciuto (8), Iniettore, Bobina accensione 1, Bobina accensione 2, Stepper minimo, Elettrovalvola canister, Climatizzatore, Pompa benzina, Relè principale, Parametri autoadattivi, RAM, ROM, EEPROM, CPU, Sensore giri/PMS, Errore funzionale sconosciuto (7), Errore funzionale sconosciuto (8)
- **Immobilizzatore** (8): Centralina non sincronizzata con il CODE, Avviamento inibito dal CODE, Ricevuto il codice universale, Errore CODE sconosciuto (4), Backdoor attivata, Errore codice chiave, Codice chiave errato o non riconosciuto, Codice non ricevuto o linea interrotta

### Test attuatori

| Nome | Durata massima | Condizione |
| --- | --- | --- |
| Relè pompa benzina | 30 s | chiave su MAR, motore spento |
| Iniettore | 5 s | chiave su MAR, motore spento |
| Bobina 1 | 5 s | chiave su MAR, motore spento |
| Bobina 2 | 5 s | chiave su MAR, motore spento |
| Elettrovalvola canister | 7 s | chiave su MAR, motore spento |
| Contagiri | 2 s | chiave su MAR, motore spento |
| Relè compressore clima | 30 s | chiave su MAR, motore spento |
| Relè principale | 30 s | chiave su MAR, motore spento |

Cancellazione errori: al massimo 10 s.

### Regolazioni

| Nome | Tipo |
| --- | --- |
| Abilita/disabilita autoadattivo miscela | abilita/disabilita |
| Abilita/disabilita autoadattivo minimo | abilita/disabilita |
| Azzeramento autoadattivi miscela | abilita/disabilita |
| Azzeramento autoadattivi minimo | abilita/disabilita |
| Imposta trimmer CO (TRIMRAM) | nuovo valore |
| Imposta correzione stepper (ALFAFTR) | nuovo valore |

## Magneti-Marelli IAW-8F/18F MPI

- **Tecnologia**: Iniezione multipoint (MPI) sequenziale fasata, comune sui motori Fire e boxer
- **Periodo**: 1993–1999
- **Veicoli riconosciuti dal codice ISO**: 7

### Parametri in tempo reale

| ID | Nome | Gruppo del cruscotto | Mostrato come |
| --- | --- | --- | --- |
| `PERIODE` | Giri motore | Motore | strumento circolare |
| `T_INJ_AP` | Tempo di iniezione | Motore | barra |
| `AVANCE` | Anticipo accensione | Motore | barra |
| `MP2_MP8` | Pressione assoluta collettore | Motore | strumento circolare |
| `MT_AIR_L` | Temperatura aria aspirata | Temperature | barra |
| `MT_EAU_L` | Temperatura acqua motore | Temperature | strumento circolare |
| `ANG_PAP0` | Angolo farfalla | Farfalla e minimo | barra |
| `M_VBATT` | Tensione batteria | Impianto elettrico | strumento circolare |
| `K_O2` | Correzione lambda | Miscela e sonda lambda | barra |
| `ALFAR` | Posizione stepper minimo | Farfalla e minimo | barra |
| `INTEGR` | Guadagno integrale stepper | Farfalla e minimo | valore |
| `PROP` | Guadagno proporzionale stepper | Farfalla e minimo | valore |
| `TRIMRAM` | Trimmer CO | Autoadattivi | barra |
| `CONS_REG` | Regime minimo obiettivo | Farfalla e minimo | valore |
| `OFNNTR` | Offset regime minimo | Farfalla e minimo | valore |
| `S2` | Motore in moto | Stato | spia |
| `S3` | Segnali sensori OK | Stato | spia |
| `S4` | Farfalla a fine corsa (min/max) | Stato | spia |
| `S5` | Lambda in anello chiuso | Stato | spia |
| `S6` | Clima inserito | Stato | spia |
| `S7` | Autoadattivo miscela abilitato | Stato | spia |
| `S25` | Anticipo ridotto per battito | Stato | spia |
| `S26` | Autoadattivo minimo abilitato | Stato | spia |
| `S27` | Marcia inserita | Stato | spia |

### Errori

- **Motore** (32): Potenziometro farfalla, Sensore pressione assoluta (MAP), Sonda lambda, Sensore temperatura acqua, Sensore temperatura aria, Tensione batteria, Regolazione minimo, Sensore di battito, Iniettori, Bobina accensione 1, Bobina accensione 2, Stepper minimo, Elettrovalvola canister, Climatizzatore, Pompa benzina, Spia avaria motore (MIL), Errore uscita sconosciuto (1a), Errore uscita sconosciuto (2a), Errore uscita sconosciuto (3a), Errore uscita sconosciuto (4a), Titolo miscela, Wastegate, Econometro, Comando EGR, Parametri autoadattivi, ROM, RAM, EEPROM, CPU, Sensore giri/PMS, Cambio automatico, Valvola EGR
- **Immobilizzatore** (8): Centralina non sincronizzata con il CODE, Avviamento inibito dal CODE, Ricevuto il codice universale, Errore CODE sconosciuto (4), Backdoor attivata, Errore codice chiave, Codice chiave errato o non riconosciuto, Codice non ricevuto o linea interrotta

### Test attuatori

| Nome | Durata massima | Condizione |
| --- | --- | --- |
| Relè pompa benzina | 30 s | chiave su MAR, motore spento |
| Iniettori | 5 s | chiave su MAR, motore spento |
| Bobina 1 | 5 s | chiave su MAR, motore spento |
| Bobina 2 | 5 s | chiave su MAR, motore spento |
| Elettrovalvola canister | 7 s | chiave su MAR, motore spento |
| Contagiri | 2 s | chiave su MAR, motore spento |
| Relè compressore clima | 30 s | chiave su MAR, motore spento |
| Spia avaria motore (MIL) | 30 s | chiave su MAR, motore spento |
| Valvola EGR | 30 s | chiave su MAR, motore spento |
| Econometro | 30 s | chiave su MAR, motore spento |

Cancellazione errori: al massimo 10 s.

### Regolazioni

| Nome | Tipo |
| --- | --- |
| Abilita/disabilita autoadattivo miscela | abilita/disabilita |
| Abilita/disabilita autoadattivo minimo | abilita/disabilita |
| Azzeramento autoadattivi miscela | abilita/disabilita |
| Azzeramento autoadattivi minimo | abilita/disabilita |

## Magneti-Marelli IAW-8F.68 MPI (Alfa 33)

- **Tecnologia**: Iniezione multipoint tarata per i motori boxer Alfa Romeo
- **Periodo**: 1992–1995
- **Veicoli riconosciuti dal codice ISO**: 1

### Parametri in tempo reale

| ID | Nome | Gruppo del cruscotto | Mostrato come |
| --- | --- | --- | --- |
| `PERIODE` | Giri motore | Motore | strumento circolare |
| `T_INJ_AP` | Tempo di iniezione | Motore | barra |
| `AVANCE` | Anticipo accensione | Motore | barra |
| `MP2_MP8` | Pressione assoluta collettore | Motore | strumento circolare |
| `MT_AIR_L` | Temperatura aria aspirata | Temperature | barra |
| `MT_EAU_L` | Temperatura acqua motore | Temperature | strumento circolare |
| `ANG_PAP0` | Angolo farfalla | Farfalla e minimo | barra |
| `M_VBATT` | Tensione batteria | Impianto elettrico | strumento circolare |
| `K_O2` | Correzione lambda | Miscela e sonda lambda | barra |
| `ALFAR` | Posizione stepper minimo | Farfalla e minimo | barra |
| `INTEGR` | Guadagno integrale stepper | Farfalla e minimo | valore |
| `PROP` | Guadagno proporzionale stepper | Farfalla e minimo | valore |
| `TRIMRAM` | Trimmer CO | Autoadattivi | barra |
| `ALFAU` | Autoadattivo minimo | Autoadattivi | valore |
| `ALFAUC` | Autoadattivo minimo con clima inserito | Autoadattivi | valore |
| `CONS_REG` | Regime minimo obiettivo | Farfalla e minimo | valore |
| `OFNNTR` | Offset regime minimo | Farfalla e minimo | valore |
| `DELREG` | Correzione stepper (delta) | Farfalla e minimo | valore |
| `ALFAFTR` | Correzione stepper da FLT | Farfalla e minimo | valore |
| `ADOFFSET` | Autoadattivo miscela: offset al minimo, canister chiuso | Autoadattivi | valore |
| `ADOFFPURG` | Autoadattivo miscela: offset al minimo, canister aperto | Autoadattivi | valore |
| `ADOFFPL` | Autoadattivo miscela: offset ai carichi parziali | Autoadattivi | valore |
| `ADGAIN` | Autoadattivo miscela: guadagno a carico medio | Autoadattivi | valore |
| `S2` | Motore in moto | Stato | spia |
| `S3` | Segnali sensori OK | Stato | spia |
| `S4` | Farfalla a fine corsa (min/max) | Stato | spia |
| `S5` | Lambda in anello chiuso | Stato | spia |
| `S6` | Clima inserito | Stato | spia |
| `S7` | Autoadattivo miscela abilitato | Stato | spia |

### Errori

- **Motore** (24): Potenziometro farfalla, Sensore pressione assoluta (MAP), Sonda lambda, Sensore temperatura acqua, Sensore temperatura aria, Tensione batteria, Regolazione minimo, Errore ingresso sconosciuto (8), Iniettore, Bobina accensione 1, Bobina accensione 2, Stepper minimo, Elettrovalvola canister, Climatizzatore, Pompa benzina, Relè principale, Parametri autoadattivi, RAM, ROM, EEPROM, CPU, Sensore giri/PMS, Errore funzionale sconosciuto (7), Errore funzionale sconosciuto (8)

### Test attuatori

| Nome | Durata massima | Condizione |
| --- | --- | --- |
| Relè pompa benzina | 30 s | chiave su MAR, motore spento |
| Iniettori | 5 s | chiave su MAR, motore spento |
| Bobina 1 | 5 s | chiave su MAR, motore spento |
| Bobina 2 | 5 s | chiave su MAR, motore spento |
| Elettrovalvola canister | 7 s | chiave su MAR, motore spento |
| Contagiri | 2 s | chiave su MAR, motore spento |
| Relè compressore clima | 30 s | chiave su MAR, motore spento |
| Relè principale | 30 s | chiave su MAR, motore spento |

Cancellazione errori: al massimo 10 s.

### Regolazioni

| Nome | Tipo |
| --- | --- |
| Abilita/disabilita autoadattivo miscela | abilita/disabilita |
| Abilita/disabilita autoadattivo minimo | abilita/disabilita |
| Azzeramento autoadattivi miscela | abilita/disabilita |
| Azzeramento autoadattivi minimo | abilita/disabilita |

## Magneti-Marelli IAW-18FD/1G7 MPI

- **Tecnologia**: Iniezione elettronica multipoint (MPI) con accensione statica
- **Periodo**: 1995–2001
- **Veicoli riconosciuti dal codice ISO**: 3

### Parametri in tempo reale

| ID | Nome | Gruppo del cruscotto | Mostrato come |
| --- | --- | --- | --- |
| `PERIODE` | Giri motore | Motore | strumento circolare |
| `T_INJ_AP` | Tempo di iniezione | Motore | barra |
| `AVANCE` | Anticipo accensione | Motore | barra |
| `MP2_MP8` | Pressione assoluta collettore | Motore | strumento circolare |
| `MT_AIR_L` | Temperatura aria aspirata | Temperature | barra |
| `MT_EAU_L` | Temperatura acqua motore | Temperature | strumento circolare |
| `ANG_PAP0` | Angolo farfalla | Farfalla e minimo | barra |
| `M_VBATT` | Tensione batteria | Impianto elettrico | strumento circolare |
| `K_O2` | Correzione lambda | Miscela e sonda lambda | barra |
| `K_O2_RAW` | Tensione sonda lambda | Miscela e sonda lambda | barra |
| `ALFAR` | Posizione stepper minimo | Farfalla e minimo | barra |
| `INTEGR` | Guadagno integrale stepper | Farfalla e minimo | valore |
| `PROP` | Guadagno proporzionale stepper | Farfalla e minimo | valore |
| `TRIMRAM` | Trimmer CO | Autoadattivi | barra |
| `ALFAU` | Autoadattivo minimo | Autoadattivi | valore |
| `ALFAUC` | Autoadattivo minimo con clima inserito | Autoadattivi | valore |
| `CONS_REG` | Regime minimo obiettivo | Farfalla e minimo | valore |
| `OFNNTR` | Offset regime minimo | Farfalla e minimo | valore |
| `OFFSET1` | Autoadattivo miscela, zona 1 | Autoadattivi | barra |
| `OFFSET2` | Autoadattivo miscela, zona 2 | Autoadattivi | barra |
| `OFFSET3` | Autoadattivo miscela, zona 3 | Autoadattivi | barra |
| `OFFSET4` | Autoadattivo miscela, zona 4 | Autoadattivi | barra |
| `OFFSET5` | Autoadattivo miscela, zona 5 | Autoadattivi | barra |
| `OFFSET6` | Autoadattivo miscela, zona 6 | Autoadattivi | barra |
| `OFFSETm` | Autoadattivo miscela, minimo | Autoadattivi | barra |
| `S2` | Motore in moto | Stato | spia |
| `S3` | Segnali sensori OK | Stato | spia |
| `S4` | Farfalla a fine corsa (min/max) | Stato | spia |
| `S5` | Lambda in anello chiuso | Stato | spia |
| `S6` | Clima inserito | Stato | spia |
| `S7` | Autoadattivo miscela abilitato | Stato | spia |
| `S25` | Anticipo ridotto per battito | Stato | spia |
| `S26` | Autoadattivo minimo abilitato | Stato | spia |
| `S27` | Marcia inserita | Stato | spia |

### Errori

- **Motore** (32): Potenziometro farfalla, Errore ingresso sconosciuto (2), Sonda lambda, Sensore temperatura acqua, Sensore temperatura aria, Tensione batteria, Regolazione minimo, Sensore di battito, Iniettori (gruppo 1), Bobina accensione 1, Bobina accensione 2, Stepper minimo, Elettrovalvola canister, Climatizzatore, Pompa benzina, Spia avaria motore (MIL), Errore uscita sconosciuto (1a), Errore uscita sconosciuto (2a), Errore uscita sconosciuto (3a), Iniettori (gruppo 2), Titolo miscela, Wastegate, Econometro, Sensore pressione assoluta (MAP), Parametri autoadattivi, RAM, ROM, EEPROM, CPU, Sensore giri/PMS, Cambio automatico, Valvola EGR
- **Immobilizzatore** (8): Centralina non sincronizzata con il CODE, Avviamento inibito dal CODE, Ricevuto il codice universale, Errore CODE sconosciuto (4), Backdoor attivata, Errore codice chiave, Codice chiave errato o non riconosciuto, Codice non ricevuto o linea interrotta

### Test attuatori

| Nome | Durata massima | Condizione |
| --- | --- | --- |
| Relè pompa benzina | 30 s | chiave su MAR, motore spento |
| Iniettori | 10 s | chiave su MAR, motore spento |
| Bobina 1 | 5 s | chiave su MAR, motore spento |
| Bobina 2 | 5 s | chiave su MAR, motore spento |
| Elettrovalvola canister | 7 s | chiave su MAR, motore spento |
| Contagiri | 2 s | chiave su MAR, motore spento |
| Relè compressore clima | 30 s | chiave su MAR, motore spento |
| Spia avaria motore (MIL) | 30 s | chiave su MAR, motore spento |
| Valvola EGR | 30 s | chiave su MAR, motore spento |
| Econometro | 30 s | chiave su MAR, motore spento |

Cancellazione errori: al massimo 10 s.

### Regolazioni

| Nome | Tipo |
| --- | --- |
| Abilita/disabilita autoadattivo miscela | abilita/disabilita |
| Abilita/disabilita autoadattivo minimo | abilita/disabilita |
| Azzeramento autoadattivi miscela | abilita/disabilita |
| Azzeramento autoadattivi minimo | abilita/disabilita |

## Weber-Marelli IAW-04K.P8 MPI

- **Tecnologia**: Centralina ad alte prestazioni per i motori turbo e plurivalvole delle sportive anni '90
- **Periodo**: 1989–1996
- **Nota anche come**: IAW 4WF.P8
- **Veicoli riconosciuti dal codice ISO**: 17

### Parametri in tempo reale

| ID | Nome | Gruppo del cruscotto | Mostrato come |
| --- | --- | --- | --- |
| `PERIODE` | Giri motore | Motore | strumento circolare |
| `T_INJ_AP` | Tempo di iniezione | Motore | barra |
| `AVANCE` | Anticipo accensione | Motore | barra |
| `MP2_MP8` | Pressione assoluta collettore | Motore | strumento circolare |
| `MP2_MP8t` | Pressione assoluta collettore (turbo) | Motore | strumento circolare |
| `MT_AIR_L` | Temperatura aria aspirata | Temperature | barra |
| `MT_EAU_L` | Temperatura acqua motore | Temperature | strumento circolare |
| `ANG_PAP0` | Angolo farfalla | Farfalla e minimo | barra |
| `M_VBATT` | Tensione batteria | Impianto elettrico | strumento circolare |
| `ALFAR` | Posizione stepper minimo | Farfalla e minimo | barra |
| `TRIMRAM` | Trimmer CO | Autoadattivi | barra |
| `INJ_AVA` | Fase di iniezione | Motore | valore |
| `T_VAE` | Duty cycle VAE (minimo) | Farfalla e minimo | barra |
| `K_O2` | Correzione lambda | Miscela e sonda lambda | barra |
| `FLGSMA` | Lambda in anello chiuso | Stato | spia |
| `FABEPAT` | Autoadattivo miscela attivo (EPROM) | Stato | spia |
| `FABEEAT` | Autoadattivo miscela attivo (EEPROM) | Stato | spia |
| `FABEPATi` | Autoadattivo minimo attivo (EPROM) | Stato | spia |
| `FABEEATi` | Autoadattivo minimo attivo (EEPROM) | Stato | spia |
| `UR2` | Anticipo ridotto per battito | Stato | spia |
| `UR3` | Quadro strumenti (spie) OK | Stato | spia |
| `UR4` | Farfalla a fine corsa (min/max) | Stato | spia |
| `UR5` | ABS attivo | Stato | spia |
| `UR6` | Clima inserito | Stato | spia |
| `UR7` | Marcia inserita | Stato | spia |
| `UR8` | Autopulizia VAE in corso | Stato | spia |

### Errori

- **Motore** (16): VAE bloccata aperta o chiusa, Titolo miscela, Sensore temperatura aria, Sensore di battito, Sensore temperatura acqua, Sonda lambda, Sensore pressione assoluta (MAP), Potenziometro farfalla, Sensore di fase, Sequenza quadro strumenti (spie), EPROM, EEPROM, Segnale folle/marcia inserita, Segnale Digimatic, RAM, Motore passo-passo (SMOT)

### Test attuatori

| Nome | Durata massima | Condizione |
| --- | --- | --- |
| Relè pompa benzina | 30 s | chiave su MAR, motore spento |
| Iniettori | 5 s | chiave su MAR, motore spento |
| Iniettore 1 | 5 s | chiave su MAR, motore spento |
| Iniettore 2 | 5 s | chiave su MAR, motore spento |
| Iniettore 3 | 5 s | chiave su MAR, motore spento |
| Iniettore 4 | 5 s | chiave su MAR, motore spento |
| Bobina 1 | 5 s | chiave su MAR, motore spento |
| Bobina 2 | 5 s | chiave su MAR, motore spento |
| Valvola minimo (VAE) | 5 s | chiave su MAR, motore spento |
| Overboost | 5 s | chiave su MAR, motore spento |
| Elettrovalvola canister | 5 s | chiave su MAR, motore spento |
| Contagiri | 2 s | chiave su MAR, motore spento |
| Econometro | 10 s | chiave su MAR, motore spento |

Cancellazione errori: al massimo 10 s.

### Regolazioni

Nessuna.

## FIAT CODE Immobiliser

- **Tecnologia**: Immobilizzatore: non controlla l'iniezione; dialoga con il transponder della chiave per consentire l'avviamento
- **Periodo**: 1995–
- **Veicoli riconosciuti dal codice ISO**: 43

### Parametri in tempo reale

| ID | Nome | Gruppo del cruscotto | Mostrato come |
| --- | --- | --- | --- |
| `CRDVAS` | Contatore errori | Altro | valore |

### Errori

- **Immobilizzatore** (8): Centralina non sincronizzata con il CODE, Avviamento inibito dal CODE, Ricevuto il codice universale, Errore CODE sconosciuto (4), Backdoor attivata, Errore codice chiave, Codice chiave errato o non riconosciuto, Codice non ricevuto o linea interrotta

### Test attuatori

Nessuna.

Cancellazione errori: al massimo 10 s.

### Regolazioni

Nessuna.
