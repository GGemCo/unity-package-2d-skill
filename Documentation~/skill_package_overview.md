# Skill 패키지 중요한 클래스 정리

이 문서는 현재 업로드된 **Skill 패키지 Runtime / Editor 클래스**를 기준으로, 소스 문서로 바로 활용할 수 있도록 핵심 클래스의 역할을 정리한 문서입니다.

기본 방향은 Skill 패키지 문서의 역할 정의와 동일하게, **스킬 데이터 / 실행 / 타겟팅 / 타임라인 이벤트 / 툴링**을 중심으로 정리했습니다.

---

## 1. 패키지 개요

Skill 패키지는 다음 책임을 담당합니다.

- 스킬 데이터 테이블 로드 및 런타임 정의 변환
- 스킬 실행 파이프라인 관리
- 타겟팅 컨텍스트 구성 및 사거리 판정
- Timeline 기반 이벤트를 런타임 이벤트로 베이크하고 실행
- 데미지 영역, 프로젝타일, 돌진, 상태 적용, VFX 등의 전투 연동
- 스킬 제작/테스트/테이블 편집용 에디터 툴 제공
- 패시브 스킬 장착 및 옵션 적용
- 스킬 UI, 퀵슬롯 UI, 정보 UI 제공

의존성 방향은 패키지 계약상 **Skill → Control, Core**이며, Skill Runtime 에서 Editor 참조를 직접 가지지 않도록 분리되어 있습니다.

---

## 2. Runtime 중요한 클래스

## 2.1 실행 코어

### `SkillExecutor`
**위치:** `Runtime/Core/SkillExecutor.cs`

Skill 패키지의 가장 핵심이 되는 실행기입니다.

주요 역할:
- 스킬 사용 요청 진입점 (`TryUse`)
- `RuntimeSkillDefinition` 조회 및 실행 가능 여부 판단
- `SkillRun` 생성 및 현재 실행 상태 관리
- 이벤트 실행 분기
  - Lunge
  - Projectile
  - Damage
  - Vfx
  - ApplyStatus
- 실행 종료 리포트(`SkillExecutionReport`) 발행
- 취소(`TryCancel`) 처리
- 테스트용 데미지 영역 Gizmo 정리

정리하면, **“스킬 1회 실행을 총괄하는 메인 런타임 오케스트레이터”** 입니다.

---

### `SkillRun`
**위치:** `Runtime/Core/SkillRun.cs`

`SkillExecutor`가 실제로 돌리는 **1회성 스킬 실행 인스턴스**입니다.

주요 역할:
- 런타임 시퀀스 비동기 로드
- 시간 진행(`Tick`)에 따라 이벤트 순차 실행
- 캐스트 시작 / 루프 / 종료 / 사용 애니메이션 재생
- 시전 시작 시점의 컨텍스트 스냅샷 보관
- 액션/모션 컨트롤러에 초기 상태 및 사용 상태 반영
- 종료 / 취소 상태 관리

실행 구조상 `SkillExecutor`가 외부 제어를 담당한다면, `SkillRun`은 **현재 스킬 한 건의 시간축 실행 상태를 들고 있는 클래스**입니다.

---

### `RuntimeSkillDefinition`
**위치:** `Runtime/Core/RuntimeSkillDefinition.cs`

테이블 row(`StruckTableSkill`, `StruckTableSkillMonster`)를 런타임에서 바로 쓰기 좋은 형태로 변환한 정의 객체입니다.

주요 필드:
- `Uid`, `Name`, `Memo`
- `SoFileName`
- `SkillKind`
- `CastTime`, `CoolTime`
- `TargetingMode`, `CastRange`, `PlacementRange`, `MaxTargets`
- `CastStartClip`, `CastLoopClip`, `CastEndClip`, `UseClip`
- `FacingMode`

즉, **테이블 원본과 실행기 사이를 연결하는 런타임 DTO** 역할입니다.

---

### `SkillDefinitionResolver`
**위치:** `Runtime/Core/SkillDefinitionResolver.cs`

스킬 UID를 받아 실제 `RuntimeSkillDefinition`으로 변환해 주는 해석기입니다.

