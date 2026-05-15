using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 실행 중 캐릭터 오브젝트 계층에서 필요한 코어 컴포넌트를 일관된 순서로 탐색합니다.
    /// </summary>
    internal static class SkillCharacterComponentResolver
    {
        /// <summary>
        /// 지정한 오브젝트 계층에서 <see cref="CharacterBase"/>를 탐색합니다.
        /// </summary>
        /// <param name="target">탐색 기준 GameObject입니다.</param>
        /// <returns>탐색된 캐릭터가 있으면 반환하고, 없으면 <see langword="null"/>을 반환합니다.</returns>
        public static CharacterBase ResolveCharacterBase(GameObject target)
        {
            if (target == null)
                return null;

            CharacterBase character = target.GetComponent<CharacterBase>();
            if (character != null)
                return character;

            character = target.GetComponentInChildren<CharacterBase>(includeInactive: true);
            if (character != null)
                return character;

            return target.GetComponentInParent<CharacterBase>();
        }

        /// <summary>
        /// 지정한 오브젝트 계층에서 애니메이션 컨트롤러를 현재 오브젝트, 자식, 부모 순으로 탐색합니다.
        /// </summary>
        /// <param name="target">애니메이션 컨트롤러를 찾을 기준 오브젝트입니다.</param>
        /// <returns>찾은 애니메이션 컨트롤러 또는 찾지 못한 경우 <see langword="null"/>입니다.</returns>
        public static ICharacterAnimationController ResolveAnimationController(GameObject target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent<ICharacterAnimationController>(out ICharacterAnimationController animationController))
                return animationController;

            animationController = target.GetComponentInChildren<ICharacterAnimationController>(includeInactive: true);
            if (animationController != null)
                return animationController;

            return target.GetComponentInParent<ICharacterAnimationController>();
        }

        /// <summary>
        /// 지정한 오브젝트 계층에서 액션 컨트롤러를 현재 오브젝트, 자식, 부모 순으로 탐색합니다.
        /// </summary>
        /// <param name="target">액션 컨트롤러를 찾을 기준 오브젝트입니다.</param>
        /// <returns>찾은 액션 컨트롤러 또는 찾지 못한 경우 <see langword="null"/>입니다.</returns>
        public static ICharacterActionController ResolveActionController(GameObject target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent<ICharacterActionController>(out ICharacterActionController actionController))
                return actionController;

            actionController = target.GetComponentInChildren<ICharacterActionController>(includeInactive: true);
            if (actionController != null)
                return actionController;

            return target.GetComponentInParent<ICharacterActionController>();
        }

        /// <summary>
        /// 지정한 오브젝트 계층에서 모션 컨트롤러를 현재 오브젝트, 자식, 부모 순으로 탐색합니다.
        /// </summary>
        /// <param name="target">모션 컨트롤러를 찾을 기준 오브젝트입니다.</param>
        /// <returns>찾은 모션 컨트롤러 또는 찾지 못한 경우 <see langword="null"/>입니다.</returns>
        public static ICharacterMotionController ResolveMotionController(GameObject target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent<ICharacterMotionController>(out ICharacterMotionController motionController))
                return motionController;

            motionController = target.GetComponentInChildren<ICharacterMotionController>(includeInactive: true);
            if (motionController != null)
                return motionController;

            return target.GetComponentInParent<ICharacterMotionController>();
        }
    }
}
