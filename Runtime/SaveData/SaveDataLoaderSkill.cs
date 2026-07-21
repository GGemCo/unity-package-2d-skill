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
        /// Skill 저장 로더가 제거될 때 컨테이너와 싱글톤 참조를 정리합니다.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            _saveDataContainerSkill = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 로컬 데이터 초기화 시 메모리에 남아 있는 Skill 저장 컨테이너를 제거합니다.
        /// </summary>
        /// <param name="scope">요청된 로컬 데이터 초기화 범위입니다.</param>
        protected override void OnClearLoadedDataForReset(SaveDataResetScope scope)
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
