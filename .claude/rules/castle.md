---
paths:
  - "Assets/Scripts/Castle/**"
  - "Assets/Resources/Castle/**"
---

# Замок (каркас, стройка, станции)

- Сетка секций: шаг `CarcassMetrics.GridPitch` 13 м, пол 14×14, колонна 1×3.36, стена 6×3.36×1. Pivot замка — SW-колонна клетки (0,0,0); центр зала = `CellCenterLocal(0,0,0)`.
- Масштаб FBX: каркас/зубцы — по высоте колонны (`MeasureKitScale`), модули стен — `FitHeight` до 3.36. Коллайдеры — по локальным габаритам меша (не мировой AABB повёрнутого модуля!).
- Стены: `CarcassWallBreakable` — целая модель до первого попадания, потом нарезанная (`*Destroyed.fbx`, куски `CastleWallChunk`). <40% кусков → пролом. Смерть мага → `Collapse` всех стен.
- Стройка: `CastleBuildController` (режимы Menu/Placing/Demolish) + `StationBuildTool` (станции). Снос/пристройка возвращают полную стоимость (`RefundSharedWalls`).
- Станции: `StationDefinition` (Resources/Castle/Stations), `StationPlacement` (сетка 1 м внутри секции, OverlapBox с триггерами: дверь, `LadderKeepClear`, маг), `StationFactory` (заглушки — заменить артом здесь).
- Лестница: `CarcassLadder.CreatePair` при добавлении верхней секции (SW-угол, у западной стены).
- Дверь: `InteractableDoor`, петля у края створки в осях модуля; `DoorwayVolume` по размеру створки.
