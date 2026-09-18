using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Nicht generischer Teil der Basis - haelt, was ohne Modultyp auskommt.
    /// </summary>
    public abstract class ThrusterTweaker
    {
        /// <summary>Beschriftung fuers Log: der Name des Teils, an dem das Modul haengt.</summary>
        public static string Describe(PartModule module)
        {
            return module != null && module.part != null && module.part.partInfo != null
                ? module.part.partInfo.title
                : "unbekanntes Teil";
        }
    }

    /// <summary>
    /// Setzt den Bonus am Schubmodul und haelt dessen Originalwerte fest.
    /// Gemeinsam genutzt von Flug- und Bauhof-Controller, fuer Triebwerke
    /// (<see cref="EngineTweaker"/>) wie fuer RCS-Duesen (<see cref="RcsTweaker"/>).
    ///
    /// Umsetzung bei einer Ersparnis p - in beiden Modularten dieselben zwei Werte:
    ///   atmosphereCurve *= 1 / (1 - p)    -> Isp steigt auf jeder Hoehe
    ///   maxFuelFlow     *= (1 - p)        -> Durchfluss sinkt um genau p
    /// Weil Schub = Durchfluss * Isp * g0 gilt, bleibt der Schub unveraendert und es wird
    /// exakt p weniger Treibstoff verbraucht.
    ///
    /// Bewusst NICHT ueber multIsp/multFlow (Triebwerk) bzw. ispMult/flowMult (RCS): diese
    /// Felder werden von den Delta-v-Rechnungen (Stock-Stufenanzeige, KER, MechJeb) nicht
    /// gelesen, der Bonus bliebe dort unsichtbar. Keines der benutzten Felder wird in
    /// Spielstaende geschrieben.
    ///
    /// Die Unterschiede zwischen den Modularten stecken in den wenigen abstrakten Zugriffen
    /// weiter unten - die Buchfuehrung darueber, welches Modul gerade angefasst ist und was
    /// vorher dort stand, gibt es nur einmal.
    /// </summary>
    public abstract class ThrusterTweaker<TModule> : ThrusterTweaker where TModule : PartModule
    {
        private sealed class Tracked
        {
            public FloatCurve BaseCurve;
            public double BaseMaxFlow;

            /// <summary>Bleibt 0, wo das Modul keinen Mindestdurchfluss kennt (RCS).</summary>
            public double BaseMinFlow;

            public int AppliedLevel = -1;
        }

        private readonly Dictionary<TModule, Tracked> tracked = new Dictionary<TModule, Tracked>();
        private readonly HashSet<TModule> seen = new HashSet<TModule>();
        private readonly List<TModule> removals = new List<TModule>();

        /// <summary>Wort fuers Log, damit dort steht, welche Art Duese gemeint ist.</summary>
        protected abstract string Kind { get; }

        protected abstract bool IsExcluded(TModule module);

        protected abstract FloatCurve GetCurve(TModule module);
        protected abstract void SetCurve(TModule module, FloatCurve curve);

        protected abstract double GetMaxFlow(TModule module);
        protected abstract void SetMaxFlow(TModule module, double value);

        /// <summary>Mindestdurchfluss - nur das Triebwerk hat einen, RCS laesst es dabei.</summary>
        protected virtual double GetMinFlow(TModule module)
        {
            return 0d;
        }

        protected virtual void SetMinFlow(TModule module, double value)
        {
        }

        /// <summary>Beginn eines Durchlaufs - danach jedes gefundene Modul durch Apply schicken.</summary>
        public void BeginScan()
        {
            seen.Clear();
        }

        /// <summary>Gibt true zurueck, wenn am Modul etwas geaendert wurde.</summary>
        public bool Apply(TModule module, int level)
        {
            if (module == null) return false;
            seen.Add(module);

            // Ohne Isp-Kurve laesst sich nichts umrechnen. An Stock-Teilen kommt das nicht
            // vor, ein kaputt gepatchtes Teil soll den Durchlauf aber nicht abbrechen.
            FloatCurve current = GetCurve(module);
            if (current == null) return false;

            if (IsExcluded(module)) level = 0;

            Tracked state;
            if (!tracked.TryGetValue(module, out state))
            {
                state = new Tracked
                {
                    BaseCurve = current,
                    BaseMaxFlow = GetMaxFlow(module),
                    BaseMinFlow = GetMinFlow(module)
                };
                tracked.Add(module, state);
            }

            float saving = EngineerBonus.GetFuelSaving(level);
            double expectedMaxFlow = state.BaseMaxFlow * (1f - saving);

            // Erneut setzen, wenn das Level sich geaendert hat ODER wenn ein anderer
            // Codepfad (Triebwerksmodus, Upgrades) unsere Werte ueberschrieben hat.
            bool levelChanged = state.AppliedLevel != level;
            bool drifted = Differs(GetMaxFlow(module), expectedMaxFlow);
            if (!levelChanged && !drifted) return false;

            SetMaxFlow(module, expectedMaxFlow);
            SetMinFlow(module, state.BaseMinFlow * (1f - saving));
            SetCurve(module, saving > 0f
                ? ScaleCurve(state.BaseCurve, 1f / (1f - saving))
                : state.BaseCurve);

            state.AppliedLevel = level;

            FloatCurve applied = GetCurve(module);
            Log.Debugging(string.Format(
                "{0} {1}: Level {2} -> {3:P1} Ersparnis (maxFuelFlow {4:F5}, Isp vac {5:F1} s, ASL {6:F1} s){7}",
                Kind, Describe(module), level, saving, GetMaxFlow(module),
                applied.Evaluate(0f), applied.Evaluate(1f),
                drifted && !levelChanged ? " [neu gesetzt, war ueberschrieben]" : string.Empty));

            return true;
        }

        /// <summary>Ende eines Durchlaufs - nicht mehr gesehene Module zuruecksetzen.</summary>
        public void EndScan()
        {
            removals.Clear();

            foreach (KeyValuePair<TModule, Tracked> entry in tracked)
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
            foreach (KeyValuePair<TModule, Tracked> entry in tracked)
            {
                Restore(entry.Key, entry.Value);
            }

            tracked.Clear();
            seen.Clear();
            removals.Clear();
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

        private void Restore(TModule module, Tracked state)
        {
            // Unity-Objekte werden beim Entladen zu "null" - dann gibt es nichts zurueckzusetzen.
            if (module == null || state == null) return;

            SetCurve(module, state.BaseCurve);
            SetMaxFlow(module, state.BaseMaxFlow);
            SetMinFlow(module, state.BaseMinFlow);
        }

        /// <summary>
        /// Vergleich mit Toleranz. Der Sollwert wird in double gerechnet, am Triebwerk steht
        /// er als float - dieser Rundungsunterschied darf nicht als fremde Aenderung
        /// durchgehen. Ein anderer Mod, der das Feld anfasst, liegt um Groessenordnungen
        /// weiter daneben.
        /// </summary>
        private static bool Differs(double actual, double expected)
        {
            double tolerance = Math.Max(Math.Abs(expected), 1e-9) * 1e-5;
            return Math.Abs(actual - expected) > tolerance;
        }
    }
}
