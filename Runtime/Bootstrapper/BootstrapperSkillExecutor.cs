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

                var playerLockedTargetProvider = ch.gameObject.GetComponent<PlayerLockedTargetProvider>();
                if (playerLockedTargetProvider == null)
                    ch.gameObject.AddComponent<PlayerLockedTargetProvider>();

                var playerSkillTargetingProvider = ch.gameObject.GetComponent<PlayerSkillTargetingProvider>();
                if (playerSkillTargetingProvider == null)
                    ch.gameObject.AddComponent<PlayerSkillTargetingProvider>();

                playerSkillDriverAdapter.SetSkillExecutor(skillExecutor);

                // TimingBattle 등 상위 프로젝트 부트스트랩이 콤보 컨트롤러를 먼저 붙인 경우,
                // SkillExecutor 생성 후 종료 이벤트 구독을 다시 보장합니다.
                PlayerSkillComboController comboController = ch.gameObject.GetComponent<PlayerSkillComboController>();
                comboController?.RefreshRuntimeReferences();
            }
            else if (ch.IsMonster())
            {
                var monsterSkillDriverAdapter = ch.gameObject.GetComponent<MonsterSkillDriverAdapter>();
                if (monsterSkillDriverAdapter == null)
                    monsterSkillDriverAdapter = ch.gameObject.AddComponent<MonsterSkillDriverAdapter>();

                monsterSkillDriverAdapter.SetSkillExecutor(skillExecutor);
            }
            
            // 차징 게이지 Presenter를 보장하고, SkillExecutor 이벤트와 캐릭터 위치 추적 기준을 연결합니다.
            var skillChargeGaugePresenter = ch.gameObject.GetComponent<SkillChargeGaugePresenter>();
            if (skillChargeGaugePresenter == null)
                skillChargeGaugePresenter = ch.gameObject.AddComponent<SkillChargeGaugePresenter>();
            skillChargeGaugePresenter.Initialize(ch, skillExecutor);
        }

        /// <summary>
        /// 몬스터 생성 후 연결된 스킬의 RuntimeSequence를 미리 로드하여
        /// 첫 사용 시 발생할 수 있는 hitch를 줄입니다.
        /// </summary>
        /// <param name="ch">스폰이 완료된 캐릭터 인스턴스입니다.</param>
        /// <returns>프리로드 작업이 완료되면 종료되는 비동기 작업입니다.</returns>
        private async Task OnCharacterSpawnedAsync(CharacterBase ch)
        {
            if (ch == null || !ch.IsMonster()) return;
            await LoadSkillRuntimeSequenceMonster(ch);
        }

        private async Task LoadSkillRuntimeSequenceMonster(CharacterBase ch)
        {
            // 몬스터가 아니면 프리로드하지 않습니다.
            if (ch == null || !ch.IsMonster())
                return;

            if (TableLoaderManager.Instance == null || TableLoaderManager.Instance.TableMonster == null)
                return;

            var info = TableLoaderManager.Instance.TableMonster.GetDataByUid(ch.uid);
            if (GcLogger.IsNull(info, $"몬스터 테이블에 정보가 없습니다. uid: {ch.uid}"))
                return;

            if (info.SkillMonsterUid == null || info.SkillMonsterUid.Length == 0)
                return;

            // 중복되거나 유효하지 않은 스킬 UID를 제거합니다.
            var uniqueSkillUids = new HashSet<int>();
            foreach (var uid in info.SkillMonsterUid)
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
                var key = ConfigAddressableKeySkill.GetRuntimeSequenceKeyMonster(skillUid);
                if (string.IsNullOrEmpty(key)) continue;

                tasks.Add(PreloadSequenceSafeAsyncMonster(skillUid, key));
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

        private static async Task PreloadSequenceSafeAsyncMonster(int skillUid, string key)
        {
            try
            {
                // SkillRun에서 사용 중인 Repository 캐시를 그대로 활용합니다.
                await AddressableLoaderSkillRuntimeSequenceMonster.LoadAsyncMonster(key);
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
            AddressableLoaderSkillRuntimeSequenceMonster.ReleaseAllMonster();
        }

        /// <summary>
        /// 캐릭터가 제거될 때 Presenter가 생성한 차징 게이지 UI 인스턴스를 정리합니다.
        /// </summary>
        /// <param name="ch">제거된 캐릭터 인스턴스입니다.</param>
        private void OnCharacterDestroyed(CharacterBase ch)
        {
            if (ch == null)
                return;

            var skillChargeGaugePresenter = ch.GetComponent<SkillChargeGaugePresenter>();
            skillChargeGaugePresenter?.ReleaseGaugeView();
        }
    }
}