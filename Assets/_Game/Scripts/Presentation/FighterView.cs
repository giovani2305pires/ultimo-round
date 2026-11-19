using UltimoRound.Combat;
using UnityEngine;

namespace UltimoRound.Presentation
{
    public enum PreviewMode { Guard, Intro, Victory, Walk }

    /// <summary>
    /// Apresenta um <see cref="FighterState"/>: posição interpolada entre ticks, orientação espelhada, pose procedural,
    /// flash de dano e tremor durante o hitstop. Só lê a simulação.
    /// </summary>
    public sealed class FighterView : MonoBehaviour
    {
        public ProceduralBody Body { get; private set; }
        public AnimatedBody Model { get; private set; }
        public FighterDefinition Definition { get; private set; }
        FighterVisualSet _visuals;

        // Reprodução do modelo: tempo de clipes em laço e posturas suavizadas.
        AnimationClip _loopClip;
        float _loopTime;
        float _crouch, _tuck, _lean;
        float _crouchTarget, _tuckTarget, _leanTarget;
        AnimationClip _hitClip;
        AnimationClip _blockClip;

        Vector2 _prevTickPos, _curTickPos;
        float _flash;
        Color _flashColor;
        float _walkPhase;
        float _shake;
        int _lastFacing = 1;
        float _visualFacing = 1f;
        FighterPose _current;
        bool _hasPose;

        public void Build(FighterDefinition def, int palette, Material template, FighterVisualSet visuals = null)
        {
            if (Body != null) { Body.Destroy(); Body = null; }
            if (Model != null) { Model.Destroy(); Model = null; }
            Definition = def;
            _visuals = visuals;
            var costume = visuals != null ? visuals.Costume(palette) : null;
            if (costume != null && costume.Prefab != null)
            {
                Model = AnimatedBody.Build(transform, visuals, costume);
                _hitClip = visuals.HitBody;
                _blockClip = visuals.BlockHigh;
                _loopClip = null;
                _crouch = _tuck = _lean = 0f;
                Model.Play(visuals.Idle, 0f, 0f);
                Model.Evaluate(0f);
            }
            else Body = ProceduralBody.Build(transform, def, palette, template);
            _hasPose = false;
        }

        void OnDestroy()
        {
            Body?.Destroy();
            Model?.Destroy();
        }

        public void SetShadows(bool on)
        {
            Body?.SetShadows(on);
            Model?.SetShadows(on);
        }

        /// <summary>Reação de acordo com a altura do golpe recebido (só visual).</summary>
        public void NotifyHit(AttackLevel level)
        {
            if (_visuals == null) return;
            _hitClip = level == AttackLevel.Low ? _visuals.HitLow : level == AttackLevel.Mid ? _visuals.HitBody : _visuals.HitHigh;
        }

        public void NotifyBlock(AttackLevel level)
        {
            if (_visuals == null) return;
            _blockClip = level == AttackLevel.Low ? _visuals.BlockLow : level == AttackLevel.Mid ? _visuals.BlockBody : _visuals.BlockHigh;
        }

        public void SnapTo(FighterState s)
        {
            _prevTickPos = _curTickPos = s.Pos;
            _lastFacing = s.Facing;
            _visualFacing = s.Facing;
            _hasPose = false;
        }

        /// <summary>Chamado após cada tick da simulação.</summary>
        public void PushTick(FighterState s)
        {
            _prevTickPos = _curTickPos;
            _curTickPos = s.Pos;
        }