주요 역할:
- 플레이어 스킬 테이블 조회
- 몬스터 스킬 테이블 조회
- 테이블 소스(`Player`, `Monster`)에 따라 적절한 해석 경로 선택

`SkillExecutor`가 직접 테이블 구조를 알지 않도록 분리하는 데 중요한 클래스입니다.

---

### `SkillExecutionReport`
**위치:** `Runtime/Core/SkillExecutionReport.cs`

스킬 실행 결과를 외부에 전달하기 위한 결과 구조체입니다.

용도:
- 성공 / 실패 / 취소 결과 전달
- 드라이버 어댑터나 BT 연동 지점에서 실행 완료 후속 처리

---

### `SkillCost`
**위치:** `Runtime/Core/SkillCost.cs`

현재 구조상 큰 로직 클래스는 아니지만, 스킬의 비용 개념을 분리해 두는 기본 타입입니다.

향후 확장 포인트:
- MP / Stamina / Ammo / Special Resource 비용
- 비용 체크 정책 분리

---

## 2.2 타겟팅 / 사거리

### `SkillTargetContext`
**위치:** `Runtime/Targeting/SkillTargetContext.cs`

스킬 실행 시 필요한 타겟 정보를 하나로 묶는 컨텍스트입니다.

주요 값:
- `caster`
- `lockedTarget`
- `groundPoint`
- `forward`

이 구조 덕분에 타겟팅 모드가 달라도 `SkillExecutor`는 하나의 입력 형식으로 처리할 수 있습니다.

---

### `SkillRangeResolver`
**위치:** `Runtime/Core/SkillRangeResolver.cs`

사거리 / 설치 거리 관련 계산을 담당합니다.

주요 역할:
- 캐스트 거리 계산
- 배치 거리 계산
- 현재 타겟이 사거리 내에 있는지 판정
- 타겟팅 모드별 거리 판정 차이 처리
- 전방 배치 위치 계산

`TableSkill.CastRange`, `PlacementRange`, `TargetingMode`를 실제 런타임 판정으로 연결하는 핵심 클래스입니다.

---

### `SkillEventOverrides`
**위치:** `Runtime/Targeting/SkillEventOverrides.cs`

개별 이벤트 단위에서 타겟팅/범위 값을 덮어쓸 수 있도록 하는 구조체입니다.

주요 용도:
- 특정 이벤트만 다른 타겟팅 모드 사용
- 특정 이벤트의 범위 / 최대 타겟 수 / 추적 여부 변경
- Area ID / 스케일 오버라이드

즉, **스킬 전체 정의와 이벤트별 세부 동작을 분리하는 확장 지점**입니다.

---

### `PlayerLockedTargetProvider`
**위치:** `Runtime/Integration/Targeting/PlayerLockedTargetProvider.cs`

플레이어의 현재 락온 대상 제공자입니다.

주요 역할:
- 플레이어가 바라보거나 락온한 타겟을 Skill 쪽에 공급
- UI 락온 마커 / 입력 시스템과 Skill 패키지 사이 연결점 역할

---

### `PlayerSkillTargetingProvider`
**위치:** `Runtime/Integration/Targeting/PlayerSkillTargetingProvider.cs`

플레이어 스킬 사용 시 최종 `SkillTargetContext`를 구성하는 타겟팅 제공자입니다.

주요 역할:
- 락온 타겟, 지면 타겟, 전방 방향 등을 정리
- 플레이어 입력 계층과 Skill 실행기의 결합도를 낮춤

---

## 2.3 런타임 시퀀스 / Authoring 데이터

### `SkillRuntimeSequence`
**위치:** `Runtime/Authoring/SkillRuntimeSequence.cs`

Timeline을 베이크한 결과물을 담는 `ScriptableObject`입니다.

주요 역할:
- 스킬 UID 보관
- 전체 길이(`Duration`) 보관
- `SkillRuntimeEvent[]` 보관
- 이벤트별 payload 서브에셋 참조 보관

즉, 에디터의 Timeline 작업 결과가 런타임에서 실제 사용되는 **실행용 시퀀스 자산**입니다.

---

### `SkillRuntimeEvent`
**위치:** `Runtime/Authoring/SkillRuntimeEvent.cs`

