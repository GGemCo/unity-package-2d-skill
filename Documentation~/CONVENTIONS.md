# Skill 문서

이 폴더는 **Skill 패키지**의 구조/규칙/변경 절차를 표준화하기 위한 문서입니다.

- Runtime 네임스페이스: `GGemCo2DSkill`
- Editor 네임스페이스: `GGemCo2DSkillEditor`

Unity 공식 문서 참고 링크:
- Assembly Definition(런타임/에디터 분리): https://docs.unity3d.com/6000.3/Documentation/Manual/cus-asmdef.html
- ScriptableObject(데이터 컨테이너/저장 특성): https://docs.unity3d.com/6000.3/Documentation/Manual/class-ScriptableObject.html
- EditorWindow(커스텀 툴): https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorWindow.html
- EditorWindow(UI Toolkit 가이드): https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateEditorWindow.html
- Addressables(패키지): https://docs.unity3d.com/Packages/com.unity.addressables%40latest/
- Addressables(개요): https://docs.unity3d.com/Packages/com.unity.addressables%401.24/manual/AddressableAssetsOverview.html
- Undo(에디터 Undo/Redo): https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Undo.html
- Serialization(직렬화 규칙): https://docs.unity3d.com/Manual/script-Serialization.html


## 1. 이벤트/타임라인 규칙

- 이벤트는 데이터(EventDefinition)와 실행(Runtime Executor)을 분리합니다.
- 이벤트의 튜닝 값은 “속도”보다 “거리/시간”처럼 디자이너 친화적으로 제공합니다.
- Easing 등 곡선은 Core의 공용 Easing을 사용하여 일관성을 유지합니다.

## 2. 타겟팅 규칙

- 타겟 선택은 `Targeting/`에 모읍니다.
- 좌표/경계/레이캐스트 정책은 Core와 공유(중복 구현 금지).
- “타겟 없음” 같은 오류는 시스템 메시지(Localization key)로 표준화합니다.

## 3. 스킬 테스트 툴 규칙

- 테스트 중에는 몬스터 BT 비활성 등 “환경 격리”를 표준으로 제공합니다.
- 테스트 종료 시 원상 복구(몬스터 위치/상태/BT 재개)를 보장합니다.
- 테스트는 Runtime 로직을 우회하지 않고, “표준 진입점”을 사용합니다.

## 4. 저장/데이터

- 스킬 레벨/해금/슬롯은 SaveData로 관리합니다.
- 테이블과 SaveData 간의 키 매핑(UID/Id)은 문서화합니다.
