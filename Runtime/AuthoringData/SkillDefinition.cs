// Assets/GGemCo/Skills/Runtime/AuthoringData/SkillDefinition.cs
using System;
using System.Collections.Generic;
using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    [CreateAssetMenu(menuName = "GGemCo/Skills/Skill Definition", fileName = "SkillDefinition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string skillId = "SK_0001";
        public string displayNameKey;
        public Sprite icon;

        [Header("Rules")]
        public float cooldownSeconds = 3f;
        public SkillCost cost;
        public float castTimeSeconds = 0f; // 0이면 즉시 사용(캐스팅 생략 가능)

        [Header("Targeting")]
        public ConfigCommonSkill.SkillTargetingMode targetingMode = ConfigCommonSkill.SkillTargetingMode.ForwardDirectional;
        public float range = 3f;
        public int maxTargets = 1;
        // SkillDefinition.cs 의 Targeting 섹션에 추가(기존 유지)
        public string defaultAreaId = "Area_Default";

        [Header("Animation Clip Name Rules (No Animator Parameters)")]
        public string castStartClipName;
        public string castLoopClipName;
        public string castEndClipName;
        public string useClipName;

        [Header("Runtime Baked Data")]
        public SkillEventSequence eventSequence;
    }

    [Serializable]
    public sealed class SkillEventSequence
    {
        public float totalDuration = 0f;
        public List<SkillEventKeyframe> keyframes = new();
    }

    [Serializable]
    public sealed class SkillEventKeyframe
    {
        [Min(0f)] public float time;
        public ConfigCommonSkill.SkillEventType type;

        // “참조 기반 Payload” 방식(권장): 런타임에서 이 Object를 특정 정의 타입으로 캐스팅해 사용
        public UnityEngine.Object payload;

        // 옵션: 같은 프레임에 여러 이벤트가 있을 때의 정렬 안정성
        public int order;
    }
}