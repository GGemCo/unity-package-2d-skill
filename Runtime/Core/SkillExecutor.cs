using System;
using System.Collections;
using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 스킬 실행을 담당하는 실행기입니다.
    /// 스킬 정의 조회, 이벤트 실행, 피격 판정, 이동, 이펙트, 상태이상 적용, 취소를 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillExecutor : MonoBehaviour
    {
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
        /// 현재 스킬 실행 중 생성된 취소 가능 이펙트 목록입니다.
        /// 취소 시 즉시 정리하여 중단 이후의 잔여 연출을 최소화합니다.
        /// </summary>
        private readonly List<VfxBehaviourBase> _spawnedVfxs = new();

        /// <summary>
        /// 현재 스킬 실행에서 생성한 더미 캐릭터를 actorKey 기준으로 관리하는 컬렉션입니다.
        /// </summary>
        private readonly Dictionary<string, DummyActorHandle> _dummyActors = new(StringComparer.Ordinal);

        /// <summary>
        /// 캐스터를 더미 액터 참조처럼 다루기 위한 내부 식별 키입니다.
        /// </summary>
        private const string CasterActorKey = "__caster__";

        /// <summary>
        /// Move/Animation 이벤트에서 Caster를 대상으로 선택했을 때 재사용하는 임시 핸들입니다.
        /// 실제 더미 레지스트리에는 등록하지 않습니다.
        /// </summary>
        private readonly DummyActorHandle _casterActorHandle = new()
        {
            ActorKey = CasterActorKey,
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

        private bool _hasPendingFinishReport;
        private SkillExecutionReport _pendingFinishReport;
        private int _executionSequence;
        private int _attackSequence;
        private readonly List<int> _resolvedOnHitCrowdControls = new(8);
        private readonly Dictionary<int, bool> _chainUnlockByAttackId = new();
        private CharacterHitStopController _hitStopController;

        private enum GroundSlamAnimationPhaseState
        {
            None = 0,
            Start = 1,
            FallLoop = 2,
            LandEnd = 3,
        }

        private struct GroundSlamAnimationState
        {
            public bool IsActive;
            public GameObject Caster;
            public ICharacterAnimationController AnimationController;
            public ICharacterMotionController MotionController;
            public GroundSlamEventDefinition Definition;
            public GroundSlamAnimationPhaseState Phase;
            public bool UsePhaseBasedLoopTransition;
            public bool IsInstantLandSequence;
            public float PhaseRemainingSeconds;
        }

        private struct PendingGroundSlamState
        {
            public bool IsActive;
            public GameObject Caster;
            public ICharacterMotionController MotionController;
            public GroundSlamEventDefinition Definition;
            public Vector2 StartPosition;
            public Vector2 TargetPosition;
            public float FallDurationSeconds;
            public float HoldRemainingSeconds;
            public bool UsePhaseBasedLoopTransition;
        }

        private GroundSlamAnimationState _groundSlamAnimationState;
        private PendingGroundSlamState _pendingGroundSlamState;

        private enum ArcLungeAnimationPhaseState
        {
            None = 0,
            Rise = 1,
            Apex = 2,
            Fall = 3,
            LandEnd = 4,
        }

        private struct ArcLungeAnimationState
        {
            public bool IsActive;
            public GameObject Caster;
            public ICharacterAnimationController AnimationController;
            public ICharacterMotionController MotionController;
            public ArcLungeEventDefinition Definition;
            public ArcLungeAnimationPhaseState Phase;
            public float ElapsedSeconds;
            public float RiseDurationSeconds;
            public float ApexHoldDurationSeconds;
            public float LandEndRemainingSeconds;
        }

        private ArcLungeAnimationState _arcLungeAnimationState;

        /// <summary>
        /// 스킬 이벤트로 생성한 더미 캐릭터의 런타임 상태를 관리합니다.
        /// </summary>
        private sealed class DummyActorHandle
        {
            /// <summary>
            /// 더미 식별 키입니다.
            /// </summary>
            public string ActorKey;

            /// <summary>
            /// 생성된 더미 캐릭터 인스턴스입니다.
            /// </summary>
            public CharacterBase Character;

            /// <summary>
            /// 스킬 정상 종료 시 자동 제거 여부입니다.
            /// </summary>
            public bool DespawnOnSkillEnd;

            /// <summary>
            /// 스킬 취소 시 자동 제거 여부입니다.
            /// </summary>
            public bool DespawnOnCancel;

            /// <summary>
            /// 진행 중인 페이드 코루틴입니다.
            /// </summary>
            public Coroutine ActiveFadeCoroutine;

            /// <summary>
            /// 진행 중인 이동 보정 코루틴입니다.
            /// </summary>
            public Coroutine ActiveMoveCoroutine;

            /// <summary>
            /// 진행 중인 공중 높이 보정 코루틴입니다.
            /// </summary>
            public Coroutine ActiveAirHeightCoroutine;

            /// <summary>
            /// 진행 중인 애니메이션 후속 전환 코루틴입니다.
            /// </summary>
            public Coroutine ActiveAnimationCoroutine;

            /// <summary>
            /// 애니메이션 후속 전환 요청 버전입니다.
            /// 새 애니메이션 요청이 들어오면 기존 대기 코루틴을 무효화하는 데 사용합니다.
            /// </summary>
            public int AnimationRequestVersion;

            /// <summary>
            /// 지면 기준 이동 좌표입니다. 실제 월드 Y는 이 값에 AirHeight를 더해 계산합니다.
            /// </summary>
            public Vector3 GroundPosition;

            /// <summary>
            /// 지면 기준 공중 높이(+Y)입니다.
            /// </summary>
            public float AirHeight;

            /// <summary>
            /// 더미 캐릭터 제어 잠금 토큰입니다.
            /// </summary>
            public object ControlLockToken;

            /// <summary>
            /// 더미 캐릭터 브레인 잠금 토큰입니다.
            /// </summary>
            public object BrainLockToken;

            /// <summary>
            /// 공중 상태에서 사용하는 중력 오버라이드 컨트롤러입니다.
            /// </summary>
            public CharacterPhysicsOverrideController PhysicsOverrideController;

            /// <summary>
            /// 공중 상태 중력 오버라이드 해제에 사용할 핸들입니다.
            /// </summary>
            public CharacterPhysicsOverrideHandle GravityOverrideHandle;
        }

        /// <summary>
        /// 실행기에 필요한 런타임 의존성을 초기화합니다.
        /// </summary>
        private void Awake()
        {
            _hitEvaluator = new AreaHitEvaluator(hitMask);
            _hitStopController = GetComponent<CharacterHitStopController>();
        }

        private void OnDisable()
        {
            CleanupDummyActors(forceAll: true, forCancel: false);
            ResetCasterActorHandleTransientState(clearCharacter: true);

            if (_current == null)
                return;

            TryCancel(SkillCancelReason.ForcedBySystem);
        }

        /// <summary>
        /// 2D 기준으로 사용할 전방 벡터를 보정합니다.
        /// 입력 전방이 비어 있거나 Z축 기준 기본값에 가까우면 캐스터의 좌우 방향을 사용합니다.
        /// </summary>
        /// <param name="caster">방향 보정 기준이 되는 캐스터 오브젝트입니다.</param>
        /// <param name="forward">원본 전방 벡터입니다.</param>
        /// <returns>Z가 제거되고 2D 기준으로 정규화된 전방 벡터를 반환합니다.</returns>
        private static Vector3 ResolveForward2D(GameObject caster, Vector3 forward)
        {
            // 2D 기준: forward가 비어있거나(0), 기본값(Vector3.forward)처럼 Z축 위주로 들어오는 경우를 보정합니다.
            var f2 = new Vector2(forward.x, forward.y);
            if (f2.sqrMagnitude < 1e-6f || Mathf.Abs(forward.z) > 0.5f)
            {
                float sign = 1f;
                if (caster != null)
                {
                    sign = Mathf.Sign(caster.transform.localScale.x);
                    if (Mathf.Approximately(sign, 0f)) sign = 1f;
                }

                return new Vector3(sign, 0f, 0f);
            }

            // Z는 사용하지 않습니다(2D).
            f2.Normalize();
            return new Vector3(f2.x, f2.y, 0f);
        }

        /// <summary>
        /// 현재 실행 중인 스킬 런타임을 프레임 단위로 갱신합니다.
        /// 실행이 완료되면 현재 런타임 참조를 해제합니다.
        /// </summary>
        private void Update()
        {
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

            UpdatePendingGroundSlam();
            UpdateGroundSlamAnimation();
            UpdateArcLungeAnimation();
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
            if (_current != null) return false;

            if (!SkillDefinitionResolver.TryResolve(skillUid, source, out var skill) || skill == null) return false;

            CleanupSpawnedVfxs();
            CleanupDummyActors(forceAll: true, forCancel: false);
            ResetCasterActorHandleTransientState(clearCharacter: true);
            _chainUnlockByAttackId.Clear();
            ClearGroundSlamAnimationState();
            ClearPendingGroundSlamState();
            ClearArcLungeAnimationState();

            var motion = ResolveMotionController(targetCtx.caster);
            motion?.CancelMotion(MotionChannel.Skill, 2001);

            if (targetCtx.caster != null)
            {
                var rb = targetCtx.caster.GetComponentInParent<Rigidbody2D>();
                if (rb != null)
                    rb.SetLinearVelocity(Vector2.zero);
            }

            _current = new SkillRun(this, skill, targetCtx,
                ResolveAnimController(targetCtx.caster),
                ResolveActionController(targetCtx.caster));
            _hasPendingFinishReport = false;
            _current.Start();
            return true;
        }

        /// <summary>
        /// 지정한 AttackId가 실제 데미지 확정 시 다음 스킬 연계를 열 수 있는 이벤트인지 확인합니다.
        /// </summary>
        public bool IsChainUnlockAttack(int attackId)
        {
            return attackId > 0 && _chainUnlockByAttackId.TryGetValue(attackId, out bool enabled) && enabled;
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
        public void ExecuteEvent(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            SkillRuntimeSequence sequence,
            in SkillRuntimeEvent e,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (!CanProcessEvent(run))
                return;

            var payload = sequence != null ? sequence.GetPayload(e.PayloadIndex) : null;

            switch (e.Type)
            {
                case ConfigCommonSkill.SkillEventType.Damage:
                    // Damage 클립 구간 동안(Start~End) Gizmo 표시가 가능하도록 duration을 전달합니다.
                    // EndTime이 비정상(=StartTime)인 경우에도 최소 1프레임은 보이도록 보정합니다.
                    float damageGizmoDuration = Mathf.Max(0.05f, e.EndTime - e.StartTime);
                    HandleDamage(run, skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint, damageGizmoDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.SpawnVfx:
                    HandleVfx(run, skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.ApplyAffect:
                    HandleApplyStatus(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.Lunge:
                    float lungeDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandleLunge(skill, ctx, payload, lungeDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.Projectile:
                    HandleProjectile(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.Laser:
                    HandleLaser(run, skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.PositionHold:
                    float positionHoldDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandlePositionHold(run, ctx, payload, positionHoldDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.GroundSlam:
                    float groundSlamDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandleGroundSlam(ctx, payload, groundSlamDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.ApplyTempHp:
                    HandleApplyTempHp(skill, ctx, payload);
                    break;
                case ConfigCommonSkill.SkillEventType.SpawnDummyCharacter:
                    HandleSpawnDummyCharacter(run, skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.MoveDummyCharacter:
                    HandleMoveDummyCharacter(run, ctx, payload, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.DespawnDummyCharacter:
                    HandleDespawnDummyCharacter(payload);
                    break;
                case ConfigCommonSkill.SkillEventType.SetDummyAirborneState:
                    HandleSetDummyAirborneState(payload);
                    break;
                case ConfigCommonSkill.SkillEventType.PlayDummyCharacterAnimation:
                    float dummyAnimationDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandlePlayDummyCharacterAnimation(ctx, payload, dummyAnimationDuration);
                    break;
                default:
                    break;
            }
        }
        

        private void HandlePositionHold(
            SkillRun run,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            if (run == null)
                return;

            if (payloadObj is not PositionHoldEventDefinition def)
                return;

            if (ctx.caster == null)
                return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            bool keepUntilSkillEnd = def.durationPolicy == PositionHoldDurationPolicy.UntilSkillEnd;
            if (!keepUntilSkillEnd && duration <= 0f)
                return;

            run.TryStartPositionHold(duration, keepUntilSkillEnd, def.stopAtEnd, def.useMovePosition, def.allowReplace);
        }

        private static bool TryResolveVfxDuration(VfxEventDefinition def, out float duration)
        {
            duration = 0f;
            if (def == null)
                return false;

            switch (def.lifetimeMode)
            {
                case VfxLifetimeMode.UseVfxDefault:
                    return false;

                case VfxLifetimeMode.OneShot:
                    duration = 0f;
                    return true;

                case VfxLifetimeMode.FixedDuration:
                    duration = Mathf.Max(0f, def.lifetimeSeconds);
                    return true;

                case VfxLifetimeMode.Infinite:
                    duration = -1f;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 스킬 이벤트 정의와 계산된 생성 위치를 Core VFX 생성 요청으로 변환합니다.
        /// </summary>
        /// <param name="def">스킬 Timeline에서 Bake된 VFX 이벤트 정의입니다.</param>
        /// <param name="spawnPos">타겟팅 규칙과 오프셋을 반영한 월드 생성 위치입니다.</param>
        /// <returns>VFX 매니저에 전달할 생성 요청입니다.</returns>
        private static VfxSpawnRequest BuildVfxSpawnRequest(VfxEventDefinition def, Vector3 spawnPos)
        {
            TryResolveVfxDuration(def, out var vfxDuration);

            return new VfxSpawnRequest
            {
                VfxUid = def != null ? def.vfxUid : 0,
                WorldPosition = spawnPos,
                DurationOverride = vfxDuration,
                SortingLayerOverride = def != null && def.overrideSortingLayer
                    ? def.sortingLayerOverride
                    : (ConfigSortingLayer.Keys?)null,
                SortingOrderOverride = def != null && def.overrideSortingOrder
                    ? def.sortingOrderOverride
                    : (int?)null,
            };
        }

        private static Vector2 ResolveCurrentFacing2D(GameObject caster)
        {
            if (caster == null)
                return Vector2.right;

            var characterBase = caster.GetComponent<CharacterBase>();
            if (characterBase != null)
            {
                var facing = CharacterConstants.FacingToVector2(characterBase.CurrentFacing);
                if (facing.sqrMagnitude > 1e-6f)
                    return facing.normalized;
            }

            float sign = Mathf.Sign(caster.transform.localScale.x);
            if (Mathf.Approximately(sign, 0f))
                sign = 1f;

            return new Vector2(sign, 0f);
        }
        
        /// <summary>
        /// 돌진 이벤트 정의에 따라 캐릭터 이동을 시작합니다.
        /// 2D 방향을 보정하고 직선 또는 포물선 이동 요청을 모션 컨트롤러에 전달합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">돌진 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        private void HandleLunge(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            if (payloadObj is ArcLungeEventDefinition arcDef)
            {
                HandleArcLunge(skill, ctx, arcDef, eventDurationSeconds);
                return;
            }

            if (payloadObj is not LungeEventDefinition def) return;
            if (ctx.caster == null) return;

            // 모션 컨트롤러는 캐릭터(플레이어/몬스터) 공용 컴포넌트에서 제공한다.
            var motion = ctx.caster.GetComponentInParent<ICharacterMotionController>();
            if (motion == null) return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            if (duration <= 0f) return;

            Vector2 fallbackDirection = def.useSnapshotForward
                ? new Vector2(ResolveForward2D(ctx.caster, ctx.forward).x, ResolveForward2D(ctx.caster, ctx.forward).y)
                : ResolveCurrentFacing2D(ctx.caster);

            if (TryResolveLungeMotion(skill, ctx, def, fallbackDirection, out var resolvedDirection, out float resolvedDistance) == false)
                return;

            if (def.invertForward)
                resolvedDirection = -resolvedDirection;

            if (Mathf.Abs(resolvedDirection.x) < 1e-4f && def.horizontalOnly)
            {
                float sign = Mathf.Sign(ctx.caster.transform.localScale.x);
                if (Mathf.Approximately(sign, 0f))
                    sign = 1f;

                resolvedDirection = new Vector2(sign, 0f);
            }

            if (def.horizontalOnly)
                resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);
            else if (resolvedDirection.sqrMagnitude > 1e-6f)
                resolvedDirection.Normalize();

            if (resolvedDistance <= 0f)
                return;

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.Linear,
                resolvedDirection,
                duration,
                resolvedDistance,
                def.easing,
                holdSecondsAfter: 0f,
                stopAtEnd: def.stopAtEnd,
                useMovePosition: def.useMovePosition,
                allowReplace: def.allowReplace,
                collisionPolicy: ResolveMotionCollisionPolicy(def),
                collisionTarget: ResolveMotionCollisionTarget(ctx, def));

            motion.TryStartMotion(in req);
        }

        private void HandleArcLunge(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ArcLungeEventDefinition def,
            float eventDurationSeconds)
        {
            if (def == null || ctx.caster == null) return;

            var motion = ctx.caster.GetComponentInParent<ICharacterMotionController>();
            if (motion == null) return;

            float riseDuration = Mathf.Max(0f, def.riseDurationSeconds);
            float apexHoldDuration = Mathf.Max(0f, def.apexHoldDurationSeconds);
            float fallDuration = Mathf.Max(0f, def.fallDurationSeconds);
            float totalDuration = def.durationOverrideSeconds > 0f
                ? def.durationOverrideSeconds
                : riseDuration + apexHoldDuration + fallDuration;
            if (totalDuration <= 0f)
                totalDuration = eventDurationSeconds;
            if (totalDuration <= 0f || def.arcHeight <= 0f)
                return;

            Vector2 fallbackDirection = def.useSnapshotForward
                ? new Vector2(ResolveForward2D(ctx.caster, ctx.forward).x, ResolveForward2D(ctx.caster, ctx.forward).y)
                : ResolveCurrentFacing2D(ctx.caster);

            if (TryResolveArcLungeMotion(skill, ctx, def, fallbackDirection, out var resolvedDirection, out float resolvedDistance) == false)
                return;

            if (def.invertForward)
                resolvedDirection = -resolvedDirection;

            if (Mathf.Abs(resolvedDirection.x) < 1e-4f && def.horizontalOnly)
            {
                float sign = Mathf.Sign(ctx.caster.transform.localScale.x);
                if (Mathf.Approximately(sign, 0f))
                    sign = 1f;

                resolvedDirection = new Vector2(sign, 0f);
            }

            if (def.horizontalOnly)
                resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);
            else if (resolvedDirection.sqrMagnitude > 1e-6f)
                resolvedDirection.Normalize();

            if (resolvedDistance <= 0f)
                return;

            NormalizeArcDurations(riseDuration, apexHoldDuration, fallDuration, totalDuration, out float normalizedRise, out float normalizedApex, out float normalizedFall);

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.Arc,
                resolvedDirection,
                totalDuration,
                resolvedDistance,
                def.easing,
                arcHeight: def.arcHeight,
                arcMode: def.arcMode,
                arcRiseEaseType: def.arcRiseEase,
                arcFallEaseType: def.arcFallEase,
                arcApexHoldNormalized: normalizedApex,
                arcRiseRatioNormalized: normalizedRise,
                arcFallRatioNormalized: normalizedFall,
                holdSecondsAfter: 0f,
                stopAtEnd: def.stopAtEnd,
                useMovePosition: def.useMovePosition,
                allowReplace: def.allowReplace,
                collisionPolicy: ResolveMotionCollisionPolicy(def.collisionPolicy),
                collisionTarget: ResolveMotionCollisionTarget(ctx, def.collisionPolicy));

            if (!motion.TryStartMotion(in req))
                return;

            BeginArcLungeAnimation(ctx.caster, motion, def, riseDuration, apexHoldDuration);
        }

        private static bool TryResolveArcLungeMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ArcLungeEventDefinition def,
            Vector2 fallbackDirection,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            switch (def.resolveMode)
            {
                case SkillLungeResolveMode.FixedDistance:
                    return EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);

                case SkillLungeResolveMode.ToLockedTarget:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: false, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetIfWithinResolveRange:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetElseFixedDistance:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: true, out resolvedDirection, out resolvedDistance);

                default:
                    return EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);
            }
        }

        private static void NormalizeArcDurations(
            float riseDuration,
            float apexHoldDuration,
            float fallDuration,
            float totalDuration,
            out float normalizedRise,
            out float normalizedApex,
            out float normalizedFall)
        {
            float rise = Mathf.Max(0f, riseDuration);
            float apex = Mathf.Max(0f, apexHoldDuration);
            float fall = Mathf.Max(0f, fallDuration);
            float sum = rise + apex + fall;
            if (sum <= 1e-6f)
            {
                if (totalDuration > 0f)
                {
                    rise = totalDuration * 0.5f;
                    fall = totalDuration * 0.5f;
                }
                else
                {
                    rise = 0.5f;
                    fall = 0.5f;
                }

                sum = rise + fall;
            }

            normalizedRise = rise / sum;
            normalizedApex = apex / sum;
            normalizedFall = fall / sum;
        }

        /// <summary>
        /// 레이저 데미지 활성 지속 시간을 Core 레이저 시스템에 전달할 값으로 보정합니다.
        /// </summary>
        /// <param name="value">스킬 이벤트 정의에 저장된 데미지 활성 지속 시간입니다.</param>
        /// <returns>0 이하이면 레이저 종료까지 유지하는 의미의 -1, 양수이면 해당 값을 반환합니다.</returns>
        private static float NormalizeLaserDamageActiveDuration(float value)
        {
            return value <= 0f ? -1f : value;
        }

        private static bool TryResolveLockedTargetMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ArcLungeEventDefinition def,
            Vector2 fallbackDirection,
            bool requireWithinResolveRange,
            bool fallbackToFixedDistance,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            if (ctx.caster == null || ctx.lockedTarget == null)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);

            Vector2 delta = ResolveCasterToTargetDelta(ctx.caster.transform.position, ctx.lockedTarget.transform.position, def.horizontalOnly);
            float targetDistance = delta.magnitude;
            float resolveRange = ResolveTargetResolveRange(skill, def.targetResolveRange, def.distance);

            bool withinResolveRange = !requireWithinResolveRange || resolveRange <= 0f || targetDistance <= resolveRange;
            if (!withinResolveRange)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);

            if (targetDistance <= 1e-4f)
            {
                if (def.targetRelationMode == SkillLungeTargetRelationMode.PassThroughTarget)
                {
                    resolvedDistance = Mathf.Max(0f, def.passThroughExtraDistance);
                    return resolvedDistance > 0f && EnsureFallbackDirection(ctx, def.horizontalOnly, ref resolvedDirection);
                }

                resolvedDistance = 0f;
                return false;
            }

            resolvedDirection = delta / targetDistance;
            if (def.horizontalOnly)
                resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);

            switch (def.targetRelationMode)
            {
                case SkillLungeTargetRelationMode.ReachTargetCenter:
                    resolvedDistance = targetDistance;
                    break;
                case SkillLungeTargetRelationMode.PassThroughTarget:
                    resolvedDistance = targetDistance + Mathf.Max(0f, def.passThroughExtraDistance);
                    break;
                default:
                    resolvedDistance = Mathf.Max(0f, targetDistance - Mathf.Max(0f, def.stopOffset));
                    break;
            }

            return resolvedDistance > 0f;
        }

        private static bool TryResolveLungeMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            LungeEventDefinition def,
            Vector2 fallbackDirection,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            switch (def.resolveMode)
            {
                case SkillLungeResolveMode.FixedDistance:
                    return EnsureFallbackDirection(ctx, def, ref resolvedDirection);

                case SkillLungeResolveMode.ToLockedTarget:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: false, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetIfWithinResolveRange:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: false, out resolvedDirection, out resolvedDistance);

                case SkillLungeResolveMode.ToLockedTargetElseFixedDistance:
                    return TryResolveLockedTargetMotion(skill, ctx, def, fallbackDirection, requireWithinResolveRange: true, fallbackToFixedDistance: true, out resolvedDirection, out resolvedDistance);

                default:
                    return EnsureFallbackDirection(ctx, def, ref resolvedDirection);
            }
        }

        private static bool TryResolveLockedTargetMotion(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            LungeEventDefinition def,
            Vector2 fallbackDirection,
            bool requireWithinResolveRange,
            bool fallbackToFixedDistance,
            out Vector2 resolvedDirection,
            out float resolvedDistance)
        {
            resolvedDirection = fallbackDirection;
            resolvedDistance = Mathf.Max(0f, def.distance);

            if (ctx.caster == null || ctx.lockedTarget == null)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def, ref resolvedDirection);

            Vector2 delta = ResolveCasterToTargetDelta(ctx.caster.transform.position, ctx.lockedTarget.transform.position, def.horizontalOnly);
            float targetDistance = delta.magnitude;
            float resolveRange = ResolveTargetResolveRange(skill, def);

            bool withinResolveRange = !requireWithinResolveRange || resolveRange <= 0f || targetDistance <= resolveRange;
            if (!withinResolveRange)
                return fallbackToFixedDistance && EnsureFallbackDirection(ctx, def, ref resolvedDirection);

            if (targetDistance <= 1e-4f)
            {
                if (def.targetRelationMode == SkillLungeTargetRelationMode.PassThroughTarget)
                {
                    resolvedDistance = Mathf.Max(0f, def.passThroughExtraDistance);
                    return resolvedDistance > 0f && EnsureFallbackDirection(ctx, def, ref resolvedDirection);
                }

                resolvedDistance = 0f;
                return false;
            }

            resolvedDirection = delta / targetDistance;
            if (def.horizontalOnly)
                resolvedDirection = new Vector2(Mathf.Sign(resolvedDirection.x), 0f);

            resolvedDistance = ResolveLockedTargetDistance(def, targetDistance);
            return resolvedDistance > 0f;
        }

        private static float ResolveLockedTargetDistance(LungeEventDefinition def, float targetDistance)
        {
            targetDistance = Mathf.Max(0f, targetDistance);
            switch (def.targetRelationMode)
            {
                case SkillLungeTargetRelationMode.ReachTargetCenter:
                    return targetDistance;

                case SkillLungeTargetRelationMode.PassThroughTarget:
                    return targetDistance + Mathf.Max(0f, def.passThroughExtraDistance);

                case SkillLungeTargetRelationMode.StopBeforeTarget:
                default:
                    return Mathf.Max(0f, targetDistance - Mathf.Max(0f, def.stopOffset));
            }
        }

        private static MotionCollisionPolicy ResolveMotionCollisionPolicy(SkillLungeCollisionPolicy collisionPolicy)
        {
            return collisionPolicy == SkillLungeCollisionPolicy.IgnoreLockedTargetCharacter
                ? MotionCollisionPolicy.IgnoreTargetCharacter
                : MotionCollisionPolicy.Default;
        }

        private static MotionCollisionPolicy ResolveMotionCollisionPolicy(LungeEventDefinition def)
        {
            return ResolveMotionCollisionPolicy(def.collisionPolicy);
        }

        private static GameObject ResolveMotionCollisionTarget(SkillTargetContext ctx, SkillLungeCollisionPolicy collisionPolicy)
        {
            if (collisionPolicy != SkillLungeCollisionPolicy.IgnoreLockedTargetCharacter)
                return null;

            return ctx.lockedTarget;
        }

        private static GameObject ResolveMotionCollisionTarget(SkillTargetContext ctx, LungeEventDefinition def)
        {
            return ResolveMotionCollisionTarget(ctx, def.collisionPolicy);
        }

        private static bool EnsureFallbackDirection(SkillTargetContext ctx, bool horizontalOnly, ref Vector2 direction)
        {
            if (horizontalOnly)
            {
                if (Mathf.Abs(direction.x) < 1e-4f)
                {
                    float sign = 1f;
                    if (ctx.caster != null)
                    {
                        sign = Mathf.Sign(ctx.caster.transform.localScale.x);
                        if (Mathf.Approximately(sign, 0f))
                            sign = 1f;
                    }

                    direction = new Vector2(sign, 0f);
                }
                else
                {
                    direction = new Vector2(Mathf.Sign(direction.x), 0f);
                }

                return true;
            }

            if (direction.sqrMagnitude <= 1e-6f)
            {
                direction = Vector2.right;
            }
            else
            {
                direction.Normalize();
            }

            return true;
        }

        private static bool EnsureFallbackDirection(SkillTargetContext ctx, LungeEventDefinition def, ref Vector2 direction)
        {
            return EnsureFallbackDirection(ctx, def.horizontalOnly, ref direction);
        }

        private static float ResolveTargetResolveRange(RuntimeSkillDefinition skill, float targetResolveRange, float distance)
        {
            if (targetResolveRange > 0f)
                return targetResolveRange;

            float castRange = SkillRangeResolver.GetCastRange(skill);
            if (castRange > 0f)
                return castRange;

            return Mathf.Max(0f, distance);
        }

        private static float ResolveTargetResolveRange(RuntimeSkillDefinition skill, LungeEventDefinition def)
        {
            return ResolveTargetResolveRange(skill, def.targetResolveRange, def.distance);
        }

        private static Vector2 ResolveCasterToTargetDelta(Vector3 casterPosition, Vector3 targetPosition, bool horizontalOnly)
        {
            if (horizontalOnly)
                return new Vector2(targetPosition.x - casterPosition.x, 0f);

            return new Vector2(targetPosition.x - casterPosition.x, targetPosition.y - casterPosition.y);
        }

        private void BeginArcLungeAnimation(
            GameObject caster,
            ICharacterMotionController motion,
            ArcLungeEventDefinition def,
            float riseDurationSeconds,
            float apexHoldDurationSeconds)
        {
            ClearArcLungeAnimationState();

            if (caster == null || motion == null || def == null)
                return;

            var anim = ResolveAnimController(caster);
            if (anim == null)
                return;

            _arcLungeAnimationState = new ArcLungeAnimationState
            {
                IsActive = true,
                Caster = caster,
                AnimationController = anim,
                MotionController = motion,
                Definition = def,
                Phase = ArcLungeAnimationPhaseState.None,
                ElapsedSeconds = 0f,
                RiseDurationSeconds = Mathf.Max(0f, riseDurationSeconds),
                ApexHoldDurationSeconds = Mathf.Max(0f, apexHoldDurationSeconds),
                LandEndRemainingSeconds = 0f,
            };

            if (_arcLungeAnimationState.RiseDurationSeconds > 0f && !string.IsNullOrWhiteSpace(def.riseAnimationName))
            {
                PlayArcLungeAnimation(anim, def.riseAnimationName, loop: false);
                _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.Rise;
                return;
            }

            if (_arcLungeAnimationState.ApexHoldDurationSeconds > 0f && !string.IsNullOrWhiteSpace(def.apexAnimationName))
            {
                PlayArcLungeAnimation(anim, def.apexAnimationName, loop: true);
                _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.Apex;
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.fallAnimationName))
            {
                PlayArcLungeAnimation(anim, def.fallAnimationName, loop: true);
                _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.Fall;
            }
        }

        private void UpdateArcLungeAnimation()
        {
            if (!_arcLungeAnimationState.IsActive)
                return;

            var anim = _arcLungeAnimationState.AnimationController;
            var motion = _arcLungeAnimationState.MotionController;
            var def = _arcLungeAnimationState.Definition;
            if (anim == null || motion == null || def == null)
            {
                ClearArcLungeAnimationState();
                return;
            }

            if (_arcLungeAnimationState.Phase == ArcLungeAnimationPhaseState.LandEnd)
            {
                _arcLungeAnimationState.LandEndRemainingSeconds -= Time.deltaTime;
                if (_arcLungeAnimationState.LandEndRemainingSeconds <= 0f)
                    ClearArcLungeAnimationState();
                return;
            }

            if (!motion.IsPlaying(MotionChannel.Skill))
            {
                PlayArcLungeLandEndOrClear();
                return;
            }

            _arcLungeAnimationState.ElapsedSeconds += Time.deltaTime;
            float riseEnd = _arcLungeAnimationState.RiseDurationSeconds;
            float apexEnd = riseEnd + _arcLungeAnimationState.ApexHoldDurationSeconds;

            if (_arcLungeAnimationState.Phase == ArcLungeAnimationPhaseState.Rise && _arcLungeAnimationState.ElapsedSeconds >= riseEnd)
            {
                if (_arcLungeAnimationState.ApexHoldDurationSeconds > 0f && !string.IsNullOrWhiteSpace(def.apexAnimationName))
                {
                    PlayArcLungeAnimation(anim, def.apexAnimationName, loop: true);
                    _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.Apex;
                }
                else if (!string.IsNullOrWhiteSpace(def.fallAnimationName))
                {
                    PlayArcLungeAnimation(anim, def.fallAnimationName, loop: true);
                    _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.Fall;
                }
                else
                {
                    _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.Fall;
                }
            }

            if (_arcLungeAnimationState.Phase == ArcLungeAnimationPhaseState.Apex && _arcLungeAnimationState.ElapsedSeconds >= apexEnd)
            {
                if (!string.IsNullOrWhiteSpace(def.fallAnimationName))
                    PlayArcLungeAnimation(anim, def.fallAnimationName, loop: true);

                _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.Fall;
            }
        }

        private void PlayArcLungeLandEndOrClear()
        {
            if (!_arcLungeAnimationState.IsActive)
                return;

            var anim = _arcLungeAnimationState.AnimationController;
            var def = _arcLungeAnimationState.Definition;
            if (anim == null || def == null)
            {
                ClearArcLungeAnimationState();
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.landEndAnimationName))
            {
                PlayArcLungeAnimation(anim, def.landEndAnimationName, loop: false);
                _arcLungeAnimationState.Phase = ArcLungeAnimationPhaseState.LandEnd;
                _arcLungeAnimationState.LandEndRemainingSeconds = GetCharacterAnimationDurationSafe(anim, def.landEndAnimationName);
                return;
            }

            ClearArcLungeAnimationState();
        }

        private static void PlayArcLungeAnimation(
            ICharacterAnimationController anim,
            string animationName,
            bool loop)
        {
            if (anim == null || string.IsNullOrWhiteSpace(animationName))
                return;

            anim.PlaySkillAnimation(new SkillAnimationRequest(
                skillUid: 0,
                phase: SkillAnimationPhase.Action,
                loop: loop,
                timeScale: 1f,
                overrideAnimationName: animationName));
        }

        private static float GetCharacterAnimationDurationSafe(
            ICharacterAnimationController anim,
            string animationName)
        {
            if (anim == null || string.IsNullOrWhiteSpace(animationName))
                return 0.05f;

            float duration = anim.GetCharacterAnimationDuration(animationName, isMilliseconds: false);
            return Mathf.Max(0.05f, duration);
        }

        private void ClearArcLungeAnimationState()
        {
            _arcLungeAnimationState = default;
        }

        /// <summary>
        /// Ground Slam 이벤트 정의에 따라 착지 지점을 계산하고 내려치기 이동을 시작합니다.
        /// </summary>
        private void HandleGroundSlam(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            if (payloadObj is not GroundSlamEventDefinition def) return;
            if (ctx.caster == null) return;

            var motion = ctx.caster.GetComponentInParent<ICharacterMotionController>();
            if (motion == null) return;

            float holdDuration = Mathf.Max(0f, def.airHoldDurationSeconds);
            float fallDuration = def.fallDurationSeconds > 0f
                ? def.fallDurationSeconds
                : (def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds);
            if (fallDuration <= 0f) return;

            Vector2 startPosition = ctx.caster.transform.position;
            Vector2 forward = def.useSnapshotForward
                ? new Vector2(ResolveForward2D(ctx.caster, ctx.forward).x, ResolveForward2D(ctx.caster, ctx.forward).y)
                : ResolveCurrentFacing2D(ctx.caster);
            if (forward.sqrMagnitude <= 1e-6f)
                forward = Vector2.right;
            else
                forward.Normalize();

            if (!TryResolveGroundSlamTargetPosition(ctx, def, startPosition, forward, out Vector2 targetPosition))
                return;

            Vector2 travel = targetPosition - startPosition;
            if (travel.sqrMagnitude <= 1e-8f)
            {
                BeginGroundSlamInstantLandSequence(ctx.caster, motion, def);
                return;
            }

            if (holdDuration > 0f)
            {
                if (def.holdPositionDuringAirHold && !TryStartGroundSlamHoldMotion(motion, def, startPosition, holdDuration))
                    return;

                ClearPendingGroundSlamState();
                BeginGroundSlamAnimation(ctx.caster, motion, def, usePhaseBasedLoopTransition: true);
                _pendingGroundSlamState = new PendingGroundSlamState
                {
                    IsActive = true,
                    Caster = ctx.caster,
                    MotionController = motion,
                    Definition = def,
                    StartPosition = startPosition,
                    TargetPosition = targetPosition,
                    FallDurationSeconds = fallDuration,
                    HoldRemainingSeconds = holdDuration,
                    UsePhaseBasedLoopTransition = true,
                };
                return;
            }

            if (!TryStartGroundSlamMotion(motion, def, startPosition, targetPosition, fallDuration))
                return;

            BeginGroundSlamAnimation(ctx.caster, motion, def, usePhaseBasedLoopTransition: false);
        }

        private void UpdatePendingGroundSlam()
        {
            if (!_pendingGroundSlamState.IsActive)
                return;

            if (_pendingGroundSlamState.Caster == null || _pendingGroundSlamState.MotionController == null || _pendingGroundSlamState.Definition == null)
            {
                ClearPendingGroundSlamState();
                ClearGroundSlamAnimationState();
                return;
            }

            _pendingGroundSlamState.HoldRemainingSeconds -= Time.deltaTime;
            if (_pendingGroundSlamState.HoldRemainingSeconds > 0f)
                return;

            var pending = _pendingGroundSlamState;
            ClearPendingGroundSlamState();

            if (!TryStartGroundSlamMotion(pending.MotionController, pending.Definition, pending.StartPosition, pending.TargetPosition, pending.FallDurationSeconds))
            {
                ClearGroundSlamAnimationState();
                return;
            }

            if (_groundSlamAnimationState.IsActive)
            {
                _groundSlamAnimationState.MotionController = pending.MotionController;
                _groundSlamAnimationState.Definition = pending.Definition;
                if (!string.IsNullOrWhiteSpace(pending.Definition.fallLoopAnimationName))
                {
                    PlayGroundSlamAnimation(_groundSlamAnimationState.AnimationController, pending.Definition.fallLoopAnimationName, loop: true);
                    _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
                }
                else
                {
                    _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
                }
            }
            else
            {
                BeginGroundSlamAnimation(pending.Caster, pending.MotionController, pending.Definition, pending.UsePhaseBasedLoopTransition);
            }
        }


        private static bool TryStartGroundSlamHoldMotion(
            ICharacterMotionController motion,
            GroundSlamEventDefinition def,
            Vector2 holdPosition,
            float holdDurationSeconds)
        {
            if (motion == null || def == null || holdDurationSeconds <= 0f)
                return false;

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.PositionHold,
                Vector2.zero,
                holdDurationSeconds,
                0f,
                Easing.EaseType.Linear,
                stopAtEnd: true,
                useMovePosition: def.useMovePosition,
                allowReplace: def.allowReplace,
                startPosition: holdPosition,
                targetPosition: holdPosition,
                groundSnapDistance: 0f);

            return motion.TryStartMotion(in req);
        }

        private static bool TryStartGroundSlamMotion(
            ICharacterMotionController motion,
            GroundSlamEventDefinition def,
            Vector2 startPosition,
            Vector2 targetPosition,
            float fallDurationSeconds)
        {
            if (motion == null || def == null)
                return false;

            Vector2 travel = targetPosition - startPosition;
            if (travel.sqrMagnitude <= 1e-8f)
                return false;

            var req = new MotionRequest(
                MotionChannel.Skill,
                MotionKind.GroundSlam,
                travel.normalized,
                fallDurationSeconds,
                travel.magnitude,
                def.easing,
                stopAtEnd: def.stopAtEnd,
                useMovePosition: def.useMovePosition,
                allowReplace: true,
                startPosition: startPosition,
                targetPosition: targetPosition,
                groundSnapDistance: def.groundSnapDistance);

            return motion.TryStartMotion(in req);
        }

        private static bool TryResolveGroundSlamTargetPosition(
            SkillTargetContext ctx,
            GroundSlamEventDefinition def,
            Vector2 startPosition,
            Vector2 forward,
            out Vector2 targetPosition)
        {
            float targetX = startPosition.x;
            switch (def.horizontalPolicy)
            {
                case GroundSlamHorizontalPolicy.KeepCurrentX:
                    targetX = startPosition.x;
                    break;
                case GroundSlamHorizontalPolicy.MoveToTargetX:
                    if (ctx.lockedTarget != null)
                        targetX = ctx.lockedTarget.transform.position.x;
                    else if (ctx.groundPoint != default)
                        targetX = ctx.groundPoint.x;
                    break;
                case GroundSlamHorizontalPolicy.MoveByForward:
                    targetX = startPosition.x + forward.x * Mathf.Max(0f, def.forwardDistance);
                    break;
            }

            switch (def.landingMode)
            {
                case GroundSlamLandingMode.FixedDistanceDown:
                {
                    float targetY = startPosition.y - Mathf.Max(0f, def.fixedDropDistance);
                    targetPosition = new Vector2(targetX, targetY);
                    return targetY < startPosition.y - 1e-4f;
                }
                case GroundSlamLandingMode.LockedTargetGround:
                {
                    Vector2 probeBase = ctx.lockedTarget != null
                        ? (Vector2)ctx.lockedTarget.transform.position
                        : new Vector2(targetX, startPosition.y);
                    targetX = probeBase.x;
                    return TryResolveGroundPoint(def, targetX, probeBase.y, startPosition.y, out targetPosition);
                }
                case GroundSlamLandingMode.GroundPoint:
                {
                    Vector2 probeBase = ctx.groundPoint != default
                        ? (Vector2)ctx.groundPoint
                        : new Vector2(targetX, startPosition.y);
                    targetX = probeBase.x;
                    return TryResolveGroundPoint(def, targetX, probeBase.y, startPosition.y, out targetPosition);
                }
                case GroundSlamLandingMode.CurrentGround:
                default:
                    return TryResolveGroundPoint(def, targetX, startPosition.y, startPosition.y, out targetPosition);
            }
        }

        private static bool TryResolveGroundPoint(
            GroundSlamEventDefinition def,
            float targetX,
            float referenceY,
            float startY,
            out Vector2 targetPosition)
        {
            Vector2 origin = new Vector2(targetX, Mathf.Max(referenceY, startY) + Mathf.Max(0f, def.groundProbeStartHeight));
            float probeDistance = Mathf.Max(0.1f, def.groundProbeDistance);
            var hit = Physics2D.Raycast(origin, Vector2.down, probeDistance, def.groundLayerMask);
            if (hit.collider != null)
            {
                targetPosition = new Vector2(targetX, hit.point.y);
                return true;
            }

            float fallbackY = startY - Mathf.Max(0f, def.fixedDropDistance);
            targetPosition = new Vector2(targetX, fallbackY);
            return true;
        }


        private void BeginGroundSlamAnimation(
            GameObject caster,
            ICharacterMotionController motion,
            GroundSlamEventDefinition def,
            bool usePhaseBasedLoopTransition)
        {
            ClearGroundSlamAnimationState();

            if (caster == null || motion == null || def == null)
                return;

            var anim = ResolveAnimController(caster);
            if (anim == null)
                return;

            _groundSlamAnimationState = new GroundSlamAnimationState
            {
                IsActive = true,
                Caster = caster,
                AnimationController = anim,
                MotionController = motion,
                Definition = def,
                Phase = GroundSlamAnimationPhaseState.None,
                UsePhaseBasedLoopTransition = usePhaseBasedLoopTransition,
                IsInstantLandSequence = false,
                PhaseRemainingSeconds = 0f,
            };

            if (!string.IsNullOrWhiteSpace(def.startAnimationName))
            {
                PlayGroundSlamAnimation(anim, def.startAnimationName, loop: false);
                _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.Start;
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.fallLoopAnimationName))
            {
                PlayGroundSlamAnimation(anim, def.fallLoopAnimationName, loop: true);
                _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
                return;
            }

            _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.None;
        }

        private void BeginGroundSlamInstantLandSequence(
            GameObject caster,
            ICharacterMotionController motion,
            GroundSlamEventDefinition def)
        {
            ClearPendingGroundSlamState();
            ClearGroundSlamAnimationState();

            if (caster == null || motion == null || def == null)
                return;

            var anim = ResolveAnimController(caster);
            if (anim == null)
                return;

            _groundSlamAnimationState = new GroundSlamAnimationState
            {
                IsActive = true,
                Caster = caster,
                AnimationController = anim,
                MotionController = motion,
                Definition = def,
                Phase = GroundSlamAnimationPhaseState.None,
                UsePhaseBasedLoopTransition = false,
                IsInstantLandSequence = true,
                PhaseRemainingSeconds = 0f,
            };

            if (!string.IsNullOrWhiteSpace(def.startAnimationName))
            {
                PlayGroundSlamAnimation(anim, def.startAnimationName, loop: false);
                _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.Start;
                _groundSlamAnimationState.PhaseRemainingSeconds = GetGroundSlamAnimationDuration(anim, def.startAnimationName);
                return;
            }

            PlayGroundSlamLandEndOrClear();
        }

        private void UpdateGroundSlamInstantLandSequence()
        {
            if (!_groundSlamAnimationState.IsActive)
                return;

            _groundSlamAnimationState.PhaseRemainingSeconds -= Time.deltaTime;
            if (_groundSlamAnimationState.PhaseRemainingSeconds > 0f)
                return;

            switch (_groundSlamAnimationState.Phase)
            {
                case GroundSlamAnimationPhaseState.Start:
                    PlayGroundSlamLandEndOrClear();
                    return;
                case GroundSlamAnimationPhaseState.LandEnd:
                    ClearGroundSlamAnimationState();
                    return;
                default:
                    ClearGroundSlamAnimationState();
                    return;
            }
        }

        private void PlayGroundSlamLandEndOrClear()
        {
            if (!_groundSlamAnimationState.IsActive)
                return;

            var anim = _groundSlamAnimationState.AnimationController;
            var def = _groundSlamAnimationState.Definition;
            if (anim == null || def == null)
            {
                ClearGroundSlamAnimationState();
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.landEndAnimationName))
            {
                PlayGroundSlamAnimation(anim, def.landEndAnimationName, loop: false);
                _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.LandEnd;
                _groundSlamAnimationState.PhaseRemainingSeconds = GetGroundSlamAnimationDuration(anim, def.landEndAnimationName);
                return;
            }

            ClearGroundSlamAnimationState();
        }

        private static float GetGroundSlamAnimationDuration(
            ICharacterAnimationController anim,
            string animationName)
        {
            return GetCharacterAnimationDurationSafe(anim, animationName);
        }

        private void UpdateGroundSlamAnimation()
        {
            if (!_groundSlamAnimationState.IsActive)
                return;

            var anim = _groundSlamAnimationState.AnimationController;
            var motion = _groundSlamAnimationState.MotionController;
            var def = _groundSlamAnimationState.Definition;

            if (anim == null || motion == null || def == null)
            {
                ClearGroundSlamAnimationState();
                return;
            }

            if (_groundSlamAnimationState.IsInstantLandSequence)
            {
                UpdateGroundSlamInstantLandSequence();
                return;
            }

            if (!motion.IsPlaying(MotionChannel.Skill))
            {
                if (_pendingGroundSlamState.IsActive &&
                    ReferenceEquals(_pendingGroundSlamState.MotionController, motion) &&
                    ReferenceEquals(_pendingGroundSlamState.Definition, def))
                {
                    return;
                }

                if (_groundSlamAnimationState.Phase != GroundSlamAnimationPhaseState.LandEnd &&
                    !string.IsNullOrWhiteSpace(def.landEndAnimationName))
                {
                    PlayGroundSlamAnimation(anim, def.landEndAnimationName, loop: false);
                    _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.LandEnd;
                }

                ClearGroundSlamAnimationState();
                return;
            }

            if (_groundSlamAnimationState.Phase != GroundSlamAnimationPhaseState.Start)
                return;

            if (_groundSlamAnimationState.UsePhaseBasedLoopTransition)
                return;

            if (!motion.TryGetMotionProgress(MotionChannel.Skill, out float progress01))
                return;

            if (progress01 < Mathf.Clamp01(def.startToLoopNormalizedTime))
                return;

            if (string.IsNullOrWhiteSpace(def.fallLoopAnimationName))
            {
                _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
                return;
            }

            PlayGroundSlamAnimation(anim, def.fallLoopAnimationName, loop: true);
            _groundSlamAnimationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
        }

        private static void PlayGroundSlamAnimation(
            ICharacterAnimationController anim,
            string animationName,
            bool loop)
        {
            if (anim == null || string.IsNullOrWhiteSpace(animationName))
                return;

            anim.PlaySkillAnimation(new SkillAnimationRequest(
                skillUid: 0,
                phase: SkillAnimationPhase.Action,
                loop: loop,
                timeScale: 1f,
                overrideAnimationName: animationName));
        }

        private void ClearGroundSlamAnimationState()
        {
            _groundSlamAnimationState = default;
        }

        private void ClearPendingGroundSlamState()
        {
            _pendingGroundSlamState = default;
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
        private void HandleLaser(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not LaserEventDefinition def) return;
            if (ctx.caster == null) return;

            var casterChar = ctx.caster.GetComponent<CharacterBase>();
            if (casterChar == null) return;

            if (TableLoaderManager.Instance == null)
                return;

            var laserInfo = TableLoaderManager.Instance.GetLaserData(def.laserUid, false);
            if (laserInfo == null)
                return;

            Vector3 casterPos = ctx.caster.transform.position;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            if (def.targetingOverride.enabled)
                mode = def.targetingOverride.mode;

            CharacterBase targetChar = null;
            if (ctx.lockedTarget != null)
                targetChar = ctx.lockedTarget.GetComponent<CharacterBase>();

            bool usePosOverride = false;
            Vector2 posOverride = default;

            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    usePosOverride = true;
                    posOverride = new Vector2(groundPoint.x, groundPoint.y);
                    break;

                case ConfigCommonSkill.SkillTargetingMode.Self:
                    targetChar = casterChar;
                    usePosOverride = false;
                    break;

                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                    if (targetChar != null)
                    {
                        usePosOverride = false;
                    }
                    else
                    {
                        usePosOverride = true;
                        posOverride = new Vector2(targetPos.x, targetPos.y);
                    }
                    break;

                default:
                    var fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                    float range = SkillRangeResolver.GetPlacementRange(skill);
                    if (def.targetingOverride.enabled && def.targetingOverride.rangeOverride > 0f)
                        range = def.targetingOverride.rangeOverride;

                    var p = SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
                    usePosOverride = true;
                    posOverride = new Vector2(p.x, p.y);
                    break;
            }

            LaserConstants.StartPositionOverrideMode resolvedStartPositionOverrideMode = def.startPositionOverrideMode;
            Vector2 resolvedStartPositionOverride = def.startPositionOverride;
            LaserConstants.StartPointUpdateMode resolvedStartPointUpdateMode = def.startPointUpdateMode;

            if (def.startAnchor != LaserStartAnchor.Caster)
            {
                if (!TryResolveLaserStartAnchorPosition(run, def, casterPos, targetPos, groundPoint, out var startAnchorPosition))
                    return;

                resolvedStartPositionOverrideMode = LaserConstants.StartPositionOverrideMode.WorldPosition;
                resolvedStartPositionOverride = ResolveLaserStartPointByAnchor(laserInfo, def, startAnchorPosition);
                resolvedStartPointUpdateMode = LaserConstants.StartPointUpdateMode.SnapshotAtLaunch;
            }

            int attackId = ++_attackSequence;
            _chainUnlockByAttackId[attackId] = def.allowSkillChainOnConfirmedDamage;

            var meta = new MetadataLaser(
                uid: def.laserUid,
                damageType: def.damageType,
                damage: def.damage,
                target: targetChar,
                owner: casterChar,
                scaleMultiplier: def.scaleMultiplier,
                visualType: def.visualType,
                visualSprite: def.visualSprite,
                visualAnimatorController: def.visualAnimatorController,
                visualVfxUidOverride: def.visualVfxUidOverride,
                useTargetPositionOverride: usePosOverride,
                targetPositionOverride: posOverride,
                skillUid: skill.Uid,
                attackId: attackId,
                allowSkillChainOnConfirmedDamage: def.allowSkillChainOnConfirmedDamage,
                elementGaugeApplications: BuildElementGaugeApplications(def.onHitElementGauges, gameObject, damageApplied: true),
                useDurationOverride: true,
                durationOverride: Mathf.Max(0f, def.durationSeconds),
                useDamageTimingOverride: true,
                damageStartDelayOverride: Mathf.Max(0f, def.damageStartDelaySeconds),
                damageActiveDurationOverride: NormalizeLaserDamageActiveDuration(def.damageActiveDurationSeconds),
                damageTickIntervalOverride: Mathf.Max(0f, def.damageTickIntervalSeconds),
                damageTickOnStartOverride: def.damageTickOnStart,
                useMaxDistanceOverride: def.maxDistance > 0f,
                maxDistanceOverride: Mathf.Max(0f, def.maxDistance),
                updateAimContinuously: def.updateAimContinuously,
                useRaycastDirectionModeOverride: def.useRaycastDirectionModeOverride,
                raycastDirectionModeOverride: def.raycastDirectionModeOverride,
                useRaycastAngleOverride: def.useRaycastAngleOverride,
                raycastAngleOverrideDeg: def.raycastAngleOverrideDeg,
                useVfxAngleSyncModeOverride: def.useVfxAngleSyncModeOverride,
                vfxAngleSyncModeOverride: def.vfxAngleSyncModeOverride,
                startPositionOverrideMode: resolvedStartPositionOverrideMode,
                startPositionOverride: resolvedStartPositionOverride,
                startPointUpdateMode: resolvedStartPointUpdateMode);


            RegisterLaserDebugGizmo(
                casterChar,
                targetChar,
                usePosOverride,
                posOverride,
                ctx.forward,
                laserInfo,
                def,
                meta);

            casterChar.LaunchLaser(meta);
        }

        /// <summary>
        /// Skill 테스트 허브에 레이저 예상 범위 기즈모를 등록합니다.
        /// 실제 LaserBeam과 동일한 조준 정책을 사용하여 Raycast 선분과 시각 회전 가이드를 함께 기록합니다.
        /// </summary>
        /// <param name="casterChar">캐스터 캐릭터입니다.</param>
        /// <param name="targetChar">고정 타겟 캐릭터입니다.</param>
        /// <param name="usePosOverride">좌표 오버라이드 사용 여부입니다.</param>
        /// <param name="posOverride">좌표 오버라이드 값입니다.</param>
        /// <param name="forward">캐스터 전방 방향입니다.</param>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="meta">실제 런타임 발사에 사용할 레이저 메타데이터입니다.</param>
        private void RegisterLaserDebugGizmo(
            CharacterBase casterChar,
            CharacterBase targetChar,
            bool usePosOverride,
            Vector2 posOverride,
            Vector3 forward,
            StruckTableLaser laserInfo,
            LaserEventDefinition def,
            MetadataLaser meta)
        {
            if (casterChar == null || SkillTestRuntimeHub.Instance == null || laserInfo == null)
                return;

            Vector3 start = LaserStartPointResolver.ResolveCurrentStartPoint(
                laserInfo,
                meta,
                casterChar.transform.position);
            Vector2 direction = ResolveLaserPreviewDirection(casterChar, targetChar, usePosOverride, posOverride, forward, laserInfo, meta, start);
            float maxDistance = def.maxDistance > 0f ? def.maxDistance : Mathf.Max(0f, laserInfo.MaxDistance);
            if (maxDistance <= 0f)
                return;

            Vector2 visualDirection = ResolveLaserPreviewVisualDirection(laserInfo, meta, direction);
            LaserConstants.VfxAngleSyncMode vfxAngleSyncMode = LaserAimPolicyUtility.ResolveVfxAngleSyncMode(laserInfo, meta);
            Vector3 end = start + (Vector3)(direction * maxDistance);
            bool hasBlockHit = TryResolveLaserPreviewEnd(casterChar, laserInfo, start, direction, maxDistance, out Vector3 blockedEnd, out Vector3 blockPoint);
            if (hasBlockHit)
                end = blockedEnd;

            float duration = def.durationSeconds > 0f
                ? def.durationSeconds
                : SkillTestRuntimeHub.CurrentSettings != null ? SkillTestRuntimeHub.CurrentSettings.defaultLaserGizmoDuration : 0.2f;

            SkillTestRuntimeHub.Instance.RegisterLaser(
                start,
                end,
                duration,
                casterChar.gameObject,
                hasBlockHit,
                blockPoint,
                direction,
                visualDirection,
                vfxAngleSyncMode);
        }

        /// <summary>
        /// 레이저 프리뷰용 Raycast 방향 벡터를 계산합니다.
        /// 실제 LaserBeam과 동일하게 RaycastDirectionMode, RaycastAngleDeg, 타겟/좌표 오버라이드 우선순위를 따릅니다.
        /// </summary>
        /// <param name="casterChar">캐스터 캐릭터입니다.</param>
        /// <param name="targetChar">고정 타겟 캐릭터입니다.</param>
        /// <param name="usePosOverride">좌표 오버라이드 사용 여부입니다.</param>
        /// <param name="posOverride">좌표 오버라이드 값입니다.</param>
        /// <param name="forward">캐스터 전방 방향입니다.</param>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="meta">실제 런타임 발사에 사용할 레이저 메타데이터입니다.</param>
        /// <param name="start">프리뷰 시작점입니다.</param>
        /// <returns>정책이 반영된 정규화 Raycast 방향입니다.</returns>
        private static Vector2 ResolveLaserPreviewDirection(
            CharacterBase casterChar,
            CharacterBase targetChar,
            bool usePosOverride,
            Vector2 posOverride,
            Vector3 forward,
            StruckTableLaser laserInfo,
            MetadataLaser meta,
            Vector3 start)
        {
            Vector2 fallbackTargetPoint = default;
            bool hasFallbackTargetPoint = false;

            if (targetChar == null && usePosOverride)
            {
                fallbackTargetPoint = posOverride;
                hasFallbackTargetPoint = true;
            }
            else if (targetChar == null && forward.sqrMagnitude > 1e-6f)
            {
                Vector2 normalizedForward = new Vector2(forward.x, forward.y).normalized;
                fallbackTargetPoint = (Vector2)start + normalizedForward;
                hasFallbackTargetPoint = true;
            }

            return LaserAimPolicyUtility.ResolveRaycastDirection(
                laserInfo,
                meta,
                casterChar,
                targetChar,
                hasFallbackTargetPoint,
                fallbackTargetPoint,
                start,
                true);
        }

        /// <summary>
        /// 레이저 프리뷰용 시각 회전 가이드 방향을 계산합니다.
        /// FollowRaycast/LockAtLaunch는 현재 Raycast 방향을 사용하고,
        /// None은 회전을 강제하지 않는 의미를 표현하기 위해 월드 +X 축을 가이드로 사용합니다.
        /// </summary>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="meta">실제 런타임 발사에 사용할 레이저 메타데이터입니다.</param>
        /// <param name="raycastDirection">프리뷰 시점의 Raycast 방향입니다.</param>
        /// <returns>프리뷰용 시각 회전 가이드 방향입니다.</returns>
        private static Vector2 ResolveLaserPreviewVisualDirection(
            StruckTableLaser laserInfo,
            MetadataLaser meta,
            Vector2 raycastDirection)
        {
            return LaserAimPolicyUtility.ResolvePreviewVisualDirection(laserInfo, meta, raycastDirection);
        }


        /// <summary>
        /// 레이저 시작점 기준 앵커를 해석하여 월드 위치를 계산합니다.
        /// startAnchor가 Caster가 아니면 Skill 계층에서 먼저 월드 위치를 확정한 뒤 Core 레이저에 전달합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="casterPos">해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        /// <param name="anchorPosition">계산된 기준 앵커의 월드 위치입니다.</param>
        /// <returns>기준 앵커 해석에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveLaserStartAnchorPosition(
            SkillRun run,
            LaserEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            out Vector3 anchorPosition)
        {
            anchorPosition = casterPos;
            if (def == null)
                return false;

            switch (def.startAnchor)
            {
                case LaserStartAnchor.Target:
                    anchorPosition = targetPos;
                    return true;
                case LaserStartAnchor.Ground:
                    anchorPosition = groundPoint;
                    return true;
                case LaserStartAnchor.NamedPositionAnchor:
                    if (TryResolveNamedAnchorPosition(run, def.namedAnchorKey, out anchorPosition))
                        return true;

                    Debug.LogWarning($"[SkillExecutor] Laser named start anchor not found. key={def.namedAnchorKey}");
                    return false;
                case LaserStartAnchor.Caster:
                default:
                    anchorPosition = casterPos;
                    return true;
            }
        }

        /// <summary>
        /// 기준 앵커 위치와 레이저 시작점 오버라이드 정책을 조합하여 최종 월드 시작점을 계산합니다.
        /// startAnchor가 Caster가 아닌 경우 Core 레이저에는 이 계산 결과를 WorldPosition으로 전달합니다.
        /// </summary>
        /// <param name="laserInfo">레이저 테이블 정보입니다.</param>
        /// <param name="def">레이저 이벤트 정의입니다.</param>
        /// <param name="anchorPosition">해석된 기준 앵커의 월드 위치입니다.</param>
        /// <returns>기준 앵커와 오버라이드 정책이 반영된 최종 월드 시작점입니다.</returns>
        private static Vector2 ResolveLaserStartPointByAnchor(
            StruckTableLaser laserInfo,
            LaserEventDefinition def,
            Vector3 anchorPosition)
        {
            Vector2 anchorPosition2D = anchorPosition;
            Vector2 tableOffset = laserInfo != null ? laserInfo.StartPosition : Vector2.zero;
            if (def == null)
                return anchorPosition2D + tableOffset;

            switch (def.startPositionOverrideMode)
            {
                case LaserConstants.StartPositionOverrideMode.ReplaceTableOffset:
                    return anchorPosition2D + def.startPositionOverride;

                case LaserConstants.StartPositionOverrideMode.AddToTableOffset:
                    return anchorPosition2D + tableOffset + def.startPositionOverride;

                case LaserConstants.StartPositionOverrideMode.WorldPosition:
                    return def.startPositionOverride;

                case LaserConstants.StartPositionOverrideMode.UseLaserTable:
                default:
                    return anchorPosition2D + tableOffset;
            }
        }

        /// <summary>
        /// 레이저 정책에 맞춰 프리뷰 종료점을 계산합니다.
        /// </summary>
        private static bool TryResolveLaserPreviewEnd(
            CharacterBase casterChar,
            StruckTableLaser laserInfo,
            Vector2 start,
            Vector2 direction,
            float maxDistance,
            out Vector3 end,
            out Vector3 blockPoint)
        {
            end = start + direction * maxDistance;
            blockPoint = Vector3.zero;

            int layerMask = Physics2D.GetLayerCollisionMask(casterChar.gameObject.layer);
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true,
            };
            filter.SetLayerMask(layerMask);

            RaycastHit2D[] hits = new RaycastHit2D[32];
            int count = Physics2D.Raycast(start, direction, filter, hits, maxDistance);
            if (count <= 0)
                return false;

            bool hasNearestGround = false;
            RaycastHit2D nearestGround = default;
            bool hasNearestHostile = false;
            RaycastHit2D nearestHostile = default;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                Collider2D col = hit.collider;
                if (!col)
                    continue;

                CharacterBase hitCharacter = CombatHitTargetUtility.ResolveTargetCharacter(col);
                if (hitCharacter != null && hitCharacter == casterChar)
                    continue;

                bool isGround = col.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.MapGround));
                if (isGround)
                {
                    if (!hasNearestGround || hit.distance < nearestGround.distance)
                    {
                        hasNearestGround = true;
                        nearestGround = hit;
                    }

                    continue;
                }

                if (!CombatHitTargetUtility.TryResolveHostileTarget(casterChar, col, out _))
                    continue;

                if (!hasNearestHostile || hit.distance < nearestHostile.distance)
                {
                    hasNearestHostile = true;
                    nearestHostile = hit;
                }
            }

            switch (laserInfo.BlockMode)
            {
                case LaserConstants.BlockMode.StopAtGround:
                    if (hasNearestGround)
                    {
                        blockPoint = nearestGround.point != Vector2.zero ? (Vector3)nearestGround.point : (Vector3)(start + direction * nearestGround.distance);
                        end = blockPoint;
                        return true;
                    }
                    break;

                case LaserConstants.BlockMode.StopAtHostile:
                    if (laserInfo.HitMode == LaserConstants.HitMode.FirstHitOnly && hasNearestHostile)
                    {
                        blockPoint = nearestHostile.point != Vector2.zero ? (Vector3)nearestHostile.point : (Vector3)(start + direction * nearestHostile.distance);
                        end = blockPoint;
                        return true;
                    }
                    break;

                case LaserConstants.BlockMode.StopAtGroundOrHostile:
                    if (laserInfo.HitMode == LaserConstants.HitMode.FirstHitOnly && hasNearestHostile && (!hasNearestGround || nearestHostile.distance <= nearestGround.distance))
                    {
                        blockPoint = nearestHostile.point != Vector2.zero ? (Vector3)nearestHostile.point : (Vector3)(start + direction * nearestHostile.distance);
                        end = blockPoint;
                        return true;
                    }

                    if (hasNearestGround)
                    {
                        blockPoint = nearestGround.point != Vector2.zero ? (Vector3)nearestGround.point : (Vector3)(start + direction * nearestGround.distance);
                        end = blockPoint;
                        return true;
                    }
                    break;
            }

            return false;
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
        private void HandleProjectile(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not ProjectileEventDefinition def) return;
            if (ctx.caster == null) return;

            var casterChar = ctx.caster.GetComponent<CharacterBase>();
            if (casterChar == null) return;

            if (TableLoaderManager.Instance == null)
                return;

            // ----------------------
            // Center/Target resolve (Vfx와 동일한 정책)
            // ----------------------
            Vector3 casterPos = ctx.caster.transform.position;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            if (def.targetingOverride.enabled)
                mode = def.targetingOverride.mode;

            // 기본: Fixed 타겟이 있으면 전달하고, 좌표 기반이면 Override 좌표로 전달한다.
            CharacterBase targetChar = null;
            if (ctx.lockedTarget != null)
                targetChar = ctx.lockedTarget.GetComponent<CharacterBase>();

            bool usePosOverride = false;
            Vector2 posOverride = default;

            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    usePosOverride = true;
                    posOverride = new Vector2(groundPoint.x, groundPoint.y);
                    break;

                case ConfigCommonSkill.SkillTargetingMode.Self:
                    targetChar = casterChar;
                    usePosOverride = false;
                    break;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                    // 타겟이 없으면 좌표 기반으로 폴백
                    if (targetChar != null)
                    {
                        // Fixed 타입이면 Core에서 Target을 사용, Area 타입이면 Target 주변 샘플링을 사용할 수 있다.
                        usePosOverride = false;
                    }
                    else
                    {
                        usePosOverride = true;
                        posOverride = new Vector2(targetPos.x, targetPos.y);
                    }
                    break;

                default:
                    // Forward / fallback
                    var fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                    float range = SkillRangeResolver.GetPlacementRange(skill);
                    if (def.targetingOverride.enabled && def.targetingOverride.rangeOverride > 0f)
                        range = def.targetingOverride.rangeOverride;

                    var p = SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
                    usePosOverride = true;
                    posOverride = new Vector2(p.x, p.y);
                    break;
            }

            int attackId = ++_attackSequence;
            _chainUnlockByAttackId[attackId] = def.allowSkillChainOnConfirmedDamage;

            var meta = new MetadataProjectile(
                uid: def.projectileUid,
                damageType: def.damageType,
                damage: def.damage,
                target: targetChar,
                owner: casterChar,
                speedMultiplier: def.speedMultiplier,
                scaleMultiplier: def.scaleMultiplier,
                visualType: def.visualType,
                visualSprite: def.visualSprite,
                visualAnimatorController: def.visualAnimatorController,
                visualVfxUidOverride: def.visualVfxUidOverride,
                useTargetPositionOverride: usePosOverride,
                targetPositionOverride: posOverride,
                skillUid: skill.Uid,
                attackId: attackId,
                allowSkillChainOnConfirmedDamage: def.allowSkillChainOnConfirmedDamage,
                elementGaugeApplications: BuildElementGaugeApplications(def.onHitElementGauges, gameObject, damageApplied: true),
                useHitLifetimeModeOverride: def.useProjectileHitBehaviorOverride,
                hitLifetimeModeOverride: def.hitLifetimeMode,
                useDamageApplyModeOverride: def.useProjectileHitBehaviorOverride,
                damageApplyModeOverride: def.damageApplyMode,
                useTickDamageIntervalOverride: def.useProjectileHitBehaviorOverride && def.damageApplyMode == ProjectileConstants.DamageApplyMode.PeriodicOverlap,
                tickDamageIntervalOverride: Mathf.Max(0f, def.tickDamageIntervalSeconds));

            casterChar.LaunchProjectile(meta);
        }

        /// <summary>
        /// 데미지 이벤트 정의를 바탕으로 피격 대상을 평가하고 실제 데미지 및 OnHit 효과를 적용합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">데미지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="gizmoDurationSeconds">에디터 디버그용 데미지 영역 표시 시간입니다.</param>
        private void HandleDamage(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            float gizmoDurationSeconds)
        {
            if (payloadObj is not DamageEventDefinition def) return;

            // 기본값은 skill 테이블의 값(SSOT)
            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            float range = SkillRangeResolver.GetPlacementRange(skill);
            int maxTargets = skill.MaxTargets > 0 ? skill.MaxTargets : 1;

            // 이벤트 override 적용
            if (def.targetingOverride.enabled)
            {
                mode = def.targetingOverride.mode;
                if (def.targetingOverride.rangeOverride > 0f) range = def.targetingOverride.rangeOverride;
                if (def.targetingOverride.maxTargetsOverride > 0) maxTargets = def.targetingOverride.maxTargetsOverride;
            }

            // 타겟/중심점 결정
            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;
            bool useDamageStartSnapshot =
                def.damageCenterReference.mode == SkillPositionReferenceMode.SkillStartSnapshot ||
                (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter);

            if (useDamageStartSnapshot)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            // 캐릭터 Flip/방향을 반영한 2D forward 보정
            var resolvedForward = ResolveForward2D(ctx.caster, ctx.forward);

            // 히트 평가
            if (_hitEvaluator == null) return;
            var areaSpec = def.area;
            areaSpec.EnsureSaneDefaults();

            Vector3 center = casterPos;
            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    center = groundPoint;
                    break;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    center = targetPos;
                    break;
                default:
                    // Forward / Fallback
                    var fwd = resolvedForward.sqrMagnitude < 1e-6f ? Vector3.right : resolvedForward.normalized;
                    center = SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
                    break;
            }

            if (!TryApplyDamagePositionReference(run, def, ref center, ref resolvedForward, skill))
                return;

