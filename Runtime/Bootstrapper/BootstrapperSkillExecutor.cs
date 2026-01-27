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
        }

        private void OnDisable()
        {
            CharacterManager.OnCharacterSpawned   -= OnCharacterSpawned;
            CharacterManager.OnCharacterDestroyed -= OnCharacterDestroyed;
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
            
            var monsterSkillDriverAdapter = ch.gameObject.GetComponent<MonsterSkillDriverAdapter>();
            if (monsterSkillDriverAdapter == null)
            {
                monsterSkillDriverAdapter = ch.gameObject.AddComponent<MonsterSkillDriverAdapter>();
            }
            monsterSkillDriverAdapter.SetSkillExecutor(skillExecutor);
        }

        private void OnCharacterDestroyed(CharacterBase ch)
        {
            // 필요 시 언바인드/풀 반환/로그 등 처리
        }
    }
}