using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Core의 IMonsterSkillDriver 호출을 Skill 런타임(SkillExecutor)으로 연결하는 어댑터.
    /// - SSOT: Core skill 테이블의 Uid(int)
    /// - 쿨다운: skillUid 기준으로 내부 관리
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSkillDriverAdapter : MonoBehaviour, IMonsterSkillDriver
    {
        [Header("References")]
        [SerializeField] private SkillExecutor executor;

        private readonly Dictionary<int, float> _cooldownReadyAt = new();

        public bool IsSkillBusy => executor != null && executor.IsBusy;

        private void Awake()
        {
            if (executor == null) executor = GetComponent<SkillExecutor>();
        }

        public SkillUseResult TryUseSkill(int skillUid, in MonsterSkillTarget target)
        {
            if (executor == null) return SkillUseResult.Rejected;
            if (skillUid <= 0) return SkillUseResult.Rejected;

            // 진행 중이면 거부(동시 1개 정책)
            if (executor.IsBusy) return SkillUseResult.Rejected;

            // 쿨다운 검사(테이블의 CoolTime을 사용)
            if (_cooldownReadyAt.TryGetValue(skillUid, out float readyAt) && Time.time < readyAt)
                return SkillUseResult.Rejected;

            // 테이블 조회
            var table = TableLoaderManager.Instance != null ? TableLoaderManagerSkill.Instance.TableSkill : null;
            if (table == null) return SkillUseResult.Rejected;
            if (!table.GetDatas().TryGetValue(skillUid, out var skill) || skill == null)
                return SkillUseResult.Rejected;

            // 타겟팅 최소 검증(예: LockOnGuaranteedHit 모드면 lockedTarget 필요)
            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            if (mode == ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit && target.LockedTarget == null)
                return SkillUseResult.Rejected;

            var ctx = new SkillTargetContext(
                caster: gameObject,
                lockedTarget: target.LockedTarget != null ? target.LockedTarget.gameObject : null,
                groundPoint: target.GroundPoint,
                forward: new Vector3(target.Forward.x, target.Forward.y, 0f)
            );

            bool started = executor.TryUse(skillUid, ctx);
            if (!started) return SkillUseResult.Rejected;

            float cd = Mathf.Max(0f, skill.CoolTime);
            if (cd > 0f) _cooldownReadyAt[skillUid] = Time.time + cd;

            return SkillUseResult.Started;
        }
    }
}
