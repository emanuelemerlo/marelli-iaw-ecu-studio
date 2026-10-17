# Contributing

Thanks for wanting to help. This project is in early development and has not yet been tested on a real car, so the most valuable contributions right now are **reports from real ECUs**, followed by fixes to definitions, code and documentation.

By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to help

### Test on a real car

If you own one of the [supported cars](README.md#supported-ecus) and a KKL cable ([docs/hardware.md](docs/hardware.md)), a session on a real ECU is worth more than any code change. Start in read-only mode:

```sh
iaw-cli identify --port /dev/ttyUSB0 --trace my-car.trace.txt
iaw-cli read     --port /dev/ttyUSB0 --count 50 --trace my-car.trace.txt
iaw-cli errors   --port /dev/ttyUSB0 --trace my-car.trace.txt
```

Then open a [**Real car report**](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/issues/new?template=car_report.yml) and attach the trace. A trace that works is as useful as one that fails: it becomes a regression test (`iaw-cli read --replay my-car.trace.txt`).

Do not run actuator tests, clear codes or adjustments unless you know what they do on your car: they drive the fuel pump, injectors and coils. See [Safety](README.md#safety).

### Report a bug

Open a [**Bug report**](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/issues/new?template=bug_report.yml). Include the version, the operating system, the cable and, for anything that involves communication, a line trace (`--trace file.txt`, or the line trace option in the application). `iaw-cli trace file.txt` prints a summary you can paste in the issue.

### Fix or add an ECU definition

ECU families are JSON files in [`src/MarelliIawEcuStudio.Core/Definitions`](src/MarelliIawEcuStudio.Core/Definitions); [docs/definitions.md](docs/definitions.md) explains the format and how to try a file without rebuilding. Before opening a pull request:

- check it with `iaw-cli validate your-file.json`;
- keep `"verified": false` for a new family: active commands stay disabled until someone has confirmed them on a car;
- say where the data comes from (your own car and trace, IES2, documentation) so it can be checked.

For built-in definitions generated from IES2, change the generator in `tools/generate-definitions` too, or explain why the file now differs from the original: `LegacyEquivalenceTests` compare every table with the IES2 code.

### Translations

The interface is in English and Italian; parameter and error names are in ten languages. Corrections from native speakers are welcome, especially for technical terms.

### Ideas

Open a [**Feature request**](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/issues/new?template=feature_request.yml). For larger changes, please discuss in an issue before writing the code, so we can agree on the approach.

## Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (version pinned in `global.json`).

```sh
dotnet build
dotnet test
dotnet run --project src/MarelliIawEcuStudio.App          # choose "Simulator" as interface
dotnet run --project src/MarelliIawEcuStudio.Cli -- read --simulate --count 5
```

The [repository layout](README.md#repository-layout) table in the README says what each project contains. A few rules that keep the code base healthy:

- **Warnings are errors** (`TreatWarningsAsErrors`, nullable reference types enabled). The build must be clean on Windows, Linux and macOS: CI runs all three.
- **Core has no UI.** Protocol, decoding and session logic belong in `MarelliIawEcuStudio.Core`; view models in `Presentation`; Avalonia code only in `App`.
- **Add a test** for each fix or feature. The simulator and trace replay let you test communication without hardware; `MarelliIawEcuStudio.App.Tests` renders the real window headless.
- **`legacy/IES2` is read-only.** It is the reference the equivalence tests compile against; do not change it.
- **Active commands stay behind a confirmation.** Any new command that drives a component must go through the same confirmation and `--allow-active` / `verified` checks as the existing ones.
- Package versions are central, in `Directory.Packages.props`.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each pull request about one thing; describe what changes and why.
3. Make sure `dotnet build` and `dotnet test` pass, and `iaw-cli validate` if you touched definitions.
4. Update the README (both `README.md` and `README.it.md` when you can) and `docs/` for any user-visible change.
5. Fill in the pull request template.

## License of contributions

The project is released under the [GNU GPL v3 or later](LICENSE). By submitting a contribution you agree that it is released under the same license. Do not submit code or data you do not have the right to share, such as manufacturer documentation under a non-disclosure agreement or dumped ECU firmware.