        public void Flash(Color c, float seconds)
        {
            _flashColor = c;
            _flash = seconds;
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        public Vector3 WorldCenter => transform.position + Vector3.up * 1.1f;

        /// <param name="alpha">Fração do tick atual (0..1).</param>
        /// <param name="frozen">Simulação em hitstop/congelamento.</param>
        public void Render(FighterState s, float alpha, bool frozen, float dt)
        {
            Vector2 p = frozen ? _curTickPos : Vector2.Lerp(_prevTickPos, _curTickPos, alpha);
            Vector3 offset = Vector3.zero;
            if (_shake > 0f)
            {
                offset.x = (Mathf.PerlinNoise(Time.unscaledTime * 60f, 0.3f) - 0.5f) * _shake;
                if (!frozen) _shake = Mathf.MoveTowards(_shake, 0f, dt * 0.5f);
            }
            transform.position = new Vector3(p.x, p.y, 0f) + offset;

            // Virar rapidamente (poucos quadros) em vez de instantâneo para não "piscar".
            if (s.Facing != _lastFacing) _lastFacing = s.Facing;
            _visualFacing = Mathf.MoveTowards(_visualFacing, _lastFacing, dt * 14f);
            if (Model != null)
            {
                Model.SetFacing(_visualFacing);
                PlayState(s, frozen ? 0f : alpha, frozen ? 0f : dt);
                UpdateOverlay(dt);
                Model.Evaluate(dt);
                UpdateFlash(dt);
                return;
            }
            float sx = Mathf.Abs(_visualFacing) < 0.15f ? 0.15f * Mathf.Sign(_visualFacing == 0 ? 1 : _visualFacing) : _visualFacing;
            Body.Root.localScale = new Vector3(sx, 1f, 1f);

            var target = EvaluatePose(s, alpha, frozen, dt);
            // Transições suaves entre estados; golpes usam a pose direta para não esconder a preparação.
            bool direct = s.State == FighterStateId.Attack || s.State == FighterStateId.Hitstun || s.State == FighterStateId.Blockstun || frozen;
            _current = !_hasPose || direct ? target : FighterPose.Lerp(_current, target, 1f - Mathf.Exp(-dt * 22f));
            _hasPose = true;
            Body.Apply(_current);
            UpdateFlash(dt);
        }

        const float ModelFlash = 0.3f; // sem mapa de emissão a cor soma por igual: forte demais apagaria a textura

        void UpdateFlash(float dt)
        {
            if (_flash <= 0f) return;
            _flash -= dt;
            Color c = _flash > 0f ? _flashColor * Mathf.Clamp01(_flash * 8f) : Color.black;
            Body?.SetEmission(c);
            Model?.SetEmission(c * ModelFlash);
        }

        // ---------------------------------------------------------------- MODELO ANIMADO

        void Loop(AnimationClip c, float rate, float dt, float fade)
        {
            if (c == null) return;
            if (_loopClip != c) { _loopClip = c; _loopTime = 0f; }
            _loopTime += dt * rate;
            Model.Play(c, Mathf.Repeat(_loopTime, c.length), fade);
        }

        void Span(AnimationClip c, float k, float fade)
        {
            if (c == null) return;
            _loopClip = null;
            Model.Play(c, Mathf.Clamp01(k) * c.length, fade);
        }

        void Posture(float crouch, float tuck, float lean)
        {
            _crouchTarget = crouch;
            _tuckTarget = tuck;
            _leanTarget = lean;
        }

        void UpdateOverlay(float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 20f);
            _crouch = Mathf.Lerp(_crouch, _crouchTarget, k);
            _tuck = Mathf.Lerp(_tuck, _tuckTarget, k);
            _lean = Mathf.Lerp(_lean, _leanTarget, k);
            Model.SetOverlay(_crouch, _tuck, _lean);
        }

        /// <summary>Queda: o clipe de derrubada avança até o fim e passa para o laço no chão.</summary>
        void PlayFall(AnimationClip fall, float seconds, float dt)
        {
            var v = _visuals;
            if (fall != null && seconds < fall.length) Span(fall, seconds / fall.length, 0.05f);
            else Loop(v.KnockdownLoop != null ? v.KnockdownLoop : fall, 1f, dt, 0.15f);
        }

