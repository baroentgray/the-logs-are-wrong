# AGENTS.md

1. Прочитай pillars, scope, state machine, network rules и тикет.
2. Проверь run context и профиль.
3. Не меняй дизайн, authority model или state machine без решения.
4. Никаких silent refactor.
5. Брёвна не покидают рельсы.
6. Клиент отправляет intent; хост меняет state.
7. Отстойник необратим; процедурная позиция обратима.
8. Gate 1 domain не зависит от UnityEngine.
9. Сначала тест/контракт, затем код.
10. Сцены/prefab/package manifest — только по разрешению.
11. Не мержи main.
12. Неясность: BLOCKED или Architecture Desk; слабый профиль — NEEDS_STRONGER_PROFILE.
13. Перед сменой модели: тесты, commit, handoff.
14. В handoff перечисли edge cases и что не трогал.
