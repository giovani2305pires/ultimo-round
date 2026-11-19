using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UltimoRound.Presentation
{
    /// <summary>
    /// Modelo humanoide tocado por um PlayableGraph em modo manual. O tempo de cada clipe é definido por
    /// quem chama (a partir do tick da simulação), então a animação nunca decide dano nem estado e o hitstop
    /// congela o corpo junto. Trocas de clipe usam crossfade; agachar/encolher são ajustes sobre o esqueleto.
    /// </summary>
    public sealed class AnimatedBody
    {
        public Transform Root { get; private set; }

        Transform _yaw;
        GameObject _instance;
        Animator _animator;
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly Dictionary<AnimationClip, int> _index = new Dictionary<AnimationClip, int>();
        readonly List<AnimationClipPlayable> _playables = new List<AnimationClipPlayable>();
        float[] _weights = new float[0];
        int _target = -1;
        float _fadeSpeed = 12f;
        readonly List<Material> _materials = new List<Material>();
        readonly List<Renderer> _renderers = new List<Renderer>();

        Transform _hips, _spine, _chest, _lThigh, _rThigh, _lShin, _rShin, _lFoot, _rFoot;
        float _legLength;
        float _crouch, _tuck, _lean;

        const float YawForward = 65f; // o peito fica voltado para a câmera, sem esconder o rosto

        public static AnimatedBody Build(Transform parent, FighterVisualSet set, FighterCostume costume)
        {
            var b = new AnimatedBody();
            b.BuildInternal(parent, set, costume);
            return b;
        }

        void BuildInternal(Transform parent, FighterVisualSet set, FighterCostume costume)
        {
            Root = new GameObject("Modelo_" + costume.Name).transform;
            Root.SetParent(parent, false);
            _yaw = new GameObject("Yaw").transform;
            _yaw.SetParent(Root, false);
            _instance = Object.Instantiate(costume.Prefab, _yaw, false);
            _instance.name = costume.Prefab.name;

            _animator = _instance.GetComponentInChildren<Animator>();
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.runtimeAnimatorController = null;

            foreach (var r in _instance.GetComponentsInChildren<Renderer>(true))
            {
                _renderers.Add(r);
                var mats = r.materials; // instâncias próprias: o flash de dano não afeta o outro lutador
                foreach (var m in mats)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                    _materials.Add(m);
                }
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            _hips = Bone(HumanBodyBones.Hips);
            _spine = Bone(HumanBodyBones.Spine);
            _chest = Bone(HumanBodyBones.Chest);
            if (_chest == null) _chest = _spine;
            _lThigh = Bone(HumanBodyBones.LeftUpperLeg);
            _rThigh = Bone(HumanBodyBones.RightUpperLeg);
            _lShin = Bone(HumanBodyBones.LeftLowerLeg);
            _rShin = Bone(HumanBodyBones.RightLowerLeg);
            _lFoot = Bone(HumanBodyBones.LeftFoot);
            _rFoot = Bone(HumanBodyBones.RightFoot);
            if (_lThigh != null && _lShin != null && _lFoot != null)
                _legLength = Vector3.Distance(_lThigh.position, _lShin.position) + Vector3.Distance(_lShin.position, _lFoot.position);

            _graph = PlayableGraph.Create("Lutador_" + costume.Name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Saida", _animator);
            var clips = CollectClips(set);
            _mixer = AnimationMixerPlayable.Create(_graph, clips.Count);
            _weights = new float[clips.Count];
            for (int i = 0; i < clips.Count; i++)
            {
                var p = AnimationClipPlayable.Create(_graph, clips[i]);
                p.SetApplyFootIK(true);
                p.Pause();
                _graph.Connect(p, 0, _mixer, i);
                _mixer.SetInputWeight(i, 0f);
                _playables.Add(p);
                _index[clips[i]] = i;
            }
            output.SetSourcePlayable(_mixer);
            _graph.Play();
            SetFacing(1f);
        }

        Transform Bone(HumanBodyBones b) => _animator.isHuman ? _animator.GetBoneTransform(b) : null;

        static List<AnimationClip> CollectClips(FighterVisualSet s)
        {
            var seen = new HashSet<AnimationClip>();
            var list = new List<AnimationClip>();
            void Add(AnimationClip c) { if (c != null && seen.Add(c)) list.Add(c); }
            Add(s.Idle); Add(s.WalkForward); Add(s.WalkBack); Add(s.HitHigh); Add(s.HitBody); Add(s.HitLow);
            Add(s.BlockHigh); Add(s.BlockBody); Add(s.BlockLow); Add(s.Knockdown); Add(s.KnockdownLoop); Add(s.GetUp);
            Add(s.KO); Add(s.Victory); Add(s.Intro); Add(s.JumpRise); Add(s.JumpFall); Add(s.DashForward); Add(s.DashBack);
            foreach (var m in s.Moves) { Add(m.Clip); Add(m.AltClip); }
            return list;
        }

        /// <param name="facing">-1..1: permite girar passando de frente para a câmera.</param>
        public void SetFacing(float facing)
        {
            _yaw.localEulerAngles = new Vector3(0, 180f - YawForward * facing, 0);
        }

        /// <summary>Define o clipe de destino e o tempo exato dele (segundos).</summary>
        public void Play(AnimationClip clip, float time, float fadeSeconds)
        {
            if (clip == null || !_index.TryGetValue(clip, out int i)) return;
            if (i != _target)
            {
                _target = i;
                _fadeSpeed = fadeSeconds <= 0.001f ? 1000f : 1f / fadeSeconds;
            }
            _playables[i].SetTime(Mathf.Max(0f, time));
        }

        public void SetOverlay(float crouch, float tuck, float lean)
        {
            _crouch = crouch;
            _tuck = tuck;
            _lean = lean;
        }

        /// <summary>Avança o crossfade, aplica a pose ao esqueleto e os ajustes de postura.</summary>
        public void Evaluate(float dt)
        {
            if (!_graph.IsValid() || _target < 0) return;
            float step = dt * _fadeSpeed;
            if (_weights[_target] <= 0f && dt <= 0f) step = 1f; // primeira pose não fica em branco
            _weights[_target] = Mathf.Min(1f, _weights[_target] + step);
            float others = 0f;
            for (int i = 0; i < _weights.Length; i++) if (i != _target) others += _weights[i];
            float remaining = 1f - _weights[_target];
            for (int i = 0; i < _weights.Length; i++)
            {
                if (i != _target && _weights[i] > 0f)
                {
                    _weights[i] = others > 0f ? _weights[i] / others * remaining : 0f;
                    if (_weights[i] < 0.001f) _weights[i] = 0f;
                }
                _mixer.SetInputWeight(i, _weights[i]);
            }
            _graph.Evaluate(0f);
            ApplyOverlay();
        }

        void ApplyOverlay()
        {
            if (_hips == null || _legLength <= 0f) return;
            float bend = Mathf.Max(_crouch * 62f, _tuck * 48f);
            Vector3 right = _yaw.right;
            if (bend > 0.5f)
            {
                BendLeg(_lThigh, _lShin, _lFoot, right, bend);
                BendLeg(_rThigh, _rShin, _rFoot, right, bend);
                // No chão o quadril desce o que as pernas encurtaram; no ar as pernas só encolhem.
                float drop = _legLength * (1f - Mathf.Cos(_crouch * 62f * Mathf.Deg2Rad));
                if (drop > 0f) _hips.position += Vector3.down * drop;
            }
            float lean = _lean + _crouch * 14f;
            if (Mathf.Abs(lean) > 0.1f && _spine != null)
                _spine.rotation = Quaternion.AngleAxis(lean, right) * _spine.rotation;
        }

        static void BendLeg(Transform thigh, Transform shin, Transform foot, Vector3 axis, float angle)
        {
            if (thigh == null || shin == null) return;
            thigh.rotation = Quaternion.AngleAxis(-angle, axis) * thigh.rotation;
            shin.rotation = Quaternion.AngleAxis(angle * 2f, axis) * shin.rotation;
            if (foot != null) foot.rotation = Quaternion.AngleAxis(-angle, axis) * foot.rotation;
        }

        public void SetEmission(Color c)
        {
            for (int i = 0; i < _materials.Count; i++) _materials[i].SetColor("_EmissionColor", c);
        }

        public void SetShadows(bool on)
        {
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < _renderers.Count; i++) _renderers[i].shadowCastingMode = mode;
        }

        public void Destroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
            foreach (var m in _materials) Object.Destroy(m);
            _materials.Clear();
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
