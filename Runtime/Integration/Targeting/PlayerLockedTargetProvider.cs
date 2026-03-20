using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어의 명시적 락온 타겟을 유지/전환하는 기본 구현입니다.
    /// <para>
    /// - <see cref="IPlayerLockedTargetProvider"/>를 통해 <see cref="PlayerSkillTargetingProvider"/>가 현재 락온 타겟을 우선 사용합니다.
    /// - 추후 UI 마커는 <see cref="LockedTargetChanged"/> 이벤트와 <see cref="CurrentLockedTarget"/>를 사용해 연결할 수 있습니다.
    /// - 입력 바인딩은 강제하지 않고, 다른 UI/입력 레이어에서 <see cref="TryToggleNearestTarget"/>, <see cref="TryCycleLockedTarget"/>, <see cref="ClearLockedTarget"/>를 호출하는 방식으로 확장합니다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLockedTargetProvider : MonoBehaviour, IPlayerLockedTargetProvider
    {
        [Header("Search")]
        [SerializeField, Tooltip("락온 대상 탐색 최대 거리입니다.")]
        private float searchRange = 12f;

        [SerializeField, Tooltip("전환/탐색 시 사용하는 전방 cone 각도(도)입니다.")]
        private float searchConeAngle = 140f;

        [SerializeField, Tooltip("이미 락온된 타겟이 유효하면 다시 가장 가까운 대상으로 바꾸지 않고 유지합니다.")]
        private bool keepCurrentTargetIfValid = true;

        [Header("Validation")]
        [SerializeField, Tooltip("이 거리보다 멀어지면 현재 락온을 자동 해제합니다. 0 이하면 searchRange를 사용합니다.")]
        private float maxLockDistance = 14f;

        [SerializeField, Tooltip("현재 락온 타겟이 시야 cone 밖으로 벗어나도 유지할지 여부입니다.")]
        private bool keepTargetOutsideCone = true;

        [SerializeField, Tooltip("락온 타겟 기준 앵커 위치를 계산할 때 더해 줄 Y 오프셋입니다.")]
        private float targetAnchorOffsetY = 0.25f;

        private CharacterBase _owner;
        private Transform _lockedTarget;

        /// <summary>
        /// 현재 락온된 타겟입니다. UI 마커가 구독할 수 있도록 공개합니다.
        /// </summary>
        public Transform CurrentLockedTarget => _lockedTarget;

        /// <summary>
        /// 락온 타겟 변경 시 발생합니다. UI 마커, 타겟 이름판 등 외부 표현 계층에서 사용할 수 있습니다.
        /// </summary>
        public event Action<Transform> LockedTargetChanged;

        private void Awake()
        {
            _owner = GetComponent<CharacterBase>();
        }

        private void Update()
        {
            RefreshLockedTarget();
        }

        /// <summary>
        /// 현재 유효한 락온 타겟이 있으면 반환합니다.
        /// </summary>
        public bool TryGetLockedTarget(GameObject caster, out Transform lockedTarget)
        {
            RefreshLockedTarget();
            lockedTarget = _lockedTarget;
            return lockedTarget != null;
        }

        /// <summary>
        /// 현재 락온이 없으면 가장 적절한 후보를 락온하고,
        /// 이미 같은 타겟이 잡혀 있으면 해제합니다.
        /// </summary>
        public bool TryToggleNearestTarget()
        {
            if (TryAcquireBestTarget(out var candidate))
            {
                if (candidate == _lockedTarget)
                {
                    ClearLockedTarget();
                    return false;
                }

                SetLockedTarget(candidate);
                return true;
            }

            ClearLockedTarget();
            return false;
        }

        /// <summary>
        /// 현재 락온을 해제합니다.
        /// </summary>
        public void ClearLockedTarget()
        {
            SetLockedTarget(null);
        }

        /// <summary>
        /// 외부 UI/입력 레이어에서 직접 타겟을 지정할 수 있도록 제공합니다.
        /// </summary>
        public bool TrySetLockedTarget(Transform candidate)
        {
            if (!IsValidTarget(candidate))
                return false;

            SetLockedTarget(candidate);
            return true;
        }

        /// <summary>
        /// 현재 락온을 기준으로 좌/우 또는 전/후 순서로 타겟을 전환합니다.
        /// direction이 1이면 다음 후보, -1이면 이전 후보를 선택합니다.
        /// </summary>
        public bool TryCycleLockedTarget(int direction = 1)
        {
            if (_owner == null)
                return false;

            var candidates = CollectOrderedCandidates();
            if (candidates.Count == 0)
            {
                ClearLockedTarget();
                return false;
            }

            if (candidates.Count == 1)
            {
                SetLockedTarget(candidates[0].transform);
                return true;
            }

            int currentIndex = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].transform == _lockedTarget)
                {
                    currentIndex = i;
                    break;
                }
            }

            int step = direction >= 0 ? 1 : -1;
            int nextIndex;
            if (currentIndex < 0)
            {
                nextIndex = step > 0 ? 0 : candidates.Count - 1;
            }
            else
            {
                nextIndex = (currentIndex + step + candidates.Count) % candidates.Count;
            }

            SetLockedTarget(candidates[nextIndex].transform);
            return true;
        }

        /// <summary>
        /// UI 마커가 사용할 월드 앵커 위치를 반환합니다.
        /// </summary>
        public bool TryGetLockedTargetAnchorWorldPosition(out Vector3 position)
        {
            position = default;
            if (_lockedTarget == null)
                return false;

            position = _lockedTarget.position;
            var character = _lockedTarget.GetComponent<CharacterBase>();
            if (character != null)
                position.y += character.GetHeightByScale() + targetAnchorOffsetY;
            else
                position.y += targetAnchorOffsetY;

            return true;
        }

        /// <summary>
        /// 현재 락온 타겟의 유효성을 갱신하고, 무효화되었으면 자동 해제합니다.
        /// </summary>
        public void RefreshLockedTarget()
        {
            if (_lockedTarget == null)
                return;

            if (!IsValidTarget(_lockedTarget))
            {
                ClearLockedTarget();
                return;
            }

            if (!IsWithinLockDistance(_lockedTarget))
            {
                ClearLockedTarget();
                return;
            }

            if (!keepTargetOutsideCone && !IsWithinSearchCone(_lockedTarget.position))
            {
                ClearLockedTarget();
            }
        }

        private bool TryAcquireBestTarget(out Transform target)
        {
            target = null;

            if (keepCurrentTargetIfValid && _lockedTarget != null && IsValidTarget(_lockedTarget) && IsWithinLockDistance(_lockedTarget))
            {
                target = _lockedTarget;
                return true;
            }

            var candidates = CollectOrderedCandidates();
            if (candidates.Count <= 0)
                return false;

            target = candidates[0].transform;
            return true;
        }

        private List<CharacterBase> CollectOrderedCandidates()
        {
            var result = new List<CharacterBase>(16);
            if (_owner == null)
                return result;

            var ownerPosition = (Vector2)transform.position;
            var forward = ResolveOwnerForward();
            float range = Mathf.Max(0.01f, searchRange);
            float halfAngle = Mathf.Max(0f, searchConeAngle) * 0.5f;
            float cosThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

            var allCharacters = Object.FindObjectsOfType<CharacterBase>();
            for (int i = 0; i < allCharacters.Length; i++)
            {
                var candidate = allCharacters[i];
                if (!IsValidCandidate(candidate))
                    continue;

                Vector2 toTarget = (Vector2)candidate.transform.position - ownerPosition;
                float horizontalDistance = Mathf.Abs(toTarget.x);
                if (horizontalDistance <= 1e-6f || horizontalDistance > range)
                    continue;

                Vector2 directionToTarget = ResolveHorizontalDirection(toTarget, forward);
                float alignment = Vector2.Dot(forward, directionToTarget);
                if (alignment < cosThreshold)
                    continue;

                result.Add(candidate);
            }

            result.Sort(CompareCandidates);
            return result;
        }

        private int CompareCandidates(CharacterBase a, CharacterBase b)
        {
            if (a == b)
                return 0;

            Vector2 ownerPosition = transform.position;
            Vector2 forward = ResolveOwnerForward();

            float scoreA = CalculateCandidateScore(ownerPosition, forward, a);
            float scoreB = CalculateCandidateScore(ownerPosition, forward, b);
            int compare = scoreB.CompareTo(scoreA);
            if (compare != 0)
                return compare;

            float angleA = CalculateSignedAngle(ownerPosition, forward, a.transform.position);
            float angleB = CalculateSignedAngle(ownerPosition, forward, b.transform.position);
            compare = angleA.CompareTo(angleB);
            if (compare != 0)
                return compare;

            float distA = Mathf.Abs(a.transform.position.x - ownerPosition.x);
            float distB = Mathf.Abs(b.transform.position.x - ownerPosition.x);
            return distA.CompareTo(distB);
        }

        private static float CalculateCandidateScore(Vector2 ownerPosition, Vector2 forward, CharacterBase candidate)
        {
            Vector2 toTarget = (Vector2)candidate.transform.position - ownerPosition;
            float horizontalDistance = Mathf.Abs(toTarget.x);
            if (horizontalDistance < 1e-6f)
                return float.MinValue;

            float alignment = Vector2.Dot(forward, ResolveHorizontalDirection(toTarget, forward));
            return alignment * 1000f - horizontalDistance;
        }

        private static float CalculateSignedAngle(Vector2 ownerPosition, Vector2 forward, Vector3 targetPosition)
        {
            Vector2 toTarget = (Vector2)targetPosition - ownerPosition;
            if (toTarget.sqrMagnitude < 1e-6f)
                return 0f;

            return Vector2.SignedAngle(forward, ResolveHorizontalDirection(toTarget, forward));
        }

        private bool IsValidTarget(Transform candidate)
        {
            if (candidate == null || candidate == transform)
                return false;

            var character = candidate.GetComponent<CharacterBase>();
            return IsValidCandidate(character);
        }

        private bool IsValidCandidate(CharacterBase candidate)
        {
            if (_owner == null || candidate == null)
                return false;

            if (candidate == _owner || candidate.gameObject == gameObject)
                return false;

            if (!candidate.gameObject.activeInHierarchy || candidate.IsStatusDead())
                return false;

            if (_owner.IsPlayer())
                return candidate.IsMonster();

            if (_owner.IsMonster())
                return candidate.IsPlayer();

            return false;
        }

        private bool IsWithinLockDistance(Transform target)
        {
            float range = maxLockDistance > 0f ? maxLockDistance : searchRange;
            if (range <= 0f)
                return true;

            return Mathf.Abs(target.position.x - transform.position.x) <= range;
        }

        private bool IsWithinSearchCone(Vector3 targetPosition)
        {
            float halfAngle = Mathf.Max(0f, searchConeAngle) * 0.5f;
            if (halfAngle >= 180f)
                return true;

            Vector2 forward = ResolveOwnerForward();
            Vector2 toTarget = (Vector2)(targetPosition - transform.position);
            if (toTarget.sqrMagnitude < 1e-6f)
                return true;

            float alignment = Vector2.Dot(forward, ResolveHorizontalDirection(toTarget, forward));
            float cosThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            return alignment >= cosThreshold;
        }

        private Vector2 ResolveOwnerForward()
        {
            if (_owner != null)
            {
                Vector2 facing = CharacterConstants.FacingToVector2(_owner.CurrentFacing);
                if (facing.sqrMagnitude > 1e-6f)
                    return facing.normalized;
            }

            Vector3 right = transform.right;
            Vector2 fallback = new Vector2(right.x, right.y);
            return fallback.sqrMagnitude < 1e-6f ? Vector2.right : fallback.normalized;
        }

        private static Vector2 ResolveHorizontalDirection(Vector2 toTarget, Vector2 fallback)
        {
            float sign = Mathf.Sign(toTarget.x);
            if (Mathf.Abs(sign) > 1e-6f)
                return new Vector2(sign, 0f);

            return fallback.sqrMagnitude < 1e-6f ? Vector2.right : fallback.normalized;
        }

        private void SetLockedTarget(Transform newTarget)
        {
            if (_lockedTarget == newTarget)
                return;

            _lockedTarget = newTarget;
            LockedTargetChanged?.Invoke(_lockedTarget);
        }
    }
}
