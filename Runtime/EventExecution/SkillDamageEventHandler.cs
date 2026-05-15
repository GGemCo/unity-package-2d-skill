using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 데미지 스킬 이벤트의 판정 위치 계산, 타겟 평가, 데미지 메타데이터 구성, OnHit 후속 효과 적용을 담당합니다.
    /// </summary>
    internal static class SkillDamageEventHandler
    {
        /// <summary>
        /// 데미지 이벤트 정의를 바탕으로 타격 대상을 평가하고 실제 데미지 및 OnHit 효과를 적용합니다.
        /// </summary>
        /// <param name="run">현재 실행 중인 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">데미지 이벤트 페이로드 오브젝트입니다.</param>
        /// <param name="snapshotCasterPos">이벤트 스냅샷 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">이벤트 스냅샷 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">이벤트 스냅샷 시점의 지면 기준점입니다.</param>
        /// <param name="gizmoDurationSeconds">에디터 디버그용 데미지 영역 표시 시간입니다.</param>
        /// <param name="hitEvaluator">범위 기반 타겟 판정을 수행하는 평가기입니다.</param>
        /// <param name="ownerObject">데미지 메타데이터의 대체 공격자와 위치 기준으로 사용할 실행기 오브젝트입니다.</param>
        /// <param name="attackSequence">공격 식별자를 발급하고 연계 해제 정책을 저장할 시퀀스입니다.</param>
        public static void Handle(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            UnityEngine.Object payloadObj,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            float gizmoDurationSeconds,
            IHitEvaluator hitEvaluator,
            GameObject ownerObject,
            SkillAttackSequence attackSequence)
        {
            if (payloadObj is not DamageEventDefinition def)
                return;
            if (hitEvaluator == null || attackSequence == null)
                return;

            ConfigCommonSkill.SkillTargetingMode mode =
                (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            float range = SkillRangeResolver.GetPlacementRange(skill);
            int maxTargets = skill.MaxTargets > 0 ? skill.MaxTargets : 1;

            if (def.targetingOverride.enabled)
            {
                mode = def.targetingOverride.mode;
                if (def.targetingOverride.rangeOverride > 0f)
                    range = def.targetingOverride.rangeOverride;
                if (def.targetingOverride.maxTargetsOverride > 0)
                    maxTargets = def.targetingOverride.maxTargetsOverride;
            }

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

            Vector3 resolvedForward = SkillDirectionResolver.ResolveForward2D(ctx.caster, ctx.forward);
            SkillAreaSpec areaSpec = def.area;
            areaSpec.EnsureSaneDefaults();

            Vector3 center = ResolveDamageCenter(mode, casterPos, targetPos, groundPoint, resolvedForward, range);
            if (!TryApplyDamagePositionReference(run, def, ref center, ref resolvedForward, skill))
                return;

#if UNITY_EDITOR
            RegisterDamageDebugGizmo(ctx.caster, center, resolvedForward, areaSpec, gizmoDurationSeconds);
#endif

            var hits = new List<GameObject>(Mathf.Max(1, maxTargets));
            hitEvaluator.EvaluateTargets(center, resolvedForward, areaSpec, range, maxTargets, ctx.caster, hits);

            CharacterBase castCharacterBase = ctx.caster.GetComponent<CharacterBase>();
            long totalDamage = 10;
            int attackId = attackSequence.Allocate(def.allowSkillChainOnConfirmedDamage);
            var resolvedOnHitCrowdControls = new List<int>(8);

            for (int i = 0; i < hits.Count; i++)
            {
                GameObject go = hits[i];
                if (go == null)
                    continue;

                CharacterHitArea characterHitArea = go.GetComponentInChildren<CharacterHitArea>();
                if (characterHitArea == null)
                    continue;

                CharacterBase target = characterHitArea.target;
                if (target == null)
                    continue;

                if (!IsDamageTargetStateAllowed(def, target))
                    continue;

                ApplyOnHitAffects(def.onHitAffects, ctx.caster, target, damageApplied: false, timing: OnHitAffectTiming.BeforeDamage);
                int crowdControlUid = ResolveOnHitCrowdControlUid(
                    def.onHitCrowdControls,
                    damageApplied: false,
                    timing: OnHitCrowdControlTiming.BeforeDamage,
                    resolvedOnHitCrowdControls);

                bool hasPendingAfterDamageCrowdControl = HasPendingAfterDamageCrowdControl(
                    def.onHitCrowdControls,
                    damageApplied: true,
                    timing: OnHitCrowdControlTiming.AfterDamage);

                var metadataDamage = new MetadataDamage
                {
                    damage = totalDamage,
                    attacker = ctx.caster != null ? ctx.caster : ownerObject,
                    damageType = ConfigCommon.DamageType.Physic,
                    affectUid = 0,
                    crowdControlUid = crowdControlUid,
                    AttackId = attackId,
                    SkillUid = skill.Uid,
                    HasPendingAfterDamageCrowdControl = hasPendingAfterDamageCrowdControl,
                    DamageCameraShakePreset = def.useCameraShakeOnHit ? def.cameraShakePreset : null,
                    DamageCameraShakeDirectionMode = def.cameraShakeDirectionMode,
                };

                bool didApplyDamage = ShouldApplyFacingDamage(castCharacterBase, target, ownerObject);

                CollectOnHitCrowdControlUids(
                    def.onHitCrowdControls,
                    didApplyDamage,
                    OnHitCrowdControlTiming.AfterDamage,
                    resolvedOnHitCrowdControls);

                metadataDamage.ElementGaugeApplications = SkillOnHitEffectUtility.BuildElementGaugeApplications(
                    def.onHitElementGauges,
                    ownerObject,
                    didApplyDamage);

                if (didApplyDamage)
                {
                    metadataDamage.ResolvedOnHitCrowdControls = resolvedOnHitCrowdControls;
                    target.TakeDamage(metadataDamage);
                    ApplyConfiguredHitStop(def, skill, castCharacterBase, target);
                }

                if (target.IsStatusDead())
                    continue;

                ApplyOnHitAffects(def.onHitAffects, ctx.caster, target, didApplyDamage, OnHitAffectTiming.AfterDamage);
            }
        }

        /// <summary>
        /// 타겟팅 모드에 따라 데미지 판정 중심 좌표를 계산합니다.
        /// </summary>
        /// <param name="mode">최종 적용할 스킬 타겟팅 모드입니다.</param>
        /// <param name="casterPos">이벤트 시점에 해석한 캐스터 위치입니다.</param>
        /// <param name="targetPos">이벤트 시점에 해석한 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석한 지면 기준점입니다.</param>
        /// <param name="resolvedForward">2D 기준으로 보정된 전방 방향입니다.</param>
        /// <param name="range">전방 배치에 사용할 거리입니다.</param>
        /// <returns>데미지 판정 중심 좌표입니다.</returns>
        private static Vector3 ResolveDamageCenter(
            ConfigCommonSkill.SkillTargetingMode mode,
            Vector3 casterPos,
            Vector3 targetPos,
            Vector3 groundPoint,
            Vector3 resolvedForward,
            float range)
        {
            switch (mode)
            {
                case ConfigCommonSkill.SkillTargetingMode.GroundTarget:
                    return groundPoint;
                case ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit:
                case ConfigCommonSkill.SkillTargetingMode.FollowTargetArea:
                    return targetPos;
                default:
                    Vector3 fwd = resolvedForward.sqrMagnitude < 1e-6f ? Vector3.right : resolvedForward.normalized;
                    return SkillRangeResolver.ResolveForwardPlacementPosition(casterPos, fwd, range);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 테스트 허브에 데미지 영역 기즈모 정보를 등록합니다.
        /// </summary>
        /// <param name="caster">기즈모 소유자로 사용할 캐스터 오브젝트입니다.</param>
        /// <param name="center">데미지 영역 중심입니다.</param>
        /// <param name="forward">데미지 영역 방향입니다.</param>
        /// <param name="areaSpec">데미지 영역 스펙입니다.</param>
        /// <param name="durationSeconds">기즈모 표시 시간입니다.</param>
        private static void RegisterDamageDebugGizmo(
            GameObject caster,
            Vector3 center,
            Vector3 forward,
            SkillAreaSpec areaSpec,
            float durationSeconds)
        {
            if (caster == null || SkillTestRuntimeHub.Instance == null)
                return;

            SkillTestRuntimeHub.Instance.RegisterDamageArea(
                center,
                forward,
                areaSpec,
                durationSeconds,
                caster);
        }
#endif

        /// <summary>
        /// 캐스터와 타겟의 바라보기 상태를 기준으로 실제 데미지를 적용할지 결정합니다.
        /// </summary>
        /// <param name="caster">공격 캐릭터입니다.</param>
        /// <param name="target">피격 후보 캐릭터입니다.</param>
        /// <param name="ownerObject">캐스터 위치 대체값으로 사용할 실행기 오브젝트입니다.</param>
        /// <returns>실제 데미지를 적용해야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldApplyFacingDamage(CharacterBase caster, CharacterBase target, GameObject ownerObject)
        {
            if (caster == null || target == null)
                return false;

            if (caster.AreFacingEachOther(target))
                return true;

            if (caster.CurrentFacing != target.CurrentFacing)
                return false;

            float ownerX = ownerObject != null ? ownerObject.transform.position.x : caster.transform.position.x;
            switch (caster.CurrentFacing)
            {
                case CharacterConstants.FacingDirection8.Right:
                    return target.transform.position.x >= ownerX;
                case CharacterConstants.FacingDirection8.Left:
                    return target.transform.position.x <= ownerX;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 데미지 이벤트 정의에 설정된 HitStop 정책을 캐스터와 타겟에게 적용합니다.
        /// </summary>
        /// <param name="def">데미지 이벤트 정의입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="caster">공격 캐릭터입니다.</param>
        /// <param name="target">피격 캐릭터입니다.</param>
        private static void ApplyConfiguredHitStop(
            DamageEventDefinition def,
            RuntimeSkillDefinition skill,
            CharacterBase caster,
            CharacterBase target)
        {
            if (def == null || caster == null)
                return;

            CharacterBase.HitStopConfig hitStopConfig = caster.GetResolvedHitStopConfig();
            if (def.useHitStopSelf && hitStopConfig.Enabled)
            {
                float selfSeconds = def.useDefaultSelfHitStop
                    ? hitStopConfig.DefaultSelfSeconds
                    : Mathf.Max(0f, def.selfHitStopSeconds);
                if (selfSeconds > 0f)
                {
                    caster.ApplyHitStop(new HitStopRequest(
                        selfSeconds,
                        pauseAnimation: hitStopConfig.PauseAnimation,
                        freezePhysics: hitStopConfig.FreezePhysics,
                        sourceSkillUid: skill != null ? skill.Uid : 0));
                }
            }

            if (target != null && def.useHitStopTarget && hitStopConfig.Enabled)
            {
                float targetSeconds = def.useDefaultTargetHitStop
                    ? hitStopConfig.DefaultReceiveSeconds
                    : Mathf.Max(0f, def.targetHitStopSeconds);
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

            SkillPositionReference reference = def.damageCenterReference;
            if (reference.mode != SkillPositionReferenceMode.NamedPositionAnchor &&
                reference.mode != SkillPositionReferenceMode.NamedPositionAnchorOrCurrent)
            {
                return true;
            }

            if (run != null && run.TryGetPositionAnchor(reference.key, out SkillPositionAnchorSnapshot snapshot))
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

        /// <summary>
        /// 데미지 이벤트의 지상/공중 필터 설정에 따라 타겟 상태가 허용되는지 확인합니다.
        /// </summary>
        /// <param name="def">데미지 이벤트 정의입니다.</param>
        /// <param name="target">검사할 피격 후보 캐릭터입니다.</param>
        /// <returns>타겟 상태가 데미지 필터를 통과하면 <see langword="true"/>입니다.</returns>
        private static bool IsDamageTargetStateAllowed(DamageEventDefinition def, CharacterBase target)
        {
            if (def == null || target == null)
                return false;

            if (def.isGroundOnly && def.isAirOnly)
            {
                Debug.LogWarning(
                    $"[SkillExecutor] DamageEventDefinition has both isGroundOnly and isAirOnly enabled. The target will be skipped. target={target.name}",
                    target);
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
        /// OnHit 설정 목록을 순회하며 조건에 맞는 Affect를 대상에게 적용합니다.
        /// </summary>
        /// <param name="entries">적용할 OnHit Affect 목록입니다.</param>
        /// <param name="caster">효과의 출처가 되는 캐스터입니다.</param>
        /// <param name="target">효과를 적용할 대상입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        private static void ApplyOnHitAffects(
            OnHitAffectEntry[] entries,
            GameObject caster,
            CharacterBase target,
            bool damageApplied,
            OnHitAffectTiming timing)
        {
            if (entries == null || entries.Length == 0)
                return;
            if (caster == null || target == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                OnHitAffectEntry entry = entries[i];
                if (entry.affectUid <= 0)
                    continue;
                if (entry.timing != timing)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;

                float chance = Mathf.Clamp01(entry.chance);
                if (chance <= 0f)
                    continue;
                if (chance < 0.9999f && Random.value > chance)
                    continue;

                int stacks = Mathf.Max(1, entry.stacks);
                float duration = entry.durationOverrideSeconds > 0f ? entry.durationOverrideSeconds : 0f;

                for (int s = 0; s < stacks; s++)
                {
                    AffectApi.Apply(target.gameObject, entry.affectUid, caster, duration);
                }
            }
        }

        /// <summary>
        /// 데미지 이후 적용할 Crowd Control 후보가 있는지 확인합니다.
        /// </summary>
        /// <param name="entries">검사할 OnHit Crowd Control 목록입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        /// <returns>조건을 만족하는 Crowd Control 항목이 있으면 <see langword="true"/>입니다.</returns>
        private static bool HasPendingAfterDamageCrowdControl(
            OnHitCrowdControlEntry[] entries,
            bool damageApplied,
            OnHitCrowdControlTiming timing)
        {
            if (entries == null || entries.Length == 0)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                OnHitCrowdControlEntry entry = entries[i];
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

        /// <summary>
        /// 현재 시점에 적용 가능한 OnHit Crowd Control UID를 순서대로 수집합니다.
        /// 배열에 등록된 순서가 실행 순서가 됩니다.
        /// </summary>
        /// <param name="entries">수집할 OnHit Crowd Control 목록입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        /// <param name="results">수집 결과를 저장할 목록입니다.</param>
        private static void CollectOnHitCrowdControlUids(
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
                OnHitCrowdControlEntry entry = entries[i];
                if (entry.crowdControlUid <= 0)
                    continue;
                if (entry.timing != timing)
                    continue;
                if (entry.requireDamageDealt && !damageApplied)
                    continue;

                float chance = Mathf.Clamp01(entry.chance);
                if (chance <= 0f)
                    continue;
                if (chance < 0.9999f && Random.value > chance)
                    continue;

                results.Add(entry.crowdControlUid);
            }
        }

        /// <summary>
        /// BeforeDamage처럼 단일 Crowd Control 전달이 필요한 구간에서 첫 번째 UID를 선택합니다.
        /// </summary>
        /// <param name="entries">검사할 OnHit Crowd Control 목록입니다.</param>
        /// <param name="damageApplied">실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="timing">현재 처리 중인 OnHit 적용 시점입니다.</param>
        /// <param name="scratch">임시 수집에 사용할 목록입니다.</param>
        /// <returns>선택한 Crowd Control UID입니다. 없으면 0입니다.</returns>
        private static int ResolveOnHitCrowdControlUid(
            OnHitCrowdControlEntry[] entries,
            bool damageApplied,
            OnHitCrowdControlTiming timing,
            List<int> scratch)
        {
            CollectOnHitCrowdControlUids(entries, damageApplied, timing, scratch);
            return scratch != null && scratch.Count > 0 ? scratch[0] : 0;
        }
    }
}
