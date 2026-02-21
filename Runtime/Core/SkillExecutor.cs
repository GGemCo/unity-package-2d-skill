using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 스킬 실행기(Authoring V2).
    /// - SSOT: Core의 skill 테이블(Uid 기반)
    /// - 연출 타이밍: Addressables로 로드한 <see cref="SkillRuntimeSequence"/>
    /// - 애니메이션: 클립 이름 규칙 + Playables(Animator 파라미터 미사용)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillExecutor : MonoBehaviour
    {
        [Header("Hit Evaluator")]
        [SerializeField] private LayerMask hitMask = ~0;

        private IHitEvaluator _hitEvaluator;

        private SkillRun _current;
        public bool IsBusy => _current != null;

        private void Awake()
        {
            _hitEvaluator = new AreaHitEvaluator(hitMask);
        }

        private void Update()
        {
            if (_current == null) return;

            _current.Tick(Time.deltaTime);
            if (_current.IsDone) _current = null;
        }

        /// <summary>
        /// 스킬 사용을 시도합니다(테이블 Uid 기반).
        /// </summary>
        public bool TryUse(int skillUid, SkillTargetContext targetCtx)
        {
            if (_current != null) return false;

            var table = TableLoaderManager.Instance != null ? TableLoaderManagerSkill.Instance.TableSkill : null;
            if (table == null) return false;

            if (!table.GetDatas().TryGetValue(skillUid, out var skill) || skill == null) return false;

            _current = new SkillRun(this, skill, targetCtx,
                ResolveAnimController(targetCtx.caster),
                ResolveActionController(targetCtx.caster));
            _current.Start();
            return true;
        }

        public void ExecuteEvent(
            StruckTableSkill skill,
            SkillTargetContext ctx,
            SkillRuntimeSequence sequence,
            in SkillRuntimeEvent e,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
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

            // 2D 기준 방향 보정
            Vector3 fwd3 = def.useSnapshotForward ? ctx.forward : (ctx.forward);
            Vector2 dir2 = new Vector2(fwd3.x, fwd3.y);

            // ctx.forward가 기본값(Vector3.forward)인 경우(2D) localScale.x 기반으로 보정
            if (dir2.sqrMagnitude < 1e-6f || Mathf.Abs(fwd3.z) > 0.5f)
            {
                float sign = Mathf.Sign(ctx.caster.transform.localScale.x);
                if (Mathf.Approximately(sign, 0f)) sign = 1f;
                dir2 = new Vector2(sign, 0f);
            }

            var req = new LungeRequest(dir2, duration, def.distance, def.easing, def.stopAtEnd, def.useMovePosition);
            motion.TryStartLunge(in req);
        }

        
        private void HandleProjectile(
            StruckTableSkill skill,
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
                    float range = skill.Range > 0f ? skill.Range : 3f;
                    if (def.targetingOverride.enabled && def.targetingOverride.rangeOverride > 0f)
                        range = def.targetingOverride.rangeOverride;

                    var p = casterPos + fwd * Mathf.Max(0.1f, range);
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

        private void HandleDamage(
            StruckTableSkill skill,
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
            float range = skill.Range > 0f ? skill.Range : 3f;
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
                    var fwd = ctx.forward.sqrMagnitude < 1e-6f ? Vector3.right : ctx.forward.normalized;
                    center = casterPos + fwd * Mathf.Max(0.1f, range);
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
                    gizmo.Show(center, ctx.forward, areaSpec, range, gizmoDurationSeconds);
                }
            }
#endif

            var hits = new List<GameObject>(Mathf.Max(1, maxTargets));
            _hitEvaluator.EvaluateTargets(center, ctx.forward, areaSpec, range, maxTargets, ctx.caster, hits);

            // 데미지 적용(현재는 로그/샘플 처리: 실제 데미지 모델은 프로젝트에 맞게 연동)
            var castCharacterBase = ctx.caster.GetComponent<CharacterBase>();
            long totalDamage = 10;
            for (int i = 0; i < hits.Count; i++)
            {
                var go = hits[i];
                if (go == null) continue;
                
                CharacterHitArea characterHitArea = go.GetComponentInChildren<CharacterHitArea>();
                if (characterHitArea == null) continue;
                
                // GcLogger.Log("Player attacked the monster after animation!");
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

                // 몬스터와 마주보고 있으면 공격 
                if (castCharacterBase.AreFacingEachOther(target))
                {
                    target.TakeDamage(metadataDamage);
                    didApplyDamage = true;
                }
                // 몬스터와 같은 곳을 바라보고 있으면,
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

        private void HandleEffect(
            StruckTableSkill skill,
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
                        float range = skill.Range > 0f ? skill.Range : 3f;
                        spawnPos = casterPos + fwd * Mathf.Max(0.1f, range);
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
        }

        private void HandleApplyStatus(
            StruckTableSkill skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint)
        {
            // SkillApplyAffectClip의 payload. (시전자에게만 적용)
            if (payloadObj is not ApplyStatusEventDefinition def) return;
            if (ctx.caster == null) return;

            if (!TryParseAffectUid(def.statusId, out int affectUid))
                return;

            float chance = Mathf.Clamp01(def.chance01);
            if (chance <= 0f) return;
            if (chance < 0.9999f && UnityEngine.Random.value > chance)
                return;

            int stacks = Mathf.Max(1, def.stacks);
            float duration = def.durationOverrideSeconds > 0f ? def.durationOverrideSeconds : 0f;

            for (int s = 0; s < stacks; s++)
            {
                AffectApi.Apply(ctx.caster, affectUid, ctx.caster, duration);
            }
        }

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

        private static bool TryParseAffectUid(StatusEffectId id, out int affectUid)
        {
            affectUid = 0;
            if (string.IsNullOrWhiteSpace(id.id)) return false;
            return int.TryParse(id.id, out affectUid) && affectUid > 0;
        }
        
        private static ICharacterAnimationController ResolveAnimController(GameObject caster)
        {
            if (caster == null) return null;

            if (caster.TryGetComponent<ICharacterAnimationController>(out var anim))
                return anim;

            anim = caster.GetComponentInChildren<ICharacterAnimationController>(includeInactive: true);
            if (anim != null) return anim;

            return caster.GetComponentInParent<ICharacterAnimationController>();
        }

        private static ICharacterActionController ResolveActionController(GameObject caster)
        {
            if (caster == null) return null;

            if (caster.TryGetComponent<ICharacterActionController>(out var action))
                return action;

            action = caster.GetComponentInChildren<ICharacterActionController>(includeInactive: true);
            if (action != null) return action;

            return caster.GetComponentInParent<ICharacterActionController>();
        }
        public bool TryCancel(SkillCancelReason reason)
        {
            if (_current == null) return false;

            _current.Cancel(reason);
            _current = null; // 즉시 종료(추가 Tick 방지)
            return true;
        }
    }
}
