using Config;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// CreateSkillWindow에서 player/monster 스킬 테이블을 공통으로 편집하기 위한 모델입니다.
    /// </summary>
    public sealed class SkillAuthoringModel
    {
        public SkillAuthoringTableKind TableKind;
        public int Uid;
        public string Name;
        public string Memo;
        public bool DefaultLearn;
        public int NeedPlayerLevel;
        public string IconFileName;
        public string SoFileName;
        public ConfigCommonSkill.SkillKind SkillKind;
        public float CastTime;
        public float CoolTime;
        public ConfigCommonSkill.SkillTargetingMode TargetingMode;
        public float Range;
        public int MaxTargets;
        public string CastStartClip;
        public string CastLoopClip;
        public string CastEndClip;
        public string UseClip;

        public bool SupportsPlayerFields => TableKind == SkillAuthoringTableKind.Player;

        public static SkillAuthoringModel FromPlayer(StruckTableSkill row)
        {
            if (row == null) return null;
            return new SkillAuthoringModel
            {
                TableKind = SkillAuthoringTableKind.Player,
                Uid = row.Uid,
                Name = row.Name,
                Memo = row.Memo,
                DefaultLearn = row.DefaultLearn,
                NeedPlayerLevel = row.NeedPlayerLevel,
                IconFileName = row.IconFileName,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                TargetingMode = row.TargetingMode,
                Range = row.Range,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
            };
        }

        public static SkillAuthoringModel FromMonster(StruckTableSkillMonster row)
        {
            if (row == null) return null;
            return new SkillAuthoringModel
            {
                TableKind = SkillAuthoringTableKind.Monster,
                Uid = row.Uid,
                Name = row.Name,
                Memo = row.Memo,
                SoFileName = row.SoFileName,
                SkillKind = row.SkillKind,
                CastTime = row.CastTime,
                CoolTime = row.CoolTime,
                TargetingMode = row.TargetingMode,
                Range = row.Range,
                MaxTargets = row.MaxTargets,
                CastStartClip = row.CastStartClip,
                CastLoopClip = row.CastLoopClip,
                CastEndClip = row.CastEndClip,
                UseClip = row.UseClip,
            };
        }

        public SkillAuthoringModel Clone()
        {
            return (SkillAuthoringModel)MemberwiseClone();
        }
    }
}
