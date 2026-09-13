using System.Collections.Generic;
using UnityEngine;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Setzt den Bonus am Triebwerk und haelt dessen Originalwerte fest.
    /// Gemeinsam genutzt von Flug- und Bauhof-Controller.
    ///
    /// Umsetzung bei einer Ersparnis p:
    ///   atmosphereCurve *= 1 / (1 - p)    -> Isp steigt auf jeder Hoehe
    ///   maxFuelFlow     *= (1 - p)        -> Durchfluss sinkt um genau p
    /// Weil Schub = Durchfluss * Isp * g0 gilt, bleibt der Schub unveraendert und es wird
    /// exakt p weniger Treibstoff verbraucht.
    ///
    /// Bewusst NICHT ueber multIsp/multFlow: diese Felder werden von den Delta-v-Rechnungen
    /// (Stock-Stufenanzeige, KER, MechJeb) nicht gelesen, der Bonus bliebe dort unsichtbar.
    /// Keines der benutzten Felder wird in Spielstaende geschrieben.
    /// </summary>
    public class EngineTweaker
    {
        private sealed class TrackedEngine
        {
            public FloatCurve BaseCurve;
            public float BaseMaxFuelFlow;
            public float BaseMinFuelFlow;
            public int AppliedLevel = -1;
        }

        private readonly Dictionary<ModuleEngines, TrackedEngine> tracked =
            new Dictionary<ModuleEngines, TrackedEngine>();
        private readonly HashSet<ModuleEngines> seen = new HashSet<ModuleEngines>();
        private readonly List<ModuleEngines> removals = new List<ModuleEngines>();

        /// <summary>Beginn eines Durchlaufs - danach jedes gefundene Triebwerk durch Apply schicken.</summary>
        public void BeginScan()
        {
            seen.Clear();
        }

        /// <summary>Gibt true zurueck, wenn am Triebwerk etwas geaendert wurde.</summary>
        public bool Apply(ModuleEngines engine, int level)
        {
            if (engine == null) return false;
            seen.Add(engine);

            if (EngineerBonus.IsExcluded(engine)) level = 0;

            TrackedEngine state;
            if (!tracked.TryGetValue(engine, out state))
            {
                state = new TrackedEngine
                {
                    BaseCurve = engine.atmosphereCurve,
                    BaseMaxFuelFlow = engine.maxFuelFlow,
                    BaseMinFuelFlow = engine.minFuelFlow
                };
                tracked.Add(engine, state);
            }

            float saving = EngineerBonus.GetFuelSaving(level);
            float expectedMaxFlow = state.BaseMaxFuelFlow * (1f - saving);

            // Erneut setzen, wenn das Level sich geaendert hat ODER wenn ein anderer
            // Codepfad (Triebwerksmodus, Upgrades) unsere Werte ueberschrieben hat.
            bool levelChanged = state.AppliedLevel != level;
            bool drifted = !Mathf.Approximately(engine.maxFuelFlow, expectedMaxFlow);
            if (!levelChanged && !drifted) return false;

            engine.maxFuelFlow = expectedMaxFlow;
            engine.minFuelFlow = state.BaseMinFuelFlow * (1f - saving);
            engine.atmosphereCurve = saving > 0f
                ? ScaleCurve(state.BaseCurve, 1f / (1f - saving))
                : state.BaseCurve;

            state.AppliedLevel = level;

            Log.Debugging(string.Format(
                "{0}: Level {1} -> {2:P1} Ersparnis (maxFuelFlow {3:F5}, Isp vac {4:F1} s, ASL {5:F1} s){6}",
                Describe(engine), level, saving, engine.maxFuelFlow,
                engine.atmosphereCurve.Evaluate(0f), engine.atmosphereCurve.Evaluate(1f),
                drifted && !levelChanged ? " [neu gesetzt, war ueberschrieben]" : string.Empty));

            return true;
        }

        /// <summary>Ende eines Durchlaufs - nicht mehr gesehene Triebwerke zuruecksetzen.</summary>
        public void EndScan()
        {
            removals.Clear();

            foreach (KeyValuePair<ModuleEngines, TrackedEngine> entry in tracked)
            {
                if (seen.Contains(entry.Key)) continue;
                removals.Add(entry.Key);
            }

            for (int i = 0; i < removals.Count; i++)
            {
                Restore(removals[i], tracked[removals[i]]);
                tracked.Remove(removals[i]);
            }
        }

        public void RestoreAll()
        {
            foreach (KeyValuePair<ModuleEngines, TrackedEngine> entry in tracked)
            {
                Restore(entry.Key, entry.Value);
            }

            tracked.Clear();
            seen.Clear();
            removals.Clear();
        }

        public static string Describe(ModuleEngines engine)
        {
            return engine != null && engine.part != null && engine.part.partInfo != null
                ? engine.part.partInfo.title
                : "unbekanntes Triebwerk";
        }

        /// <summary>Kopie der Isp-Kurve mit skalierten Stuetzstellen und Tangenten.</summary>
        private static FloatCurve ScaleCurve(FloatCurve source, float factor)
        {
            FloatCurve scaled = new FloatCurve();
            Keyframe[] keys = source.Curve.keys;

            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe key = keys[i];
                scaled.Add(key.time, key.value * factor, key.inTangent * factor, key.outTangent * factor);
            }

            return scaled;
        }

        private static void Restore(ModuleEngines engine, TrackedEngine state)
        {
            // Unity-Objekte werden beim Entladen zu "null" - dann gibt es nichts zurueckzusetzen.
            if (engine == null || state == null) return;

            engine.atmosphereCurve = state.BaseCurve;
            engine.maxFuelFlow = state.BaseMaxFuelFlow;
            engine.minFuelFlow = state.BaseMinFuelFlow;
        }
    }
}