베이크된 이벤트 1건을 표현하는 경량 구조체입니다.

주요 필드:
- `Type`
- `StartTime`
- `EndTime`
- `Order`
- `PayloadIndex`

`SkillRun`은 시간 경과에 따라 이 이벤트 배열을 순차 처리합니다.

---

### `SkillBakedPayloads`
**위치:** `Runtime/Authoring/SkillBakedPayloads.cs`

베이크 단계에서 사용하는 payload 형식을 정리한 타입 모음입니다.

포함 payload 예시:
- `DamagePayload`
- `SpawnVfxPayload`
- `PlaySfxPayload`
- `ApplyAffectPayload`

역할상 **Timeline 클립 데이터와 런타임 이벤트 실행 데이터 사이의 중간 규격**입니다.

---

## 2.4 이벤트 정의(EventDefinition)

Skill 패키지는 이벤트 정의를 `ScriptableObject` 단위로 분리하고 있습니다. 이 구조는 Timeline Clip ↔ Runtime 실행기를 연결할 때 유지보수성이 좋습니다.

### `DamageEventDefinition`
**위치:** `Runtime/EventDefinition/DamageEventDefinition.cs`

데미지 영역, 계수, 타겟 수, OnHit 후처리 등 **타격 판정 이벤트 정의**를 담당합니다.

관련 구조:
- `OnHitAffectEntry`
- `OnHitCrowdControlEntry`

---

### `ProjectileEventDefinition`
**위치:** `Runtime/EventDefinition/ProjectileEventDefinition.cs`

프로젝타일 발사 이벤트 정의입니다.

주요 역할:
- 발사체 UID/동작 파라미터 정의
- 타겟 또는 전방 발사 정책 연결
- 스킬 Timeline 이벤트와 Projectile 시스템 연결

---

### `LungeEventDefinition`
**위치:** `Runtime/EventDefinition/LungeEventDefinition.cs`

돌진/이동형 이벤트 정의입니다.

주요 역할:
- 거리 기반 이동
- 타겟 추적형 이동
- 스냅샷 기반 / 타겟 추적 기반 이동 정책 분리

Control의 Motion 처리와 충돌하기 쉬운 영역이라, Skill 쪽 이동 이벤트 설계에서 매우 중요합니다.

---

### `VfxEventDefinition`
**위치:** `Runtime/EventDefinition/VfxEventDefinition.cs`

스킬 실행 중 VFX 재생 이벤트를 정의합니다.

주요 역할:
- 어떤 VFX를 어디에 어떤 방식으로 붙일지 정의
- 캐스터 / 타겟 / 지면 기준 위치 선택
- 실행 중 생성된 VFX의 정리 정책 연결

---

### `ApplyStatusEventDefinition`
**위치:** `Runtime/EventDefinition/ApplyStatusEventDefinition.cs`

상태 적용형 이벤트 정의입니다.

주요 역할:
- Affect / Status 계열 시스템과 Skill 이벤트 연결
- 적용 대상, 지속시간, 적용 시점 정의

---

## 2.5 전투 판정

### `IHitEvaluator`
**위치:** `Runtime/Combat/IHitEvaluator.cs`

데미지 영역 평가 인터페이스입니다.

역할:
- 특정 영역 스펙을 기준으로 실제 타겟 목록을 구하는 로직 추상화

덕분에 나중에 Overlap 기반, Raycast 기반, 커스텀 타겟 수집 방식으로 교체하기 쉽습니다.

---

### `AreaHitEvaluator`
**위치:** `Runtime/Combat/AreaHitEvaluator.cs`

현재 기본 구현체로 보이는 데미지 영역 평가기입니다.

주요 역할:
- Collider2D / LayerMask 기반 타격 대상 수집
- 캐시(`DamageAreaProbeCache`) 활용
- ContactFilter2D 기반 필터링

스킬 데미지 판정의 실제 충돌 수집 책임을 가집니다.

---

### `SkillAreaSpec`
**위치:** `Runtime/Combat/SkillAreaSpec.cs`

스킬 영역의 형태와 오프셋을 정의하는 구조체입니다.

주요 내용:
- Shape
- CapsuleDirection
- LocalOffset
- 기본값 보정(`EnsureSaneDefaults`)

