using System.Collections.Generic;
using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>Cordas laterais visuais (LineRenderer). O rebote é decidido pela simulação; aqui só há a animação.</summary>
    public sealed class RingRopes : MonoBehaviour
    {
        public LineRenderer[] Left = new LineRenderer[0];
        public LineRenderer[] Right = new LineRenderer[0];
        public float SideX = 4.75f;
        public float HalfDepth = 2.85f;
        public float[] Heights = { 0.45f, 0.85f, 1.25f };

        float _ampL, _ampR, _velL, _velR;

        public void Bounce(int side, float strength = 0.45f)
        {
            if (side < 0) _velL -= strength * 12f;
            else _velR += strength * 12f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Spring(ref _ampL, ref _velL, dt);
            Spring(ref _ampR, ref _velR, dt);
            Apply(Left, -SideX, _ampL);
            Apply(Right, SideX, _ampR);
        }

        static void Spring(ref float x, ref float v, float dt)
        {
            const float k = 140f, damp = 7f;
            v += (-k * x - damp * v) * dt;
            x += v * dt;
        }

        void Apply(LineRenderer[] ropes, float x, float amp)
        {
            for (int i = 0; i < ropes.Length; i++)
            {
                var lr = ropes[i];
                if (lr == null) continue;
                int n = lr.positionCount;
                float h = i < Heights.Length ? Heights[i] : 0.5f;
                for (int p = 0; p < n; p++)
                {
                    float u = p / (float)(n - 1);
                    float z = Mathf.Lerp(-HalfDepth, HalfDepth, u);
                    float bend = Mathf.Sin(u * Mathf.PI) * amp * (1f - i * 0.15f);
                    lr.SetPosition(p, new Vector3(x + bend, h, z));
                }
            }
        }
    }
}
