using Config;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임에서 player/monster 스킬 테이블을 공통으로 다루기 위한 정의 모델입니다.
    /// </summary>
    public sealed class RuntimeSkillDefinition
    {
        public int Uid;
        public string Name;
        public string Memo;
        public string SoFileName;
        public ConfigCommonSkill.SkillOwnerType OwnerType;
        
        public ConfigCommonSkill.SkillKind SkillKind;
        public float CastTime;
        public float CoolTime;
        public ConfigCommonSkill.SkillTargetingMode TargetingMode;

        /// <summary>스킬 사용 가능 거리입니다.</summary>
        public float CastRange;

        /// <summary>Projectile/Effect/Damage 중심점 계산에 사용하는 기본 배치 거리입니다.</summary>
        public float PlacementRange;

        public int MaxTargets;
        public string CastStartClip;
        public string CastLoopClip;
        public string CastEndClip;
        public string UseClip;
        public ConfigCommonSkill.SkillFacingMode FacingMode;

        public static RuntimeSkillDefinition From(StruckTableSkill row)
        {
            if (row == null) return null;
            return new RuntimeSkillDefinition
            {
                Uid = row.Uid,
                Name = row.Name,
                Memo = row.Memo,
                OwnerType = ConfigCommonSkill.SkillOwnerType.Player,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                TargetingMode = row.TargetingMode,
                CastRange = row.CastRange,
                PlacementRange = row.PlacementRange,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
                FacingMode = row.FacingMode,
            };
        }

        public static RuntimeSkillDefinition From(StruckTableSkillMonster row)
        {
            if (row == null) return null;
            return new RuntimeSkillDefinition
            {
                Uid = row.Uid,
                Name = row.Name,
                Memo = row.Memo,
                OwnerType = ConfigCommonSkill.SkillOwnerType.Monster,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                TargetingMode = row.TargetingMode,
                CastRange = row.CastRange,
                PlacementRange = row.PlacementRange,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
                FacingMode = row.FacingMode,
            };
        }
    }
}
