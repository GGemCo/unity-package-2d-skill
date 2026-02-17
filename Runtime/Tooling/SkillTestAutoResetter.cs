#if UNITY_EDITOR
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// SkillExecutor의 진행 상태를 감시하여, 스킬 종료 시 선택 몬스터를 원래 위치로 복원합니다.
    /// - Skill 패키지가 Core/BT에 과도하게 의존하지 않도록, "폴링" 방식으로 구현합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillTestAutoResetter : MonoBehaviour
    {
        [SerializeField] private bool autoResetAfterSkill = true;

        private SkillTestTargetSnapshot _snapshot;
        private GGemCo2DSkill.SkillExecutor _executor;
        private bool _wasBusy;

        public void SetAutoReset(bool enabled) => autoResetAfterSkill = enabled;

        public void BindSnapshot(SkillTestTargetSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        private void Awake()
        {
            _executor = GetComponent<GGemCo2DSkill.SkillExecutor>();
        }

        private void Update()
        {
            if (!autoResetAfterSkill) return;
            if (_executor == null || _snapshot == null) return;

            bool busy = _executor.IsBusy;
            if (busy)
            {
                _wasBusy = true;
                return;
            }

            // Busy -> Idle 전환 감지(스킬 1회 종료)
            if (_wasBusy)
            {
                _wasBusy = false;
                _snapshot.Apply(gameObject);
            }
        }
    }
}
#endif
