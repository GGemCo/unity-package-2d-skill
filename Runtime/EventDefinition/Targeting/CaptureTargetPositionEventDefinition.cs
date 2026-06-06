using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 실행 중 특정 시점의 위치를 이름 있는 위치 앵커로 저장하는 이벤트 정의입니다.
    /// </summary>
    public sealed class CaptureTargetPositionEventDefinition : ScriptableObject
    {
        /// <summary>
        /// 같은 스킬 실행 안에서 이후 이벤트가 참조할 위치 앵커 키입니다.
        /// </summary>
        [Tooltip("같은 스킬 실행 안에서 이후 이벤트가 참조할 위치 앵커 키입니다.")]
        public string anchorKey;

        /// <summary>
        /// 위치 앵커에 저장할 기준 위치입니다.
        /// </summary>
        [Tooltip("위치 앵커에 저장할 기준 위치입니다.")]
        public SkillPositionCaptureSource source = SkillPositionCaptureSource.Target;

        /// <summary>
        /// 타겟 기준 위치를 저장할 때 적용할 세부 목표점 보정 정책입니다.
        /// </summary>
        [Tooltip("타겟 기준 위치를 저장할 때 적용할 세부 목표점 보정 정책입니다.")]
        public SkillPositionCaptureTargetPointPolicy targetPointPolicy =
            SkillPositionCaptureTargetPointPolicy.UseSourcePosition;

        /// <summary>
        /// 타겟 중심 또는 최종 기준 위치에 더할 월드 오프셋입니다.
        /// </summary>
        [Tooltip("타겟 중심 또는 최종 기준 위치에 더할 월드 오프셋입니다.")]
        public Vector2 offset = Vector2.zero;

        /// <summary>
        /// 타겟 HitArea 안에서 사용할 정규화 좌표입니다.
        /// </summary>
        [Tooltip("타겟 HitArea 안에서 사용할 정규화 좌표입니다. (0,0)=좌하단, (1,1)=우상단입니다.")]
        public Vector2 targetHitAreaNormalized = new(0.5f, 0.5f);

        /// <summary>
        /// 스킬 기본 타겟팅 모드를 이벤트 단위로 덮어쓸 때 사용합니다.
        /// </summary>
        [Tooltip("스킬 기본 타겟팅 모드를 이벤트 단위로 덮어쓸 때 사용합니다.")]
        public TargetingOverride targetingOverride;
    }
}
