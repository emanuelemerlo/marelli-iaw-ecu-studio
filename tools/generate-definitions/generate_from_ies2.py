#!/usr/bin/env python3
"""Generate ECU definition files (JSON) from the original IES2 C# sources.

This is a one-off migration tool. It reads the ECU modules under
legacy/IES2/IES_2/ECU, extracts every table (live data, errors, tests,
adjustments, vehicles, detection rules) and translates each decoding
function into the expression language understood by
MarelliIawEcuStudio.Core.

The generated files are checked into src/MarelliIawEcuStudio.Core/Definitions
and verified against the original code by the LegacyEquivalence tests, so
this script only needs to run again if the migration rules change.

Usage: python3 tools/generate-definitions/generate_from_ies2.py
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEGACY = ROOT / "legacy" / "IES2" / "IES_2" / "ECU"
OUT = ROOT / "src" / "MarelliIawEcuStudio.Core" / "Definitions"

# Per-module metadata that is expressed as code (not tables) in IES2.
MODULES = {
    "iaw16f": {"file": "IAW-6F-16F.json", "identification": "standard", "detectionOrder": 40},
    "iaw18f": {"file": "IAW-8F-18F.json", "identification": "standard", "detectionOrder": 20},
    "iaw8f_68": {"file": "IAW-8F68.json", "identification": "standard", "detectionOrder": 30},
    "iaw18fd": {"file": "IAW-18FD-1G7.json", "identification": "standard", "detectionOrder": 10},
    "iaw04k": {"file": "IAW-04K-P8.json", "identification": "p8", "detectionOrder": 50},
    "code": {"file": "FIAT-CODE.json", "identification": "standard", "detectionOrder": None},
}

# Error decoding functions -> scheme names in the Core.
ERROR_SCHEMES = {
    "err16F": "storedFirst",
    "err18F": "activeVerifiedStored",
    "err04K": "activeVerifiedStoredNoVerifiedFlag",
    "err8F_68": "noReason",
    "errCODE": "fiatCode",
}

# Decoders whose bodies are not a single-line expression.
MULTILINE_DECODERS = {"PERIODE"}


# ---------------------------------------------------------------------------
# Dashboard layout. Not in IES2: chosen for this project. Ranges are the
# usual instrument scales for these engines; zones mark unusual values.
# ---------------------------------------------------------------------------

def zone(a, b, level):
    return {"from": a, "to": b, "level": level}


DISPLAY = {
    "PERIODE": {"group": "engine", "widget": "gauge", "min": 0, "max": 7000,
                "zones": [zone(6000, 6500, "warning"), zone(6500, 7000, "danger")]},
    "MP2_MP8": {"group": "engine", "widget": "gauge", "min": 0, "max": 1100},
    "MP2_MP8t": {"group": "engine", "widget": "gauge", "min": 0, "max": 2600,
                 "zones": [zone(2100, 2350, "warning"), zone(2350, 2600, "danger")]},
    "T_INJ_AP": {"group": "engine", "widget": "bar", "min": 0, "max": 20},
    "AVANCE": {"group": "engine", "widget": "bar", "min": -10, "max": 50},
    "INJ_AVA": {"group": "engine", "widget": "value"},
    "MT_EAU_L": {"group": "temperatures", "widget": "gauge", "min": 40, "max": 130,
                 "zones": [zone(40, 60, "cold"), zone(105, 115, "warning"), zone(115, 130, "danger")]},
    "MT_AIR_L": {"group": "temperatures", "widget": "bar", "min": -20, "max": 80,
                 "zones": [zone(60, 80, "warning")]},
    "M_VBATT": {"group": "electrical", "widget": "gauge", "min": 8, "max": 16,
                "zones": [zone(8, 10.5, "danger"), zone(10.5, 11.8, "warning"), zone(14.8, 15.5, "warning"), zone(15.5, 16, "danger")]},
    "K_O2_RAW": {"group": "mixture", "widget": "bar", "min": 0, "max": 1000},
    "K_O2": {"group": "mixture", "widget": "bar", "min": 0.75, "max": 1.25, "center": 1.0},
    "ANG_PAP0": {"group": "idle", "widget": "bar", "min": 0, "max": 90},
    "T_VAE": {"group": "idle", "widget": "bar", "min": 0, "max": 100},
    "CONS_REG": {"group": "idle", "widget": "value"},
    "OFNNTR": {"group": "idle", "widget": "value"},
    "INTEGR": {"group": "idle", "widget": "value"},
    "PROP": {"group": "idle", "widget": "value"},
    "DELREG": {"group": "idle", "widget": "value"},
    "ALFAFTR": {"group": "idle", "widget": "value"},
    "TRIMRAM": {"group": "adaptation", "widget": "bar", "min": -128, "max": 127, "center": 0},
    "CRDVAS": {"group": "other", "widget": "value"},
}
for _id in ["ALFAU", "ALFAUC", "ADOFFSET", "ADOFFPURG", "ADOFFPL", "ADGAIN"]:
    DISPLAY[_id] = {"group": "adaptation", "widget": "value"}
for _id in ["OFFSET1", "OFFSET2", "OFFSET3", "OFFSET4", "OFFSET5", "OFFSET6", "OFFSETm"]:
    DISPLAY[_id] = {"group": "adaptation", "widget": "bar", "min": -2, "max": 2, "center": 0}

# LED colour when the flag is on.
LED_WARNING = {"StaAdvDecr"}
LED_INFO = {"StaThrMM", "StaAirCoON", "StaGearEng", "StaABSON", "StaVAESC",
            "FABEPAT", "FABEEAT", "FABEPATi", "FABEEATi"}


def display_for(name, p):
    if p["decode"]["raw"] == "bit":
        key = p.get("nameKey", "")
        led = "warning" if key in LED_WARNING else "info" if key in LED_INFO else "good"
        return {"group": "status", "widget": "led", "led": led}
    d = dict(DISPLAY.get(p["id"], {"group": "other", "widget": "value"}))
    # Idle stepper position: raw 0..255, but IAW-04K reports it as -128..127.
    if p["id"] == "ALFAR":
        signed = p["decode"].get("expr") == "x - 128"
        d = {"group": "idle", "widget": "bar", "min": -128 if signed else 0, "max": 127 if signed else 255}
        if signed:
            d["center"] = 0
    return d


# ---------------------------------------------------------------------------
# Family information supplied by the project owner (October 2026). Not
# checked against real units yet: shown as an indicative list.
# ---------------------------------------------------------------------------

# Other names of units that work with a definition, so users find their ECU
# by the code on its label. Supplied by the project owner (October 2026).
ALIASES = {
    "iaw04k": [
        {"name": "IAW 4WF.P8", "note": "Fiat Coupé 2.0 Turbo 16V (software P8)"},
    ],
}


def car(brand, model, engines=None):
    return {"brand": brand, "model": model, **({"engines": engines} if engines else {})}


FAMILY = {
    "iaw18fd": {
        "technology": {"en": "Multipoint electronic injection (MPI) with static ignition",
                       "it": "Iniezione elettronica multipoint (MPI) con accensione statica"},
        "period": "1995–2001",
        "compatibility": [
            car("Fiat", "Punto 60 / 75 (Mk1)", "1.2 8V Fire (1242 cc)"),
            car("Fiat", "Palio / Palio Weekend", "1.2 8V, 1.5 8V SPI/MPI"),
            car("Fiat", "Uno / Fiorino", "1.5 MPI (Latin American and European markets, late 1990s)"),
            car("Fiat", "Siena / Strada", "1.2 8V Fire"),
        ],
    },
    "iaw18f": {
        "technology": {"en": "Sequential phased multipoint injection (MPI), common on Fire and boxer engines",
                       "it": "Iniezione multipoint (MPI) sequenziale fasata, comune sui motori Fire e boxer"},
        "period": "1993–1999",
        "compatibility": [
            car("Fiat", "Punto 75 / 90 / Cabrio (Mk1)", "1.2 8V, 1.6 8V"),
            car("Fiat", "Bravo / Brava / Marea", "1.6 16V (early versions), 1.4 12V"),
            car("Lancia", "Y (Mk1)", "1.2 8V (LX / Elefantino Rosso), 1.4 12V"),
            car("Lancia", "Dedra / Delta II", "1.6 8V, 1.6 16V"),
            car("Alfa Romeo", "145 / 146", "1.3 8V Boxer"),
        ],
    },
    "iaw8f_68": {
        "technology": {"en": "Multipoint injection calibrated for the Alfa Romeo boxer engines",
                       "it": "Iniezione multipoint tarata per i motori boxer Alfa Romeo"},
        "period": "1992–1995",
        "compatibility": [
            car("Alfa Romeo", "33 (3rd series / Nuova 33)", "Boxer 1.3 ie, 1.5 ie, 1.7 ie 8V"),
        ],
    },
    "iaw16f": {
        "technology": {"en": "Single-point injection (one injector on the throttle body) for small engines",
                       "it": "Iniezione single-point (monoiniettore sul corpo farfallato) per motori di piccola cilindrata"},
        "period": "1993–2000",
        "compatibility": [
            car("Fiat", "Cinquecento", "900 ie, 1.1 Sporting"),
            car("Fiat", "Seicento", "900 ie, 1.1 Sporting (until Euro 3, 2000)"),
            car("Fiat", "Panda (141)", "900 ie, 1100 Fire 4x4 / Trekking"),
            car("Fiat", "Punto 55 / 60 (Mk1)", "1.1 8V, 1.2 8V Fire SPI"),
            car("Lancia", "Y10 (3rd series)", "1.1 i.e. Fire"),
            car("Seat", "Marbella", "0.9 i.e. (last series, built under Fiat licence)"),
        ],
    },
    "iaw04k": {
        "technology": {"en": "High-performance ECU for the turbocharged and multi-valve engines of 1990s sports cars",
                       "it": "Centralina ad alte prestazioni per i motori turbo e plurivalvole delle sportive anni '90"},
        "period": "1989–1996",
        "compatibility": [
            car("Lancia", "Delta HF Integrale", "16V, Evoluzione I/II (2.0 Turbo 16V)"),
            car("Alfa Romeo", "155 Q4", "2.0 Turbo 16V 4WD"),
            car("Fiat", "Coupé", "2.0 Turbo 16V"),
            car("Ducati", "888 (SP/SPS/Strada, from 1993), 916 (Strada/SP/SPS), 996 SPS", "P8/IAW module"),
            car("Moto Guzzi", "Daytona 1000, California i.e. (early series)", "P8/IAW module"),
        ],
        "notes": {"en": "Motorcycles use the same module family with their own software and connector; not tested with this program.",
                  "it": "Le moto usano la stessa famiglia di moduli con software e connettore propri; non provate con questo programma."},
    },
    "code": {
        "technology": {"en": "Immobiliser: does not control the injection; it talks to the transponder in the key to enable starting",
                       "it": "Immobilizzatore: non controlla l'iniezione; dialoga con il transponder della chiave per consentire l'avviamento"},
        "period": "1995–",
        "compatibility": [
            car("Fiat", "Punto, Bravo, Marea, Barchetta, Coupé"),
            car("Alfa Romeo", "145, 146, 155, 156"),
            car("Lancia", "Y, K, Delta II"),
        ],
    },
}


def hexs(value: int) -> str:
    return f"0x{value:02X}"


def parse_bytes(text: str):
    text = text.strip()
    if text == "null":
        return None
    m = re.match(r"new byte\[\]\s*\{(.*)\}", text)
    if not m:
        raise ValueError(f"not a byte array: {text}")
    items = [i.strip() for i in m.group(1).split(",") if i.strip()]
    return [int(i, 0) for i in items]


def strip_comments(src: str) -> str:
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return "\n".join(re.sub(r"(?<!:)//.*$", "", line) for line in src.splitlines())


def lang_key(token: str):
    """Return (key, literal) for a C# string argument."""
    token = token.strip()
    if token.startswith("lang."):
        return token[5:], None
    if token.startswith('"') and token.endswith('"'):
        return None, token[1:-1]
    raise ValueError(f"unexpected string token {token}")