        void PlayState(FighterState s, float sub, float dt)
        {
            var v = _visuals;
            float tick = s.StateTick + sub;
            float seconds = tick / CombatConfig.TickRate;
            switch (s.State)
            {
                case FighterStateId.Idle:
                    Loop(v.Idle, 1f, dt, 0.12f);
                    Posture(0f, 0f, 0f);
                    break;
                case FighterStateId.Walk:
                {
                    bool forward = Mathf.Sign(s.Vel.x) == s.Facing;
                    Loop(forward ? v.WalkForward : v.WalkBack, 1.15f, dt, 0.1f);
                    Posture(0f, 0f, forward ? 4f : -3f);
                    break;
                }
                case FighterStateId.Crouch:
                    Loop(v.Idle, 0.6f, dt, 0.1f);
                    Posture(1f, 0f, 0f);
                    break;
                case FighterStateId.Guard:
                    Span(s.Crouching ? v.BlockLow : v.BlockHigh, 0.4f, 0.07f);
                    Posture(s.Crouching ? 1f : 0f, 0f, 0f);
                    break;
                case FighterStateId.Blockstun:
                    Span(_blockClip, tick / Mathf.Max(1f, s.StunTicks), 0.03f);
                    Posture(s.Crouching ? 1f : 0f, 0f, 0f);
                    break;
                case FighterStateId.Dash:
                    Loop(v.DashForward != null ? v.DashForward : v.WalkForward, v.DashForward != null ? 1.3f : 2.2f, dt, 0.06f);
                    Posture(0.15f, 0f, 16f);
                    break;
                case FighterStateId.BackDash:
                    Loop(v.DashBack != null ? v.DashBack : v.WalkBack, v.DashBack != null ? 1.3f : 2.2f, dt, 0.06f);
                    Posture(0.1f, 0f, -8f);
                    break;
                case FighterStateId.PreJump:
                case FighterStateId.Landing:
                    Loop(v.Idle, 1f, dt, 0.06f);
                    Posture(0.45f, 0f, 4f);
                    break;
                case FighterStateId.Air:
                {
                    var c = s.Vel.y > 0f && v.JumpRise != null ? v.JumpRise : (v.JumpFall != null ? v.JumpFall : v.Idle);
                    Loop(c, 1f, dt, 0.1f);
                    Posture(0f, 1f, 0f);
                    break;
                }
                case FighterStateId.Attack:
                    PlayMove(s, sub);
                    break;
                case FighterStateId.Hitstun:
                    Span(s.Crouching ? v.HitLow : _hitClip, tick / Mathf.Max(1f, s.StunTicks), 0.03f);
                    Posture(s.Crouching ? 1f : 0f, 0f, 0f);
                    break;
                case FighterStateId.Launched:
                    PlayFall(v.Knockdown, Mathf.Min(seconds, v.Knockdown != null ? v.Knockdown.length * 0.5f : 0f), dt);
                    Posture(0f, 0f, 0f);
                    break;
                case FighterStateId.RopeBounce:
                    Span(v.HitBody, 0.5f, 0.03f);
                    Posture(0f, 0f, -18f);
                    break;
                case FighterStateId.Knockdown:
                {
                    float start = v.Knockdown != null ? v.Knockdown.length * 0.5f : 0f;
                    PlayFall(v.Knockdown, start + seconds, dt);
                    Posture(0f, 0f, 0f);
                    break;
                }
                case FighterStateId.KO:
                {
                    var fall = v.KO != null ? v.KO : v.Knockdown;
                    float start = s.Airborne ? 0f : (fall != null ? fall.length * 0.4f : 0f);
                    PlayFall(fall, start + seconds, dt);
                    Posture(0f, 0f, 0f);
                    break;
                }
                case FighterStateId.GetUp:
                    Span(v.GetUp, tick / 22f, 0.1f);
                    Posture(0f, 0f, 0f);
                    break;
                case FighterStateId.Grabbing:
                {
                    var a = s.Move != null ? v.Find(s.Move.Slot) : v.Find(MoveSlot.Grab);
                    if (a != null && a.Clip != null)
                    {
                        float strike = StrikeOf(a, a.Clip);
                        float time = tick <= 11f ? strike : Mathf.Lerp(strike, a.Clip.length, (tick - 11f) / 15f);
                        _loopClip = null;
                        Model.Play(a.Clip, time, 0.05f);
                    }
                    Posture(0f, 0f, 0f);
                    break;
                }
                case FighterStateId.Grabbed:
                    Span(v.HitBody, 0.45f, 0.05f);
                    Posture(0f, 0f, 0f);
                    break;
                case FighterStateId.ThrowTech:
                    Span(v.BlockBody, tick / 16f, 0.05f);
                    Posture(0f, 0f, 0f);
                    break;
                case FighterStateId.Victory:
                    Loop(v.Victory, 0.8f, dt, 0.2f);
                    Posture(0f, 0f, 0f);
                    break;
            }
        }

