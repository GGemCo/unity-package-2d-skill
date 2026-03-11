#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Play Mode에서 스킬 테스트 도구가 사용할 런타임 허브입니다.
    /// 몬스터 스폰, 선택 대상 관리, 타겟 및 지점 정보를 중앙에서 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillTestRuntimeHub : MonoBehaviour
    {
        /// <summary>
        /// 현재 활성화된 런타임 허브 인스턴스입니다.
        /// </summary>
        public static SkillTestRuntimeHub Instance { get; private set; }

        [Header("Spawn")]
        [Tooltip("몬스터 스폰 시 기본으로 사용할 월드 좌표입니다.")]
        [SerializeField] private Vector3 defaultSpawnPoint = Vector3.zero;

        [Tooltip("기본 스폰 좌표 주변에 랜덤 오프셋을 줄 반경입니다.")]
        [SerializeField] private float spawnRadius = 0.5f;

        [Header("Target")]
        [Tooltip("툴 테스트에서 스킬 타겟으로 고정할 대상입니다. 기본 정책: Player")]
        [SerializeField] private Transform lockedTarget;

        [Tooltip("true이면 CreateSkillWindow에서 수동으로 지정한 lockedTarget을 우선 사용합니다.")]
        [SerializeField] private bool useManualLockedTarget;

        [Tooltip("스킬이 지점 기반으로 사용할 기준 좌표입니다. 기본 정책: Player 위치")]
        [SerializeField] private Vector3 groundPoint;

        [Tooltip("캐스터가 바라보는 기본 방향입니다. (캐스터->Player 방향으로 자동 갱신)")]
        [SerializeField] private Vector2 forward = Vector2.right;

        /// <summary>
        /// 테스트 허브가 관리 중인 스폰 대상 목록입니다.
        /// </summary>
        private readonly List<GameObject> _spawned = new();

        /// <summary>
        /// 스폰 시점 또는 수동 캡처 시점의 위치/물리 상태 스냅샷입니다.
        /// </summary>
        private readonly Dictionary<int, SkillTestTargetSnapshot> _snapshots = new();

        /// <summary>
        /// 기본 스폰 좌표가 원점일 때 플레이어 기준으로 사용할 몬스터 스폰 오프셋입니다.
        /// </summary>
        private readonly Vector2 _monsterSpawnPosition = new Vector2(150, 0);

        [Header("Reset")]
        [Tooltip("스킬 실행이 종료되면, 선택 몬스터를 스폰 당시 위치로 자동 복원합니다.")]
        [SerializeField] private bool autoResetSelectedMonsterAfterSkill = true;

        /// <summary>
        /// 스킬 실행 종료 후 선택된 몬스터를 자동으로 스냅샷 위치로 복원할지 여부입니다.
        /// 이미 선택된 몬스터가 있으면 즉시 AutoResetter 설정에 반영합니다.
        /// </summary>
        public bool AutoResetSelectedMonsterAfterSkill
        {
            get => autoResetSelectedMonsterAfterSkill;
            set
            {
                autoResetSelectedMonsterAfterSkill = value;

                // 이미 선택된 몬스터가 있으면 즉시 반영합니다.
                if (SelectedMonster != null)
                {
                    var resetter = SelectedMonster.GetComponent<SkillTestAutoResetter>();
                    if (resetter != null) resetter.SetAutoReset(value);
                }
            }
        }

        /// <summary>
        /// 현재 허브가 추적 중인 스폰 대상 목록입니다.
        /// </summary>
        public IReadOnlyList<GameObject> Spawned => _spawned;

        /// <summary>
        /// 현재 선택된 캐스터 또는 몬스터입니다.
        /// </summary>
        public GameObject SelectedMonster { get; private set; }

        /// <summary>
        /// 현재 고정 타겟으로 사용되는 Transform입니다.
        /// </summary>
        public Transform LockedTarget => lockedTarget;

        /// <summary>
        /// 수동으로 지정한 lockedTarget을 자동 정책보다 우선할지 여부입니다.
        /// </summary>
        public bool UseManualLockedTarget => useManualLockedTarget;

        /// <summary>
        /// 지점 기반 스킬이 사용할 기준 좌표입니다.
        /// </summary>
        public Vector3 GroundPoint => groundPoint;

        /// <summary>
        /// 현재 캐스터의 기본 전방 방향입니다.
        /// </summary>
        public Vector2 Forward => forward;

        /// <summary>
        /// 싱글톤 인스턴스를 초기화하고 기본 타겟 및 기준 지점을 설정합니다.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            groundPoint = defaultSpawnPoint;

            // 기본 타겟을 Player로 고정
            TryBindPlayerAsTarget();
        }

        /// <summary>
        /// 오브젝트가 파괴될 때 호출됩니다.
        /// 현재는 별도 정리 작업을 수행하지 않습니다.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 기본 몬스터 스폰 좌표를 설정하고 지점 기반 기준 좌표도 함께 갱신합니다.
        /// </summary>
        /// <param name="worldPos">새 기본 스폰 월드 좌표입니다.</param>
        public void SetDefaultSpawnPoint(Vector3 worldPos)
        {
            defaultSpawnPoint = worldPos;
            groundPoint = worldPos;
        }

        /// <summary>
        /// 현재 씬의 Player를 타겟으로 바인딩합니다.
        /// SceneGame의 player 참조를 우선 사용하고, 없으면 Player 태그를 폴백으로 사용합니다.
        /// </summary>
        /// <returns>플레이어 바인딩에 성공하면 true이고, 실패하면 false입니다.</returns>
        public bool TryBindPlayerAsTarget()
        {
            // SceneGame의 player 참조를 우선 사용합니다.
            if (SceneGame.Instance != null && SceneGame.Instance.player != null)
            {
                lockedTarget = SceneGame.Instance.player.transform;
                groundPoint = lockedTarget.position;
                return true;
            }

            // 폴백: Tag 기반 검색을 사용합니다.
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null)
            {
                lockedTarget = tagged.transform;
                groundPoint = lockedTarget.position;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 지점 기반 스킬이 사용할 기준 좌표를 설정합니다.
        /// </summary>
        /// <param name="worldPos">설정할 월드 좌표입니다.</param>
        public void SetGroundPoint(Vector3 worldPos) => groundPoint = worldPos;

        /// <summary>
        /// 고정 타겟을 직접 설정합니다.
        /// 수동 우선 정책 여부는 변경하지 않습니다.
        /// </summary>
        /// <param name="target">고정할 타겟 Transform입니다.</param>
        public void SetLockedTarget(Transform target) => lockedTarget = target;

        /// <summary>
        /// 툴에서 타겟을 수동으로 지정합니다.
        /// 이후 스킬 실행 시 자동 정책보다 수동 타겟을 우선 사용합니다.
        /// </summary>
        /// <param name="target">수동으로 지정할 타겟 Transform입니다.</param>
        public void SetManualLockedTarget(Transform target)
        {
            lockedTarget = target;
            useManualLockedTarget = target != null;
            if (target != null)
                groundPoint = target.position;
        }

        /// <summary>
        /// 수동 타겟 우선 정책을 해제합니다.
        /// 기존 lockedTarget 참조 자체는 유지합니다.
        /// </summary>
        public void ClearManualLockedTarget()
        {
            useManualLockedTarget = false;
        }

        /// <summary>
        /// 캐스터의 기본 전방 방향을 설정합니다.
        /// 길이가 매우 작은 벡터는 무시하며, 유효한 경우 정규화하여 저장합니다.
        /// </summary>
        /// <param name="dir">설정할 방향 벡터입니다.</param>
        public void SetForward(Vector2 dir)
        {
            if (dir.sqrMagnitude < 1e-6f) return;
            forward = dir.normalized;
        }

        /// <summary>
        /// 다음 프레임까지 비동기로 대기합니다.
        /// Start 이후 상태 캡처가 필요한 흐름에서 사용됩니다.
        /// </summary>
        /// <returns>다음 프레임에 완료되는 비동기 작업입니다.</returns>
        private Task WaitNextFrameAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(CoWait());
            return tcs.Task;

            System.Collections.IEnumerator CoWait()
            {
                yield return null; // 다음 프레임까지 대기합니다.
                tcs.TrySetResult(true);
            }
        }

        /// <summary>
        /// 지정한 몬스터 UID로 캐릭터를 스폰하고, 스킬 테스트에 필요한 초기 설정을 적용합니다.
        /// 필요 시 스냅샷을 캡처하고 자동 복원 기능도 연결합니다.
        /// </summary>
        /// <param name="monsterUid">스폰할 몬스터의 테이블 UID입니다.</param>
        /// <param name="struckTableMonster">몬스터 관련 테이블 데이터입니다.</param>
        /// <param name="struckTableAnimation">방향 계산에 사용할 애니메이션 테이블 데이터입니다.</param>
        /// <param name="captureAfterStart">true이면 Start 이후 상태를 캡처하기 위해 한 프레임 대기 후 스냅샷을 저장합니다.</param>
        /// <returns>생성된 몬스터 오브젝트를 반환하며, 생성에 실패하면 null을 반환합니다.</returns>
        public async Task<GameObject> SpawnMonster(
            int monsterUid,
            StruckTableMonster struckTableMonster,
            StruckTableAnimation struckTableAnimation,
            bool captureAfterStart = true)
        {
            if (SceneGame.Instance == null || SceneGame.Instance.mapManager == null)
                return null;

            var pos = defaultSpawnPoint + (Vector3)(Random.insideUnitCircle * spawnRadius);
            if (defaultSpawnPoint == Vector3.zero)
            {
                pos = SceneGame.Instance.player.transform.position +
                      new Vector3(_monsterSpawnPosition.x, _monsterSpawnPosition.y, 0);
            }

            // 프리팹 로드가 완료될 때까지 대기합니다.
            await SceneGame.Instance.AddressableLoaderPrefabCharacter.LoadCharacterByMonsterUid(monsterUid);

            // 타겟(Player) 기준으로 forward와 groundPoint를 갱신합니다.
            if (lockedTarget == null) TryBindPlayerAsTarget();
            if (lockedTarget != null)
            {
                var d = lockedTarget.position - pos;
                SetForward(new Vector2(d.x, d.y));
                groundPoint = lockedTarget.position;
            }

            // 로드 완료 후 생성, 배치, 선택 로직을 수행합니다.
            bool flip = false;
            var dir = CharacterConstants.ToFacingDirection8(forward);
            if ((struckTableAnimation.DefaultFacingDirection8 == CharacterConstants.FacingDirection8.Right &&
                 dir is CharacterConstants.FacingDirection8.Left or CharacterConstants.FacingDirection8.DownLeft
                     or CharacterConstants.FacingDirection8.UpLeft) ||
                (struckTableAnimation.DefaultFacingDirection8 == CharacterConstants.FacingDirection8.Left &&
                 dir is CharacterConstants.FacingDirection8.Right or CharacterConstants.FacingDirection8.DownRight
                     or CharacterConstants.FacingDirection8.UpRight))
            {
                flip = true;
            }

            int mapUid = SceneGame.Instance.mapManager.GetCurrentMapUid();
            CharacterRegenData monsterData = new CharacterRegenData(monsterUid, pos, flip, mapUid, true);
            var monster = SceneGame.Instance.CharacterManager.CreateMonster(monsterUid, monsterData);
            if (monster == null) return null;

            EnsureSkillTestComponents(monster);
            monster.transform.position = pos;

            // Start 이후 상태를 캡처해야 한다면 한 프레임 뒤에 진행합니다.
            if (captureAfterStart)
                await WaitNextFrameAsync();

            CaptureSnapshot(monster);
            BindAutoResetter(monster);

            _spawned.Add(monster);
            SelectedMonster = monster;

            return monster;
        }

        /// <summary>
        /// 스킬 테스트에 필요한 런타임 컴포넌트를 대상 오브젝트에 보장합니다.
        /// 캐릭터 유형에 따라 적절한 스킬 드라이버를 부착하고, 테스트 간섭 요소도 비활성화합니다.
        /// </summary>
        /// <param name="caster">컴포넌트를 보장할 캐스터 오브젝트입니다.</param>
        private static void EnsureSkillTestComponents(GameObject caster)
        {
            if (caster == null) return;

            // Runtime Skill 컴포넌트를 보장합니다.
            if (caster.GetComponent<GGemCo2DSkill.SkillExecutor>() == null)
                caster.AddComponent<GGemCo2DSkill.SkillExecutor>();

            var character = caster.GetComponent<CharacterBase>();
            if (character != null && character.IsPlayer())
            {
                if (caster.GetComponent<GGemCo2DSkill.PlayerSkillDriverAdapter>() == null)
                    caster.AddComponent<GGemCo2DSkill.PlayerSkillDriverAdapter>();
            }
            else
            {
                if (caster.GetComponent<GGemCo2DSkill.MonsterSkillDriverAdapter>() == null)
                    caster.AddComponent<GGemCo2DSkill.MonsterSkillDriverAdapter>();
            }

            // Editor Gizmo: Damage 클립 구간 동안 데미지 영역을 시각화합니다.
            if (caster.GetComponent<SkillDamageAreaGizmo>() == null)
                caster.AddComponent<SkillDamageAreaGizmo>();

            // 스킬 테스트 중 몬스터 AI가 흐름에 간섭하지 않도록 중지합니다.
            // Core의 BrainTicker를 비활성화하면 등록된 Brain 평가가 중단됩니다.
            var brainTicker = caster.GetComponent<MonsterBrainTicker>();
            if (brainTicker != null)
                brainTicker.enabled = false;

            // BT 패키지가 설치된 경우를 대비하여 타입 참조 없이 안전하게 비활성화합니다.
            foreach (var mb in caster.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "MonsterBtRunner")
                {
                    mb.enabled = false;
                }
            }
        }

        /// <summary>
        /// 툴에서 임의의 씬 캐릭터를 캐스터로 선택합니다.
        /// 필요한 테스트용 컴포넌트를 자동 부착하고, 필요 시 현재 상태를 스냅샷으로 저장합니다.
        /// </summary>
        /// <param name="caster">선택할 캐스터 오브젝트입니다.</param>
        /// <param name="captureSnapshot">true이면 선택 시점 상태를 스냅샷으로 저장합니다.</param>
        public void SelectCaster(GameObject caster, bool captureSnapshot = true)
        {
            if (caster == null) return;

            EnsureSkillTestComponents(caster);

            if (!_spawned.Contains(caster))
                _spawned.Add(caster);

            SelectedMonster = caster;

            if (captureSnapshot)
                CaptureSnapshot(caster);

            BindAutoResetter(caster);
        }

        /// <summary>
        /// 지정 대상의 현재 위치 및 물리 상태를 스냅샷으로 저장합니다.
        /// 스폰 직후 기본 호출되며, 필요 시 툴에서 수동으로 재캡처할 수 있습니다.
        /// </summary>
        /// <param name="monster">스냅샷을 저장할 대상 오브젝트입니다.</param>
        public void CaptureSnapshot(GameObject monster)
        {
            if (monster == null) return;

            int id = monster.GetInstanceID();
            if (!_snapshots.TryGetValue(id, out var snap) || snap == null)
            {
                snap = new SkillTestTargetSnapshot();
                _snapshots[id] = snap;
            }

            snap.Capture(monster);
        }

        /// <summary>
        /// 현재 선택된 몬스터를 마지막 스냅샷 상태로 복원합니다.
        /// </summary>
        /// <returns>복원에 성공하면 true이고, 대상 또는 스냅샷이 없으면 false입니다.</returns>
        public bool ResetSelectedMonsterToSnapshot()
        {
            if (SelectedMonster == null) return false;
            return ResetMonsterToSnapshot(SelectedMonster);
        }

        /// <summary>
        /// 지정한 몬스터를 저장된 스냅샷 상태로 복원합니다.
        /// </summary>
        /// <param name="monster">복원할 대상 오브젝트입니다.</param>
        /// <returns>복원에 성공하면 true이고, 대상 또는 스냅샷이 없으면 false입니다.</returns>
        public bool ResetMonsterToSnapshot(GameObject monster)
        {
            if (monster == null) return false;

            int id = monster.GetInstanceID();
            if (!_snapshots.TryGetValue(id, out var snap) || snap == null)
                return false;

            snap.Apply(monster);
            return true;
        }

        /// <summary>
        /// 지정 몬스터에 자동 복원 컴포넌트를 연결하고 현재 스냅샷을 바인딩합니다.
        /// </summary>
        /// <param name="monster">자동 복원을 연결할 대상 오브젝트입니다.</param>
        private void BindAutoResetter(GameObject monster)
        {
            if (monster == null) return;

            // SkillExecutor의 Busy -> Idle 전환을 감지하여 스냅샷 복원을 수행합니다.
            var resetter = monster.GetComponent<SkillTestAutoResetter>();
            if (resetter == null) resetter = monster.AddComponent<SkillTestAutoResetter>();

            resetter.SetAutoReset(autoResetSelectedMonsterAfterSkill);

            int id = monster.GetInstanceID();
            if (_snapshots.TryGetValue(id, out var snap) && snap != null)
                resetter.BindSnapshot(snap);
        }

        /// <summary>
        /// 지정 몬스터를 테스트 허브 관리 대상에서 제거하고 씬에서도 제거합니다.
        /// 제거 후 선택 대상이면 마지막 남은 스폰 대상으로 선택을 갱신합니다.
        /// </summary>
        /// <param name="monster">제거할 몬스터 오브젝트입니다.</param>
        public void DespawnMonster(GameObject monster)
        {
            if (monster == null) return;

            _snapshots.Remove(monster.GetInstanceID());
            _spawned.Remove(monster);

            if (SceneGame.Instance != null && SceneGame.Instance.CharacterManager != null)
            {
                SceneGame.Instance.CharacterManager.RemoveCharacter(monster);
            }
            else
            {
                Destroy(monster);
            }

            if (SelectedMonster == monster)
                SelectedMonster = _spawned.Count > 0 ? _spawned[^1] : null;
        }

        /// <summary>
        /// 관리 대상 몬스터를 현재 선택 대상으로 지정합니다.
        /// 목록에 없는 경우 관리 목록에도 추가하고 자동 복원 설정을 연결합니다.
        /// </summary>
        /// <param name="monster">선택할 몬스터 오브젝트입니다.</param>
        public void SelectMonster(GameObject monster)
        {
            if (monster == null) return;

            if (!_spawned.Contains(monster))
                _spawned.Add(monster);

            SelectedMonster = monster;
            BindAutoResetter(monster);
        }
    }
}
#endif