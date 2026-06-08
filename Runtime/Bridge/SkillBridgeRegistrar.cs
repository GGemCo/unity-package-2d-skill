using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 패키지가 로드될 때 Core의 SkillBridge에 Provider를 자동 등록합니다.
    /// </summary>
    internal static class SkillBridgeRegistrar
    {
        /// <summary>
        /// 플레이/빌드 환경에서 씬 로드 전에 Skill NeedMp Provider를 Core 브리지에 주입합니다.
        /// </summary>
        /// <remarks>
        /// Core UI가 Skill 패키지를 직접 참조하지 않도록, Skill 패키지가 존재하는 경우에만 Provider를 등록합니다.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SkillBridge.SetNeedMpProvider(new SkillNeedMpProvider());
        }
    }
}
