// Assets/GGemCo/Skills/Runtime/Combat/AreaHitEvaluator.cs
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    public sealed class AreaHitEvaluator : IHitEvaluator
    {
        private readonly Collider[] _buffer = new Collider[128];
        private readonly LayerMask _mask;

        public AreaHitEvaluator(LayerMask mask) => _mask = mask;

        public void EvaluateTargets(
            Vector3 center,
            Vector3 forward,
            AreaDefinition area,
            float range,
            int maxTargets,
            GameObject caster,
            List<GameObject> results)
        {
            results.Clear();
            if (area == null) return;

            // 후보 수집: 가장 큰 반경으로 Overlap (정밀 판정은 후처리)
            float probeRadius = EstimateProbeRadius(area);
            int count = Physics.OverlapSphereNonAlloc(center, probeRadius, _buffer, _mask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count && results.Count < maxTargets; i++)
            {
                var col = _buffer[i];
                var go = col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
                if (go == null || go == caster) continue;

                var p = go.transform.position;

                if (IsInside(area, center, forward, p))
                    results.Add(go);
            }
        }

        private static float EstimateProbeRadius(AreaDefinition a)
        {
            return a.shape switch
            {
                SkillAreaShape.Circle => a.radius,
                SkillAreaShape.Box => Mathf.Max(a.width, a.length) * 0.75f,
                SkillAreaShape.Cone => a.length,
                SkillAreaShape.Capsule => Mathf.Max(a.radius, a.length * 0.5f),
                SkillAreaShape.Line => Mathf.Max(a.width, a.length),
                _ => a.radius
            };
        }

        private static bool IsInside(AreaDefinition a, Vector3 center, Vector3 forward, Vector3 point)
        {
            // localOffset 적용(중심 이동)
            center += a.localOffset;

            switch (a.shape)
            {
                case SkillAreaShape.Circle:
                {
                    var d = point - center;
                    d.y = 0f;
                    return d.sqrMagnitude <= a.radius * a.radius;
                }
                case SkillAreaShape.Box:
                {
                    // forward 기준 로컬 박스
                    var rot = Quaternion.LookRotation(Flatten(forward), Vector3.up);
                    var local = Quaternion.Inverse(rot) * (point - center);
                    return Mathf.Abs(local.x) <= a.width * 0.5f && local.z >= 0f && local.z <= a.length;
                }
                case SkillAreaShape.Cone:
                {
                    var v = point - center;
                    v.y = 0f;
                    float dist = v.magnitude;
                    if (dist > a.length) return false;
                    if (dist < 1e-4f) return true;

                    var f = Flatten(forward);
                    float ang = Vector3.Angle(f, v);
                    return ang <= a.angle * 0.5f;
                }
                default:
                    return false;
            }
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }
    }
}
