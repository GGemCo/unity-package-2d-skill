using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 전진(러시/대시) 이벤트 클립(Authoring).
    /// - Bake 시 <see cref="GGemCo2DSkill.LungeEventDefinition"/> Payload 로 변환된다.
    /// - Speed가 아니라 "거리(Distance)" 기반으로 설계한다.
    /// </summary>
    [Serializable]
    public sealed class SkillLungeClip : SkillEventClipBase
    {
        [SerializeField] private float distance = 2.5f;
        [SerializeField] private float durationOverrideSeconds = 0f;
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;
        [SerializeField] private bool stopAtEnd = true;
        [SerializeField] private bool useMovePosition = true;
        [SerializeField] private bool useSnapshotForward = true;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;

        public float Distance => distance;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public Easing.EaseType Easing => easing;
        public bool StopAtEnd => stopAtEnd;
        public bool UseMovePosition => useMovePosition;
        public bool UseSnapshotForward => useSnapshotForward;
    }
}
