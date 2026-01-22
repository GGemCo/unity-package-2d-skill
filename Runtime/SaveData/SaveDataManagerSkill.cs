using System.Collections.Generic;
using System.IO;
using GGemCo2DCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 저장할 데이터 컨테이너 클래스
    /// </summary>
    public class SaveDataContainerSkill
    {
        public SkillData SkillData;

        public Dictionary<string, JToken> Extensions;
    }
    /// <summary>
    /// 세이브 데이터 메인 매니저
    /// </summary>
    public class SaveDataManagerSkill : SaveDataManagerBase
    {
        public SkillData Skill { get; private set; }

        /// <summary>
        /// 슬롯 관리, 파일 관리, 썸네일 관리 매니저 초기화
        /// </summary>
        protected override void InitializeData()
        {
            // 로드한 세이브 데이터 가져오기 
            SaveDataContainerSkill saveDataContainer = SaveDataLoaderSkill.Instance.GetSaveDataContainer();
            // 각 데이터 클래스 초기화
            Skill = new SkillData();

            // 초기화 실행
            Skill.Initialize(tableLoaderManager, saveDataContainer);
            
            // 외부 섹션 복원
            if (saveDataContainer?.Extensions != null)
            {
                var env = new SaveEnvelope();
                foreach (var kv in saveDataContainer.Extensions)
                    env.Sections[kv.Key] = kv.Value;

                // 순서와 무관하게 복원 보장
                SaveRegistry.ApplyRestore(env);
            }
        }
        
        /// <summary>
        /// 현재 데이터를 선택한 슬롯에 저장 + 메타파일 업데이트
        /// </summary>
        public override bool SaveData()
        {
            if (!base.SaveData()) return false;
            
            string filePath = saveFileController.GetSaveFilePath(currentSaveSlot);
            string thumbnailPath = thumbnailController.GetThumbnailPath(currentSaveSlot);

            // 외부 기여자에게 현재 상태 캡처 요청
            var env = BuildEnvelopeForSave();
            
            SaveDataContainerSkill saveData = new SaveDataContainerSkill()
            {
                SkillData = Skill,
            };

            string json = JsonConvert.SerializeObject(saveData);
            File.WriteAllText(filePath, json);
            // GcLogger.Log($"데이터가 저장되었습니다. 슬롯 {currentSaveSlot}");
            
            // 메타파일 업데이트
            // todo. 정리 필요
            // slotMetaDatController.UpdateSlot(currentSaveSlot, thumbnailPath, true, Player.CurrentLevel, filePath);
            return true;
        }
    }
}