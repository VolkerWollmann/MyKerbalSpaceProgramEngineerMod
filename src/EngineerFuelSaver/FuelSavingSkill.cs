using Experience;

namespace EngineerFuelSaver
{
    /// <summary>
    /// Zeigt die Treibstoffersparnis im Faehigkeitenblock des Kerbals an, neben
    /// "Provides repair skills" und den uebrigen Ingenieurs-Faehigkeiten.
    ///
    /// Rein informativ - gerechnet wird in <see cref="EngineTweaker"/>. Der Effekt fuehrt
    /// bewusst keine eigenen modifiers, damit die Prozentwerte nur an einer Stelle stehen:
    /// in der cfg unter fuelSavingPerLevel.
    ///
    /// KSP findet diese Klasse per Reflection ueber die geladenen Assemblies
    /// ("[ExperienceSystem]: Found N effect types"). Angehaengt wird sie ueber die
    /// mitgelieferte EXPERIENCE_TRAIT-Node in EngineerTrait.cfg - derselbe Weg, ueber den
    /// die Serenity-Erweiterung dem Ingenieur Effekte ergaenzt.
    /// </summary>
    public class FuelSavingSkill : ExperienceEffect
    {
        public FuelSavingSkill(ExperienceTrait parent) : base(parent)
        {
        }

        public FuelSavingSkill(ExperienceTrait parent, float[] modifiers) : base(parent, modifiers)
        {
        }

        protected override float GetDefaultValue()
        {
            return 0f;
        }

        protected override string GetDescription()
        {
            // Die Beschreibung wird erst beim Anzeigen gebaut. Statt sich darauf zu
            // verlassen, dass das Settings-Addon schon lief, die cfg notfalls selbst
            // nachladen - das haelt GetDescription unabhaengig von der Szenen-Reihenfolge.
            // In der Praxis startet das MainMenu-Addon vor der Initialisierung des
            // Experience-Systems, die Absicherung greift also selten.
            Settings.EnsureLoaded();
            return Settings.BuildEffectDescription(GetCrewLevel());
        }

        /// <summary>
        /// Level des Kerbals, an dem diese Faehigkeit haengt. Jeder Kerbal hat eine eigene
        /// Instanz, der Text kann also seinen konkreten Wert zeigen.
        /// </summary>
        private int GetCrewLevel()
        {
            // Im Testmodus dasselbe Level wie beim Bonus zeigen, sonst widersprechen sich
            // Anzeige und Wirkung. Der Effekt haengt per Definition an einem Ingenieur,
            // die Pruefung auf einen vorhandenen Ingenieur eruebrigt sich hier also.
            if (Settings.DebugLevelOverride >= 0) return Settings.DebugLevelOverride;

            if (Parent == null) return 0;
            return Parent.CrewMemberExperienceLevel();
        }
    }
}
