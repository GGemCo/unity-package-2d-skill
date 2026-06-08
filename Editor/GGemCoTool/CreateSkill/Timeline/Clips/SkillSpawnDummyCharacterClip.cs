using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 더미 캐릭터를 생성하는 타임라인 이벤트 클립입니다.
    /// Bake 과정에서 <see cref="SpawnDummyCharacterEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillSpawnDummyCharacterClip : SkillEventClipBase
    {
        [Header("Identity")]
        [SerializeField] private string actorKey = "dummy_1";

        [Header("Source")]
        [SerializeField] private DummyCharacterSourceType sourceType = DummyCharacterSourceType.Monster;
        [SerializeField] private int characterUid;

        [Header("Spawn")]
        [SerializeField] private DummySpawnAnchor spawnAnchor = DummySpawnAnchor.Caster;
        [SerializeField] private string namedAnchorKey;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private bool useSnapshotCenter;

        [Tooltip("생성 직후 바라보기 방향을 해석하는 정책입니다.")]
        [SerializeField] private DummySpawnFacingPolicy spawnFacingPolicy = DummySpawnFacingPolicy.FixedDirection;

        [Tooltip("생성 직후 적용할 바라보기 방향입니다. None이면 기본 방향을 유지합니다.")]
        [SerializeField] private CharacterConstants.FacingDirection8 spawnFacing = CharacterConstants.FacingDirection8.None;

        [Header("Presentation")]
        [SerializeField] private bool fadeInEnabled;
        [SerializeField] private float fadeInDurationSeconds = 0.15f;
        [SerializeField] private string initialAnimationName = ICharacterAnimationController.WaitForwardAnim;
        [SerializeField] private bool initialAnimationLoop = true;
        [SerializeField] private float initialAnimationTimeScale = 1f;

        [Header("Lifecycle")]
        [SerializeField] private bool despawnOnSkillEnd = true;
        [SerializeField] private bool despawnOnCancel = true;
        [SerializeField] private bool replaceIfExists = true;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.SpawnDummyCharacter;

        /// <summary>
        /// 더미 캐릭터 식별 키를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 더미 생성 소스 타입을 반환합니다.
        /// </summary>
        public DummyCharacterSourceType SourceType => sourceType;

        /// <summary>
        /// 더미 생성에 사용할 캐릭터 UID를 반환합니다.
        /// </summary>
        public int CharacterUid => characterUid;

        /// <summary>
        /// 더미 생성 위치 기준점을 반환합니다.
        /// </summary>
        public DummySpawnAnchor SpawnAnchor => spawnAnchor;

        /// <summary>
        /// 이름 있는 위치 앵커 키를 반환합니다.
        /// </summary>
        public string NamedAnchorKey => namedAnchorKey;

        /// <summary>
        /// 생성 위치 오프셋을 반환합니다.
        /// </summary>
        public Vector3 LocalOffset => localOffset;

        /// <summary>
        /// 스냅샷 중심 사용 여부를 반환합니다.
        /// </summary>
        public bool UseSnapshotCenter => useSnapshotCenter;

        /// <summary>
        /// 생성 직후 바라보기 방향 해석 정책을 반환합니다.
        /// </summary>
        public DummySpawnFacingPolicy SpawnFacingPolicy => spawnFacingPolicy;

        /// <summary>
        /// 생성 직후 적용할 바라보기 방향을 반환합니다.
        /// </summary>
        public CharacterConstants.FacingDirection8 SpawnFacing => spawnFacing;

        /// <summary>
        /// 페이드 인 사용 여부를 반환합니다.
        /// </summary>
        public bool FadeInEnabled => fadeInEnabled;

        /// <summary>
        /// 페이드 인 시간(초)을 반환합니다.
        /// </summary>
        public float FadeInDurationSeconds => fadeInDurationSeconds;

        /// <summary>
        /// 생성 직후 애니메이션 이름을 반환합니다.
        /// </summary>
        public string InitialAnimationName => initialAnimationName;

        /// <summary>
        /// 생성 직후 애니메이션 루프 여부를 반환합니다.
        /// </summary>
        public bool InitialAnimationLoop => initialAnimationLoop;

        /// <summary>
        /// 생성 직후 애니메이션 시간 배율을 반환합니다.
        /// </summary>
        public float InitialAnimationTimeScale => initialAnimationTimeScale;

        /// <summary>
        /// 스킬 종료 시 자동 제거 여부를 반환합니다.
        /// </summary>
        public bool DespawnOnSkillEnd => despawnOnSkillEnd;

        /// <summary>
        /// 스킬 취소 시 자동 제거 여부를 반환합니다.
        /// </summary>
        public bool DespawnOnCancel => despawnOnCancel;

        /// <summary>
        /// 동일 키 존재 시 교체 여부를 반환합니다.
        /// </summary>
        public bool ReplaceIfExists => replaceIfExists;
    }
}
