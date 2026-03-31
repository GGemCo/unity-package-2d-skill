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
        /// 실행기에 필요한 런타임 의존성을 초기화합니다.
        /// </summary>
        private void Awake()
        {
            _hitEvaluator = new AreaHitEvaluator(hitMask);
            _hitStopController = GetComponent<CharacterHitStopController>();
        }

        private void OnDisable()
        {
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
                    HandleDamage(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint, damageGizmoDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.SpawnVfx:
                    HandleVfx(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
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
                case ConfigCommonSkill.SkillEventType.PositionHold:
                    float positionHoldDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandlePositionHold(run, ctx, payload, positionHoldDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.GroundSlam:
                    float groundSlamDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandleGroundSlam(ctx, payload, groundSlamDuration);
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
                allowSkillChainOnConfirmedDamage: def.allowSkillChainOnConfirmedDamage);

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

            if (def.targetingOverride.enabled && def.targetingOverride.useSnapshotCenter)
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
                };

                bool didApplyDamage = false;

                // 몬스터와 마주보고 있으면 공격합니다.
                if (castCharacterBase.AreFacingEachOther(target))
                {
                    target.TakeDamage(metadataDamage);
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
                                target.TakeDamage(metadataDamage);
                                didApplyDamage = true;
                            }
                            break;
                        }
                        case CharacterConstants.FacingDirection8.Left:
                        {
                            if (target.transform.position.x <= transform.position.x)
                            {
                                target.TakeDamage(metadataDamage);
                                didApplyDamage = true;
                            }
                            break;
                        }
                    }
                }

                if (didApplyDamage)
                {
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

                _resolvedOnHitCrowdControls.Clear();
                CollectOnHitCrowdControlUids(
                    def.onHitCrowdControls,
                    didApplyDamage,
                    OnHitCrowdControlTiming.AfterDamage,
                    _resolvedOnHitCrowdControls);

                if (didApplyDamage && _resolvedOnHitCrowdControls.Count > 0)
                {
                    target.ApplyCrowdControlSequence(_resolvedOnHitCrowdControls, ctx.caster != null ? ctx.caster : gameObject);
                }
            }
        }

        private static void ApplyConfiguredHitStop(DamageEventDefinition def, RuntimeSkillDefinition skill, CharacterBase caster, CharacterBase target)
        {
            if (def == null)
                return;

            if (caster != null && def.useHitStopSelf)
            {
                var casterConfig = caster.GetResolvedHitStopConfig();
                if (casterConfig.Enabled)
                {
                    float selfSeconds = def.useDefaultSelfHitStop ? casterConfig.DefaultSelfSeconds : Mathf.Max(0f, def.selfHitStopSeconds);
                    if (selfSeconds > 0f)
                    {
                        caster.ApplyHitStop(new HitStopRequest(
                            selfSeconds,
                            lockControl: casterConfig.LockControl,
                            lockMovement: casterConfig.LockMovement,
                            pauseAnimation: casterConfig.PauseAnimation,
                            freezePhysics: casterConfig.FreezePhysics,
                            sourceSkillUid: skill != null ? skill.Uid : 0));
                    }
                }
            }

            if (target != null && def.useHitStopTarget)
            {
                var targetConfig = target.GetResolvedHitStopConfig();
                if (targetConfig.Enabled)
                {
                    float targetSeconds = def.useDefaultTargetHitStop ? targetConfig.DefaultReceiveSeconds : Mathf.Max(0f, def.targetHitStopSeconds);
                    if (targetSeconds > 0f)
                    {
                        target.ApplyHitStop(new HitStopRequest(
                            targetSeconds,
                            lockControl: targetConfig.LockControl,
                            lockMovement: targetConfig.LockMovement,
                            pauseAnimation: targetConfig.PauseAnimation,
                            freezePhysics: targetConfig.FreezePhysics,
                            sourceSkillUid: skill != null ? skill.Uid : 0));
                    }
                }
            }
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
        /// 이펙트 이벤트 정의를 바탕으로 생성 위치를 계산하고 이펙트를 생성합니다.
        /// </summary>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">이펙트 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        private void HandleVfx(
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

            // 기본은 caster 기준. attachToTarget이면 target 기준.
            Vector3 spawnPos = def.attachToTarget ? targetPos : casterPos;
            if (!def.attachToTarget)
            {
                var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
                if (def.targetingOverride.enabled)
                    mode = def.targetingOverride.mode;

                switch (mode)
                {
                    case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                        spawnPos = groundPoint;
                        break;
                    case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                    case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                        spawnPos = targetPos;
                        break;
                    default:
                        // Forward / fallback
                        var fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                        float range = SkillRangeResolver.GetPlacementRange(skill);
                        if (def.targetingOverride.enabled && def.targetingOverride.rangeOverride > 0f)
                            range = def.targetingOverride.rangeOverride;
                        spawnPos = SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
                        break;
                }
            }

            // localOffset은 월드 오프셋으로 처리(2D 프로젝트 기준: z는 그대로)
            spawnPos += def.localOffset;

            // ----------------------
            // Vfx create
            // ----------------------
            VfxBehaviourBase vfx = null;
            var sceneGame = SceneGame.Instance;

            // 1) Core VfxManager 기반 생성(권장)
            if (sceneGame != null && sceneGame.VfxManager != null)
            {
                TryResolveVfxDuration(def, out var vfxDuration);
                vfx = sceneGame.VfxManager.CreateVfx(def.vfxUid, vfxDuration);
            }

            // 2) 폴백: 프리팹 직접 Instantiate
            if (vfx == null) return;

            if (def.attachToTarget && ctx.lockedTarget != null)
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
#endif
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