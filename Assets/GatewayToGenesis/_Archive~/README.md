# Archive (ignored by Unity)

Unity skips folders whose name ends in `~`, so nothing here is imported or compiled.
These scripts were unused: no scene, prefab or asset referenced them, and no live code did.
They are the first-prototype economy (ResourceSO-based managers, the Resource/ProductionEntity
mediator) plus a few unused UI helpers. Each keeps its .meta file, so moving a script back
into Assets/ restores it with the same GUID.

- Scripts/Managers: FoodManager, GameData, HousingManager, MoraleManager, PopulationManager
  (superseded by PopGrowthLogic and StatManager), DialogueManager (only used by Swipe)
- Scripts/Resource & Production Management: prototype economy, only used by TestScript
- Scripts/Test/TestScript: exercised the prototype economy
- Scripts/UI: DataTracker (ResourceSO display), Swipe (card swipe prototype)
