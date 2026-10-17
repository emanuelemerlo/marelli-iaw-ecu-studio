## What and why

<!-- What this pull request changes and why. Link the issue it fixes: "Fixes #123". -->

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] ECU definition (new or corrected)
- [ ] Translation
- [ ] Documentation
- [ ] Build / CI / refactoring

## How it was tested

<!-- Simulator, trace replay, real car (which one), new or updated tests. -->

- [ ] Simulator / unit tests
- [ ] Trace replay
- [ ] Real car: <!-- model, engine, ECU -->

## Checklist

- [ ] `dotnet build` and `dotnet test` pass with no warnings
- [ ] Tests added or updated for the change
- [ ] `iaw-cli validate` passes for changed definitions; new families keep `"verified": false`
- [ ] Active commands (actuator tests, clear codes, adjustments) still require confirmation and `--allow-active`
- [ ] `legacy/IES2` is unchanged
- [ ] README (`README.md`, and `README.it.md` if possible) and `docs/` updated for user-visible changes
- [ ] I agree that this contribution is released under the GNU GPL v3 or later
