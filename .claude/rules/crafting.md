---
paths:
  - "Assets/Scripts/Crafting/**"
  - "Assets/Scripts/Inventory/**"
  - "Assets/Resources/Crafting/**"
  - "Assets/Resources/Items/**"
  - "Assets/Data/**"
---

# Крафт, инвентарь, изучение

- Рецепт: `CraftRecipe._station` (Hand = меню C, Workbench, Smelter) и `_requiresUnlock`. Каталог — `Resources/Crafting/CraftCatalog`.
- `CraftMenuController.OpenAt(station)`; `CraftUI` показывает рецепты станции, которые знает `PlayerRecipeBook`.
- Изучение: `ResearchNode` (тир, пререквизиты, рецепты, цена) + `ResearchTree` (Resources/Crafting/ResearchTree), окно `ResearchUI`. Предмет-рецепт: `ItemKind.Blueprint` + `_teachesRecipe`, ПКМ в инвентаре → `PlayerInventory.TryUseSlot`.
- Стартовый набор — `Data/Player/PlayerLoadoutConfig` (кладётся только в сумку: хотбар потом перезаписывает `EnemyCombatBootstrap`).
- Иконка предмета: `_icon` или `Resources/ItemIcons/{id}.png`.
