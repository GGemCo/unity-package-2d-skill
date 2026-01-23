using Config;

namespace GGemCo2DSkillEditor
{
    [System.Serializable]
    public sealed class SkillEffectClipV2 : SkillEventClipV2
    {
        public SkillEffectClipV2()
        {
            SetType(ConfigCommonSkill.SkillEventType.SpawnEffect);
        }
    }
}
