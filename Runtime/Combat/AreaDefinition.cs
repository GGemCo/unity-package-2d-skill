// Assets/GGemCo/Skills/Runtime/Combat/AreaDefinition.cs

using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    [CreateAssetMenu(menuName = "GGemCo/Skills/Area/Area Definition", fileName = "AreaDefinition")]
    public sealed class AreaDefinition : ScriptableObject
    {
        public string areaId = "Area_Default";
        public ConfigCommonSkill.SkillAreaShape shape = ConfigCommonSkill.SkillAreaShape.Circle;

        [Min(0f)] public float radius = 2f;          // Circle/Capsule
        [Min(0f)] public float length = 3f;          // Capsule/Line/Cone
        [Min(0f)] public float width = 2f;           // Box/Line
        [Range(0f, 180f)] public float angle = 60f;  // Cone(부채꼴)
        public Vector3 localOffset;                  // 중심 오프셋(캐스터/타겟 기준)
    }
}