using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 직선 런지(대시/백스텝) 이벤트를 정의하는 타임라인 Authoring 클립입니다.
    /// Bake 시점에 <see cref="GGemCo2DSkill.LungeEventDefinition"/>으로 변환되어 런타임에서 실행됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillLungeClip : SkillEventClipBase
    {
        [Header("Actor")]
        [Tooltip("런지를 수행할 캐릭터 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        [SerializeField] private DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Caster;

        [Tooltip("actorReferenceType이 Actor일 때 사용할 더미 캐릭터 식별 키입니다.")]
        [SerializeField] private string actorKey = "dummy_1";

        [Tooltip("actorKey에 해당하는 더미 캐릭터를 찾지 못했을 때 처리 정책입니다.")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        [Header("Motion")]
        [Tooltip("총 이동 거리(월드 단위)입니다. Duration과 함께 실제 체감 속도가 결정됩니다.")]
        [SerializeField] private float distance = 2.5f;

        [Tooltip("0보다 크면 타임라인 클립 길이 대신 이 값을 이동 시간(초)으로 사용합니다.")]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("시간 진행에 따른 거리 보간(Easing) 방식입니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;

        [Header("Resolve")]
        [Tooltip("고정 거리 또는 잠금 타겟 기준으로 실제 이동 거리 계산 정책을 결정합니다.")]
        [SerializeField] private SkillLungeResolveMode resolveMode = SkillLungeResolveMode.FixedDistance;

        [Tooltip("타겟 해석 최대 거리입니다. 0 이하면 스킬 CastRange를 사용합니다.")]
        [SerializeField] private float targetResolveRange = -1f;

        [Tooltip("타겟과의 종단 관계(앞에서 멈춤/중심 도달/관통)를 설정합니다.")]
        public SkillLungeTargetRelationMode targetRelationMode = SkillLungeTargetRelationMode.StopBeforeTarget;

        [Tooltip("StopBeforeTarget 모드에서 타겟과 겹치지 않기 위한 여유 거리입니다.")]
        [SerializeField] private float stopOffset = 0.2f;

        [Tooltip("PassThroughTarget 모드에서 타겟 중심 통과 후 추가 이동 거리입니다.")]
        public float passThroughExtraDistance = 0.5f;

        [Tooltip("체크 시 X축 기준으로만 타겟 거리를 계산합니다.")]
        [SerializeField] private bool horizontalOnly = true;

        [Tooltip("런지 중 타겟 충돌 처리 정책입니다.")]
        public SkillLungeCollisionPolicy collisionPolicy = SkillLungeCollisionPolicy.Default;

        [Header("Screen Clamp")]
        [Tooltip("최종 도착 위치가 카메라 화면을 벗어나면 화면 가장자리까지만 이동합니다.")]
        [SerializeField] private SkillLungeScreenClampPolicy screenClampPolicy = SkillLungeScreenClampPolicy.None;

        [Tooltip("화면 경계 안쪽으로 유지할 여유 거리(월드 단위)입니다.")]
        [Min(0f)]
        [SerializeField] private float screenEdgePadding = 0f;

        [Header("Direction")]
        [Tooltip("체크 시 최종 이동 방향을 반전합니다. (뒤로 회피/백스텝)")]
        [SerializeField] private bool invertForward = false;

        [Header("Rigidbody2D")]
        [Tooltip("모션 종료 시 Rigidbody2D 속도를 0으로 초기화합니다.")]
        [SerializeField] private bool stopAtEnd = true;

        [Tooltip("Kinematic Rigidbody2D에서 MovePosition 기반 이동을 사용합니다.")]
        [SerializeField] private bool useMovePosition = true;

        [Tooltip("모션 시작 시점의 Forward를 고정해서 사용합니다.")]
        [SerializeField] private bool useSnapshotForward = true;

        [Header("Policy")]
        [Tooltip("같은 채널의 기존 모션이 실행 중이어도 덮어쓰기를 허용합니다.")]
        [SerializeField] private bool allowReplace = false;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;

        /// <summary>
        /// 런지를 수행할 캐릭터 참조 방식을 반환합니다.
        /// </summary>
        public DummyActorReferenceType ActorReferenceType => actorReferenceType;

        /// <summary>
        /// 런지를 수행할 더미 캐릭터의 actorKey를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 런지 실행 주체를 찾지 못했을 때 사용할 처리 정책을 반환합니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;

        /// <summary>
        /// 총 이동 거리(월드 단위)입니다.
        /// </summary>
        public float Distance => distance;

        /// <summary>
        /// 이동 시간 오버라이드 값(초)입니다. 0 이하면 클립 길이를 사용합니다.
        /// </summary>
        public float DurationOverrideSeconds => durationOverrideSeconds;

        /// <summary>
        /// 이동 보간(Easing) 방식입니다.
        /// </summary>
        public Easing.EaseType Easing => easing;

        /// <summary>
        /// 이동 거리 해석 정책입니다.
        /// </summary>
        public SkillLungeResolveMode ResolveMode => resolveMode;

        /// <summary>
        /// 타겟 해석 최대 거리입니다. 0 이하면 CastRange를 사용합니다.
        /// </summary>
        public float TargetResolveRange => targetResolveRange;

        /// <summary>
        /// 타겟과의 종단 관계 정책입니다.
        /// </summary>
        public SkillLungeTargetRelationMode TargetRelationMode => targetRelationMode;

        /// <summary>
        /// 타겟과 겹치지 않기 위한 정지 여유 거리입니다.
        /// </summary>
        public float StopOffset => stopOffset;

        /// <summary>
        /// 관통 모드에서 타겟 이후 추가 이동 거리입니다.
        /// </summary>
        public float PassThroughExtraDistance => passThroughExtraDistance;

        /// <summary>
        /// 수평(X축) 기준 계산 사용 여부입니다.
        /// </summary>
        public bool HorizontalOnly => horizontalOnly;

        /// <summary>
        /// 충돌 처리 정책입니다.
        /// </summary>
        public SkillLungeCollisionPolicy CollisionPolicy => collisionPolicy;

        /// <summary>
        /// 화면 경계 클램프 정책입니다.
        /// </summary>
        public SkillLungeScreenClampPolicy ScreenClampPolicy => screenClampPolicy;

        /// <summary>
        /// 화면 경계 안쪽 여유 거리(월드 단위)입니다.
        /// </summary>
        public float ScreenEdgePadding => screenEdgePadding;

        /// <summary>
        /// 이동 방향 반전 여부입니다.
        /// </summary>
        public bool InvertForward => invertForward;

        /// <summary>
        /// 모션 종료 시 속도 정지 여부입니다.
        /// </summary>
        public bool StopAtEnd => stopAtEnd;

        /// <summary>
        /// MovePosition 이동 사용 여부입니다.
        /// </summary>
        public bool UseMovePosition => useMovePosition;

        /// <summary>
        /// 시작 시점 Forward 고정 사용 여부입니다.
        /// </summary>
        public bool UseSnapshotForward => useSnapshotForward;

        /// <summary>
        /// 동일 채널 모션 덮어쓰기 허용 여부입니다.
        /// </summary>
        public bool AllowReplace => allowReplace;
    }
}
