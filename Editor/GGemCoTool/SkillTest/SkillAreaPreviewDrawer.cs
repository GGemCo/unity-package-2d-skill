using Config;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// SceneView에 스킬 범위/데미지 영역을 Handles로 표시한다.
    /// - SkillDefinition.defaultAreaId -> AreaRegistry -> AreaDefinition 기반
    /// </summary>
    [InitializeOnLoad]
    internal static class SkillAreaPreviewDrawer
    {
        static SkillAreaPreviewDrawer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!Application.isPlaying) return;
            if (!SkillTestSelection.PreviewEnabled) return;

            var monster = SkillTestSelection.SelectedMonster;
            if (monster == null) return;

            var defSkill = SkillTestSelection.SelectedDevSkill;
            if (defSkill == null) return; // devSkill 선택 시에만 프리뷰(명확성 우선)

            // 기준점/방향 결정
            Vector3 origin = monster.transform.position;
            Vector3 forward3 = new Vector3(SkillTestSelection.Forward.x, SkillTestSelection.Forward.y, 0f);
            if (forward3.sqrMagnitude < 1e-6f) forward3 = Vector3.right;

            // areaId 해석
            AreaDefinition area = null;
            if (SkillTestSelection.AreaRegistry != null)
            {
                SkillTestSelection.AreaRegistry.TryGet(defSkill.defaultAreaId, out area);
            }

            // area가 없으면 fallback: 스킬 range를 원으로 표시
            if (area == null)
            {
                Handles.DrawWireDisc(origin, Vector3.forward, Mathf.Max(0.1f, defSkill.range));
                return;
            }

            DrawArea(area, origin, forward3);
        }

        private static void DrawArea(AreaDefinition area, Vector3 casterPos, Vector3 forward)
        {
            var center = casterPos + area.localOffset;
            switch (area.shape)
            {
                case ConfigCommonSkill.SkillAreaShape.Circle:
                    Handles.DrawWireDisc(center, Vector3.forward, Mathf.Max(0.05f, area.radius));
                    break;

                case ConfigCommonSkill.SkillAreaShape.Box:
                {
                    var len = Mathf.Max(0.05f, area.length);
                    var w = Mathf.Max(0.05f, area.width);

                    // forward 기준 회전 박스(2D)
                    var rot = Quaternion.FromToRotation(Vector3.right, forward.normalized);
                    using (new Handles.DrawingScope(Matrix4x4.TRS(center, rot, Vector3.one)))
                    {
                        Handles.DrawWireCube(Vector3.zero + Vector3.right * (len * 0.5f), new Vector3(len, w, 0f));
                    }
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Cone:
                {
                    float radius = Mathf.Max(0.05f, area.length);
                    float angle = Mathf.Clamp(area.angle, 0f, 180f);
                    var dir = forward.normalized;

                    Handles.DrawWireArc(center, Vector3.forward, Quaternion.Euler(0f, 0f, -angle) * dir, angle * 2f, radius);

                    // 경계선
                    var a0 = Quaternion.Euler(0f, 0f, -angle) * dir;
                    var a1 = Quaternion.Euler(0f, 0f, angle) * dir;
                    Handles.DrawLine(center, center + a0 * radius);
                    Handles.DrawLine(center, center + a1 * radius);
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Line:
                {
                    var len = Mathf.Max(0.05f, area.length);
                    var w = Mathf.Max(0.05f, area.width);
                    var rot = Quaternion.FromToRotation(Vector3.right, forward.normalized);
                    using (new Handles.DrawingScope(Matrix4x4.TRS(center, rot, Vector3.one)))
                    {
                        Handles.DrawWireCube(Vector3.zero + Vector3.right * (len * 0.5f), new Vector3(len, w, 0f));
                    }
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Capsule:
                {
                    // 단순 표현: 중심선 + 양끝 원(2D)
                    var len = Mathf.Max(0.05f, area.length);
                    var r = Mathf.Max(0.05f, area.radius);
                    var dir = forward.normalized;

                    var p0 = center;
                    var p1 = center + dir * len;
                    Handles.DrawLine(p0, p1);
                    Handles.DrawWireDisc(p0, Vector3.forward, r);
                    Handles.DrawWireDisc(p1, Vector3.forward, r);
                    break;
                }

                default:
                    Handles.DrawWireDisc(center, Vector3.forward, Mathf.Max(0.05f, area.radius));
                    break;
            }
        }
    }
}
