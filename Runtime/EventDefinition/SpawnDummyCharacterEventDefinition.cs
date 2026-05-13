using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 더미 캐릭터를 생성할 때 사용하는 정의 데이터입니다.
    /// </summary>
    public sealed class SpawnDummyCharacterEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("같은 스킬 실행 안에서 더미 캐릭터를 식별할 키입니다.")]
        public string actorKey = "dummy_1";

        [Header("Source")]
        [Tooltip("더미를 생성할 원본 테이블 종류입니다.")]
        public DummyCharacterSourceType sourceType = DummyCharacterSourceType.Monster;

        [Tooltip("sourceType에 해당하는 테이블의 캐릭터 UID입니다.")]
        public int characterUid;

        [Header("Spawn")]
        [Tooltip("더미 캐릭터 생성 위치 기준점입니다.")]
        public DummySpawnAnchor spawnAnchor = DummySpawnAnchor.Caster;

        [Tooltip("spawnAnchor가 NamedPositionAnchor일 때 참조할 위치 앵커 키입니다.")]
        public string namedAnchorKey;

        [Tooltip("spawnAnchor 기준점에 더해질 로컬 오프셋입니다.")]
        public Vector3 localOffset;

        [Tooltip("true이면 생성 위치 계산에 현재 시점 대신 스킬 시작 스냅샷(caster/target/ground)을 사용합니다.")]
        public bool useSnapshotCenter;

        [Tooltip("true이면 생성 직후 고정 타겟 방향을 바라보도록 좌우 방향을 보정합니다.")]
        public bool faceLockedTarget = true;

        [Header("Presentation")]
        [Tooltip("true이면 생성 시 페이드 인을 재생합니다.")]
        public bool fadeInEnabled;

        [Tooltip("페이드 인 시간(초)입니다.")]
        public float fadeInDurationSeconds = 0.15f;

        [Tooltip("생성 직후 재생할 애니메이션 이름입니다. 비워두면 재생하지 않습니다.")]
        public string initialAnimationName = ICharacterAnimationController.WaitForwardAnim;

        [Tooltip("생성 직후 애니메이션 루프 여부입니다.")]
        public bool initialAnimationLoop = true;

        [Tooltip("생성 직후 애니메이션 재생 속도 배율입니다.")]
        public float initialAnimationTimeScale = 1f;

        [Header("Lifecycle")]
        [Tooltip("같은 스킬 실행이 정상 종료될 때 이 더미를 자동으로 제거할지 여부입니다.")]
        public bool despawnOnSkillEnd = true;

        [Tooltip("스킬 취소 시 이 더미를 자동으로 제거할지 여부입니다.")]
        public bool despawnOnCancel = true;

        [Tooltip("같은 actorKey가 이미 존재하면 새 더미로 교체할지 여부입니다.")]
        public bool replaceIfExists = true;
    }
}
