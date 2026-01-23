#if UNITY_EDITOR
using System.Collections.Generic;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// SkillDamageClip(=Damage 이벤트) 실행 구간 동안 데미지 판정 영역을 Gizmo로 표시합니다.
    /// 
    /// 설계 의도:
    /// - SkillAuthoringWindow의 PlayMode 테스트에서 "스킬 사용하기"를 눌렀을 때
    ///   Damage 클립이 처리되는 시간(Start~End) 동안만 영역을 시각화할 수 있게 합니다.
    /// - 런타임 빌드에는 포함되지 않도록 UNITY_EDITOR로 컴파일을 제한합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillDamageAreaGizmo : MonoBehaviour
    {
        private struct ActiveArea
        {
            public float expireTime;
            public Vector3 center;
            public Vector3 forward;
            public GGemCo2DSkill.SkillAreaSpec area;
            public float range;
        }

        private readonly List<ActiveArea> _areas = new List<ActiveArea>(8);

        /// <summary>
        /// 데미지 영역을 지정 시간 동안 표시합니다.
        /// </summary>
        /// <param name="center">히트 평가에서 사용하는 중심점(타겟/지점/전방 보정 반영 완료)</param>
        /// <param name="forward">캐스터 전방(원뿔/박스 방향 기준)</param>
        /// <param name="area">영역 스펙(Shape/Radius/Length/Width/Angle/LocalOffset)</param>
        /// <param name="range">기본 사거리(Forward 모드 중심 보정 시 사용)</param>
        /// <param name="durationSeconds">표시 유지 시간(초)</param>
        public void Show(Vector3 center, Vector3 forward, GGemCo2DSkill.SkillAreaSpec area, float range, float durationSeconds)
        {
            if (durationSeconds <= 0f) durationSeconds = 0.05f;

            area.EnsureSaneDefaults();

            _areas.Add(new ActiveArea
            {
                expireTime = Time.time + durationSeconds,
                center = center,
                forward = forward,
                area = area,
                range = range,
            });
        }

        private void LateUpdate()
        {
            if (_areas.Count == 0) return;

            float now = Time.time;
            for (int i = _areas.Count - 1; i >= 0; i--)
            {
                if (now >= _areas[i].expireTime)
                    _areas.RemoveAt(i);
            }
        }

        private void OnDrawGizmos()
        {
            if (_areas.Count == 0) return;

            // 너무 두드러진 색상 고정은 피하고, 기본 색상에 알파만 조정합니다.
            var prev = Gizmos.color;
            Gizmos.color = new Color(prev.r, prev.g, prev.b, 0.9f);

            for (int i = 0; i < _areas.Count; i++)
            {
                DrawArea(_areas[i]);
            }

            Gizmos.color = prev;
        }

        private static void DrawArea(in ActiveArea a)
        {
            // AreaHitEvaluator와 동일하게 localOffset을 중심에 적용합니다.
            Vector3 center = a.center + a.area.localOffset;
            Vector3 fwd = Flatten(a.forward);

            switch (a.area.shape)
            {
                case ConfigCommonSkill.SkillAreaShape.Circle:
                    Gizmos.DrawWireSphere(center, a.area.radius);
                    break;

                case ConfigCommonSkill.SkillAreaShape.Box:
                {
                    var rot = Quaternion.LookRotation(fwd, Vector3.up);
                    var prevMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

                    // IsInside 로직: x=width/2, z=0..length (전방 박스)
                    // DrawWireCube는 중심 기준이므로 z방향으로 length/2 만큼 전방 이동시켜 표시합니다.
                    var boxCenter = new Vector3(0f, 0f, a.area.length * 0.5f);
                    var size = new Vector3(a.area.width, 0.02f, a.area.length);
                    Gizmos.DrawWireCube(boxCenter, size);

                    Gizmos.matrix = prevMatrix;
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Cone:
                {
                    float half = Mathf.Clamp(a.area.angle, 0f, 180f) * 0.5f;
                    var left = Quaternion.AngleAxis(-half, Vector3.up) * fwd;
                    var right = Quaternion.AngleAxis(half, Vector3.up) * fwd;

                    Vector3 p0 = center;
                    Vector3 pL = center + left.normalized * a.area.length;
                    Vector3 pR = center + right.normalized * a.area.length;

                    Gizmos.DrawLine(p0, pL);
                    Gizmos.DrawLine(p0, pR);

                    // 간단한 호(arc) 근사
                    const int segments = 16;
                    Vector3 prev = pL;
                    for (int s = 1; s <= segments; s++)
                    {
                        float t = s / (float)segments;
                        float ang = Mathf.Lerp(-half, half, t);
                        Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                        Vector3 p = center + dir.normalized * a.area.length;
                        Gizmos.DrawLine(prev, p);
                        prev = p;
                    }
                    break;
                }
            }
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }
    }
}
#endif
