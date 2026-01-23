using Config;

namespace GGemCo2DSkillEditor
{
    [System.Serializable]
    public sealed class SkillApplyStatusClipV2 : SkillEventClipV2
    {
        public SkillApplyStatusClipV2()
        {
            SetType(ConfigCommonSkill.SkillEventType.ApplyAffect);
        }
    }
}
