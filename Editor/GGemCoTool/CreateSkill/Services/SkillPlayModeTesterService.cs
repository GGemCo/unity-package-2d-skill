using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// PlayMode 테스트 영역에서 공통으로 사용하는 UI 상태 갱신을 담당합니다.
    /// 실제 스킬 실행/캐스터 선택 로직은 CreateSkillWindow에 남겨 두고,
    /// 패널 단에서는 버튼 상태와 안내 메시지 관리만 담당합니다.
    /// </summary>
    public static class SkillPlayModeTesterService
    {
        public static void UpdatePlayModeUiState(
            bool isPlaying,
            bool hasSelection,
            HelpBox playModeHelp,
            Button useSkillButton,
            Button spawnMonsterButton)
        {
            useSkillButton?.SetEnabled(isPlaying && hasSelection);
            spawnMonsterButton?.SetEnabled(isPlaying);

            if (playModeHelp != null)
            {
                playModeHelp.messageType = isPlaying ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning;
            }
        }

        public static bool ValidatePlayModeAndSelection(string title, SkillAuthoringModel selectedSkill)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(title, "Play Mode에서만 사용할 수 있습니다.", "OK");
                return false;
            }

            if (selectedSkill == null)
            {
                EditorUtility.DisplayDialog(title, "스킬을 먼저 선택하세요.", "OK");
                return false;
            }

            return true;
        }
    }
}
