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


## 1. 신규 타임라인 이벤트 추가(Clip/Track)

1) `EventDefinition`에 정의 클래스 추가(필드/검증)
2) Authoring(Clip/Track) 추가
3) Runtime Executor 추가(실제 동작)
4) Editor Tooling(스킬 제작 툴)에서 노출/검증
5) 샘플 타임라인/데이터 추가(가능하면)
6) 테스트
- [ ] 재생/중단/캔슬 시 이벤트 정리(특히 VFX/Projectile)
- [ ] 에디터 재열기/리로드 후에도 동일하게 동작

---

## 2. Lunge(이동) 계열 변경

1) 파라미터: 거리/클립시간/Easing 정의 확인
2) Control/AutoMove/Wall/Guard 등과 충돌 정책 정의
3) 이동 적용 방식(물리/키네마틱/루트모션) 결정
4) 테스트
- [ ] 지상/공중/벽/피격 중 동작
- [ ] 스킬 종료 시 원위치 복귀(테스트 모드) 여부

---

## 3. 타겟팅 확장(예: 이미지에서의 타겟 위치 지정)

1) 타겟 위치 산출 규칙(거리/방향/충돌/스크린 경계) 정의
2) Targeting 모듈에 구현
3) 스킬 이벤트(Projectile/Lunge 등)에서 해당 위치를 입력으로 사용
4) 디버그 표시 옵션 추가
5) 테스트(다양한 맵/카메라/해상도)

---

## 4. 시스템 메시지/Localization 추가

1) 키 추가 + 한/영 문구 추가
2) UI 바인딩(LocalizeStringEvent 등) 확인
3) “키 누락” 시 fallback 정책 확인
