using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 전진(러시, 대시, 회피 등) 이동 이벤트를 정의하는 Authoring용 타임라인 클립입니다.
    /// Bake 과정에서 <see cref="GGemCo2DSkill.LungeEventDefinition"/> Payload로 변환되어
    /// 실제 스킬 실행 시스템에서 사용됩니다.
    /// 
    /// 이동은 속도 기반이 아니라 총 이동 거리(Distance) 기반으로 설계되며,
    /// Arc 계열 모션은 <see cref="SkillArcLungeClip"/> 에서 별도로 정의합니다.
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
        [SerializeField] private SkillLungeResolveMode resolveMode = SkillLungeResolveMode.FixedDistance;

        [Tooltip("타겟 추적 허용 최대 거리입니다. 0 이하이면 스킬 CastRange를 사용합니다.")]
        [SerializeField] private float targetResolveRange = -1f;

        [Tooltip("타겟 종착 관계를 해석하는 방식입니다. StopBeforeTarget은 앞에서 멈추고, ReachTargetCenter는 중심까지, PassThroughTarget은 타겟을 지나갑니다.")]
        public SkillLungeTargetRelationMode targetRelationMode = SkillLungeTargetRelationMode.StopBeforeTarget;
        
        [Tooltip("타겟 중심에 완전히 겹치지 않도록 남길 거리입니다.")]
        [SerializeField] private float stopOffset = 0.2f;

        [Tooltip("PassThroughTarget일 때 타겟 중심을 지난 뒤 추가로 이동할 거리입니다.")]
        public float passThroughExtraDistance = 0.5f;
        
        [Tooltip("체크 시 X축 기준으로만 타겟 접근 거리를 계산합니다.")]
        [SerializeField] private bool horizontalOnly = true;

        [Tooltip("돌진 중 타겟과의 충돌 처리 정책입니다. 타겟을 지나가는 연출이 필요할 때 사용할 수 있습니다.")]
        public SkillLungeCollisionPolicy collisionPolicy = SkillLungeCollisionPolicy.Default;
        
        [Header("Direction")]
        [Tooltip("체크 시 현재 Forward 방향의 반대로 이동합니다. (뒤로 회피/백스텝 구현용)")]
        [SerializeField] private bool invertForward = false;

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

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;

        /// <summary>
        /// 캐릭터가 이동할 총 거리(월드 유닛)입니다.
        /// </summary>
        public float Distance => distance;

        /// <summary>
        /// 타임라인 클립 길이를 대신하여 사용할 이동 지속 시간(초)입니다.
        /// 0 이하일 경우 타임라인 클립 길이를 사용합니다.
        /// </summary>
        public float DurationOverrideSeconds => durationOverrideSeconds;

        /// <summary>
        /// 이동 진행 시 적용되는 보간(Easing) 방식입니다.
        /// </summary>
        public Easing.EaseType Easing => easing;

        /// <summary>
        /// 실제 이동 거리 계산 방식입니다.
        /// </summary>
        public GGemCo2DSkill.SkillLungeResolveMode ResolveMode => resolveMode;

        /// <summary>
        /// 타겟 추적 허용 최대 거리입니다. 0 이하이면 스킬 CastRange를 사용합니다.
        /// </summary>
        public float TargetResolveRange => targetResolveRange;
        public SkillLungeTargetRelationMode TargetRelationMode => targetRelationMode;

        /// <summary>
        /// 타겟 중심에 완전히 겹치지 않도록 남길 거리입니다.
        /// </summary>
        public float StopOffset => stopOffset;
        public float PassThroughExtraDistance => passThroughExtraDistance;

        /// <summary>
        /// X축 기준으로만 타겟 접근 거리를 계산할지 여부입니다.
        /// </summary>
        public bool HorizontalOnly => horizontalOnly;
        public SkillLungeCollisionPolicy CollisionPolicy => collisionPolicy;

        /// <summary>
        /// 이동 방향을 Forward의 반대로 뒤집을지 여부입니다.
        /// </summary>
        public bool InvertForward => invertForward;

        /// <summary>
        /// 이동 종료 시 Rigidbody2D의 속도를 0으로 초기화할지 여부입니다.
        /// </summary>
        public bool StopAtEnd => stopAtEnd;

        /// <summary>
        /// Rigidbody2D 이동 시 MovePosition을 사용할지 여부입니다.
        /// </summary>
        public bool UseMovePosition => useMovePosition;

        /// <summary>
        /// 이동 시작 시점의 Forward 방향을 고정하여 사용할지 여부입니다.
        /// </summary>
        public bool UseSnapshotForward => useSnapshotForward;

        /// <summary>
        /// 동일 채널에서 실행 중인 기존 모션을 덮어쓸 수 있는지 여부입니다.
        /// </summary>
        public bool AllowReplace => allowReplace;
    }
}