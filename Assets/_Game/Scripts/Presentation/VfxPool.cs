using UnityEngine;

namespace UltimoRound.Presentation
{
    public enum VfxKind { Hit, HeavyHit, Block, Dust, Signature, Tech, KO, Special, Lightning, Flames, Tornado, Implosion, Heal }

    /// <summary>Efeitos de partículas criados uma vez e reutilizados (sem instanciar durante a luta).</summary>
    public sealed class VfxPool : MonoBehaviour
    {
        const int PerKind = 4;
        const int PerRareKind = 2;
        ParticleSystem[][] _pools;
        int[] _next;

        public void Init(Material particleMaterial, GameObject[] prefabs = null)
        {
            int kinds = System.Enum.GetValues(typeof(VfxKind)).Length;
            _pools = new ParticleSystem[kinds][];
            _next = new int[kinds];
            for (int k = 0; k < kinds; k++)
            {
                _pools[k] = new ParticleSystem[(VfxKind)k <= VfxKind.Dust ? PerKind : PerRareKind];
                var prefab = prefabs != null && k < prefabs.Length ? prefabs[k] : null;
                for (int i = 0; i < _pools[k].Length; i++)
                    _pools[k][i] = prefab != null ? FromPrefab(prefab, (VfxKind)k) : Create((VfxKind)k, particleMaterial);
            }
        }

        ParticleSystem FromPrefab(GameObject prefab, VfxKind kind)
        {
            var go = Instantiate(prefab, transform);
            go.name = "VFX_" + kind;
            go.transform.localScale = Vector3.one * Scale(kind);
            // Raiz sempre com sistema próprio: Play(true) dispara todos os filhos do efeito.
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                ps = go.AddComponent<ParticleSystem>();
                var em = ps.emission;
                em.enabled = false;
                go.GetComponent<ParticleSystemRenderer>().enabled = false;
            }
            Timing(kind, out float maxDuration, out float maxLife);
            foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = p.main;
                main.playOnAwake = false;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                // Os efeitos do pacote duram segundos; numa luta o impacto precisa acabar logo.
                main.duration = Mathf.Min(main.duration, maxDuration);
                main.startLifetime = ClampLife(main.startLifetime, maxLife);
                // Halo e fumaça pretos viram mancha sobre a lona e escondem quem foi atingido.
                if (IsBlack(main.startColor))
                {
                    var em = p.emission;
                    em.enabled = false;
                }
                if (kind == VfxKind.Dust) MakeDust(p);
            }
            return ps;
        }

        static bool IsBlack(ParticleSystem.MinMaxGradient c)
        {
            return c.mode == ParticleSystemGradientMode.Color && c.color.r + c.color.g + c.color.b < 0.02f;
        }

        static ParticleSystem.MinMaxCurve ClampLife(ParticleSystem.MinMaxCurve life, float max)
        {
            switch (life.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(Mathf.Min(life.constant, max));
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(Mathf.Min(life.constantMin, max), Mathf.Min(life.constantMax, max));
                default:
                    life.curveMultiplier = Mathf.Min(life.curveMultiplier, max);
                    return life;
            }
        }

        /// <summary>A fumaça do pacote é escura; a poeira da lona é clara e bem mais discreta.</summary>
        static void MakeDust(ParticleSystem p)
        {
            var main = p.main;
            main.startColor = new Color(0.86f, 0.84f, 0.78f, 0.4f);
            var col = p.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0.55f, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;
        }

        static void Timing(VfxKind kind, out float duration, out float life)
        {
            switch (kind)
            {
                case VfxKind.Hit: duration = 0.1f; life = 0.45f; break;
                case VfxKind.HeavyHit: duration = 0.1f; life = 0.6f; break;
                case VfxKind.Block: duration = 0.12f; life = 0.4f; break;
                case VfxKind.Dust: duration = 0.15f; life = 0.7f; break;
                case VfxKind.Signature: duration = 0.5f; life = 0.7f; break;
                case VfxKind.Tech: duration = 0.2f; life = 0.5f; break;
                case VfxKind.KO: duration = 0.3f; life = 1.4f; break;
                case VfxKind.Special: duration = 0.25f; life = 0.8f; break;
                case VfxKind.Lightning: duration = 0.3f; life = 0.5f; break;
                case VfxKind.Flames: duration = 0.5f; life = 0.8f; break;
                case VfxKind.Tornado: duration = 0.6f; life = 0.8f; break;
                case VfxKind.Implosion: duration = 0.4f; life = 0.6f; break;
                default: duration = 1.2f; life = 1.2f; break;
            }
        }

        static float Scale(VfxKind kind)
        {
            switch (kind)
            {
                case VfxKind.HeavyHit: return 0.3f;
                case VfxKind.Signature: return 0.9f;
                case VfxKind.KO: return 0.6f;
                case VfxKind.Special: return 0.35f;
                case VfxKind.Dust: return 0.5f;
                case VfxKind.Block: return 0.35f;
                case VfxKind.Tech: return 0.4f;
                case VfxKind.Lightning: return 0.5f;
                case VfxKind.Flames: return 0.3f;
                case VfxKind.Tornado: return 0.6f;
                case VfxKind.Implosion: return 0.5f;
                case VfxKind.Heal: return 0.6f;
                default: return 0.45f;
            }
        }

        ParticleSystem Create(VfxKind kind, Material mat)
        {
            var go = new GameObject("VFX_" + kind);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            main.maxParticles = 48;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1.2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            Color a, b;
            short burst;
            switch (kind)
            {
                case VfxKind.KO:
                case VfxKind.Special:
                case VfxKind.Flames:
                case VfxKind.Tornado:
                case VfxKind.HeavyHit:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
                    a = new Color(1f, 0.95f, 0.7f); b = new Color(1f, 0.45f, 0.1f); burst = 26;
                    break;
                case VfxKind.Block:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                    a = new Color(0.75f, 0.9f, 1f); b = new Color(0.2f, 0.5f, 1f); burst = 14;
                    break;
                case VfxKind.Dust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                    main.gravityModifier = -0.05f;
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.radius = 0.3f;
                    a = new Color(0.8f, 0.78f, 0.72f, 0.5f); b = new Color(0.5f, 0.5f, 0.5f, 0f); burst = 12;
                    break;
                case VfxKind.Lightning:
                case VfxKind.Implosion:
                case VfxKind.Heal:
                case VfxKind.Signature:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                    shape.radius = 0.4f;
                    a = new Color(1f, 0.9f, 0.4f); b = new Color(1f, 0.3f, 0.7f); burst = 40;
                    break;
                case VfxKind.Tech:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                    a = Color.white; b = new Color(0.6f, 1f, 0.6f); burst = 16;
                    break;
                default:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                    a = new Color(1f, 1f, 0.85f); b = new Color(1f, 0.7f, 0.2f); burst = 16;
                    break;
            }
            main.startColor = a;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                         new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });
            return ps;
        }

        public void Play(VfxKind kind, Vector3 pos, float facing = 1f)
        {
            if (_pools == null) return;
            int k = (int)kind;
            var ps = _pools[k][_next[k]];
            _next[k] = (_next[k] + 1) % _pools[k].Length;
            ps.transform.position = pos;
            ps.transform.rotation = Quaternion.Euler(0, facing >= 0 ? 90 : -90, 0);
            ps.Clear(true);
            ps.Play(true);
        }

        public void ClearAll()
        {
            if (_pools == null) return;
            foreach (var arr in _pools)
                foreach (var ps in arr) ps.Clear(true);
        }
    }
}
