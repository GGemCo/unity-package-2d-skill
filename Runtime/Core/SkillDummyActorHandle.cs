using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 생성하거나 참조한 더미 캐릭터의 런타임 상태를 관리합니다.
    /// </summary>
    internal sealed class SkillDummyActorHandle
    {
        /// <summary>
        /// 더미 식별 키입니다.
        /// </summary>
        public string ActorKey;

        /// <summary>
        /// 생성된 더미 캐릭터 인스턴스입니다.
        /// </summary>
        public CharacterBase Character;

        /// <summary>
        /// 스킬 정상 종료 시 자동 제거 여부입니다.
        /// </summary>
        public bool DespawnOnSkillEnd;

        /// <summary>
        /// 스킬 취소 시 자동 제거 여부입니다.
        /// </summary>
        public bool DespawnOnCancel;

        /// <summary>
        /// 진행 중인 페이드 코루틴입니다.
        /// </summary>
        public Coroutine ActiveFadeCoroutine;

        /// <summary>
        /// 진행 중인 이동 보정 코루틴입니다.
        /// </summary>
        public Coroutine ActiveMoveCoroutine;

        /// <summary>
        /// 진행 중인 공중 높이 보정 코루틴입니다.
        /// </summary>
        public Coroutine ActiveAirHeightCoroutine;

        /// <summary>
        /// 진행 중인 애니메이션 후속 전환 코루틴입니다.
        /// </summary>
        public Coroutine ActiveAnimationCoroutine;

        /// <summary>
        /// 애니메이션 후속 전환 요청 버전입니다.
        /// 새 애니메이션 요청이 들어오면 기존 대기 코루틴을 무효화하는 데 사용합니다.
        /// </summary>
        public int AnimationRequestVersion;

        /// <summary>
        /// 지면 기준 이동 좌표입니다. 실제 월드 Y는 이 값에 AirHeight를 더해 계산합니다.
        /// </summary>
        public Vector3 GroundPosition;

        /// <summary>
        /// 지면 기준 공중 높이(+Y)입니다.
        /// </summary>
        public float AirHeight;

        /// <summary>
        /// 더미 캐릭터 제어 잠금 토큰입니다.
        /// </summary>
        public object ControlLockToken;

        /// <summary>
        /// 더미 캐릭터 브레인 잠금 토큰입니다.
        /// </summary>
        public object BrainLockToken;

        /// <summary>
        /// 공중 상태에서 사용하는 중력 오버라이드 컨트롤러입니다.
        /// </summary>
        public CharacterPhysicsOverrideController PhysicsOverrideController;

        /// <summary>
        /// 공중 상태 중력 오버라이드 해제에 사용할 핸들입니다.
        /// </summary>
        public CharacterPhysicsOverrideHandle GravityOverrideHandle;
    }
}
