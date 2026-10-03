---
paths:
  - "Assets/Scripts/Enemies/**"
  - "Assets/Resources/Enemies/**"
---

# Враги

- Решения: `EnemyTargeting` (Exit → Engage/Siege/Guard, опрос 4 Гц, память о игроке). Восприятие: `EnemySenses` (HasClearPath — землю не пропускаем, CastleCenter, GroundBelow от тела, не с +8 м).
- Атаки с телеграфом (`EnemyBodyView`), очереди, `EnemyAttackTokens` (≤2 стрелка одновременно). Bomber — `EnemyBallistics` (решение дуги + проверка). Roller — свип прыжка.
- Урон по стороне: `DamageUtility.ApplyEnemyHit` / `fromPlayer`. Вражеские болты/бомбы проходят сквозь свои замки и юнитов.
- Баланс только в `Resources/Enemies/*Config.asset` (EnemyConfig), игрок — `Resources/Player/PlayerCombatConfig`.
- Спавн по умолчанию выключен (`EnemySpawnDebugHud`, F8).
