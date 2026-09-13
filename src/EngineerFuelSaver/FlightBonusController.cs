using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Setzt den Ingenieurs-Bonus auf allen geladenen Schiffen im Flug.
    ///
    /// Es wird periodisch komplett neu berechnet statt auf einzelne GameEvents zu hoeren:
    /// Docking, Crew-Transfer, Staging, EVA und Level-Ups sind damit automatisch abgedeckt.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightBonusController : MonoBehaviour
    {
        private readonly EngineTweaker tweaker = new EngineTweaker();
        private readonly Dictionary<Guid, int> vesselLevels = new Dictionary<Guid, int>();

        private float nextScan;

        private void Update()
        {
            if (Time.realtimeSinceStartup < nextScan) return;
            nextScan = Time.realtimeSinceStartup + Settings.RefreshInterval;
            Scan();
        }

        private void OnDestroy()
        {
            tweaker.RestoreAll();
            vesselLevels.Clear();
        }

        private void Scan()
        {
            List<Vessel> vessels = FlightGlobals.VesselsLoaded;
            if (vessels == null) return;

            tweaker.BeginScan();

            for (int v = 0; v < vessels.Count; v++)
            {
                Vessel vessel = vessels[v];
                if (vessel == null || vessel.parts == null) continue;

                int level = EngineerBonus.GetBestEngineerLevel(vessel);
                LogVesselLevel(vessel, level);

                bool changed = false;

                for (int p = 0; p < vessel.parts.Count; p++)
                {
                    Part part = vessel.parts[p];
                    if (part == null || part.Modules == null) continue;

                    for (int m = 0; m < part.Modules.Count; m++)
                    {
                        // Deckt ModuleEngines und ModuleEnginesFX ab (FX leitet davon ab).
                        ModuleEngines engine = part.Modules[m] as ModuleEngines;
                        if (engine == null) continue;

                        if (tweaker.Apply(engine, level)) changed = true;
                        LogBurn(engine);
                    }
                }

                if (changed) MarkDeltaVDirty(vessel);
            }

            tweaker.EndScan();
        }

        /// <summary>Stock-Delta-v-Anzeige zur Neuberechnung zwingen.</summary>
        private static void MarkDeltaVDirty(Vessel vessel)
        {
            if (vessel.VesselDeltaV == null) return;
            vessel.VesselDeltaV.SetCalcsDirty(true);
        }

        /// <summary>Messwerte aus dem laufenden Triebwerk - zeigt, ob der Bonus real ankommt.</summary>
        private void LogBurn(ModuleEngines engine)
        {
            if (!Settings.DebugLog) return;
            if (!engine.EngineIgnited || engine.currentThrottle <= 0f) return;

            Log.Debugging(string.Format(
                "{0} brennt: realIsp {1:F1} s, Schub {2:F1} kN, Durchfluss {3:F4}, Drossel {4:P0}",
                EngineTweaker.Describe(engine), engine.realIsp, engine.finalThrust,
                engine.fuelFlowGui, engine.currentThrottle));
        }

        /// <summary>Einmalige Log-Zeile je Schiff, wenn sich das erkannte Level aendert.</summary>
        private void LogVesselLevel(Vessel vessel, int level)
        {
            if (!Settings.DebugLog) return;

            int previous;
            if (vesselLevels.TryGetValue(vessel.id, out previous) && previous == level) return;
            vesselLevels[vessel.id] = level;

            Log.Debugging(string.Format("{0}: bester Ingenieur Level {1} -> {2:P1} Ersparnis. Crew: {3}",
                vessel.vesselName, level, EngineerBonus.GetFuelSaving(level),
                EngineerBonus.DescribeCrew(vessel)));
        }
    }
}
