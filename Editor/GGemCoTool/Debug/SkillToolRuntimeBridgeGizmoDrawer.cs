#if UNITY_EDITOR
using UnityEditor;
using GGemCo2DCore;
using UnityEngine;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    internal static class SkillToolRuntimeBridgeGizmoDrawer
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        private static void DrawSkillBridgeGizmos(SkillTestRuntimeHub hub, GizmoType gizmoType)
        {
            if (hub == null)
                return;

            var prevColor = Gizmos.color;
            var prevMatrix = Gizmos.matrix;

            int selectedCasterId = hub.SelectedMonster != null ? hub.SelectedMonster.GetInstanceID() : 0;
            bool drawOnlySelectedCaster = hub.DrawOnlyWhenSelectedCaster;

            if (hub.IsDamageAreaGizmoEnabled)
            {
                var areas = hub.ActiveDamageAreas;
                if (areas != null && areas.Count > 0)
                {
                    Gizmos.color = hub.DamageAreaGizmoColor;

                    for (int i = 0; i < areas.Count; i++)
                    {
                        var area = areas[i];
                        if (area == null)
                            continue;

                        if (drawOnlySelectedCaster && selectedCasterId != 0 && area.CasterInstanceId != selectedCasterId)
                            continue;

                        Gizmos.matrix = area.Matrix;
                        DrawArea(area);
                    }
                }
            }

            if (hub.IsLaserGizmoEnabled)
            {
                var lasers = hub.ActiveLasers;
                if (lasers != null && lasers.Count > 0)
                {
                    Gizmos.color = hub.LaserGizmoColor;
                    Gizmos.matrix = Matrix4x4.identity;

                    for (int i = 0; i < lasers.Count; i++)
                    {
                        var laser = lasers[i];
                        if (laser == null)
                            continue;

                        if (drawOnlySelectedCaster && selectedCasterId != 0 && laser.casterInstanceId != selectedCasterId)
                            continue;

                        DrawLaser(laser);
                    }
                }
            }

            Gizmos.matrix = prevMatrix;
            Gizmos.color = prevColor;
        }

        private static void DrawArea(SkillDebugAreaRecord area)
        {
            switch (area.Shape)
            {
                case Config.ConfigCommonSkill.SkillAreaShape.Circle:
                    Gizmos.DrawWireSphere(Vector3.zero, area.Radius);
                    break;

                case Config.ConfigCommonSkill.SkillAreaShape.Box:
                case Config.ConfigCommonSkill.SkillAreaShape.Line:
                    Gizmos.DrawWireCube(
                        new Vector3(area.Offset.x, area.Offset.y, 0f),
                        new Vector3(area.Size.x, area.Size.y, 0.02f));
                    break;

                case Config.ConfigCommonSkill.SkillAreaShape.Capsule:
                    DrawCapsuleWire(area.Offset, area.Size, area.CapsuleDirection);
                    break;

                case Config.ConfigCommonSkill.SkillAreaShape.Cone:
                    DrawPolygon(area.PolygonPoints);
                    break;
            }
        }

        /// <summary>
        /// 레이저 선분과 시작/종료/차단 지점, 시각 회전 가이드를 그립니다.
        /// 메인 선은 Raycast 기준이며, 보조 화살표는 VfxAngleSyncMode에 따른 시각 방향을 나타냅니다.
        /// </summary>
        /// <param name="laser">그릴 레이저 기록입니다.</param>
        private static void DrawLaser(SkillDebugLaserRecord laser)
        {
            Vector3 start = laser.start;
            Vector3 end = laser.end;
            Gizmos.DrawLine(start, end);

            float size = Mathf.Max(0.08f, HandleUtility.GetHandleSize(start) * 0.05f);
            Gizmos.DrawWireSphere(start, size);
            Gizmos.DrawWireSphere(end, size * 0.85f);

            if (laser.hasBlockHit)
                Gizmos.DrawSphere(laser.blockPoint, size * 0.45f);

            DrawLaserVisualGuide(laser, size);
        }

        /// <summary>
        /// 레이저의 시각 회전 가이드를 보조 화살표로 그립니다.
        /// 메인 Raycast 선과 겹치지 않도록 수직 오프셋을 더해 표시합니다.
        /// </summary>
        /// <param name="laser">표시할 레이저 기록입니다.</param>
        /// <param name="markerSize">기준 마커 크기입니다.</param>
        private static void DrawLaserVisualGuide(SkillDebugLaserRecord laser, float markerSize)
        {
            Vector3 visualDirection = laser.visualDirection;
            if (visualDirection.sqrMagnitude <= 1e-6f)
                return;

            visualDirection.Normalize();
            Vector3 raycastDirection = laser.raycastDirection.sqrMagnitude > 1e-6f
                ? laser.raycastDirection.normalized
                : (laser.end - laser.start).normalized;
            if (raycastDirection.sqrMagnitude <= 1e-6f)
                raycastDirection = Vector3.right;

            Vector3 perpendicular = new Vector3(-raycastDirection.y, raycastDirection.x, 0f);
            if (perpendicular.sqrMagnitude <= 1e-6f)
                perpendicular = Vector3.up;

            float beamLength = Mathf.Max(0.01f, Vector3.Distance(laser.start, laser.end));
            float guideLength = Mathf.Clamp(beamLength * 0.25f, 0.45f, 1.2f);
            float guideOffset = markerSize * 2.2f;
            Vector3 guideStart = laser.start + perpendicular.normalized * guideOffset;
            Vector3 guideEnd = guideStart + visualDirection * guideLength;

            Gizmos.DrawLine(guideStart, guideEnd);
            DrawArrowHead(guideEnd, visualDirection, guideLength * 0.22f);

            if (laser.vfxAngleSyncMode == LaserConstants.VfxAngleSyncMode.None)
                Gizmos.DrawWireCube(guideStart, Vector3.one * markerSize * 0.9f);
        }

        /// <summary>
        /// 지정한 끝점에 간단한 화살표 머리를 그립니다.
        /// </summary>
        /// <param name="tip">화살표 끝점입니다.</param>
        /// <param name="direction">화살표 진행 방향입니다.</param>
        /// <param name="headSize">화살표 머리 크기입니다.</param>
        private static void DrawArrowHead(Vector3 tip, Vector3 direction, float headSize)
        {
            if (direction.sqrMagnitude <= 1e-6f || headSize <= 0f)
                return;

            Vector3 dir = direction.normalized;
            Vector3 back = -dir;
            Vector3 side = new Vector3(-dir.y, dir.x, 0f);
            Vector3 headA = tip + (back + side).normalized * headSize;
            Vector3 headB = tip + (back - side).normalized * headSize;

            Gizmos.DrawLine(tip, headA);
            Gizmos.DrawLine(tip, headB);
        }

        private static void DrawPolygon(Vector2[] points)
        {
            if (points == null || points.Length < 2)
                return;

            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                Gizmos.DrawLine(new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
            }
        }

        private static void DrawCapsuleWire(Vector2 offset, Vector2 size, CapsuleDirection2D direction)
        {
            bool vertical = direction == CapsuleDirection2D.Vertical;
            float radius = vertical ? size.x * 0.5f : size.y * 0.5f;
            radius = Mathf.Max(0.0001f, radius);

            float major = vertical ? size.y : size.x;
            float core = Mathf.Max(0f, major - 2f * radius);
            float halfCore = core * 0.5f;

            Vector3 axis = vertical ? Vector3.up : Vector3.right;
            Vector3 center = new Vector3(offset.x, offset.y, 0f);
            Vector3 cA = center + axis * halfCore;
            Vector3 cB = center - axis * halfCore;

            Gizmos.DrawWireSphere(cA, radius);
            Gizmos.DrawWireSphere(cB, radius);

            Vector3 ortho = vertical ? Vector3.right : Vector3.up;
            Gizmos.DrawLine(cA + ortho * radius, cB + ortho * radius);
            Gizmos.DrawLine(cA - ortho * radius, cB - ortho * radius);
        }
    }
}
#endif