#if UNITY_EDITOR
            // SkillTestRuntimeHub가 에디터 전용 데미지 영역 데이터를 보관하고,
            // 실제 Gizmo 그리기는 Editor Drawer가 담당합니다.
            if (ctx.caster != null && SkillTestRuntimeHub.Instance != null)
            {
                SkillTestRuntimeHub.Instance.RegisterDamageArea(
                    center,
                    resolvedForward,
                    areaSpec,
                    gizmoDurationSeconds,
                    ctx.caster);
            }
#endif

            var hits = new List<GameObject>(Mathf.Max(1, maxTargets));
            _hitEvaluator.EvaluateTargets(center, resolvedForward, areaSpec, range, maxTargets, ctx.caster, hits);

            // 데미지 적용(현재는 로그/샘플 처리: 실제 데미지 모델은 프로젝트에 맞게 연동)
            var castCharacterBase = ctx.caster.GetComponent<CharacterBase>();

            // TODO: 데미지 계산 공식을 프로젝트 규칙에 맞게 적용해야 합니다.
            long totalDamage = 10;

            int attackId = ++_attackSequence;
            _chainUnlockByAttackId[attackId] = def.allowSkillChainOnConfirmedDamage;

            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i];
                if (go == null) continue;

                CharacterHitArea characterHitArea = go.GetComponentInChildren<CharacterHitArea>();
                if (characterHitArea == null) continue;

                CharacterBase target = characterHitArea.target;
                if (target == null) continue;

                if (!IsDamageTargetStateAllowed(def, target))
                    continue;

                // OnHit Affect / Crowd Control (BeforeDamage)
                ApplyOnHitAffects(def.onHitAffects, ctx.caster, target, damageApplied: false, timing: OnHitAffectTiming.BeforeDamage);
                int crowdControlUid = ResolveOnHitCrowdControlUid(
                    def.onHitCrowdControls,
                    damageApplied: false,
                    timing: OnHitCrowdControlTiming.BeforeDamage);

                bool hasPendingAfterDamageCrowdControl = HasPendingAfterDamageCrowdControl(
                    def.onHitCrowdControls,
                    damageApplied: true,
                    timing: OnHitCrowdControlTiming.AfterDamage);

                MetadataDamage metadataDamage = new MetadataDamage
                {
                    damage = totalDamage,
                    attacker = ctx.caster != null ? ctx.caster : gameObject,
                    damageType = ConfigCommon.DamageType.Physic,
                    affectUid = 0,
                    crowdControlUid = crowdControlUid,
                    AttackId = attackId,
                    SkillUid = skill.Uid,
                    HasPendingAfterDamageCrowdControl = hasPendingAfterDamageCrowdControl,
                    DamageCameraShakePreset = def.useCameraShakeOnHit ? def.cameraShakePreset : null,
                    DamageCameraShakeDirectionMode = def.cameraShakeDirectionMode,
                };

                bool didApplyDamage = false;

                // 몬스터와 마주보고 있으면 공격합니다.
                if (castCharacterBase.AreFacingEachOther(target))
                {
                    didApplyDamage = true;
                }
                // 같은 방향을 보고 있는 경우에는 상대 위치를 기준으로 피격 여부를 결정합니다.
                else if (castCharacterBase.CurrentFacing == target.CurrentFacing)
                {
                    switch (castCharacterBase.CurrentFacing)
                    {
                        case CharacterConstants.FacingDirection8.Right:
                        {
                            if (target.transform.position.x >= transform.position.x)
                            {
                                didApplyDamage = true;
                            }
                            break;
                        }
                        case CharacterConstants.FacingDirection8.Left:
                        {
                            if (target.transform.position.x <= transform.position.x)
                            {
                                didApplyDamage = true;
                            }
                            break;
                        }
                    }
                }

                _resolvedOnHitCrowdControls.Clear();
                CollectOnHitCrowdControlUids(
                    def.onHitCrowdControls,
                    didApplyDamage,
                    OnHitCrowdControlTiming.AfterDamage,
                    _resolvedOnHitCrowdControls);

                metadataDamage.ElementGaugeApplications = BuildElementGaugeApplications(def.onHitElementGauges, gameObject, didApplyDamage);

                if (didApplyDamage)
                {
                    metadataDamage.ResolvedOnHitCrowdControls = _resolvedOnHitCrowdControls;
                    target.TakeDamage(metadataDamage);
                    // 순서 중요. TakeDamage 먼저 처리
                    ApplyConfiguredHitStop(def, skill, castCharacterBase, target);
                }

                // OnHit Affect / Crowd Control (AfterDamage)
                // 이번 타격으로 대상이 사망했다면, 사망 대상에게 후속 Affect / CC를 다시 적용하지 않습니다.
                if (target.IsStatusDead())
                {
                    // NotifyOnHit는 TakeDamage 내부의 타격 확정 경로에서 이미 처리되므로 여기서는 생략 가능
                    continue;
                }
                
                // OnHit Affect / Crowd Control (AfterDamage)
                ApplyOnHitAffects(def.onHitAffects, ctx.caster, target, didApplyDamage, OnHitAffectTiming.AfterDamage);
            }
        }

        private static void ApplyConfiguredHitStop(DamageEventDefinition def, RuntimeSkillDefinition skill, CharacterBase caster, CharacterBase target)
        {
            if (def == null)
                return;

            var hitStopConfig = caster.GetResolvedHitStopConfig();
            if (caster != null && def.useHitStopSelf)
            {
                if (hitStopConfig.Enabled)
                {
                    float selfSeconds = def.useDefaultSelfHitStop ? hitStopConfig.DefaultSelfSeconds : Mathf.Max(0f, def.selfHitStopSeconds);
                    if (selfSeconds > 0f)
                    {
                        caster.ApplyHitStop(new HitStopRequest(
                            selfSeconds,
                            pauseAnimation: hitStopConfig.PauseAnimation,
                            freezePhysics: hitStopConfig.FreezePhysics,
                            sourceSkillUid: skill != null ? skill.Uid : 0));
                    }
                }
            }

            if (target != null && def.useHitStopTarget)
            {
                if (hitStopConfig.Enabled)
                {
                    float targetSeconds = def.useDefaultTargetHitStop ? hitStopConfig.DefaultReceiveSeconds : Mathf.Max(0f, def.targetHitStopSeconds);
                    if (targetSeconds > 0f)
                    {
                        target.ApplyHitStop(new HitStopRequest(
                            targetSeconds,
                            pauseAnimation: hitStopConfig.PauseAnimation,
                            freezePhysics: hitStopConfig.FreezePhysics,
                            sourceSkillUid: skill != null ? skill.Uid : 0));
                    }
                }
            }
        }

        /// <summary>
        /// 데미지 이벤트의 위치 참조 설정에 따라 판정 중심과 방향을 이름 있는 위치 앵커로 교체합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="def">데미지 이벤트 정의입니다.</param>
        /// <param name="center">현재 계산된 데미지 영역 중심입니다.</param>
        /// <param name="resolvedForward">현재 계산된 데미지 영역 방향입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <returns>데미지 처리를 계속할 수 있으면 <see langword="true"/>입니다.</returns>
        private static bool TryApplyDamagePositionReference(
            SkillRun run,
            DamageEventDefinition def,
            ref Vector3 center,
            ref Vector3 resolvedForward,
            RuntimeSkillDefinition skill)
        {
            if (def == null)
                return false;

            var reference = def.damageCenterReference;
            if (reference.mode != SkillPositionReferenceMode.NamedPositionAnchor &&
                reference.mode != SkillPositionReferenceMode.NamedPositionAnchorOrCurrent)
            {
                return true;
            }

            if (run != null && run.TryGetPositionAnchor(reference.key, out var snapshot))
            {
                center = snapshot.Position;
                if (snapshot.Forward.sqrMagnitude > 1e-6f)
                    resolvedForward = snapshot.Forward.normalized;
                return true;
            }

            if (reference.mode == SkillPositionReferenceMode.NamedPositionAnchorOrCurrent)
                return true;

            Debug.LogWarning(
                $"[SkillExecutor] Damage position anchor not found. skillUid={skill?.Uid ?? 0}, key={reference.key}");
            return false;
        }

        private static bool IsDamageTargetStateAllowed(DamageEventDefinition def, CharacterBase target)
        {
            if (def == null || target == null)
                return false;

            if (def.isGroundOnly && def.isAirOnly)
            {
                Debug.LogWarning($"[SkillExecutor] DamageEventDefinition has both isGroundOnly and isAirOnly enabled. The target will be skipped. target={target.name}", target);
                return false;
            }

            if (!def.isGroundOnly && !def.isAirOnly)
                return true;

            bool isGrounded = target.IsCurrentlyGrounded();
            if (def.isGroundOnly)
                return isGrounded;

            if (def.isAirOnly)
                return !isGrounded;

            return true;
        }

        /// <summary>
        /// VFX 이벤트가 계산한 최종 생성 위치를 같은 스킬 실행 안의 이름 있는 위치 앵커로 저장합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="spawnPos">VFX가 생성될 최종 월드 위치입니다.</param>
        /// <param name="resolvedForward">이벤트 시점에 해석된 2D 전방 방향입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        private static void SaveVfxPositionAnchorIfNeeded(
            SkillRun run,
            VfxEventDefinition def,
            Vector3 spawnPos,
            Vector3 resolvedForward,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint)
        {
            if (run == null || def == null || !def.positionAnchorWrite.enabled)
                return;

            if (string.IsNullOrWhiteSpace(def.positionAnchorWrite.key))
                return;

            var snapshot = new SkillPositionAnchorSnapshot(
                spawnPos,
                resolvedForward,
                casterPos,
                targetPos,
                groundPoint,
                run.CurrentTime);

            run.SavePositionAnchor(def.positionAnchorWrite.key, snapshot);
        }

        /// <summary>
        /// VFX 이벤트의 앵커 설정을 런타임 생성 위치 기준점으로 해석합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <returns>이벤트가 사용해야 할 생성 위치 기준점입니다.</returns>
        private static VfxSpawnAnchor ResolveVfxSpawnAnchor(VfxEventDefinition def)
        {
            if (def == null)
                return VfxSpawnAnchor.Caster;

            // 기존 RuntimeSequence 에셋은 Target 앵커를 attachToTarget 플래그로만 저장했습니다.
            // 새 spawnAnchor 필드가 없던 에셋도 타겟 부착 VFX는 이전처럼 타겟 기준으로 처리합니다.
            if (def.attachToTarget)
                return VfxSpawnAnchor.Target;

            return def.spawnAnchor;
        }

        /// <summary>
        /// VFX 이벤트에 명시된 타겟팅 오버라이드로 생성 월드 위치를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <returns>타겟팅 오버라이드가 가리키는 VFX 생성 월드 위치입니다.</returns>
        private static Vector3 ResolveVfxTargetingOverridePosition(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            VfxEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint)
        {
            switch (def.targetingOverride.mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    return groundPoint;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.TargetCenteredArea:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    return targetPos;
                case ConfigCommonSkill.SkillTargetingMode.Self:
                    return casterPos;
                default:
                    var fwd = ResolveForward2D(ctx.caster, ctx.forward);
                    float range = def.targetingOverride.rangeOverride > 0f
                        ? def.targetingOverride.rangeOverride
                        : SkillRangeResolver.GetPlacementRange(skill);
                    return SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
            }
        }

        /// <summary>
        /// VFX 이벤트의 앵커 설정에 따라 실제 생성 월드 위치를 계산합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <returns>VFX를 생성할 월드 위치입니다.</returns>
        private static Vector3 ResolveVfxSpawnPosition(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            VfxEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint)
        {
            if (def != null && def.targetingOverride.enabled)
                return ResolveVfxTargetingOverridePosition(skill, ctx, def, casterPos, targetPos, groundPoint);

            switch (ResolveVfxSpawnAnchor(def))
            {
                case VfxSpawnAnchor.Target:
                    return targetPos;
                case VfxSpawnAnchor.Ground:
                    return groundPoint;
                case VfxSpawnAnchor.Caster:
                default:
                    return casterPos;
            }
        }

        /// <summary>
        /// 생성된 VFX를 타겟 Transform에 부착해야 하는지 확인합니다.
        /// </summary>
        /// <param name="def">VFX 이벤트 정의입니다.</param>
        /// <returns>타겟에 부착해야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldAttachVfxToTarget(VfxEventDefinition def)
        {
            return ResolveVfxSpawnAnchor(def) == VfxSpawnAnchor.Target;
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
        private void HandleVfx(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not VfxEventDefinition def) return;

            // ----------------------
            // Spawn position resolve
            // ----------------------
            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            // VFX의 생성 위치는 스킬 타겟팅 모드가 아니라 SkillSpawnVfxClip의 Anchor 설정을 기준으로 결정합니다.
            Vector3 spawnPos = ResolveVfxSpawnPosition(skill, ctx, def, casterPos, targetPos, groundPoint);

            // localOffset은 월드 오프셋으로 처리(2D 프로젝트 기준: z는 그대로)
            spawnPos += def.localOffset;
            SaveVfxPositionAnchorIfNeeded(
                run,
                def,
                spawnPos,
                ResolveForward2D(ctx.caster, ctx.forward),
                casterPos,
                targetPos,
                groundPoint);

            // ----------------------
            // Vfx create
            // ----------------------
            VfxBehaviourBase vfx = null;
            var sceneGame = SceneGame.Instance;

            // 1) Core VfxManager 기반 생성(권장)
            if (sceneGame != null && sceneGame.VfxManager != null)
            {
                vfx = sceneGame.VfxManager.CreateVfx(BuildVfxSpawnRequest(def, spawnPos));
            }

            // 2) 폴백: 프리팹 직접 Instantiate
            if (vfx == null) return;

            if (ShouldAttachVfxToTarget(def) && ctx.lockedTarget != null)
            {
                vfx.transform.SetParent(ctx.lockedTarget.transform, worldPositionStays: true);
            }

            vfx.transform.position = spawnPos;
            RegisterSpawnedVfx(vfx);
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
        private void HandleApplyStatus(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not ApplyStatusEventDefinition def) return;
            if (ctx.caster == null) return;

            if (!TryParseAffectUid(def.statusId, out int affectUid))
                return;

            float chance = Mathf.Clamp01(def.chance01);
            if (chance <= 0f) return;
            if (chance < 0.9999f && UnityEngine.Random.value > chance)
                return;

            // Apply target resolve
            GameObject applyTarget;
            switch (def.applyTo)
            {
                case ApplyAffectTarget.LockedTarget:
                    applyTarget = ctx.lockedTarget != null ? ctx.lockedTarget : ctx.caster;
                    break;
                case ApplyAffectTarget.Caster:
                default:
                    applyTarget = ctx.caster;
                    break;
            }

            int stacks = Mathf.Max(1, def.stacks);
            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : 0f;

            for (int s = 0; s < stacks; s++)
            {
                // source는 caster로 유지합니다(버프/힐 출처 트래킹 용도)
                AffectApi.Apply(applyTarget, affectUid, ctx.caster, duration);
            }
        }


        /// <summary>
        /// 런타임 Temp HP(비저장 보호막/임시 하트)를 적용합니다.
        /// - 같은 source key가 다시 들어오면 누적하지 않고 설정값까지 다시 채웁니다.
        /// - 현재치가 모두 소모되면 Core 쪽에서 해당 source가 제거됩니다.
        /// </summary>
        private void HandleApplyTempHp(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj)
        {
            if (payloadObj is not ApplyTempHpEventDefinition def)
                return;

            if (ctx.caster == null)
                return;

            GameObject applyTarget;
            switch (def.applyTo)
            {
                case ApplyAffectTarget.LockedTarget:
                    applyTarget = ctx.lockedTarget != null ? ctx.lockedTarget : ctx.caster;
                    break;
                case ApplyAffectTarget.Caster:
                default:
                    applyTarget = ctx.caster;
                    break;
            }

            if (applyTarget == null)
                return;

            var targetCharacter = applyTarget.GetComponent<CharacterBase>() ?? applyTarget.GetComponentInParent<CharacterBase>();
            if (targetCharacter == null)
                return;

            long tempHpValue = def.tempHpValue > 0 ? def.tempHpValue : 0;
            int sourceKey = def.sourceKeyOverride != 0 ? def.sourceKeyOverride : skill.Uid;

            if (tempHpValue <= 0)
            {
                targetCharacter.ClearRuntimeBonusHpTemp(sourceKey);
                return;
            }

            targetCharacter.SetRuntimeBonusHpTemp(sourceKey, tempHpValue, fillToMax: true);
        }

        private static ElementGaugeApplication[] BuildElementGaugeApplications(OnHitElementGaugeEntry[] entries, GameObject caster, bool damageApplied)
        {
            if (entries == null || entries.Length == 0)
                return null;

            List<ElementGaugeApplication> results = null;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.damageType == ConfigCommon.DamageType.None || entry.damageType == ConfigCommon.DamageType.Physic)
                    continue;
                if (entry.gaugeValue <= 0f)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;
                if (entry.requireAffectUid > 0 && !AffectApi.HasAttached(caster, entry.requireAffectUid))
                    continue;

                float chance = entry.chance <= 0f ? 1f : Mathf.Clamp01(entry.chance);
                if (chance <= 0f)
                    continue;
                if (chance < 0.9999f && UnityEngine.Random.value > chance)
                    continue;

                results ??= new List<ElementGaugeApplication>(4);
                results.Add(new ElementGaugeApplication(entry.damageType, entry.gaugeValue));
            }

            return results != null && results.Count > 0 ? results.ToArray() : null;
        }


        /// <summary>
        /// OnHit 설정 목록을 순회하며 조건에 맞는 Affect를 대상에게 적용합니다.
        /// </summary>
        /// <param name="entries">적용할 OnHit Affect 목록입니다.</param>
        /// <param name="caster">효과의 출처가 되는 캐스터입니다.</param>
        /// <param name="target">효과를 적용할 대상입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        private static void ApplyOnHitAffects(OnHitAffectEntry[] entries, GameObject caster, CharacterBase target, bool damageApplied, OnHitAffectTiming timing)
        {
            if (entries == null || entries.Length == 0) return;
            if (caster == null || target == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.affectUid <= 0) continue;
                if (e.timing != timing) continue;
                if (e.requireDamageDealt && !damageApplied) continue;

                float chance = Mathf.Clamp01(e.chance);
                if (chance <= 0f) continue;
                if (chance < 0.9999f && UnityEngine.Random.value > chance) continue;

                int stacks = Mathf.Max(1, e.stacks);
                float duration = e.durationOverrideSeconds > 0f ? e.durationOverrideSeconds : 0f;

                for (int s = 0; s < stacks; s++)
                {
                    AffectApi.Apply(target.gameObject, e.affectUid, caster, duration);
                }
            }
        }


        /// <summary>
        /// 현재 시점에 적용 가능한 OnHit Crowd Control UID를 순서대로 수집합니다.
        /// 배열에 등록된 순서가 실행 순서가 됩니다.
        /// </summary>
        private static bool HasPendingAfterDamageCrowdControl(
            OnHitCrowdControlEntry[] entries,
            bool damageApplied,
            OnHitCrowdControlTiming timing)
        {
            if (entries == null || entries.Length == 0)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.crowdControlUid <= 0)
                    continue;
                if (entry.timing != timing)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;
                return true;
            }

            return false;
        }

        private void CollectOnHitCrowdControlUids(
            OnHitCrowdControlEntry[] entries,
            bool damageApplied,
            OnHitCrowdControlTiming timing,
            List<int> results)
        {
            results?.Clear();

            if (entries == null || entries.Length == 0 || results == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.crowdControlUid <= 0)
                    continue;
                if (entry.timing != timing)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;

                float chance = Mathf.Clamp01(entry.chance);
                if (chance <= 0f)
                    continue;
                if (chance < 0.9999f && UnityEngine.Random.value > chance)
                    continue;

                results.Add(entry.crowdControlUid);
            }
        }

        /// <summary>
        /// BeforeDamage처럼 단일 Crowd Control 전달이 필요한 구간에서 첫 번째 UID를 선택합니다.
        /// </summary>
        private int ResolveOnHitCrowdControlUid(OnHitCrowdControlEntry[] entries, bool damageApplied, OnHitCrowdControlTiming timing)
        {
            _resolvedOnHitCrowdControls.Clear();
            CollectOnHitCrowdControlUids(entries, damageApplied, timing, _resolvedOnHitCrowdControls);
            return _resolvedOnHitCrowdControls.Count > 0 ? _resolvedOnHitCrowdControls[0] : 0;
        }

        /// <summary>
        /// 상태 식별자를 Affect UID 정수값으로 변환합니다.
        /// </summary>
        /// <param name="id">파싱할 상태 식별자입니다.</param>
        /// <param name="affectUid">파싱에 성공한 Affect UID입니다.</param>
        /// <returns>유효한 양의 정수 UID로 변환되면 <see langword="true"/>를 반환합니다.</returns>
        private static bool TryParseAffectUid(StatusVfxId id, out int affectUid)
        {
            affectUid = 0;
            if (string.IsNullOrWhiteSpace(id.id)) return false;
            return int.TryParse(id.id, out affectUid) && affectUid > 0;
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
        private void HandleSpawnDummyCharacter(
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

            var sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.CharacterManager == null)
                return;

            if (def.characterUid <= 0)
            {
                Debug.LogWarning("[SkillExecutor] SpawnDummyCharacter characterUid must be greater than 0.");
                return;
            }

            string actorKey = NormalizeDummyActorKey(def.actorKey);
            if (string.IsNullOrEmpty(actorKey))
            {
                Debug.LogWarning("[SkillExecutor] SpawnDummyCharacter actorKey is empty.");
                return;
            }

            PruneDummyActors();

            if (_dummyActors.TryGetValue(actorKey, out var existing) && existing != null)
            {
                if (!def.replaceIfExists)
                    return;

                DestroyDummyActor(existing, destroyGameObject: true, removeFromRegistry: true);
            }

            Vector3 casterPos = ctx.caster != null ? ctx.caster.transform.position : snapshotCasterPos;
            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.useSnapshotCenter)
            {
                casterPos = snapshotCasterPos;
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            if (!TryResolveDummySpawnPosition(run, def, casterPos, targetPos, groundPoint, out Vector3 spawnPos))
                return;

            spawnPos += def.localOffset;

            int mapUid = sceneGame.mapManager != null ? sceneGame.mapManager.GetCurrentMapUid() : 0;
            var regenData = new CharacterRegenData(def.characterUid, spawnPos, flip: false, mapUid, defaultVisible: true);

            CharacterConstants.Type sourceType = ResolveDummySourceCharacterType(def.sourceType);
            GameObject dummyObject = sceneGame.CharacterManager.CreateDummyCharacter(
                sourceType,
                def.characterUid,
                regenData);

            if (dummyObject == null)
            {
                Debug.LogWarning($"[SkillExecutor] Failed to spawn dummy character. source={def.sourceType}, uid={def.characterUid}, key={actorKey}");
                return;
            }

            dummyObject.transform.position = new Vector3(spawnPos.x, spawnPos.y, dummyObject.transform.position.z);

            var character = dummyObject.GetComponent<CharacterBase>() ?? dummyObject.GetComponentInParent<CharacterBase>();
            if (character == null)
            {
                sceneGame.CharacterManager.RemoveCharacter(dummyObject);
                Debug.LogWarning($"[SkillExecutor] Spawned dummy has no CharacterBase. key={actorKey}, uid={def.characterUid}");
                return;
            }

            ConfigureDummyCharacterRuntime(character);

            var handle = new DummyActorHandle
            {
                ActorKey = actorKey,
                Character = character,
                DespawnOnSkillEnd = def.despawnOnSkillEnd,
                DespawnOnCancel = def.despawnOnCancel,
                GroundPosition = new Vector3(spawnPos.x, spawnPos.y, character.transform.position.z),
                AirHeight = 0f,
            };

            if (def.spawnFacing != CharacterConstants.FacingDirection8.None)
                character.SetFacing(def.spawnFacing);

            if (!string.IsNullOrWhiteSpace(def.initialAnimationName))
                PlayDummyAnimation(handle, def.initialAnimationName, def.initialAnimationLoop, def.initialAnimationTimeScale);

            var marker = character.GetComponent<SkillDummyCharacterMarker>();
            if (marker == null)
                marker = character.gameObject.AddComponent<SkillDummyCharacterMarker>();

            marker.Bind(actorKey, run != null ? run.SkillUid : (skill != null ? skill.Uid : 0));

            ApplyDummyRuntimeLocks(handle);
            _dummyActors[actorKey] = handle;
            ApplyDummyWorldPosition(handle);

            if (def.fadeInEnabled && def.fadeInDurationSeconds > 0f)
            {
                SetDummyVisualAlpha(character, 0f);
                handle.ActiveFadeCoroutine = StartCoroutine(FadeDummyCharacterCoroutine(
                    handle,
                    fadeIn: true,
                    durationSeconds: def.fadeInDurationSeconds,
                    destroyAfterFade: false,
                    removeFromRegistry: false));
            }
            else
            {
                SetDummyVisualAlpha(character, 1f);
            }
        }

        /// <summary>
        /// 스킬 더미 소스 타입을 코어 캐릭터 타입으로 변환합니다.
        /// </summary>
        /// <param name="sourceType">스킬 이벤트에서 지정한 더미 소스 타입입니다.</param>
        /// <returns>CharacterManager에서 사용하는 코어 캐릭터 타입입니다.</returns>
        private static CharacterConstants.Type ResolveDummySourceCharacterType(DummyCharacterSourceType sourceType)
        {
            return sourceType == DummyCharacterSourceType.Npc
                ? CharacterConstants.Type.Npc
                : CharacterConstants.Type.Monster;
        }

        /// <summary>
        /// 스킬 이벤트로 생성된 더미 캐릭터를 이동시킵니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotTargetPos">스킬 시작 시점 타겟 위치 스냅샷입니다.</param>
        /// <param name="snapshotGroundPoint">스킬 시작 시점 지면 기준점 스냅샷입니다.</param>
        private void HandleMoveDummyCharacter(
            SkillRun run,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not MoveDummyCharacterEventDefinition def)
                return;

            if (!TryResolveDummyActorHandle(ctx, def.actorReferenceType, def.actorKey, def.missingActorPolicy, out var handle))
                return;

            if (handle.Character == null)
                return;

            bool requiresLockedTarget =
                def.moveTargetMode == DummyMoveTargetMode.LockedTarget ||
                def.moveTargetMode == DummyMoveTargetMode.LockedTargetFront;

            if (requiresLockedTarget && !def.useSnapshotCenter && ctx.lockedTarget == null)
            {
                if (def.missingActorPolicy == DummyMissingActorPolicy.Warn)
                {
                    Debug.LogWarning($"[SkillExecutor] MoveDummyCharacter requires locked target. actor={GetDummyActorDisplayName(def.actorReferenceType, def.actorKey)}");
                }
                return;
            }

            Vector3 targetPos = ctx.lockedTarget != null ? ctx.lockedTarget.transform.position : snapshotTargetPos;
            Vector3 groundPoint = ctx.groundPoint;

            if (def.useSnapshotCenter)
            {
                targetPos = snapshotTargetPos;
                groundPoint = snapshotGroundPoint;
            }

            Vector3 actorPos = handle.Character.transform.position;
            if (!TryResolveDummyMoveTargetPosition(run, def, actorPos, targetPos, groundPoint, out Vector3 moveTarget))
                return;

            moveTarget += def.localOffset;

            if (def.playMoveAnimation && !string.IsNullOrWhiteSpace(def.moveAnimationName))
            {
                PlayDummyAnimation(handle, def.moveAnimationName, def.moveAnimationLoop, def.moveAnimationTimeScale);
            }

            StartDummyMove(handle, moveTarget, def);
        }

        /// <summary>
        /// 스킬 이벤트로 생성된 더미 캐릭터를 파괴(또는 비활성화)합니다.
        /// </summary>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        private void HandleDespawnDummyCharacter(UnityEngine.Object payloadObj)
        {
            if (payloadObj is not DespawnDummyCharacterEventDefinition def)
                return;

            if (!TryGetDummyActorHandle(def.actorKey, def.missingActorPolicy, out var handle))
                return;

            BeginDummyDespawn(
                handle,
                fadeOutEnabled: def.fadeOutEnabled,
                fadeOutDurationSeconds: def.fadeOutDurationSeconds,
                destroyAfterFade: def.destroyAfterFade,
                removeFromRegistry: true);
        }

        /// <summary>
        /// 스킬 이벤트로 더미 캐릭터의 공중 상태(높이/중력)를 제어합니다.
        /// </summary>
        /// <param name="payloadObj">이벤트 페이로드 오브젝트입니다.</param>
        private void HandleSetDummyAirborneState(UnityEngine.Object payloadObj)
        {
            if (payloadObj is not SetDummyAirborneStateEventDefinition def)
                return;

            if (!TryGetDummyActorHandle(def.actorKey, def.missingActorPolicy, out var handle))
                return;

            if (handle.Character == null)
                return;

            float targetAirHeight = def.airborneEnabled ? Mathf.Max(0f, def.targetAirHeight) : 0f;
            StartDummyAirHeightTransition(
                handle,
                targetAirHeight: targetAirHeight,
                durationSeconds: Mathf.Max(0f, def.durationSeconds),
                easing: def.easing,
                allowReplace: def.allowReplace,
                keepAirborneGravity: def.airborneEnabled || targetAirHeight > 0f);
        }

        /// <summary>
        /// 더미 캐릭터에 지정한 애니메이션을 재생하고 필요 시 후속 애니메이션 전환을 예약합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">애니메이션 이벤트 정의 페이로드입니다.</param>
        /// <param name="eventDurationSeconds">타임라인 클립 구간에서 계산한 이벤트 지속 시간(초)입니다.</param>
        private void HandlePlayDummyCharacterAnimation(SkillTargetContext ctx, UnityEngine.Object payloadObj, float eventDurationSeconds)
        {
            if (payloadObj is not PlayDummyCharacterAnimationEventDefinition def)
                return;

            if (!TryResolveDummyActorHandle(ctx, def.actorReferenceType, def.actorKey, def.missingActorPolicy, out var handle))
                return;

            PlayDummyAnimation(handle, def.animationName, def.loop, def.timeScale);

            if (def.durationPolicy != DummyAnimationDurationPolicy.UseClipWindow)
                return;

            if (def.endPolicy == DummyAnimationEndPolicy.None)
                return;

            float duration = Mathf.Max(0f, eventDurationSeconds);
            if (duration <= 0f)
            {
                ApplyDummyAnimationEndPolicy(handle, def.endPolicy, def.endAnimationName, def.endAnimationLoop, def.endAnimationTimeScale);
                return;
            }

            handle.ActiveAnimationCoroutine = StartCoroutine(DummyAnimationFollowupCoroutine(
                handle,
                ++handle.AnimationRequestVersion,
                duration,
                def.endPolicy,
                def.endAnimationName,
                def.endAnimationLoop,
                def.endAnimationTimeScale));
        }

        /// <summary>
        /// 더미 생성 기준점을 해석하여 최종 생성 위치를 계산합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="def">더미 생성 이벤트 정의입니다.</param>
        /// <param name="casterPos">해석된 캐스터 위치입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        /// <param name="spawnPos">계산된 생성 위치입니다.</param>
        /// <returns>생성 위치 계산에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveDummySpawnPosition(
            SkillRun run,
            SpawnDummyCharacterEventDefinition def,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            out Vector3 spawnPos)
        {
            spawnPos = casterPos;
            if (def == null)
                return false;

            switch (def.spawnAnchor)
            {
                case DummySpawnAnchor.Target:
                    spawnPos = targetPos;
                    return true;
                case DummySpawnAnchor.Ground:
                    spawnPos = groundPoint;
                    return true;
                case DummySpawnAnchor.NamedPositionAnchor:
                    if (TryResolveNamedAnchorPosition(run, def.namedAnchorKey, out spawnPos))
                        return true;

                    Debug.LogWarning($"[SkillExecutor] SpawnDummyCharacter named anchor not found. key={def.namedAnchorKey}");
                    return false;
                case DummySpawnAnchor.Caster:
                default:
                    spawnPos = casterPos;
                    return true;
            }
        }

        /// <summary>
        /// 더미 이동 목표 위치를 해석합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="def">더미 이동 이벤트 정의입니다.</param>
        /// <param name="actorPos">이동 대상 더미의 현재 월드 위치입니다.</param>
        /// <param name="targetPos">해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">해석된 지면 기준점입니다.</param>
        /// <param name="moveTarget">해석된 이동 목표 위치입니다.</param>
        /// <returns>이동 목표 해석에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveDummyMoveTargetPosition(
            SkillRun run,
            MoveDummyCharacterEventDefinition def,
            Vector3 actorPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            out Vector3 moveTarget)
        {
            moveTarget = targetPos;
            if (def == null)
                return false;

            switch (def.moveTargetMode)
            {
                case DummyMoveTargetMode.GroundPoint:
                    moveTarget = groundPoint;
                    return true;
                case DummyMoveTargetMode.LockedTarget:
                    moveTarget = targetPos;
                    return true;
                case DummyMoveTargetMode.LockedTargetFront:
                    float sideSign = ResolveTargetFrontSideSign(actorPos, targetPos);
                    float frontDistance = Mathf.Max(0f, def.targetFrontDistance);
                    moveTarget = targetPos + new Vector3(sideSign * frontDistance, 0f, 0f);
                    return true;
                case DummyMoveTargetMode.AbsoluteWorld:
                    moveTarget = def.absoluteWorldPosition;
                    return true;
                case DummyMoveTargetMode.NamedPositionAnchor:
                    if (TryResolveNamedAnchorPosition(run, def.namedAnchorKey, out moveTarget))
                        return true;

                    Debug.LogWarning($"[SkillExecutor] MoveDummyCharacter named anchor not found. key={def.namedAnchorKey}");
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 타겟 중심 대비 더미가 서 있던 좌/우 방향 부호를 계산합니다.
        /// </summary>
        /// <param name="actorPos">더미의 현재 월드 위치입니다.</param>
        /// <param name="targetPos">타겟 중심 월드 위치입니다.</param>
        /// <returns>더미가 타겟의 오른쪽이면 1, 왼쪽이면 -1이며 겹치면 1을 반환합니다.</returns>
        private static float ResolveTargetFrontSideSign(Vector3 actorPos, Vector3 targetPos)
        {
            float deltaX = actorPos.x - targetPos.x;
            if (Mathf.Abs(deltaX) <= 1e-4f)
                return 1f;

            return Mathf.Sign(deltaX);
        }

        /// <summary>
        /// 같은 스킬 런에 저장된 이름 있는 위치 앵커를 조회합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런입니다.</param>
        /// <param name="anchorKey">조회할 앵커 키입니다.</param>
        /// <param name="position">조회된 위치입니다.</param>
        /// <returns>앵커 조회에 성공하면 <see langword="true"/>입니다.</returns>
        private static bool TryResolveNamedAnchorPosition(SkillRun run, string anchorKey, out Vector3 position)
        {
            position = Vector3.zero;
            if (run == null || string.IsNullOrWhiteSpace(anchorKey))
                return false;

            if (!run.TryGetPositionAnchor(anchorKey, out var snapshot))
                return false;

            position = snapshot.Position;
            return true;
        }

        /// <summary>
        /// 더미 액터 키를 정규화합니다.
        /// </summary>
        /// <param name="actorKey">원본 액터 키입니다.</param>
        /// <returns>앞뒤 공백을 제거한 키이며, 비어 있으면 빈 문자열입니다.</returns>
        private static string NormalizeDummyActorKey(string actorKey)
        {
            return string.IsNullOrWhiteSpace(actorKey) ? string.Empty : actorKey.Trim();
        }

        /// <summary>
        /// 더미 이벤트가 지정한 대상(Actor/Caster)을 실제 런타임 핸들로 해석합니다.
        /// </summary>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="actorReferenceType">대상 참조 방식입니다.</param>
        /// <param name="actorKey">Actor 참조일 때 사용할 식별 키입니다.</param>
        /// <param name="missingPolicy">대상 미존재 시 처리 정책입니다.</param>
        /// <param name="handle">해석된 런타임 핸들입니다.</param>
        /// <returns>해석에 성공하면 <see langword="true"/>를 반환합니다.</returns>
        private bool TryResolveDummyActorHandle(
            SkillTargetContext ctx,
            DummyActorReferenceType actorReferenceType,
            string actorKey,
            DummyMissingActorPolicy missingPolicy,
            out DummyActorHandle handle)
        {
            switch (actorReferenceType)
            {
                case DummyActorReferenceType.Caster:
                    return TryGetCasterActorHandle(ctx, missingPolicy, out handle);
                case DummyActorReferenceType.Actor:
                default:
                    return TryGetDummyActorHandle(actorKey, missingPolicy, out handle);
            }
        }

        /// <summary>
        /// 현재 컨텍스트의 캐스터를 더미 액터 핸들 형태로 변환해 반환합니다.
        /// </summary>
        /// <param name="ctx">현재 스킬 실행 컨텍스트입니다.</param>
        /// <param name="missingPolicy">캐스터 해석 실패 시 처리 정책입니다.</param>
        /// <param name="handle">캐스터에 바인딩된 임시 핸들입니다.</param>
        /// <returns>캐스터를 핸들로 해석하면 <see langword="true"/>를 반환합니다.</returns>
        private bool TryGetCasterActorHandle(
            SkillTargetContext ctx,
            DummyMissingActorPolicy missingPolicy,
            out DummyActorHandle handle)
        {
            handle = null;

            if (ctx.caster == null)
            {
                if (missingPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning("[SkillExecutor] Dummy actor target is Caster, but caster is null.");
                return false;
            }

            var character = ResolveCharacterBase(ctx.caster);
            if (character == null)
            {
                if (missingPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning($"[SkillExecutor] Dummy actor target is Caster, but CharacterBase was not found. caster={ctx.caster.name}");
                return false;
            }

            if (!ReferenceEquals(_casterActorHandle.Character, character))
            {
                // 캐스터가 바뀌면 이전 캐스터에 예약된 이동/애니메이션 후속 작업을 정리합니다.
                ResetCasterActorHandleTransientState(clearCharacter: true);
                _casterActorHandle.Character = character;
            }

            _casterActorHandle.ActorKey = CasterActorKey;
            _casterActorHandle.AirHeight = 0f;
            SyncDummyGroundFromTransform(_casterActorHandle);
            handle = _casterActorHandle;
            return true;
        }

        /// <summary>
        /// Caster 참조 임시 핸들에 남아 있는 이동/애니메이션 후속 작업을 정리합니다.
        /// </summary>
        /// <param name="clearCharacter">
        /// <see langword="true"/>이면 캐릭터 참조까지 제거합니다.
        /// <see langword="false"/>이면 캐릭터 참조는 유지하고 코루틴/상태만 초기화합니다.
        /// </param>
        private void ResetCasterActorHandleTransientState(bool clearCharacter)
        {
            if (_casterActorHandle.ActiveMoveCoroutine != null)
            {
                StopCoroutine(_casterActorHandle.ActiveMoveCoroutine);
                _casterActorHandle.ActiveMoveCoroutine = null;
            }

            if (_casterActorHandle.ActiveFadeCoroutine != null)
            {
                StopCoroutine(_casterActorHandle.ActiveFadeCoroutine);
                _casterActorHandle.ActiveFadeCoroutine = null;
            }

            if (_casterActorHandle.ActiveAirHeightCoroutine != null)
            {
                StopCoroutine(_casterActorHandle.ActiveAirHeightCoroutine);
                _casterActorHandle.ActiveAirHeightCoroutine = null;
            }

            CancelDummyAnimationFollowup(_casterActorHandle);
            ReleaseDummyGravityOverride(_casterActorHandle);
            ReleaseDummyRuntimeLocks(_casterActorHandle);

            if (!clearCharacter)
                return;

            _casterActorHandle.Character = null;
            _casterActorHandle.GroundPosition = Vector3.zero;
            _casterActorHandle.AirHeight = 0f;
        }

        /// <summary>
        /// 더미 이벤트 로그에 표시할 대상 식별 문자열을 반환합니다.
        /// </summary>
        /// <param name="actorReferenceType">대상 참조 방식입니다.</param>
        /// <param name="actorKey">Actor 참조 시 원본 actorKey입니다.</param>
        /// <returns>로그 출력용 대상 식별 문자열입니다.</returns>
        private static string GetDummyActorDisplayName(DummyActorReferenceType actorReferenceType, string actorKey)
        {
            return actorReferenceType == DummyActorReferenceType.Caster
                ? "Caster"
                : NormalizeDummyActorKey(actorKey);
        }

        /// <summary>
        /// actorKey에 해당하는 더미 핸들을 조회합니다.
        /// </summary>
        /// <param name="actorKey">조회할 더미 액터 키입니다.</param>
        /// <param name="missingPolicy">미존재 시 로깅 정책입니다.</param>
        /// <param name="handle">조회된 더미 핸들입니다.</param>
        /// <returns>조회에 성공하면 <see langword="true"/>입니다.</returns>
        private bool TryGetDummyActorHandle(string actorKey, DummyMissingActorPolicy missingPolicy, out DummyActorHandle handle)
        {
            handle = null;

            string normalizedKey = NormalizeDummyActorKey(actorKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                if (missingPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning("[SkillExecutor] Dummy actorKey is empty.");
                return false;
            }

            PruneDummyActors();

            if (_dummyActors.TryGetValue(normalizedKey, out handle) && handle != null && handle.Character != null)
                return true;

            _dummyActors.Remove(normalizedKey);
            handle = null;

            if (missingPolicy == DummyMissingActorPolicy.Warn)
                Debug.LogWarning($"[SkillExecutor] Dummy actor not found. key={normalizedKey}");

            return false;
        }

        /// <summary>
        /// 더미 캐릭터 이동을 시작합니다.
        /// </summary>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="targetPosition">이동 목표 위치입니다.</param>
        /// <param name="def">이동 이벤트 정의입니다.</param>
        private void StartDummyMove(DummyActorHandle handle, Vector3 targetPosition, MoveDummyCharacterEventDefinition def)
        {
            if (handle == null || handle.Character == null || def == null)
                return;

            SyncDummyGroundFromTransform(handle);
            var character = handle.Character;
            Vector3 targetGroundPosition = new Vector3(targetPosition.x, targetPosition.y, handle.GroundPosition.z);

            if (handle.ActiveMoveCoroutine != null)
            {
                if (!def.allowReplace)
                    return;

                StopCoroutine(handle.ActiveMoveCoroutine);
                handle.ActiveMoveCoroutine = null;
            }

            var motion = ResolveMotionController(character.gameObject);
            if (motion != null && motion.IsPlaying(MotionChannel.Skill))
            {
                if (!def.allowReplace)
                    return;

                motion.CancelMotion(MotionChannel.Skill, reason: 9202);
            }

            Vector3 currentGroundPosition = handle.GroundPosition;
            Vector2 delta = new Vector2(
                targetGroundPosition.x - currentGroundPosition.x,
                targetGroundPosition.y - currentGroundPosition.y);
            float distance = delta.magnitude;
            float duration = Mathf.Max(0f, def.durationSeconds);

            if (distance <= 1e-4f || duration <= 0f)
            {
                handle.GroundPosition = targetGroundPosition;
                ApplyDummyWorldPosition(handle);
                return;
            }

            // 더미는 지면 좌표와 공중 높이를 분리 관리하므로 Transform 보간으로 지면 좌표만 갱신합니다.
            handle.ActiveMoveCoroutine = StartCoroutine(CoMoveDummyByTransform(
                handle,
                currentGroundPosition,
                targetGroundPosition,
                duration,
                def.easing));
        }

        /// <summary>
        /// 더미 캐릭터의 지면 좌표를 보간하여 이동시키고, 공중 높이를 합성해 최종 위치를 갱신합니다.
        /// </summary>
        /// <param name="handle">이동 대상 더미 핸들입니다.</param>
        /// <param name="from">시작 위치입니다.</param>
        /// <param name="to">도착 위치입니다.</param>
        /// <param name="durationSeconds">이동 시간(초)입니다.</param>
        /// <param name="easeType">보간 easing입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator CoMoveDummyByTransform(
            DummyActorHandle handle,
            Vector3 from,
            Vector3 to,
            float durationSeconds,
            Easing.EaseType easeType)
        {
            if (handle == null || handle.Character == null)
                yield break;

            float duration = Mathf.Max(0.0001f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (handle.Character == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Easing.Apply(t, easeType);
                handle.GroundPosition = Vector3.LerpUnclamped(from, to, eased);
                ApplyDummyWorldPosition(handle);
                yield return null;
            }

            if (handle.Character != null)
            {
                handle.GroundPosition = to;
                ApplyDummyWorldPosition(handle);
            }

            handle.ActiveMoveCoroutine = null;
        }

        /// <summary>
        /// 더미 캐릭터의 지면 기준 좌표를 현재 Transform 값으로 동기화합니다.
        /// </summary>
        /// <param name="handle">동기화할 더미 핸들입니다.</param>
        private static void SyncDummyGroundFromTransform(DummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            Vector3 worldPosition = handle.Character.transform.position;
            handle.GroundPosition = new Vector3(
                worldPosition.x,
                worldPosition.y - handle.AirHeight,
                worldPosition.z);
        }

        /// <summary>
        /// 더미 캐릭터의 지면 좌표와 공중 높이로 최종 월드 좌표를 계산합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <returns>지면 좌표 + 공중 높이가 반영된 월드 좌표입니다.</returns>
        private static Vector3 ComposeDummyWorldPosition(DummyActorHandle handle)
        {
            return new Vector3(
                handle.GroundPosition.x,
                handle.GroundPosition.y + Mathf.Max(0f, handle.AirHeight),
                handle.GroundPosition.z);
        }

        /// <summary>
        /// 더미 캐릭터에 현재 지면 좌표/공중 높이를 반영하여 Transform을 갱신합니다.
        /// </summary>
        /// <param name="handle">위치를 갱신할 더미 핸들입니다.</param>
        private static void ApplyDummyWorldPosition(DummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            handle.Character.transform.position = ComposeDummyWorldPosition(handle);
        }

        /// <summary>
        /// 공중 상태로 유지 중인 더미의 중력/속도/좌표를 프레임마다 보정합니다.
        /// 스킬 런이 끝난 뒤에도 더미가 유지되는 구성에서 물리 오차로 서서히 내려오는 현상을 방지합니다.
        /// </summary>
        private void MaintainDummyAirborneState()
        {
            if (_dummyActors.Count == 0)
                return;

            foreach (var pair in _dummyActors)
            {
                MaintainDummyAirborneState(pair.Value);
            }
        }

        /// <summary>
        /// 단일 더미의 공중 유지 상태를 점검하고, 필요 시 중력 오버라이드 및 월드 좌표를 재적용합니다.
        /// </summary>
        /// <param name="handle">점검할 더미 핸들입니다.</param>
        private static void MaintainDummyAirborneState(DummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            if (handle.AirHeight <= 1e-4f)
                return;

            EnsureDummyGravityOverride(handle);
            ZeroDummyRigidbodyVelocity(handle);
            ApplyDummyWorldPosition(handle);
        }

        /// <summary>
        /// 더미를 수동 좌표 제어할 때 물리 속도로 인해 위치가 미세하게 누적되는 현상을 방지하기 위해 속도를 0으로 고정합니다.
        /// </summary>
        /// <param name="handle">속도를 보정할 더미 핸들입니다.</param>
        private static void ZeroDummyRigidbodyVelocity(DummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            var rb = handle.Character.characterRigidbody2D != null
                ? handle.Character.characterRigidbody2D
                : handle.Character.GetComponent<Rigidbody2D>();

            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
                return;

            rb.SetLinearVelocity(Vector2.zero);
            rb.angularVelocity = 0f;
        }

        /// <summary>
        /// 더미 캐릭터의 공중 높이 전환을 시작합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <param name="targetAirHeight">목표 공중 높이(+Y)입니다.</param>
        /// <param name="durationSeconds">보간 시간(초)입니다.</param>
        /// <param name="easing">보간 easing입니다.</param>
        /// <param name="allowReplace">기존 공중 보간 덮어쓰기 허용 여부입니다.</param>
        /// <param name="keepAirborneGravity">완료 후에도 공중 중력 오버라이드를 유지할지 여부입니다.</param>
        private void StartDummyAirHeightTransition(
            DummyActorHandle handle,
            float targetAirHeight,
            float durationSeconds,
            Easing.EaseType easing,
            bool allowReplace,
            bool keepAirborneGravity)
        {
            if (handle == null || handle.Character == null)
                return;

            if (handle.ActiveAirHeightCoroutine != null)
            {
                if (!allowReplace)
                    return;

                StopCoroutine(handle.ActiveAirHeightCoroutine);
                handle.ActiveAirHeightCoroutine = null;
            }

            SyncDummyGroundFromTransform(handle);

            float startHeight = Mathf.Max(0f, handle.AirHeight);
            float endHeight = Mathf.Max(0f, targetAirHeight);
            float duration = Mathf.Max(0f, durationSeconds);

            if (keepAirborneGravity || startHeight > 0f || endHeight > 0f)
                EnsureDummyGravityOverride(handle);

            ZeroDummyRigidbodyVelocity(handle);

            if (Mathf.Abs(endHeight - startHeight) <= 1e-4f || duration <= 0f)
            {
                handle.AirHeight = endHeight;
                ApplyDummyWorldPosition(handle);
                ZeroDummyRigidbodyVelocity(handle);

                if (!keepAirborneGravity && endHeight <= 0f)
                    ReleaseDummyGravityOverride(handle);
                return;
            }

            handle.ActiveAirHeightCoroutine = StartCoroutine(CoDummyAirHeightTransition(
                handle,
                startHeight,
                endHeight,
                duration,
                easing,
                keepAirborneGravity));
        }

        /// <summary>
        /// 더미 캐릭터 공중 높이 전환을 프레임 단위로 보간합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <param name="startAirHeight">시작 공중 높이입니다.</param>
        /// <param name="targetAirHeight">목표 공중 높이입니다.</param>
        /// <param name="durationSeconds">보간 시간(초)입니다.</param>
        /// <param name="easing">보간 easing입니다.</param>
        /// <param name="keepAirborneGravity">완료 후 중력 오버라이드 유지 여부입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator CoDummyAirHeightTransition(
            DummyActorHandle handle,
            float startAirHeight,
            float targetAirHeight,
            float durationSeconds,
            Easing.EaseType easing,
            bool keepAirborneGravity)
        {
            if (handle == null || handle.Character == null)
                yield break;

            float duration = Mathf.Max(0.0001f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (handle.Character == null)
                {
                    handle.ActiveAirHeightCoroutine = null;
                    ReleaseDummyGravityOverride(handle);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Easing.Apply(t, easing);
                handle.AirHeight = Mathf.Lerp(startAirHeight, targetAirHeight, eased);
                ApplyDummyWorldPosition(handle);
                ZeroDummyRigidbodyVelocity(handle);
                yield return null;
            }

            if (handle.Character != null)
            {
                handle.AirHeight = targetAirHeight;
                ApplyDummyWorldPosition(handle);
                ZeroDummyRigidbodyVelocity(handle);
            }

            handle.ActiveAirHeightCoroutine = null;

            if (!keepAirborneGravity && targetAirHeight <= 0f)
                ReleaseDummyGravityOverride(handle);
        }

        /// <summary>
        /// 더미 캐릭터의 중력을 비활성화하는 오버라이드를 획득합니다.
        /// </summary>
        /// <param name="handle">중력 오버라이드를 적용할 더미 핸들입니다.</param>
        private static void EnsureDummyGravityOverride(DummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            var physicsOverride = handle.Character.GetComponent<CharacterPhysicsOverrideController>();
            if (physicsOverride == null)
                physicsOverride = handle.Character.gameObject.AddComponent<CharacterPhysicsOverrideController>();

            if (physicsOverride == null)
                return;

            if (handle.GravityOverrideHandle.IsValid)
            {
                if (ReferenceEquals(handle.PhysicsOverrideController, physicsOverride))
                    return;

                if (handle.PhysicsOverrideController != null)
                    handle.PhysicsOverrideController.ReleaseGravityOverride(ref handle.GravityOverrideHandle);
                else
                    handle.GravityOverrideHandle = default;
            }

            handle.PhysicsOverrideController = physicsOverride;
            handle.GravityOverrideHandle = physicsOverride.AcquireGravityOverride(
                ownerKey: handle,
                lifecycleOwner: handle.Character,
                channel: CharacterPhysicsOverrideChannel.Skill,
                priority: CharacterPhysicsOverridePriority.Skill,
                gravityScale: 0f,
                reason: "SkillDummyAirborne");
        }

        /// <summary>
        /// 더미 캐릭터에 적용한 중력 오버라이드를 해제합니다.
        /// </summary>
        /// <param name="handle">중력 오버라이드를 해제할 더미 핸들입니다.</param>
        private static void ReleaseDummyGravityOverride(DummyActorHandle handle)
        {
            if (handle == null)
                return;

            if (handle.PhysicsOverrideController != null && handle.GravityOverrideHandle.IsValid)
                handle.PhysicsOverrideController.ReleaseGravityOverride(ref handle.GravityOverrideHandle);
            else
                handle.GravityOverrideHandle = default;

            handle.PhysicsOverrideController = null;
        }

        /// <summary>
        /// 더미 캐릭터 제거를 시작합니다.
        /// </summary>
        /// <param name="handle">제거할 더미 핸들입니다.</param>
        /// <param name="fadeOutEnabled">페이드 아웃 사용 여부입니다.</param>
        /// <param name="fadeOutDurationSeconds">페이드 아웃 시간(초)입니다.</param>
        /// <param name="destroyAfterFade">페이드 이후 Destroy 여부입니다.</param>
        /// <param name="removeFromRegistry">완료 후 레지스트리 제거 여부입니다.</param>
        private void BeginDummyDespawn(
            DummyActorHandle handle,
            bool fadeOutEnabled,
            float fadeOutDurationSeconds,
            bool destroyAfterFade,
            bool removeFromRegistry)
        {
            if (handle == null)
                return;

            if (handle.ActiveMoveCoroutine != null)
            {
                StopCoroutine(handle.ActiveMoveCoroutine);
                handle.ActiveMoveCoroutine = null;
            }

            if (handle.ActiveAirHeightCoroutine != null)
            {
                StopCoroutine(handle.ActiveAirHeightCoroutine);
                handle.ActiveAirHeightCoroutine = null;
            }

            CancelDummyAnimationFollowup(handle);

            if (handle.Character == null)
            {
                ReleaseDummyGravityOverride(handle);
                if (removeFromRegistry && !string.IsNullOrEmpty(handle.ActorKey))
                    _dummyActors.Remove(handle.ActorKey);
                return;
            }

            var motion = ResolveMotionController(handle.Character.gameObject);
            motion?.CancelMotion(MotionChannel.Skill, reason: 9203);
            ReleaseDummyGravityOverride(handle);

            float duration = Mathf.Max(0f, fadeOutDurationSeconds);
            if (fadeOutEnabled && duration > 0f)
            {
                if (handle.ActiveFadeCoroutine != null)
                {
                    StopCoroutine(handle.ActiveFadeCoroutine);
                    handle.ActiveFadeCoroutine = null;
                }

                handle.ActiveFadeCoroutine = StartCoroutine(FadeDummyCharacterCoroutine(
                    handle,
                    fadeIn: false,
                    durationSeconds: duration,
                    destroyAfterFade: destroyAfterFade,
                    removeFromRegistry: removeFromRegistry));
                return;
            }

            DestroyDummyActor(handle, destroyGameObject: destroyAfterFade, removeFromRegistry: removeFromRegistry);
        }

        /// <summary>
        /// 더미 캐릭터 페이드 인/아웃을 처리합니다.
        /// </summary>
        /// <param name="handle">페이드를 적용할 더미 핸들입니다.</param>
        /// <param name="fadeIn">페이드 인이면 <see langword="true"/>입니다.</param>
        /// <param name="durationSeconds">페이드 시간(초)입니다.</param>
        /// <param name="destroyAfterFade">페이드 아웃 완료 후 Destroy 여부입니다.</param>
        /// <param name="removeFromRegistry">완료 후 레지스트리 제거 여부입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator FadeDummyCharacterCoroutine(
            DummyActorHandle handle,
            bool fadeIn,
            float durationSeconds,
            bool destroyAfterFade,
            bool removeFromRegistry)
        {
            if (handle == null || handle.Character == null)
            {
                if (handle != null && removeFromRegistry && !string.IsNullOrEmpty(handle.ActorKey))
                    _dummyActors.Remove(handle.ActorKey);
                yield break;
            }

            var character = handle.Character;
            var anim = ResolveAnimController(character.gameObject);
            float duration = Mathf.Max(0f, durationSeconds);

            if (duration <= 0f)
            {
                SetDummyVisualAlpha(character, fadeIn ? 1f : 0f);
            }
            else if (anim != null)
            {
                if (fadeIn)
                    SetDummyVisualAlpha(character, 0f);

                yield return anim.FadeEffect(duration, fadeIn);
                SetDummyVisualAlpha(character, fadeIn ? 1f : 0f);
            }
            else
            {
                float startAlpha = fadeIn ? 0f : 1f;
                float endAlpha = fadeIn ? 1f : 0f;
                float elapsed = 0f;
                SetDummyVisualAlpha(character, startAlpha);

                while (elapsed < duration)
                {
                    if (handle.Character == null)
                        yield break;

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SetDummyVisualAlpha(handle.Character, Mathf.Lerp(startAlpha, endAlpha, t));
                    yield return null;
                }

                if (handle.Character != null)
                    SetDummyVisualAlpha(handle.Character, endAlpha);
            }

            handle.ActiveFadeCoroutine = null;

            if (fadeIn)
                yield break;

            DestroyDummyActor(handle, destroyGameObject: destroyAfterFade, removeFromRegistry: removeFromRegistry);
        }

        /// <summary>
        /// 더미 캐릭터 비주얼 알파값을 설정합니다.
        /// </summary>
        /// <param name="character">알파를 적용할 캐릭터입니다.</param>
        /// <param name="alpha">적용할 알파값(0~1)입니다.</param>
        private static void SetDummyVisualAlpha(CharacterBase character, float alpha)
        {
            if (character == null)
                return;

            float clamped = Mathf.Clamp01(alpha);
            var anim = ResolveAnimController(character.gameObject);
            anim?.SetCharacterColor(new Color(1f, 1f, 1f, clamped));

            var spriteRenderers = character.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                if (sr == null)
                    continue;

                Color color = sr.color;
                color.a = clamped;
                sr.color = color;
            }
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 요청을 시작합니다.
        /// 기존에 예약된 후속 애니메이션 전환이 있으면 취소한 뒤 새 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="handle">애니메이션을 재생할 더미 핸들입니다.</param>
        /// <param name="animationName">재생할 애니메이션 이름입니다.</param>
        /// <param name="loop">루프 재생 여부입니다.</param>
        /// <param name="timeScale">재생 속도 배율입니다.</param>
        private void PlayDummyAnimation(DummyActorHandle handle, string animationName, bool loop, float timeScale)
        {
            if (handle == null)
                return;

            CancelDummyAnimationFollowup(handle);
            PlayDummyAnimation(handle.Character, animationName, loop, timeScale);
        }

        /// <summary>
        /// 더미 캐릭터에 예약된 애니메이션 후속 전환을 취소합니다.
        /// </summary>
        /// <param name="handle">취소할 더미 핸들입니다.</param>
        private void CancelDummyAnimationFollowup(DummyActorHandle handle)
        {
            if (handle == null)
                return;

            if (handle.ActiveAnimationCoroutine != null)
            {
                StopCoroutine(handle.ActiveAnimationCoroutine);
                handle.ActiveAnimationCoroutine = null;
            }

            handle.AnimationRequestVersion++;
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 유지 시간이 끝난 뒤 후속 애니메이션 전환을 처리합니다.
        /// </summary>
        /// <param name="handle">후속 전환을 적용할 더미 핸들입니다.</param>
        /// <param name="requestVersion">예약 당시의 애니메이션 요청 버전입니다.</param>
        /// <param name="durationSeconds">대기 시간(초)입니다.</param>
        /// <param name="endPolicy">대기 완료 후 적용할 종료 정책입니다.</param>
        /// <param name="endAnimationName">커스텀 종료 애니메이션 이름입니다.</param>
        /// <param name="endAnimationLoop">커스텀 종료 애니메이션 루프 여부입니다.</param>
        /// <param name="endAnimationTimeScale">커스텀 종료 애니메이션 재생 속도 배율입니다.</param>
        /// <returns>코루틴 이터레이터입니다.</returns>
        private IEnumerator DummyAnimationFollowupCoroutine(
            DummyActorHandle handle,
            int requestVersion,
            float durationSeconds,
            DummyAnimationEndPolicy endPolicy,
            string endAnimationName,
            bool endAnimationLoop,
            float endAnimationTimeScale)
        {
            float remaining = Mathf.Max(0f, durationSeconds);
            while (remaining > 0f)
            {
                if (handle == null || handle.Character == null)
                    yield break;

                if (handle.AnimationRequestVersion != requestVersion)
                    yield break;

                remaining -= Time.deltaTime;
                yield return null;
            }

            if (handle == null || handle.Character == null)
                yield break;

            if (handle.AnimationRequestVersion != requestVersion)
                yield break;

            handle.ActiveAnimationCoroutine = null;
            ApplyDummyAnimationEndPolicy(handle, endPolicy, endAnimationName, endAnimationLoop, endAnimationTimeScale);
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 종료 정책을 적용합니다.
        /// </summary>
        /// <param name="handle">종료 정책을 적용할 더미 핸들입니다.</param>
        /// <param name="endPolicy">적용할 종료 정책입니다.</param>
        /// <param name="endAnimationName">커스텀 종료 애니메이션 이름입니다.</param>
        /// <param name="endAnimationLoop">커스텀 종료 애니메이션 루프 여부입니다.</param>
        /// <param name="endAnimationTimeScale">커스텀 종료 애니메이션 재생 속도 배율입니다.</param>
        private static void ApplyDummyAnimationEndPolicy(
            DummyActorHandle handle,
            DummyAnimationEndPolicy endPolicy,
            string endAnimationName,
            bool endAnimationLoop,
            float endAnimationTimeScale)
        {
            if (handle == null || handle.Character == null)
                return;

            switch (endPolicy)
            {
                case DummyAnimationEndPolicy.PlayWait:
                    PlayDummyAnimation(handle.Character, ICharacterAnimationController.WaitForwardAnim, true, 1f);
                    break;
                case DummyAnimationEndPolicy.PlayCustom:
                    PlayDummyAnimation(handle.Character, endAnimationName, endAnimationLoop, endAnimationTimeScale);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="character">애니메이션 대상 캐릭터입니다.</param>
        /// <param name="animationName">재생할 애니메이션 이름입니다.</param>
        /// <param name="loop">루프 재생 여부입니다.</param>
        /// <param name="timeScale">재생 속도 배율입니다.</param>
        private static void PlayDummyAnimation(CharacterBase character, string animationName, bool loop, float timeScale)
        {
            if (character == null || string.IsNullOrWhiteSpace(animationName))
                return;

            var anim = ResolveAnimController(character.gameObject);
            if (anim == null)
                return;

            anim.PlaySkillAnimation(new SkillAnimationRequest(
                skillUid: 0,
                phase: SkillAnimationPhase.Action,
                loop: loop,
                timeScale: Mathf.Max(0f, timeScale),
                overrideAnimationName: animationName));
        }

        /// <summary>
        /// 더미 캐릭터의 런타임 제어 상태를 정리합니다.
        /// </summary>
        /// <param name="character">정리할 더미 캐릭터입니다.</param>
        private static void ConfigureDummyCharacterRuntime(CharacterBase character)
        {
            if (character == null)
                return;

            var brainTicker = character.GetComponent<MonsterBrainTicker>();
            if (brainTicker != null)
                brainTicker.enabled = false;

            var controllers = character.GetComponents<CharacterBaseController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                    controllers[i].enabled = false;
            }

            var behaviours = character.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null)
                    continue;

                if (mb is IMonsterBrain || mb.GetType().Name == "MonsterBtRunner")
                    mb.enabled = false;
            }

            var rb = character.characterRigidbody2D != null
                ? character.characterRigidbody2D
                : character.GetComponentInParent<Rigidbody2D>();
            if (rb != null)
            {
                rb.SetLinearVelocity(Vector2.zero);
                rb.angularVelocity = 0f;
            }

            character.SetAggro(false);
            character.SetAttackerTarget(null);
            character.SetStatusIdle();

            var anim = ResolveAnimController(character.gameObject);
            anim?.PlayWaitAnimation();
        }

        /// <summary>
        /// 더미 캐릭터 런타임에 불필요한 스크립트를 정리합니다.
        /// 애니메이션/이동에 필요한 컴포넌트와 필수 의존성만 유지합니다.
        /// </summary>
        /// <param name="character">정리 대상 더미 캐릭터입니다.</param>
        /// <summary>
        /// 더미 캐릭터 런타임에서 반드시 유지해야 하는 스크립트인지 판별합니다.
        /// </summary>
        /// <param name="behaviour">판별 대상 스크립트입니다.</param>
        /// <returns>유지 대상이면 <see langword="true"/>를 반환합니다.</returns>
        /// <summary>
        /// 더미 캐릭터 런타임에서 제거 대상 스크립트인지 판별합니다.
        /// </summary>
        /// <param name="behaviour">판별 대상 스크립트입니다.</param>
        /// <returns>제거 대상이면 <see langword="true"/>를 반환합니다.</returns>
        /// <summary>
        /// 더미 캐릭터 제어/브레인 잠금을 적용합니다.
        /// </summary>
        /// <param name="handle">잠금을 적용할 더미 핸들입니다.</param>
        private static void ApplyDummyRuntimeLocks(DummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            if (handle.ControlLockToken == null)
                handle.ControlLockToken = handle.Character.AcquireControlLock(handle);

            if (handle.BrainLockToken == null)
                handle.BrainLockToken = handle.Character.AcquireBrainLock(handle);
        }

        /// <summary>
        /// 더미 캐릭터 제어/브레인 잠금을 해제합니다.
        /// </summary>
        /// <param name="handle">잠금을 해제할 더미 핸들입니다.</param>
        private static void ReleaseDummyRuntimeLocks(DummyActorHandle handle)
        {
            if (handle == null)
                return;

            var character = handle.Character;
            if (character != null)
            {
                if (handle.ControlLockToken != null)
                    character.ReleaseControlLock(handle.ControlLockToken);

                if (handle.BrainLockToken != null)
                    character.ReleaseBrainLock(handle.BrainLockToken);
            }

            handle.ControlLockToken = null;
            handle.BrainLockToken = null;
        }

        /// <summary>
        /// 더미 캐릭터를 즉시 정리합니다.
        /// </summary>
        /// <param name="handle">정리할 더미 핸들입니다.</param>
        /// <param name="destroyGameObject">Destroy 수행 여부입니다.</param>
        /// <param name="removeFromRegistry">레지스트리 제거 여부입니다.</param>
        private void DestroyDummyActor(DummyActorHandle handle, bool destroyGameObject, bool removeFromRegistry)
        {
            if (handle == null)
                return;

            if (handle.ActiveMoveCoroutine != null)
            {
                StopCoroutine(handle.ActiveMoveCoroutine);
                handle.ActiveMoveCoroutine = null;
            }

            if (handle.ActiveFadeCoroutine != null)
            {
                StopCoroutine(handle.ActiveFadeCoroutine);
                handle.ActiveFadeCoroutine = null;
            }

            if (handle.ActiveAirHeightCoroutine != null)
            {
                StopCoroutine(handle.ActiveAirHeightCoroutine);
                handle.ActiveAirHeightCoroutine = null;
            }

            CancelDummyAnimationFollowup(handle);

            var character = handle.Character;
            if (character != null)
            {
                var motion = ResolveMotionController(character.gameObject);
                motion?.CancelMotion(MotionChannel.Skill, reason: 9201);

                ReleaseDummyGravityOverride(handle);
                ReleaseDummyRuntimeLocks(handle);

                if (destroyGameObject)
                {
                    var sceneGame = SceneGame.Instance;
                    if (sceneGame != null && sceneGame.CharacterManager != null)
                        sceneGame.CharacterManager.RemoveCharacter(character.gameObject);
                    else
                        Destroy(character.gameObject);
                }
                else
                {
                    character.gameObject.SetActive(false);
                }
            }
            else
            {
                ReleaseDummyGravityOverride(handle);
                ReleaseDummyRuntimeLocks(handle);
            }

            if (removeFromRegistry && !string.IsNullOrEmpty(handle.ActorKey))
                _dummyActors.Remove(handle.ActorKey);

            handle.Character = null;
            handle.AirHeight = 0f;
        }

        /// <summary>
        /// 레지스트리에서 파괴된 더미 핸들을 정리합니다.
        /// </summary>
        private void PruneDummyActors()
        {
            if (_dummyActors.Count == 0)
                return;

            var keysToRemove = new List<string>();
            foreach (var pair in _dummyActors)
            {
                if (pair.Value == null || pair.Value.Character == null)
                {
                    if (pair.Value != null)
                        ReleaseDummyGravityOverride(pair.Value);
                    keysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _dummyActors.Remove(keysToRemove[i]);
            }
        }

        /// <summary>
        /// 현재 등록된 더미 캐릭터를 종료 정책에 맞게 정리합니다.
        /// </summary>
        /// <param name="forceAll">모든 더미를 강제 정리할지 여부입니다.</param>
        /// <param name="forCancel">취소 종료 기준(<see langword="true"/>) 또는 정상 종료 기준(<see langword="false"/>)을 선택합니다.</param>
        private void CleanupDummyActors(bool forceAll, bool forCancel)
        {
            if (_dummyActors.Count == 0)
                return;

            var keys = new List<string>(_dummyActors.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (!_dummyActors.TryGetValue(key, out var handle) || handle == null)
                {
                    _dummyActors.Remove(key);
                    continue;
                }

                bool shouldCleanup = forceAll || (forCancel ? handle.DespawnOnCancel : handle.DespawnOnSkillEnd);
                if (!shouldCleanup)
                    continue;

                DestroyDummyActor(handle, destroyGameObject: true, removeFromRegistry: true);
            }

            PruneDummyActors();
        }

        /// <summary>
        /// 지정한 오브젝트 계층에서 <see cref="CharacterBase"/>를 탐색합니다.
        /// </summary>
        /// <param name="target">탐색 기준 GameObject입니다.</param>
        /// <returns>탐색된 캐릭터가 있으면 반환하고, 없으면 <see langword="null"/>을 반환합니다.</returns>
        private static CharacterBase ResolveCharacterBase(GameObject target)
        {
            if (target == null)
                return null;

            var character = target.GetComponent<CharacterBase>();
            if (character != null)
                return character;

            character = target.GetComponentInChildren<CharacterBase>(includeInactive: true);
            if (character != null)
                return character;

            return target.GetComponentInParent<CharacterBase>();
        }

        /// <summary>
        /// 캐스터에서 사용할 애니메이션 컨트롤러를 현재 오브젝트, 자식, 부모 순으로 탐색합니다.
        /// </summary>
        /// <param name="caster">애니메이션 컨트롤러를 찾을 기준 오브젝트입니다.</param>
        /// <returns>찾은 애니메이션 컨트롤러 또는 찾지 못한 경우 <see langword="null"/>입니다.</returns>
        private static ICharacterAnimationController ResolveAnimController(GameObject caster)
        {
            if (caster == null) return null;

            if (caster.TryGetComponent<ICharacterAnimationController>(out var anim))
                return anim;

            anim = caster.GetComponentInChildren<ICharacterAnimationController>(includeInactive: true);
            if (anim != null) return anim;

            return caster.GetComponentInParent<ICharacterAnimationController>();
        }

        /// <summary>
        /// 캐스터에서 사용할 액션 컨트롤러를 현재 오브젝트, 자식, 부모 순으로 탐색합니다.
        /// </summary>
        /// <param name="caster">액션 컨트롤러를 찾을 기준 오브젝트입니다.</param>
        /// <returns>찾은 액션 컨트롤러 또는 찾지 못한 경우 <see langword="null"/>입니다.</returns>
        private static ICharacterActionController ResolveActionController(GameObject caster)
        {
            if (caster == null) return null;

            if (caster.TryGetComponent<ICharacterActionController>(out var action))
                return action;

            action = caster.GetComponentInChildren<ICharacterActionController>(includeInactive: true);
            if (action != null) return action;

            return caster.GetComponentInParent<ICharacterActionController>();
        }

        /// <summary>
        /// 캐스터에서 사용할 모션 컨트롤러를 현재 오브젝트, 자식, 부모 순으로 탐색합니다.
        /// </summary>
        /// <param name="caster">모션 컨트롤러를 찾을 기준 오브젝트입니다.</param>
        /// <returns>찾은 모션 컨트롤러 또는 찾지 못한 경우 <see langword="null"/>입니다.</returns>
        private static ICharacterMotionController ResolveMotionController(GameObject caster)
        {
            if (caster == null) return null;

            if (caster.TryGetComponent<ICharacterMotionController>(out var motion))
                return motion;

            motion = caster.GetComponentInChildren<ICharacterMotionController>(includeInactive: true);
            if (motion != null) return motion;

            return caster.GetComponentInParent<ICharacterMotionController>();
        }

        /// <summary>
        /// 지정한 런이 현재 활성 런과 동일하고 이벤트를 처리 가능한 상태인지 반환합니다.
        /// </summary>
        internal bool CanProcessEvent(SkillRun run)
        {
            return run != null && ReferenceEquals(_current, run) && !run.IsDone;
        }

        /// <summary>
        /// 스킬 런 종료 시 실행기에 남아 있는 참조를 정리합니다.
        /// </summary>
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

        internal void NotifyRunEnded(SkillRun run)
        {
            if (run == null || !ReferenceEquals(_current, run))
                return;

            var report = _hasPendingFinishReport
                ? _pendingFinishReport
                : new SkillExecutionReport(run.SkillUid, MonsterSkillExecutionState.Succeeded, ++_executionSequence, Time.time);

            _hasPendingFinishReport = false;
            _current = null;
            _chainUnlockByAttackId.Clear();
            CleanupDummyActors(forceAll: false, forCancel: false);
            ExecutionFinished?.Invoke(report);
        }

        private void RegisterSpawnedVfx(VfxBehaviourBase vfx)
        {
            if (vfx == null)
                return;

            _spawnedVfxs.RemoveAll(x => x == null);
            _spawnedVfxs.Add(vfx);
        }

        private void CleanupSpawnedVfxs()
        {
            if (_spawnedVfxs.Count == 0)
                return;

            for (int i = _spawnedVfxs.Count - 1; i >= 0; i--)
            {
                var vfx = _spawnedVfxs[i];
                if (vfx != null)
                {
                    Destroy(vfx.gameObject);
                }
            }

            _spawnedVfxs.Clear();
        }

        private static void ClearDamageAreaGizmo(GameObject caster)
        {
#if UNITY_EDITOR
            SkillTestRuntimeHub.Instance?.ClearDamageAreas(caster);
            SkillTestRuntimeHub.Instance?.ClearLasers(caster);
#endif
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
            ClearDamageAreaGizmo(run.Caster);
            CleanupSpawnedVfxs();
            CleanupDummyActors(forceAll: false, forCancel: true);
            ClearGroundSlamAnimationState();
            ClearPendingGroundSlamState();
            ClearArcLungeAnimationState();

            _pendingFinishReport = new SkillExecutionReport(run.SkillUid, MonsterSkillExecutionState.Canceled, ++_executionSequence, Time.time);
            _hasPendingFinishReport = true;

            run.Cancel(reason);
            if (ReferenceEquals(_current, run))
            {
                _current = null; // 즉시 종료(추가 Tick/이벤트 전달 방지)
                _chainUnlockByAttackId.Clear();
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
