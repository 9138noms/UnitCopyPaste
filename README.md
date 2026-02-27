# UnitCopyPaste

Nuclear Option 미션 에디터용 유닛 그룹 복사/붙여넣기 BepInEx 모드

## 기능
- **Ctrl+C** — 선택된 유닛(단일/다중) 복사
- **Ctrl+V** — 마우스 커서 위치에 그룹 붙여넣기 (상대 위치 유지)
- **Ctrl+D** — 제자리 복제 (10m 오프셋)

복사 시 유닛 타입, 팩션, 로드아웃, 웨이포인트, 연료 등 모든 속성이 복사됩니다.

## 설치
1. [BepInEx 5.x](https://github.com/BepInEx/BepInEx) 설치
2. `UnitCopyPaste.dll`을 `BepInEx/plugins/` 폴더에 복사

## 알려진 문제
- 항공기 스킨(livery)은 복사되지 않습니다
- 장식물/엄폐물 등 일부 오브젝트가 지형에 약간 묻혀서 스폰될 수 있습니다 — 에디터에서 살짝 움직이면 자동 보정됩니다
- 함선은 수면 위 35m에서 스폰되어 낙하합니다

## 빌드
```
dotnet build -c Release
```

## 요구사항
- Nuclear Option
- BepInEx 5.x
- .NET Framework 4.7.2
