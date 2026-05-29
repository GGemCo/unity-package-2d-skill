using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트에서 사용하는 카메라 화면 경계 보정 계산을 제공합니다.
    /// </summary>
    internal static class SkillScreenClampUtility
    {
        /// <summary>
        /// 현재 메인 카메라 기준으로 월드 공간의 화면 경계 사각형을 계산합니다.
        /// </summary>
        /// <param name="worldZ">보정할 오브젝트의 월드 Z 좌표입니다.</param>
        /// <param name="padding">화면 경계 안쪽으로 유지할 여유 거리입니다.</param>
        /// <param name="rect">계산된 화면 경계 Rect입니다.</param>
        /// <returns>경계 계산에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryGetCameraWorldRect(float worldZ, float padding, out Rect rect)
        {
            rect = default;

            Camera camera = SceneGame.Instance != null && SceneGame.Instance.mainCamera != null
                ? SceneGame.Instance.mainCamera
                : Camera.main;
            if (camera == null)
                return false;

            float z = Mathf.Abs(camera.transform.position.z - worldZ);
            Vector3 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, z));
            Vector3 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, z));

            float safePadding = Mathf.Max(0f, padding);
            float minX = Mathf.Min(min.x, max.x) + safePadding;
            float maxX = Mathf.Max(min.x, max.x) - safePadding;
            float minY = Mathf.Min(min.y, max.y) + safePadding;
            float maxY = Mathf.Max(min.y, max.y) - safePadding;
            if (maxX < minX || maxY < minY)
                return false;

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        /// <summary>
        /// 시작 위치가 화면 안쪽일 때, 지정한 방향으로 이동 가능한 최대 거리를 카메라 화면 경계 기준으로 계산합니다.
        /// </summary>
        /// <param name="startPosition">이동 시작 월드 위치입니다.</param>
        /// <param name="direction">정규화 전 이동 방향입니다.</param>
        /// <param name="distance">원래 이동 거리입니다.</param>
        /// <param name="padding">화면 경계 안쪽으로 유지할 여유 거리입니다.</param>
        /// <param name="clampedDistance">화면 경계가 반영된 이동 거리입니다.</param>
        /// <returns>거리 축소가 필요하면 <see langword="true"/>입니다.</returns>
        public static bool TryClampDistanceToCameraRect(
            Vector3 startPosition,
            Vector2 direction,
            float distance,
            float padding,
            out float clampedDistance)
        {
            clampedDistance = distance;
            if (!TryGetCameraWorldRect(startPosition.z, padding, out Rect screenRect))
                return false;

            return TryClampDistanceToScreenRect(startPosition, direction, distance, screenRect, out clampedDistance);
        }

        /// <summary>
        /// 시작 위치가 화면 안쪽일 때, 지정한 방향으로 이동 가능한 최대 거리를 화면 경계 기준으로 계산합니다.
        /// </summary>
        /// <param name="startPosition">이동 시작 월드 위치입니다.</param>
        /// <param name="direction">정규화 전 이동 방향입니다.</param>
        /// <param name="distance">원래 이동 거리입니다.</param>
        /// <param name="screenRect">월드 공간 화면 경계입니다.</param>
        /// <param name="clampedDistance">화면 경계가 반영된 이동 거리입니다.</param>
        /// <returns>거리 축소가 필요하면 <see langword="true"/>입니다.</returns>
        public static bool TryClampDistanceToScreenRect(
            Vector2 startPosition,
            Vector2 direction,
            float distance,
            Rect screenRect,
            out float clampedDistance)
        {
            clampedDistance = distance;
            if (distance <= 0f || direction.sqrMagnitude <= 1e-6f)
                return false;

            if (!screenRect.Contains(startPosition))
                return false;

            Vector2 normalizedDirection = direction.normalized;
            float maxAllowedDistance = distance;
            const float epsilon = 1e-6f;

            if (normalizedDirection.x > epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.xMax - startPosition.x) / normalizedDirection.x);
            else if (normalizedDirection.x < -epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.xMin - startPosition.x) / normalizedDirection.x);

            if (normalizedDirection.y > epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.yMax - startPosition.y) / normalizedDirection.y);
            else if (normalizedDirection.y < -epsilon)
                maxAllowedDistance = Mathf.Min(maxAllowedDistance, (screenRect.yMin - startPosition.y) / normalizedDirection.y);

            maxAllowedDistance = Mathf.Max(0f, maxAllowedDistance);
            if (maxAllowedDistance >= distance)
                return false;

            clampedDistance = maxAllowedDistance;
            return true;
        }

        /// <summary>
        /// 월드 좌표가 현재 카메라 화면 경계를 벗어나지 않도록 X/Y 좌표를 보정합니다.
        /// </summary>
        /// <param name="worldPosition">보정할 월드 좌표입니다.</param>
        /// <param name="padding">화면 경계 안쪽으로 유지할 여유 거리입니다.</param>
        /// <param name="clampedWorldPosition">화면 경계가 반영된 월드 좌표입니다.</param>
        /// <returns>카메라 화면 경계 계산에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryClampWorldPosition(
            Vector3 worldPosition,
            float padding,
            out Vector3 clampedWorldPosition)
        {
            clampedWorldPosition = worldPosition;
            if (!TryGetCameraWorldRect(worldPosition.z, padding, out Rect screenRect))
                return false;

            clampedWorldPosition = new Vector3(
                Mathf.Clamp(worldPosition.x, screenRect.xMin, screenRect.xMax),
                Mathf.Clamp(worldPosition.y, screenRect.yMin, screenRect.yMax),
                worldPosition.z);
            return true;
        }

        /// <summary>
        /// 지면 좌표와 공중 높이를 합산한 실제 표시 좌표를 화면 안쪽으로 보정한 뒤 다시 지면 좌표로 환산합니다.
        /// </summary>
        /// <param name="groundPosition">보정할 지면 기준 좌표입니다.</param>
        /// <param name="airHeight">지면 기준 공중 높이(+Y)입니다.</param>
        /// <param name="padding">화면 경계 안쪽으로 유지할 여유 거리입니다.</param>
        /// <param name="clampedGroundPosition">화면 경계가 반영된 지면 기준 좌표입니다.</param>
        /// <returns>카메라 화면 경계 계산에 성공하면 <see langword="true"/>입니다.</returns>
        public static bool TryClampGroundPositionWithAirHeight(
            Vector3 groundPosition,
            float airHeight,
            float padding,
            out Vector3 clampedGroundPosition)
        {
            clampedGroundPosition = groundPosition;
            float safeAirHeight = Mathf.Max(0f, airHeight);
            Vector3 worldPosition = new Vector3(
                groundPosition.x,
                groundPosition.y + safeAirHeight,
                groundPosition.z);

            if (!TryClampWorldPosition(worldPosition, padding, out Vector3 clampedWorldPosition))
                return false;

            clampedGroundPosition = new Vector3(
                clampedWorldPosition.x,
                clampedWorldPosition.y - safeAirHeight,
                groundPosition.z);
            return true;
        }
    }
}
