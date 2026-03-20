using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 전진(러시, 대시, 회피 등) 이동 이벤트를 정의하는 Authoring용 타임라인 클립입니다.
    /// Bake 과정에서 <see cref="GGemCo2DSkill.LungeEventDefinition"/> Payload로 변환되어
    /// 실제 스킬 실행 시스템에서 사용됩니다.
    /// 
    /// 이동은 속도 기반이 아니라 총 이동 거리(Distance) 기반으로 설계되며
    /// 필요 시 아크(Arc) 모션을 사용해 점프형 이동, 공중 추적(Air Chase)을 표현할 수 있습니다.
    /// </summary>
    [Serializable]
    public sealed class SkillLungeClip : SkillEventClipBase
    {
        [Header("Motion")]
        [Tooltip("총 이동 거리(월드 유닛 기준). Duration과 함께 실제 이동 속도가 결정됩니다.")]
        [SerializeField] private float distance = 2.5f;

        [Tooltip("0보다 크면 타임라인 클립 길이 대신 이 값을 사용합니다. (초 단위)")]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("시간 진행에 따른 거리 보간 방식. Linear, EaseIn, EaseOut 등 이동 감속/가속 패턴을 제어합니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;

        [Header("Resolve")]
        [Tooltip("고정 거리 / 타겟 추적 중 어떤 방식으로 실제 이동 거리를 계산할지 결정합니다.")]
        [SerializeField] private GGemCo2DSkill.SkillLungeResolveMode resolveMode = GGemCo2DSkill.SkillLungeResolveMode.FixedDistance;

        [Tooltip("타겟 추적 허용 최대 거리입니다. 0 이하이면 스킬 CastRange를 사용합니다.")]
        [SerializeField] private float targetResolveRange = -1f;

        [Tooltip("타겟 중심에 완전히 겹치지 않도록 남길 거리입니다.")]
        [SerializeField] private float stopOffset = 0.2f;

        [Tooltip("체크 시 X축 기준으로만 타겟 접근 거리를 계산합니다.")]
        [SerializeField] private bool horizontalOnly = true;

        [Header("Target Anchor")]
        [Tooltip("타겟 추적 시 어떤 기준점을 향해 이동할지 결정합니다.")]
        [SerializeField] private GGemCo2DSkill.SkillLungeTargetAnchorMode targetAnchorMode = GGemCo2DSkill.SkillLungeTargetAnchorMode.TargetTransform;

        [Tooltip("타겟 기준점에 추가로 더할 오프셋입니다.")]
        [SerializeField] private Vector2 targetAnchorOffset = Vector2.zero;

        [Header("Direction")]
        [Tooltip("체크 시 현재 Forward 방향의 반대로 이동합니다. (뒤로 회피/백스텝 구현용)")]
        [SerializeField] private bool invertForward = false;

        [Header("Arc")]
        [Tooltip("활성화 시 수직 아크 모션을 추가합니다. 점프형 회피/도약 이동, 공중 추적에 사용됩니다.")]
        [SerializeField] private bool useArcMotion = false;

        [Tooltip("아크의 최고 높이(월드 유닛). useArcMotion이 활성화되어야 적용됩니다.")]
        [SerializeField] private float arcHeight = 0f;

        [Tooltip("Arc 구현 모드입니다. Air Chase는 DistancePhased 권장입니다.")]
        [SerializeField] private MotionArcMode arcMode = MotionArcMode.LegacyTimeSine;

        [Tooltip("DistancePhased Arc에서 상승 구간 easing입니다.")]
        [SerializeField] private Easing.EaseType arcRiseEase = GGemCo2DCore.Easing.EaseType.EaseOutQuad;

        [Tooltip("DistancePhased Arc에서 하강 구간 easing입니다.")]
        [SerializeField] private Easing.EaseType arcFallEase = GGemCo2DCore.Easing.EaseType.EaseInQuad;

        [Range(0f, 1f)]
        [Tooltip("정점 유지 구간 폭(정규화 0..1). 0이면 즉시 하강합니다.")]
        [SerializeField] private float arcApexHoldNormalized = 0f;

        [Header("End Position")]
        [Tooltip("이동 종료 시 Y 좌표를 어떻게 보정할지 결정합니다.")]
        [SerializeField] private GGemCo2DSkill.SkillLungeEndYMode endYMode = GGemCo2DSkill.SkillLungeEndYMode.None;

        [Tooltip("EndYMode 적용 후 추가 Y 오프셋입니다.")]
        [SerializeField] private float endYOffset = 0f;

        [Tooltip("GroundAtEndX일 때 레이캐스트 시작 높이입니다.")]
        [SerializeField] private float groundProbeHeight = 2f;

        [Tooltip("GroundAtEndX일 때 아래 방향 탐색 거리입니다.")]
        [SerializeField] private float groundProbeDistance = 8f;

        [Header("Rigidbody2D")]
        [Tooltip("모션 종료 시 Rigidbody2D의 속도를 0으로 초기화합니다.")]
        [SerializeField] private bool stopAtEnd = true;

        [Tooltip("Kinematic Rigidbody2D에서 MovePosition을 사용하여 이동합니다. 권장 옵션입니다.")]
        [SerializeField] private bool useMovePosition = true;

        [Tooltip("모션 시작 시 Forward 방향을 고정합니다. 해제 시 이동 중 Forward 변경을 반영합니다.")]
        [SerializeField] private bool useSnapshotForward = true;

        [Header("Policy")]
        [Tooltip("같은 채널의 기존 모션이 실행 중이어도 덮어쓰기를 허용합니다.")]
        [SerializeField] private bool allowReplace = false;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;
        public float Distance => distance;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public Easing.EaseType Easing => easing;
        public GGemCo2DSkill.SkillLungeResolveMode ResolveMode => resolveMode;
        public float TargetResolveRange => targetResolveRange;
        public float StopOffset => stopOffset;
        public bool HorizontalOnly => horizontalOnly;
        public GGemCo2DSkill.SkillLungeTargetAnchorMode TargetAnchorMode => targetAnchorMode;
        public Vector2 TargetAnchorOffset => targetAnchorOffset;
        public bool InvertForward => invertForward;
        public bool UseArcMotion => useArcMotion;
        public float ArcHeight => arcHeight;
        public MotionArcMode ArcMode => arcMode;
        public Easing.EaseType ArcRiseEase => arcRiseEase;
        public Easing.EaseType ArcFallEase => arcFallEase;
        public float ArcApexHoldNormalized => arcApexHoldNormalized;
        public GGemCo2DSkill.SkillLungeEndYMode EndYMode => endYMode;
        public float EndYOffset => endYOffset;
        public float GroundProbeHeight => groundProbeHeight;
        public float GroundProbeDistance => groundProbeDistance;
        public bool StopAtEnd => stopAtEnd;
        public bool UseMovePosition => useMovePosition;
        public bool UseSnapshotForward => useSnapshotForward;
        public bool AllowReplace => allowReplace;
    }
}
