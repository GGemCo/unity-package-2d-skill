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


## 1. 역할

Skill은 스킬 데이터/실행/타겟팅/저장/툴링을 담당합니다.

- 스킬 정의(테이블/Config)
- 실행 파이프라인(발동 조건, 쿨다운, 캔슬 등)
- Timeline/Clip 기반 이벤트(예: Lunge, Projectile 발사 등)
- 타겟 선택(Targeting)
- 스킬 테스트/제작 툴(Editor)

## 2. 구성 개요

- `Core/` : 스킬 실행 핵심(컨텍스트/상태/드라이버)
- `EventDefinition/` : 타임라인 이벤트 정의(필드/데이터)
- `Authoring/` : 타임라인/클립 Authoring 지원
- `Tooling/` : 제작/테스트 도구(에디터)
- `Targeting/` : 타겟 선정 규칙(위치/범위/우선순위)
- `Combat/` : 전투 연동(히트, 데미지 등)
- `Config/`, `SaveData/`, `Localization/`, `UI/` : 설정/저장/표시

## 3. 스킬 실행 흐름(표준)

1) 요청(입력/AI) → SkillDriver
2) 조건 체크(쿨다운/자원/상태) → 실행 시작
3) 애니메이션/타임라인 재생
4) 이벤트(Projectile, Lunge, ApplyAffect, CC 등) 발생
5) 종료/캔슬 처리 + 후처리(원위치 복귀 등 테스트 정책 포함)

## 4. 확장 포인트(권장)

- 신규 이벤트는 “EventDefinition + Clip/Track + Runtime Executor” 3단 구성으로 추가합니다.
- Lunge처럼 이동 이벤트는 Control의 Motion/Action과 충돌하지 않도록 “단일 인계 지점”을 둡니다.
