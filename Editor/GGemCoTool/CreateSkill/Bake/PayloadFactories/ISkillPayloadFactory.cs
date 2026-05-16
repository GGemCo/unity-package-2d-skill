using System;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 Timeline 클립을 런타임 Payload 생성 팩터리로 변환하는 계약입니다.
    /// </summary>
    internal interface ISkillPayloadFactory
    {
        /// <summary>
        /// 이 팩터리가 처리할 수 있는 Timeline 클립 타입입니다.
        /// </summary>
        Type ClipType { get; }

        /// <summary>
        /// 지정한 Timeline 클립을 기반으로 런타임 Payload 생성 함수를 생성합니다.
        /// </summary>
        /// <param name="clip">변환할 Timeline 클립입니다.</param>
        /// <returns>Payload를 생성하는 함수입니다. 처리할 수 없으면 <see langword="null"/>을 반환합니다.</returns>
        Func<UnityEngine.Object> CreateFactory(SkillEventClipBase clip);
    }
}
