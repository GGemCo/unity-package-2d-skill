#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Play Mode에서 스킬 테스트 도구가 사용할 런타임 허브입니다.
    /// 몬스터 스폰, 선택 대상 관리, 타겟/지점 정보, 디버그 영역, 자동 복원 상태를 중앙에서 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillTestRuntimeHub : MonoBehaviour
    {
        public static SkillTestRuntimeHub Instance { get; private set; }
        public static GGemCoSkillSettings CurrentSettings { get; private set; }

        public bool IsSkillDebugEnabled => CurrentSettings == null || CurrentSettings.EnableSkillDebug;
        public bool IsDamageAreaGizmoEnabled => IsSkillDebugEnabled && (CurrentSettings == null || CurrentSettings.EnableDamageAreaGizmo);
        public bool IsLaserGizmoEnabled => IsSkillDebugEnabled && (CurrentSettings == null || CurrentSettings.EnableLaserGizmo);
        public Color DamageAreaGizmoColor => CurrentSettings != null ? CurrentSettings.damageAreaGizmoColor : new Color(1f, 0.35f, 0.2f, 0.9f);
        public Color LaserGizmoColor => CurrentSettings != null ? CurrentSettings.laserGizmoColor : new Color(0.2f, 0.95f, 1f, 0.95f);
        public bool DrawOnlyWhenSelectedCaster => CurrentSettings != null && CurrentSettings.drawOnlyWhenSelectedCaster;
        
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

        [Header("Reset")]
        [Tooltip("스킬 실행이 종료되면, 선택 몬스터를 스냅샷 위치로 자동 복원합니다.")]
        [SerializeField] private bool autoResetSelectedMonsterAfterSkill = true;

        private readonly List<GameObject> _spawned = new();
        private readonly Dictionary<int, SkillTestTargetSnapshot> _snapshots = new();
        private readonly List<SkillDebugAreaRecord> _activeDamageAreas = new(8);
        private readonly List<SkillDebugLaserRecord> _activeLasers = new(8);
        private readonly Vector2 _monsterSpawnPosition = new(150, 0);

        private SkillExecutor _selectedExecutor;
        private bool _selectedExecutorWasBusy;

        public bool AutoResetSelectedMonsterAfterSkill
        {
            get => autoResetSelectedMonsterAfterSkill;
            set => autoResetSelectedMonsterAfterSkill = value;
        }

        public IReadOnlyList<GameObject> Spawned => _spawned;
        public IReadOnlyList<SkillDebugAreaRecord> ActiveDamageAreas => _activeDamageAreas;
        public IReadOnlyList<SkillDebugLaserRecord> ActiveLasers => _activeLasers;
        public GameObject SelectedMonster { get; private set; }
        public Transform LockedTarget => lockedTarget;
        public bool UseManualLockedTarget => useManualLockedTarget;
        public Vector3 GroundPoint => groundPoint;
        public Vector2 Forward => forward;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ApplyLoadedSettings(CurrentSettings);

            if (Application.isPlaying && (CurrentSettings == null || CurrentSettings.keepBridgeDontDestroyOnLoad))
                DontDestroyOnLoad(gameObject);

            groundPoint = defaultSpawnPoint;

            if (CurrentSettings == null || CurrentSettings.autoBindPlayerAsTarget)
                TryBindPlayerAsTarget();
        }

        private void Update()
        {
            CleanupExpiredDamageAreas();
            CleanupExpiredLasers();
            UpdateAutoResetState();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void ApplySettings(GGemCoSkillSettings settings)
        {
            CurrentSettings = settings;

            if (settings == null)
                return;

            autoResetSelectedMonsterAfterSkill = settings.autoResetSelectedMonsterAfterSkill;
            spawnRadius = Mathf.Max(0f, settings.defaultSpawnRadius);
        }

        public void ApplyLoadedSettings(GGemCoSkillSettings settings)
        {
            CurrentSettings = settings;
            if (settings == null)
                return;

            autoResetSelectedMonsterAfterSkill = settings.autoResetSelectedMonsterAfterSkill;
            spawnRadius = Mathf.Max(0f, settings.defaultSpawnRadius);

            if (Application.isPlaying && settings.autoBindPlayerAsTarget && lockedTarget == null)
                TryBindPlayerAsTarget();
        }

        public static void SetCurrentSettings(GGemCoSkillSettings settings)
        {
            CurrentSettings = settings;
            if (Instance != null)
                Instance.ApplyLoadedSettings(settings);
        }

        public static bool TryInitializeFromLoadedSettings(GGemCoSkillSettings settings)
        {
            if (settings == null)
                return false;

            SetCurrentSettings(settings);
            return true;
        }

        public static void ResetLoadedSettings()
        {
            CurrentSettings = null;
        }

        private void CleanupExpiredDamageAreas()
        {
            if (_activeDamageAreas.Count == 0)
                return;

            float now = Time.time;
            for (int i = _activeDamageAreas.Count - 1; i >= 0; i--)
            {
                var area = _activeDamageAreas[i];
                if (area == null || now >= area.ExpireTime)
                    _activeDamageAreas.RemoveAt(i);
            }
        }


        /// <summary>
        /// 만료된 레이저 기즈모 기록을 정리합니다.
        /// </summary>
        private void CleanupExpiredLasers()
        {
            if (_activeLasers.Count == 0)
                return;

            float now = Time.time;
            for (int i = _activeLasers.Count - 1; i >= 0; i--)
            {
                var laser = _activeLasers[i];
                if (laser == null || now >= laser.ExpireTime)
                    _activeLasers.RemoveAt(i);
            }
        }

        private void UpdateAutoResetState()
        {
            if (!autoResetSelectedMonsterAfterSkill || SelectedMonster == null)
                return;

            if (_selectedExecutor == null)
            {
                RefreshSelectedExecutor();
                if (_selectedExecutor == null)
                    return;
            }

            bool busy = _selectedExecutor.IsBusy;
            if (busy)
            {
                _selectedExecutorWasBusy = true;
                return;
            }

            if (!_selectedExecutorWasBusy)
                return;

            _selectedExecutorWasBusy = false;
            ResetSelectedMonsterToSnapshot();
        }

        private void RefreshSelectedExecutor()
        {
            _selectedExecutorWasBusy = false;
            _selectedExecutor = null;

            if (SelectedMonster == null)
                return;

            _selectedExecutor = SelectedMonster.GetComponent<SkillExecutor>() ??
                                SelectedMonster.GetComponentInChildren<SkillExecutor>();
        }

        public void RegisterDamageArea(
            Vector3 center,
            Vector3 resolvedForward,
            in SkillAreaSpec area,
            float durationSeconds,
            GameObject caster)
        {
            if (!IsDamageAreaGizmoEnabled)
                return;

            var settings = CurrentSettings;
            float resolvedDuration = durationSeconds > 0f
                ? durationSeconds
                : settings != null ? settings.defaultDamageAreaGizmoDuration : 0.2f;

            var saneArea = area;
            saneArea.EnsureSaneDefaults();
            _activeDamageAreas.Add(SkillDebugAreaRecord.Create(center, resolvedForward, saneArea, resolvedDuration, caster));
        }

        public void ClearDamageAreas(GameObject caster)
        {
            if (_activeDamageAreas.Count == 0)
                return;

            int casterId = caster != null ? caster.GetInstanceID() : 0;
            for (int i = _activeDamageAreas.Count - 1; i >= 0; i--)
            {
                var area = _activeDamageAreas[i];
                if (area == null)
                {
                    _activeDamageAreas.RemoveAt(i);
                    continue;
                }

                if (caster == null || area.CasterInstanceId == casterId)
                    _activeDamageAreas.RemoveAt(i);
            }
        }


        /// <summary>
        /// 레이저 범위 기즈모를 등록합니다.
        /// </summary>
        /// <param name="start">레이저 시작점입니다.</param>
        /// <param name="end">레이저 종료점입니다.</param>
        /// <param name="durationSeconds">기즈모 유지 시간입니다.</param>
        /// <param name="caster">레이저를 생성한 캐스터 오브젝트입니다.</param>
        /// <param name="hasBlockHit">차단 지점 존재 여부입니다.</param>
        /// <param name="blockPoint">차단 지점입니다.</param>
        public void RegisterLaser(
            Vector3 start,
            Vector3 end,
            float durationSeconds,
            GameObject caster,
            bool hasBlockHit,
            Vector3 blockPoint)
        {
            if (!IsLaserGizmoEnabled)
                return;

            var settings = CurrentSettings;
            float resolvedDuration = durationSeconds > 0f
                ? durationSeconds
                : settings != null ? settings.defaultLaserGizmoDuration : 0.2f;

            _activeLasers.Add(SkillDebugLaserRecord.Create(start, end, resolvedDuration, caster, hasBlockHit, blockPoint));
        }

        /// <summary>
        /// 지정한 캐스터의 레이저 기즈모를 정리합니다.
        /// </summary>
        /// <param name="caster">정리할 캐스터입니다. null이면 전체를 정리합니다.</param>
        public void ClearLasers(GameObject caster)
        {
            if (_activeLasers.Count == 0)
                return;

            int casterId = caster != null ? caster.GetInstanceID() : 0;
            for (int i = _activeLasers.Count - 1; i >= 0; i--)
            {
                var laser = _activeLasers[i];
                if (laser == null)
                {
                    _activeLasers.RemoveAt(i);
                    continue;
                }

                if (caster == null || laser.CasterInstanceId == casterId)
                    _activeLasers.RemoveAt(i);
            }
        }

        public void SetDefaultSpawnPoint(Vector3 worldPos)
        {
            defaultSpawnPoint = worldPos;
            groundPoint = worldPos;
        }

        public bool TryBindPlayerAsTarget()
        {
            if (SceneGame.Instance != null && SceneGame.Instance.player != null)
            {
                lockedTarget = SceneGame.Instance.player.transform;
                groundPoint = lockedTarget.position;
                return true;
            }

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

        public void SetManualLockedTarget(Transform target)
        {
            lockedTarget = target;
            useManualLockedTarget = target != null;
            if (target != null)
                groundPoint = target.position;
        }

        public void ClearManualLockedTarget()
        {
            useManualLockedTarget = false;
        }

        public void SetForward(Vector2 dir)
        {
            if (dir.sqrMagnitude < 1e-6f)
                return;

            forward = dir.normalized;
        }

        private Task WaitNextFrameAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(CoWait());
            return tcs.Task;

            System.Collections.IEnumerator CoWait()
            {
                yield return null;
                tcs.TrySetResult(true);
            }
        }

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

            await SceneGame.Instance.AddressableLoaderPrefabCharacter.LoadCharacterByMonsterUid(monsterUid);

            if (lockedTarget == null && (CurrentSettings == null || CurrentSettings.autoBindPlayerAsTarget))
                TryBindPlayerAsTarget();

            if (lockedTarget != null)
            {
                var d = lockedTarget.position - pos;
                SetForward(new Vector2(d.x, d.y));
                groundPoint = lockedTarget.position;
            }

            bool flip = false;
            var dir = CharacterConstants.ToFacingDirection8(forward);
            if ((struckTableAnimation.DefaultFacingDirection8 == CharacterConstants.FacingDirection8.Right &&
                 dir is CharacterConstants.FacingDirection8.Left or CharacterConstants.FacingDirection8.DownLeft or CharacterConstants.FacingDirection8.UpLeft) ||
                (struckTableAnimation.DefaultFacingDirection8 == CharacterConstants.FacingDirection8.Left &&
                 dir is CharacterConstants.FacingDirection8.Right or CharacterConstants.FacingDirection8.DownRight or CharacterConstants.FacingDirection8.UpRight))
            {
                flip = true;
            }

            int mapUid = SceneGame.Instance.mapManager.GetCurrentMapUid();
            CharacterRegenData monsterData = new CharacterRegenData(monsterUid, pos, flip, mapUid, true);
            var monster = SceneGame.Instance.CharacterManager.CreateMonster(monsterUid, monsterData);
            if (monster == null)
                return null;

            EnsureSkillTestComponents(monster);
            monster.transform.position = pos;

            if (captureAfterStart)
                await WaitNextFrameAsync();

            CaptureSnapshot(monster);
            _spawned.Add(monster);
            SelectedMonster = monster;
            RefreshSelectedExecutor();
            return monster;
        }

        private static void EnsureSkillTestComponents(GameObject caster)
        {
            if (caster == null)
                return;

            if (caster.GetComponent<SkillExecutor>() == null)
                caster.AddComponent<SkillExecutor>();

            var character = caster.GetComponent<CharacterBase>();
            if (character != null && character.IsPlayer())
            {
                if (caster.GetComponent<PlayerSkillDriverAdapter>() == null)
                    caster.AddComponent<PlayerSkillDriverAdapter>();
            }
            else
            {
                if (caster.GetComponent<MonsterSkillDriverAdapter>() == null)
                    caster.AddComponent<MonsterSkillDriverAdapter>();
            }

            var brainTicker = caster.GetComponent<MonsterBrainTicker>();
            if (brainTicker != null)
                brainTicker.enabled = false;

            foreach (var mb in caster.GetComponents<MonoBehaviour>())
            {
                if (mb == null)
                    continue;

                if (mb.GetType().Name == "MonsterBtRunner")
                    mb.enabled = false;
            }
        }

        public void SelectCaster(GameObject caster, bool captureSnapshot = true)
        {
            if (caster == null)
                return;

            EnsureSkillTestComponents(caster);

            if (!_spawned.Contains(caster))
                _spawned.Add(caster);

            SelectedMonster = caster;

            if (captureSnapshot)
                CaptureSnapshot(caster);

            RefreshSelectedExecutor();
        }

        public void CaptureSnapshot(GameObject monster)
        {
            if (monster == null)
                return;

            int id = monster.GetInstanceID();
            if (!_snapshots.TryGetValue(id, out var snap) || snap == null)
            {
                snap = new SkillTestTargetSnapshot();
                _snapshots[id] = snap;
            }

            snap.Capture(monster);
        }

        public bool ResetSelectedMonsterToSnapshot()
        {
            if (SelectedMonster == null)
                return false;

            return ResetMonsterToSnapshot(SelectedMonster);
        }

        public bool ResetMonsterToSnapshot(GameObject monster)
        {
            if (monster == null)
                return false;

            int id = monster.GetInstanceID();
            if (!_snapshots.TryGetValue(id, out var snap) || snap == null)
                return false;

            snap.Apply(monster);
            return true;
        }

        public void DespawnMonster(GameObject monster)
        {
            if (monster == null)
                return;

            _snapshots.Remove(monster.GetInstanceID());
            _spawned.Remove(monster);
            ClearDamageAreas(monster);

            if (SceneGame.Instance != null && SceneGame.Instance.CharacterManager != null)
                SceneGame.Instance.CharacterManager.RemoveCharacter(monster);
            else
                Destroy(monster);

            if (SelectedMonster == monster)
            {
                SelectedMonster = _spawned.Count > 0 ? _spawned[^1] : null;
                RefreshSelectedExecutor();
            }
        }

        public void SelectMonster(GameObject monster)
        {
            if (monster == null)
                return;

            if (!_spawned.Contains(monster))
                _spawned.Add(monster);

            SelectedMonster = monster;
            RefreshSelectedExecutor();
        }
    }
}
#endif
