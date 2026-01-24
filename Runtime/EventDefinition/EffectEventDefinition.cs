using UnityEngine;

namespace GGemCo2DSkill
{
    public sealed class EffectEventDefinition : ScriptableObject
    {
        [Header("Effect")]
        public int effectUid;
        // public GameObject prefab;
        public float lifetimeSeconds = 2f;

        [Header("Spawn Rule")]
        public bool attachToTarget;
        public Vector3 localOffset;

        [Header("Overrides")]
        public TargetingOverride targetingOverride; // 타겟/지점 중심을 이벤트별로 바꿀 수 있음

    }
}