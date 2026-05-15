using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 캐릭터의 월드 위치, 바라보기, Rigidbody 속도, 공중 중력 오버라이드 같은 런타임 보조 처리를 담당합니다.
    /// </summary>
    internal static class SkillDummyActorRuntimeUtility
    {
        /// <summary>
        /// 이동 중 더미의 바라보는 방향을 타겟 기준으로 갱신합니다.
        /// </summary>
        /// <param name="handle">방향을 갱신할 더미 핸들입니다.</param>
        /// <param name="lookTargetTransform">실시간으로 추적할 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 타겟 좌표입니다.</param>
        public static void UpdateFacingDuringMove(
            SkillDummyActorHandle handle,
            Transform lookTargetTransform,
            Vector3 fallbackLookTargetPosition)
        {
            if (handle == null || handle.Character == null)
                return;

            if (!TryResolveLookTargetPosition(lookTargetTransform, fallbackLookTargetPosition, out Vector3 lookTargetPosition))
                return;

            ApplyFacingByPosition(handle.Character, lookTargetPosition);
        }

        /// <summary>
        /// 더미 캐릭터의 지면 기준 좌표를 현재 Transform 값으로 동기화합니다.
        /// </summary>
        /// <param name="handle">동기화할 더미 핸들입니다.</param>
        public static void SyncGroundFromTransform(SkillDummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            Vector3 worldPosition = handle.Character.transform.position;
            handle.GroundPosition = new Vector3(
                worldPosition.x,
                worldPosition.y - handle.AirHeight,
                worldPosition.z);
        }

        /// <summary>
        /// 더미 캐릭터의 지면 좌표와 공중 높이로 최종 월드 좌표를 계산합니다.
        /// </summary>
        /// <param name="handle">대상 더미 핸들입니다.</param>
        /// <returns>지면 좌표와 공중 높이가 반영된 월드 좌표입니다.</returns>
        public static Vector3 ComposeWorldPosition(SkillDummyActorHandle handle)
        {
            return new Vector3(
                handle.GroundPosition.x,
                handle.GroundPosition.y + Mathf.Max(0f, handle.AirHeight),
                handle.GroundPosition.z);
        }

        /// <summary>
        /// 더미 캐릭터에 현재 지면 좌표와 공중 높이를 반영하여 Transform을 갱신합니다.
        /// </summary>
        /// <param name="handle">위치를 갱신할 더미 핸들입니다.</param>
        public static void ApplyWorldPosition(SkillDummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            handle.Character.transform.position = ComposeWorldPosition(handle);
        }

        /// <summary>
        /// 단일 더미의 공중 유지 상태를 점검하고, 필요 시 중력 오버라이드 및 월드 좌표를 재적용합니다.
        /// </summary>
        /// <param name="handle">점검할 더미 핸들입니다.</param>
        public static void MaintainAirborneState(SkillDummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            if (handle.AirHeight <= 1e-4f)
                return;

            EnsureGravityOverride(handle);
            ZeroRigidbodyVelocity(handle);
            ApplyWorldPosition(handle);
        }

        /// <summary>
        /// 더미를 수동 좌표 제어할 때 물리 속도로 인해 위치가 미세하게 누적되는 현상을 방지하기 위해 속도를 0으로 고정합니다.
        /// </summary>
        /// <param name="handle">속도를 보정할 더미 핸들입니다.</param>
        public static void ZeroRigidbodyVelocity(SkillDummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            Rigidbody2D rb = handle.Character.characterRigidbody2D != null
                ? handle.Character.characterRigidbody2D
                : handle.Character.GetComponent<Rigidbody2D>();

            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
                return;

            rb.SetLinearVelocity(Vector2.zero);
            rb.angularVelocity = 0f;
        }

        /// <summary>
        /// 더미 캐릭터의 중력을 비활성화하는 오버라이드를 획득합니다.
        /// </summary>
        /// <param name="handle">중력 오버라이드를 적용할 더미 핸들입니다.</param>
        public static void EnsureGravityOverride(SkillDummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            CharacterPhysicsOverrideController physicsOverride = handle.Character.GetComponent<CharacterPhysicsOverrideController>();
            if (physicsOverride == null)
                physicsOverride = handle.Character.gameObject.AddComponent<CharacterPhysicsOverrideController>();

            if (physicsOverride == null)
                return;

            if (handle.GravityOverrideHandle.IsValid)
            {
                if (object.ReferenceEquals(handle.PhysicsOverrideController, physicsOverride))
                    return;

                if (handle.PhysicsOverrideController != null)
                    handle.PhysicsOverrideController.ReleaseGravityOverride(ref handle.GravityOverrideHandle);
                else
                    handle.GravityOverrideHandle = default;
            }

            handle.PhysicsOverrideController = physicsOverride;
            handle.GravityOverrideHandle = physicsOverride.AcquireGravityOverride(
                ownerKey: handle,
                lifecycleOwner: handle.Character,
                channel: CharacterPhysicsOverrideChannel.Skill,
                priority: CharacterPhysicsOverridePriority.Skill,
                gravityScale: 0f,
                reason: "SkillDummyAirborne");
        }

        /// <summary>
        /// 더미 캐릭터에 적용한 중력 오버라이드를 해제합니다.
        /// </summary>
        /// <param name="handle">중력 오버라이드를 해제할 더미 핸들입니다.</param>
        public static void ReleaseGravityOverride(SkillDummyActorHandle handle)
        {
            if (handle == null)
                return;

            if (handle.PhysicsOverrideController != null && handle.GravityOverrideHandle.IsValid)
                handle.PhysicsOverrideController.ReleaseGravityOverride(ref handle.GravityOverrideHandle);
            else
                handle.GravityOverrideHandle = default;

            handle.PhysicsOverrideController = null;
        }

        /// <summary>
        /// 이동 중 바라보기 계산에 사용할 타겟 좌표를 결정합니다.
        /// </summary>
        /// <param name="lookTargetTransform">실시간 타겟 Transform입니다.</param>
        /// <param name="fallbackLookTargetPosition">실시간 타겟이 없을 때 사용할 고정 타겟 좌표입니다.</param>
        /// <param name="lookTargetPosition">결정된 타겟 좌표입니다.</param>
        /// <returns>타겟 좌표를 결정했으면 <see langword="true"/>를 반환합니다.</returns>
        private static bool TryResolveLookTargetPosition(
            Transform lookTargetTransform,
            Vector3 fallbackLookTargetPosition,
            out Vector3 lookTargetPosition)
        {
            if (lookTargetTransform != null)
            {
                lookTargetPosition = lookTargetTransform.position;
                return true;
            }

            lookTargetPosition = fallbackLookTargetPosition;
            return true;
        }

        /// <summary>
        /// 타겟의 X축 상대 위치를 기준으로 더미의 좌우 바라보기 방향을 적용합니다.
        /// </summary>
        /// <param name="character">방향을 적용할 캐릭터입니다.</param>
        /// <param name="lookTargetPosition">바라볼 타겟 월드 좌표입니다.</param>
        private static void ApplyFacingByPosition(CharacterBase character, Vector3 lookTargetPosition)
        {
            if (character == null)
                return;

            float deltaX = lookTargetPosition.x - character.transform.position.x;
            if (Mathf.Abs(deltaX) <= 1e-4f)
                return;

            CharacterConstants.FacingDirection8 facing = deltaX >= 0f
                ? CharacterConstants.FacingDirection8.Right
                : CharacterConstants.FacingDirection8.Left;
            character.SetFacing(facing);
        }
    }
}
