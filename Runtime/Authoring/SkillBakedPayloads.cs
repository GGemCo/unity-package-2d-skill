using System;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Bake된 타입별 Payload 컨테이너.
    /// 이벤트는 PayloadIndex로 각 배열을 참조한다.
    /// </summary>
    [Serializable]
    public sealed class SkillBakedPayloads
    {
        public DamagePayload[] Damage;
        public SpawnVfxPayload[] SpawnVfx;
        public PlaySfxPayload[] PlaySfx;
        public ApplyAffectPayload[] ApplyAffect;
    }

    [Serializable]
    public struct DamagePayload
    {
        public float Coef;
        public int DamageTypeUid;
        public int AreaUid; // 예: area 테이블 UID
    }

    [Serializable]
    public struct SpawnVfxPayload
    {
        public GameObject Prefab;
        public int Anchor;          // 0=캐스터,1=타겟,2=지면 등 규칙화
        public Vector2 Offset;
    }

    [Serializable]
    public struct PlaySfxPayload
    {
        public AudioClip Clip;
        public float Volume;
    }

    [Serializable]
    public struct ApplyAffectPayload
    {
        public int AffectUid;
        public float Duration; // -1이면 무기한 등 규칙화 가능
    }
}
