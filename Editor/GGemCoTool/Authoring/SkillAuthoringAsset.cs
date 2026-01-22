// Assets/GGemCo/Skills/Runtime/AuthoringData/SkillAuthoringAsset.cs
using GGemCo2DSkill;
using UnityEngine;
using UnityEngine.Playables;

namespace GGemCo2DSkillEditor
{
    [CreateAssetMenu(menuName = "GGemCo/Skills/Authoring/Skill Authoring Asset", fileName = "SkillAuthoring")]
    public sealed class SkillAuthoringAsset : ScriptableObject
    {
        public SkillDefinition targetSkill;
        public PlayableAsset timelineAsset; // 제작용 타임라인
    }
}