using GGemCo2DSkill;
using Newtonsoft.Json;

namespace GGemCo2DCore
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