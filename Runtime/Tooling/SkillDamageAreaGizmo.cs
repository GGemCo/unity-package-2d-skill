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
            // Physics2D/2D 연산과 동일하게 XY 평면(=Z 고정) 기준으로 그립니다.
            Vector3 center = a.center + a.area.localOffset;
            Vector2 fwd2 = Flatten2D(a.forward);

            switch (a.area.shape)
            {
                case ConfigCommonSkill.SkillAreaShape.Circle:
                    Gizmos.DrawWireSphere(center, a.area.radius);
                    break;

                case ConfigCommonSkill.SkillAreaShape.Box:
                {
                    var rot = Quaternion.Euler(0f, 0f, ToAngleDeg(fwd2) - 90f);
                    var prevMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

                    // IsInside 로직: x=width/2, z=0..length (전방 박스)
                    // Physics2D(XY) 기준으로는 local y축이 전방(0..length)입니다.
                    // DrawWireCube는 중심 기준이므로 y방향으로 length/2 만큼 전방 이동시켜 표시합니다.
                    var boxCenter = new Vector3(0f, a.area.length * 0.5f, 0f);
                    var size = new Vector3(a.area.width, a.area.length, 0.02f);
                    Gizmos.DrawWireCube(boxCenter, size);

                    Gizmos.matrix = prevMatrix;
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Cone:
                {
                    float half = Mathf.Clamp(a.area.angle, 0f, 180f) * 0.5f;
                    var left = Rotate2D(fwd2, -half);
                    var right = Rotate2D(fwd2, half);

                    Vector3 p0 = center;
                    Vector3 pL = center + new Vector3(left.x, left.y, 0f) * a.area.length;
                    Vector3 pR = center + new Vector3(right.x, right.y, 0f) * a.area.length;

                    Gizmos.DrawLine(p0, pL);
                    Gizmos.DrawLine(p0, pR);

                    // 간단한 호(arc) 근사
                    const int segments = 16;
                    Vector3 prev = pL;
                    for (int s = 1; s <= segments; s++)
                    {
                        float t = s / (float)segments;
                        float ang = Mathf.Lerp(-half, half, t);
                        var dir = Rotate2D(fwd2, ang);
                        Vector3 p = center + new Vector3(dir.x, dir.y, 0f) * a.area.length;
                        Gizmos.DrawLine(prev, p);
                        prev = p;
                    }
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Capsule:
                {
                    // Gizmos에는 wire capsule이 없으므로, 2개의 원 + 연결선으로 근사합니다.
                    // 전방 축(local +Y) 기준 length를 따라 배치합니다.
                    float r = Mathf.Max(0.01f, a.area.radius);
                    float len = Mathf.Max(0.01f, a.area.length);

                    var rot = Quaternion.Euler(0f, 0f, ToAngleDeg(fwd2) - 90f);
                    var prevMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

                    // 캡슐 중심선 길이(양 끝 원 중심 간 거리)
                    float core = Mathf.Max(0f, len - 2f * r);
                    var cA = new Vector3(0f, r, 0f);
                    var cB = new Vector3(0f, r + core, 0f);
                    Gizmos.DrawWireSphere(cA, r);
                    Gizmos.DrawWireSphere(cB, r);

                    // 옆선 (좌/우)
                    Gizmos.DrawLine(cA + new Vector3(-r, 0f, 0f), cB + new Vector3(-r, 0f, 0f));
                    Gizmos.DrawLine(cA + new Vector3(r, 0f, 0f), cB + new Vector3(r, 0f, 0f));

                    Gizmos.matrix = prevMatrix;
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Line:
                {
                    // Line을 "폭이 있는 직선 구간"으로 보고 Box와 동일한 방식으로 표시합니다.
                    var rot = Quaternion.Euler(0f, 0f, ToAngleDeg(fwd2) - 90f);
                    var prevMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

                    var boxCenter = new Vector3(0f, a.area.length * 0.5f, 0f);
                    var size = new Vector3(a.area.width, a.area.length, 0.02f);
                    Gizmos.DrawWireCube(boxCenter, size);

                    Gizmos.matrix = prevMatrix;
                    break;
                }
            }
        }

        private static Vector2 Flatten2D(Vector3 v)
        {
            var r = new Vector2(v.x, v.y);
            return r.sqrMagnitude < 1e-6f ? Vector2.up : r.normalized;
        }

        private static float ToAngleDeg(Vector2 dir)
        {
            // Vector2.up(0,1)을 90도로, Vector2.right(1,0)을 0도로 두는 표준 atan2 각도
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        private static Vector2 Rotate2D(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float s = Mathf.Sin(rad);
            float c = Mathf.Cos(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
#endif
