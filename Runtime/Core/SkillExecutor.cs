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
        private readonly List<DefaultEffect> _spawnedEffects = new();

        /// <summary>
        /// 현재 스킬 실행 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsBusy => _current != null;

        /// <summary>
        /// 실행기에 필요한 런타임 의존성을 초기화합니다.
        /// </summary>
        private void Awake()
        {
            _hitEvaluator = new AreaHitEvaluator(hitMask);
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
            if (_current == null) return;

            var run = _current;
            run.Tick(Time.deltaTime);
            if (ReferenceEquals(_current, run) && run.IsDone)
            {
                NotifyRunEnded(run);
            }
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

            CleanupSpawnedEffects();

            _current = new SkillRun(this, skill, targetCtx,
                ResolveAnimController(targetCtx.caster),
                ResolveActionController(targetCtx.caster));
            _current.Start();
            return true;
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
                case ConfigCommonSkill.SkillEventType.SpawnEffect:
                    HandleEffect(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.ApplyAffect:
                    HandleApplyStatus(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                case ConfigCommonSkill.SkillEventType.Lunge:
                    float lungeDuration = Mathf.Max(0f, e.EndTime - e.StartTime);
                    HandleLunge(ctx, payload, lungeDuration);
                    break;
                case ConfigCommonSkill.SkillEventType.Projectile:
                    HandleProjectile(skill, ctx, payload, snapshotCasterPos, snapshotTargetPos, snapshotGroundPoint);
                    break;
                default:
                    break;
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
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">돌진 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="eventDurationSeconds">이벤트 구간에서 계산된 기본 지속 시간입니다.</param>
        private void HandleLunge(
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            float eventDurationSeconds)
        {
            if (payloadObj is not LungeEventDefinition def) return;
            if (ctx.caster == null) return;

            // 모션 컨트롤러는 캐릭터(플레이어/몬스터) 공용 컴포넌트에서 제공한다.
            var motion = ctx.caster.GetComponentInParent<ICharacterMotionController>();
            if (motion == null) return;

            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : eventDurationSeconds;
            if (duration <= 0f) return;

            Vector2 dir2 = def.useSnapshotForward
                ? new Vector2(ResolveForward2D(ctx.caster, ctx.forward).x, ResolveForward2D(ctx.caster, ctx.forward).y)
                : ResolveCurrentFacing2D(ctx.caster);

            if (def.invertForward)
                dir2 = -dir2;

            if (Mathf.Abs(dir2.x) < 1e-4f)
            {
                float sign = Mathf.Sign(ctx.caster.transform.localScale.x);
                if (Mathf.Approximately(sign, 0f))
                    sign = 1f;

                dir2 = new Vector2(sign, 0f);
            }

            dir2 = new Vector2(Mathf.Sign(dir2.x), 0f);

            var kind = def.useArcMotion && def.arcHeight > 0f ? MotionKind.Arc : MotionKind.Linear;

            var req = new MotionRequest(
                MotionChannel.Skill,
                kind,
                dir2,
                duration,
                def.distance,
                def.easing,
                arcHeight: def.useArcMotion ? def.arcHeight : 0f,
                holdSecondsAfter: 0f,
                stopAtEnd: def.stopAtEnd,
                useMovePosition: def.useMovePosition,
                allowReplace: def.allowReplace);

            motion.TryStartMotion(in req);
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
            // Center/Target resolve (Effect와 동일한 정책)
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
                visualEffectUidOverride: def.visualEffectUidOverride,
                useTargetPositionOverride: usePosOverride,
                targetPositionOverride: posOverride);

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
            // SkillDamageClip이 처리되는 동안만 데미지 영역을 Gizmo로 표시합니다.
            // (에디터 PlayMode 테스트 및 씬 디버깅 용도)
            if (ctx.caster != null)
            {
                var gizmo = ctx.caster.GetComponent<GGemCo2DSkillEditor.SkillDamageAreaGizmo>();
                if (gizmo != null)
                {
                    gizmo.Show(center, resolvedForward, areaSpec, range, gizmoDurationSeconds, ctx.caster);
                }
            }
#endif

            var hits = new List<GameObject>(Mathf.Max(1, maxTargets));
            _hitEvaluator.EvaluateTargets(center, resolvedForward, areaSpec, range, maxTargets, ctx.caster, hits);

            // 데미지 적용(현재는 로그/샘플 처리: 실제 데미지 모델은 프로젝트에 맞게 연동)
            var castCharacterBase = ctx.caster.GetComponent<CharacterBase>();

            // TODO: 데미지 계산 공식을 프로젝트 규칙에 맞게 적용해야 합니다.
            long totalDamage = 10;

            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i];
                if (go == null) continue;

                CharacterHitArea characterHitArea = go.GetComponentInChildren<CharacterHitArea>();
                if (characterHitArea == null) continue;

                CharacterBase target = characterHitArea.target;

                // OnHit Affect (BeforeDamage)
                ApplyOnHitAffects(def.onHitAffects, ctx.caster, target, damageApplied: false, timing: OnHitAffectTiming.BeforeDamage);

                MetadataDamage metadataDamage = new MetadataDamage
                {
                    damage = totalDamage,
                    attacker = gameObject,
                    damageType = ConfigCommon.DamageType.Physic,
                    affectUid = 0
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

                // OnHit Affect (AfterDamage)
                ApplyOnHitAffects(def.onHitAffects, ctx.caster, target, didApplyDamage, OnHitAffectTiming.AfterDamage);

                // 공격 성공 알림(공격자 버프의 OnHit 트리거 등)
                if (didApplyDamage)
                {
                    AffectApi.NotifyOnHit(ctx.caster, target.gameObject);
                }
            }
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
        private void HandleEffect(
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            if (payloadObj is not EffectEventDefinition def) return;

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
            // Effect create
            // ----------------------
            DefaultEffect effect = null;
            var sceneGame = SceneGame.Instance;

            // 1) Core EffectManager 기반 생성(권장)
            if (sceneGame != null && sceneGame.EffectManager != null)
            {
                effect = sceneGame.EffectManager.CreateEffect(def.effectUid);
            }

            // 2) 폴백: 프리팹 직접 Instantiate
            if (effect == null) return;

            // lifetimeSeconds가 지정되면 Core DefaultEffect의 duration을 사용
            if (def.lifetimeSeconds > 0f)
                effect.SetDuration(def.lifetimeSeconds);

            if (def.attachToTarget && ctx.lockedTarget != null)
            {
                effect.transform.SetParent(ctx.lockedTarget.transform, worldPositionStays: true);
            }

            effect.transform.position = spawnPos;
            RegisterSpawnedEffect(effect);
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
        /// 상태 식별자를 Affect UID 정수값으로 변환합니다.
        /// </summary>
        /// <param name="id">파싱할 상태 식별자입니다.</param>
        /// <param name="affectUid">파싱에 성공한 Affect UID입니다.</param>
        /// <returns>유효한 양의 정수 UID로 변환되면 <see langword="true"/>를 반환합니다.</returns>
        private static bool TryParseAffectUid(StatusEffectId id, out int affectUid)
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

            _current = null;
        }

        private void RegisterSpawnedEffect(DefaultEffect effect)
        {
            if (effect == null)
                return;

            _spawnedEffects.RemoveAll(x => x == null);
            _spawnedEffects.Add(effect);
        }

        private void CleanupSpawnedEffects()
        {
            if (_spawnedEffects.Count == 0)
                return;

            for (int i = _spawnedEffects.Count - 1; i >= 0; i--)
            {
                var effect = _spawnedEffects[i];
                if (effect != null)
                {
                    Destroy(effect.gameObject);
                }
            }

            _spawnedEffects.Clear();
        }

        private static void ClearDamageAreaGizmo(GameObject caster)
        {
#if UNITY_EDITOR
            if (caster == null)
                return;

            var gizmo = caster.GetComponent<GGemCo2DSkillEditor.SkillDamageAreaGizmo>();
            gizmo?.ClearAll();
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
            CleanupSpawnedEffects();

            run.Cancel(reason);
            if (ReferenceEquals(_current, run))
            {
                _current = null; // 즉시 종료(추가 Tick/이벤트 전달 방지)
            }

            return true;
        }
    }
}