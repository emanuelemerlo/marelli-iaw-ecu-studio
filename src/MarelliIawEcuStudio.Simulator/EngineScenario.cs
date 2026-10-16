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

    private static void Set(SimulatedEcu ecu, string id, double value)
    {
        if (ecu.Definition.Parameters.Any(p => p.Id == id))
            ecu.SetValue(id, value);
    }
}
