using System.Collections.Generic;
using UnityEngine;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Gleicher Bonus im Bauhof (VAB und SPH), damit die Delta-v-Anzeige schon dort die
    /// zugewiesene Besatzung beruecksichtigt und nicht erst auf der Startrampe.
    ///
    /// Gelesen wird das Crew-Manifest des Bauhofs - also genau die Zuweisung, die der
    /// Spieler im Crew-Dialog vornimmt.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorBonusController : MonoBehaviour
    {
        private readonly EngineTweaker tweaker = new EngineTweaker();
        private readonly RcsTweaker rcsTweaker = new RcsTweaker();

        private float nextScan;
        private int lastLevel = -1;

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
        }

        private void Scan()
        {
            ShipConstruct ship = EditorLogic.fetch != null ? EditorLogic.fetch.ship : null;
            if (ship == null || ship.parts == null)
            {
                // Leerer Bauhof: nichts zu tun, aber angefasste Teile zuruecksetzen.
                tweaker.BeginScan();
                tweaker.EndScan();
                rcsTweaker.BeginScan();
                rcsTweaker.EndScan();
                return;
            }

            int level = EngineerBonus.GetBestEngineerLevelInEditor();
            LogLevel(level);

            tweaker.BeginScan();
            rcsTweaker.BeginScan();
            bool changed = false;

            for (int p = 0; p < ship.parts.Count; p++)
            {
                Part part = ship.parts[p];
                if (part == null || part.Modules == null) continue;

                for (int m = 0; m < part.Modules.Count; m++)
                {
                    PartModule module = part.Modules[m];

                    ModuleEngines engine = module as ModuleEngines;
                    if (engine != null)
                    {
                        if (tweaker.Apply(engine, level)) changed = true;
                        continue;
                    }

                    // RCS wird hier zwar noch nicht verbraucht, der Bonus haengt aber
                    // schon am Teil - so zeigen Werkzeuge wie der RCS Build Aid im Bauhof
                    // dieselben Werte wie spaeter im Flug.
                    ModuleRCS rcs = module as ModuleRCS;
                    if (rcs != null && Settings.IncludeRcs) rcsTweaker.Apply(rcs, level);
                }
            }

            tweaker.EndScan();
            rcsTweaker.EndScan();

            // Nur Triebwerke gehen in die Stock-Delta-v-Anzeige ein, RCS nicht.
            if (changed) MarkDeltaVDirty(ship);
        }

        /// <summary>Delta-v-Anzeige des Bauhofs zur Neuberechnung zwingen.</summary>
        private static void MarkDeltaVDirty(ShipConstruct ship)
        {
            if (ship.vesselDeltaV == null) return;
            ship.vesselDeltaV.SetCalcsDirty(true);
        }

        private void LogLevel(int level)
        {
            if (!Settings.DebugLog) return;
            if (level == lastLevel) return;
            lastLevel = level;

            List<ProtoCrewMember> crew = EngineerBonus.GetEditorCrew();
            Log.Debugging(string.Format("Bauhof: bester Ingenieur Level {0} -> {1:P1} Ersparnis. {2}: {3}",
                level, EngineerBonus.GetFuelSaving(level), EngineerBonus.CrewLabel,
                EngineerBonus.DescribeCrew(crew)));
        }
    }
}
