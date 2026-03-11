using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Core의 <c>IMonsterSkillDriver</c> 호출을 <see cref="SkillExecutor"/> 기반 스킬 실행 흐름으로 연결하는 어댑터입니다.
    /// 몬스터 스킬 UID를 기준으로 실행 가능 여부와 내부 쿨다운을 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSkillDriverAdapter : MonoBehaviour, ISkillCancelableDriver
    {
        /// <summary>
        /// 실제 스킬 실행과 취소를 담당하는 런타임 실행기입니다.
        /// </summary>
        private SkillExecutor _executor;

        /// <summary>
        /// 외부에서 사용할 <see cref="SkillExecutor"/> 인스턴스를 설정합니다.
        /// </summary>
        /// <param name="value">이 어댑터가 사용할 스킬 실행기입니다.</param>
        public void SetSkillExecutor(SkillExecutor value) => _executor = value;

        /// <summary>
        /// 스킬 UID별 다음 사용 가능 시각을 저장합니다.
        /// </summary>
        private readonly Dictionary<int, float> _cooldownReadyAt = new();

        /// <summary>
        /// 현재 스킬 실행기가 다른 스킬을 처리 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsSkillBusy => _executor != null && _executor.IsBusy;

        /// <summary>
        /// 컴포넌트 초기화 시 동일한 게임 오브젝트에서 <see cref="SkillExecutor"/>를 찾아 연결합니다.
        /// </summary>
        private void Awake()
        {
            if (_executor == null) _executor = GetComponent<SkillExecutor>();
        }

        /// <summary>
        /// 드라이버 요청 정보를 바탕으로 몬스터 스킬 사용을 시도합니다.
        /// </summary>
        /// <param name="skillUid">사용할 몬스터 스킬의 고유 식별자입니다.</param>
        /// <param name="request">타겟, 지면 위치, 방향, 테이블 출처를 포함한 스킬 요청 정보입니다.</param>
        /// <returns>스킬 실행이 시작되면 <see cref="SkillUseResult.Started"/>, 실패하면 <see cref="SkillUseResult.Rejected"/>를 반환합니다.</returns>
        public SkillUseResult TryUseSkill(int skillUid, in SkillDriverRequest request)
        {
            if (request.Source != ConfigCommon.SkillTableSource.Monster)
                return SkillUseResult.Rejected;

            var target = new MonsterSkillTarget(request.LockedTarget, request.GroundPoint, request.Forward);
            return TryUseSkill(skillUid, target);
        }

        /// <summary>
        /// 대상 지정 정보와 함께 몬스터 스킬 사용을 시도합니다.
        /// 실행기 상태, 입력값, 쿨다운, 스킬 정의, 타겟 조건을 검증한 뒤 실행을 요청합니다.
        /// </summary>
        /// <param name="skillUid">사용할 몬스터 스킬의 고유 식별자입니다.</param>
        /// <param name="target">고정 대상, 지면 위치, 방향을 포함한 타겟 정보입니다.</param>
        /// <returns>스킬 실행이 시작되면 <see cref="SkillUseResult.Started"/>, 실행할 수 없으면 <see cref="SkillUseResult.Rejected"/>를 반환합니다.</returns>
        public SkillUseResult TryUseSkill(int skillUid, in MonsterSkillTarget target)
        {
            if (_executor == null) return SkillUseResult.Rejected;
            if (skillUid <= 0) return SkillUseResult.Rejected;

            // 동시 실행은 허용하지 않으므로 이미 실행 중이면 거부합니다.
            if (_executor.IsBusy) return SkillUseResult.Rejected;

            // 스킬 UID 기준 내부 쿨다운이 남아 있으면 사용을 거부합니다.
            if (_cooldownReadyAt.TryGetValue(skillUid, out float readyAt) && Time.time < readyAt)
                return SkillUseResult.Rejected;

            // 몬스터 스킬 정의를 조회할 수 없으면 실행하지 않습니다.
            if (!SkillDefinitionResolver.TryResolve(skillUid, ConfigCommon.SkillTableSource.Monster, out var skill) || skill == null)
                return SkillUseResult.Rejected;

            // LockOnGuaranteedHit 모드는 잠금 대상이 반드시 필요합니다.
            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            if (mode == ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit && target.LockedTarget == null)
                return SkillUseResult.Rejected;

            var ctx = new SkillTargetContext(
                caster: gameObject,
                lockedTarget: target.LockedTarget != null ? target.LockedTarget.gameObject : null,
                groundPoint: target.GroundPoint,
                forward: new Vector3(target.Forward.x, target.Forward.y, 0f)
            );

            if (!SkillRangeResolver.IsWithinCastRange(skill, gameObject, ctx))
                return SkillUseResult.Rejected;

            bool started = _executor.TryUse(skillUid, ctx, ConfigCommon.SkillTableSource.Monster);
            if (!started) return SkillUseResult.Rejected;

            float cd = Mathf.Max(0f, skill.CoolTime);
            if (cd > 0f) _cooldownReadyAt[skillUid] = Time.time + cd;

            return SkillUseResult.Started;
        }

        /// <summary>
        /// 현재 실행 중인 스킬에 취소를 요청합니다.
        /// </summary>
        /// <param name="reason">스킬 취소 사유입니다.</param>
        /// <returns>취소 요청이 정상적으로 전달되면 <see langword="true"/>, 실행기가 없거나 취소할 수 없으면 <see langword="false"/>를 반환합니다.</returns>
        public bool RequestCancelSkill(SkillCancelReason reason)
        {
            if (_executor == null) return false;
            return _executor.TryCancel(reason);
        }
    }
}