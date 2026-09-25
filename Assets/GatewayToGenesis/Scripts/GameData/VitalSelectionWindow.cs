/// <summary>Selection window for the Vital Resource click button (see <see cref="ResourceSelectionWindow"/>).</summary>
public class VitalSelectionWindow : ResourceSelectionWindow
{
    protected override string TargetButton => "VitalResource";

    // Bound to the HUD button's OnClick in the scene.
    public void OpenForVitalResources() => Open();
}
