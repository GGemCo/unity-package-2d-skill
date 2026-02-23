#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Play Mode에서 스킬 테스트 툴이 사용할 런타임 허브.
    /// - 몬스터 스폰(테이블 UID 기반)
    /// - 선택 몬스터 관리
    /// - 타겟/지점(ground point) 관리
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillTestRuntimeHub : MonoBehaviour
    {
        public static SkillTestRuntimeHub Instance { get; private set; }

        [Header("Spawn")]
        [SerializeField] private Vector3 defaultSpawnPoint = Vector3.zero;
        [SerializeField] private float spawnRadius = 0.5f;

        [Header("Target")]
        [Tooltip("툴 테스트에서 스킬 타겟으로 고정할 대상입니다. 기본 정책: Player")]
        [SerializeField] private Transform lockedTarget;
        [Tooltip("스킬이 지점 기반으로 사용할 기준 좌표입니다. 기본 정책: Player 위치")]
        [SerializeField] private Vector3 groundPoint;
        [Tooltip("캐스터가 바라보는 기본 방향입니다. (캐스터->Player 방향으로 자동 갱신)")]
        [SerializeField] private Vector2 forward = Vector2.right;

        private readonly List<GameObject> _spawned = new();

        // 스폰 시점(또는 수동 캡처 시점)의 위치/물리 스냅샷(원복용)
        private readonly Dictionary<int, SkillTestTargetSnapshot> _snapshots = new();
        private readonly Vector2 _monsterSpawnPosition = new Vector2(150, 0);

        [Header("Reset")]
        [Tooltip("스킬 실행이 종료되면, 선택 몬스터를 스폰 당시 위치로 자동 복원합니다.")]
        [SerializeField] private bool autoResetSelectedMonsterAfterSkill = true;
        public bool AutoResetSelectedMonsterAfterSkill
        {
            get => autoResetSelectedMonsterAfterSkill;
            set
            {
                autoResetSelectedMonsterAfterSkill = value;
                // 이미 선택된 몬스터가 있으면 즉시 반영
                if (SelectedMonster != null)
                {
                    var resetter = SelectedMonster.GetComponent<SkillTestAutoResetter>();
                    if (resetter != null) resetter.SetAutoReset(value);
                }
            }
        }

        public IReadOnlyList<GameObject> Spawned => _spawned;

        public GameObject SelectedMonster { get; private set; }

        public Transform LockedTarget => lockedTarget;
        public Vector3 GroundPoint => groundPoint;
        public Vector2 Forward => forward;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else
            {
                Destroy(gameObject);
            }

            Instance = this;
            groundPoint = defaultSpawnPoint;

            // 기본 타겟을 Player로 고정
            TryBindPlayerAsTarget();
        }

        private void OnDestroy()
        {
            // if (Instance == this) Instance = null;
        }

        public void SetDefaultSpawnPoint(Vector3 worldPos)
        {
            defaultSpawnPoint = worldPos;
            groundPoint = worldPos;
        }

        /// <summary>
        /// 현재 씬의 Player를 타겟으로 묶습니다.
        /// </summary>
        public bool TryBindPlayerAsTarget()
        {
            // SceneGame의 player 참조 우선 사용
            if (SceneGame.Instance != null && SceneGame.Instance.player != null)
            {
                lockedTarget = SceneGame.Instance.player.transform;
                groundPoint = lockedTarget.position;
                return true;
            }

            // 폴백: Tag 기반
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null)
            {
                lockedTarget = tagged.transform;
                groundPoint = lockedTarget.position;
                return true;
            }

            return false;
        }

        public void SetGroundPoint(Vector3 worldPos) => groundPoint = worldPos;

        public void SetLockedTarget(Transform target) => lockedTarget = target;

        public void SetForward(Vector2 dir)
        {
            if (dir.sqrMagnitude < 1e-6f) return;
            forward = dir.normalized;
        }
        private Task WaitNextFrameAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(CoWait());
            return tcs.Task;

            System.Collections.IEnumerator CoWait()
            {
                yield return null; // 다음 프레임
                tcs.TrySetResult(true);
            }
        }

        public async Task<GameObject> SpawnMonster(int monsterUid, StruckTableMonster struckTableMonster,
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

            // 1) 프리팹 로드 완료까지 대기
            //    ※ 아래 타입 인자는 실제 LoadCharacterByMonsterUid 반환/Result 타입에 맞춰 조정하세요.
            await SceneGame.Instance.AddressableLoaderPrefabCharacter.LoadCharacterByMonsterUid(monsterUid);

            // 타겟(Player) 기준 forward 갱신
            if (lockedTarget == null) TryBindPlayerAsTarget();
            if (lockedTarget != null)
            {
                var d = lockedTarget.position - pos;
                SetForward(new Vector2(d.x, d.y));
                groundPoint = lockedTarget.position;
            }

            // 2) 로드 완료 후 생성/배치/선택 로직 수행
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

            // Start 이후 상태를 캡처하려면 한 프레임 지연
            if (captureAfterStart)
                await WaitNextFrameAsync();

            CaptureSnapshot(monster);
            BindAutoResetter(monster);

            _spawned.Add(monster);
            SelectedMonster = monster;

            return monster;
        }

        /// <summary>
        /// 스킬 테스트를 위해 필요한 컴포넌트들을 자동으로 추가합니다.
        /// - SkillExecutor
        /// - MonsterSkillDriverAdapter
        /// </summary>
        private static void EnsureSkillTestComponents(GameObject caster)
        {
            if (caster == null) return;            // Runtime Skill components
            if (caster.GetComponent<GGemCo2DSkill.SkillExecutor>() == null)
                caster.AddComponent<GGemCo2DSkill.SkillExecutor>();

            if (caster.GetComponent<GGemCo2DSkill.MonsterSkillDriverAdapter>() == null)
                caster.AddComponent<GGemCo2DSkill.MonsterSkillDriverAdapter>();

            // Editor Gizmo: Damage 클립 구간 동안 데미지 영역을 표시
            if (caster.GetComponent<SkillDamageAreaGizmo>() == null)
                caster.AddComponent<SkillDamageAreaGizmo>();

            // 스킬 테스트 툴에서는 몬스터 AI(레거시 Brain/BT)가 스킬 테스트 흐름에 간섭하지 않도록 중지합니다.
            // - Core의 BrainTicker를 끄면, 등록된 Brain(레거시/BT 포함) 평가가 모두 중단됩니다.
            var brainTicker = caster.GetComponent<MonsterBrainTicker>();
            if (brainTicker != null)
                brainTicker.enabled = false;

            // BT 패키지가 설치되어 있는 경우(런타임에 AddComponent 되는 구조), 타입 참조 없이 안전하게 비활성화합니다.
            // (Skill 패키지는 BT 패키지를 직접 참조하지 않습니다.)
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
        /// 선택 몬스터(또는 지정 대상)의 "원래 위치" 스냅샷을 저장합니다.
        /// - 스폰 직후 기본 호출되며, 필요 시 툴에서 수동 캡처도 가능합니다.
        /// </summary>
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
        /// 선택 몬스터를 스폰 당시(또는 마지막 캡처 당시) 위치로 되돌립니다.
        /// </summary>
        public bool ResetSelectedMonsterToSnapshot()
        {
            if (SelectedMonster == null) return false;
            return ResetMonsterToSnapshot(SelectedMonster);
        }

        /// <summary>
        /// 지정 몬스터를 스냅샷으로 복원합니다.
        /// </summary>
        public bool ResetMonsterToSnapshot(GameObject monster)
        {
            if (monster == null) return false;

            int id = monster.GetInstanceID();
            if (!_snapshots.TryGetValue(id, out var snap) || snap == null)
                return false;

            snap.Apply(monster);
            return true;
        }

        private void BindAutoResetter(GameObject monster)
        {
            if (monster == null) return;

            // Auto resetter는 SkillExecutor의 Busy->Idle 전환을 감지하여 스냅샷 복원을 수행합니다.
            var resetter = monster.GetComponent<SkillTestAutoResetter>();
            if (resetter == null) resetter = monster.AddComponent<SkillTestAutoResetter>();

            resetter.SetAutoReset(autoResetSelectedMonsterAfterSkill);

            int id = monster.GetInstanceID();
            if (_snapshots.TryGetValue(id, out var snap) && snap != null)
                resetter.BindSnapshot(snap);
        }
        
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

        public void SelectMonster(GameObject monster)
        {
            if (monster == null) return;
            if (!_spawned.Contains(monster)) _spawned.Add(monster);
            SelectedMonster = monster;
            BindAutoResetter(monster);
        }
    }
}
#endif