데미지 영역 Gizmo와 실제 판정이 같은 기준을 바라보도록 만드는 데 중요합니다.

---

### `DamageAreaProbeCache`
**위치:** `Runtime/Combat/DamageAreaProbeCache.cs`

영역 판정 시 반복 생성 비용을 줄이기 위한 캐시입니다.

역할:
- 프로브용 임시 데이터 재사용
- 빈번한 영역 체크 시 GC 감소

---

## 2.6 캐릭터 / 드라이버 연동

### `PlayerSkillDriverAdapter`
**위치:** `Runtime/Integration/PlayerSkillDriverAdapter.cs`

플레이어 입력 계층과 `SkillExecutor`를 연결하는 어댑터입니다.

주요 역할:
- 스킬 사용 요청 수신
- 쿨다운 관리
- `SkillDriverRequest`를 `SkillTargetContext` 기반 사용 요청으로 변환
- 실행 결과를 `SkillUseResult`로 반환

즉, **플레이어가 실제로 스킬을 누를 때 거치는 Skill 측 입구**입니다.

---

### `MonsterSkillDriverAdapter`
**위치:** `Runtime/Integration/MonsterSkillDriverAdapter.cs`

몬스터 AI/BT 계층과 `SkillExecutor`를 연결하는 어댑터입니다.

주요 역할:
- 몬스터 스킬 사용 요청 처리
- 쿨다운 관리
- 스킬 시작 전 방향 보정
- 실행 완료 리포트 저장 및 소비
- 피격 시 취소 요청 / 액션 취소 연동
- 전투 결과 리포트 브리지

BT와 Skill 사이의 실제 실무 연결점이라 매우 중요합니다.

---

## 2.7 패시브 스킬

### `CharacterPassiveSkillController`
**위치:** `Runtime/Characters/CharacterPassiveSkillController.cs`

패시브 스킬 장착 및 옵션 적용의 중심 클래스입니다.

주요 역할:
- 장착된 패시브 스킬 목록 관리
- 저장 데이터 기준 재구성
- 패시브 옵션 레벨별 재적용
- 적용된 Affect 동기화
- 패시브 기반 임시 HP 정책 처리

액티브 스킬과 별개로, **패시브 시스템의 런타임 중심축**입니다.

---

### `PlayerPassiveSkillController`
**위치:** `Runtime/Characters/Player/PlayerPassiveSkillController.cs`

플레이어 전용 패시브 컨트롤러입니다.

추가 역할:
- 플레이어 초기화 타이밍과 패시브 재적용 시점 조정
- 저장 데이터 로드 후 안정적으로 재구성

초기화 순서 이슈를 완화하기 위한 플레이어 특화 클래스입니다.

---

## 2.8 데이터 로드 / 테이블

### `TableLoaderManagerSkill`
**위치:** `Runtime/TableLoader/TableLoaderManagerSkill.cs`

Skill 패키지의 테이블 로더 집합 관리 클래스입니다.

주요 역할:
- Skill 관련 테이블 로딩 진입점
- 다른 런타임 클래스가 필요한 테이블을 중앙에서 접근하도록 지원

---

### `TableSkill`
**위치:** `Runtime/TableLoader/TableSkill.cs`

플레이어 액티브 스킬 테이블입니다.

주요 컬럼 예시:
- `Uid`, `Name`
- `DefaultLearn`, `NeedPlayerLevel`
- `IconFileName`, `SoFileName`
- `SkillKind`
- `CastTime`, `CoolTime`
- `TargetingMode`, `CastRange`, `PlacementRange`, `MaxTargets`
- `CastStartClip`, `CastLoopClip`, `CastEndClip`, `UseClip`
- `FacingMode`

---

### `TableSkillMonster`
**위치:** `Runtime/TableLoader/TableSkillMonster.cs`

몬스터 액티브 스킬 테이블입니다.

특징:
- 플레이어용과 거의 같은 실행 필드를 가지되
- 학습/레벨 조건 등 플레이어 전용 메타는 제외된 구조

---

### `TableSkillPassive`
**위치:** `Runtime/TableLoader/TableSkillPassive.cs`

