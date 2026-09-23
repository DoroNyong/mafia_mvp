# Mafia Game Project Rules

## Game Spec
- 4인 싱글플레이 마피아 게임 (플레이어 1명 + AI 봇 3명)
- 직업: 마피아 1, 경찰 1, 시민 2
- 페이즈: Setup -> Day(토론) -> Vote(투표) -> Night(능력사용) -> WinCheck

## Architecture Guidelines
- 씬 의존성을 줄이기 위해 UI 바인딩은 가능한 코드로 찾거나(`GetComponentInChildren`), 명확한 `UIManager` 클래스를 단일 진입점으로 둘 것.
- 향후 멀티플레이(Server-Authoritative) 전환을 고려해 판정 로직(`GameManager`)과 플레이어 입력/선택 로직을 엄격히 분리할 것.
- 유니티 버전 호환 C# 문법 준수 (최신 C# 10+ 전용 문법 남발 금지).