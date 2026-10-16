# ECU definition files

Each ECU family is described by one JSON file. The built-in ones live in `src/MarelliIawEcuStudio.Core/Definitions` and were generated from the IES2 source by `tools/generate-definitions/generate_from_ies2.py`; the `LegacyEquivalenceTests` check every table and every possible raw value against the original code.

To fix or extend a definition without rebuilding, copy it into your user folder:

| OS | Folder |
| --- | --- |
| Windows | `%APPDATA%\marelli-iaw-ecu-studio\definitions` |
| Linux, Raspberry Pi, macOS | `~/.config/marelli-iaw-ecu-studio/definitions` |

A file with the same `id` as a built-in definition replaces it. Broken files are skipped with a warning. Check a file with:

```sh
iaw-cli validate my-definition.json
```

Editors that understand JSON Schema (VS Code, Rider) validate and complete the file through its `$schema` line ([schema](../schemas/ecu-definition.schema.json)).

## Structure

```jsonc
{
  "id": "IAW-6F/16F",                 // unique; replaces a built-in definition with the same id
  "name": "IAW-6F/16F",
  "longName": "Magneti-Marelli IAW-6F/16F SPI",
  "verified": true,                   // false for new or community files: active commands stay disabled
  "protocol": { "identification": "standard" },   // or "p8" (IAW-04K.P8: no init, other identification requests)
  "detection": {
    "autoDetect": true,
    "order": 40,                                   // lower is tried first
    "isoMatches": [{ "offset": 2, "value": "D085" }],
    "codricMatches": [{ "offset": 0, "value": "61602" }]
  },
  "vehicles": [{ "iso": "55D085029440", "model": "Punto 55 1.1 SPI 5M/6M ECE F2" }],
  "parameters": [ ... ],
  "errors": { "engine": { ... }, "immobilizer": { ... } },
  "activeTests": [ ... ],
  "clearCodes": { "timeoutSeconds": 10, "requests": ["0x84"] },
  "adjustments": [ ... ]
}
```

Bytes can be written as numbers or as `"0x2A"` strings.

### Texts

Names, units and reasons use either a key into the translations (`nameKey`, `unitKey`, `formatKey`, `highReasonKey`...) or a literal (`name`, `unit`, `format`, `highReason`...). Keys come from the IES2 resources and are translated into ten languages; literals are shown as written.

### Parameters

```jsonc
{
  "id": "PERIODE",
  "nameKey": "PERIODE",                // "Engine Speed"
  "unitKey": "rpm",
  "format": "0",                       // .NET numeric format
  "requests": ["0x01", "0x02"],       // one or two request bytes
  "decode": { "raw": "u16", "expr": "x == 0 || x == 65535 ? 0 : 15000000 / x" }
}
```

`raw` tells how the request bytes become the integer `x`:

| raw | x |
| --- | --- |
| `u8` / `s8` | the byte, unsigned / two's complement |
| `u16` / `s16` | first byte × 256 + second byte, unsigned / two's complement |
| `bit` | bit `bit` (0-7) of the byte, as 1 or 0; `"invert": true` swaps them |

Then either `expr` (an expression in `x`) or `table` (a 256-entry lookup table in the file's `tables`) gives the value. Without both, the value is `x`.

Expressions use `+ - * /`, parentheses, comparisons (`< <= > >= == !=`), `&& || !`, `cond ? a : b`, and the functions `idiv(a, b)` (integer division), `min`, `max`, `abs`. They are evaluated in double precision in the same order as C#, so a formula copied from C# code gives identical results. Examples from the built-in files:

```
x - 40                                       coolant temperature (IAW-16F)
x * 0.4234 - 2.9638                          throttle angle
idiv(x * 100, 255)                           VAE duty cycle in % (IAW-04K)
x < 171 ? x * 0.1848 - 1.41 : x * 0.7058 - 90   throttle angle (IAW-04K, two slopes)
```

### Errors

Each error is a bit in three flag registers (`active`, `verified`, `stored`) and, optionally, a bit in three extension registers (`ext`) that chooses between `highReason` and `lowReason`. `scheme` selects the decoding used by the family:

| scheme | Family in IES2 | Reason taken from |
| --- | --- | --- |
| `storedFirst` | IAW-16F | stored extension if stored, else active extension |
| `activeVerifiedStored` | IAW-18F, 18FD | active, then verified, then stored extension |
| `activeVerifiedStoredNoVerifiedFlag` | IAW-04K | as above, but no "verified" state is reported |
| `noReason` | IAW-8F.68 | none |
| `fiatCode` | immobiliser | always "FIAT CODE" |

### Active tests and adjustments

```jsonc
{ "nameKey": "FuelPump", "engineRunning": false, "timeoutSeconds": 30, "requests": ["0x80"] }
```

```jsonc
{ "nameKey": "TrimSet", "statusRequest": "0x0F", "statusMask": "0xFF",
  "preSet": ["0x8F"], "postSet": ["0xFF"], "type": "onebyte" }
```

`type` is `toggle` (send the codes), `onebyte` (read `statusRequest`, let the user choose a value, send it) or `code` (reserved, not implemented in IES2 either).

**Active commands drive real actuators.** Leave `verified` false until a definition has been tested on a real car or bench.
