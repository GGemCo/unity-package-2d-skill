// TODO: 네임스페이스 구조를 정리하고 명칭을 변경할 필요가 있습니다.
namespace Config
{
    /// <summary>
    /// 스킬 시스템에서 공통으로 사용하는 열거형과 상수를 정의합니다.
    /// 런타임과 Bake 단계에서 공유되는 스킬 관련 공용 타입을 포함합니다.
    /// </summary>
    public static class ConfigCommonSkill
    {
        /// <summary>
        /// 스킬 실행 중 발생하는 이벤트 유형을 정의합니다.
        /// 런타임과 Bake 단계에서 공통으로 사용됩니다.
        /// </summary>
        public enum SkillEventType
        {
            /// <summary>
            /// 데미지 판정과 피해 적용을 수행합니다.
            /// </summary>
            Damage = 0,

            /// <summary>
            /// 이펙트를 생성합니다.
            /// </summary>
            SpawnEffect = 1,

            /// <summary>
            /// Affect 또는 상태이상을 적용합니다.
            /// </summary>
            ApplyAffect = 2,

            /// <summary>
            /// 특정 상태를 활성화하거나 비활성화합니다.
            /// </summary>
            StateToggle = 3,

            /// <summary>
            /// 오디오를 재생합니다.
            /// </summary>
            PlayAudio = 4,

            /// <summary>
            /// 카메라 연출을 수행합니다.
            /// </summary>
            Camera = 5,

            /// <summary>
            /// 돌진 또는 강제 이동을 수행합니다.
            /// </summary>
            Lunge = 6,

            /// <summary>
            /// 투사체를 생성하거나 발사합니다.
            /// </summary>
            Projectile = 7,
        }

        /// <summary>
        /// 스킬의 동작 분류를 정의합니다.
        /// </summary>
        public enum SkillKind
        {
            /// <summary>
            /// 사용 시 캐스팅, 쿨타임, 타겟팅 절차를 거치는 능동형 스킬입니다.
            /// </summary>
            Active = 0,

            /// <summary>
            /// 장착 또는 획득 상태에서 지속적으로 적용되는 수동형 스킬입니다.
            /// </summary>
            Passive = 1,
        }

        /// <summary>
        /// 스킬의 타겟 지정 및 판정 기준을 정의합니다.
        /// </summary>
        public enum SkillTargetingMode
        {
            /// <summary>
            /// 타겟을 고정하고 명중을 보장하는 방식입니다.
            /// </summary>
            LockOnGuaranteedHit = 0,

            /// <summary>
            /// 타겟 위치를 중심으로 범위를 판정하는 방식입니다.
            /// </summary>
            TargetCenteredArea = 1,

            /// <summary>
            /// 이동하는 타겟을 추적하며 범위를 판정하는 방식입니다.
            /// </summary>
            FollowTargetArea = 2,

            /// <summary>
            /// 지정한 지면 좌표를 기준으로 판정하는 방식입니다.
            /// </summary>
            GroundTarget = 3,

            /// <summary>
            /// 캐스터의 전방 방향을 기준으로 판정하는 방식입니다.
            /// </summary>
            ForwardDirectional = 4,

            /// <summary>
            /// 투사체를 생성해 충돌 또는 도달 시점을 기준으로 처리하는 방식입니다.
            /// </summary>
            Projectile = 5,

            /// <summary>
            /// 시전자 자신에게 적용하는 방식입니다.
            /// </summary>
            Self = 6,
        }

        /// <summary>
        /// 스킬 실행 직전에 시전자 방향을 자동으로 보정하는 방식을 정의합니다.
        /// </summary>
        public enum SkillFacingMode
        {
            /// <summary>
            /// 실행 전에 방향을 자동 보정하지 않습니다.
            /// </summary>
            None = 0,

            /// <summary>
            /// 고정된 타겟을 우선 바라보며, 타겟이 없으면 전달된 전방 입력을 사용합니다.
            /// </summary>
            FaceLockedTarget = 1,

            /// <summary>
            /// 요청 컨텍스트의 전방 입력만 사용해 방향을 보정합니다.
            /// </summary>
            FaceForwardInput = 2,
        }

        /// <summary>
        /// 범위 판정에 사용하는 영역 형태를 정의합니다.
        /// </summary>
        public enum SkillAreaShape
        {
            /// <summary>
            /// 원형 범위입니다.
            /// </summary>
            Circle = 0,

            /// <summary>
            /// 박스형 범위입니다.
            /// </summary>
            Box = 1,

            /// <summary>
            /// 원뿔형 범위입니다.
            /// </summary>
            Cone = 2,

            /// <summary>
            /// 캡슐형 범위입니다.
            /// </summary>
            Capsule = 3,

            /// <summary>
            /// 선형 범위입니다.
            /// </summary>
            Line = 4,
        }
        public enum SkillOwnerType
        {
            Player = 0,
            Monster = 1,
        }
    }
}