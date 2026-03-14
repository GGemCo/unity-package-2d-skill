using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 캐릭터 생성/파괴 이벤트와 맵 전환 훅을 구독하여
    /// 스킬 실행 관련 컴포넌트를 자동으로 부착하고 프리로드를 수행합니다.
    /// </summary>
    public class BootstrapperSkillExecutor : MonoBehaviour
    {
        /// <summary>
        /// 대상 캐릭터에 필요한 스킬 실행 컴포넌트가 없을 때 자동으로 추가할지 여부입니다.
        /// </summary>
        [SerializeField] private bool addIfMissing = true;

        /// <summary>
        /// 오브젝트가 활성화될 때 캐릭터 및 맵 관련 이벤트를 구독합니다.
        /// </summary>
        private void OnEnable()
        {
            CharacterManager.OnCharacterSpawned += OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed += OnCharacterDestroyed;

            // MapLoadCharacters가 스폰 완료 대기를 위해 호출하는 비동기 Hook입니다.
            CharacterSpawnHooks.OnCharacterSpawnedAsync += OnCharacterSpawnedAsync;
            CharacterSpawnHooks.OnMapUnload += OnMapUnload;
        }

        /// <summary>
        /// 오브젝트가 비활성화될 때 등록한 이벤트 구독을 해제합니다.
        /// </summary>
        private void OnDisable()
        {
            CharacterManager.OnCharacterSpawned -= OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed -= OnCharacterDestroyed;

            CharacterSpawnHooks.OnCharacterSpawnedAsync -= OnCharacterSpawnedAsync;
            CharacterSpawnHooks.OnMapUnload -= OnMapUnload;
        }

        /// <summary>
        /// 캐릭터가 생성되었을 때 스킬 실행기와 캐릭터 유형별 드라이버를 자동으로 부착합니다.
        /// 플레이어인 경우 패시브 스킬 컨트롤러도 함께 구성합니다.
        /// </summary>
        /// <param name="ch">스폰된 캐릭터 인스턴스입니다.</param>
        private void OnCharacterSpawned(CharacterBase ch)
        {
            if (!addIfMissing) return;

# if GGEMCO_USE_SPINE

#else

#endif
            // 스킬 실행 컴포넌트를 보장합니다.
            var skillExecutor = ch.gameObject.GetComponent<SkillExecutor>();
            if (skillExecutor == null)
                skillExecutor = ch.gameObject.AddComponent<SkillExecutor>();

            // 플레이어 전용 패시브 스킬 컨트롤러를 보장합니다.
            if (ch.IsPlayer())
            {
                var passive = ch.gameObject.GetComponent<PlayerPassiveSkillController>();
                if (passive == null) ch.gameObject.AddComponent<PlayerPassiveSkillController>();
            }

            // 캐릭터 유형에 맞는 스킬 드라이버를 연결합니다.
            if (ch.IsPlayer())
            {
                var playerSkillDriverAdapter = ch.gameObject.GetComponent<PlayerSkillDriverAdapter>();
                if (playerSkillDriverAdapter == null)
                    playerSkillDriverAdapter = ch.gameObject.AddComponent<PlayerSkillDriverAdapter>();

                playerSkillDriverAdapter.SetSkillExecutor(skillExecutor);
            }
            else if (ch.IsMonster())
            {
                var monsterSkillDriverAdapter = ch.gameObject.GetComponent<MonsterSkillDriverAdapter>();
                if (monsterSkillDriverAdapter == null)
                    monsterSkillDriverAdapter = ch.gameObject.AddComponent<MonsterSkillDriverAdapter>();

                monsterSkillDriverAdapter.SetSkillExecutor(skillExecutor);
            }
        }

        /// <summary>
        /// 몬스터 생성 후 연결된 스킬의 RuntimeSequence를 미리 로드하여
        /// 첫 사용 시 발생할 수 있는 hitch를 줄입니다.
        /// </summary>
        /// <param name="ch">스폰이 완료된 캐릭터 인스턴스입니다.</param>
        /// <returns>프리로드 작업이 완료되면 종료되는 비동기 작업입니다.</returns>
        private async Task OnCharacterSpawnedAsync(CharacterBase ch)
        {
            // 몬스터가 아니면 프리로드하지 않습니다.
            if (ch == null || !ch.IsMonster())
                return;

            if (TableLoaderManager.Instance == null || TableLoaderManager.Instance.TableMonster == null)
                return;

            var info = TableLoaderManager.Instance.TableMonster.GetDataByUid(ch.uid);
            if (GcLogger.IsNull(info, $"몬스터 테이블에 정보가 없습니다. uid: {ch.uid}"))
                return;

            if (info.SkillUid == null || info.SkillUid.Length == 0)
                return;

            // 중복되거나 유효하지 않은 스킬 UID를 제거합니다.
            var uniqueSkillUids = new HashSet<int>();
            foreach (var uid in info.SkillUid)
            {
                if (uid <= 0) continue;
                uniqueSkillUids.Add(uid);
            }

            if (uniqueSkillUids.Count == 0)
                return;

            // 각 스킬의 RuntimeSequence를 병렬로 프리로드합니다.
            var tasks = new List<Task>(uniqueSkillUids.Count);
            foreach (var skillUid in uniqueSkillUids)
            {
                var key = ConfigAddressableKeySkill.GetRuntimeSequenceKey(skillUid);
                if (string.IsNullOrEmpty(key)) continue;

                tasks.Add(PreloadSequenceSafeAsync(skillUid, key));
            }

            if (tasks.Count == 0)
                return;

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                // 개별 Task 내부에서 예외를 처리하므로 집계 예외는 무시합니다.
            }
        }

        /// <summary>
        /// 지정한 스킬의 RuntimeSequence 에셋을 안전하게 프리로드합니다.
        /// 실패 시 예외를 전파하지 않고 로그만 기록합니다.
        /// </summary>
        /// <param name="skillUid">프리로드 대상 스킬의 고유 식별자입니다.</param>
        /// <param name="key">Addressables에서 사용할 RuntimeSequence 키입니다.</param>
        /// <returns>프리로드 작업이 완료되면 종료되는 비동기 작업입니다.</returns>
        /// <exception cref="Exception">
        /// 내부 로드 과정에서 예외가 발생할 수 있으나, 메서드 내부에서 처리 후 로그만 남깁니다.
        /// </exception>
        private static async Task PreloadSequenceSafeAsync(int skillUid, string key)
        {
            try
            {
                // SkillRun에서 사용 중인 Repository 캐시를 그대로 활용합니다.
                await AddressableLoaderSkillRuntimeSequence.LoadAsync(key);
            }
            catch (Exception e)
            {
                GcLogger.LogException(e);
                GcLogger.LogError($"[Skill] RuntimeSequence preload failed. skillUid={skillUid} key={key}");
            }
        }

        /// <summary>
        /// 맵 언로드 시점에 캐시된 RuntimeSequence 핸들을 모두 해제합니다.
        /// </summary>
        private void OnMapUnload()
        {
            // 정책: 맵 언로드 시 Addressables 핸들을 해제합니다.
            AddressableLoaderSkillRuntimeSequence.ReleaseAll();
        }

        /// <summary>
        /// 캐릭터가 제거될 때 후처리를 수행할 수 있는 지점입니다.
        /// 현재는 별도의 정리 작업을 수행하지 않습니다.
        /// </summary>
        /// <param name="ch">제거된 캐릭터 인스턴스입니다.</param>
        private void OnCharacterDestroyed(CharacterBase ch)
        {
            // 필요 시 언바인드, 풀 반환, 로그 기록 등을 처리합니다.
        }
    }
}