def split_args(text: str):
    """Split a C# argument list at top-level commas."""
    args, depth, cur, in_str = [], 0, "", False
    for ch in text:
        if ch == '"':
            in_str = not in_str
        if not in_str:
            if ch in "([{":
                depth += 1
            elif ch in ")]}":
                depth -= 1
            elif ch == "," and depth == 0:
                args.append(cur.strip())
                cur = ""
                continue
        cur += ch
    if cur.strip():
        args.append(cur.strip())
    return args


def find_calls(src: str, ctor: str):
    """Yield argument lists of `new <ctor>( ... )` calls in source order."""
    pattern = re.compile(r"new\s+" + re.escape(ctor) + r"\s*\(")
    for m in pattern.finditer(src):
        i, depth = m.end(), 1
        while depth:
            if src[i] == "(":
                depth += 1
            elif src[i] == ")":
                depth -= 1
            i += 1
        yield split_args(src[m.end(): i - 1])


def block(src: str, name: str) -> str:
    """Return the `{ ... }` initialiser assigned to `name = new X[] { ... }`."""
    m = re.search(re.escape(name) + r"\s*=\s*new\s+\w+\[\]\s*\{", src)
    if not m:
        return ""
    i, depth = m.end(), 1
    while depth:
        if src[i] == "{":
            depth += 1
        elif src[i] == "}":
            depth -= 1
        i += 1
    return src[m.end(): i - 1]


