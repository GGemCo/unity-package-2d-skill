namespace Config
{
    public static class ConfigCommonSkill
    {
        /// <summary>
        /// 스킬 이벤트 타입(런타임/Bake 공통).
        /// </summary>
        public enum SkillEventType
        {
            Damage = 0,
            SpawnEffect = 1,
            ApplyAffect = 2,
            StateToggle = 3,
            PlayAudio = 4,
            Camera = 5,
            Lunge = 6,
            // 확장: Projectile, HitStop, CameraShake, Dash, AreaEnable...
        }

        public enum SkillTargetingMode
        {
            LockOnGuaranteedHit = 0, // 타겟 고정 + 무조건 피격 정책
            TargetCenteredArea = 1, // 타겟 중심 범위
            FollowTargetArea = 2, // 타겟 추적 범위
            GroundTarget = 3, // 지점 고정
            ForwardDirectional = 4, // 캐스터 전방 기준
            Projectile = 5, // 투사체 기반(확장)
        }

        public enum SkillAreaShape
        {
            Circle = 0,
            Box = 1,
            Cone = 2,
            Capsule = 3,
            Line = 4,
        }

        // legacy
        public enum Target
        {
            None,
            Player, // 플레이어 자신
            Monster, //몬스터
        }

        // legacy
        public enum TargetType
        {
            None,
            Fixed, // 고정 타겟
            Range, // 범위
        }
    }
}