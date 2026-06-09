using System;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 스킬 실행을 담당하는 실행기입니다.
    /// 스킬 정의 조회, 이벤트 실행, 피격 판정, 이동, 이펙트, 상태이상 적용, 취소를 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillExecutor : MonoBehaviour, IGameInitializable, IGameActivatable, IGameDeinitializable
    {
        // 아래 위치 캡처 메서드는 기존 Lunge 주석과 분리해 XML 문서가 섞이지 않도록 둡니다.
        /// <summary>
        /// 피격 판정에 사용할 레이어 마스크입니다.
        /// </summary>
        [Header("Hit Evaluator")]
        [SerializeField] private LayerMask hitMask = ~0;

        /// <summary>
        /// 범위 기반 타겟 판정을 수행하는 평가기입니다.
        /// </summary>
        private IHitEvaluator _hitEvaluator;

        /// <summary>
        /// 현재 실행 중인 스킬 런타임입니다.
        /// </summary>
        private SkillRun _current;

        /// <summary>
        /// 현재 스킬 실행에서 생성한 취소 가능 VFX를 추적하고 정리합니다.
        /// </summary>
        private readonly SkillOwnedVfxTracker _ownedVfxTracker = new();

        /// <summary>
        /// 런타임 이벤트 타입을 실제 실행 로직으로 전달하는 디스패처입니다.
        /// </summary>
        private readonly SkillEventDispatcher _eventDispatcher = new();

        /// <summary>
        /// 스킬 화면 페이드 재생과 종료 시 정리 정책을 관리합니다.
        /// </summary>
        private readonly SkillScreenFadeController _screenFadeController = new();

        /// <summary>
        /// 스킬 캐스터 페이드 재생과 종료 시 복구 정책을 관리합니다.
        /// </summary>
        private readonly SkillCasterFadeController _casterFadeController = new();

        /// <summary>
        /// 스킬 캐릭터 잔상 재생과 종료 시 정리 정책을 관리합니다.
        /// </summary>
        private readonly SkillAfterimageController _afterimageController = new();

        /// <summary>
        /// 그라운드슬램 애니메이션과 공중 대기 후 낙하 전환 상태를 관리합니다.
        /// </summary>
        private readonly SkillGroundSlamAnimationController _groundSlamAnimationController = new();

        /// <summary>
        /// 아크 런지의 단계별 애니메이션 상태를 관리합니다.
        /// </summary>
        private readonly SkillArcLungeAnimationController _arcLungeAnimationController = new();

        /// <summary>
        /// 현재 스킬 실행에서 생성한 더미 캐릭터를 actorKey 기준으로 관리하는 컬렉션입니다.
        /// </summary>
        private readonly Dictionary<string, SkillDummyActorHandle> _dummyActors = new(StringComparer.Ordinal);

        /// <summary>
        /// Move/Animation 이벤트에서 Caster를 대상으로 선택했을 때 재사용하는 임시 핸들입니다.
        /// 실제 더미 레지스트리에는 등록하지 않습니다.
        /// </summary>
        private readonly SkillDummyActorHandle _casterActorHandle = new()
        {
            ActorKey = SkillDummyActorReferenceUtility.CasterActorKey,
        };

        /// <summary>
        /// 현재 스킬 실행 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsBusy => _current != null;

        /// <summary>
        /// 현재 실행 중인 스킬 UID입니다. 실행 중이 아니면 0입니다.
        /// </summary>
        public int CurrentSkillUid => _current != null ? _current.SkillUid : 0;

        /// <summary>
        /// 스킬 실행 종료 리포트가 발생했을 때 외부 어댑터에 알립니다.
        /// </summary>
        public event System.Action<SkillExecutionReport> ExecutionFinished;

        /// <summary>
        /// 현재 스킬의 차징 상태가 변경될 때 UI/디버그 도구에 알립니다.
        /// </summary>
        public event System.Action<SkillChargeSnapshot> ChargeStateChanged;

        /// <summary>
        /// 캐릭터 초기화 이후 기본 단계에서 실행기를 준비합니다.
        /// </summary>
        public int InitializeOrder => 0;

        private bool _hasPendingFinishReport;
        private bool _isInitialized;
        private bool _isActivated;
        private SkillExecutionReport _pendingFinishReport;
        private int _executionSequence;
        private readonly SkillAttackSequence _attackSequence = new();
        private CharacterHitStopController _hitStopController;

        /// <summary>
        /// 실행기에 필요한 로컬 참조만 준비합니다.
        /// 실제 사용 가능 상태는 명시적 Initialize/Activate 단계에서 열립니다.
        /// </summary>
        private void Awake()
        {
            CacheLocalReferences();
        }

        /// <summary>
        /// 스킬 실행에 필요한 평가기와 캐릭터 보조 컨트롤러를 준비합니다.
        /// </summary>
        /// <param name="context">초기화 컨텍스트입니다. SkillExecutor는 캐릭터 로컬 참조를 사용하므로 null을 허용합니다.</param>
        public void Initialize(GameInitContext context)
        {
            if (_isInitialized)
                return;

            CacheLocalReferences();
            _isInitialized = true;
        }

        /// <summary>
        /// 캐릭터와 드라이버 연결 이후 스킬 사용과 런타임 Tick을 허용합니다.
        /// </summary>
        /// <param name="context">초기화 컨텍스트입니다.</param>
        public void Activate(GameInitContext context)
        {
            if (!_isInitialized)
                Initialize(context);

            if (!_isInitialized)
                return;

            _isActivated = true;
            enabled = true;
        }

        /// <summary>
        /// 스킬 사용을 차단하고 진행 중인 스킬을 시스템 취소로 정리합니다.
        /// </summary>
        public void Deinitialize()
        {
            _isActivated = false;
            if (_current != null)
                TryCancel(SkillCancelReason.ForcedBySystem);
        }

        /// <summary>
        /// 동일 GameObject에서 스킬 실행에 필요한 로컬 컴포넌트와 평가기를 캐싱합니다.
        /// </summary>
        private void CacheLocalReferences()
        {
            _hitEvaluator ??= new AreaHitEvaluator(hitMask);
            _hitStopController = GetComponent<CharacterHitStopController>();
        }

        /// <summary>
        /// 실행기가 비활성화될 때 남아 있는 런타임 리소스를 정리하고 진행 중인 스킬을 강제 취소합니다.
        /// </summary>
        private void OnDisable()
        {
            SkillExecutorCleanupUtility.CleanupOnDisable(
                this,
                _dummyActors,
                _casterActorHandle,
                _casterFadeController,
                _afterimageController);

            if (_current == null)
                return;

            TryCancel(SkillCancelReason.ForcedBySystem);
        }

        /// <summary>
        /// 현재 실행 중인 스킬 런타임을 프레임 단위로 갱신합니다.
        /// 실행이 완료되면 현재 런타임 참조를 해제합니다.
        /// </summary>
        private void Update()
        {
            if (!_isInitialized || !_isActivated)
                return;

            if (_hitStopController == null)
            {
                _hitStopController = GetComponent<CharacterHitStopController>();
            }

            if (_hitStopController != null && _hitStopController.IsActive)
                return;

            if (_current != null)
            {
                var run = _current;
                run.Tick(Time.deltaTime);
                if (ReferenceEquals(_current, run) && run.IsDone)
                {
                    NotifyRunEnded(run);
                }
            }

            _groundSlamAnimationController.Tick();
            _arcLungeAnimationController.Tick();
            MaintainDummyAirborneState();
        }

        /// <summary>
        /// 지정한 스킬 UID의 실행을 시도합니다.
        /// 스킬 정의를 조회하고 실행 컨텍스트를 구성한 뒤 새 <see cref="SkillRun"/>을 시작합니다.
        /// </summary>
        /// <param name="skillUid">실행할 스킬의 고유 식별자입니다.</param>
        /// <param name="targetCtx">캐스터, 타겟, 지면 위치, 방향 정보를 포함한 대상 컨텍스트입니다.</param>
        /// <param name="source">스킬 정의를 조회할 테이블 출처입니다.</param>
        /// <returns>스킬 실행이 시작되면 <see langword="true"/>, 실행할 수 없으면 <see langword="false"/>를 반환합니다.</returns>
        public bool TryUse(int skillUid, SkillTargetContext targetCtx, ConfigCommon.SkillTableSource source = ConfigCommon.SkillTableSource.Player)
        {
            if (!_isInitialized)
                Initialize(null);
            if (!_isActivated)
                Activate(null);

            if (!_isInitialized || !_isActivated) return false;
            if (_current != null) return false;

            if (!SkillDefinitionResolver.TryResolve(skillUid, source, out var skill) || skill == null) return false;

            SkillExecutorCleanupUtility.PrepareForNewRun(
                this,
                _ownedVfxTracker,
                _dummyActors,
                _casterActorHandle,
                _attackSequence,
                _screenFadeController,
                _casterFadeController,
                _afterimageController,
                _groundSlamAnimationController,
                _arcLungeAnimationController);

            var motion = SkillCharacterComponentResolver.ResolveMotionController(targetCtx.caster);
            motion?.CancelMotion(MotionChannel.Skill, 2001);

            if (targetCtx.caster != null)
            {
                var rb = targetCtx.caster.GetComponentInParent<Rigidbody2D>();
                if (rb != null)
                    rb.SetLinearVelocity(Vector2.zero);
            }

            _current = new SkillRun(this, skill, targetCtx,
                SkillCharacterComponentResolver.ResolveAnimationController(targetCtx.caster),
                SkillCharacterComponentResolver.ResolveActionController(targetCtx.caster));
            _hasPendingFinishReport = false;
            _current.Start();
            return true;
        }


        /// <summary>
        /// 여러 플레이어 스킬을 하나의 대표 스킬 애니메이션에 맞춰 동시에 실행합니다.
        /// </summary>
        /// <remarks>
        /// 첫 번째 또는 지정된 대표 스킬은 캐릭터 액션 상태와 Use 애니메이션을 담당하고,
        /// 나머지 스킬은 같은 Use 시작 시점부터 RuntimeSequence 이벤트만 함께 실행합니다.
        /// </remarks>
        /// <param name="entries">검증이 끝난 묶음 스킬 실행 항목입니다.</param>
        /// <param name="primarySkillUid">대표 애니메이션으로 사용할 스킬 UID입니다.</param>
        /// <returns>묶음 실행이 시작되면 <see langword="true"/>입니다.</returns>
        internal bool TryUseBundle(IReadOnlyList<SkillBundleRuntimeEntry> entries, int primarySkillUid)
        {
            if (!_isInitialized)
                Initialize(null);
            if (!_isActivated)
                Activate(null);

            if (!_isInitialized || !_isActivated) return false;
            if (_current != null) return false;
            if (entries == null || entries.Count == 0) return false;

            int primaryIndex = ResolvePrimaryBundleIndex(entries, primarySkillUid);
            if (primaryIndex < 0) return false;

            SkillBundleRuntimeEntry primaryEntry = entries[primaryIndex];
            if (primaryEntry.Skill == null) return false;

            var additionalEntries = new List<SkillBundleRuntimeEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i == primaryIndex || entries[i].Skill == null)
                    continue;

                additionalEntries.Add(entries[i]);
            }

            SkillExecutorCleanupUtility.PrepareForNewRun(
                this,
                _ownedVfxTracker,
                _dummyActors,
                _casterActorHandle,
                _attackSequence,
                _screenFadeController,
                _casterFadeController,
                _afterimageController,
                _groundSlamAnimationController,
                _arcLungeAnimationController);

            var motion = SkillCharacterComponentResolver.ResolveMotionController(primaryEntry.Context.caster);
            motion?.CancelMotion(MotionChannel.Skill, 2002);

            if (primaryEntry.Context.caster != null)
            {
                var rb = primaryEntry.Context.caster.GetComponentInParent<Rigidbody2D>();
                if (rb != null)
                    rb.SetLinearVelocity(Vector2.zero);
            }

            _current = new SkillRun(
                this,
                primaryEntry.Skill,
                primaryEntry.Context,
                SkillCharacterComponentResolver.ResolveAnimationController(primaryEntry.Context.caster),
                SkillCharacterComponentResolver.ResolveActionController(primaryEntry.Context.caster),
                additionalEntries);
            _hasPendingFinishReport = false;
            _current.Start();
            return true;
        }

        /// <summary>
        /// 묶음 실행 요청에서 대표 스킬로 사용할 항목 인덱스를 찾습니다.
        /// </summary>
        /// <param name="entries">묶음 스킬 실행 항목입니다.</param>
        /// <param name="primarySkillUid">대표 스킬 UID입니다.</param>
        /// <returns>대표 스킬 인덱스이며, 찾지 못하면 첫 번째 유효 스킬 인덱스입니다.</returns>
        private static int ResolvePrimaryBundleIndex(IReadOnlyList<SkillBundleRuntimeEntry> entries, int primarySkillUid)
        {
            if (entries == null || entries.Count == 0)
                return -1;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Skill != null && entries[i].Skill.Uid == primarySkillUid)
                    return i;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Skill != null)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 지정한 AttackId가 실제 데미지 확정 시 다음 스킬 연계를 열 수 있는 이벤트인지 확인합니다.
        /// </summary>
        public bool IsChainUnlockAttack(int attackId)
        {
            return _attackSequence.IsChainUnlockAttack(attackId);
        }

        /// <summary>
        /// 런타임 이벤트 유형에 따라 실제 스킬 효과를 실행합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="sequence">이벤트 페이로드를 제공하는 런타임 시퀀스입니다.</param>
        /// <param name="e">실행할 런타임 이벤트 정보입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="timingContext">UseClip 재생 속도와 실제 길이를 반영한 이벤트 시간 보정 정보입니다.</param>
        public void ExecuteEvent(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            SkillRuntimeSequence sequence,
            in SkillRuntimeEvent e,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            SkillRunTimingContext timingContext)
        {
            if (!CanProcessEvent(run))
                return;

            var eventContext = new SkillEventExecutionContext(
                run,
                skill,
                ctx,
                sequence,
                in e,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                timingContext);
            _eventDispatcher.Execute(this, in eventContext);
        }


        /// <summary>
        /// 화면 페이드 이벤트 정의를 Core 공용 화면 페이드 서비스로 전달합니다.
        /// 스킬 종료 또는 취소 시 초기화 정책도 함께 기록합니다.
        /// </summary>
        /// <param name="payloadObj">Bake된 화면 페이드 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        internal void HandleScreenFade(UnityEngine.Object payloadObj, float eventDurationSeconds)
        {
            _screenFadeController.Play(this, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 캐스터 페이드 이벤트 정의를 현재 스킬 캐스터에 적용합니다.
        /// 스킬 종료 또는 취소 시 복구 정책은 <see cref="SkillCasterFadeController"/>가 추적합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 캐스터 페이드 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        internal void HandleCasterFade(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            _casterFadeController.Play(this, ctx, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 캐릭터 잔상 이벤트 정의를 대상 캐릭터의 <c>CharacterAfterimageTrail</c> 컴포넌트로 전달합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 캐릭터 잔상 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        internal void HandleAfterimage(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            if (payloadObj is not SkillAfterimageEventDefinition def)
                return;

            if (!SkillDummyActorReferenceUtility.TryResolveAfterimageTarget(_dummyActors, ctx, def, out var targetObject))
                return;

            _afterimageController.Play(targetObject, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 오디오 이벤트 정의를 바탕으로 스킬 사운드를 재생합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 오디오 이벤트 정의입니다.</param>
        internal void HandlePlayAudio(SkillTargetContext ctx, UnityEngine.Object payloadObj)
        {
            SkillPlayAudioEventHandler.Handle(ctx, payloadObj);
        }

        /// <summary>
        /// 위치 고정 이벤트를 전용 핸들러로 위임합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">위치 고정 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        internal void HandlePositionHold(
            SkillRun run,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            SkillPositionHoldEventHandler.Handle(run, ctx, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 이동 조작 잠금 이벤트를 전용 핸들러로 위임합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이동 조작 잠금 이벤트 Payload입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        internal void HandleMovementControlLock(
            SkillRun run,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            SkillMovementControlLockEventHandler.Handle(run, ctx, payloadObj, eventDurationSeconds);
        }

        /// <summary>
        /// 위치 캡처 이벤트 정의를 바탕으로 현재 타임라인 시점의 기준 위치를 이름 있는 위치 앵커로 저장합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">위치 캡처 이벤트 Payload입니다.</param>
        /// <param name="snapshotCasterPos">스킬 시작 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점의 지면 기준점입니다.</param>
        internal void HandleCaptureTargetPosition(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillCaptureTargetPositionEventHandler.Handle(
                run,
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                _dummyActors);
        }

        /// <summary>
        /// 돌진 이벤트 정의에 따라 캐릭터 이동을 시작합니다.
        /// 2D 방향을 보정하고 직선 또는 호 형태의 이동 요청을 모션 컨트롤러에 전달합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">돌진 이벤트 Payload입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산한 이벤트 지속 시간입니다.</param>
        internal void HandleLunge(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            SkillLungeEventHandler.Handle(
                this,
                skill,
                ctx,
                payloadObj,
                eventDurationSeconds,
                _dummyActors,
                _casterActorHandle,
                _arcLungeAnimationController);
        }

        /// <summary>
        /// 그라운드슬램 이벤트 실행을 전용 핸들러에 위임합니다.
        /// </summary>
        internal void HandleGroundSlam(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            SkillGroundSlamEventHandler.Handle(
                ctx,
                payloadObj,
                eventDurationSeconds,
                _groundSlamAnimationController);
        }

        /// <summary>
        /// 레이저 이벤트 정의를 바탕으로 발사 대상과 좌표를 계산하고 분리된 레이저 시스템을 호출합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">레이저 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        internal void HandleLaser(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillLaserEventHandler.Handle(
                this,
                run,
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                _dummyActors,
                _casterActorHandle,
                gameObject,
                _attackSequence);
        }

        /// <summary>
        /// 투사체 이벤트 정의를 바탕으로 발사 대상과 좌표를 계산하고 투사체를 생성합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">투사체 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        internal void HandleProjectile(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillProjectileEventHandler.Handle(
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                gameObject,
                _attackSequence);
        }

        /// <summary>
        /// 데미지 이벤트 정의를 전용 핸들러에 위임하여 피격 판정과 OnHit 처리를 실행합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">데미지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="gizmoDurationSeconds">에디터 디버그용 데미지 영역 표시 시간입니다.</param>
        internal void HandleDamage(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            float gizmoDurationSeconds)
        {
            SkillDamageEventHandler.Handle(
                run,
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                gizmoDurationSeconds,
                _hitEvaluator,
                gameObject,
                _attackSequence);
        }

        /// <summary>
        /// 이펙트 이벤트 정의를 바탕으로 생성 위치를 계산하고 이펙트를 생성합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이펙트 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        internal void HandleVfx(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillVfxEventHandler.Handle(
                run,
                skill,
                ctx,
                payloadObj,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                _ownedVfxTracker);
        }

        /// <summary>
        /// 상태 적용 이벤트 정의를 바탕으로 대상에게 Affect를 적용합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">상태 적용 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        internal void HandleApplyStatus(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            SkillStatusEventHandler.HandleApplyStatus(ctx, payloadObj);
        }


        /// <summary>
        /// 런타임 Temp HP(비저장 보호막/임시 하트)를 적용합니다.
        /// - 같은 source key가 다시 들어오면 누적하지 않고 설정값까지 다시 채웁니다.
        /// - 현재치가 모두 소모되면 Core 쪽에서 해당 source가 제거됩니다.
        /// </summary>
        internal void HandleApplyTempHp(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj)
        {
            SkillStatusEventHandler.HandleApplyTempHp(skill, ctx, payloadObj);
        }

        /// <summary>
        /// 스킬 이벤트로 더미 캐릭터를 생성합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">스킬 시작 시점 캐스터 위치 스냅샷입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        internal void HandleSpawnDummyCharacter(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not SpawnDummyCharacterEventDefinition def)
                return;

            SkillDummyActorSpawnUtility.TrySpawn(
                this,
                _dummyActors,
                run,
                skill,
                ctx,
                def,
                snapshotCasterPos,
                snapshotTargetPos,
                snapshotGroundPoint,
                out _);
        }

        /// <summary>
        /// 스킬 이벤트로 생성된 더미 캐릭터를 이동시킵니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        internal void HandleMoveDummyCharacter(
            SkillRun run,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not MoveDummyCharacterEventDefinition def)
                return;

            SkillDummyActorMoveEventUtility.TryExecuteMove(
                this,
                _dummyActors,
                _casterActorHandle,
                run,
                ctx,
                def,
                snapshotTargetPos,
                snapshotGroundPoint);
        }

        /// <summary>
        /// 스킬 이벤트로 생성된 더미 캐릭터를 파괴(또는 비활성화)합니다.
        /// </summary>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        internal void HandleDespawnDummyCharacter(UnityEngine.Object payloadObj)
        {
            if (payloadObj is not DespawnDummyCharacterEventDefinition def)
                return;

            SkillDummyActorDespawnEventUtility.TryExecuteDespawn(
                this,
                _dummyActors,
                def);
        }

        /// <summary>
        /// 스킬 이벤트로 더미 캐릭터의 공중 상태(높이/중력)를 제어합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        internal void HandleSetDummyAirborneState(SkillTargetContext ctx, UnityEngine.Object payloadObj)
        {
            if (payloadObj is not SetDummyAirborneStateEventDefinition def)
                return;

            SkillDummyActorAirborneEventUtility.TryExecuteAirborneState(
                this,
                _dummyActors,
                _casterActorHandle,
                ctx,
                def);
        }

        /// <summary>
        /// 더미 캐릭터에 지정한 애니메이션을 재생하고 필요 시 후속 애니메이션 전환을 예약합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">애니메이션 이벤트 정의 페이로드입니다.</param>
        /// <param name="eventDurationSeconds">타임라인 클립 구간에서 계산한 이벤트 지속 시간(초)입니다.</param>
        internal void HandlePlayDummyCharacterAnimation(SkillTargetContext ctx, UnityEngine.Object payloadObj, float eventDurationSeconds)
        {
            if (payloadObj is not PlayDummyCharacterAnimationEventDefinition def)
                return;

            SkillDummyActorAnimationEventUtility.TryExecuteAnimation(
                this,
                _dummyActors,
                _casterActorHandle,
                ctx,
                def,
                eventDurationSeconds);
        }

        /// <summary>
        /// 공중 상태로 유지 중인 더미의 중력/속도/좌표를 프레임마다 보정합니다.
        /// 스킬 런이 끝난 뒤에도 더미가 유지되는 구성에서 물리 오차로 서서히 내려오는 현상을 방지합니다.
        /// </summary>
        private void MaintainDummyAirborneState()
        {
            SkillDummyActorMotionUtility.MaintainAirborneState(_dummyActors);
        }

        /// <summary>
        /// 지정한 런이 현재 활성 런과 동일하고 이벤트를 처리 가능한 상태인지 반환합니다.
        /// </summary>
        internal bool CanProcessEvent(SkillRun run)
        {
            return run != null && ReferenceEquals(_current, run) && !run.IsDone;
        }

        /// <summary>
        /// 차징 상태 변경 스냅샷을 외부 UI/디버그 도구로 전달합니다.
        /// </summary>
        /// <param name="snapshot">현재 차징 상태 스냅샷입니다.</param>
        internal void NotifyChargeStateChanged(SkillChargeSnapshot snapshot)
        {
            ChargeStateChanged?.Invoke(snapshot);
        }

        /// <summary>
        /// 차징 실패가 발생했을 때 현재 실행 결과를 Failed로 예약합니다.
        /// 실패 애니메이션이 끝난 뒤 <see cref="NotifyRunEnded"/>에서 실제 리포트가 발행됩니다.
        /// </summary>
        /// <param name="run">실패한 스킬 런타임입니다.</param>
        internal void NotifyChargeFailed(SkillRun run)
        {
            if (run == null || !ReferenceEquals(_current, run))
                return;

            _pendingFinishReport = new SkillExecutionReport(run.SkillUid, MonsterSkillExecutionState.Failed, ++_executionSequence, Time.time);
            _hasPendingFinishReport = true;
        }

        /// <summary>
        /// 스킬 런 종료 시 현재 실행 참조와 실행 중 생성한 리소스를 정리하고 종료 리포트를 발행합니다.
        /// </summary>
        /// <param name="run">종료된 스킬 런타임입니다.</param>
        internal void NotifyRunEnded(SkillRun run)
        {
            if (run == null || !ReferenceEquals(_current, run))
                return;

            var report = _hasPendingFinishReport
                ? _pendingFinishReport
                : new SkillExecutionReport(run.SkillUid, MonsterSkillExecutionState.Succeeded, ++_executionSequence, Time.time);

            _hasPendingFinishReport = false;
            _current = null;
            SkillExecutorCleanupUtility.CleanupOnRunEnd(
                this,
                _dummyActors,
                _attackSequence,
                _screenFadeController,
                _casterFadeController,
                _afterimageController);
            ExecutionFinished?.Invoke(report);
        }

        /// <summary>
        /// 현재 실행 중인 스킬이 차징 중이면 피격을 차징 게이지 감소로 처리합니다.
        /// 게이지가 0이 되면 내부적으로 실패 애니메이션과 Failed 리포트를 예약합니다.
        /// </summary>
        /// <param name="reason">피격/인터럽트 사유입니다.</param>
        /// <param name="gaugeDamage">감소시킬 차징 게이지 값입니다. 0 이하이면 스킬 설정값을 사용합니다.</param>
        /// <returns>차징 게이지가 해당 피격을 처리했으면 <see langword="true"/>입니다.</returns>
        public bool TryApplyIncomingHitToChargeGauge(SkillCancelReason reason, float gaugeDamage = 0f)
        {
            if (_current == null)
                return false;

            return _current.TryApplyChargeGaugeDamage(reason, gaugeDamage);
        }

        /// <summary>
        /// 현재 실행 중인 스킬의 취소를 시도합니다.
        /// </summary>
        /// <param name="reason">스킬 취소 사유입니다.</param>
        /// <returns>실행 중인 스킬을 취소하면 <see langword="true"/>, 취소할 대상이 없으면 <see langword="false"/>를 반환합니다.</returns>
        public bool TryCancel(SkillCancelReason reason)
        {
            if (_current == null) return false;

            var run = _current;
            SkillExecutorCleanupUtility.CleanupForCancel(
                this,
                run.Caster,
                _ownedVfxTracker,
                _dummyActors,
                _screenFadeController,
                _casterFadeController,
                _afterimageController,
                _groundSlamAnimationController,
                _arcLungeAnimationController);

            _pendingFinishReport = new SkillExecutionReport(
                run.SkillUid,
                MonsterSkillExecutionState.Canceled,
                ++_executionSequence,
                Time.time,
                reason);
            _hasPendingFinishReport = true;

            run.Cancel(reason);
            if (ReferenceEquals(_current, run))
            {
                _current = null; // 즉시 종료(추가 Tick/이벤트 전달 방지)
                _attackSequence.Clear();
                if (_hasPendingFinishReport)
                {
                    var report = _pendingFinishReport;
                    _hasPendingFinishReport = false;
                    ExecutionFinished?.Invoke(report);
                }
            }

            return true;
        }
    }
}