def decoder_body(src: str, fn: str) -> str:
    m = re.search(r"private static decimal " + re.escape(fn) + r"\(\)\s*\{\s*return\s+(.*?);\s*\}", src)
    if not m:
        raise ValueError(f"decoder {fn} not found")
    return m.group(1).strip()


def engine_speed_decoder(src: str, fn: str, requests):
    """PERIODE: engine speed from the 16-bit period between ignition events.

    IES2 returns 0 when both bytes are 0x00 or both are 0xFF, otherwise
    K / period. K is 15e6 for most families and 30e6 for IAW-04K.P8.
    """
    m = re.search(r"private static decimal " + fn + r"\(\)(.*?)\n\s*\}", src, re.S)
    body = re.sub(r"\s+", " ", m.group(1))
    assert "if ((Buffer[0x01] | Buffer[0x02]) == 0) return 0;" in body, body
    assert "else if ((Buffer[0x01] & Buffer[0x02]) == 255) return 0;" in body, body
    k = re.search(r"return \(decimal\)\(([0-9.e+]+) / unchecked", body).group(1)
    assert requests == [0x01, 0x02], requests
    return {"raw": "u16", "expr": f"x == 0 || x == 65535 ? 0 : {int(float(k))} / x"}


def translate(body: str, requests):
    """Translate a C# decoder body into (raw, expr|None, bit)."""
    b = re.sub(r"\s+", " ", body)
    # Bit flags: (Buffer[0xNN].GetBit(k)) ? 1 : 0   (or ? 0 : 1 inverted)
    m = re.fullmatch(r"\(Buffer\[(0x[0-9A-Fa-f]+)\]\.GetBit\((\d)\)\) \? (\d) : (\d)", b)
    if m:
        reg, bit, t, f = int(m.group(1), 16), int(m.group(2)), m.group(3), m.group(4)
        assert [reg] == requests, (body, requests)
        return {"raw": "bit", "bit": bit, **({"invert": True} if (t, f) == ("0", "1") else {})}
    m = re.fullmatch(r"\(decimal\)\(ConvTab\[Buffer\[(0x[0-9A-Fa-f]+)\]\]\)", b)
    if m:
        assert [int(m.group(1), 16)] == requests
        return {"raw": "u8", "table": "temperature"}

    raw = None
    word = r"\(\(ushort\)Buffer\[(0x[0-9A-Fa-f]+)\]\) << 8 \| \(ushort\)Buffer\[(0x[0-9A-Fa-f]+)\]"

    def word_sub(signed):
        nonlocal b, raw
        pat = (r"unchecked\(\(short\)\(" + word + r"\)\)") if signed else (r"unchecked\(" + word + r"\)")
        m2 = re.search(pat, b)
        if m2:
            assert [int(m2.group(1), 16), int(m2.group(2), 16)] == requests, (body, requests)
            raw = "s16" if signed else "u16"
            b = b[: m2.start()] + "x" + b[m2.end():]

    word_sub(True)
    word_sub(False)
    m = re.search(r"unchecked\(\(sbyte\)\(Buffer\[(0x[0-9A-Fa-f]+)\]\)\)", b)
    if m:
        assert [int(m.group(1), 16)] == requests, (body, requests)
        raw = "s8"
        b = b[: m.start()] + "x" + b[m.end():]
    regs = re.findall(r"Buffer\[(0x[0-9A-Fa-f]+)\]", b)
    if regs:
        assert raw is None and all(int(r, 16) == requests[0] for r in regs) and len(requests) == 1, (body, requests)
        raw = "u8"
        b = re.sub(r"Buffer\[0x[0-9A-Fa-f]+\]", "x", b)
    assert raw is not None, body

    expr = b.replace("(decimal)", "").strip()
    # Remove redundant outer parentheses.
    while expr.startswith("(") and expr.endswith(")") and balanced(expr[1:-1]):
        expr = expr[1:-1].strip()
    # C# integer division (no floating literal involved) -> idiv().
    m = re.fullmatch(r"x \* (\d+) / (\d+)", expr)
    if m:
        expr = f"idiv(x * {m.group(1)}, {m.group(2)})"
    expr = re.sub(r"(\d)\.0\b", r"\1", expr)  # 2.0 -> 2, 90.0 -> 90
    expr = expr.replace("15e+6", "15000000").replace("8e+3", "8000")
    if expr == "x":
        return {"raw": raw}
    return {"raw": raw, "expr": expr}


