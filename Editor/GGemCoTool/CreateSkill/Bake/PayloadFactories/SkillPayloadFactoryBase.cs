using System;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 특정 Timeline 클립 타입을 처리하는 Payload Factory의 공통 기반 클래스입니다.
    /// </summary>
    /// <typeparam name="TClip">처리할 Timeline 클립 타입입니다.</typeparam>
    internal abstract class SkillPayloadFactoryBase<TClip> : ISkillPayloadFactory
        where TClip : SkillEventClipBase
    {
        /// <inheritdoc />
        public Type ClipType => typeof(TClip);

        /// <inheritdoc />
        public Func<UnityEngine.Object> CreateFactory(SkillEventClipBase clip)
        {
            if (clip is not TClip typedClip)
                return null;

            return () => CreatePayload(typedClip);
        }

        /// <summary>
        /// Timeline 클립 데이터를 런타임 Payload ScriptableObject로 변환합니다.
        /// </summary>
        /// <param name="clip">변환할 Timeline 클립입니다.</param>
        /// <returns>생성된 런타임 Payload입니다.</returns>
        protected abstract UnityEngine.Object CreatePayload(TClip clip);
    }
}
