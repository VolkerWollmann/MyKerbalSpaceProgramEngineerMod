using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Regelwerk: bester Ingenieur an Bord zaehlt, 1 % Ersparnis pro Sterne-Level.
    /// Mit <see cref="Settings.RequireCommandPod"/> zaehlt er nur, wenn er in einer
    /// Kommandokapsel oder im externen Kommandositz sitzt - wer bloss mitfliegt, ohne an
    /// die Hebel zu kommen, spart nichts.
    /// </summary>
    public static class EngineerBonus
    {
        /// <summary>Nicht lokalisiert - so steht es auch in der persistent.sfs (trait = Engineer).</summary>
        public const string EngineerTrait = "Engineer";

        /// <summary>Hoechstes Ingenieurs-Level an Bord, 0 wenn kein Ingenieur mitfliegt.</summary>
        public static int GetBestEngineerLevel(Vessel vessel)
        {
            if (vessel == null) return 0;
            return GetBestEngineerLevel(GetVesselCrew(vessel));
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
        /// Massgebliche Besatzung eines Schiffs im Flug: mit <see cref="Settings.RequireCommandPod"/>
        /// nur die Kerbals an den Hebeln (Kommandokapsel oder Freisitz), sonst die ganze Besatzung.
        /// </summary>
        public static List<ProtoCrewMember> GetVesselCrew(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!Settings.RequireCommandPod) return vessel.GetVesselCrew();

            List<ProtoCrewMember> crew = new List<ProtoCrewMember>();

            List<Part> parts = vessel.parts;
            if (parts == null) return crew;

            for (int p = 0; p < parts.Count; p++)
            {
                Part part = parts[p];
                if (part == null || !IsCommandPart(part)) continue;

                List<ProtoCrewMember> partCrew = part.protoModuleCrew;
                if (partCrew == null) continue;

                for (int i = 0; i < partCrew.Count; i++)
                {
                    if (partCrew[i] != null) crew.Add(partCrew[i]);
                }
            }

            return crew;
        }

        /// <summary>
        /// Besatzung aus dem Crew-Manifest des Bauhofs. Leer, solange nichts zugewiesen ist.
        /// Mit <see cref="Settings.RequireCommandPod"/> zaehlen nur Kommandokapseln und Freisitze.
        /// </summary>
        public static List<ProtoCrewMember> GetEditorCrew()
        {
            VesselCrewManifest manifest = ShipConstruction.ShipManifest;
            if (manifest == null) return null;

            if (!Settings.RequireCommandPod) return manifest.GetAllCrew(false);

            List<ProtoCrewMember> crew = new List<ProtoCrewMember>();

            // Das Manifest kennt die Sitze, aber nicht die Module - ob ein Teil einen Platz an
            // den Hebeln bietet, steht deshalb am Teil im Bauhof.
            ShipConstruct ship = EditorLogic.fetch != null ? EditorLogic.fetch.ship : null;
            if (ship == null || ship.parts == null) return crew;

            for (int p = 0; p < ship.parts.Count; p++)
            {
                Part part = ship.parts[p];
                if (part == null || !IsCommandPart(part)) continue;

                PartCrewManifest partManifest = manifest.GetPartCrewManifest(part.craftID);
                if (partManifest == null) continue;

                // Ein Eintrag je Sitz, leere Sitze sind null.
                ProtoCrewMember[] partCrew = partManifest.GetPartCrew();
                if (partCrew == null) continue;

                for (int i = 0; i < partCrew.Length; i++)
                {
                    if (partCrew[i] != null) crew.Add(partCrew[i]);
                }
            }

            return crew;
        }

        /// <summary>
        /// Platz an den Hebeln. Das sind zum einen die Teile mit <c>ModuleCommand</c> -
        /// Kapseln, Cockpits und die Cupola -, zum anderen der externe Kommandositz.
        /// Mitfahrer-Kabinen und das Labor zaehlen nicht.
        ///
        /// Der Freisitz traegt kein ModuleCommand, seine Steuerung kommt vom Kerbal selbst.
        /// Deshalb zaehlen hier drei Modularten, je nachdem, wo der Kerbal gerade gefuehrt
        /// wird: <c>KerbalSeat</c> ist der Sitz, wie ihn das Crew-Manifest im Bauhof kennt,
        /// <c>KerbalEVA</c> das Teil des Sitzenden, das im Flug am Sitz haengt und laut
        /// Spielstand die Zeile "crew = ..." traegt. Doppelt gezaehlt wird dabei nichts:
        /// im Bauhof gibt es kein EVA-Teil, im Flug ist der Sitz selbst leer.
        ///
        /// Ein Kerbal, der frei im All schwebt, ist ein eigenes Schiff ohne Triebwerke -
        /// sein EVA-Teil kann also keinem anderen Schiff einen Bonus verschaffen.
        /// </summary>
        public static bool IsCommandPart(Part part)
        {
            if (part == null || part.Modules == null) return false;

            for (int m = 0; m < part.Modules.Count; m++)
            {
                PartModule module = part.Modules[m];
                if (module is ModuleCommand) return true;
                if (module is KerbalSeat) return true;
                if (module is KerbalEVA) return true;
            }

            return false;
        }

        /// <summary>Beschriftung fuers Log, damit dort steht, welche Besatzung gezaehlt wurde.</summary>
        public static string CrewLabel
        {
            get { return Settings.RequireCommandPod ? "Crew an den Hebeln" : "Crew"; }
        }

        /// <summary>
        /// Ersparnis als Anteil. Der Zuschlag aus <see cref="Settings.LevelOffset"/> wird vor
        /// der Deckelung aufgeschlagen: Level 1 spart so viel wie sonst Level 2, Level 4 und 5
        /// landen beide beim Maximum. Ohne Erfahrung (Level 0) bleibt es bei 0 %.
        /// </summary>
        public static float GetFuelSaving(int level)
        {
            if (level <= 0) return 0f;

            int effectiveLevel = Mathf.Min(level + Settings.LevelOffset, Settings.MaxLevel);
            return Mathf.Clamp(Settings.FuelSavingPerLevel * effectiveLevel, 0f, Settings.MaxFuelSaving);
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
            return DescribeCrew(GetVesselCrew(vessel));
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
