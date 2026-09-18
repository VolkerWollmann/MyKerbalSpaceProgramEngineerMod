namespace EngineerFuelSaver
{
    /// <summary>
    /// Bonus am Haupttriebwerk. <see cref="ModuleEngines"/> deckt auch ModuleEnginesFX ab,
    /// das davon ableitet.
    ///
    /// Rechenweg und Buchfuehrung stehen in <see cref="ThrusterTweaker{TModule}"/>; hier
    /// stehen nur die Felder, unter denen das Triebwerk seine Werte fuehrt. Anders als die
    /// RCS-Duese kennt es zusaetzlich einen Mindestdurchfluss, der mitskaliert werden muss -
    /// sonst laege er nach dem Bonus ueber dem Hoechstdurchfluss.
    /// </summary>
    public class EngineTweaker : ThrusterTweaker<ModuleEngines>
    {
        protected override string Kind
        {
            get { return "Triebwerk"; }
        }

        protected override bool IsExcluded(ModuleEngines engine)
        {
            return EngineerBonus.IsExcluded(engine);
        }

        protected override FloatCurve GetCurve(ModuleEngines engine)
        {
            return engine.atmosphereCurve;
        }

        protected override void SetCurve(ModuleEngines engine, FloatCurve curve)
        {
            engine.atmosphereCurve = curve;
        }

        protected override double GetMaxFlow(ModuleEngines engine)
        {
            return engine.maxFuelFlow;
        }

        protected override void SetMaxFlow(ModuleEngines engine, double value)
        {
            engine.maxFuelFlow = (float)value;
        }

        protected override double GetMinFlow(ModuleEngines engine)
        {
            return engine.minFuelFlow;
        }

        protected override void SetMinFlow(ModuleEngines engine, double value)
        {
            engine.minFuelFlow = (float)value;
        }
    }
}
