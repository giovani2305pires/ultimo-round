using UltimoRound.Combat;
using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>
    /// Overlay de desenvolvimento (opcional, modo treino): pushbox (amarelo), hurtboxes (verde),
    /// hitbox ativo (vermelho), hitbox em preparação/recuperação (cinza) e alcance de agarrão (magenta).
    /// Lê os mesmos volumes usados pela simulação.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        const int MaxRects = 24;
        LineRenderer[] _lines;
        int _used;
        public bool Visible { get; private set; }

        public void Init(Material mat)
        {
            _lines = new LineRenderer[MaxRects];
            for (int i = 0; i < MaxRects; i++)
            {
                var go = new GameObject("Box" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = 4;
                lr.widthMultiplier = 0.018f;
                lr.sharedMaterial = mat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _lines[i] = lr;
            }
        }

        public void SetVisible(bool v)
        {
            Visible = v;
            if (!v && _lines != null) foreach (var l in _lines) l.enabled = false;
        }

        void Rect(Rect r, Color c, float z = -0.6f)
        {
            if (_used >= MaxRects) return;
            var lr = _lines[_used++];
            lr.enabled = true;
            lr.startColor = lr.endColor = c;
            lr.SetPosition(0, new Vector3(r.xMin, r.yMin, z));
            lr.SetPosition(1, new Vector3(r.xMax, r.yMin, z));
            lr.SetPosition(2, new Vector3(r.xMax, r.yMax, z));
            lr.SetPosition(3, new Vector3(r.xMin, r.yMax, z));
        }

        public void Draw(FightSimulation sim)
        {
            if (!Visible || _lines == null) return;
            _used = 0;
            for (int i = 0; i < 2; i++)
            {
                var f = sim[i];
                float hw = f.Def.PushHalfWidth;
                Rect(new Rect(f.Pos.x - hw, f.PushBottom, hw * 2, f.PushTop - f.PushBottom), new Color(1f, 0.85f, 0.1f));
                var hurt = f.CurrentHurtboxes;
                if (!f.IsDown && hurt != null)
                    foreach (var b in hurt) Rect(b.ToWorld(f.Pos, f.Facing), new Color(0.2f, 1f, 0.35f), -0.62f);

                if (f.State == FighterStateId.Attack && f.Move != null)
                {
                    var m = f.Move;
                    if (m.IsThrow)
                    {
                        float reach = hw + m.ThrowRange;
                        float x0 = f.MoveFacing > 0 ? f.Pos.x : f.Pos.x - reach;
                        Rect(new Rect(x0, f.Pos.y + 0.6f, reach, 0.6f), m.IsActiveTick(f.MoveTick) ? new Color(1f, 0.2f, 1f) : new Color(0.5f, 0.3f, 0.5f), -0.64f);
                    }
                    else
                    {
                        bool active = m.HitIndexAt(f.MoveTick) >= 0;
                        Rect(m.Hitbox.ToWorld(f.Pos, f.MoveFacing), active ? new Color(1f, 0.15f, 0.15f) : new Color(0.45f, 0.45f, 0.45f), -0.64f);
                    }
                }
            }
            for (int i = _used; i < MaxRects; i++) _lines[i].enabled = false;
        }
    }
}
