using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Core의 캐릭터 생성 이벤트를 구독하여 ControlBase 를 자동 부착
    /// </summary>
    public class BootstrapperSkillExecutor : MonoBehaviour
    {
        [SerializeField] private bool addIfMissing = true;

        private void OnEnable()
        {
            CharacterManager.OnCharacterSpawned   += OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed += OnCharacterDestroyed;
            
            // MapLoadCharacters가 스폰 완료 대기를 위해 호출하는 비동기 Hook.
            CharacterSpawnHooks.OnCharacterSpawnedAsync += OnCharacterSpawnedAsync;
            CharacterSpawnHooks.OnMapUnload += OnMapUnload;
        }

        private void OnDisable()
        {
            CharacterManager.OnCharacterSpawned   -= OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed -= OnCharacterDestroyed;
            
            CharacterSpawnHooks.OnCharacterSpawnedAsync -= OnCharacterSpawnedAsync;
            CharacterSpawnHooks.OnMapUnload -= OnMapUnload;
        }

        private void OnCharacterSpawned(CharacterBase ch)
        {
            if (!addIfMissing) return;
# if GGEMCO_USE_SPINE
            
#else

#endif
            // 스킬 컴포넌트 추가하기
            var skillExecutor = ch.gameObject.GetComponent<SkillExecutor>();
            if (skillExecutor == null) ch.gameObject.AddComponent<SkillExecutor>();

            // 패시브 스킬 컨트롤러(플레이어만)
            if (ch.IsPlayer())
            {
                var passive = ch.gameObject.GetComponent<PlayerPassiveSkillController>();
                if (passive == null) ch.gameObject.AddComponent<PlayerPassiveSkillController>();
            }
            
            var monsterSkillDriverAdapter = ch.gameObject.GetComponent<MonsterSkillDriverAdapter>();
            if (monsterSkillDriverAdapter == null)
            {
                monsterSkillDriverAdapter = ch.gameObject.AddComponent<MonsterSkillDriverAdapter>();
            }
            monsterSkillDriverAdapter.SetSkillExecutor(skillExecutor);
        }

        private async Task OnCharacterSpawnedAsync(CharacterBase ch)
        {
            // 몬스터 스폰 시, 연결된 스킬의 RuntimeSequence를 선 로드(캐시)하여 첫 사용 hitch를 줄입니다.
            // 정책: 실패는 로그만 남기고 다음 단계로 진행합니다.
            if (ch == null || !ch.IsMonster())
                return;

            if (TableLoaderManager.Instance == null || TableLoaderManager.Instance.TableMonster == null)
                return;

            var info = TableLoaderManager.Instance.TableMonster.GetDataByUid(ch.uid);
            if (GcLogger.IsNull(info, $"몬스터 테이블에 정보가 없습니다. uid: {ch.uid}"))
                return;

            if (info.SkillUid == null || info.SkillUid.Length == 0)
                return;

            // 1) 중복/0 스킬 제거
            var uniqueSkillUids = new HashSet<int>();
            foreach (var uid in info.SkillUid)
            {
                if (uid <= 0) continue;
                uniqueSkillUids.Add(uid);
            }

            if (uniqueSkillUids.Count == 0)
                return;

            // 2) 병렬 프리로드
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
                // 개별 task에서 예외를 처리하므로, 집계 예외가 오더라도 무시(정책).
            }
        }
        private static async Task PreloadSequenceSafeAsync(int skillUid, string key)
        {
            try
            {
                // SkillRun에서 사용 중인 Repository 캐시를 그대로 활용하는 것이 일관성이 좋습니다.
                await AddressableLoaderSkillRuntimeSequence.LoadAsync(key);
            }
            catch (Exception e)
            {
                GcLogger.LogException(e);
                GcLogger.LogError($"[Skill] RuntimeSequence preload failed. skillUid={skillUid} key={key}");
            }
        }
        private void OnMapUnload()
        {
            // 정책: 맵 언로드 시 Addressables 핸들 해제
            AddressableLoaderSkillRuntimeSequence.ReleaseAll();
        }
        private void OnCharacterDestroyed(CharacterBase ch)
        {
            // 필요 시 언바인드/풀 반환/로그 등 처리
        }
    }
}