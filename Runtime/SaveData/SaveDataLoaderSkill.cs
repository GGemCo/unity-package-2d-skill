using GGemCo2DCore;
using Newtonsoft.Json;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 세이브 데이터 json 파일 로드
    /// </summary>
    public class SaveDataLoaderSkill : SaveDataLoaderBase
    {
        public static SaveDataLoaderSkill Instance { get; private set; }
        
        private SaveDataContainerSkill _saveDataContainerSkill;
        
        protected override void Awake()
        {
            base.Awake();

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }
        /// <summary>
        /// 바로 해제를 위해 추가
        /// </summary>
        private void OnDestroy()
        {
            _saveDataContainerSkill = null;
        }

        protected override string GetSaveFilePath(int slotIndex)
        {
            return saveFileController.GetSaveFilePath(slotIndex, SaveDataConstantsSkill.SaveDataFileName);
        }

        /// <summary>
        /// Skill 저장 파일의 논리 저장 식별자를 반환합니다.
        /// </summary>
        /// <param name="slotIndex">로드할 저장 슬롯 번호입니다.</param>
        /// <returns>Skill 저장 데이터 AAD 구성에 사용할 논리 저장 식별자입니다.</returns>
        protected override SaveDataIdentity GetSaveDataIdentity(int slotIndex)
        {
            return SaveDataIdentity.Skill(slotIndex);
        }

        protected override void OnLoaded(string json) 
        {
            _saveDataContainerSkill = JsonConvert.DeserializeObject<SaveDataContainerSkill>(json);
        }
        public SaveDataContainerSkill GetSaveDataContainer()
        {
            return _saveDataContainerSkill;
        }
    }
}
