#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Unity Editor에서 Play Mode 진입 시 <see cref="SkillTestRuntimeHub"/>가
    /// 현재 씬에 존재하지 않을 경우 자동으로 생성하여 배치하는 Editor 전용 유틸리티입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="InitializeOnLoadAttribute"/>를 통해 Unity Editor가 로드될 때
    /// 정적 생성자가 실행되며, Play Mode 상태 변경 이벤트에 등록됩니다.
    /// </para>
    /// <para>
    /// Play Mode 진입 후 씬에 <see cref="SkillTestRuntimeHub"/>가 없다면
    /// 지정된 이름의 GameObject를 찾거나 새로 생성한 뒤 컴포넌트를 추가합니다.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class SkillTestRuntimeHubAutoSpawner
    {
        /// <summary>
        /// 씬에서 사용할 RuntimeHub GameObject의 기본 이름입니다.
        /// </summary>
        private const string HubObjectName = "SkillTestRuntimeHub";

        /// <summary>
        /// 에디터 로드 시 실행되는 정적 생성자입니다.
        /// Play Mode 상태 변경 이벤트에 핸들러를 등록합니다.
        /// </summary>
        static SkillTestRuntimeHubAutoSpawner()
        {
            // 중복 등록 방지를 위해 먼저 제거 후 다시 등록
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Unity Editor의 Play Mode 상태가 변경될 때 호출되는 이벤트 핸들러입니다.
        /// </summary>
        /// <param name="state">현재 Play Mode 상태 변화 단계입니다.</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Play Mode에 실제로 진입했을 때만 RuntimeHub 존재 여부를 확인
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            EnsureHubExists();
        }

        /// <summary>
        /// 씬에 <see cref="SkillTestRuntimeHub"/>가 존재하는지 확인하고,
        /// 없다면 GameObject를 생성한 뒤 컴포넌트를 추가합니다.
        /// </summary>
        private static void EnsureHubExists()
        {
            // 이미 존재하는 RuntimeHub가 있는지 먼저 검색
            var hub = Object.FindFirstObjectByType<SkillTestRuntimeHub>();
            if (hub != null) return;

            // 이름으로 GameObject 검색
            var go = GameObject.Find(HubObjectName);

            // 없으면 새 GameObject 생성
            if (go == null) 
                go = new GameObject(HubObjectName);

            // RuntimeHub 컴포넌트가 없으면 추가
            if (go.GetComponent<SkillTestRuntimeHub>() == null)
                go.AddComponent<SkillTestRuntimeHub>();
        }
    }
}
#endif