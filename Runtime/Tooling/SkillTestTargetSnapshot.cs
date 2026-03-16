#if UNITY_EDITOR
using UnityEngine;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 테스트에서 "선택 몬스터"의 원래 위치/물리 상태를 복원하기 위한 스냅샷입니다.
    /// - 스킬 이동(전진/대시)이나 넉백 등으로 위치가 변해도, 다음 테스트를 동일 조건에서 반복할 수 있게 합니다.
    /// - 툴 목적상, Animator/HP/버프 등의 완전 복원은 포함하지 않고, 위치/물리 중심으로 복원합니다.
    /// </summary>
    public sealed class SkillTestTargetSnapshot
    {
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 LocalScale { get; private set; }

        // Rigidbody2D(있을 경우)
        private bool _hasRb2d;
        private RigidbodyType2D _bodyType;
        private float _gravityScale;
        private Vector2 _velocity;
        private float _angularVelocity;
        private RigidbodyConstraints2D _constraints;
        private bool _simulated;

        public void Capture(GameObject target)
        {
            if (target == null) return;

            var tr = target.transform;
            Position = tr.position;
            Rotation = tr.rotation;
            LocalScale = tr.localScale;

            var rb2d = target.GetComponent<Rigidbody2D>();
            if (rb2d == null)
            {
                _hasRb2d = false;
                return;
            }

            _hasRb2d = true;
            _bodyType = rb2d.bodyType;
            _gravityScale = rb2d.gravityScale;
            _velocity = rb2d.GetLinearVelocity();
            _angularVelocity = rb2d.angularVelocity;
            _constraints = rb2d.constraints;
            _simulated = rb2d.simulated;
        }

        public void Apply(GameObject target)
        {
            if (target == null) return;

            // 진행 중인 모션 이동이 있다면 중단(전진/대시/러시 등)
            var motion = target.GetComponentInParent<ICharacterMotionController>();
            motion?.CancelMotion(MotionChannel.Skill, 999); // 테스트 리셋

            // 물리 중단 후 좌표 복원
            var rb2d = target.GetComponent<Rigidbody2D>();
            if (_hasRb2d && rb2d != null)
            {
                rb2d.SetLinearVelocity(Vector2.zero);
                rb2d.angularVelocity = 0f;

                rb2d.simulated = _simulated;
                rb2d.bodyType = _bodyType;
                rb2d.gravityScale = _gravityScale;
                rb2d.constraints = _constraints;

                // 물리 영향 최소화를 위해 MovePosition 사용(가능한 경우)
                if (rb2d.bodyType == RigidbodyType2D.Kinematic)
                {
                    rb2d.position = Position;
                }
            }

            var tr = target.transform;
            tr.position = Position;
            tr.rotation = Rotation;
            tr.localScale = LocalScale;
        }
    }
}
#endif
