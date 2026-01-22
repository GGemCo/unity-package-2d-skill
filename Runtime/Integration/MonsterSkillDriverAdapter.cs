using System;
using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Core의 IMonsterSkillDriver 호출을 Skill 런타임(SkillExecutor)으로 연결하는 어댑터.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSkillDriverAdapter : MonoBehaviour, IMonsterSkillDriver
    {
        [Header("References")]
        [SerializeField] private SkillExecutor executor;
        [SerializeField] private SkillDefinitionRegistry registry;

        private readonly Dictionary<string, float> _cooldownReadyAt = new(StringComparer.Ordinal);

        public bool IsSkillBusy => executor != null && executor.IsBusy;

        private void Awake()
        {
            if (executor == null) executor = GetComponent<SkillExecutor>();
            if (registry == null) registry = GetComponent<SkillDefinitionRegistry>();
        }

        public SkillUseResult TryUseSkill(string skillId, in MonsterSkillTarget target)
        {
            if (executor == null || registry == null) return SkillUseResult.Rejected;
            if (string.IsNullOrEmpty(skillId)) return SkillUseResult.Rejected;

            if (!registry.TryGet(skillId, out var def) || def == null)
                return SkillUseResult.Rejected;

            // 진행 중이면 거부(동시 1개 정책: SkillExecutor.TryUse도 동일)
            if (executor.IsBusy) return SkillUseResult.Rejected;

            // 쿨다운 검사
            if (_cooldownReadyAt.TryGetValue(skillId, out float readyAt) && Time.time < readyAt)
                return SkillUseResult.Rejected;

            // 타겟팅 최소 검증(스킬 설정에 따라 필요한 데이터가 없으면 거부)
            if (def.targetingMode == ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit && target.LockedTarget == null)
                return SkillUseResult.Rejected;

            // SkillTargetContext 구성
            var ctx = new SkillTargetContext(
                caster: gameObject,
                lockedTarget: target.LockedTarget != null ? target.LockedTarget.gameObject : null,
                groundPoint: target.GroundPoint,
                forward: new Vector3(target.Forward.x, target.Forward.y, 0f)
            );

            bool started = executor.TryUse(def, ctx);
            if (!started) return SkillUseResult.Rejected;

            // 시작 성공 시 쿨다운 소비
            float cd = Mathf.Max(0f, def.cooldownSeconds);
            if (cd > 0f) _cooldownReadyAt[skillId] = Time.time + cd;

            return SkillUseResult.Started;
        }
    }
}
