// Assets/GGemCo/Skills/Runtime/Anim/SkillAnimationPlayer.cs
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Animator 파라미터 없이 AnimationClip을 Playables API로 직접 재생한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;

        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _a;
        private AnimationClipPlayable _b;

        private bool _initialized;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            InitializeIfNeeded();
        }

        private void OnDisable()
        {
            if (_graph.IsValid())
                _graph.Destroy();
            _initialized = false;
        }

        private void InitializeIfNeeded()
        {
            if (_initialized) return;

            _graph = PlayableGraph.Create($"{name}.SkillAnimGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            _output = AnimationPlayableOutput.Create(_graph, "SkillAnimOutput", animator);
            _output.SetSourcePlayable(_mixer); // Animator에 출력 연결 :contentReference[oaicite:3]{index=3}

            _graph.Play();
            _initialized = true;
        }

        public ClipHandle PlayOneShot(AnimationClip clip, float fadeIn = 0.05f)
        {
            InitializeIfNeeded();
            if (clip == null) return default;

            // A/B 토글로 cross-fade
            var useA = !_a.IsValid() || _mixer.GetInputWeight(0) < 0.5f;
            if (useA)
            {
                if (_a.IsValid()) _a.Destroy();
                _a = AnimationClipPlayable.Create(_graph, clip); // 클립을 Playable로 래핑하여 재생 :contentReference[oaicite:4]{index=4}
                _a.SetApplyFootIK(false);
                _a.SetTime(0);
                _a.SetDuration(clip.length);

                _graph.Connect(_a, 0, _mixer, 0);
                _mixer.SetInputWeight(0, 1f);
                _mixer.SetInputWeight(1, 0f);
                return new ClipHandle(this, 0, clip.length, fadeIn);
            }
            else
            {
                if (_b.IsValid()) _b.Destroy();
                _b = AnimationClipPlayable.Create(_graph, clip);
                _b.SetApplyFootIK(false);
                _b.SetTime(0);
                _b.SetDuration(clip.length);

                _graph.Connect(_b, 0, _mixer, 1);
                _mixer.SetInputWeight(1, 1f);
                _mixer.SetInputWeight(0, 0f);
                return new ClipHandle(this, 1, clip.length, fadeIn);
            }
        }

        public LoopHandle PlayLoop(AnimationClip clip, float fadeIn = 0.05f)
        {
            InitializeIfNeeded();
            if (clip == null) return default;

            // Loop은 B 채널을 우선 사용(정책). 필요시 더 정교한 혼합 확장 가능.
            if (_b.IsValid()) _b.Destroy();
            _b = AnimationClipPlayable.Create(_graph, clip);
            _b.SetApplyFootIK(false);
            _b.SetTime(0);
            _b.SetDuration(double.PositiveInfinity);
            // _b.SetTimeWrapMode(DirectorWrapMode.Loop);

            _graph.Connect(_b, 0, _mixer, 1);
            _mixer.SetInputWeight(1, 1f);
            _mixer.SetInputWeight(0, 0f);

            return new LoopHandle(this, 1, fadeIn);
        }

        internal void SetMixerWeight(int index, float weight)
        {
            if (!_initialized) return;
            if ((uint)index > 1u) return;
            _mixer.SetInputWeight(index, Mathf.Clamp01(weight));
        }

        public readonly struct ClipHandle
        {
            private readonly SkillAnimationPlayer _player;
            public readonly int channelIndex;
            public readonly float duration;
            public readonly float fadeIn;

            internal ClipHandle(SkillAnimationPlayer player, int channelIndex, float duration, float fadeIn)
            {
                _player = player;
                this.channelIndex = channelIndex;
                this.duration = duration;
                this.fadeIn = fadeIn;
            }

            public bool IsValid => _player != null;

            public void ApplyFadeIn(float t01)
            {
                if (!IsValid) return;
                _player.SetMixerWeight(channelIndex, t01);
                _player.SetMixerWeight(1 - channelIndex, 1f - t01);
            }
        }

        public readonly struct LoopHandle
        {
            private readonly SkillAnimationPlayer _player;
            public readonly int channelIndex;
            public readonly float fadeIn;

            internal LoopHandle(SkillAnimationPlayer player, int channelIndex, float fadeIn)
            {
                _player = player;
                this.channelIndex = channelIndex;
                this.fadeIn = fadeIn;
            }

            public bool IsValid => _player != null;

            public void Stop(float fadeOutSeconds = 0.05f)
            {
                if (!IsValid) return;
                // 간단 정책: weight를 0으로 보내며 종료 (필요 시 코루틴으로 fadeOut 구현)
                _player.SetMixerWeight(channelIndex, 0f);
            }
        }
    }
}
