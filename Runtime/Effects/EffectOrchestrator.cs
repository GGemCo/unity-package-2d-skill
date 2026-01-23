using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    public sealed class EffectOrchestrator
    {
        public void Play(EffectEventDefinition def, GameObject caster, GameObject target, Vector3 worldPoint)
        {
            if (def == null || def.effectUid <= 0) return;

            Transform parent = null;
            Vector3 pos = worldPoint;

            if (def.attachToTarget && target != null)
            {
                parent = target.transform;
                pos = parent.position;
            }
            else if (caster != null)
            {
                pos = caster.transform.position;
            }

            var go = SceneGame.Instance.EffectManager.CreateEffect(def.effectUid);
            if (def.localOffset != Vector3.zero)
            {
                go.transform.localPosition += def.localOffset;
            }

            if (def.lifetimeSeconds > 0f)
                Object.Destroy(go, def.lifetimeSeconds);
        }
    }
}