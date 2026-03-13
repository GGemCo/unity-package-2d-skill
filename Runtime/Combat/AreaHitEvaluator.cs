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

        // 데미지 영역을 Collider로 구성하여 HitArea(CapsuleCollider2D)와의 오버랩으로 판정합니다.
        // - shape 별로 Probe Collider를 1개씩 생성/캐시하고, EvaluateTargets 호출 시 Transform/Size만 갱신합니다.
        private readonly DamageAreaProbeCache _probeCache = new DamageAreaProbeCache();
        private ContactFilter2D _attackHitFilter;
        private int _hitAreaLayerMask;

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

            var castCharacterBase = caster.GetComponent<CharacterBase>();
            if (castCharacterBase == null) return;

            // 1) 데미지 영역 Probe Collider 갱신
            Collider2D probe = _probeCache.GetOrCreate(area.shape);
            _probeCache.Configure(probe, area, center, forward, caster);

            // 2) Probe Collider와 겹치는 colliderHitArea(=CapsuleCollider2D) 수집
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _mask,
                useTriggers = true
            };
            
            // Transform/Collider 변경을 물리 엔진에 반영
            Physics2D.SyncTransforms();

            _hitAreaLayerMask = -1;
            if (castCharacterBase.IsPlayer())
            {
                _hitAreaLayerMask = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.HitAreaMonster));
            }
            else if (castCharacterBase.IsMonster())
            {
                _hitAreaLayerMask = LayerMask.GetMask(ConfigLayer.GetValue(ConfigLayer.Keys.HitAreaPlayer));
            }
            _attackHitFilter = CompatPhysics2D.CreateLayerFilter(_hitAreaLayerMask, true);
          
            int count = CompatPhysics2D.OverlapColliderNonAlloc(probe, _attackHitFilter, _buffer);
            for (int i = 0; i < count && results.Count < maxTargets; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;

                // 팀/진영 필터(기존 태그 기반 로직 유지)
                if (castCharacterBase.IsPlayer() && col.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.Player))) continue;
                if (castCharacterBase.IsMonster() && col.CompareTag(ConfigTags.GetValue(ConfigTags.Keys.Monster))) continue;
                
                var targetHitArea = col.GetComponent<CharacterHitArea>();
                if (targetHitArea == null) continue;

                results.Add(targetHitArea.target.gameObject);
            }
        }
    }
}
