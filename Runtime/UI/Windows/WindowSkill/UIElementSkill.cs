using GGemCo2DCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 스킬 윈도우 - 스킬 리스트 element
    /// </summary>
    public class UIElementSkill : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler 
    {
        public Vector3 iconPosition;
        public TextMeshProUGUI textName;
        public TextMeshProUGUI textLevel;
        public TextMeshProUGUI textNeedLevel;
        public TextMeshProUGUI textNeedCurrency;
        public Button buttonLearn;
        public Button buttonLevelUp;
        
        private UIWindowSkill _uiWindowSkill;
        private UIWindowSkillInfo _uiWindowSkillInfo;
        private StruckTableSkill _struckTableSkill;
        private SaveDataIcon _saveDataIcon;
        private TableSkill _tableSkill;
        private int _slotIndex;

        private LocalizationManagerSkill _localizationManagerSkill;
        
        /// <summary>
        /// 초기화
        /// </summary>
        /// <param name="uiWindowSkill"></param>
        /// <param name="slotIndex"></param>
        /// <param name="struckTableSkill"></param>
        public void Initialize(UIWindowSkill uiWindowSkill, int slotIndex, StruckTableSkill struckTableSkill)
        {
            _slotIndex = slotIndex;
            _struckTableSkill = struckTableSkill;
            if (buttonLearn != null)
            {
                buttonLearn.gameObject.SetActive(false);
                buttonLearn.onClick.AddListener(OnClickLearn);
            }
            if (buttonLevelUp != null)
            {
                buttonLevelUp.gameObject.SetActive(false);
                buttonLevelUp.onClick.AddListener(OnClickLevelUp);
            }

            _uiWindowSkill = uiWindowSkill;
            _tableSkill = TableLoaderManagerSkill.Instance.TableSkill;
            _localizationManagerSkill = LocalizationManagerSkill.Instance;
            
            if (textName != null) textName.text = _struckTableSkill.Name;
            
            // todo. 정리 필요
            textNeedLevel.gameObject.SetActive(false);
            textNeedCurrency.gameObject.SetActive(false);
        }
        
        private void Start()
        {
            _uiWindowSkillInfo =
                SceneGame.Instance.uIWindowManager.GetUIWindowByUid<UIWindowSkillInfo>(
                    UIWindowConstants.WindowUid.SkillInfo);
        }

        /// <summary>
        /// slotIndex 로 아이템 정보를 가져온다.
        /// SaveDataIcon 정보에 따라 버튼 visible 업데이트
        /// </summary>
        public void UpdateInfos(SaveDataIcon saveDataIcon)
        {
            if (saveDataIcon == null)
            {
                GcLogger.LogError($"저장된 정보가 없습니다.");
                return;
            }

            // 안배운 상태
            if (!saveDataIcon.IsLearned)
            {
                var icon = _uiWindowSkill.GetIconByIndex(_slotIndex);
                if (icon)
                {
                    icon.SetIconLock(true);
                }
                if (buttonLearn)
                    buttonLearn.gameObject.SetActive(true);
            }
            return;

            // todo. 정리 필요
            int level = saveDataIcon?.Level ?? 1;
            if (textLevel != null) textLevel.text = $"Lv.{level}";
            if (textNeedLevel != null)
            {
                textNeedLevel.text = string.Format(_localizationManagerSkill.GetUIWindowSkillInfoByKey("Text_NeedLevel"), _struckTableSkill.NeedPlayerLevel);
            }

            // 필요 재화
            if (textNeedCurrency != null)
            {
                textNeedCurrency.gameObject.SetActive(false);
                // textNeedCurrency.text = $"{_struckTableSkill.NeedCurrencyType} {_struckTableSkill.NeedCurrencyValue}";
                // if (_struckTableSkill.NeedCurrencyType == CurrencyConstants.Type.None)
                // {
                //     textNeedCurrency.gameObject.SetActive(false);
                // }
            }
            
            // 최대 레벨
            int maxLevel = 1; //_struckTableSkill.MaxLevel;
            if (saveDataIcon != null && _struckTableSkill != null && saveDataIcon.Level >= maxLevel)
            {
                buttonLearn.gameObject.SetActive(false);
                textNeedLevel.gameObject.SetActive(false);
                buttonLevelUp.gameObject.SetActive(true);
                buttonLevelUp.GetComponentInChildren<TextMeshProUGUI>().text = _localizationManagerSkill.GetUIWindowSkillByKey("Element_Text_MaxLevel");
                buttonLevelUp.interactable = false;
            }
            // 레벨업 할때는 다음 레벨 정보로 셋팅
            else if (saveDataIcon is { IsLearned: true })
            {
                buttonLearn.gameObject.SetActive(false);
                textNeedLevel.gameObject.SetActive(true);
                buttonLevelUp.gameObject.SetActive(true);
                textNeedCurrency.gameObject.SetActive(false);
                /*
                int nextLevel = level + 1;
                var infoNextLevel = _tableSkill.GetDataByUidLevel(_struckTableSkill.Uid, nextLevel);
                if (infoNextLevel == null)
                {
                    GcLogger.LogError("skill 테이블에 정보가 없습니다. skill uid: " + _struckTableSkill.Uid + " / Level: " + nextLevel);
                    return;
                }

                if (textNeedLevel != null)
                {
                    string text = LocalizationManagerSkill.Instance.GetUIWindowSkillInfoByKey("Text_NeedLevel");
                    textNeedLevel.text = string.Format(text, infoNextLevel.NeedPlayerLevel);
                }
                
                // 필요 재화
                if (textNeedCurrency != null)
                {
                    textNeedCurrency.text = $"{infoNextLevel.NeedCurrencyType} {infoNextLevel.NeedCurrencyValue}";
                    if (infoNextLevel.NeedCurrencyType == CurrencyConstants.Type.None)
                    {
                        textNeedCurrency.gameObject.SetActive(false);
                    }
                }
                */
            }
            else
            {
                textNeedLevel.gameObject.SetActive(true);
                buttonLearn.gameObject.SetActive(true);
                buttonLevelUp.gameObject.SetActive(false); 
            }
        }
        /// <summary>
        /// 레벨업
        /// </summary>
        private void OnClickLevelUp()
        {
            // todo. 정리 필요
            /*
            bool result = SceneGame.Instance.player.GetComponent<Player>().IsRequireLevel(_struckTableSkill.NeedPlayerLevel);
            if (!result) return;
            // 다음 레벨 있는지 체크, 아니면 최대 레벨
            int nextLevel = _struckTableSkill.Level + 1;
            if (nextLevel > _struckTableSkill.MaxLevel)
            {
                SceneGame.Instance.systemMessageManager.ShowMessageWarning("Skill_MaxLevel");
                return;
            }
            var infoNextLevel = _tableSkill.GetDataByUidLevel(_struckTableSkill.Uid, nextLevel);
            if (infoNextLevel == null)
            {
                GcLogger.LogError("skill 테이블에 정보가 없습니다. skill uid: " + _struckTableSkill.Uid + " / Level: " + nextLevel);
                return;
            }
            bool resultRequireLevel = CheckLevelCurrency(infoNextLevel.NeedPlayerLevel, infoNextLevel.NeedCurrencyType,
                infoNextLevel.NeedCurrencyValue);
            if (!resultRequireLevel) return;

            var result2 =
                SkillPackageManager.Instance.SaveDataManagerSkill.Skill.SetSkillLevelUp(_slotIndex, _struckTableSkill.Uid, 1, nextLevel,
                    true);
            if (result2.Result == ResultCommon.ResultType.Success)
            {
                MinusNeedCurrency(infoNextLevel.NeedCurrencyType, infoNextLevel.NeedCurrencyValue);
            }
            _uiWindowSkill.SetIcons(result2);
            */
        }
        /// <summary>
        /// 레벨, 재화 체크
        /// </summary>
        /// <param name="needPlayerLevel"></param>
        /// <param name="needCurrencyType"></param>
        /// <param name="needCurrencyValue"></param>
        /// <returns></returns>
        private bool CheckLevelCurrency(int needPlayerLevel, CurrencyConstants.Type needCurrencyType, int needCurrencyValue)
        {
            if (!SceneGame.Instance || !SceneGame.Instance.player) return false;
            // 레벨 체크
            bool result = SceneGame.Instance.player.GetComponent<Player>().IsRequireLevel(needPlayerLevel);
            if (!result) return false;
            // 재화 체크
            if (needCurrencyType != CurrencyConstants.Type.None)
            {
                var checkNeedCurrency = SceneGame.Instance.saveDataManager.Player.CheckNeedCurrency(needCurrencyType, needCurrencyValue);
                if (checkNeedCurrency.Result == ResultCommon.ResultType.Fail) return false;
            }
            return true;
        }
        /// <summary>
        /// 필요 재화 처리
        /// </summary>
        /// <param name="needCurrencyType"></param>
        /// <param name="needCurrencyValue"></param>
        /// <returns></returns>
        private bool MinusNeedCurrency(CurrencyConstants.Type needCurrencyType, int needCurrencyValue)
        {
            if (needCurrencyType == CurrencyConstants.Type.None) return true;
            // 재화 빼주기
            var minusCurrency = SceneGame.Instance.saveDataManager.Player.MinusCurrency(needCurrencyType, needCurrencyValue);
            if (minusCurrency.Result == ResultCommon.ResultType.Fail) return false;
            return true;
        }
        /// <summary>
        /// 배우기
        /// </summary>
        private void OnClickLearn()
        {
            // GcLogger.Log("click learn");
            // bool result = CheckLevelCurrency(_struckTableSkill.NeedPlayerLevel, _struckTableSkill.NeedCurrencyType,
            //     _struckTableSkill.NeedCurrencyValue);
            // if (!result) return;

            var result2 = SkillPackageManager.Instance.SaveDataManagerSkill.Skill.SetSkillLearn(_slotIndex, _struckTableSkill.Uid, 1, 1, true);
            if (result2.Result == ResultCommon.ResultType.Success)
            {
                // MinusNeedCurrency(_struckTableSkill.NeedCurrencyType, _struckTableSkill.NeedCurrencyValue);
            }
            var icon = _uiWindowSkill.GetIconByIndex(_slotIndex);
            if (icon)
            {
                icon.SetIconLock(false);
            }
            if (buttonLearn)
                buttonLearn.gameObject.SetActive(false);
            _uiWindowSkill.SetIcons(result2);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // todo. 정리 필요
            // _uiWindowSkillInfo.SetSkillUid(_struckTableSkill.Uid, _struckTableSkill.Level, new Vector2(1f, 1f), new Vector3(transform.position.x - _uiWindowSkill.containerIcon.cellSize.x / 2f,
            //     transform.position.y + _uiWindowSkill.containerIcon.cellSize.y / 2f));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _uiWindowSkillInfo.Show(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
        }
        public Vector3 GetIconPosition() => iconPosition;
    }
}