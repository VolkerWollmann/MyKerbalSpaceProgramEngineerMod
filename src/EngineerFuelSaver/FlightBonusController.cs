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
        private readonly RcsTweaker rcsTweaker = new RcsTweaker();
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
            rcsTweaker.RestoreAll();
            vesselLevels.Clear();
        }

        private void Scan()
        {
            List<Vessel> vessels = FlightGlobals.VesselsLoaded;
            if (vessels == null) return;

            tweaker.BeginScan();
            rcsTweaker.BeginScan();

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
                        PartModule module = part.Modules[m];

                        // Deckt ModuleEngines und ModuleEnginesFX ab (FX leitet davon ab).
                        ModuleEngines engine = module as ModuleEngines;
                        if (engine != null)
                        {
                            if (tweaker.Apply(engine, level)) changed = true;
                            LogBurn(engine);
                            continue;
                        }

                        // Dasselbe fuer RCS: ModuleRCS deckt auch ModuleRCSFX ab. Die
                        // Stock-Delta-v-Anzeige rechnet RCS nicht mit, deshalb bleibt
                        // "changed" davon unberuehrt - sonst wuerde sie ohne Grund neu rechnen.
                        ModuleRCS rcs = module as ModuleRCS;
                        if (rcs != null && Settings.IncludeRcs)
                        {
                            rcsTweaker.Apply(rcs, level);
                            LogBurn(rcs);
                        }
                    }
                }

                if (changed) MarkDeltaVDirty(vessel);
            }

            tweaker.EndScan();

            // Bei includeRcs = False laeuft der Durchlauf leer durch und setzt damit
            // zurueck, was zuvor womoeglich schon gesetzt war.
            rcsTweaker.EndScan();
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
                ThrusterTweaker.Describe(engine), engine.realIsp, engine.finalThrust,
                engine.fuelFlowGui, engine.currentThrottle));
        }

        /// <summary>
        /// Dasselbe an der feuernden RCS-Duese. realISP und die Schubkraefte je Duesenmuendung
        /// rechnet KSP selbst - sie zeigen, ob der angehobene Isp im Spiel ankommt. Die
        /// Summe steht in ModuleRCS zwar fertig, ist dort aber privat, also hier noch einmal.
        /// </summary>
        private void LogBurn(ModuleRCS rcs)
        {
            if (!Settings.DebugLog) return;
            if (!rcs.rcs_active || rcs.flameout) return;

            float thrust = TotalThrust(rcs);
            if (thrust <= 0f) return;

            Log.Debugging(string.Format(
                "{0} feuert: realISP {1:F1} s, Schub {2:F2} kN",
                ThrusterTweaker.Describe(rcs), rcs.realISP, thrust));
        }

        /// <summary>Schub aller Muendungen einer RCS-Duese, 0 wenn sie gerade nicht feuert.</summary>
        private static float TotalThrust(ModuleRCS rcs)
        {
            float[] forces = rcs.thrustForces;
            if (forces == null) return 0f;

            float total = 0f;
            for (int i = 0; i < forces.Length; i++) total += forces[i];
            return total;
        }

        /// <summary>Einmalige Log-Zeile je Schiff, wenn sich das erkannte Level aendert.</summary>
        private void LogVesselLevel(Vessel vessel, int level)
        {
            if (!Settings.DebugLog) return;

            int previous;
            if (vesselLevels.TryGetValue(vessel.id, out previous) && previous == level) return;
            vesselLevels[vessel.id] = level;

            Log.Debugging(string.Format("{0}: bester Ingenieur Level {1} -> {2:P1} Ersparnis. {3}: {4}",
                vessel.vesselName, level, EngineerBonus.GetFuelSaving(level),
                EngineerBonus.CrewLabel, EngineerBonus.DescribeCrew(vessel)));
        }
    }
}
