using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    internal static class SkillTestSelection
    {
        public static GameObject SelectedMonster;
        public static SkillDefinition SelectedDevSkill;
        public static string SelectedSkillId;

        public static bool PreviewEnabled;
        public static Vector3 GroundPoint;
        public static Transform LockedTarget;
        public static Vector2 Forward = Vector2.right;

        public static AreaRegistry AreaRegistry; // 프리뷰용 레지스트리(에셋 지정)
    }
}