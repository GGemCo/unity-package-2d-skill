using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    [DefaultExecutionOrder((int)ConfigCommon.ExecutionOrdering.Skill)]
    public class SkillPackageManager : MonoBehaviour
    {
        public static SkillPackageManager Instance { get; private set; }
        [HideInInspector] public SaveDataManagerSkill SaveDataManagerSkill;

        private void Awake()
        {
            // 게임 씬이 로드되지 않았다면 초기화하지 않는다.
            if (TableLoaderManager.Instance == null)
            {
                return;
            }

            // 게임 씬 싱글톤으로 사용한다.
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            GameObject managerContainer = GameObject.Find("Managers");
            if (GcLogger.IsNullGameObject(managerContainer, nameof(managerContainer))) return;
            SaveDataManagerSkill = SceneGame.Instance.CreateManager<SaveDataManagerSkill>(managerContainer);
        }

        private void Start()
        {
            if (SceneGame.Instance)
                SceneGame.Instance.OnSceneGameDestroyed += OnDestroyBySceneGame;
        }

        /// <summary>
        /// SceneGame 종료 이벤트에 의해 호출되며, 자신을 파괴한다.
        /// </summary>
        private void OnDestroyBySceneGame()
        {
            Destroy(gameObject);
        }

        /// <summary>
        /// 오브젝트 파괴 시 SceneGame 이벤트 구독을 해제한다.
        /// </summary>
        /// <remarks>
        /// 이벤트 누수 및 중복 호출을 방지하기 위한 정리 로직이다.
        /// </remarks>
        private void OnDestroy()
        {
            if (SceneGame.Instance)
                SceneGame.Instance.OnSceneGameDestroyed -= OnDestroyBySceneGame;
        }
    }
}
