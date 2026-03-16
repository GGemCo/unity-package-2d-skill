#if UNITY_EDITOR
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    internal static class SkillToolRuntimeBridgeGizmoDrawer
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        private static void DrawSkillBridgeGizmos(SkillTestRuntimeHub hub, GizmoType gizmoType)
        {
            if (hub == null || !SkillSettingsRuntime.IsDamageAreaGizmoEnabled)
                return;

            var areas = hub.ActiveDamageAreas;
            if (areas == null || areas.Count == 0)
                return;

            var settings = SkillSettingsRuntime.Current;
            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            Gizmos.color = settings != null ? settings.damageAreaGizmoColor : new Color(1f, 0.35f, 0.2f, 0.9f);

            int selectedCasterId = hub.SelectedMonster != null ? hub.SelectedMonster.GetInstanceID() : 0;
            bool drawOnlySelectedCaster = settings != null && settings.drawOnlyWhenSelectedCaster;

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

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
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
                    Gizmos.DrawWireCube(new Vector3(area.Offset.x, area.Offset.y, 0f), new Vector3(area.Size.x, area.Size.y, 0.02f));
                    break;

                case Config.ConfigCommonSkill.SkillAreaShape.Capsule:
                    DrawCapsuleWire(area.Offset, area.Size, area.CapsuleDirection);
                    break;

                case Config.ConfigCommonSkill.SkillAreaShape.Cone:
                    DrawPolygon(area.PolygonPoints);
                    break;
            }
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
