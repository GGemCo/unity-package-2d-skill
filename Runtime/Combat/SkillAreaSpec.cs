using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트(주로 Damage)의 히트 영역 정의.
    /// AreaDefinition(ScriptableObject)을 사용하지 않고, 이벤트 payload에 직접 포함하여 사용한다.
    /// </summary>
    [Serializable]
    public struct SkillAreaSpec
    {
        public ConfigCommonSkill.SkillAreaShape shape;

        [Min(0f)] public float radius;          // Circle/Capsule
        [Min(0f)] public float length;          // Box/Cone/Capsule/Line
        [Min(0f)] public float width;           // Box/Line
        [Range(0f, 180f)] public float angle;   // Cone
        public Vector3 localOffset;             // 중심 오프셋(캐스터/타겟 기준)

        public static SkillAreaSpec Default => new SkillAreaSpec
        {
            shape = ConfigCommonSkill.SkillAreaShape.Circle,
            radius = 2f,
            length = 3f,
            width = 2f,
            angle = 60f,
            localOffset = Vector3.zero
        };

        public void EnsureSaneDefaults()
        {
            if (radius <= 0f) radius = 2f;
            if (length <= 0f) length = 3f;
            if (width <= 0f) width = 2f;
            if (angle <= 0f) angle = 60f;
        }
    }
}
