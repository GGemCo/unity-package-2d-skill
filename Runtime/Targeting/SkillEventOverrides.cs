// Assets/GGemCo/Skills/Runtime/Targeting/SkillEventOverrides.cs
using System;
using UnityEngine;

namespace GGemCo2DSkill
{
    [Serializable]
    public struct TargetingOverride
    {
        public bool enabled;
        public SkillTargetingMode mode;

        [Tooltip("스킬 기본 range를 덮어쓰기. 0 이하이면 무시.")]
        public float rangeOverride;

        [Tooltip("스킬 기본 maxTargets를 덮어쓰기. 0 이하이면 무시.")]
        public int maxTargetsOverride;

        [Tooltip("타겟/지점 중심을 '시작 시점 스냅샷'으로 고정할지 여부")]
        public bool useSnapshotCenter;

        [Tooltip("FollowTargetArea 모드일 때, 이벤트 시점마다 타겟 Transform을 따라갈지 여부(=true면 실시간 추적)")]
        public bool followTarget;
    }

    [Serializable]
    public struct AreaOverride
    {
        public bool enabled;
        public string areaId;

        [Tooltip("AreaDefinition의 크기 계수를 이벤트별로 가산/보정하고 싶을 때 사용(1=기본)")]
        public float scale;
    }
}