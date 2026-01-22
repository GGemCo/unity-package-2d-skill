using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Timeline에서 Bake된 스킬 런타임 시퀀스.
    /// </summary>
    [CreateAssetMenu(menuName = "GGemCo/Skill/RuntimeSequence", fileName = "SkillRuntimeSequence")]
    public sealed class SkillRuntimeSequence : ScriptableObject
    {
        public int SkillUid;
        public float Duration;

        public SkillRuntimeEvent[] Events;
        public SkillBakedPayloads Payloads;
    }
}
