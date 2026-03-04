using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 장착(또는 테스트 툴 적용)으로 임시 최대 HP(<see cref="CharacterStat.TotalHpTemp"/>)가 증가한 경우,
    /// 증가분만큼 현재 임시 HP(저장되는 스트림: <see cref="CharacterBase.CurrentHpTemp"/>)도 채워주는 유틸.
    /// 
    /// 설계 의도:
    /// - 기본 런타임 정책은 임시 최대 HP 변화 시 Current를 자동 충전하지 않습니다(KeepCurrent).
    /// - 그러나 "UI에서 패시브 장착"과 "패시브 테스트 툴"에서는 장착 직후 체감이 필요하므로,
    ///   해당 진입점에서만 증가분(delta) 만큼 Current를 채웁니다.
    /// </summary>
    public static class PassiveTempHpFillUtility
    {
        /// <summary>
        /// 임시 HP 관련 스냅샷.
        /// </summary>
        public readonly struct TempHpSnapshot
        {
            public readonly long TempMax;
            public readonly long TempCurrent;

            public TempHpSnapshot(long tempMax, long tempCurrent)
            {
                TempMax = tempMax;
                TempCurrent = tempCurrent;
            }
        }

        /// <summary>
        /// 현재 캐릭터의 임시 최대/현재 값 스냅샷을 캡처합니다.
        /// </summary>
        public static TempHpSnapshot Capture(CharacterBase character)
        {
            if (character == null) return default;
            return new TempHpSnapshot(character.TotalHpTemp.Value, character.CurrentHpTemp.Value);
        }

        /// <summary>
        /// 임시 최대 HP가 증가한 경우에만 증가분(delta) 만큼 Current를 채웁니다.
        /// - 감소/동일: 아무 것도 하지 않습니다.
        /// </summary>
        public static void FillCurrentIfTempMaxIncreased(CharacterBase character, TempHpSnapshot before)
        {
            if (character == null) return;

            var nextTempMax = character.TotalHpTemp.Value;
            var delta = nextTempMax - before.TempMax;
            if (delta <= 0) return;

            // SetItemBonusHpCurrent 내부에서 0..TotalTempHp 범위로 클램프됩니다.
            character.SetItemBonusHpCurrent(before.TempCurrent + delta);
        }
    }
}