        static float StrikeOf(MoveAnimation a, AnimationClip clip)
        {
            float strike = clip == a.AltClip ? a.AltStrikeTime : a.StrikeTime;
            return strike > 0f ? strike : clip.length * 0.45f;
        }

        /// <summary>
        /// O tempo do clipe é função do tick do golpe: o impacto do clipe coincide com o primeiro tick ativo,
        /// qualquer que seja o frame data, e a recuperação ocupa o restante.
        /// </summary>
        void PlayMove(FighterState s, float sub)
        {
            var m = s.Move;
            var a = m != null ? _visuals.Find(m.Slot) : null;
            if (a == null || a.Clip == null) { Span(_visuals.Idle, 0f, 0.1f); Posture(0f, 0f, 0f); return; }

            float t = s.MoveTick + sub;
            float activeEnd = m.Startup + m.Active;
            var clip = a.Clip;
            float time;
            if (m.Hits > 1 && m.HitInterval > 0 && t >= m.Startup && t < activeEnd)
            {
                // Multi-hit: cada hit explícito refaz a extensão, alternando os clipes quando houver dois.
                float local = (t - m.Startup) / m.HitInterval;
                int idx = Mathf.FloorToInt(local);
                if (a.AltClip != null && idx % 2 == 1) clip = a.AltClip;
                float strike = StrikeOf(a, clip);
                time = Mathf.Lerp(strike * 0.5f, strike + 0.03f, Mathf.Min(1f, (local - idx) * 1.4f));
            }
            else
            {
                float strike = StrikeOf(a, clip);
                float hold = Mathf.Min(0.05f, Mathf.Max(0f, clip.length - strike));
                if (t < m.Startup) time = strike * (t / Mathf.Max(1, m.Startup));
                else if (t < activeEnd) time = strike + hold * ((t - m.Startup) / Mathf.Max(1, m.Active));
                else time = Mathf.Lerp(strike + hold, clip.length, (t - activeEnd) / Mathf.Max(1, m.Recovery));
            }
            _loopClip = null;
            Model.Play(clip, time, 0.05f);
            Posture(a.Crouch, a.Tuck, 0f);
        }

