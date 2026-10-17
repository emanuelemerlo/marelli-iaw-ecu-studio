# Security policy

## Supported versions

The project is in early development: only the latest release and the `main` branch receive fixes.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem. Report it privately instead:

- through GitHub: **Security → Report a vulnerability** on this repository ([direct link](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/security/advisories/new)), or
- by email to emanuele.merlo@gmail.com.

Describe the problem, the affected version and, if possible, how to reproduce it. You should receive an answer within a week. Once a fix is released, the report will be credited to you unless you prefer otherwise.

## What counts as a security problem

Besides the usual software vulnerabilities, for a program that talks to an engine control unit the following are treated as security issues:

- an **active command** (actuator test, clear codes, adjustment) that can be sent without the confirmation, without `--allow-active` on the command line, or for a definition not marked `verified`;
- the **network bridge** (`iaw-cli bridge`, RFC 2217) accepting connections or commands it should not, or exposing the serial port beyond the address it was asked to listen on;
- a **definition, log or trace file** that, when opened, makes the program run code, write outside its folders, or send commands to the ECU;
- a problem in a published **release executable** or **NuGet package** (for example a tampered artifact).

Wrong values, a definition that decodes a parameter badly, or a failed connection are ordinary bugs: please open a normal [bug report](https://github.com/emanuelemerlo/marelli-iaw-ecu-studio/issues/new?template=bug_report.yml).

Note that the network bridge has no authentication: run it only on a network you trust, such as the car's own Wi-Fi access point.
