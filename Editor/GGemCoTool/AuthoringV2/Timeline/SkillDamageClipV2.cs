using Config;

namespace GGemCo2DSkillEditor
{
    [System.Serializable]
    public sealed class SkillDamageClipV2 : SkillEventClipV2
    {
        public SkillDamageClipV2()
        {
            SetType(ConfigCommonSkill.SkillEventType.Damage);
        }
    }
}
