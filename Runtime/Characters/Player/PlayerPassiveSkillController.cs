using System;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    public class PlayerPassiveSkillController : CharacterPassiveSkillController
    {
        private CharacterBase _character;
        private Action _onInitializedHandler;

        protected override void Start()
        {
            // CharacterBase 초기화(테이블/리소스 세팅) 이전에 패시브가 적용되면,
            // Player.InitializeByTable()의 startHp 세팅 로직에 의해 "패시브 증가분까지 채워진 것처럼" 보일 수 있습니다.
            // 따라서 CharacterBase 초기화 완료 이후에 세이브 데이터를 읽어 패시브를 적용합니다.
            _character = GetComponent<CharacterBase>();
            if (_character == null || _character.IsInitialized)
            {
                RefreshFromSaveData();
                return;
            }

            _onInitializedHandler = OnCharacterInitialized;
            _character.Initialized += _onInitializedHandler;
        }

        private void OnDestroy()
        {
            UnsubscribeInitialized();
        }

        private void OnCharacterInitialized()
        {
            UnsubscribeInitialized();
            RefreshFromSaveData();
        }

        private void UnsubscribeInitialized()
        {
            if (_character == null || _onInitializedHandler == null)
                return;

            _character.Initialized -= _onInitializedHandler;
            _onInitializedHandler = null;
        }

        /// <summary>
        /// 퀵슬롯 세이브 데이터에 저장된 패시브 장착 정보를 다시 읽어서 적용합니다.
        /// - 내부적으로 <see cref="SkillPackageManager"/>의 <see cref="SaveDataManagerSkill"/>을 참조합니다.
        /// </summary>
        public override void RefreshFromSaveData()
        {
            var mgr = SceneGame.Instance?.saveDataManager;
            if (mgr?.QuickSlot == null)
            {
                Clear();
                return;
            }
            ApplyEquippedPassives(mgr?.QuickSlot.GetAllSkillPassive());
            RebuildUsingPolicy();
        }
    }
}
