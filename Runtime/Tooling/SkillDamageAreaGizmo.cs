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
            public float ExpireTime;
            public ConfigCommonSkill.SkillAreaShape Shape;
            public Collider2D Probe;
        }

        private readonly List<ActiveArea> _areas = new List<ActiveArea>(8);

        // Gizmo는 판정에 사용하는 Probe 설정 규칙과 1:1로 동일해야 하므로,
        // DamageAreaProbeCache의 Configure 로직을 그대로 사용합니다.
        private readonly GGemCo2DSkill.DamageAreaProbeCache _probeCache = new GGemCo2DSkill.DamageAreaProbeCache();

        /// <summary>
        /// 데미지 영역을 지정 시간 동안 표시합니다.
        /// </summary>
        /// <param name="center">히트 평가에서 사용하는 중심점(타겟/지점/전방 보정 반영 완료)</param>
        /// <param name="forward">캐스터 전방(원뿔/박스 방향 기준)</param>
        /// <param name="area">영역 스펙(Shape/Radius/Length/Width/Angle/LocalOffset)</param>
        /// <param name="range">기본 사거리(Forward 모드 중심 보정 시 사용)</param>
        /// <param name="durationSeconds">표시 유지 시간(초)</param>
        public void Show(Vector3 center, Vector3 forward, GGemCo2DSkill.SkillAreaSpec area, float range, float durationSeconds, GameObject caster)
        {
            if (durationSeconds <= 0f) durationSeconds = 0.05f;

            area.EnsureSaneDefaults();

            // 동시 표시를 지원하기 위해 Gizmo 전용 Probe를 풀에서 획득합니다.
            var probe = _probeCache.AcquireForGizmo(area.shape);
            _probeCache.Configure(probe, area, center, forward, caster);

            _areas.Add(new ActiveArea
            {
                ExpireTime = Time.time + durationSeconds,
                Shape = area.shape,
                Probe = probe,
            });
        }

        private void LateUpdate()
        {
            if (_areas.Count == 0) return;

            float now = Time.time;
            for (int i = _areas.Count - 1; i >= 0; i--)
            {
                if (now >= _areas[i].ExpireTime)
                {
                    ReleaseArea(i);
                }
            }
        }

        private void OnDestroy()
        {
            // PlayMode 테스트 종료 등으로 오브젝트가 파괴될 때 Probe를 풀로 반환합니다.
            for (int i = _areas.Count - 1; i >= 0; i--)
            {
                ReleaseArea(i);
            }
        }

        private void ReleaseArea(int index)
        {
            if (index < 0 || index >= _areas.Count) return;
            var a = _areas[index];
            _areas.RemoveAt(index);

            if (a.Probe != null)
                _probeCache.ReleaseForGizmo(a.Shape, a.Probe);
        }

        private void OnDrawGizmos()
        {
            if (_areas.Count == 0) return;

            // 너무 두드러진 색상 고정은 피하고, 기본 색상에 알파만 조정합니다.
            var prev = Gizmos.color;
            Gizmos.color = new Color(prev.r, prev.g, prev.b, 0.9f);

            for (int i = 0; i < _areas.Count; i++)
            {
                DrawProbe(_areas[i].Probe);
            }

            Gizmos.color = prev;
        }

        private static void DrawProbe(Collider2D probe)
        {
            if (probe == null || !probe.gameObject.activeInHierarchy) return;

            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = probe.transform.localToWorldMatrix;

            switch (probe)
            {
                case CircleCollider2D c:
                    Gizmos.DrawWireSphere(new Vector3(c.offset.x, c.offset.y, 0f), c.radius);
                    break;

                case BoxCollider2D b:
                    Gizmos.DrawWireCube(new Vector3(b.offset.x, b.offset.y, 0f), new Vector3(b.size.x, b.size.y, 0.02f));
                    break;

                case CapsuleCollider2D cap:
                    DrawCapsuleWire(cap);
                    break;

                case PolygonCollider2D poly:
                    DrawPolygonWire(poly);
                    break;
            }

            Gizmos.matrix = prevMatrix;
        }

        private static void DrawPolygonWire(PolygonCollider2D poly)
        {
            int pathCount = poly.pathCount;
            if (pathCount <= 0) return;

            var offset = poly.offset;

            for (int p = 0; p < pathCount; p++)
            {
                var pts = poly.GetPath(p);
                if (pts == null || pts.Length < 2) continue;

                for (int i = 0; i < pts.Length; i++)
                {
                    var a = pts[i] + offset;
                    var b = pts[(i + 1) % pts.Length] + offset;
                    Gizmos.DrawLine(new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
                }
            }
        }

        private static void DrawCapsuleWire(CapsuleCollider2D cap)
        {
            // Gizmos에는 wire capsule이 없으므로, 2개의 원 + 연결선으로 근사합니다.
            // CapsuleCollider2D는 offset을 중심으로 size를 갖습니다.
            var offset = cap.offset;
            var size = cap.size;

            bool vertical = cap.direction == CapsuleDirection2D.Vertical;
            float r = vertical ? size.x * 0.5f : size.y * 0.5f;
            r = Mathf.Max(0.0001f, r);

            float major = vertical ? size.y : size.x;
            float core = Mathf.Max(0f, major - 2f * r);
            float halfCore = core * 0.5f;

            Vector3 axis = vertical ? Vector3.up : Vector3.right;

            Vector3 cA = new Vector3(offset.x, offset.y, 0f) + axis * halfCore;
            Vector3 cB = new Vector3(offset.x, offset.y, 0f) - axis * halfCore;

            Gizmos.DrawWireSphere(cA, r);
            Gizmos.DrawWireSphere(cB, r);

            // 옆선(좌/우 또는 상/하)
            Vector3 ortho = vertical ? Vector3.right : Vector3.up;
            Gizmos.DrawLine(cA + ortho * r, cB + ortho * r);
            Gizmos.DrawLine(cA - ortho * r, cB - ortho * r);
        }
    }
}
#endif
