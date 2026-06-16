using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Damage 이벤트가 실제 타겟에게 적중했을 때 재생할 사운드 정의입니다.
    /// </summary>
    [Serializable]
    public struct OnHitSoundEntry
    {
        /// <summary>
        /// 적중 사운드를 재생할 시점입니다.
        /// </summary>
        public OnHitSoundTiming timing;

        /// <summary>
        /// 광역 또는 다중 타겟 적중 시 사운드를 몇 번 재생할지 결정합니다.
        /// </summary>
        public OnHitSoundPlayMode playMode;

        /// <summary>
        /// 재생 확률입니다. 0 이하면 항상 재생하고, 1이면 항상 재생합니다.
        /// </summary>
        [Range(0f, 1f)]
        public float chance;

        /// <summary>
        /// 레거시 또는 단순 입력용 sound 테이블 UID입니다.
        /// </summary>
        public int soundUid;

        /// <summary>
        /// Core SoundManager에 전달할 상세 재생 요청입니다. 유효하면 soundUid보다 우선합니다.
        /// </summary>
        public SoundPlayRequest soundRequest;

        /// <summary>
        /// 현재 항목에서 사용할 최종 사운드 UID를 반환합니다.
        /// </summary>
        /// <returns>유효한 사운드 UID입니다. 없으면 0을 반환합니다.</returns>
        public int ResolveSoundUid()
        {
            return soundRequest != null && soundRequest.IsValid
                ? soundRequest.soundUid
                : soundUid;
        }

        /// <summary>
        /// 현재 항목이 실제 재생 가능한 사운드 요청을 가지고 있는지 확인합니다.
        /// </summary>
        /// <returns>재생 가능한 사운드 UID가 있으면 <see langword="true"/>입니다.</returns>
        public bool IsValid()
        {
            return ResolveSoundUid() > 0;
        }

        /// <summary>
        /// Core SoundManager에 전달할 사운드 재생 요청을 생성합니다.
        /// </summary>
        /// <returns>재생 요청입니다. 유효한 사운드가 없으면 <see langword="null"/>을 반환합니다.</returns>
        public SoundPlayRequest ResolveRequest()
        {
            if (soundRequest != null && soundRequest.IsValid)
                return soundRequest.Clone();

            return soundUid > 0
                ? SoundPlayRequest.Create(soundUid)
                : null;
        }
    }

    /// <summary>
    /// Damage 적중 사운드를 재생할 시점입니다.
    /// </summary>
    public enum OnHitSoundTiming
    {
        /// <summary>
        /// 피해가 실제로 적용된 직후 재생합니다.
        /// </summary>
        AfterDamage = 0,

        /// <summary>
        /// 피해 적용 직전에 재생합니다.
        /// </summary>
        BeforeDamage = 1,
    }

    /// <summary>
    /// 다중 타겟 적중 시 사운드 재생 횟수 정책입니다.
    /// </summary>
    public enum OnHitSoundPlayMode
    {
        /// <summary>
        /// Damage 이벤트 한 번당 사운드를 한 번만 재생합니다.
        /// </summary>
        OncePerDamageEvent = 0,

        /// <summary>
        /// 실제 피해가 적용된 타겟마다 사운드를 재생합니다.
        /// </summary>
        PerTarget = 1,
    }
}