def balanced(s: str) -> bool:
    depth = 0
    for ch in s:
        depth += ch == "("
        depth -= ch == ")"
        if depth < 0:
            return False
    return depth == 0


def text_field(prefix: str, token: str, out: dict):
    key, literal = lang_key(token)
    if key is not None:
        out[prefix + "Key"] = key
    elif literal:
        out[prefix] = literal


def module_definition(name: str, meta: dict) -> dict:
    raw_src = (LEGACY / f"{name}.cs").read_text(encoding="utf-8-sig")
    src = strip_comments(raw_src)
    d = {"$schema": "../../../schemas/ecu-definition.schema.json"}
    d["id"] = re.search(r'public const string name = "([^"]+)"', src).group(1)
    d["name"] = d["id"]
    d["longName"] = re.search(r'public const string longName = "([^"]+)"', src).group(1)
    d["source"] = f"IES2 0.86, legacy/IES2/IES_2/ECU/{name}.cs"
    d["verified"] = True

    d["protocol"] = {"identification": meta["identification"]}
    if name in FAMILY:
        d["family"] = {**FAMILY[name], "compatibilityVerified": False}
    if name in ALIASES:
        d["aliases"] = ALIASES[name]
    detection = {"autoDetect": meta["detectionOrder"] is not None}
    if meta["detectionOrder"] is not None:
        detection["order"] = meta["detectionOrder"]
        iso_check = re.search(r"static bool CheckISO\(\)\s*\{(.*?)\n\s*\}", src, re.S).group(1)
        detection["isoMatches"] = [
            {"offset": int(o), "value": v}
            for o, _, v in re.findall(r'ISO\.Substring\((\d+), (\d+)\) == "([0-9A-F]+)"', iso_check)
        ]
        codric_check = re.search(r"static bool CheckCODRIC\(\)\s*\{(.*?)\n\s*\}", src, re.S).group(1)
        detection["codricMatches"] = [
            {"offset": int(o), "value": v}
            for o, _, v in re.findall(r'CODRIC\.Substring\((\d+), (\d+)\) == "([0-9]+)"', codric_check)
        ]
    d["detection"] = detection

    d["vehicles"] = [{"iso": iso, "model": model} for iso, model in re.findall(r'\{"([0-9A-F?]{12})", "([^"]*)"\}', src)]

    tables = {}
    m = re.search(r"ConvTab = \{(.*?)\};", src, re.S)
    if m:
        tables["temperature"] = [float(v) for v in m.group(1).replace("\n", " ").split(",") if v.strip()]
    if tables:
        d["tables"] = tables

    params = []
    for args in find_calls(block(src, "engineData"), "dataElement"):
        p = {}
        text_field("name", args[0], p)
        text_field("unit", args[1], p)
        text_field("format", args[2], p)
        p["requests"] = [hexs(v) for v in parse_bytes(args[3])]
        fn = re.search(r"ValDecode\((\w+)\)", args[4]).group(1)
        p["id"] = fn
        reqs = parse_bytes(args[3])
        if fn in MULTILINE_DECODERS:
            p["decode"] = engine_speed_decoder(src, fn, reqs)
        else:
            p["decode"] = translate(decoder_body(src, fn), reqs)
        p["display"] = display_for(name, p)
        params.append({"id": p.pop("id"), **p})
    d["parameters"] = params

    def error_list(block_name):
        items, scheme = [], None
        for args in find_calls(block(src, block_name), "errorElement"):
            e = {}
            text_field("name", args[0], e)
            nums = [int(a, 0) for a in args[1:9]]
            e["active"], e["verified"], e["stored"], e["bit"] = hexs(nums[0]), hexs(nums[1]), hexs(nums[2]), nums[3]
            e["ext"] = {"active": hexs(nums[4]), "verified": hexs(nums[5]), "stored": hexs(nums[6]), "bit": nums[7]}
            text_field("highReason", args[9], e)
            text_field("lowReason", args[10], e)
            s = ERROR_SCHEMES[re.search(r"ErrDecode\((\w+)\)", args[11]).group(1)]
            assert scheme in (None, s)
            scheme = s
            items.append(e)
        return items, scheme

    def req_list(var):
        m2 = re.search(var + r"\s*=\s*(new byte\[\]\s*\{[^}]*\})", src)
        return [hexs(v) for v in parse_bytes(m2.group(1))] if m2 else []

    engine, engine_scheme = error_list("engineErrors")
    immo, immo_scheme = error_list("immoErrors")
    d["errors"] = {
        "engine": {"scheme": engine_scheme or "noReason", "requests": req_list("engineErrReq"), "items": engine},
        "immobilizer": {"scheme": immo_scheme or "fiatCode", "requests": req_list("immoErrReq"), "items": immo},
    }

    tests = []
    for args in find_calls(block(src, "activeTest"), "testElement"):
        t = {}
        text_field("name", args[0], t)
        t["engineRunning"] = args[1] == "true"
        t["timeoutSeconds"] = int(args[2])
        t["requests"] = [hexs(v) for v in parse_bytes(args[3])]
        tests.append(t)
    d["activeTests"] = tests

    m = re.search(r"clearCodes = new testElement\((.*?)\);", src)
    args = split_args(m.group(1))
    d["clearCodes"] = {"timeoutSeconds": int(args[2]), "requests": [hexs(v) for v in parse_bytes(args[3])]}

    adjustments = []
    for args in find_calls(block(src, "adjustments"), "adjustElement"):
        a = {}
        text_field("name", args[0], a)
        a["statusRequest"] = hexs(int(args[1], 0))
        a["statusMask"] = hexs(int(args[2], 0))
        a["preSet"] = [hexs(v) for v in parse_bytes(args[3])]
        post = parse_bytes(args[4])
        if post is not None:
            a["postSet"] = [hexs(v) for v in post]
        a["type"] = args[5].strip('"')
        adjustments.append(a)
    d["adjustments"] = adjustments
    return d


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, meta in MODULES.items():
        d = module_definition(name, meta)
        path = OUT / meta["file"]
        path.write_text(json.dumps(d, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"{path.relative_to(ROOT)}: {len(d['parameters'])} parameters, "
              f"{len(d['errors']['engine']['items'])}+{len(d['errors']['immobilizer']['items'])} errors, "
              f"{len(d['activeTests'])} tests, {len(d['adjustments'])} adjustments, {len(d['vehicles'])} vehicles")
    return 0


if __name__ == "__main__":
    sys.exit(main())
