// Assets/GGemCo/Skills/Runtime/Targeting/SkillTargetContext.cs
using UnityEngine;

namespace GGemCo2DSkill
{
    public readonly struct SkillTargetContext
    {
        public readonly GameObject caster;
        public readonly GameObject lockedTarget;   // Lock-On일 때 사용
        public readonly Vector3 groundPoint;       // GroundTarget일 때 사용
        public readonly Vector3 forward;           // ForwardDirectional일 때 사용

        public SkillTargetContext(GameObject caster, GameObject lockedTarget, Vector3 groundPoint, Vector3 forward)
        {
            this.caster = caster;
            this.lockedTarget = lockedTarget;
            this.groundPoint = groundPoint;
            this.forward = forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
        }
    }
}