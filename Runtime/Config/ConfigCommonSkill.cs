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
            SpawnVfx = 1,

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
            /// 공중에서 지면으로 내려치는 강하 이동을 수행합니다.
            /// </summary>
            GroundSlam = 7,

            /// <summary>
            /// 투사체를 생성하거나 발사합니다.
            /// </summary>
            Projectile = 8,

            /// <summary>
            /// 현재 위치에 일정 시간 또는 스킬 종료까지 머무릅니다.
            /// </summary>
            PositionHold = 9,

            /// <summary>
            /// 런타임 Temp HP(비저장 보호막/임시 하트)를 적용합니다.
            /// </summary>
            ApplyTempHp = 10,

            /// <summary>
            /// 분리된 Laser 시스템을 사용해 레이저를 생성하거나 발사합니다.
            /// </summary>
            Laser = 11,

            /// <summary>
            /// 스킬 실행 중 더미 캐릭터를 생성합니다.
            /// </summary>
            SpawnDummyCharacter = 12,

            /// <summary>
            /// 스킬 실행 중 생성된 더미 캐릭터를 이동시킵니다.
            /// </summary>
            MoveDummyCharacter = 13,

            /// <summary>
            /// 스킬 실행 중 생성된 더미 캐릭터를 제거합니다.
            /// </summary>
            DespawnDummyCharacter = 14,

            /// <summary>
            /// 스킬 실행 중 생성된 더미 캐릭터의 공중 상태(높이/중력)를 제어합니다.
            /// </summary>
            SetDummyAirborneState = 15,

            /// <summary>
            /// 스킬 실행 중 생성된 더미 캐릭터에 특정 애니메이션을 재생합니다.
            /// </summary>
            PlayDummyCharacterAnimation = 16,

            /// <summary>
            /// 스킬 실행 중 화면 전체 페이드 연출을 수행합니다.
            /// </summary>
            ScreenFade = 17,

            /// <summary>
            /// 스킬 실행 중 캐릭터 잔상 트레일 또는 단발 잔상을 생성합니다.
            /// </summary>
            Afterimage = 18,
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
        /// UseClip 애니메이션 길이와 재생 속도를 스킬 런타임 시퀀스에 반영하는 방식을 정의합니다.
        /// </summary>
        public enum SkillUseClipTimingPolicy
        {
            /// <summary>
            /// RuntimeSequence에 Bake된 시간을 그대로 사용합니다. 기존 스킬과 동일한 기본 정책입니다.
            /// </summary>
            RuntimeSequence = 0,

            /// <summary>
            /// UseClip의 실제 재생 시간에 맞춰 RuntimeSequence의 이벤트 시작 시간과 이벤트 구간 길이를 함께 스케일합니다.
            /// </summary>
            ScaleSequenceToUseClip = 1,

            /// <summary>
            /// 이벤트 시간축은 그대로 유지하되, UseClip 실제 재생 시간이 끝나면 스킬 런을 종료합니다.
            /// </summary>
            EndByUseClip = 2,

            /// <summary>
            /// RuntimeSequence와 UseClip 실제 재생 시간 중 더 긴 쪽이 끝날 때 스킬 런을 종료합니다.
            /// </summary>
            EndByLonger = 3,
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
