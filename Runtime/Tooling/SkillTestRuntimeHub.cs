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
        [SerializeField] private Transform lockedTarget;
        [SerializeField] private Vector3 groundPoint;
        [SerializeField] private Vector2 forward = Vector2.right;

        private readonly List<GameObject> _spawned = new();
        public IReadOnlyList<GameObject> Spawned => _spawned;

        public GameObject SelectedMonster { get; private set; }

        public Transform LockedTarget => lockedTarget;
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
            groundPoint = defaultSpawnPoint;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetDefaultSpawnPoint(Vector3 worldPos)
        {
            defaultSpawnPoint = worldPos;
            groundPoint = worldPos;
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

            // 1) 프리팹 로드 완료까지 대기
            //    ※ 아래 타입 인자는 실제 LoadCharacterByMonsterUid 반환/Result 타입에 맞춰 조정하세요.
            await SceneGame.Instance.AddressableLoaderPrefabCharacter.LoadCharacterByMonsterUid(monsterUid);

            // 2) 로드 완료 후 생성/배치/선택 로직 수행
            var monster = SceneGame.Instance.CharacterManager.CreateMonster(monsterUid);
            if (monster == null) return null;

            monster.transform.position = pos;
            _spawned.Add(monster);
            SelectedMonster = monster;

            if (lockedTarget != null)
            {
                var d = lockedTarget.position - monster.transform.position;
                SetForward(new Vector2(d.x, d.y));
            }

            return monster;
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