        FighterPose EvaluatePose(FighterState s, float alpha, bool frozen, float dt)
        {
            var style = Definition.Guard;
            float t = Time.time;
            float sub = frozen ? 0f : alpha;
            switch (s.State)
            {
                case FighterStateId.Idle:
                {
                    var g = PoseLibrary.Guard(style);
                    float bounce = Mathf.Sin(t * Definition.IdleBounce * Mathf.PI * 2f);
                    g.HipY += bounce * 0.018f;
                    g.FKnee += bounce * 4f;
                    g.RKnee += bounce * 4f;
                    g.FShoulder += bounce * 2f;
                    return g;
                }
                case FighterStateId.Walk:
                {
                    var g = PoseLibrary.Guard(style);
                    float dir = Mathf.Sign(s.Vel.x) * s.Facing;
                    _walkPhase += dt * (dir >= 0 ? 9f : -8f);
                    float sw = Mathf.Sin(_walkPhase);
                    g.FHip += sw * 16f;
                    g.RHip -= sw * 16f;
                    g.FKnee += Mathf.Max(0, -sw) * 22f;
                    g.RKnee += Mathf.Max(0, sw) * 22f;
                    g.HipY += Mathf.Abs(Mathf.Cos(_walkPhase)) * 0.02f;
                    return g;
                }
                case FighterStateId.Crouch:
                    return PoseLibrary.Crouch(style);
                case FighterStateId.Guard:
                case FighterStateId.Blockstun:
                {
                    var b = PoseLibrary.Block(style, s.Crouching);
                    if (s.State == FighterStateId.Blockstun) b.HipX -= 0.05f;
                    return b;
                }
                case FighterStateId.Dash:
                    return PoseLibrary.Dash(style, false);
                case FighterStateId.BackDash:
                    return PoseLibrary.Dash(style, true);
                case FighterStateId.PreJump:
                case FighterStateId.Landing:
                    return PoseLibrary.Squat(style);
                case FighterStateId.Air:
                {
                    var a = PoseLibrary.Air(style);
                    a.Lean += Mathf.Clamp(-s.Vel.y * 1.5f, -10f, 12f);
                    return a;
                }
                case FighterStateId.Attack:
                    return AttackPose(s, sub);
                case FighterStateId.Hitstun:
                {
                    var h = PoseLibrary.Hitstun(style, s.Crouching, s.Crouching);
                    float k = Mathf.Clamp01((s.StateTick + sub) / Mathf.Max(1f, s.StunTicks));
                    return FighterPose.Lerp(h, s.Crouching ? PoseLibrary.Crouch(style) : PoseLibrary.Guard(style), FighterPose.EaseInOut(k));
                }
                case FighterStateId.Launched:
                case FighterStateId.RopeBounce:
                {
                    var l = PoseLibrary.Launched(style);
                    l.BodyRoll = Mathf.Clamp(35f + (s.StateTick + sub) * 2.2f, 0f, 80f);
                    if (s.State == FighterStateId.RopeBounce) l.BodyRoll = 20f;
                    return l;
                }
                case FighterStateId.Knockdown:
                    return PoseLibrary.Lying(Body.HipHeight, false);
                case FighterStateId.KO:
                {
                    if (s.Airborne)
                    {
                        var l = PoseLibrary.Launched(style);
                        l.BodyRoll = Mathf.Clamp(30f + (s.StateTick + sub) * 3f, 0f, 85f);
                        return l;
                    }
                    return PoseLibrary.Lying(Body.HipHeight, true);
                }
                case FighterStateId.GetUp:
                {
                    float k = (s.StateTick + sub) / 22f;
                    var lying = PoseLibrary.Lying(Body.HipHeight, false);
                    var crouch = PoseLibrary.Crouch(style);
                    return k < 0.5f ? FighterPose.Lerp(lying, crouch, FighterPose.EaseInOut(k * 2f))
                                    : FighterPose.Lerp(crouch, PoseLibrary.Guard(style), FighterPose.EaseInOut((k - 0.5f) * 2f));
                }
                case FighterStateId.Grabbing:
                {
                    PoseLibrary.Attack(AnimPose.Grab, style, out _, out var strike);
                    if (s.StateTick > 11) { strike.Lean = -18; strike.FShoulder = 150; strike.RShoulder = 150; strike.Twist = 40; }
                    return strike;
                }
                case FighterStateId.Grabbed:
                {
                    var h = PoseLibrary.Hitstun(style, false, false);
                    h.Lean = 18; h.HipY = 0.03f;
                    return h;
                }
                case FighterStateId.ThrowTech:
                {
                    var g = PoseLibrary.Guard(style);
                    g.FShoulder = 80; g.FElbow = 10; g.RShoulder = 75; g.RElbow = 10; g.Lean = -6;
                    return FighterPose.Lerp(g, PoseLibrary.Guard(style), (s.StateTick + sub) / 16f);
                }
                case FighterStateId.Victory:
                    return PoseLibrary.Victory(style, t);
                default:
                    return PoseLibrary.Guard(style);
            }
        }

