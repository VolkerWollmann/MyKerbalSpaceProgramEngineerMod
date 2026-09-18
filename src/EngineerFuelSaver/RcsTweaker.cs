namespace EngineerFuelSaver
{
    /// <summary>
    /// Bonus an der RCS-Duese. <see cref="ModuleRCS"/> deckt auch ModuleRCSFX ab, das davon
    /// ableitet und nur Effekte ergaenzt - beide Stock-Varianten sind damit erfasst.
    ///
    /// ModuleRCS rechnet anders als ein Triebwerk, das Ergebnis ist aber dasselbe:
    ///   maxFuelFlow = thrusterPower / (Isp(Vakuum) * g0)   einmal beim Laden des Teils
    ///   exhaustVel  = Isp(Hoehe) * g0                      in jedem FixedUpdate neu
    ///   Verbrauch   = maxFuelFlow * Drossel,  Schub = Verbrauch * exhaustVel
    /// Wer also maxFuelFlow mit (1 - p) und die Isp-Kurve mit 1 / (1 - p) skaliert, laesst den
    /// Schub unveraendert und spart genau p Treibstoff - dieselben zwei Werte wie am
    /// Triebwerk. Weil exhaustVel jeden Physikschritt neu aus der Kurve kommt, wirkt die
    /// geaenderte Kurve sofort; im Rechtsklick-Menue der Duese steht der angehobene Wert
    /// unter "realISP".
    ///
    /// Einen Mindestdurchfluss kennt ModuleRCS nicht, GetMinFlow/SetMinFlow bleiben deshalb
    /// bei der Vorgabe der Basisklasse und tun nichts.
    /// </summary>
    public class RcsTweaker : ThrusterTweaker<ModuleRCS>
    {
        protected override string Kind
        {
            get { return "RCS-Duese"; }
        }

        protected override bool IsExcluded(ModuleRCS rcs)
        {
            return EngineerBonus.IsExcluded(rcs);
        }

        protected override FloatCurve GetCurve(ModuleRCS rcs)
        {
            return rcs.atmosphereCurve;
        }

        protected override void SetCurve(ModuleRCS rcs, FloatCurve curve)
        {
            rcs.atmosphereCurve = curve;
        }

        protected override double GetMaxFlow(ModuleRCS rcs)
        {
            return rcs.maxFuelFlow;
        }

        protected override void SetMaxFlow(ModuleRCS rcs, double value)
        {
            rcs.maxFuelFlow = value;
        }
    }
}
