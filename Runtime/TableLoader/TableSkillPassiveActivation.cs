using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 장착된 패시브 스킬이 수동 입력으로 실행할 스킬과 발동 정책을 정의합니다.
    /// </summary>
    public sealed class StruckTableSkillPassiveActivation : IUidName
    {
        public int Uid { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// 발동 권한을 제공하는 패시브 스킬 UID입니다.
        /// </summary>
        public int PassiveSkillUid;

        /// <summary>
        /// 이 정의가 적용되는 패시브 레벨입니다. 0이면 모든 레벨에 적용됩니다.
        /// </summary>
        public int Level;

        /// <summary>
        /// 실제 MP·쿨다운·Timeline 실행을 담당하는 플레이어 스킬 UID입니다.
        /// </summary>
        public int ExecutionSkillUid;

        /// <summary>
        /// 조작 불가 상태에서도 발동 요청을 허용할지 여부입니다.
        /// </summary>
        public bool AllowWhileControlLocked;

        /// <summary>
        /// 실행 중인 스킬을 중단하고 발동할지 여부입니다.
        /// </summary>
        public bool InterruptRunningSkill;

        /// <summary>
        /// 스킬 시작 직전에 현재 및 예약된 Crowd Control을 해제할지 여부입니다.
        /// </summary>
        public bool StopCrowdControlOnStart;

        /// <summary>
        /// 발동에 필요한 필수 식별자가 유효한지 여부입니다.
        /// </summary>
        public bool IsValid => Uid > 0 && PassiveSkillUid > 0 && ExecutionSkillUid > 0;

        /// <summary>
        /// 테이블 값을 공용 스킬 발동 정책으로 변환합니다.
        /// </summary>
        /// <returns>이번 실행 요청에 적용할 발동 정책입니다.</returns>
        public SkillActivationOptions BuildActivationOptions()
        {
            return new SkillActivationOptions(
                AllowWhileControlLocked,
                InterruptRunningSkill,
                StopCrowdControlOnStart);
        }
    }

    /// <summary>
    /// 수동 발동형 패시브 스킬 정의를 로드하고 패시브 UID와 레벨로 조회합니다.
    /// </summary>
    public sealed class TableSkillPassiveActivation : DefaultTable<StruckTableSkillPassiveActivation>
    {
        private readonly Dictionary<(int passiveSkillUid, int level), StruckTableSkillPassiveActivation>
            _byPassiveAndLevel = new();

        public override string Key => ConfigAddressableTableSkill.SkillPassiveActivation;

        /// <summary>
        /// 테이블을 다시 로드하기 전에 런타임 조회 캐시를 정리합니다.
        /// </summary>
        protected override void PreLoad()
        {
            base.PreLoad();
            _byPassiveAndLevel.Clear();
        }

        /// <summary>
        /// 원본 문자열 행을 수동 패시브 발동 정의로 변환합니다.
        /// </summary>
        /// <param name="data">컬럼 이름과 원본 문자열 값입니다.</param>
        /// <returns>변환된 테이블 행입니다.</returns>
        protected override StruckTableSkillPassiveActivation BuildRow(Dictionary<string, string> data)
        {
            TableRowReader reader = ReadRow(data);
            int uid = reader.Int("Uid", 0);
            return new StruckTableSkillPassiveActivation
            {
                Uid = uid,
                Name = reader.String("Name", $"SkillPassiveActivation_{uid}"),
                PassiveSkillUid = reader.Int("PassiveSkillUid", 0),
                Level = reader.Int("Level", 0),
                ExecutionSkillUid = reader.Int("ExecutionSkillUid", 0),
                AllowWhileControlLocked = reader.BoolYN("AllowWhileControlLocked"),
                InterruptRunningSkill = reader.BoolYN("InterruptRunningSkill"),
                StopCrowdControlOnStart = reader.BoolYN("StopCrowdControlOnStart"),
            };
        }

        /// <summary>
        /// 유효한 행을 패시브 UID와 레벨 기준 캐시에 등록합니다.
        /// </summary>
        /// <param name="row">로드가 완료된 테이블 행입니다.</param>
        protected override void OnLoadedData(StruckTableSkillPassiveActivation row)
        {
            base.OnLoadedData(row);
            if (row == null || !row.IsValid)
            {
                return;
            }

            _byPassiveAndLevel[(row.PassiveSkillUid, row.Level)] = row;
        }

        /// <summary>
        /// 패시브 UID와 장착 레벨에 맞는 수동 발동 정의를 조회합니다.
        /// </summary>
        /// <param name="passiveSkillUid">장착된 패시브 스킬 UID입니다.</param>
        /// <param name="level">현재 장착 레벨입니다.</param>
        /// <param name="definition">조회된 발동 정의입니다.</param>
        /// <returns>정확한 레벨 또는 전체 레벨 정의를 찾으면 <see langword="true"/>입니다.</returns>
        public bool TryGetActivation(
            int passiveSkillUid,
            int level,
            out StruckTableSkillPassiveActivation definition)
        {
            definition = null;
            if (passiveSkillUid <= 0)
            {
                return false;
            }

            int normalizedLevel = level > 0 ? level : 1;
            if (_byPassiveAndLevel.TryGetValue(
                    (passiveSkillUid, normalizedLevel),
                    out definition))
            {
                return true;
            }

            return _byPassiveAndLevel.TryGetValue((passiveSkillUid, 0), out definition);
        }
    }
}