패시브 스킬 기본 정보 테이블입니다.

주요 역할:
- 패시브 기본 메타 제공
- 이름 / 아이콘 / 학습 조건 / 종류 제공

---

### `TableSkillPassiveOption`
**위치:** `Runtime/TableLoader/TableSkillPassiveOption.cs`

패시브 옵션 상세 테이블입니다.

주요 역할:
- 패시브 UID + 레벨별 옵션 목록 캐시
- `SkillOptionKind`, `TargetId`, `Op`, `Value`, `Duration` 관리
- `GetOptions(skillPassiveUid, level)` 제공

패시브의 실제 효과량/대상/연산 타입을 제공하는 핵심 테이블입니다.

---

## 2.9 주소 지정 자산 / 로딩 / 패키지 초기화

### `AddressableLoaderSkill`
**위치:** `Runtime/AddressableLoader/AddressableLoaderSkill.cs`

Skill 패키지 Addressables 로딩 진입점입니다.

---

### `AddressableLoaderSettingsSkill`
**위치:** `Runtime/AddressableLoader/AddressableLoaderSettingsSkill.cs`

Skill 설정 자산 로더입니다.

---

### `AddressableLoaderSkillRuntimeSequencePlayer`
### `AddressableLoaderSkillRuntimeSequenceMonster`
**위치:** `Runtime/AddressableLoader/...`

플레이어 / 몬스터용 런타임 시퀀스 자산 로딩 분리를 담당합니다.

---

### `SkillPackageManager`
**위치:** `Runtime/Core/SkillPackageManager.cs`

패키지 초기화, 로더 연결, 런타임 초기 상태 구성 쪽의 허브 성격을 가집니다.

---

### `BootstrapperSkillExecutor`
**위치:** `Runtime/Bootstrapper/BootstrapperSkillExecutor.cs`

씬에서 `SkillExecutor`를 부트스트랩하는 보조 클래스입니다.

---

## 2.10 저장 / 설정 / 로컬라이즈 / 상태 VFX

### `SkillData`
**위치:** `Runtime/SaveData/Data/SkillData.cs`

스킬 관련 저장 데이터 컨테이너입니다.

---

### `SaveDataLoaderSkill`
### `SaveDataManagerSkill`
**위치:** `Runtime/SaveData/...`

Skill 저장 데이터 로드/매니저 계층입니다.

---

### `GGemCoSkillSettings`
**위치:** `Runtime/ScriptableSettings/GGemCoSkillSettings.cs`

Skill 패키지 공통 설정 `ScriptableObject`입니다.

현재 구조상 디버그 옵션과 테스트 허브 동작 설정에도 연결됩니다.

---

### `LocalizationManagerSkill`
### `LocalizationConstantsSkill`
**위치:** `Runtime/Localization/...`

스킬 이름/설명 등 로컬라이즈 자원 연결용 클래스입니다.

---

### `IStatusVfxSystem`
**위치:** `Runtime/Status/IStatusVfxSystem.cs`

상태 이상 또는 스킬 상태 표시용 VFX 시스템 추상화입니다.

Skill이 VFX 구체 구현을 직접 알지 않도록 분리하는 인터페이스입니다.

---

## 2.11 테스트 / 디버그 / UI

### `SkillTestRuntimeHub`
**위치:** `Runtime/Tooling/SkillTestRuntimeHub.cs`

에디터 스킬 테스트 툴과 플레이 모드 런타임을 연결하는 허브입니다.

주요 역할:
- 테스트용 몬스터 스폰/선택/리셋
- 수동 락온 타겟 지정
- GroundPoint / Forward 설정
- 디버그 데미지 영역 기록 관리
- Skill 테스트용 설정 반영

실무상 **CreateSkillWindow의 런타임 쌍**으로 보는 것이 가장 정확합니다.

---

### `SkillDebugAreaRecord`
**위치:** `Runtime/Tooling/SkillDebugAreaRecord.cs`

스킬 데미지 영역 디버그 기록 1건을 담는 데이터입니다.

---

### `SkillTestTargetSnapshot`
**위치:** `Runtime/Tooling/SkillTestTargetSnapshot.cs`

