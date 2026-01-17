// Assets/GGemCo/Skills/Runtime/Core/SkillTypes.cs
using System;
using UnityEngine;

namespace GGemCo2DSkill
{
    public enum SkillEventType
    {
        Damage = 0,
        SpawnEffect = 1,
        ApplyStatus = 2,
        StateToggle = 3,
        Audio = 4,
        Camera = 5,
    }

    public enum SkillTargetingMode
    {
        LockOnGuaranteedHit = 0,     // 타겟 고정 + 무조건 피격 정책
        TargetCenteredArea = 1,      // 타겟 중심 범위
        FollowTargetArea = 2,        // 타겟 추적 범위
        GroundTarget = 3,            // 지점 고정
        ForwardDirectional = 4,      // 캐스터 전방 기준
        Projectile = 5,              // 투사체 기반(확장)
    }

    public enum SkillAreaShape
    {
        Circle = 0,
        Box = 1,
        Cone = 2,
        Capsule = 3,
        Line = 4,
    }

    [Serializable]
    public struct SkillCost
    {
        public int mana;
        public int stamina;
    }
}