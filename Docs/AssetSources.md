# Внешние источники ассетов

Куда смотреть, когда берём арт/окружение/VFX из других проектов. Не копировать чужие репозитории целиком.

---

## 1. Project_2 — террейн, деревья, постпроцесс

**Путь:** `C:\Users\sgani\OneDrive\Documents\GitHub\Project_2`

Отсюда берём **визуал локации T1**: террейн с деревьями и камерные эффекты (URP Volume). Игровой код Project_2 (карты, HUD, таргетинг) **не** тащим.

### Что брать

| Что | Где в Project_2 | Зачем |
|-----|-----------------|-------|
| Сцена-референс | `Assets/Scenes/3DNew.unity` | Террейн + Global Volume + свет, как это выглядит вместе |
| TerrainData | `Assets/Terrain/New Terrain.asset` (~3.3 MB) | Высоты, splat, **tree prototypes**, grass/details |
| Слои земли | `Assets/Terrain/NewLayer*.terrainlayer` + текстуры (`seamless_grass_01.png`, `grass_05_…`, dirt) | Трава / грунт на террейне |
| Деревья (кастом) | `Assets/Trees/` — `tree1_low`, `tree2_low`, `tree3_low`, `tree11_low` | Lowpoly деревья на террейне |
| Nature pack | `Assets/Packages/SimpleNaturePack_URP/` | Деревья, кусты, камни, грибы, пни — пропсы поселений / леса |
| Polytope (lowpoly env) | `Assets/Packages/Polytope Studio/` и дубль `Assets/new/Polytope Studio/` | Деревья/кусты/камни, village modular (заборы, мосты) |
| Volume (основной) | `Assets/Scenes/3DNew/Global Volume Profile.asset` | Постпроцесс сцены 3DNew |
| Volume (nature demo) | `Assets/Packages/SimpleNaturePack_URP/Scenes/PostProcessing/PostProcessing_SimpleNaturePack.asset` | Более «открыточный» look пака |

Вода в `Assets/Terrain/` (`water.shadergraph`, `water_low.fbx`) — опционально, не для первой переноски.

### Что внутри Global Volume (3DNew)

| Эффект | Настройки | Для FPS |
|--------|-----------|---------|
| Tonemapping | ACES | брать |
| Bloom | threshold 0.7, intensity 1.24, HQ | брать (чуть приглушить, если ядра/плазмы выжгут кадр) |
| Color Adjustments | exposure +0.2, тёплый фильтр, saturation +10 | брать |
| White Balance | temperature +10 | брать |
| Split Toning | холодные тени / тёплые света, balance 6 | брать |
| Depth of Field | Bokeh, focus 10, aperture 1.6, focal 72 | **не брать как есть** — в FPS размоет прицел и ближний бой |

Nature-пак дополнительно: Vignette, Chromatic Aberration, более жирный Bloom. Для шутера — слабее, чем в демо-сцене пака.

### Ловушки при переносе

1. В `3DNew` на камере **`Render Post Processing = Off`**. Volume лежит в сцене, но картинка без эффектов, пока на камере игрока не включить PP. В Catapulto сейчас то же: `SampleScene` → камера `m_RenderPostProcessing: 0`.
2. Catapulto раньше генерил свой `T1_Location.asset` (600×600 м). Сейчас в сцене **оригинал** `New Terrain.asset` из Project_2. Пады ставим руками (для тестов — 2 штуки).
3. Деревья живут как **Terrain Tree Prototypes** (внутри TerrainData) и как отдельные префабы в `Assets/Trees/`. Нужны оба: прототипы + меши/материалы.
4. Не копировать весь Project_2: там карточная боёвка, куча UI и дубли паков (`Assets/Packages` vs `Assets/new`).

### Как стыкуется с Catapulto сейчас

- Карта: **тот же** `New Terrain.asset` (покраска splat + деревья как в 3DNew). Меню `Install Project_2 Terrain`.
- Мост: `Assets/Environment/Bridge/bridge_low.fbx` — позиция/поворот/скейл как в 3DNew.
- Вода: `water_low.fbx` + `waterMat` — как в 3DNew.
- Пады: два тестовых (свой / враг), дальше двигаешь руками.
- Камера: URP, постпроцесс выключен.
- Volume / ACES из 3DNew — следующий шаг.

---

## 2. Stylized Projectile Pack 1 — простые снаряды (сейчас)

**Пакет:** `C:\Users\sgani\Downloads\Stylized Projectile Pack 1.unitypackage`  
(~9.1 MB, EffectCore)  
**В проекте:** `Assets/EffectCore/packs/StylizedProjectilePack1/`

