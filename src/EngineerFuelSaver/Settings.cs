using System.Collections.Generic;
using UnityEngine;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Liest GameData/EngineerFuelSaver/EngineerFuelSaver.cfg einmalig beim Spielstart.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class Settings : MonoBehaviour
    {
        public const string NodeName = "ENGINEER_FUEL_SAVER";

        /// <summary>Ersparnis je Sterne-Level des besten Ingenieurs (0.01 = 1 %).</summary>
        public static float FuelSavingPerLevel = 0.01f;

        /// <summary>Hoechstes beruecksichtigtes Level. Stock: 5.</summary>
        public static int MaxLevel = 5;

        /// <summary>Harte Obergrenze der Ersparnis, verhindert Division durch 0.</summary>
        public static float MaxFuelSaving = 0.9f;

        /// <summary>Sekunden zwischen zwei Neuberechnungen im Flug.</summary>
        public static float RefreshInterval = 0.5f;

        /// <summary>
        /// Bei deaktivierter Kerbal-Erfahrung (Sandbox) behandelt Stock alle Kerbals als Level 5.
        /// true = genauso verfahren, false = Bonus dort komplett abschalten.
        /// </summary>
        public static bool FullBonusWhenExperienceDisabled = true;

        /// <summary>Triebwerke mit einem dieser Treibstoffe bekommen keinen Bonus (Feststoffbooster).</summary>
        public static readonly HashSet<string> ExcludedPropellants =
            new HashSet<string> { "SolidFuel" };

        /// <summary>
        /// Nur zum Testen: ersetzt das Level des erkannten Ingenieurs. Ein echter Ingenieur
        /// muss weiterhin an Bord sein, sonst gibt es keinen Bonus.
        /// -1 = aus (Normalbetrieb).
        /// </summary>
        public static int DebugLevelOverride = -1;

        /// <summary>
        /// Text der Faehigkeit im Kerbal-Infoblock. Platzhalter:
        /// &lt;saving&gt; Ersparnis dieses Kerbals, &lt;level&gt; sein Level,
        /// &lt;perLevel&gt; Ersparnis pro Level, &lt;maxSaving&gt; maximale Ersparnis,
        /// &lt;maxLevel&gt; hoechstes Level.
        /// Keine geschweiften Klammern verwenden - die beendet der cfg-Parser als Node.
        /// </summary>
        public static string EffectDescription = "FuelSaving:<saving>";

        public static bool DebugLog = false;

        private static bool loaded;

        private void Start()
        {
            DontDestroyOnLoad(this);
            EnsureLoaded();
        }

        /// <summary>
        /// Laedt die cfg beim ersten Aufruf. Noetig, weil <see cref="FuelSavingSkill"/>
        /// schon waehrend des Datenbankladens gebaut wird - lange bevor dieses Addon startet.
        /// </summary>
        public static void EnsureLoaded()
        {
            if (loaded) return;
            if (GameDatabase.Instance == null) return;

            loaded = true;
            Load();
        }

        /// <summary>
        /// Platzhalter durch die tatsaechlich eingestellten Werte ersetzen.
        /// <paramref name="level"/> ist das Level des Kerbals, an dem die Faehigkeit haengt.
        /// </summary>
        public static string BuildEffectDescription(int level)
        {
            string template = EffectDescription;
            if (string.IsNullOrEmpty(template)) return string.Empty;

            // Bewusst Replace statt string.Format: ein Tippfehler in der cfg soll den
            // Spielstart nicht mit einer FormatException abbrechen.
            return template
                .Replace("<saving>", Percent(EngineerBonus.GetFuelSaving(level)))
                .Replace("<level>", level.ToString())
                .Replace("<perLevel>", Percent(FuelSavingPerLevel))
                .Replace("<maxSaving>", Percent(EngineerBonus.GetFuelSaving(MaxLevel)))
                .Replace("<maxLevel>", MaxLevel.ToString());
        }

        /// <summary>Anteil als kurze Prozentangabe: 0.05 wird zu "5%", 0.015 zu "1,5%".</summary>
        private static string Percent(float fraction)
        {
            return (fraction * 100f).ToString("0.##") + "%";
        }

        private static void Load()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(NodeName);
            if (nodes == null || nodes.Length == 0)
            {
                Log.Warn("Keine " + NodeName + "-Node gefunden, benutze Standardwerte.");
                return;
            }

            // Mehrere Nodes (z. B. durch ModuleManager-Patches) werden der Reihe nach angewendet.
            foreach (ConfigNode node in nodes)
            {
                node.TryGetValue("fuelSavingPerLevel", ref FuelSavingPerLevel);
                node.TryGetValue("maxLevel", ref MaxLevel);
                node.TryGetValue("maxFuelSaving", ref MaxFuelSaving);
                node.TryGetValue("refreshInterval", ref RefreshInterval);
                node.TryGetValue("fullBonusWhenExperienceDisabled", ref FullBonusWhenExperienceDisabled);
                node.TryGetValue("debugLog", ref DebugLog);
                node.TryGetValue("debugLevelOverride", ref DebugLevelOverride);
                node.TryGetValue("effectDescription", ref EffectDescription);

                string excluded = null;
                if (node.TryGetValue("excludedPropellants", ref excluded))
                {
                    ExcludedPropellants.Clear();
                    foreach (string name in excluded.Split(','))
                    {
                        string trimmed = name.Trim();
                        if (trimmed.Length > 0) ExcludedPropellants.Add(trimmed);
                    }
                }
            }

            FuelSavingPerLevel = Mathf.Max(0f, FuelSavingPerLevel);
            DebugLevelOverride = Mathf.Clamp(DebugLevelOverride, -1, 5);
            MaxLevel = Mathf.Clamp(MaxLevel, 0, 5);
            MaxFuelSaving = Mathf.Clamp(MaxFuelSaving, 0f, 0.95f);
            RefreshInterval = Mathf.Clamp(RefreshInterval, 0.1f, 10f);

            Log.Info(string.Format(
                "Konfiguration geladen: {0:P1} pro Level, max. Level {1} (= max. {2:P1} Ersparnis).",
                FuelSavingPerLevel, MaxLevel, EngineerBonus.GetFuelSaving(MaxLevel)));

            if (DebugLevelOverride >= 0)
            {
                Log.Warn("debugLevelOverride = " + DebugLevelOverride
                    + " ist aktiv - jeder Ingenieur an Bord zaehlt als Level " + DebugLevelOverride
                    + ", egal welches Level er wirklich hat. Ohne Ingenieur bleibt es bei 0 %. "
                    + "Fuer den Normalbetrieb wieder auf -1 setzen.");
            }
        }
    }
}
