using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬 장착 슬롯 윈도우
    /// - SkillData.PassiveEquipDatas(slotIndex -> SaveDataIcon)를 시각화한다.
    /// - 실제 장착/해제 로직은 SkillData 갱신 + CharacterPassiveSkillController.RefreshFromSaveData()로 반영한다.
    /// </summary>
    public class UIWindowSkillPassive : UIWindow
    {
        [Header(UIWindowConstants.TitleHeaderIndividual)] [Tooltip("패시브 스킬 Element 프리팹")]
        public GameObject prefabUIElementSkill;
        
        private TableSkillPassive _tableSkillPassive;
        private readonly Dictionary<int, UIElementSkillPassive> _uiElementPassiveSkills = new Dictionary<int, UIElementSkillPassive>();
        
        private QuickSlotData _quickSlotData;
        private SkillData _skillData;
        
        private UIWindowQuickSlot _uiWindowQuickSlot;
        private UIWindowSkillInfo _uIWindowSkillInfo;
        private CharacterPassiveSkillController _characterPassiveSkillController;
        
        protected override void Awake()
        {
            _uiElementPassiveSkills.Clear();
            uid = UIWindowConstants.WindowUid.SkillPassive;
            if (TableLoaderManagerSkill.Instance == null) return;
            _tableSkillPassive = TableLoaderManagerSkill.Instance.TableSkillPassive;
            maxCountIcon = _tableSkillPassive.GetDatas().Count;

            // 기본 슬롯/아이콘 전략(DefaultSlotIconBuildStrategy)을 사용한다.
            if (iconType == IconConstants.Type.None)
                iconType = IconConstants.Type.Skill;
            // 순서 중요: IconPoolManager에서 사용 (슬롯 빌드 전략 등록 후 base.Awake 호출)
            SlotIconBuildStrategyRegistry.Register(uid, window => new SlotIconBuildStrategySkillPassive(_tableSkillPassive, _uiElementPassiveSkills));

            base.Awake();

            IconPoolManager.SetSetIconHandler(new SetIconHandlerSkillPassive());
            DragDropHandler.SetStrategy(new DragDropStrategySkillPassive());
        }

        protected override void Start()
        {
            base.Start();
            _quickSlotData = SceneGame.saveDataManager.QuickSlot;
            _skillData = SkillPackageManager.Instance.SaveDataManagerSkill.Skill;
            _uIWindowSkillInfo =
                SceneGame.uIWindowManager.GetUIWindowByUid<UIWindowSkillInfo>(UIWindowConstants.WindowUid
                    .SkillInfo);
            _uiWindowQuickSlot =
                SceneGame.uIWindowManager.GetUIWindowByUid<UIWindowQuickSlot>(UIWindowConstants.WindowUid
                    .QuickSlot);
        }

        /// <summary>
        /// 슬롯 위치 정해주기
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="index"></param>
        public void SetPositionUiSlot(UISlot slot, int index)
        {
            UIElementSkillPassive uiElementSkillPassive = _uiElementPassiveSkills[index];
            if (uiElementSkillPassive == null) return;
            Vector3 position = uiElementSkillPassive.GetIconPosition();
            if (position == Vector3.zero) return;
            slot.transform.localPosition = position;
        }
        public override void OnShow(bool show)
        {
            if (SceneGame.Instance == null || TableLoaderManager.Instance == null) return;
            if (!show)
            {
                _uIWindowSkillInfo?.Show(false);
                return;
            }

            LoadIcons();
        }
        /// <summary>
        /// 저장되어있는 패시브 장착 정보로 아이콘 셋팅
        /// </summary>
        public void LoadIcons()
        {
            if (!gameObject.activeSelf) return;

            var skillData = SkillPackageManager.Instance?.SaveDataManagerSkill?.Skill;
            if (skillData == null) return;

            var datas = skillData.GetAllPassive();
            for (int index = 0; index < maxCountIcon; index++)
            {
                if (index >= icons.Length) continue;
                var icon = icons[index];
                if (icon == null) continue;
                UIIconSkillPassive uiIcon = icon.GetComponent<UIIconSkillPassive>();
                if (uiIcon == null) continue;
                
                // todo. 정리 필요. 다음 Level 정보
                var info = _tableSkillPassive.GetDataByUid(uiIcon.uid);
                if (info == null) continue;
                
                SaveDataIcon saveDataIcon = datas.GetValueOrDefault(index);
                if (saveDataIcon == null)
                {
                    _skillData.SetSkillPassiveLearn(index, info.Uid, 1, 1, info.DefaultLearn);
                    saveDataIcon = datas.GetValueOrDefault(index);
                }

                UIElementSkillPassive uiElementPassiveSkill = _uiElementPassiveSkills[index];
                if (uiElementPassiveSkill != null)
                {
                    uiElementPassiveSkill.UpdateInfos(saveDataIcon);
                }
            }
        }
        /// <summary>
        /// 아이콘 우클릭했을때 처리 
        /// </summary>
        /// <param name="icon"></param>
        public override void OnRightClick(UIIcon icon)
        {
            if (icon == null) return;
        }

        public UIElementSkillPassive GetElementSkillByIndex(int slotIndex)
        {
            return _uiElementPassiveSkills[slotIndex];
        }
    }
}
