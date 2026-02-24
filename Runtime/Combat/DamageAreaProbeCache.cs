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
    /// <summary>
    /// SkillAreaSpec(데미지 영역)를 2D Collider로 구성하기 위한 Probe 캐시입니다.
    ///
    /// 에디터(PlayMode 테스트)에서는 동일한 설정 로직을 Gizmo에도 재사용할 수 있도록 public 으로 제공합니다.
    /// </summary>
    public sealed class DamageAreaProbeCache
    {
        private readonly Dictionary<ConfigCommonSkill.SkillAreaShape, Collider2D> _probes = new();
        private GameObject _root;

#if UNITY_EDITOR
        // 에디터에서 Gizmo 표시 등으로 동시에 여러 영역을 시각화할 수 있도록,
        // shape별 Probe 풀을 별도로 제공합니다.
        // (런타임 판정은 shape당 1개 캐시를 재사용해도 되지만, Gizmo는 동시 표시가 필요합니다.)
        private readonly Dictionary<ConfigCommonSkill.SkillAreaShape, Stack<Collider2D>> _gizmoPools = new();
#endif

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

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 Gizmo 등에서 동시에 여러 영역을 표시하기 위한 Probe를 획득합니다.
        /// - shape별로 풀링하여 GC/Instantiate 비용을 줄입니다.
        /// - 반환된 Probe는 사용이 끝나면 <see cref="ReleaseForGizmo"/>로 되돌려야 합니다.
        /// </summary>
        public Collider2D AcquireForGizmo(ConfigCommonSkill.SkillAreaShape shape)
        {
            EnsureRoot();

            if (_gizmoPools.TryGetValue(shape, out var pool))
            {
                while (pool.Count > 0)
                {
                    var col = pool.Pop();
                    if (col != null)
                    {
                        col.gameObject.SetActive(true);
                        return col;
                    }
                }
            }

            // 풀에 없으면 새로 생성
            var go = new GameObject($"DamageAreaProbe_Gizmo_{shape}")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            go.transform.SetParent(_root.transform, false);

            Collider2D created = shape switch
            {
                ConfigCommonSkill.SkillAreaShape.Circle => go.AddComponent<CircleCollider2D>(),
                ConfigCommonSkill.SkillAreaShape.Box => go.AddComponent<BoxCollider2D>(),
                ConfigCommonSkill.SkillAreaShape.Line => go.AddComponent<BoxCollider2D>(),
                ConfigCommonSkill.SkillAreaShape.Capsule => CreateCapsule(go),
                ConfigCommonSkill.SkillAreaShape.Cone => go.AddComponent<PolygonCollider2D>(),
                _ => go.AddComponent<CircleCollider2D>()
            };

            created.isTrigger = true;
            return created;
        }

        /// <summary>
        /// <see cref="AcquireForGizmo"/>로 획득한 Probe를 풀에 반환합니다.
        /// </summary>
        public void ReleaseForGizmo(ConfigCommonSkill.SkillAreaShape shape, Collider2D probe)
        {
            if (probe == null) return;

            probe.gameObject.SetActive(false);
            probe.transform.SetParent(_root != null ? _root.transform : null, false);

            if (!_gizmoPools.TryGetValue(shape, out var pool))
            {
                pool = new Stack<Collider2D>(8);
                _gizmoPools.Add(shape, pool);
            }

            pool.Push(probe);
        }
#endif

        public void Configure(Collider2D probe, in SkillAreaSpec area, Vector3 center, Vector3 forward,
            GameObject caster)
        {
            if (probe == null) return;

            // area.EnsureSaneDefaults();

            // localOffset 해석 규칙(2D 횡스크롤 기준):
            // - area.localOffset.x : 전방(forward) 방향으로의 오프셋(= 캐릭터가 바라보는 방향으로 +)
            // - area.localOffset.y : 월드 Up(+Y) 방향 오프셋
            //
            // 따라서 오프셋은 forward/Up 기저로 월드로 투영하여 적용합니다.
            // (forward가 ResolveForward2D로 이미 Flip을 반영하므로 별도의 localScale 미러링은 하지 않습니다.)
            var f2 = Flatten2D(forward);
            var fwd3 = new Vector3(f2.x, f2.y, 0f);

            var localOffset = area.localOffset;
            var worldCenter = center
                              + fwd3 * localOffset.x
                              + Vector3.up * localOffset.y;

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
                    probe.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
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
                    c.direction = area.capsuleDirection;
                    c.size = new Vector2(Mathf.Max(0f, area.radius), Mathf.Max(0f, area.length));
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
            _root = new GameObject("DamageAreaProbes")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
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