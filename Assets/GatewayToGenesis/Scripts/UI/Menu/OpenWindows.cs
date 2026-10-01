/// <summary>
/// Whether any window that Escape closes (or backs out of) is open, so the Escape goes to it and not to the quick menu
/// (<see cref="GameInput.CancelUnclaimed"/>). A new window that listens to <see cref="GameInput.CancelPressed"/> belongs here.
/// </summary>
public static class OpenWindows
{
    public static bool Any => WorldView.IsOpen || OverTheView;

    /// <summary>A window drawn over the capital or the map (every one above but the map itself).</summary>
    public static bool OverTheView =>
        LibraryWindow.IsOpen
        || RumoursWindow.IsOpen
        || BattleWindow.IsOpen
        || BattleEncounterWindow.IsOpen
        || BattleRecoveryWindow.IsOpen
        || SymphonyWindow.IsOpen
        || BestiaryWindow.IsOpen
        || EraTimelineWindow.IsOpen
        || CultureWindow.IsOpen
        || CultureAtlasWindow.IsOpen
        || CultureNamingDialog.IsOpen
        || RecipeInventionDialog.IsOpen
        || EdictsSection.IsOpen
        || BalladJournalView.IsOpen;
}
