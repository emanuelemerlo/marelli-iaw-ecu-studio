namespace MarelliIawEcuStudio.Simulator;

/// <summary>
/// Moves the simulated engine values over time, so live data and graphs
/// show something realistic. Only parameters the definition has are set.
/// </summary>
public static class EngineScenario
{
    /// <summary>A warm engine idling, with small oscillations.</summary>
    public static Action<SimulatedEcu, TimeSpan> Idle(int updateIntervalMs = 100)
    {
        TimeSpan? last = null;
        return (ecu, t) =>
        {
            if (last is { } previous && (t - previous).TotalMilliseconds < updateIntervalMs)
                return;
            last = t;
            var s = t.TotalSeconds;
            Set(ecu, "PERIODE", 850 + 25 * Math.Sin(s * 1.3));
            Set(ecu, "T_INJ_AP", 2.4 + 0.1 * Math.Sin(s * 1.3));
            Set(ecu, "AVANCE", 10 + Math.Sin(s * 0.7));
            Set(ecu, "MP2_MP8", 350 + 10 * Math.Sin(s * 1.3));
            Set(ecu, "MT_AIR_L", 25);
            Set(ecu, "MT_EAU_L", Math.Min(90, 60 + s / 4));
            Set(ecu, "ANG_PAP0", 2);
            Set(ecu, "M_VBATT", 13.9 + 0.1 * Math.Sin(s * 0.3));
            Set(ecu, "V_BATT", 13.9);
            Set(ecu, "K_O2", 1 + 0.03 * Math.Sin(s * 2.1));
            Set(ecu, "ALFAR", 40);
            Set(ecu, "CONS_REG", 850);
            Set(ecu, "S2", 1);
            Set(ecu, "S3", 1);
            Set(ecu, "S5", 1);
        };
    }

    /// <summary>
    /// A 20-second loop: idle, acceleration to about 4500 rpm, release.
    /// Warms up the engine and toggles flags, so every instrument moves.
    /// </summary>
    public static Action<SimulatedEcu, TimeSpan> Drive(int updateIntervalMs = 100)
    {
        TimeSpan? last = null;
        return (ecu, t) =>
        {
            if (last is { } previous && (t - previous).TotalMilliseconds < updateIntervalMs)
                return;
            last = t;
            var s = t.TotalSeconds;
            var load = Math.Pow(0.5 - 0.5 * Math.Cos(2 * Math.PI * s / 20), 1.5); // 0 idle .. 1 full
            Set(ecu, "PERIODE", 850 + 3650 * load + 15 * Math.Sin(s * 3));
            Set(ecu, "T_INJ_AP", 2.4 + 6.5 * load);
            Set(ecu, "AVANCE", 10 + 22 * load);
            Set(ecu, "MP2_MP8", 340 + 620 * load);
            Set(ecu, "MP2_MP8t", 360 + 1500 * load); // IAW-04K turbo: same register, boost scale
            Set(ecu, "MT_AIR_L", 24 + 6 * load);
            Set(ecu, "MT_EAU_L", Math.Min(91, 55 + s * 1.5));
            Set(ecu, "ANG_PAP0", 2 + 60 * load);
            Set(ecu, "M_VBATT", 13.8 + 0.2 * Math.Sin(s * 0.4));
            Set(ecu, "K_O2", 1 + 0.04 * Math.Sin(s * 2.3));
            Set(ecu, "K_O2_RAW", 450 + 400 * Math.Sin(s * 2.3));
            Set(ecu, "ALFAR", load < 0.05 ? 45 : 20);
            Set(ecu, "CONS_REG", 850);
            Set(ecu, "T_VAE", load < 0.05 ? 35 : 0);
            Set(ecu, "TRIMRAM", 3);
            Set(ecu, "OFNNTR", 0);
            Set(ecu, "DELREG", 0);
            Set(ecu, "ALFAFTR", 0);
            Set(ecu, "INJ_AVA", 300);
            var closedLoop = load < 0.6 ? 1 : 0;
            Set(ecu, "S2", 1);              // engine running
            Set(ecu, "S3", 1);              // sensor signals OK
            Set(ecu, "S4", load < 0.02 ? 1 : 0); // throttle at min
            Set(ecu, "S5", closedLoop);
            Set(ecu, "S6", (int)(s / 30) % 2); // A/C on every other 30 s
            Set(ecu, "S7", 1);
            Set(ecu, "S25", load > 0.92 ? 1 : 0); // knock: advance reduced at full load
            Set(ecu, "S26", 1);
            Set(ecu, "FLGSMA", closedLoop);
            Set(ecu, "UR4", load < 0.02 ? 1 : 0);
        };
    }

    private static void Set(SimulatedEcu ecu, string id, double value)
    {
        if (ecu.Definition.Parameters.Any(p => p.Id == id))
            ecu.SetValue(id, value);
    }
}