        FighterPose AttackPose(FighterState s, float sub)
        {
            var m = s.Move;
            var style = Definition.Guard;
            if (m == null) return PoseLibrary.Guard(style);
            var basePose = s.Airborne ? PoseLibrary.Air(style) : (m.Slot == MoveSlot.CrouchPunch || m.Slot == MoveSlot.Low ? PoseLibrary.Crouch(style) : PoseLibrary.Guard(style));
            var kind = m.Pose;
            if (kind == AnimPose.Signature && m.Hits > 1) kind = AnimPose.Flurry;
            PoseLibrary.Attack(kind, style, out var windup, out var strike);
            if (s.Airborne) { windup.HipY = basePose.HipY; }

            float t = s.MoveTick + sub;
            float startup = Mathf.Max(1, m.Startup);
            float activeEnd = m.Startup + m.Active;

            if (kind == AnimPose.Flurry && t >= m.Startup && t < activeEnd)
            {
                // Rajada: alterna braços a cada hit explícito.
                float interval = Mathf.Max(2, m.HitInterval);
                float local = (t - m.Startup) / interval;
                int idx = Mathf.FloorToInt(local);
                float k = local - idx;
                var a = strike;
                if (idx % 2 == 1)
                {
                    a.FShoulder = basePose.FShoulder; a.FElbow = basePose.FElbow;
                    a.RShoulder = 92; a.RElbow = 2; a.Twist = 30;
                }
                float ext = k < 0.5f ? FighterPose.EaseOut(k * 2f) : 1f - (k - 0.5f) * 1.2f;
                return FighterPose.Lerp(windup, a, ext);
            }

            if (t < startup)
            {
                float k = t / startup;
                // 65% para preparar, 35% para estender até o primeiro tick ativo: a preparação fica visível.
                if (k < 0.65f) return FighterPose.Lerp(basePose, windup, FighterPose.EaseInOut(k / 0.65f));
                return FighterPose.Lerp(windup, strike, FighterPose.EaseOut((k - 0.65f) / 0.35f));
            }
            if (t < activeEnd) return strike;
            float rec = (t - activeEnd) / Mathf.Max(1, m.Recovery);
            return FighterPose.Lerp(strike, basePose, FighterPose.EaseInOut(rec));
        }

        // ---------------------------------------------------------------- PRÉVIA (menus)

        public void RenderPreview(PreviewMode mode, float dt)
        {
            if (Model != null)
            {
                var clip = mode == PreviewMode.Intro && _visuals.Intro != null ? _visuals.Intro
                    : mode == PreviewMode.Victory && _visuals.Victory != null ? _visuals.Victory : _visuals.Idle;
                Loop(clip, 1f, dt, 0.15f);
                Posture(0f, 0f, 0f);
                UpdateOverlay(dt);
                Model.Evaluate(dt);
                return;
            }
            if (Body == null) return;
            FighterPose p;
            float t = Time.unscaledTime;
            switch (mode)
            {
                case PreviewMode.Intro: p = PoseLibrary.Intro(Definition.Guard, t); break;
                case PreviewMode.Victory: p = PoseLibrary.Victory(Definition.Guard, t); break;
                default:
                {
                    p = PoseLibrary.Guard(Definition.Guard);
                    float bounce = Mathf.Sin(t * Definition.IdleBounce * Mathf.PI * 2f);
                    p.HipY += bounce * 0.018f;
                    p.FKnee += bounce * 4f;
                    p.RKnee += bounce * 4f;
                    break;
                }
            }
            _current = _hasPose ? FighterPose.Lerp(_current, p, 1f - Mathf.Exp(-dt * 12f)) : p;
            _hasPose = true;
            Body.Apply(_current);
        }

        public void SetFacing(int facing)
        {
            _lastFacing = facing;
            _visualFacing = facing;
            if (Body != null) Body.Root.localScale = new Vector3(facing, 1f, 1f);
            Model?.SetFacing(facing);
        }
    }
}
