using System.Collections.Generic;
using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>Público econômico: instâncias desenhadas em lote (sem GameObjects, Animator ou IA).</summary>
    public sealed class CrowdRenderer : MonoBehaviour
    {
        public Mesh BodyMesh;
        public Mesh HeadMesh;
        public Material[] BodyMaterials = new Material[0];
        public Material HeadMaterial;
        public List<Vector3> Seats = new List<Vector3>();

        Matrix4x4[][] _bodies;
        Matrix4x4[] _heads;
        int[] _groupOf;
        float[] _phase;
        float[] _freq;
        int[] _counts;
        int[] _fill;
        float _excite;

        void Start() => Rebuild();

        public void Rebuild()
        {
            int groups = Mathf.Max(1, BodyMaterials.Length);
            _counts = new int[groups];
            _groupOf = new int[Seats.Count];
            _phase = new float[Seats.Count];
            _freq = new float[Seats.Count];
            var rng = new System.Random(1234);
            for (int i = 0; i < Seats.Count; i++)
            {
                _groupOf[i] = rng.Next(groups);
                _counts[_groupOf[i]]++;
                _phase[i] = (float)rng.NextDouble() * 6.28f;
                _freq[i] = 1.2f + (float)rng.NextDouble() * 1.6f;
            }
            _fill = new int[groups];
            _bodies = new Matrix4x4[groups][];
            for (int g = 0; g < groups; g++) _bodies[g] = new Matrix4x4[_counts[g]];
            _heads = new Matrix4x4[Seats.Count];
        }

        public void Excite(float amount) => _excite = Mathf.Clamp01(Mathf.Max(_excite, amount));

        void Update()
        {
            if (_bodies == null || BodyMesh == null || HeadMesh == null || Seats.Count == 0) return;
            float t = Time.time;
            _excite = Mathf.MoveTowards(_excite, 0f, Time.deltaTime * 0.35f);
            var fill = _fill;
            System.Array.Clear(fill, 0, fill.Length);
            for (int i = 0; i < Seats.Count; i++)
            {
                float amp = 0.03f + _excite * 0.22f;
                float y = Mathf.Max(0f, Mathf.Sin(t * _freq[i] * (1f + _excite * 1.5f) + _phase[i])) * amp;
                var basePos = Seats[i] + new Vector3(0, y, 0);
                int g = _groupOf[i];
                _bodies[g][fill[g]++] = Matrix4x4.TRS(basePos + new Vector3(0, 0.35f, 0), Quaternion.identity, new Vector3(0.48f, 0.38f, 0.36f));
                _heads[i] = Matrix4x4.TRS(basePos + new Vector3(0, 0.86f, 0), Quaternion.identity, Vector3.one * 0.24f);
            }
            for (int g = 0; g < _bodies.Length; g++)
            {
                if (_bodies[g].Length == 0 || BodyMaterials[g] == null) continue;
                var rp = new RenderParams(BodyMaterials[g]) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
                Graphics.RenderMeshInstanced(rp, BodyMesh, 0, _bodies[g]);
            }
            if (HeadMaterial != null)
            {
                var rp = new RenderParams(HeadMaterial) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
                Graphics.RenderMeshInstanced(rp, HeadMesh, 0, _heads);
            }
        }
    }
}
