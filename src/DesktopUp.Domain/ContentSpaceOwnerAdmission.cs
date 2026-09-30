namespace DesktopUp.Domain;

/// <summary>Classifies facts supplied by the existing outer owner. A reduced poster frame or
/// changed generation alone is not proof that the physical workspace cannot fit an Item.</summary>
public static class ContentSpaceOwnerAdmission
{
    public static ContentSpaceCause Classify(ContentSpaceWitness witness, ContentLayoutSnapshot snapshot,
        ContentSpaceDecision? published, double intendedCrossFrameDip)
    {
        if (witness.Environment is not { IsValid: true } environment || !witness.EffectiveFrame.IsValid)
            return ContentSpaceCause.AuthoritativeRefresh;
        bool vertical = snapshot.FlowAxis == ContentFlowAxis.Vertical;
        double scale = (vertical ? environment.DpiX : environment.DpiY) / 96d;
        double chrome = (vertical ? witness.EffectiveFrame.Width : witness.EffectiveFrame.Height) / scale -
            (vertical ? witness.ContentWidthDip : witness.ContentHeightDip);
        double physical = (vertical ? environment.WorkArea.Width : environment.WorkArea.Height) / scale;
        bool insufficient = double.IsFinite(chrome) && chrome >= 0 && physical - chrome < snapshot.MinimumCrossAxisDip;
        if (published is not null && published.Witness.Environment != environment && insufficient)
            return ContentSpaceCause.PassivePhysicalEnvironmentChange;
        if (published is null && insufficient && double.IsFinite(intendedCrossFrameDip) &&
            intendedCrossFrameDip - chrome >= snapshot.MinimumCrossAxisDip)
            return ContentSpaceCause.ColdStartPhysicalShortage;
        return ContentSpaceCause.AuthoritativeRefresh;
    }
}
