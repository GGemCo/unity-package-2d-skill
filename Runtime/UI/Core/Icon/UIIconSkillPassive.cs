using GGemCo2DCore;
using UnityEngine.EventSystems;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 아이콘
    /// </summary>
    public class UIIconSkillPassive : UIIcon, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private TableSkillPassive _tableSkillPassive;
        private StruckTableSkillPassive _struckTableSkillPassive;
        private UIWindowSkillInfo _uiWindowSkillInfo;
        private SkillData _skillData;
        protected override void Awake()
        {
            base.Awake();
            IconType = IconConstants.Type.SkillPassive;
            _tableSkillPassive = TableLoaderManagerSkill.Instance.TableSkillPassive;
            _struckTableSkillPassive = null;
        }

        protected override void Start()
        {
            base.Start();
            _uiWindowSkillInfo =
                SceneGame.Instance.uIWindowManager.GetUIWindowByUid<UIWindowSkillInfo>(
                    UIWindowConstants.WindowUid.SkillInfo);
            _skillData = SkillPackageManager.Instance.SaveDataManagerSkill.Skill;
        }

        /// <summary>
        /// 다른 uid 로 변경하기
        /// </summary>
        /// <param name="iconUid"></param>
        /// <param name="iconCount"></param>
        /// <param name="iconLevel"></param>
        /// <param name="iconIsLearn"></param>
        /// <param name="remainCoolTime"></param>
        /// <param name="iconInstanceId"></param>
        /// <param name="iconType"></param>
        public override bool ChangeInfoByUid(int iconUid, int iconCount = 0, int iconLevel = 0,
            bool iconIsLearn = false, int remainCoolTime = 0, long iconInstanceId = 0, IconConstants.Type iconType = IconConstants.Type.None)
        {
            if (!base.ChangeInfoByUid(iconUid, iconCount, iconLevel, iconIsLearn, remainCoolTime, iconInstanceId, iconType))
                return false;
            // todo. 정리 필요. Level 정보
            var info = _tableSkillPassive.GetDataByUid(iconUid);
            if (info == null)
            {
                GcLogger.LogError("스킬 테이블에 없는 아이템 입니다.");
                return false;
            }

            _struckTableSkillPassive = info;
            UpdateInfo();
            return true;
        }

        /// <summary>
        /// 아이콘 이미지 업데이트 하기
        /// </summary>
        protected override void UpdateIconImage()
        {
            if (ImageIcon == null) return;
            string path = GetIconImagePath();
            if (string.IsNullOrEmpty(path))
            {
                ImageIcon.sprite = null;
                return;
            }

            ImageIcon.sprite = AddressableLoaderSkill.Instance.GetSkillPassiveIconImageByName(path);
        }
        /// <summary>
        /// 아이콘 이미지 경로 가져오기 
        /// </summary>
        /// <returns></returns>
        protected override string GetIconImagePath()
        {
            if (_struckTableSkillPassive == null) return null;
            return _struckTableSkillPassive.IconFileName;
        }
        public override bool CheckRequireLevel()
        {
            // todo. 정리 필요
            // return SceneGame.Instance.player.GetComponent<Player>().IsRequireLevel(struckSkill.NeedPlayerLevel);
            return true;
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            // GcLogger.Log("OnPointerEnter "+eventData);
            // uiWindowSkillInfo.SetSkillUid(uid);
            ShowOverImage(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // GcLogger.Log("OnPointerExit "+eventData);
            // uiWindowSkillInfo.Show(false);
            ShowOverImage(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Left)
            {
                if (!window) return;
                window.SetSelectedIcon(index);
            }
            else if(eventData.button == PointerEventData.InputButton.Middle)
            {
            }
            else if(eventData.button == PointerEventData.InputButton.Right)
            {
                if (uid <= 0 || GetCount() <= 0) return;
                window.OnRightClick(this);
            }
        }

        public StruckTableSkillPassive GetTableInfo()
        {
            return _struckTableSkillPassive;
        }

        public SaveDataIcon GetSaveDataInfo()
        {
            return _skillData.GetData(slotIndex);
        }
    }
}