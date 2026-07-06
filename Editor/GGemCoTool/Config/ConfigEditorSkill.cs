using GGemCo2DCore;
using GGemCo2DCoreEditor;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Skill 패키지 에디터 툴 메뉴 경로와 정렬 순서를 정의합니다.
    /// </summary>
    public static class ConfigEditorSkill
    {
        /// <summary>
        /// Skill 패키지 Unity 메뉴 항목의 정렬 순서(order) 값을 정의합니다.
        /// </summary>
        /// <remarks>
        /// 공통 메뉴 우선순위 기준값에 Skill 내부 로컬 순서를 더해 메뉴 배치 순서를 결정합니다.
        /// </remarks>
        public enum ToolOrdering
        {
            /// <summary>기본 셋팅 메뉴 섹션의 시작 위치입니다.</summary>
            DefaultSetting = GGemCoToolMenuPriority.SkillSettings + 1,

            /// <summary>Addressables 셋팅 메뉴 섹션의 시작 위치입니다.</summary>
            SettingAddressable,

            /// <summary>Pre-Intro 씬 셋팅 메뉴 섹션의 시작 위치입니다.</summary>
            SettingScenePreIntro,

            /// <summary>로딩 씬 셋팅 메뉴 섹션의 시작 위치입니다.</summary>
            SettingSceneLoading,

            /// <summary>게임 씬 셋팅 메뉴 섹션의 시작 위치입니다.</summary>
            SettingSceneGame,

            /// <summary>개발 도구 메뉴 섹션의 시작 위치입니다.</summary>
            Development = GGemCoToolMenuPriority.SkillDevelopment,

            /// <summary>테스트 도구 메뉴 섹션의 시작 위치입니다.</summary>
            Test = GGemCoToolMenuPriority.SkillTest,
            SettingTestSkill,

            /// <summary>패시브 스킬 사용 툴 메뉴의 위치입니다.</summary>
            UsePassiveSkill,

            /// <summary>셔플(Shuffle) 미리보기 메뉴의 위치입니다.</summary>
            PreviewShuffle,

            /// <summary>기타 도구 메뉴 섹션의 시작 위치입니다.</summary>
            Etc = GGemCoToolMenuPriority.SkillEtc,
        }

       private const string NameToolGGemCoSkill = GGemCoToolMenu.Skill;

        // 기본 셋팅하기

        /// <summary>
        /// 기본 셋팅 메뉴(설정하기)의 경로 접두사입니다.
        /// </summary>
        private const string NameToolSettings = NameToolGGemCoSkill + GGemCoToolMenu.Settings;

        /// <summary>
        /// "자동 셋팅하기" 메뉴 경로입니다.
        /// </summary>
        public const string NameToolSettingAuto = NameToolSettings + "자동 셋팅하기";

        /// <summary>
        /// "기본 셋팅하기" 메뉴 경로입니다.
        /// </summary>
        public const string NameToolSettingDefault = NameToolSettings + "기본 셋팅하기";

        /// <summary>
        /// "Addressable 셋팅하기" 메뉴 경로입니다.
        /// </summary>
        public const string NameToolSettingAddressable = NameToolSettings + "Addressable 셋팅하기";

        /// <summary>
        /// "Pre 인트로 씬 셋팅하기" 메뉴 경로입니다.
        /// </summary>
        public const string NameToolSettingScenePreIntro = NameToolSettings + "Pre 인트로 씬 셋팅하기";

        /// <summary>
        /// "로딩 씬 셋팅하기" 메뉴 경로입니다.
        /// </summary>
        public const string NameToolSettingSceneLoading = NameToolSettings + "로딩 씬 셋팅하기";

        /// <summary>
        /// "게임 씬 셋팅하기" 메뉴 경로입니다.
        /// </summary>
        public const string NameToolSettingSceneGame = NameToolSettings + "게임 씬 셋팅하기";

        // 개발툴

        /// <summary>
        /// 개발툴 메뉴의 경로 접두사입니다.
        /// </summary>
        private const string NameToolDevelopment = NameToolGGemCoSkill + GGemCoToolMenu.Development;

        // 테스트

        /// <summary>
        /// 테스트툴 메뉴의 경로 접두사입니다.
        /// </summary>
        private const string NameToolTest = NameToolGGemCoSkill + GGemCoToolMenu.Test;

        public const string NameToolSettingTestSkill = NameToolTest + "스킬 테스트 툴";

        public const string NameToolUsePassiveSkill = NameToolTest + "패시브 스킬 사용하기";

        // etc

        /// <summary>
        /// 기타 메뉴의 경로 접두사입니다.
        /// </summary>
        private const string NameToolEtc = NameToolGGemCoSkill + GGemCoToolMenu.Etc;

        public const string PathPackageCore = "Packages/com.ggemco.2d.skill";
    }
}
