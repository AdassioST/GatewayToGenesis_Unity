/// <summary>Selection window for the Building Material click button (see <see cref="ResourceSelectionWindow"/>).</summary>
public class ProductionSelectionWindow : ResourceSelectionWindow
{
    protected override string TargetButton => "BuildingMaterial";

    // Bound to the HUD button's OnClick in the scene.
    public void OpenForBuildingMaterials() => Open();
}
