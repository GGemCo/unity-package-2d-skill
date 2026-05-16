using System;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Payload Bake 과정에서 여러 Factory가 공유하는 보정 및 호환성 유틸리티입니다.
    /// </summary>
    internal static class SkillPayloadBakeUtility
    {
        /// <summary>
        /// 런타임 버전의 <see cref="ApplyStatusEventDefinition"/>에 존재할 수 있는 applyTo 필드에 대한 리플렉션 정보입니다.
        /// 구버전 런타임과의 호환성을 위해 직접 참조 대신 리플렉션으로 접근합니다.
        /// </summary>
        private static readonly System.Reflection.FieldInfo ApplyStatusApplyToField =
            typeof(ApplyStatusEventDefinition).GetField("applyTo");

        /// <summary>
        /// <see cref="ApplyStatusEventDefinition"/>의 applyTo 필드가 존재하는 경우 값을 설정합니다.
        /// 런타임 타입 확장 여부에 따라 선택적으로 적용되며, 필드가 없거나 형식이 맞지 않으면 무시합니다.
        /// </summary>
        /// <param name="def">설정할 상태 적용 이벤트 정의입니다.</param>
        /// <param name="applyToRaw">설정할 enum 원시 값입니다.</param>
        public static void TrySetApplyStatusApplyTo(ApplyStatusEventDefinition def, int applyToRaw)
        {
            if (def == null || ApplyStatusApplyToField == null)
                return;

            var fieldType = ApplyStatusApplyToField.FieldType;
            if (!fieldType.IsEnum)
                return;

            try
            {
                var value = Enum.ToObject(fieldType, applyToRaw);
                ApplyStatusApplyToField.SetValue(def, value);
            }
            catch
            {
                // 런타임 구버전 또는 enum 불일치 상황에서는 Bake를 중단하지 않는다.
            }
        }

        /// <summary>
        /// Timeline VFX 클립의 Anchor 값을 런타임 VFX 생성 앵커로 변환합니다.
        /// </summary>
        /// <param name="anchorRaw">Timeline 클립에 저장된 Anchor enum 원시 값입니다.</param>
        /// <returns>런타임에서 사용할 VFX 생성 앵커입니다.</returns>
        public static VfxSpawnAnchor ConvertVfxSpawnAnchor(int anchorRaw)
        {
            switch ((SkillSpawnVfxClip.AnchorType)anchorRaw)
            {
                case SkillSpawnVfxClip.AnchorType.Target:
                    return VfxSpawnAnchor.Target;
                case SkillSpawnVfxClip.AnchorType.Ground:
                    return VfxSpawnAnchor.Ground;
                case SkillSpawnVfxClip.AnchorType.Caster:
                default:
                    return VfxSpawnAnchor.Caster;
            }
        }

        /// <summary>
        /// 레이저 데미지 활성 지속 시간을 런타임 이벤트 정의에서 사용하는 값으로 보정합니다.
        /// </summary>
        /// <param name="value">타임라인 클립에 저장된 데미지 활성 지속 시간입니다.</param>
        /// <returns>0 이하이면 레이저 종료까지 유지하는 의미의 -1, 양수이면 해당 값을 반환합니다.</returns>
        public static float NormalizeLaserDamageActiveDuration(float value)
        {
            return value <= 0f ? -1f : value;
        }
    }
}
