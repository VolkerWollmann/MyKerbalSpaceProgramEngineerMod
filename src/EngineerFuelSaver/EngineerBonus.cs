using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Regelwerk: bester Ingenieur an Bord zaehlt, 1 % Ersparnis pro Sterne-Level.
    /// </summary>
    public static class EngineerBonus
    {
        /// <summary>Nicht lokalisiert - so steht es auch in der persistent.sfs (trait = Engineer).</summary>
        public const string EngineerTrait = "Engineer";

        /// <summary>Hoechstes Ingenieurs-Level an Bord, 0 wenn kein Ingenieur mitfliegt.</summary>
        public static int GetBestEngineerLevel(Vessel vessel)
        {
            if (vessel == null) return 0;
            return GetBestEngineerLevel(vessel.GetVesselCrew());
        }

        /// <summary>
        /// Hoechstes Ingenieurs-Level im Besatzungsmanifest des Bauhofs.
        /// Greift auf dieselbe Zuweisung zu, die der Spieler im Crew-Dialog vornimmt.
        /// </summary>
        public static int GetBestEngineerLevelInEditor()
        {
            return GetBestEngineerLevel(GetEditorCrew());
        }

        public static int GetBestEngineerLevel(List<ProtoCrewMember> crew)
        {
            if (crew == null || crew.Count == 0) return 0;

            bool experienceEnabled = IsKerbalExperienceEnabled();
            if (!experienceEnabled && !Settings.FullBonusWhenExperienceDisabled) return 0;

            bool engineerAboard = false;
            int best = 0;

            for (int i = 0; i < crew.Count; i++)
            {
                ProtoCrewMember member = crew[i];
                if (member == null) continue;
                if (member.trait != EngineerTrait) continue;

                engineerAboard = true;

                // Ohne Erfahrungssystem behandelt Stock jeden Kerbal als voll ausgebildet.
                int level = experienceEnabled ? member.experienceLevel : Settings.MaxLevel;
                if (level > best) best = level;
            }

            // Ohne Ingenieur gibt es keinen Bonus - auch im Testmodus nicht.
            if (!engineerAboard) return 0;

            // Testmodus: ersetzt nur das Level des erkannten Ingenieurs.
            if (Settings.DebugLevelOverride >= 0) best = Settings.DebugLevelOverride;

            return Mathf.Clamp(best, 0, Settings.MaxLevel);
        }

        /// <summary>
        /// Besatzung aus dem Crew-Manifest des Bauhofs. Leer, solange nichts zugewiesen ist.
        /// </summary>
        public static List<ProtoCrewMember> GetEditorCrew()
        {
            VesselCrewManifest manifest = ShipConstruction.ShipManifest;
            if (manifest == null) return null;

            return manifest.GetAllCrew(false);
        }

        /// <summary>Ersparnis als Anteil, z. B. 0.05 bei Level 5 und 1 % pro Level.</summary>
        public static float GetFuelSaving(int level)
        {
            if (level <= 0) return 0f;
            return Mathf.Clamp(Settings.FuelSavingPerLevel * level, 0f, Settings.MaxFuelSaving);
        }

        /// <summary>
        /// Feststoffbooster u. a. sind vom Bonus ausgenommen - an einem brennenden SRB
        /// kann auch ein Ingenieur nichts mehr sparen.
        /// </summary>
        public static bool IsExcluded(ModuleEngines engine)
        {
            if (engine == null) return true;
            if (Settings.ExcludedPropellants.Count == 0) return false;

            List<Propellant> propellants = engine.propellants;
            if (propellants == null) return false;

            for (int i = 0; i < propellants.Count; i++)
            {
                if (propellants[i] != null && Settings.ExcludedPropellants.Contains(propellants[i].name))
                    return true;
            }

            return false;
        }

        /// <summary>Crew-Auflistung fuer das Log, z. B. "Bill Kerman (Engineer Level 0)".</summary>
        public static string DescribeCrew(Vessel vessel)
        {
            return DescribeCrew(vessel != null ? vessel.GetVesselCrew() : null);
        }

        public static string DescribeCrew(List<ProtoCrewMember> crew)
        {
            if (crew == null || crew.Count == 0) return "keine";

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < crew.Count; i++)
            {
                ProtoCrewMember member = crew[i];
                if (member == null) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(member.name).Append(" (").Append(member.trait)
                    .Append(" Level ").Append(member.experienceLevel).Append(")");
            }

            return text.Length > 0 ? text.ToString() : "keine";
        }

        private static bool IsKerbalExperienceEnabled()
        {
            Game game = HighLogic.CurrentGame;
            if (game == null || game.Parameters == null) return true;
            return game.Parameters.CustomParams<GameParameters.AdvancedParams>().EnableKerbalExperience;
        }
    }
}
