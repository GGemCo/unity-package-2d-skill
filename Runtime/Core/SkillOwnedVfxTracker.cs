using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 실행 중 생성되어 실행기가 소유하는 VFX 인스턴스를 추적하고 정리합니다.
    /// </summary>
    internal sealed class SkillOwnedVfxTracker
    {
        /// <summary>
        /// 현재 스킬 실행에서 생성한 취소 가능 VFX 목록입니다.
        /// </summary>
        private readonly List<VfxBehaviourBase> _spawnedVfxs = new();

        /// <summary>
        /// 실행기가 소유해야 하는 VFX를 추적 목록에 등록합니다.
        /// </summary>
        /// <param name="vfx">추적할 VFX 인스턴스입니다.</param>
        public void Register(VfxBehaviourBase vfx)
        {
            if (vfx == null)
                return;

            _spawnedVfxs.RemoveAll(x => x == null);
            _spawnedVfxs.Add(vfx);
        }

        /// <summary>
        /// 추적 중인 VFX를 모두 파괴하고 목록을 비웁니다.
        /// </summary>
        public void Cleanup()
        {
            if (_spawnedVfxs.Count == 0)
                return;

            for (int i = _spawnedVfxs.Count - 1; i >= 0; i--)
            {
                VfxBehaviourBase vfx = _spawnedVfxs[i];
                if (vfx != null)
                {
                    Object.Destroy(vfx.gameObject);
                }
            }

            _spawnedVfxs.Clear();
        }
    }
}
