using System.Collections.Generic;
using Config;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 엑티브 스킬 테이블 Structure
    /// </summary>
    public class StruckTableSkillMonster
    {
        public int Uid { get; set; }
        public string Name { get; set; }
        public string Memo;
        public string SoFileName;

        /// <summary>스킬 분류(Active/Passive)</summary>
        public ConfigCommonSkill.SkillKind SkillKind;

        /// <summary>캐스팅 시간(초). 0이면 즉시 사용.</summary>
        public float CastTime;
        
        public float CoolTime;

        /// <summary>타겟팅 모드(스킬 패키지의 SkillTargetingMode enum 값을 int로 저장).</summary>
        public ConfigCommonSkill.SkillTargetingMode TargetingMode;

        /// <summary>스킬 사용 가능 거리입니다. 값이 없으면 Range를 fallback으로 사용합니다.</summary>
        public float CastRange;

        /// <summary>이벤트 기본 생성/배치 거리입니다. 값이 없으면 Range를 fallback으로 사용합니다.</summary>
        public float PlacementRange;

        /// <summary>구버전 호환용 거리 값입니다. CastRange/PlacementRange 미지정 시 fallback으로 사용합니다.</summary>
        public float Range;

        /// <summary>최대 타겟 수</summary>
        public int MaxTargets;

        /// <summary>애니메이션 클립 이름 규칙: 캐스팅 시작</summary>
        public string CastStartClip;

        /// <summary>애니메이션 클립 이름 규칙: 캐스팅 루프</summary>
        public string CastLoopClip;

        /// <summary>애니메이션 클립 이름 규칙: 캐스팅 종료</summary>
        public string CastEndClip;

        /// <summary>애니메이션 클립 이름 규칙: 사용</summary>
        public string UseClip;
    }

    /// <summary>
    /// 플레이어 엑티브 스킬 테이블
    /// </summary>
    public class TableSkillMonster : DefaultTable<StruckTableSkillMonster>
    {
        public override string Key => ConfigAddressableTableSkill.SkillMonster;
        
        protected override StruckTableSkillMonster BuildRow(Dictionary<string, string> data)
        {
            int uid = MathHelper.ParseInt(data.GetValueOrDefault("Uid"));
            
            return new StruckTableSkillMonster
            {
                Uid = uid,
                Name = data.GetValueOrDefault("Name", data.GetValueOrDefault("Memo")),
                Memo = data["Memo"],
                SoFileName = data["SoFileName"],
                SkillKind = ConfigCommonSkill.SkillKind.Active,
                CastTime = MathHelper.ParseFloat(data["CastTime"]),
                CoolTime = MathHelper.ParseFloat(data["CoolTime"]),
                TargetingMode = EnumHelper.ConvertEnum<ConfigCommonSkill.SkillTargetingMode>(data["TargetingMode"]),
                CastRange = ResolveRangeColumn(data, "CastRange"),
                PlacementRange = ResolveRangeColumn(data, "PlacementRange"),
                Range = MathHelper.ParseFloat(data.GetValueOrDefault("Range")),
                MaxTargets = MathHelper.ParseInt(data["MaxTargets"]),
                CastStartClip = data["CastStartClip"],
                CastLoopClip = data["CastLoopClip"],
                CastEndClip = data["CastEndClip"],
                UseClip = data["UseClip"],
            };
        }
        /// <summary>
        /// 거리 컬럼을 안전하게 파싱합니다. 컬럼이 비어 있으면 0을 반환하고, 런타임에서 Range fallback을 사용합니다.
        /// </summary>
        /// <param name="data">원본 행 데이터입니다.</param>
        /// <param name="columnName">읽을 컬럼 이름입니다.</param>
        /// <returns>파싱된 거리 값입니다.</returns>
        private static float ResolveRangeColumn(Dictionary<string, string> data, string columnName)
        {
            return MathHelper.ParseFloat(data.GetValueOrDefault(columnName));
        }

    }
}