Импорт уже сделан (не через меню Unity, файлы лежат в Assets). Демо-сцена: `…/scenes/stylizedProjectilePack 1.unity`

### Формат префабов

Каждый выстрел — **триплет**: `*_Projectile` + `*_MuzzleFlare` + `*_Impact`.  
Три размера: **Small / Medium / Big**.

Семейства:

| Семья | Варианты | Куда в игре (черновик) |
|-------|----------|-------------------------|
| **Bullet** | BlazingRed, DemonPurple, SilverFlare, VenomGreen + GoldFire template | арбалет, башни, обычные враги |
| **Laser** | Blue, Green, Orange, Purple, Red + Red template | турели, «лучи» мага |
| **Plasma** | LightBlue, OceanBlue, PoisonGreen, PurpleHaze, RagingRed + LightBlue template | ручная пушка, более жирные выстрелы T1 |

Ещё: гильзы (`prefabs/Bullet/shells/`) — для FPS не обязательны.

Скрипты демо (`WebDemoAssets`: camera shake, `ECProjectile`) **не** подключать к геймплею. Спавн/урон остаются нашими: `CannonWeapon`, `CrossbowBolt`, `EnemyProjectile`. Префаб — только визуал (дочерний ParticleSystem / подмена примитива).

### Стыковка с кодом сейчас

Сейчас всё примитивы:

- пушка → `Sphere` (`CannonWeapon`)
- арбалет → `Capsule` (`CrossbowBolt`)
- враги → `Sphere` (`EnemyProjectile`)

Пак — VFX-обёртка, не физика. Физика (радиус, масса, lifetime) остаётся в `WeaponConfig`.

### Ловушка: Built-in vs URP

EffectCore пак 1 часто на **Built-in** шейдерах. После импорта в URP часть материалов может стать розовой. Чинить: URP Particle Lit / Unlit, либо Render Pipeline Converter. Не тащить Built-in PP stack.

Templates (`*_Template`) — для перекраски под редкость/стихию, не обязательны в первом проходе.

---

## 3. Toon Projectiles (Hovl) — модные снаряды, сильное оружие

**Пакет:** `C:\Users\sgani\Downloads\Toon Projectiles.unitypackage` (~35 MB, Hovl Studio)  
**В проекте:** `Assets/Hovl Studio/Toon projectiles/`

15 комплектов: `Projectile N` + `Flash N` + `Hit N`. Демо: `…/Demo scene/Toon projectiles.unity`.

Пока **не вешаем** на пушку/арбалет. Резерв под rare+ / мага / босса.

Правило: **Pack 1 = common**, **Toon = rare+**.

---

## 4. FPS-оружие

Общий Maya-файл больше **не режем в рантайме**. Отдельные FBX из Blender.

| Файл | Куда |
|------|------|
| `Assets/Resources/Weapons/Crossbow.fbx` | меш арбалета |
| `Assets/Resources/Weapons/CrossbowView.prefab` | FPS view: Mesh + **Muzzle** (двигать в префабе). Выстрел = позиция Muzzle, направление = его +Z |
| `Assets/Resources/Weapons/HandCannon.fbx` | FPS пушка — ждём экспорт |
| Катапульта | позже, не в руках |

Muzzle правится в префабе (`Catapulto / Weapons / Open Crossbow View Prefab`). Жёлтый гизмо = куда полетит болт. Mesh крутить, пока лук не совпадёт с гизмо.

Исходник арбалета: `C:\Users\sgani\Downloads\Crossbow.fbx` (меш `polySurface54`).  
Старый комбинированный файл: `Assets/Resources/Weapons/CanonBowCatapult_low.fbx` — не используем для рук.

---

## 5. Уже в Catapulto (не из этих источников)

Замковые меши T1: `Assets/Prefabs/CastleParts/` (`wallbot`, `walltop`, `bottower`, `toptower`, `EnemyCore_2`) + trash-префабы.

---

## 6. Статус переноса

| Что | Статус |
|-----|--------|
| Stylized Projectile Pack 1 | **импортирован**, на оружие ещё не повешен |
| Toon Projectiles | **импортирован**, не вешаем |
| Пушка / арбалет из FBX | арбалет: `Crossbow.fbx`. пушка: ждём `HandCannon.fbx` |
| Катапульта из того же FBX | лежит в файле, не подключена |
| Terrain + деревья из Project_2 | **тот же TerrainData**, что в 3DNew. Меню `Install Project_2 Terrain` + 2 тестовых пада |
| Volume / постпроцесс | ещё нет |

---

*Обновлять, когда подвесим VFX или дотащим окружение.*
