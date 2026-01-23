using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    public sealed class AreaHitEvaluator : IHitEvaluator
    {
        private readonly Collider2D[] _buffer = new Collider2D[128];
        private readonly LayerMask _mask;

        public AreaHitEvaluator(LayerMask mask) => _mask = mask;

        public void EvaluateTargets(
            Vector3 center,
            Vector3 forward,
            SkillAreaSpec area,
            float range,
            int maxTargets,
            GameObject caster,
            List<GameObject> results)
        {
            results.Clear();
            area.EnsureSaneDefaults();

            // 후보 수집: 가장 큰 범위로 Overlap (정밀 판정은 후처리)
            // - Physics2D 계열을 사용하므로, 2D Capsule로 넓게 스캔합니다.
            // - 타임라인/기획 데이터 상 'localOffset'이 중심을 이동시키므로, probe 중심에도 반영합니다.
            float probeRadius = EstimateProbeRadius(area);
            Vector3 probeCenter3 = center + area.localOffset;
            Vector2 probeCenter = new Vector2(probeCenter3.x, probeCenter3.y);

            var castCharacterBase = caster.GetComponent<CharacterBase>();
            if (castCharacterBase == null) return;
            
            var (size, dir, angle) = BuildProbeCapsule(area, probeRadius, forward);
            int count = CompatPhysics2D.OverlapCapsuleNonAlloc(probeCenter, size, dir, angle, _buffer);

            for (int i = 0; i < count && results.Count < maxTargets; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                // todo. 정리 필요
                // if (col.isTrigger) continue;
                // var go = col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
                // if (go == null || go == caster) continue;
                // if (((1 << go.layer) & _mask.value) == 0) continue;
                if (castCharacterBase.IsPlayer() && col.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.Player))) continue;
                if (castCharacterBase.IsMonster() && col.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.Monster))) continue;
                
                CharacterHitArea characterHitArea = col.GetComponent<CharacterHitArea>();
                if (characterHitArea == null) continue;

                var p = col.transform.position;

                if (IsInside(area, center, forward, p))
                    results.Add(col.gameObject);
            }
        }

        private static float EstimateProbeRadius(in SkillAreaSpec a)
        {
            return a.shape switch
            {
                ConfigCommonSkill.SkillAreaShape.Circle => a.radius,
                ConfigCommonSkill.SkillAreaShape.Box => Mathf.Max(a.width, a.length) * 0.75f,
                ConfigCommonSkill.SkillAreaShape.Cone => a.length,
                ConfigCommonSkill.SkillAreaShape.Capsule => Mathf.Max(a.radius, a.length * 0.5f),
                ConfigCommonSkill.SkillAreaShape.Line => Mathf.Max(a.width, a.length),
                _ => a.radius
            };
        }

        private static bool IsInside(in SkillAreaSpec a, Vector3 center, Vector3 forward, Vector3 point)
        {
            // localOffset 적용(중심 이동)
            center += a.localOffset;

            // Physics2D 기반이므로 XY 평면 기준으로 계산합니다.
            var c2 = new Vector2(center.x, center.y);
            var p2 = new Vector2(point.x, point.y);
            var f2 = Flatten2D(forward);

            switch (a.shape)
            {
                case ConfigCommonSkill.SkillAreaShape.Circle:
                {
                    var d = p2 - c2;
                    return d.sqrMagnitude <= a.radius * a.radius;
                }
                case ConfigCommonSkill.SkillAreaShape.Box:
                {
                    // forward 기준 로컬 박스 (XY)
                    float ang = Mathf.Atan2(f2.y, f2.x) * Mathf.Rad2Deg;
                    var rot = Quaternion.Euler(0f, 0f, ang);
                    var local3 = Quaternion.Inverse(rot) * new Vector3(p2.x - c2.x, p2.y - c2.y, 0f);
                    return Mathf.Abs(local3.x) <= a.width * 0.5f && local3.y >= 0f && local3.y <= a.length;
                }
                case ConfigCommonSkill.SkillAreaShape.Cone:
                {
                    var v = p2 - c2;
                    float dist = v.magnitude;
                    if (dist > a.length) return false;
                    if (dist < 1e-4f) return true;

                    float ang = Vector2.Angle(f2, v);
                    return ang <= a.angle * 0.5f;
                }
                default:
                    return false;
            }
        }

        private static (Vector2 size, CapsuleDirection2D direction, float angle) BuildProbeCapsule(
            in SkillAreaSpec a,
            float probeRadius,
            Vector3 forward)
        {
            // 가장 큰 후보를 넓게 긁어오는 용도이므로, shape별로 너무 타이트하게 맞추지 않습니다.
            // - Circle: 지름 기반 캡슐(사실상 원)
            // - Box/Cone/Line: 길이 축을 length 쪽으로 크게
            // - Capsule: 기존 스펙을 최대한 반영
            float width = probeRadius * 2f;
            float height = probeRadius * 2f;

            switch (a.shape)
            {
                case ConfigCommonSkill.SkillAreaShape.Box:
                    width = Mathf.Max(width, a.width);
                    height = Mathf.Max(height, a.length);
                    break;
                case ConfigCommonSkill.SkillAreaShape.Cone:
                    width = Mathf.Max(width, a.length);
                    height = Mathf.Max(height, a.length);
                    break;
                case ConfigCommonSkill.SkillAreaShape.Capsule:
                    width = Mathf.Max(width, a.radius * 2f);
                    height = Mathf.Max(height, a.length);
                    break;
                case ConfigCommonSkill.SkillAreaShape.Line:
                    width = Mathf.Max(width, a.width);
                    height = Mathf.Max(height, a.length);
                    break;
            }

            // 캡슐 기본 축은 Vertical로 두고, angle로 회전시켜 forward에 정렬합니다.
            var f2 = Flatten2D(forward);
            // vertical(0,1) 기준으로 forward를 맞추기 위한 회전각
            float angle = Mathf.Atan2(f2.y, f2.x) * Mathf.Rad2Deg - 90f;
            return (new Vector2(width, height), CapsuleDirection2D.Vertical, angle);
        }

        private static Vector2 Flatten2D(Vector3 v)
        {
            var r = new Vector2(v.x, v.y);
            return r.sqrMagnitude < 1e-6f ? Vector2.up : r.normalized;
        }
    }
}
