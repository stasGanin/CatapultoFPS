---
paths:
  - "Assets/Scripts/UI/**"
  - "Assets/Resources/UI/**"
---

# UI

- Тема: `InventoryUiTheme` — нейтральные тёмные панели, светлый текст (`TextPrimary/TextSecondary`), акцент `Accent/TabActive`. Спрайты `frame_*` белые: цвет задаётся через `Image.color`.
- Кнопки/плитки: `StylePanel`, `StyleButton`, `StyleTile`, `ApplyInteractionTint` (отклик — множитель к базовому цвету, без двойного тонирования). `pixelsPerUnitMultiplier` не трогать (=1).
- Новые экраны собирать через `UiFactory`; масштаб канвасов — `UiScale.Configure` (match 0.5).
- `frame_panel_large` имеет прозрачное поле 76 px: под него свёрстаны отступы ~100 в CraftUI/MageUI.
- Сообщения на экране — `GameMessages.Post`; хитмаркер — `CombatFeedback`.
