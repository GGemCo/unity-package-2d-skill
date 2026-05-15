using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 더미 캐릭터의 비주얼 알파, 애니메이션, 초기 런타임 상태, 제어 잠금 처리를 담당합니다.
    /// </summary>
    internal static class SkillDummyActorPresentationUtility
    {
        /// <summary>
        /// 더미 캐릭터 비주얼 알파값을 설정합니다.
        /// </summary>
        /// <param name="character">알파를 적용할 캐릭터입니다.</param>
        /// <param name="alpha">적용할 알파값(0~1)입니다.</param>
        public static void SetVisualAlpha(CharacterBase character, float alpha)
        {
            if (character == null)
                return;

            float clamped = Mathf.Clamp01(alpha);
            ICharacterAnimationController anim = ResolveAnimationController(character.gameObject);
            anim?.SetCharacterColor(new Color(1f, 1f, 1f, clamped));

            SpriteRenderer[] spriteRenderers = character.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                Color color = spriteRenderer.color;
                color.a = clamped;
                spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션 종료 정책을 적용합니다.
        /// </summary>
        /// <param name="handle">종료 정책을 적용할 더미 핸들입니다.</param>
        /// <param name="endPolicy">적용할 종료 정책입니다.</param>
        /// <param name="endAnimationName">커스텀 종료 애니메이션 이름입니다.</param>
        /// <param name="endAnimationLoop">커스텀 종료 애니메이션 루프 여부입니다.</param>
        /// <param name="endAnimationTimeScale">커스텀 종료 애니메이션 재생 속도 배율입니다.</param>
        public static void ApplyAnimationEndPolicy(
            SkillDummyActorHandle handle,
            DummyAnimationEndPolicy endPolicy,
            string endAnimationName,
            bool endAnimationLoop,
            float endAnimationTimeScale)
        {
            if (handle == null || handle.Character == null)
                return;

            switch (endPolicy)
            {
                case DummyAnimationEndPolicy.PlayWait:
                    PlayAnimation(handle.Character, ICharacterAnimationController.WaitForwardAnim, true, 1f);
                    break;
                case DummyAnimationEndPolicy.PlayCustom:
                    PlayAnimation(handle.Character, endAnimationName, endAnimationLoop, endAnimationTimeScale);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 더미 캐릭터 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="character">애니메이션 대상 캐릭터입니다.</param>
        /// <param name="animationName">재생할 애니메이션 이름입니다.</param>
        /// <param name="loop">루프 재생 여부입니다.</param>
        /// <param name="timeScale">재생 속도 배율입니다.</param>
        public static void PlayAnimation(CharacterBase character, string animationName, bool loop, float timeScale)
        {
            if (character == null || string.IsNullOrWhiteSpace(animationName))
                return;

            ICharacterAnimationController anim = ResolveAnimationController(character.gameObject);
            if (anim == null)
                return;

            anim.PlaySkillAnimation(new SkillAnimationRequest(
                skillUid: 0,
                phase: SkillAnimationPhase.Action,
                loop: loop,
                timeScale: Mathf.Max(0f, timeScale),
                overrideAnimationName: animationName));
        }

        /// <summary>
        /// 더미 캐릭터가 스킬 이벤트용 수동 제어 대상이 되도록 런타임 상태를 정리합니다.
        /// </summary>
        /// <param name="character">정리할 더미 캐릭터입니다.</param>
        public static void ConfigureRuntime(CharacterBase character)
        {
            if (character == null)
                return;

            MonsterBrainTicker brainTicker = character.GetComponent<MonsterBrainTicker>();
            if (brainTicker != null)
                brainTicker.enabled = false;

            CharacterBaseController[] controllers = character.GetComponents<CharacterBaseController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                    controllers[i].enabled = false;
            }

            MonoBehaviour[] behaviours = character.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (behaviour is IMonsterBrain || behaviour.GetType().Name == "MonsterBtRunner")
                    behaviour.enabled = false;
            }

            Rigidbody2D rb = character.characterRigidbody2D != null
                ? character.characterRigidbody2D
                : character.GetComponentInParent<Rigidbody2D>();
            if (rb != null)
            {
                rb.SetLinearVelocity(Vector2.zero);
                rb.angularVelocity = 0f;
            }

            character.SetAggro(false);
            character.SetAttackerTarget(null);
            character.SetStatusIdle();

            ICharacterAnimationController anim = ResolveAnimationController(character.gameObject);
            anim?.PlayWaitAnimation();
        }

        /// <summary>
        /// 더미 캐릭터 제어/브레인 잠금을 적용합니다.
        /// </summary>
        /// <param name="handle">잠금을 적용할 더미 핸들입니다.</param>
        public static void ApplyRuntimeLocks(SkillDummyActorHandle handle)
        {
            if (handle == null || handle.Character == null)
                return;

            if (handle.ControlLockToken == null)
                handle.ControlLockToken = handle.Character.AcquireControlLock(handle);

            if (handle.BrainLockToken == null)
                handle.BrainLockToken = handle.Character.AcquireBrainLock(handle);
        }

        /// <summary>
        /// 더미 캐릭터 제어/브레인 잠금을 해제합니다.
        /// </summary>
        /// <param name="handle">잠금을 해제할 더미 핸들입니다.</param>
        public static void ReleaseRuntimeLocks(SkillDummyActorHandle handle)
        {
            if (handle == null)
                return;

            CharacterBase character = handle.Character;
            if (character != null)
            {
                if (handle.ControlLockToken != null)
                    character.ReleaseControlLock(handle.ControlLockToken);

                if (handle.BrainLockToken != null)
                    character.ReleaseBrainLock(handle.BrainLockToken);
            }

            handle.ControlLockToken = null;
            handle.BrainLockToken = null;
        }

        /// <summary>
        /// 캐릭터 오브젝트에서 애니메이션 컨트롤러를 조회합니다.
        /// </summary>
        /// <param name="target">조회할 캐릭터 또는 하위 오브젝트입니다.</param>
        /// <returns>조회된 애니메이션 컨트롤러입니다. 없으면 <see langword="null"/>입니다.</returns>
        public static ICharacterAnimationController ResolveAnimationController(GameObject target)
        {
            if (target == null)
                return null;

            ICharacterAnimationController direct = target.GetComponent<ICharacterAnimationController>();
            if (direct != null)
                return direct;

            ICharacterAnimationController inChildren = target.GetComponentInChildren<ICharacterAnimationController>();
            if (inChildren != null)
                return inChildren;

            return target.GetComponentInParent<ICharacterAnimationController>();
        }
    }
}
