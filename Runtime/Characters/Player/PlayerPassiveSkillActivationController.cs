using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 수동 패시브 발동 요청이 실패한 상위 원인을 나타냅니다.
    /// </summary>
    public enum PassiveSkillActivationFailReason
    {
        None = 0,
        InvalidInput = 1,
        NotEquipped = 2,
        DefinitionMissing = 3,
        TargetingFailed = 4,
        SkillRejected = 5,
    }

    /// <summary>
    /// 수동 패시브 발동 결과와 기존 스킬 실행 실패 원인을 함께 전달합니다.
    /// </summary>
    public readonly struct PassiveSkillActivationResult
    {
        public bool IsStarted { get; }
        public PassiveSkillActivationFailReason FailReason { get; }
        public SkillUseFailReason SkillFailReason { get; }

        /// <summary>
        /// 수동 패시브 발동 결과를 생성합니다.
        /// </summary>
        /// <param name="isStarted">실제 실행 스킬이 시작되었는지 여부입니다.</param>
        /// <param name="failReason">패시브 발동 계층의 실패 원인입니다.</param>
        /// <param name="skillFailReason">스킬 실행 계층의 세부 실패 원인입니다.</param>
        public PassiveSkillActivationResult(
            bool isStarted,
            PassiveSkillActivationFailReason failReason,
            SkillUseFailReason skillFailReason)
        {
            IsStarted = isStarted;
            FailReason = failReason;
            SkillFailReason = skillFailReason;
        }

        /// <summary>
        /// 실행 성공 결과를 반환합니다.
        /// </summary>
        public static PassiveSkillActivationResult Started =>
            new(true, PassiveSkillActivationFailReason.None, SkillUseFailReason.None);

        /// <summary>
        /// 지정한 원인으로 실패 결과를 생성합니다.
        /// </summary>
        /// <param name="reason">패시브 발동 계층의 실패 원인입니다.</param>
        /// <param name="skillFailReason">스킬 실행 계층의 세부 실패 원인입니다.</param>
        /// <returns>실패 상태가 기록된 결과입니다.</returns>
        public static PassiveSkillActivationResult Fail(
            PassiveSkillActivationFailReason reason,
            SkillUseFailReason skillFailReason = SkillUseFailReason.None)
        {
            return new PassiveSkillActivationResult(false, reason, skillFailReason);
        }
    }

    /// <summary>
    /// 장착된 수동 패시브를 실제 플레이어 스킬 실행 요청으로 변환합니다.
    /// </summary>
    /// <remarks>
    /// 입력 장치와 키 바인딩은 알지 않으며, 상위 입력 계층이 패시브 UID만 전달합니다.
    /// MP 차감과 쿨다운은 연결된 실행 스킬의 기존 <see cref="ICharacterSkillDriver"/> 흐름을 사용합니다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerPassiveSkillActivationController : MonoBehaviour
    {
        private CharacterPassiveSkillController _passiveSkillController;
        private ICharacterSkillDriver _skillDriver;
        private IPlayerSkillTargetingProvider _targetingProvider;

        /// <summary>
        /// 같은 캐릭터에 구성된 패시브 장착 상태와 스킬 실행 포트를 캐시합니다.
        /// </summary>
        private void Awake()
        {
            ResolveRuntimeReferences();
        }

        /// <summary>
        /// 지정한 패시브 스킬의 수동 발동을 시도합니다.
        /// </summary>
        /// <param name="passiveSkillUid">발동할 장착 패시브 스킬 UID입니다.</param>
        /// <returns>실행 시작 여부와 실패 원인이 포함된 결과입니다.</returns>
        public PassiveSkillActivationResult TryActivate(int passiveSkillUid)
        {
            if (passiveSkillUid <= 0)
            {
                return PassiveSkillActivationResult.Fail(
                    PassiveSkillActivationFailReason.InvalidInput);
            }

            ResolveRuntimeReferences();
            if (_passiveSkillController == null ||
                !_passiveSkillController.TryGetEquippedPassiveLevel(
                    passiveSkillUid,
                    out int passiveLevel))
            {
                return PassiveSkillActivationResult.Fail(
                    PassiveSkillActivationFailReason.NotEquipped);
            }

            TableSkillPassiveActivation table =
                TableLoaderManagerSkill.Instance?.TableSkillPassiveActivation;
            if (table == null ||
                !table.TryGetActivation(passiveSkillUid, passiveLevel, out var definition) ||
                definition == null ||
                !definition.IsValid)
            {
                return PassiveSkillActivationResult.Fail(
                    PassiveSkillActivationFailReason.DefinitionMissing);
            }

            if (_skillDriver == null)
            {
                return PassiveSkillActivationResult.Fail(
                    PassiveSkillActivationFailReason.InvalidInput);
            }

            SkillDriverRequest request;
            if (_targetingProvider != null)
            {
                if (!_targetingProvider.TryBuildSkillRequest(
                        gameObject,
                        definition.ExecutionSkillUid,
                        ConfigCommon.SkillTableSource.Player,
                        out request,
                        out SkillUseFailReason targetingFailReason))
                {
                    return PassiveSkillActivationResult.Fail(
                        PassiveSkillActivationFailReason.TargetingFailed,
                        targetingFailReason);
                }
            }
            else
            {
                Vector2 forward = ResolveForward2D();
                request = new SkillDriverRequest(
                    null,
                    transform.position,
                    forward,
                    ConfigCommon.SkillTableSource.Player);
            }

            SkillActivationOptions activationOptions = definition.BuildActivationOptions();
            request = request.WithActivationOptions(in activationOptions);
            SkillUseResult skillUseResult =
                _skillDriver.TryUseSkill(definition.ExecutionSkillUid, in request);

            return skillUseResult.IsStarted
                ? PassiveSkillActivationResult.Started
                : PassiveSkillActivationResult.Fail(
                    PassiveSkillActivationFailReason.SkillRejected,
                    skillUseResult.FailReason);
        }

        /// <summary>
        /// 늦게 부착될 수 있는 Skill 패키지 런타임 컴포넌트를 다시 확인합니다.
        /// </summary>
        private void ResolveRuntimeReferences()
        {
            _passiveSkillController ??= GetComponent<CharacterPassiveSkillController>();
            _skillDriver ??= GetComponent<ICharacterSkillDriver>();
            _targetingProvider ??= GetComponent<IPlayerSkillTargetingProvider>();
        }

        /// <summary>
        /// 타겟팅 제공자가 없을 때 사용할 캐릭터의 2D 전방 방향을 계산합니다.
        /// </summary>
        /// <returns>정규화된 2D 전방 방향입니다.</returns>
        private Vector2 ResolveForward2D()
        {
            CharacterBase character = GetComponent<CharacterBase>();
            if (character != null)
            {
                Vector2 facing = CharacterConstants.FacingToVector2(character.CurrentFacing);
                if (facing.sqrMagnitude > 1e-6f)
                {
                    return facing.normalized;
                }
            }

            Vector3 transformRight = transform.right;
            Vector2 fallback = new Vector2(transformRight.x, transformRight.y);
            return fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector2.right;
        }
    }
}
