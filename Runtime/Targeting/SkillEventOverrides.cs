using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    [Serializable]
    public struct TargetingOverride
    {
        public bool enabled;
        public ConfigCommonSkill.SkillTargetingMode mode;

        [Tooltip("스킬 기본 range를 덮어쓰기. 0 이하이면 무시.")]
        public float rangeOverride;

        [Tooltip("스킬 기본 maxTargets를 덮어쓰기. 0 이하이면 무시.")]
        public int maxTargetsOverride;

        [Tooltip("타겟/지점 중심을 '시작 시점 스냅샷'으로 고정할지 여부")]
        public bool useSnapshotCenter;

        [Tooltip("FollowTargetArea 모드일 때, 이벤트 시점마다 타겟 Transform을 따라갈지 여부(=true면 실시간 추적)")]
        public bool followTarget;
    }

    [Serializable]
    public struct AreaOverride
    {
        public bool enabled;
        public string areaId;

        [Tooltip("AreaDefinition의 크기 계수를 이벤트별로 가산/보정하고 싶을 때 사용(1=기본)")]
        public float scale;
    }

    /// <summary>
    /// 스킬 위치 캡처 결과에 적용할 지면 투영 방식입니다.
    /// </summary>
    public enum SkillGroundProjectionMode
    {
        /// <summary>지면 투영을 적용하지 않고 계산된 원본 위치를 사용합니다.</summary>
        None = 0,

        /// <summary>계산된 위치에서 아래 방향으로 가장 가까운 지면 표면을 탐색합니다.</summary>
        ProjectDownToGround = 1,
    }

    /// <summary>
    /// 위치 캡처 과정에서 지면을 찾지 못했을 때 적용할 실패 처리 정책입니다.
    /// </summary>
    public enum SkillGroundProjectionFailurePolicy
    {
        /// <summary>지면을 찾지 못하면 투영 전 원본 위치를 그대로 저장합니다.</summary>
        KeepOriginalPosition = 0,

        /// <summary>지면을 찾지 못하면 해당 위치 캡처 이벤트를 중단합니다.</summary>
        SkipCapture = 1,

        /// <summary>경고를 남기고 투영 전 원본 위치를 그대로 저장합니다.</summary>
        WarnAndKeepOriginalPosition = 2,
    }

    /// <summary>
    /// 스킬 위치 캡처 결과를 실제 2D 지면 표면으로 투영하기 위한 설정입니다.
    /// </summary>
    [Serializable]
    public struct SkillGroundProjectionOptions
    {
        /// <summary>지면 투영 사용 방식입니다.</summary>
        [Tooltip("계산된 위치를 아래쪽 지면 표면으로 투영할지 여부입니다.")]
        public SkillGroundProjectionMode mode;

        /// <summary>지면 탐색에 사용할 Core 공통 레이어 정책입니다.</summary>
        [Tooltip("일반 지면, 원웨이 플랫폼 또는 사용자 정의 레이어 중 탐색 대상을 선택합니다.")]
        public GroundSurfaceLayerPolicy layerPolicy;

        /// <summary>사용자 정의 레이어 정책에서 사용할 Physics2D 레이어 마스크입니다.</summary>
        [Tooltip("Layer Policy가 Custom일 때 지면 탐색에 사용할 레이어 마스크입니다.")]
        public LayerMask customGroundLayerMask;

        /// <summary>Ray 시작점을 계산된 위치보다 위로 올릴 거리입니다.</summary>
        [Min(0f)]
        [Tooltip("Ray 시작점을 계산된 위치보다 위로 올릴 거리입니다.")]
        public float probeStartUpOffset;

        /// <summary>계산된 위치에서 아래쪽으로 지면을 탐색할 최대 거리입니다.</summary>
        [Min(0f)]
        [Tooltip("계산된 위치에서 아래쪽으로 지면을 탐색할 최대 거리입니다.")]
        public float maxProbeDistance;

        /// <summary>탐색된 표면의 Normal 방향으로 결과 위치를 띄울 거리입니다.</summary>
        [Tooltip("지면에 VFX가 파묻히지 않도록 표면 Normal 방향으로 더할 오프셋입니다.")]
        public float surfaceNormalOffset;

        /// <summary>지면 탐색 실패 시 적용할 처리 정책입니다.</summary>
        [Tooltip("지면을 찾지 못했을 때 원본 위치 유지, 경고 또는 캡처 중단 중 하나를 선택합니다.")]
        public SkillGroundProjectionFailurePolicy failurePolicy;

        /// <summary>
        /// 새 위치 캡처 클립에 사용할 기본 지면 투영 설정을 생성합니다.
        /// </summary>
        /// <returns>투영은 비활성화되어 있고 권장 탐색 거리 값이 채워진 설정입니다.</returns>
        public static SkillGroundProjectionOptions CreateDefault()
        {
            return new SkillGroundProjectionOptions
            {
                mode = SkillGroundProjectionMode.None,
                layerPolicy = GroundSurfaceLayerPolicy.DefaultGroundAndOneWay,
                customGroundLayerMask = 0,
                probeStartUpOffset = CharacterGroundProbeUtility.ProbeUpOffset,
                maxProbeDistance = 12f,
                surfaceNormalOffset = 0f,
                failurePolicy = SkillGroundProjectionFailurePolicy.WarnAndKeepOriginalPosition,
            };
        }
    }

    /// <summary>
    /// 스킬 이벤트가 위치 중심점을 어디에서 가져올지 정의합니다.
    /// </summary>
    public enum SkillPositionReferenceMode
    {
        /// <summary>
        /// 기존 타겟팅 규칙을 그대로 사용합니다.
        /// </summary>
        CurrentTargeting = 0,

        /// <summary>
        /// 스킬 실행 시작 시점에 저장된 타겟팅 스냅샷을 사용합니다.
        /// </summary>
        SkillStartSnapshot = 1,

        /// <summary>
        /// 같은 스킬 실행 중 이전 이벤트가 기록한 이름 있는 위치 앵커를 사용합니다.
        /// 앵커를 찾지 못하면 해당 이벤트를 처리하지 않습니다.
        /// </summary>
        NamedPositionAnchor = 2,

        /// <summary>
        /// 이름 있는 위치 앵커를 우선 사용하고, 없으면 기존 타겟팅 규칙을 사용합니다.
        /// </summary>
        NamedPositionAnchorOrCurrent = 3,
    }

    /// <summary>
    /// 스킬 이벤트가 계산한 위치를 이후 이벤트에서 참조할 수 있도록 저장하는 설정입니다.
    /// </summary>
    [Serializable]
    public struct SkillPositionAnchorWriteOptions
    {
        /// <summary>
        /// 위치 앵커를 저장할지 여부입니다.
        /// </summary>
        public bool enabled;

        /// <summary>
        /// 같은 스킬 실행 안에서 위치 앵커를 찾을 때 사용할 키입니다.
        /// </summary>
        [Tooltip("같은 스킬 실행 안에서 이후 이벤트가 참조할 위치 앵커 키입니다.")]
        public string key;
    }

    /// <summary>
    /// 스킬 이벤트가 사용할 위치 기준점을 지정하는 설정입니다.
    /// </summary>
    [Serializable]
    public struct SkillPositionReference
    {
        /// <summary>
        /// 위치 기준점 해석 방식입니다.
        /// </summary>
        public SkillPositionReferenceMode mode;

        /// <summary>
        /// 이름 있는 위치 앵커를 참조할 때 사용할 키입니다.
        /// </summary>
        [Tooltip("NamedPositionAnchor 모드에서 참조할 위치 앵커 키입니다.")]
        public string key;
    }

    /// <summary>
    /// 스킬 실행 중 위치 앵커로 저장할 기준 위치를 정의합니다.
    /// </summary>
    public enum SkillPositionCaptureSource
    {
        /// <summary>
        /// 이벤트 시점의 캐스터 위치를 저장합니다.
        /// </summary>
        Caster = 0,

        /// <summary>
        /// 이벤트 시점의 타겟 위치를 저장합니다.
        /// 타겟이 없으면 스킬 시작 시점의 타겟 스냅샷을 사용합니다.
        /// </summary>
        Target = 1,

        /// <summary>
        /// 이벤트 시점의 지면 기준점을 저장합니다.
        /// </summary>
        Ground = 2,

        /// <summary>
        /// 스킬 타겟팅 모드와 사거리 정책을 기준으로 계산한 위치를 저장합니다.
        /// </summary>
        CurrentTargeting = 3,

        /// <summary>
        /// 스킬 시작 시점에 저장된 타겟 위치 스냅샷을 저장합니다.
        /// </summary>
        SkillStartTargetSnapshot = 4,

        /// <summary>
        /// 같은 스킬 실행 중 actorKey로 등록된 더미 Actor의 현재 위치를 저장합니다.
        /// </summary>
        DummyActor = 5,
    }

    /// <summary>
    /// 타겟 기준 위치를 저장할 때 세부 목표점을 보정하는 정책입니다.
    /// </summary>
    public enum SkillPositionCaptureTargetPointPolicy
    {
        /// <summary>
        /// 계산된 기준 위치를 그대로 사용합니다.
        /// </summary>
        UseSourcePosition = 0,

        /// <summary>
        /// 타겟 중심 위치에 고정 오프셋을 더한 위치를 사용합니다.
        /// </summary>
        FixedOffsetFromTargetCenter = 1,

        /// <summary>
        /// 타겟 HitArea의 정규화 좌표를 월드 위치로 변환해서 사용합니다.
        /// HitArea를 찾지 못하면 타겟 중심과 고정 오프셋으로 대체합니다.
        /// </summary>
        FixedNormalizedPointInTargetHitArea = 2,
    }

    /// <summary>
    /// 스킬 실행 중 특정 이벤트가 계산한 위치 정보를 보관하는 스냅샷입니다.
    /// </summary>
    public readonly struct SkillPositionAnchorSnapshot
    {
        /// <summary>
        /// 이벤트가 계산한 최종 월드 위치입니다.
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>
        /// 이벤트 시점에 해석된 2D 전방 방향입니다.
        /// </summary>
        public readonly Vector3 Forward;

        /// <summary>
        /// 이벤트 시점에 해석된 캐스터 위치입니다.
        /// </summary>
        public readonly Vector3 CasterPosition;

        /// <summary>
        /// 이벤트 시점에 해석된 타겟 위치입니다.
        /// </summary>
        public readonly Vector3 TargetPosition;

        /// <summary>
        /// 이벤트 시점에 해석된 지면 기준점입니다.
        /// </summary>
        public readonly Vector3 GroundPoint;

        /// <summary>
        /// 스킬 사용 애니메이션 기준으로 위치가 기록된 시간입니다.
        /// </summary>
        public readonly float Time;

        /// <summary>
        /// 위치가 지면 투영으로 계산된 경우의 표면 Normal입니다.
        /// 지면 투영을 사용하지 않은 위치는 기본 위쪽 방향을 사용합니다.
        /// </summary>
        public readonly Vector3 SurfaceNormal;

        /// <summary>
        /// 위치 앵커 스냅샷을 생성합니다.
        /// </summary>
        /// <param name="position">이벤트가 계산한 최종 월드 위치입니다.</param>
        /// <param name="forward">이벤트 시점에 해석된 2D 전방 방향입니다.</param>
        /// <param name="casterPosition">이벤트 시점에 해석된 캐스터 위치입니다.</param>
        /// <param name="targetPosition">이벤트 시점에 해석된 타겟 위치입니다.</param>
        /// <param name="groundPoint">이벤트 시점에 해석된 지면 기준점입니다.</param>
        /// <param name="time">스킬 사용 애니메이션 기준 기록 시간입니다.</param>
        /// <param name="surfaceNormal">위치가 투영된 지면의 표면 Normal입니다.</param>
        public SkillPositionAnchorSnapshot(
            Vector3 position,
            Vector3 forward,
            Vector3 casterPosition,
            Vector3 targetPosition,
            Vector3 groundPoint,
            float time,
            Vector3 surfaceNormal = default)
        {
            Position = position;
            Forward = forward;
            CasterPosition = casterPosition;
            TargetPosition = targetPosition;
            GroundPoint = groundPoint;
            Time = time;
            SurfaceNormal = surfaceNormal.sqrMagnitude > 0.000001f
                ? surfaceNormal.normalized
                : Vector3.up;
        }
    }
}
