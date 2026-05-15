using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트가 시작한 캐릭터 잔상 재생과 종료 시 정리 정책을 관리합니다.
    /// </summary>
    internal sealed class SkillAfterimageController
    {
        /// <summary>
        /// 스킬 종료 시 정리해야 할 잔상 트레일과 정리 정책을 보관합니다.
        /// </summary>
        private readonly List<CleanupEntry> _cleanupEntries = new();

        private struct CleanupEntry
        {
            public CharacterAfterimageTrail Trail;
            public bool ClearOnSkillEnd;
            public bool ClearOnCancel;
        }

        /// <summary>
        /// 캐릭터 잔상 이벤트 정의를 해석하여 대상 캐릭터의 잔상 컴포넌트에 전달합니다.
        /// </summary>
        /// <param name="targetObject">잔상 효과를 적용할 대상 GameObject입니다.</param>
        /// <param name="payloadObj">Bake된 캐릭터 잔상 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public void Play(GameObject targetObject, Object payloadObj, float eventDurationSeconds)
        {
            if (targetObject == null)
                return;

            if (payloadObj is not SkillAfterimageEventDefinition def)
                return;

            var trail = targetObject.GetComponentInChildren<CharacterAfterimageTrail>(true);
            if (trail == null)
            {
                if (def.missingActorPolicy == DummyMissingActorPolicy.Warn)
                    Debug.LogWarning($"[SkillAfterimageController] CharacterAfterimageTrail not found. target={targetObject.name}");
                return;
            }

            switch (def.mode)
            {
                case SkillAfterimageMode.Snapshot:
                    CaptureSnapshot(trail, def);
                    break;
                case SkillAfterimageMode.Stop:
                    trail.StopTrail();
                    Unregister(trail);
                    break;
                case SkillAfterimageMode.Trail:
                default:
                    StartTrail(trail, def, eventDurationSeconds);
                    break;
            }
        }

        /// <summary>
        /// 등록된 잔상 트레일을 스킬 종료 사유에 맞게 정리합니다.
        /// </summary>
        /// <param name="forCancel">취소 종료이면 <see langword="true"/>, 정상 종료이면 <see langword="false"/>입니다.</param>
        public void Cleanup(bool forCancel)
        {
            for (int i = _cleanupEntries.Count - 1; i >= 0; i--)
            {
                var entry = _cleanupEntries[i];
                var trail = entry.Trail;
                bool shouldClear = forCancel ? entry.ClearOnCancel : entry.ClearOnSkillEnd;

                if (trail != null && shouldClear)
                    trail.StopTrail();
            }

            _cleanupEntries.Clear();
        }

        /// <summary>
        /// 스킬 실행 전에 이전 실행에서 남은 정리 예약 정보를 초기화합니다.
        /// </summary>
        public void ResetCleanupFlags()
        {
            _cleanupEntries.Clear();
        }

        /// <summary>
        /// 트레일 모드 잔상 생성을 시작합니다.
        /// </summary>
        /// <param name="trail">실행 대상 잔상 컴포넌트입니다.</param>
        /// <param name="def">잔상 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        private void StartTrail(CharacterAfterimageTrail trail, SkillAfterimageEventDefinition def, float eventDurationSeconds)
        {
            float duration = def.ResolveDuration(eventDurationSeconds);
            var settings = new StruckAnimationEventBackstepTrail
            {
                DurationSeconds = duration,
                SpawnIntervalSeconds = Mathf.Max(0.005f, def.spawnIntervalSeconds),
                GhostLifetimeSeconds = Mathf.Max(0.01f, def.ghostLifetimeSeconds),
                ColorHex = ColorUtility.ToHtmlStringRGBA(def.ghostColor),
                SortingOrderOffset = def.sortingOrderOffset,
            };

            trail.StartTrail(settings);
            Register(trail, def.clearOnSkillEnd, def.clearOnCancel);
        }

        /// <summary>
        /// 단발 스냅샷 잔상을 생성합니다.
        /// </summary>
        /// <param name="trail">실행 대상 잔상 컴포넌트입니다.</param>
        /// <param name="def">잔상 이벤트 정의입니다.</param>
        private static void CaptureSnapshot(CharacterAfterimageTrail trail, SkillAfterimageEventDefinition def)
        {
            var settings = new StruckAnimationEventAfterimageSnapshot
            {
                GhostLifetimeSeconds = Mathf.Max(0.01f, def.ghostLifetimeSeconds),
                ColorHex = ColorUtility.ToHtmlStringRGBA(def.ghostColor),
                Alpha = Mathf.Clamp01(def.ghostColor.a),
                SortingOrderOffset = def.sortingOrderOffset,
            };

            trail.CaptureOnce(settings);
        }

        /// <summary>
        /// 스킬 종료 시 정리가 필요한 트레일을 등록하거나 기존 등록 정보를 갱신합니다.
        /// </summary>
        /// <param name="trail">정리 대상 잔상 컴포넌트입니다.</param>
        /// <param name="clearOnSkillEnd">정상 종료 시 정리 여부입니다.</param>
        /// <param name="clearOnCancel">취소 종료 시 정리 여부입니다.</param>
        private void Register(CharacterAfterimageTrail trail, bool clearOnSkillEnd, bool clearOnCancel)
        {
            if (trail == null)
                return;

            if (!clearOnSkillEnd && !clearOnCancel)
                return;

            for (int i = 0; i < _cleanupEntries.Count; i++)
            {
                if (_cleanupEntries[i].Trail != trail)
                    continue;

                var entry = _cleanupEntries[i];
                entry.ClearOnSkillEnd |= clearOnSkillEnd;
                entry.ClearOnCancel |= clearOnCancel;
                _cleanupEntries[i] = entry;
                return;
            }

            _cleanupEntries.Add(new CleanupEntry
            {
                Trail = trail,
                ClearOnSkillEnd = clearOnSkillEnd,
                ClearOnCancel = clearOnCancel,
            });
        }

        /// <summary>
        /// 지정한 잔상 컴포넌트의 정리 예약 정보를 제거합니다.
        /// </summary>
        /// <param name="trail">정리 예약에서 제거할 잔상 컴포넌트입니다.</param>
        private void Unregister(CharacterAfterimageTrail trail)
        {
            if (trail == null)
                return;

            for (int i = _cleanupEntries.Count - 1; i >= 0; i--)
            {
                if (_cleanupEntries[i].Trail == trail)
                    _cleanupEntries.RemoveAt(i);
            }
        }
    }
}
