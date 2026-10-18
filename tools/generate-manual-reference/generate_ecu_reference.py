#!/usr/bin/env python3
"""Generates the ECU reference pages of the user manual from the JSON definitions.

    python3 tools/generate-manual-reference/generate_ecu_reference.py

Writes docs/manual/en/ecu-reference.md and docs/manual/it/ecu-reference.md.
Run it again after changing a definition or a translation, so the manual
always shows the names the program shows.
"""
import json
import pathlib
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[2]
CORE = ROOT / "src" / "MarelliIawEcuStudio.Core"
UI = ROOT / "src" / "MarelliIawEcuStudio.Presentation" / "Localization"
ORDER = ["IAW-6F/16F", "IAW-8F/18F", "IAW-8F.68", "IAW-18FD/1G7", "IAW-04K.P8", "FIAT CODE"]


def resx(path):
    if not path.exists():
        return {}
    return {d.get("name"): (d.find("value").text or "") for d in ET.parse(path).getroot().findall("data")}


TEXT = {
    "en": {
        "title": "ECU reference",
        "intro": "What each supported ECU family offers: live parameters, errors, actuator tests and adjustments, "
                 "with the names the program shows. This page is generated from the definition files by "
                 "`tools/generate-manual-reference/generate_ecu_reference.py`; do not edit it by hand.",
        "contents": "Families",
        "tech": "Technology", "period": "Period", "aka": "Also known as", "vehicles": "Vehicles recognised by ISO code",
        "params": "Live parameters", "errors": "Errors", "engine": "Engine", "immo": "Immobiliser",
        "tests": "Actuator tests", "adjust": "Adjustments", "clear": "Clear codes: max {0} s.",
        "id": "ID", "name": "Name", "group": "Dashboard group", "widget": "Shown as",
        "dur": "Max duration", "cond": "Condition", "type": "Type", "none": "None.",
        "unverified": "**Not verified**: active commands are disabled.",
        "widgets": {"gauge": "round gauge", "bar": "bar", "value": "value", "led": "light"},
        "back": "[← Manual](README.md) · Previous: [Command line](command-line.md) · Next: [Glossary →](glossary.md)",
    },
    "it": {
        "title": "Riferimento centraline",
        "intro": "Cosa offre ogni famiglia di centraline supportata: parametri in tempo reale, errori, test attuatori "
                 "e regolazioni, con i nomi che mostra il programma. Questa pagina è generata dai file di definizione "
                 "con `tools/generate-manual-reference/generate_ecu_reference.py`; non modificarla a mano.",
        "contents": "Famiglie",
        "tech": "Tecnologia", "period": "Periodo", "aka": "Nota anche come", "vehicles": "Veicoli riconosciuti dal codice ISO",
        "params": "Parametri in tempo reale", "errors": "Errori", "engine": "Motore", "immo": "Immobilizzatore",
        "tests": "Test attuatori", "adjust": "Regolazioni", "clear": "Cancellazione errori: al massimo {0} s.",
        "id": "ID", "name": "Nome", "group": "Gruppo del cruscotto", "widget": "Mostrato come",
        "dur": "Durata massima", "cond": "Condizione", "type": "Tipo", "none": "Nessuna.",
        "unverified": "**Non verificata**: i comandi attivi sono disabilitati.",
        "widgets": {"gauge": "strumento circolare", "bar": "barra", "value": "valore", "led": "spia"},
        "back": "[← Manuale](README.md) · Precedente: [Riga di comando](command-line.md) · Successivo: [Glossario →](glossary.md)",
    },
}


def anchor(text):
    keep = "".join(c.lower() if c.isalnum() else ("-" if c in " -" else "") for c in text)
    return keep


