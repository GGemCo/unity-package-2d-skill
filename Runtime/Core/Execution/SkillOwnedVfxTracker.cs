using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 실행 중 생성되어 실행기가 소유하는 VFX 인스턴스를 추적하고 정리합니다.
    /// </summary>
    internal sealed class SkillOwnedVfxTracker
    {
        /// <summary>
        /// VFX 인스턴스와 해당 생성 시점의 종료 콜백을 함께 보관하는 추적 항목입니다.
        /// </summary>
        private sealed class TrackingEntry
        {
            private SkillOwnedVfxTracker _owner;

            /// <summary>
            /// 추적할 VFX와 소유 추적기를 연결합니다.
            /// </summary>
            /// <param name="owner">VFX를 등록한 소유 추적기입니다.</param>
            /// <param name="vfx">추적할 VFX 인스턴스입니다.</param>
            public TrackingEntry(SkillOwnedVfxTracker owner, VfxBehaviourBase vfx)
            {
                _owner = owner;
                Vfx = vfx;
            }

            /// <summary>
            /// 현재 등록 시점에 추적 중인 VFX 인스턴스입니다.
            /// </summary>
            public VfxBehaviourBase Vfx { get; private set; }

            /// <summary>
            /// VFX가 자연 종료되거나 풀로 반환되기 직전에 현재 등록 항목을 해제합니다.
            /// </summary>
            public void HandleVfxDestroy()
            {
                SkillOwnedVfxTracker owner = _owner;
                Clear();
                owner?.Remove(this);
            }

            /// <summary>
            /// 종료 콜백 구독을 해제하고 보유 참조를 비웁니다.
            /// </summary>
            public void Detach()
            {
                VfxBehaviourBase vfx = Vfx;
                if (vfx != null)
                    vfx.OnVfxDestroy -= HandleVfxDestroy;

                Clear();
            }

            /// <summary>
            /// 추적 항목이 풀에서 재사용된 VFX를 계속 소유하지 않도록 내부 참조를 초기화합니다.
            /// </summary>
            private void Clear()
            {
                _owner = null;
                Vfx = null;
            }
        }

        /// <summary>
        /// 현재 스킬 실행에서 생성한 취소 가능 VFX의 등록 항목 목록입니다.
        /// </summary>
        private readonly List<TrackingEntry> _trackingEntries = new();

        /// <summary>
        /// 정리 도중 목록 변경을 방지하면서 VFX 참조를 임시 보관하는 재사용 버퍼입니다.
        /// </summary>
        private readonly List<VfxBehaviourBase> _cleanupBuffer = new();

        /// <summary>
        /// 실행기가 소유해야 하는 VFX를 추적 목록에 등록합니다.
        /// </summary>
        /// <param name="vfx">추적할 VFX 인스턴스입니다.</param>
        public void Register(VfxBehaviourBase vfx)
        {
            if (vfx == null)
                return;

            RemoveInvalidEntries();

            // 동일 생성 인스턴스를 중복 등록하면 종료 콜백과 정리 요청도 중복되므로 한 번만 추적합니다.
            for (int i = 0; i < _trackingEntries.Count; i++)
            {
                if (_trackingEntries[i].Vfx == vfx)
                    return;
            }

            TrackingEntry entry = new TrackingEntry(this, vfx);
            _trackingEntries.Add(entry);
            vfx.OnVfxDestroy += entry.HandleVfxDestroy;
        }

        /// <summary>
        /// 추적 중인 VFX를 Core VFX 수명주기로 종료하고 목록을 비웁니다.
        /// </summary>
        public void Cleanup()
        {
            if (_trackingEntries.Count == 0)
                return;

            _cleanupBuffer.Clear();

            // DestroyForce가 종료 콜백을 동기적으로 호출할 수 있으므로 먼저 구독과 추적 목록을 분리합니다.
            for (int i = _trackingEntries.Count - 1; i >= 0; i--)
            {
                TrackingEntry entry = _trackingEntries[i];
                VfxBehaviourBase vfx = entry.Vfx;
                entry.Detach();

                if (vfx != null)
                    _cleanupBuffer.Add(vfx);
            }

            _trackingEntries.Clear();

            // 직접 Destroy하지 않고 Core의 반환 경로를 사용해야 VFX 풀이 인스턴스 수명을 정상 관리할 수 있습니다.
            for (int i = 0; i < _cleanupBuffer.Count; i++)
            {
                VfxBehaviourBase vfx = _cleanupBuffer[i];
                if (vfx != null)
                    vfx.DestroyForce();
            }

            _cleanupBuffer.Clear();
        }

        /// <summary>
        /// 자연 종료된 VFX의 등록 항목을 현재 추적 목록에서 제거합니다.
        /// </summary>
        /// <param name="entry">종료 콜백을 전달한 등록 항목입니다.</param>
        private void Remove(TrackingEntry entry)
        {
            if (entry == null)
                return;

            _trackingEntries.Remove(entry);
        }

        /// <summary>
        /// 외부에서 파괴되어 Unity null 상태가 된 VFX 등록 항목을 제거합니다.
        /// </summary>
        private void RemoveInvalidEntries()
        {
            for (int i = _trackingEntries.Count - 1; i >= 0; i--)
            {
                TrackingEntry entry = _trackingEntries[i];
                if (entry != null && entry.Vfx != null)
                    continue;

                entry?.Detach();
                _trackingEntries.RemoveAt(i);
            }
        }
    }
}
