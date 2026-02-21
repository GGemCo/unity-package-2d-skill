using System.Collections.Generic;
using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// SkillAreaSpec(데미지 영역)를 2D Collider로 구성하여, 타겟의 colliderHitArea(CapsuleCollider2D)와
    /// 오버랩 판정(OverlapCollider)으로 타격 여부를 계산합니다.
    ///
    /// - shape 별 Probe Collider를 1개씩 생성/캐시합니다(런타임 재사용).
    /// - Probe는 Rigidbody2D 없이 동작하며, IsTrigger/UsedByEffector 설정과 무관하게 오버랩 쿼리만 사용합니다.
    /// </summary>
    internal sealed class DamageAreaProbeCache
    {
        private readonly Dictionary<ConfigCommonSkill.SkillAreaShape, Collider2D> _probes = new();
        private GameObject _root;

        public Collider2D GetOrCreate(ConfigCommonSkill.SkillAreaShape shape)
        {
            if (_probes.TryGetValue(shape, out var col) && col != null)
                return col;

            EnsureRoot();

            var go = new GameObject($"DamageAreaProbe_{shape}")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            go.transform.SetParent(_root.transform, false);

            col = shape switch
            {
                ConfigCommonSkill.SkillAreaShape.Circle => go.AddComponent<CircleCollider2D>(),
                ConfigCommonSkill.SkillAreaShape.Box => go.AddComponent<BoxCollider2D>(),
                ConfigCommonSkill.SkillAreaShape.Line => go.AddComponent<BoxCollider2D>(),
                ConfigCommonSkill.SkillAreaShape.Capsule => CreateCapsule(go),
                ConfigCommonSkill.SkillAreaShape.Cone => go.AddComponent<PolygonCollider2D>(),
                _ => go.AddComponent<CircleCollider2D>()
            };

            // 오버랩 쿼리용이므로 Trigger로 고정해두면, 프로젝트 설정(queriesHitTriggers)에 덜 민감합니다.
            col.isTrigger = true;

            _probes[shape] = col;
            return col;
        }

        public void Configure(Collider2D probe, in SkillAreaSpec area, Vector3 center, Vector3 forward)
        {
            if (probe == null) return;

            area.EnsureSaneDefaults();

            // localOffset 적용(중심 이동)
            var worldCenter = center + area.localOffset;

            // forward 기준 로컬(+Y) 전방 정렬
            var f2 = Flatten2D(forward);
            float z = Mathf.Atan2(f2.y, f2.x) * Mathf.Rad2Deg - 90f;

            // shape별로 기준점이 다릅니다.
            // - Circle: center가 곧 원 중심
            // - Box/Line/Capsule: center는 '시작점'(near edge). 콜라이더는 중앙 기준이므로 offset으로 보정
            // - Cone: apex(꼭지점)가 center(시작점)

            switch (area.shape)
            {
                case ConfigCommonSkill.SkillAreaShape.Circle:
                {
                    var c = (CircleCollider2D)probe;
                    probe.transform.position = worldCenter;
                    probe.transform.rotation = Quaternion.identity;
                    c.offset = Vector2.zero;
                    c.radius = Mathf.Max(0f, area.radius);
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Box:
                {
                    var b = (BoxCollider2D)probe;
                    probe.transform.position = worldCenter;
                    probe.transform.rotation = Quaternion.Euler(0f, 0f, z);
                    b.size = new Vector2(Mathf.Max(0f, area.width), Mathf.Max(0f, area.length));
                    b.offset = new Vector2(0f, area.length * 0.5f);
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Line:
                {
                    // Line은 "폭이 있는 직선"으로 해석합니다(=Box)
                    var b = (BoxCollider2D)probe;
                    probe.transform.position = worldCenter;
                    probe.transform.rotation = Quaternion.Euler(0f, 0f, z);
                    b.size = new Vector2(Mathf.Max(0f, area.width), Mathf.Max(0f, area.length));
                    b.offset = new Vector2(0f, area.length * 0.5f);
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Capsule:
                {
                    var c = (CapsuleCollider2D)probe;
                    probe.transform.position = worldCenter;
                    probe.transform.rotation = Quaternion.Euler(0f, 0f, z);

                    c.direction = CapsuleDirection2D.Vertical;
                    c.size = new Vector2(Mathf.Max(0f, area.radius * 2f), Mathf.Max(0f, area.length));
                    c.offset = new Vector2(0f, area.length * 0.5f);
                    break;
                }

                case ConfigCommonSkill.SkillAreaShape.Cone:
                {
                    var p = (PolygonCollider2D)probe;
                    probe.transform.position = worldCenter;
                    probe.transform.rotation = Quaternion.Euler(0f, 0f, z);

                    float len = Mathf.Max(0.0001f, area.length);
                    float half = Mathf.Max(0f, area.angle) * 0.5f * Mathf.Deg2Rad;

                    // local 공간에서 forward는 +Y
                    Vector2 apex = Vector2.zero;
                    Vector2 left = new Vector2(-Mathf.Sin(half) * len, Mathf.Cos(half) * len);
                    Vector2 right = new Vector2(Mathf.Sin(half) * len, Mathf.Cos(half) * len);

                    // PolygonCollider2D는 path 단위 설정
                    p.pathCount = 1;
                    p.SetPath(0, new[] { apex, left, right });
                    p.offset = Vector2.zero;
                    break;
                }

                default:
                {
                    // 예외 shape은 circle로 처리
                    if (probe is CircleCollider2D c)
                    {
                        probe.transform.position = worldCenter;
                        probe.transform.rotation = Quaternion.identity;
                        c.offset = Vector2.zero;
                        c.radius = Mathf.Max(0f, area.radius);
                    }
                    break;
                }
            }
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("DamageAreaProbes") { hideFlags = HideFlags.HideAndDontSave };
        }

        private static CapsuleCollider2D CreateCapsule(GameObject go)
        {
            var c = go.AddComponent<CapsuleCollider2D>();
            c.direction = CapsuleDirection2D.Vertical;
            return c;
        }

        private static Vector2 Flatten2D(Vector3 v)
        {
            var r = new Vector2(v.x, v.y);
            return r.sqrMagnitude < 1e-6f ? Vector2.up : r.normalized;
        }
    }
}
