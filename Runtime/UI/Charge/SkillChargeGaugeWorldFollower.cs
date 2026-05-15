using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 월드 캐릭터 위치를 Canvas 로컬 좌표로 변환하여 차징 게이지 UI를 따라가게 하는 컴포넌트입니다.
    /// </summary>
    public sealed class SkillChargeGaugeWorldFollower : MonoBehaviour
    {
        private RectTransform _targetRect;
        private Transform _worldTarget;
        private Canvas _canvas;
        private RectTransform _parentRect;
        private Camera _worldCamera;
        private Vector3 _worldOffset;
        private Vector2 _screenOffset;

        /// <summary>
        /// 위치 추적에 필요한 대상과 Canvas 정보를 초기화합니다.
        /// </summary>
        /// <param name="targetRect">위치를 갱신할 차징 게이지 RectTransform입니다.</param>
        /// <param name="worldTarget">추적할 월드 캐릭터 Transform입니다.</param>
        /// <param name="canvas">게이지가 배치된 Canvas입니다.</param>
        /// <param name="parentRect">게이지가 속한 부모 RectTransform입니다.</param>
        /// <param name="worldCamera">월드 좌표를 스크린 좌표로 변환할 카메라입니다.</param>
        /// <param name="worldOffset">캐릭터 월드 위치 기준 오프셋입니다.</param>
        /// <param name="screenOffset">Canvas 로컬 좌표 변환 후 추가할 픽셀 오프셋입니다.</param>
        public void Initialize(
            RectTransform targetRect,
            Transform worldTarget,
            Canvas canvas,
            RectTransform parentRect,
            Camera worldCamera,
            Vector3 worldOffset,
            Vector2 screenOffset)
        {
            _targetRect = targetRect;
            _worldTarget = worldTarget;
            _canvas = canvas;
            _parentRect = parentRect;
            _worldCamera = worldCamera;
            _worldOffset = worldOffset;
            _screenOffset = screenOffset;
        }

        /// <summary>
        /// 캐릭터의 현재 월드 위치를 기준으로 게이지 UI 위치를 즉시 갱신합니다.
        /// </summary>
        public void RefreshPosition()
        {
            if (_targetRect == null || _worldTarget == null)
                return;

            Vector3 worldPosition = _worldTarget.position + _worldOffset;

            if (_canvas == null)
            {
                _targetRect.position = worldPosition;
                return;
            }

            if (_canvas.renderMode == RenderMode.WorldSpace)
            {
                _targetRect.position = worldPosition;
                _targetRect.localPosition += (Vector3)_screenOffset;
                return;
            }

            RectTransform parentRect = _parentRect != null ? _parentRect : _canvas.transform as RectTransform;
            if (parentRect == null)
                return;

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null)
                return;

            Vector2 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
            {
                _targetRect.anchoredPosition = localPoint + _screenOffset;
            }
        }

        /// <summary>
        /// 우선순위에 따라 사용할 월드 카메라를 반환합니다.
        /// </summary>
        /// <returns>월드 좌표 변환에 사용할 카메라입니다.</returns>
        private Camera ResolveWorldCamera()
        {
            if (_worldCamera != null)
                return _worldCamera;

            if (SceneGame.Instance != null && SceneGame.Instance.mainCamera != null)
                return SceneGame.Instance.mainCamera;

            return Camera.main;
        }
    }
}