테스트 대상 몬스터의 위치/상태를 저장해 두었다가 리셋할 때 사용하는 스냅샷 데이터입니다.

---

### `UIWindowSkill`
### `UIWindowSkillPassive`
### `UIWindowSkillInfo`
**위치:** `Runtime/UI/Windows/...`

스킬 목록, 패시브 목록, 상세 정보 UI를 담당하는 메인 윈도우들입니다.

---

### `UIElementSkill`
### `UIElementSkillPassive`
**위치:** `Runtime/UI/Windows/...`

각 스킬 슬롯/패시브 슬롯 개별 UI 요소입니다.

---

### `UIIconSkill`
### `UIIconSkillPassive`
**위치:** `Runtime/UI/Core/Icon/...`

아이콘 표시와 마우스 상호작용을 담당하는 아이콘 컴포넌트입니다.

---

### 퀵슬롯 연동 클래스
**위치:** `Runtime/UI/Windows/WindowQuickSlot/...`

중요 클래스:
- `DragDropStrategyQuickSlotSkill`
- `DragDropStrategyQuickSlotSkillPassive`
- `SetIconHandlerQuickSlotSkill`
- `SetIconHandlerQuickSlotSkillPassive`

역할:
- 퀵슬롯 Drag & Drop 연결
- 스킬/패시브 아이콘 표시 전략 분리

---

## 3. Editor 중요한 클래스

## 3.1 메인 제작 툴

### `CreateSkillWindow`
**위치:** `Editor/GGemCoTool/CreateSkill/CreateSkillWindow.cs`

Skill 패키지 에디터 툴의 핵심 창입니다.

주요 역할:
- 플레이어 / 몬스터 스킬 테이블 선택
- 선택된 스킬 row 표시 및 수정
- 시전자 / 타겟 / 실행 패널 표시
- Timeline 베이크 실행
- 플레이 모드 테스트 UI 제공

현재 구조상 Skill 제작, 테스트, 일부 데이터 수정이 한곳에 모여 있는 **대표 툴 윈도우**입니다.

---

### `SkillTimelineBaker`
**위치:** `Editor/GGemCoTool/CreateSkill/Bake/SkillTimelineBaker.cs`

Timeline 자산을 `SkillRuntimeSequence`로 베이크하는 핵심 클래스입니다.

주요 역할:
- Timeline Track/Clip 순회
- Clip 데이터를 `SkillRuntimeEvent + Payload`로 변환
- ScriptableObject 서브에셋 정리
- 기존 베이크 자산 갱신

즉, **에디터 Authoring 데이터 → 런타임 실행 자산** 변환기입니다.

---

## 3.2 CreateSkillWindow 패널 분리

다음 파일들은 `CreateSkillWindow`의 partial 구조로 보이며, 기능을 패널 단위로 나누고 있습니다.

### `SkillPanelSelection`
- 스킬 선택 영역
- 플레이어/몬스터 소스 전환
- 드롭다운 재구성

### `SkillPanelRowEditor`
- 현재 선택된 스킬 row 편집
- 캐시 row와 실제 테이블 저장 흐름의 핵심 구간

### `SkillPanelCaster`
- 테스트용 시전자 선택/표시

### `SkillPanelTarget`
- 테스트용 타겟, 락온 대상, GroundPoint, Forward 제어

### `SkillPanelExecutor`
- 실제 스킬 실행 버튼, 실행 상태 표시

### `SkillPanelTimelineBake`
- Timeline 베이크 실행과 결과 안내

이 구조는 유지보수 측면에서 적절하며, 향후에는 각 패널을 서비스 객체로 더 분리해도 좋습니다.

---

## 3.3 Timeline Authoring 클래스

### `SkillEventTrack`
**위치:** `Editor/GGemCoTool/CreateSkill/Timeline/SkillEventTrack.cs`

Skill 이벤트 전용 Timeline Track 입니다.

역할:
- 여러 Skill 이벤트 클립을 하나의 Track으로 묶음
- Mixer 생성

---

### `SkillEventClipBase`
**위치:** `Editor/GGemCoTool/CreateSkill/Timeline/SkillEventClipBase.cs`

모든 스킬 이벤트 클립의 공통 베이스입니다.

