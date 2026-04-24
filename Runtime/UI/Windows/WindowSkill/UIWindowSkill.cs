using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 스킬 윈도우
    /// </summary>
    public class UIWindowSkill : UIWindow
    {
        [Header(UIWindowConstants.TitleHeaderIndividual)] [Tooltip("스킬 Element 프리팹")]
        public GameObject prefabUIElementSkill;

        private TableSkill _tableSkill;
        private readonly Dictionary<int, UIElementSkill> _uiElementSkills = new Dictionary<int, UIElementSkill>();

        private QuickSlotData _quickSlotData;
        private SkillData _skillData;

        private UIWindowQuickSlot _uiWindowQuickSlot;
        private UIWindowSkillInfo _uIWindowSkillInfo;

        protected override void Awake()
        {
            _uiElementSkills.Clear();
            uid = UIWindowConstants.WindowUid.Skill;
            if (TableLoaderManager.Instance == null) return;
            _tableSkill = TableLoaderManagerSkill.Instance.TableSkill;
            maxCountIcon = _tableSkill.GetDatas().Count;

            // 순서 중요: IconPoolManager에서 사용 (슬롯 빌드 전략 등록 후 base.Awake 호출)
            SlotIconBuildStrategyRegistry.Register(uid, window => new SlotIconBuildStrategySkill(_tableSkill, _uiElementSkills));

            base.Awake();
            IconPoolManager.SetSetIconHandler(new SetIconHandlerSkill());
            DragDropHandler.SetStrategy(new DragDropStrategySkill());
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
            UIElementSkill uiElementSkill = _uiElementSkills[index];
            if (uiElementSkill == null) return;
            Vector3 position = uiElementSkill.GetIconPosition();
            if (position == Vector3.zero) return;
            slot.transform.localPosition = position;
        }

        public override void OnShow(bool show)
        {
            if (SceneGame.Instance == null || TableLoaderManagerSkill.Instance == null) return;
            base.OnShow(show);
            if (!show)
            {
                _uIWindowSkillInfo?.Show(false);
                return;
            }

            LoadIcons();
        }

        /// <summary>
        /// 저장되어있는 스킬 정보로 아이콘 셋팅하기
        /// 스킬창이 열려있지 않으면 업데이트 하지 않음
        /// </summary>
        private void LoadIcons()
        {
            if (!gameObject.activeSelf) return;
            var datas = _skillData.GetAllDatas();
            if (datas == null) return;
            for (int index = 0; index < maxCountIcon; index++)
            {
                if (index >= icons.Length) continue;
                var icon = icons[index];
                if (icon == null) continue;
                UIIconSkill uiIcon = icon.GetComponent<UIIconSkill>();
                if (uiIcon == null) continue;
                
                // todo. 정리 필요. 다음 Level 정보
                var info = _tableSkill.GetDataByUid(uiIcon.uid);
                if (info == null) continue;
                
                SaveDataIcon saveDataIcon = datas.GetValueOrDefault(index);
                if (saveDataIcon == null)
                {
                    _skillData.SetSkillLearn(index, info.Uid, 1, 1, info.DefaultLearn);
                    saveDataIcon = datas.GetValueOrDefault(index);
                }

                UIElementSkill uiElementSkill = _uiElementSkills[index];
                if (uiElementSkill != null)
                {
                    uiElementSkill.UpdateInfos(saveDataIcon);
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

        public UIElementSkill GetElementSkillByIndex(int slotIndex)
        {
            return _uiElementSkills[slotIndex];
        }

    }
}