def main():
    core_en = resx(CORE / "Localization" / "Strings.resx")
    ui_en = resx(UI / "UiStrings.resx")
    defs = {}
    for f in (CORE / "Definitions").glob("*.json"):
        d = json.loads(f.read_text(encoding="utf-8"))
        defs[d["id"]] = d
    ids = [i for i in ORDER if i in defs] + sorted(i for i in defs if i not in ORDER)

    for lang, t in TEXT.items():
        core = dict(core_en, **resx(CORE / "Localization" / f"Strings.{lang}.resx")) if lang != "en" else core_en
        ui = dict(ui_en, **resx(UI / f"UiStrings.{lang}.resx")) if lang != "en" else ui_en

        def name(o):
            k = o.get("nameKey")
            return core.get(k) or o.get("name") or k or "?"

        def loc(v):
            if isinstance(v, dict):
                return v.get(lang) or v.get("en") or ""
            return v or ""

        out = [f"# {t['title']}", "", t["back"], "", t["intro"], "", f"## {t['contents']}", ""]
        for i in ids:
            out.append(f"- [{defs[i].get('longName', i)}](#{anchor(defs[i].get('longName', i))})")
        out.append("")

        for i in ids:
            d = defs[i]
            out += [f"## {d.get('longName', i)}", ""]
            fam = d.get("family") or {}
            if not d.get("verified", False):
                out += [t["unverified"], ""]
            if fam.get("technology"):
                out.append(f"- **{t['tech']}**: {loc(fam['technology'])}")
            if fam.get("period"):
                out.append(f"- **{t['period']}**: {fam['period']}")
            if d.get("aliases"):
                out.append(f"- **{t['aka']}**: " + ", ".join(a["name"] for a in d["aliases"]))
            if d.get("vehicles"):
                out.append(f"- **{t['vehicles']}**: {len(d['vehicles'])}")
            out.append("")

            out += [f"### {t['params']}", ""]
            out += [f"| {t['id']} | {t['name']} | {t['group']} | {t['widget']} |", "| --- | --- | --- | --- |"]
            for p in d.get("parameters", []):
                disp = p.get("display") or {}
                group = ui.get("Group_" + disp.get("group", "other"), disp.get("group", ""))
                widget = t["widgets"].get(disp.get("widget", "value"), disp.get("widget", ""))
                out.append(f"| `{p['id']}` | {name(p)} | {group} | {widget} |")
            out.append("")

            errs = d.get("errors") or {}
            if any((errs.get(k) or {}).get("items") for k in ("engine", "immobilizer")):
                out += [f"### {t['errors']}", ""]
                for key, label in (("engine", t["engine"]), ("immobilizer", t["immo"])):
                    items = (errs.get(key) or {}).get("items") or []
                    if items:
                        out.append(f"- **{label}** ({len(items)}): " + ", ".join(name(e) for e in items))
                out.append("")

            tests = d.get("activeTests") or []
            out += [f"### {t['tests']}", ""]
            if tests:
                out += [f"| {t['name']} | {t['dur']} | {t['cond']} |", "| --- | --- | --- |"]
                for x in tests:
                    cond = ui["EngineRunning"] if x.get("engineRunning") else ui["EngineStopped"]
                    out.append(f"| {name(x)} | {x.get('timeoutSeconds')} s | {cond} |")
            else:
                out.append(t["none"])
            out.append("")
            if d.get("clearCodes"):
                out += [t["clear"].format(d["clearCodes"].get("timeoutSeconds", "?")), ""]

            adjs = d.get("adjustments") or []
            out += [f"### {t['adjust']}", ""]
            if adjs:
                out += [f"| {t['name']} | {t['type']} |", "| --- | --- |"]
                for a in adjs:
                    out.append(f"| {name(a)} | {ui.get('AdjustType_' + a.get('type', ''), a.get('type', ''))} |")
            else:
                out.append(t["none"])
            out.append("")

        path = ROOT / "docs" / "manual" / lang / "ecu-reference.md"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(out).rstrip() + "\n", encoding="utf-8")
        print("wrote", path.relative_to(ROOT))


if __name__ == "__main__":
    main()
