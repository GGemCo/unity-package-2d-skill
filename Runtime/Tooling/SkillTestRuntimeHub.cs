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

        public async Task<GameObject> SpawnMonster(int monsterUid)
        {
            if (SceneGame.Instance == null || SceneGame.Instance.mapManager == null)
                return null;

            var pos = defaultSpawnPoint + (Vector3)(Random.insideUnitCircle * spawnRadius);
            if (defaultSpawnPoint == Vector3.zero)
            {
                pos = SceneGame.Instance.player.transform.position + new Vector3(50, 0, 0);
            }

            // 1) 프리팹 로드 완료까지 대기
            //    ※ 아래 타입 인자는 실제 LoadCharacterByMonsterUid 반환/Result 타입에 맞춰 조정하세요.
            await SceneGame.Instance.AddressableLoaderPrefabCharacter.LoadCharacterByMonsterUid(monsterUid);

            // 2) 로드 완료 후 생성/배치/선택 로직 수행
            var monster = SceneGame.Instance.CharacterManager.CreateMonster(monsterUid);
            if (monster == null) return null;

            EnsureSkillTestComponents(monster);

            monster.transform.position = pos;
            _spawned.Add(monster);
            SelectedMonster = monster;

            // 타겟(Player) 기준 forward 갱신
            if (lockedTarget == null) TryBindPlayerAsTarget();
            if (lockedTarget != null)
            {
                var d = lockedTarget.position - monster.transform.position;
                SetForward(new Vector2(d.x, d.y));
                groundPoint = lockedTarget.position;
            }

            return monster;
        }

        /// <summary>
        /// 스킬 테스트를 위해 필요한 컴포넌트들을 자동으로 추가합니다.
        /// - SkillExecutor
        /// - SkillAnimationPlayer
        /// - MonsterSkillDriverAdapter
        /// </summary>
        private static void EnsureSkillTestComponents(GameObject caster)
        {
            if (caster == null) return;

            // Runtime Skill components
            if (caster.GetComponent<GGemCo2DSkill.SkillAnimationPlayer>() == null)
                caster.AddComponent<GGemCo2DSkill.SkillAnimationPlayer>();

            if (caster.GetComponent<GGemCo2DSkill.SkillExecutor>() == null)
                caster.AddComponent<GGemCo2DSkill.SkillExecutor>();

            if (caster.GetComponent<GGemCo2DSkill.MonsterSkillDriverAdapter>() == null)
                caster.AddComponent<GGemCo2DSkill.MonsterSkillDriverAdapter>();

            // Editor Gizmo: Damage 클립 구간 동안 데미지 영역을 표시
            if (caster.GetComponent<SkillDamageAreaGizmo>() == null)
                caster.AddComponent<SkillDamageAreaGizmo>();
        }

        public void DespawnMonster(GameObject monster)
        {
            if (monster == null) return;
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
        }
    }
}
#endif