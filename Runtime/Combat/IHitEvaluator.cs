using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    public interface IHitEvaluator
    {
        void EvaluateTargets(
            Vector3 center,
            Vector3 forward,
            SkillAreaSpec area,
            float range,
            int maxTargets,
            GameObject caster,
            List<GameObject> results);
    }
}