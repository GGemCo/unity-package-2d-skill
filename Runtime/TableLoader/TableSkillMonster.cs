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
        public string SoFileName;

        /// <summary>스킬 분류(Active/Passive)</summary>
        public ConfigCommonSkill.SkillKind SkillKind;

        /// <summary>캐스팅 시간(초). 0이면 즉시 사용.</summary>
        public float CastTime;
        
        public float CoolTime;

        /// <summary>타겟팅 모드(스킬 패키지의 SkillTargetingMode enum 값을 int로 저장).</summary>
        public ConfigCommonSkill.SkillTargetingMode TargetingMode;

        /// <summary>스킬 사용 가능 거리입니다.</summary>
        public float CastRange;

        /// <summary>이벤트 기본 생성/배치 거리입니다.</summary>
        public float PlacementRange;

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

        /// <summary>스킬 실행 직전에 자동으로 맞출 방향 정책입니다.</summary>
        public ConfigCommonSkill.SkillFacingMode FacingMode;
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
                Name = data.GetValueOrDefault("Name", ""),
                SoFileName = data["SoFileName"],
                SkillKind = ConfigCommonSkill.SkillKind.Active,
                CastTime = MathHelper.ParseFloat(data["CastTime"]),
                CoolTime = MathHelper.ParseFloat(data["CoolTime"]),
                TargetingMode = EnumHelper.ConvertEnum<ConfigCommonSkill.SkillTargetingMode>(data["TargetingMode"]),
                CastRange = MathHelper.ParseFloat(data["CastRange"]),
                PlacementRange = MathHelper.ParseFloat(data["PlacementRange"]),
                MaxTargets = MathHelper.ParseInt(data["MaxTargets"]),
                CastStartClip = data["CastStartClip"],
                CastLoopClip = data["CastLoopClip"],
                CastEndClip = data["CastEndClip"],
                UseClip = data["UseClip"],
                FacingMode = EnumHelper.ConvertEnum<ConfigCommonSkill.SkillFacingMode>(data["FacingMode"])
            };
        }
    }
}