using System;
using System.Collections.Generic;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 Timeline 클립 타입별 Payload Factory를 관리하는 레지스트리입니다.
    /// </summary>
    internal static class SkillPayloadFactoryRegistry
    {
        /// <summary>
        /// 클립 타입별 Payload Factory 캐시입니다.
        /// </summary>
        private static readonly Dictionary<Type, ISkillPayloadFactory> Factories = BuildFactories();

        /// <summary>
        /// 지정한 Timeline 클립에 대응하는 Payload 생성 팩터리를 반환합니다.
        /// </summary>
        /// <param name="clip">변환할 Timeline 클립입니다.</param>
        /// <returns>Payload 생성 팩터리입니다. 지원하지 않는 클립이면 <see langword="null"/>을 반환합니다.</returns>
        public static Func<UnityEngine.Object> CreatePayloadFactory(SkillEventClipBase clip)
        {
            if (clip == null)
                return null;

            Type clipType = clip.GetType();
            if (!Factories.TryGetValue(clipType, out ISkillPayloadFactory factory))
                return null;

            return factory.CreateFactory(clip);
        }

        /// <summary>
        /// 지원하는 Timeline 클립 타입과 Payload Factory 매핑을 구성합니다.
        /// </summary>
        /// <returns>클립 타입별 Payload Factory 사전입니다.</returns>
        private static Dictionary<Type, ISkillPayloadFactory> BuildFactories()
        {
            var factories = new ISkillPayloadFactory[]
            {
                new SkillDamagePayloadFactory(),
                new SkillSpawnVfxPayloadFactory(),
                new SkillCaptureTargetPositionPayloadFactory(),
                new SkillApplyAffectPayloadFactory(),
                new SkillApplyTempHpPayloadFactory(),
                new SkillPlayAudioPayloadFactory(),
                new SkillScreenFadePayloadFactory(),
                new SkillAfterimagePayloadFactory(),
                new SkillLungePayloadFactory(),
                new SkillGroundSlamPayloadFactory(),
                new SkillPositionHoldPayloadFactory(),
                new SkillMovementControlLockPayloadFactory(),
                new SkillArcLungePayloadFactory(),
                new SkillProjectilePayloadFactory(),
                new SkillLaserPayloadFactory(),
                new SkillSpawnDummyCharacterPayloadFactory(),
                new SkillMoveDummyCharacterPayloadFactory(),
                new SkillDespawnDummyCharacterPayloadFactory(),
                new SkillSetDummyAirborneStatePayloadFactory(),
                new SkillPlayDummyCharacterAnimationPayloadFactory(),
            };

            var result = new Dictionary<Type, ISkillPayloadFactory>(factories.Length);
            for (int i = 0; i < factories.Length; i++)
            {
                ISkillPayloadFactory factory = factories[i];
                if (factory == null || factory.ClipType == null)
                    continue;

                result[factory.ClipType] = factory;
            }

            return result;
        }
    }
}