역할:
- 공통 Timeline Clip 계약 제공
- 베이크 시 공통적으로 읽을 수 있는 기반 형식 제공

---

### `SkillEventMixerBehaviour`
**위치:** `Editor/GGemCoTool/CreateSkill/Timeline/SkillEventMixerBehaviour.cs`

Track Mixer Behaviour 입니다.

실행보다는 Track/Clip 구성을 Timeline 시스템에 연결하는 쪽의 의미가 큽니다.

---

### 개별 Clip 클래스
**위치:** `Editor/GGemCoTool/CreateSkill/Timeline/Clips/...`

중요 클래스:
- `SkillApplyAffectClip`
- `SkillDamageClip`
- `SkillLungeClip`
- `SkillPlayAudioClip`
- `SkillProjectileClip`
- `SkillSpawnVfxClip`

역할:
- 각 이벤트 타입에 필요한 authoring 필드 제공
- 베이크 시 해당 EventDefinition / Payload 생성에 필요한 데이터 원본 제공

즉, 디자이너 입장에서는 이 Clip들이 **실제 스킬 타임라인 제작 단위**입니다.

---

## 3.4 테스트 런타임 브리지

### `SkillTestRuntimeHubEditorFacade`
**위치:** `Editor/GGemCoTool/CreateSkill/SkillTestRuntimeHubEditorFacade.cs`

에디터 창과 런타임 허브(`SkillTestRuntimeHub`)를 이어주는 브리지입니다.

---

### `SkillTestRuntimeHubAutoSpawner`
**위치:** `Editor/GGemCoTool/CreateSkill/SkillTestRuntimeHubAutoSpawner.cs`

플레이 모드 테스트 시 필요한 허브 오브젝트 자동 생성 보조 클래스입니다.

---

### `SkillToolRuntimeBridgeGizmoDrawer`
**위치:** `Editor/GGemCoTool/Debug/SkillToolRuntimeBridgeGizmoDrawer.cs`

스킬 테스트 중 디버그 시각화 보조 클래스입니다.

---

## 3.5 테이블 / Addressables / 설정 툴

### `SkillTableEditorModule`
**위치:** `Editor/GGemCoTool/TableEditor/SkillTableEditorModule.cs`

공용 TableEditorWindow에 Skill 테이블 정의를 등록하는 모듈입니다.

주요 역할:
- Skill 관련 테이블 정의 목록 제공
- 헤더별 참조 관계 정의

Skill 테이블 편집 환경을 전체 Table Editor 시스템에 연결하는 핵심 모듈입니다.

---

### `AddressableEditorSkill`
**위치:** `Editor/GGemCoTool/Addressables/AddressableEditorSkill.cs`

Skill 패키지의 Addressables 설정을 다루는 메인 에디터 창입니다.

---

### `SettingSkill`
### `SettingTableSkill`
### `SettingScriptableObjectSkill`
**위치:** `Editor/GGemCoTool/Addressables/...`

Skill 패키지 Addressables 그룹/라벨/경로/테이블 설정을 구성하는 보조 설정 클래스들입니다.

---

### `ConfigEditorSkill`
**위치:** `Editor/GGemCoTool/Config/ConfigEditorSkill.cs`

Skill 패키지 에디터 설정 상수/도우미 역할의 클래스입니다.

---

## 3.6 씬 연동 / 기본 에디터 창

### `DefaultEditorWindowSkill`
**위치:** `Editor/GGemCoTool/DefaultEditorWindowSkill.cs`

Skill 계열 에디터 창들의 공통 기반 클래스입니다.

역할:
- 선택 캐릭터 관리
- 공통 OnEnable / 도구 창 초기화 흐름 제공
- Game 씬 의존 도구의 기반 기능 제공

---

### `DefaultSceneEditorSkill`
### `SceneEditorGameSkill`
### `SceneEditorLoadingSkill`
**위치:** `Editor/GGemCoTool/Scene/...`

Skill 패키지가 사용하는 씬 전환 / 기본 씬 세팅용 에디터 클래스입니다.

---

## 3.7 패시브 테스트 툴

### `UsePassiveSkill`
**위치:** `Editor/GGemCoTool/Test/UsePassiveSkill.cs`

