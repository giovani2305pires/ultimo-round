using System.Collections.Generic;
using UltimoRound.Combat;
using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>
    /// Corpo humanoide montado com primitivas e articulações. Placeholder declarado (Docs/ASSETS.md) até
    /// existir modelo/rig autorizado. As proporções e roupas variam por lutador; as cores, pela paleta.
    /// </summary>
    public sealed class ProceduralBody
    {
        public Transform Root { get; private set; }
        public Transform Yaw { get; private set; }
        public float HipHeight { get; private set; }

        Transform _pelvis, _torso, _neck;
        Transform _fShoulder, _fElbow, _rShoulder, _rElbow;
        Transform _fHip, _fKnee, _rHip, _rKnee;
        readonly List<Material> _materials = new List<Material>();
        readonly List<Renderer> _renderers = new List<Renderer>();
        public IReadOnlyList<Material> Materials => _materials;
        public Transform FrontHand { get; private set; }
        public Transform RearHand { get; private set; }
        public Transform FrontFoot { get; private set; }
        public Transform Head { get; private set; }

        static Mesh _capsule, _sphere, _cube, _cylinder;

        static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            return mesh;
        }

        static void EnsureMeshes()
        {
            if (_capsule != null) return;
            _capsule = PrimitiveMesh(PrimitiveType.Capsule);
            _sphere = PrimitiveMesh(PrimitiveType.Sphere);
            _cube = PrimitiveMesh(PrimitiveType.Cube);
            _cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        }

        Material NewMat(Material template, Color c, float smooth = 0.3f)
        {
            var m = new Material(template) { name = "FighterMat" };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            _materials.Add(m);
            return m;
        }

        static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        Transform Part(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            _renderers.Add(r);
            return go.transform;
        }

        /// <summary>Membro: cápsula entre a articulação e o próximo ponto (para baixo em -Y).</summary>
        Transform Limb(string name, Transform joint, float length, float thickness, Material mat)
        {
            return Part(name, joint, _capsule, mat, new Vector3(0, -length * 0.5f, 0), new Vector3(thickness, length * 0.5f + thickness * 0.25f, thickness));
        }

        public static ProceduralBody Build(Transform parent, FighterDefinition def, int paletteIndex, Material template)
        {
            EnsureMeshes();
            var b = new ProceduralBody();
            b.BuildInternal(parent, def, def.GetPalette(paletteIndex), template);
            return b;
        }

        void BuildInternal(Transform parent, FighterDefinition def, FighterPalette pal, Material template)
        {
            var shape = def.Body;
            if (shape.Height <= 0) shape = BodyShape.Default;
            var style = def.Guard;

            Root = new GameObject("Body_" + def.Id).transform;
            Root.SetParent(parent, false);
            Yaw = Joint("Yaw", Root, Vector3.zero);
            Yaw.localEulerAngles = new Vector3(0, 28f, 0);
            Yaw.localScale = Vector3.one * shape.Height;

            var skin = NewMat(template, pal.Skin, 0.35f);
            var primary = NewMat(template, pal.Primary, 0.25f);
            var secondary = NewMat(template, pal.Secondary, 0.3f);
            var accent = NewMat(template, pal.Accent, 0.5f);
            var hair = NewMat(template, pal.Hair, 0.15f);
            var dark = NewMat(template, new Color(0.06f, 0.06f, 0.07f), 0.6f);

            // Roupa por estilo: cada lutador tem silhueta e figurino próprios.
            Material torsoMat = skin, thighMat = primary, shinMat = skin, footMat = secondary, gloveMat = secondary, forearmMat = skin;
            bool tankTop = false, longHair = false, ponytail = false, beard = false, headband = false, wraps = false;
            switch (style)
            {
                case GuardStyle.Kickboxer: wraps = true; forearmMat = skin; break;
                case GuardStyle.Brawler: torsoMat = secondary; tankTop = true; shinMat = primary; footMat = dark; gloveMat = dark; beard = true; break;
                case GuardStyle.Agile: torsoMat = primary; thighMat = secondary; shinMat = secondary; footMat = primary; gloveMat = accent; ponytail = true; break;
                case GuardStyle.KickSpecialist: torsoMat = secondary; thighMat = primary; shinMat = primary; footMat = accent; gloveMat = secondary; longHair = true; headband = true; break;
            }

            float bulk = shape.Bulk;
            float limb = shape.Limb;
            float leg = shape.LegLength;
            float thigh = 0.42f * leg, shin = 0.42f * leg, foot = 0.07f;
            HipHeight = (thigh + shin + foot) * shape.Height;
            float torsoLen = 0.52f;
            float shoulderZ = 0.2f * bulk;
            float hipZ = 0.1f * bulk;

            _pelvis = Joint("Pelvis", Yaw, new Vector3(0, thigh + shin + foot, 0));
            Part("Shorts", _pelvis, _sphere, thighMat, new Vector3(0, 0.0f, 0), new Vector3(0.27f * bulk, 0.3f, 0.38f * bulk));
            Part("Belt", _pelvis, _cylinder, accent, new Vector3(0, 0.1f, 0), new Vector3(0.26f * bulk, 0.03f, 0.36f * bulk));

            _torso = Joint("Torso", _pelvis, new Vector3(0, 0.1f, 0));
            Part("Abdomen", _torso, _capsule, torsoMat, new Vector3(0, 0.16f, 0), new Vector3(0.24f * bulk, 0.18f, 0.3f * bulk));
            Part("Chest", _torso, _sphere, torsoMat, new Vector3(0.01f, 0.34f, 0), new Vector3(0.27f * bulk, 0.36f, 0.46f * bulk));
            if (tankTop) Part("TankStrap", _torso, _cube, secondary, new Vector3(0, 0.44f, 0), new Vector3(0.2f * bulk, 0.08f, 0.3f * bulk));

            _neck = Joint("Neck", _torso, new Vector3(0.01f, torsoLen, 0));
            Part("NeckMesh", _neck, _capsule, skin, new Vector3(0, 0.04f, 0), new Vector3(0.1f * Mathf.Max(1f, bulk * 0.9f), 0.06f, 0.1f * Mathf.Max(1f, bulk * 0.9f)));
            Head = Joint("Head", _neck, new Vector3(0.01f, 0.17f, 0));
            Part("Skull", Head, _sphere, skin, Vector3.zero, new Vector3(0.21f, 0.24f, 0.2f));
            Part("EyeF", Head, _sphere, dark, new Vector3(0.095f, 0.025f, -0.045f), Vector3.one * 0.03f);
            Part("EyeR", Head, _sphere, dark, new Vector3(0.095f, 0.025f, 0.045f), Vector3.one * 0.03f);
            Part("Brow", Head, _cube, hair, new Vector3(0.09f, 0.06f, 0), new Vector3(0.02f, 0.02f, 0.13f));
            if (style != GuardStyle.Brawler) Part("Hair", Head, _sphere, hair, new Vector3(-0.015f, 0.06f, 0), new Vector3(0.22f, 0.16f, 0.21f));
            if (beard) Part("Beard", Head, _sphere, hair, new Vector3(0.05f, -0.08f, 0), new Vector3(0.13f, 0.1f, 0.16f));
            if (ponytail) Part("Ponytail", Head, _capsule, hair, new Vector3(-0.13f, 0.0f, 0), new Vector3(0.07f, 0.12f, 0.07f), new Vector3(0, 0, -30));
            if (longHair) Part("LongHair", Head, _cube, hair, new Vector3(-0.08f, -0.06f, 0), new Vector3(0.08f, 0.24f, 0.2f));
            if (headband) Part("Headband", Head, _cube, accent, new Vector3(0, 0.05f, 0), new Vector3(0.215f, 0.035f, 0.205f));

            // Braços: "Front" fica do lado da câmera (Z negativo local).
            float upper = 0.29f, fore = 0.27f, armT = 0.095f * limb;
            _fShoulder = Joint("FShoulder", _torso, new Vector3(0.02f, torsoLen - 0.06f, -shoulderZ));
            Part("FDelt", _fShoulder, _sphere, torsoMat == skin ? skin : torsoMat, Vector3.zero, Vector3.one * 0.12f * limb);
            Limb("FUpperArm", _fShoulder, upper, armT, skin);
            _fElbow = Joint("FElbow", _fShoulder, new Vector3(0, -upper, 0));
            Limb("FForearm", _fElbow, fore, armT * 0.92f, wraps ? accent : forearmMat);
            FrontHand = Joint("FHand", _fElbow, new Vector3(0, -fore, 0));
            Part("FGlove", FrontHand, _sphere, gloveMat, Vector3.zero, new Vector3(0.13f, 0.12f, 0.12f) * Mathf.Lerp(1f, limb, 0.5f));

            _rShoulder = Joint("RShoulder", _torso, new Vector3(0.02f, torsoLen - 0.06f, shoulderZ));
            Part("RDelt", _rShoulder, _sphere, torsoMat == skin ? skin : torsoMat, Vector3.zero, Vector3.one * 0.12f * limb);
            Limb("RUpperArm", _rShoulder, upper, armT, skin);
            _rElbow = Joint("RElbow", _rShoulder, new Vector3(0, -upper, 0));
            Limb("RForearm", _rElbow, fore, armT * 0.92f, wraps ? accent : forearmMat);
            RearHand = Joint("RHand", _rElbow, new Vector3(0, -fore, 0));
            Part("RGlove", RearHand, _sphere, gloveMat, Vector3.zero, new Vector3(0.13f, 0.12f, 0.12f) * Mathf.Lerp(1f, limb, 0.5f));

            // Pernas
            float legT = 0.13f * limb;
            _fHip = Joint("FHip", _pelvis, new Vector3(0, -0.02f, -hipZ));
            Limb("FThigh", _fHip, thigh, legT, thighMat);
            _fKnee = Joint("FKnee", _fHip, new Vector3(0, -thigh, 0));
            Limb("FShin", _fKnee, shin, legT * 0.85f, shinMat);
            FrontFoot = Joint("FAnkle", _fKnee, new Vector3(0, -shin, 0));
            Part("FFoot", FrontFoot, _cube, footMat, new Vector3(0.05f, -0.03f, 0), new Vector3(0.22f, 0.07f, 0.1f));

            _rHip = Joint("RHip", _pelvis, new Vector3(0, -0.02f, hipZ));
            Limb("RThigh", _rHip, thigh, legT, thighMat);
            _rKnee = Joint("RKnee", _rHip, new Vector3(0, -thigh, 0));
            Limb("RShin", _rKnee, shin, legT * 0.85f, shinMat);
            var rAnkle = Joint("RAnkle", _rKnee, new Vector3(0, -shin, 0));
            Part("RFoot", rAnkle, _cube, footMat, new Vector3(0.05f, -0.03f, 0), new Vector3(0.22f, 0.07f, 0.1f));

            Apply(PoseLibrary.Guard(style));
        }

        public void Apply(in FighterPose p)
        {
            float baseHip = HipHeight / Mathf.Max(0.01f, Yaw.localScale.y);
            _pelvis.localPosition = new Vector3(p.HipX, baseHip + p.HipY, 0);
            _pelvis.localRotation = Quaternion.Euler(0, 0, p.BodyRoll);
            _torso.localRotation = Quaternion.Euler(0, p.Twist, -p.Lean);
            _neck.localRotation = Quaternion.Euler(0, 0, -p.Head);
            // Ombro: Z gira o braço para frente/trás; X abre o braço para o lado (para longe do corpo).
            _fShoulder.localRotation = Quaternion.Euler(p.FArmOut, 0, p.FShoulder);
            _rShoulder.localRotation = Quaternion.Euler(-p.RArmOut, 0, p.RShoulder);
            _fElbow.localRotation = Quaternion.Euler(0, 0, p.FElbow);
            _rElbow.localRotation = Quaternion.Euler(0, 0, p.RElbow);
            _fHip.localRotation = Quaternion.Euler(0, 0, p.FHip);
            _rHip.localRotation = Quaternion.Euler(0, 0, p.RHip);
            _fKnee.localRotation = Quaternion.Euler(0, 0, -p.FKnee);
            _rKnee.localRotation = Quaternion.Euler(0, 0, -p.RKnee);
        }

        public void SetEmission(Color c)
        {
            for (int i = 0; i < _materials.Count; i++) _materials[i].SetColor("_EmissionColor", c);
        }

        public void SetShadows(bool on)
        {
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var r in _renderers) r.shadowCastingMode = mode;
        }

        public void Destroy()
        {
            foreach (var m in _materials)
            {
                if (Application.isPlaying) Object.Destroy(m);
                else Object.DestroyImmediate(m);
            }
            _materials.Clear();
            if (Root != null)
            {
                if (Application.isPlaying) Object.Destroy(Root.gameObject);
                else Object.DestroyImmediate(Root.gameObject);
            }
        }
    }
}