패시브 스킬 테스트 전용 에디터 창입니다.

주요 역할:
- 패시브 스킬 선택
- 장착 대상 캐릭터 지정
- 레벨별 장착/해제/초기화
- 옵션 미리보기
- 테이블 리로드

패시브 시스템을 독립적으로 검증할 수 있는 실용적인 테스트 도구입니다.

---

## 4. 우선적으로 기억해야 할 핵심 클래스 요약

실무에서 가장 먼저 알아야 하는 클래스를 추리면 다음과 같습니다.

### Runtime 핵심 1순위
- `SkillExecutor`
- `SkillRun`
- `RuntimeSkillDefinition`
- `SkillDefinitionResolver`
- `SkillTargetContext`
- `SkillRangeResolver`
- `PlayerSkillDriverAdapter`
- `MonsterSkillDriverAdapter`
- `CharacterPassiveSkillController`
- `TableSkill`, `TableSkillMonster`, `TableSkillPassive`, `TableSkillPassiveOption`

### Editor 핵심 1순위
- `CreateSkillWindow`
- `SkillTimelineBaker`
- `SkillEventClipBase` 및 개별 Clip들
- `SkillTableEditorModule`
- `UsePassiveSkill`

---

## 5. 클래스 관계를 한 문장으로 정리하면

- **테이블**은 `TableSkill / TableSkillMonster / TableSkillPassive / TableSkillPassiveOption`이 담당합니다.
- **런타임 정의 변환**은 `SkillDefinitionResolver`, `RuntimeSkillDefinition`이 담당합니다.
- **실행 전체 제어**는 `SkillExecutor`가 담당합니다.
- **실행 1건의 시간 흐름**은 `SkillRun`이 담당합니다.
- **타겟팅과 사거리**는 `SkillTargetContext`, `SkillRangeResolver`가 담당합니다.
- **전투 판정**은 `AreaHitEvaluator`, `SkillAreaSpec`이 담당합니다.
- **플레이어/몬스터 연결**은 `PlayerSkillDriverAdapter`, `MonsterSkillDriverAdapter`가 담당합니다.
- **패시브 효과 적용**은 `CharacterPassiveSkillController`가 담당합니다.
- **에디터 제작 툴**은 `CreateSkillWindow`가 중심입니다.
- **Timeline → 런타임 자산 변환**은 `SkillTimelineBaker`가 담당합니다.

---

## 6. 권장 읽기 순서

Skill 패키지를 처음 다시 읽을 때는 아래 순서를 권장합니다.

1. `TableSkill`, `TableSkillMonster`
2. `RuntimeSkillDefinition`, `SkillDefinitionResolver`
3. `SkillTargetContext`, `SkillRangeResolver`
4. `SkillExecutor`
5. `SkillRun`
6. `DamageEventDefinition`, `ProjectileEventDefinition`, `LungeEventDefinition`, `VfxEventDefinition`
7. `PlayerSkillDriverAdapter`, `MonsterSkillDriverAdapter`
8. `CharacterPassiveSkillController`, `TableSkillPassive`, `TableSkillPassiveOption`
9. `CreateSkillWindow`
10. `SkillTimelineBaker`와 각종 Timeline Clip

이 순서로 보면 Skill 패키지의 데이터 흐름과 실행 흐름을 가장 빠르게 파악할 수 있습니다.

---

## 7. 부록: 구조적으로 중요한 포인트

- Skill 패키지는 **테이블 기반 정의 + Timeline 기반 이벤트 + 런타임 실행기** 3축으로 구성되어 있습니다.
- 액티브 스킬과 패시브 스킬은 같은 패키지 안에 있지만, 런타임 중심 클래스는 분리되어 있습니다.
- 플레이어/몬스터는 공통 실행기를 공유하되, 드라이버 어댑터와 테이블 소스로 분기합니다.
- Editor 쪽은 `CreateSkillWindow` 중심의 제작 툴과 `SkillTimelineBaker` 중심의 베이크 파이프라인으로 정리됩니다.
- 유지보수 우선순위는 일반적으로 **SkillExecutor → SkillRun → Table/Resolver → Editor Baker** 순으로 높습니다.

