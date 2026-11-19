using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>
    /// Monta o ringue e as arquibancadas de uma base militar à noite a partir de primitivas (os prédios vêm do AssetIntegrator). Executado pela ferramenta de editor
    /// (o resultado fica salvo na cena) — sem colisores: a colisão de gameplay é da simulação.
    /// Orçamento de luz: 1 spot com sombras + 1 spot sem sombras + direcional fraca + 2 pontuais.
    /// </summary>
    public static class ArenaBuilder
    {
        public const float RingHalfX = 4.75f;
        public const float RingHalfZ = 2.85f;
        public static readonly float[] RopeHeights = { 0.45f, 0.85f, 1.25f };

        /// <summary>Provedor de materiais: (nome, cor, emissão, transparente) → material persistente.</summary>
        public delegate Material MaterialProvider(string name, Color color, float emission, bool transparent);

        static Mesh _cube, _cyl, _sphere, _capsule;

        static Mesh Prim(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(go);
            return m;
        }

        static GameObject Block(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        static TMPro.TextMeshPro Text3D(Transform parent, string name, string text, Vector3 pos, Vector3 euler, float size, Color color, TMPro.TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var t = go.AddComponent<TMPro.TextMeshPro>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(20, 4);
            return t;
        }

        public static GameObject Build(Transform parent, MaterialProvider mats, Material instancedTemplate, TMPro.TMP_FontAsset font)
        {
            _cube = Prim(PrimitiveType.Cube);
            _cyl = Prim(PrimitiveType.Cylinder);
            _sphere = Prim(PrimitiveType.Sphere);
            _capsule = Prim(PrimitiveType.Capsule);

            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);

            var canvas = mats("Arena_Lona", new Color(0.58f, 0.62f, 0.7f), 0f, false);
            var canvasLogo = mats("Arena_LonaLogo", new Color(0.12f, 0.18f, 0.38f), 0f, false);
            var apron = mats("Arena_Saia", new Color(0.09f, 0.11f, 0.2f), 0f, false);
            var trim = mats("Arena_Friso", new Color(0.2f, 0.5f, 1f), 2.2f, false);
            var floor = mats("Arena_Piso", new Color(0.07f, 0.07f, 0.08f), 0f, false);
            var metal = mats("Arena_Metal", new Color(0.55f, 0.57f, 0.6f), 0f, false);
            var padRed = mats("Arena_CornerRed", new Color(0.75f, 0.12f, 0.14f), 0f, false);
            var padBlue = mats("Arena_CornerBlue", new Color(0.12f, 0.28f, 0.8f), 0f, false);
            var padWhite = mats("Arena_CornerWhite", new Color(0.85f, 0.85f, 0.85f), 0f, false);
            var ropeRed = mats("Arena_RopeRed", new Color(0.85f, 0.1f, 0.12f), 0.15f, false);
            var ropeWhite = mats("Arena_RopeWhite", new Color(0.92f, 0.92f, 0.95f), 0.15f, false);
            var ropeBlue = mats("Arena_RopeBlue", new Color(0.15f, 0.3f, 0.9f), 0.15f, false);
            var bleacher = mats("Arena_Arquibancada", new Color(0.12f, 0.12f, 0.14f), 0f, false);
            var screenMat = mats("Arena_Telao", new Color(0.02f, 0.03f, 0.06f), 0f, false);
            var screenFrame = mats("Arena_TelaoMoldura", new Color(1f, 0.55f, 0.1f), 1.6f, false);
            var truss = mats("Arena_Trelica", new Color(0.18f, 0.18f, 0.2f), 0f, false);
            var lamp = mats("Arena_Refletor", new Color(1f, 0.92f, 0.75f), 4f, false);
            var signA = mats("Arena_PlacaA", new Color(0.9f, 0.1f, 0.5f), 1.4f, false);
            var signB = mats("Arena_PlacaB", new Color(0.1f, 0.8f, 0.9f), 1.4f, false);

            // Ringue -------------------------------------------------------------
            var ring = new GameObject("Ringue").transform;
            ring.SetParent(root, false);
            Block(ring, "Lona", _cube, canvas, new Vector3(0, -0.06f, 0), new Vector3(RingHalfX * 2 + 0.5f, 0.12f, RingHalfZ * 2 + 0.5f));
            Block(ring, "LogoLona", _cyl, canvasLogo, new Vector3(0, 0.002f, 0.4f), new Vector3(2.6f, 0.002f, 2.6f), default, false);
            Text3D(ring, "LogoTexto", "ÚLTIMO ROUND", new Vector3(0, 0.01f, 0.4f), new Vector3(90, 0, 0), 3.2f, new Color(0.85f, 0.9f, 1f, 0.85f), font);
            Block(ring, "Saia", _cube, apron, new Vector3(0, -0.78f, 0), new Vector3(RingHalfX * 2 + 0.7f, 1.32f, RingHalfZ * 2 + 0.7f));
            Block(ring, "FrisoFrente", _cube, trim, new Vector3(0, -0.16f, -RingHalfZ - 0.36f), new Vector3(RingHalfX * 2 + 0.72f, 0.05f, 0.02f), default, false);
            Text3D(ring, "SaiaTexto", "TORNEIO DOS RIVAIS", new Vector3(0, -0.72f, -RingHalfZ - 0.37f), Vector3.zero, 3.2f, new Color(0.9f, 0.92f, 1f), font);

            var posts = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
            foreach (var p in posts)
            {
                var pos = new Vector3(p.x * RingHalfX, 0.72f, p.y * RingHalfZ);
                Block(ring, "Poste", _cyl, metal, pos, new Vector3(0.12f, 0.78f, 0.12f));
                var pad = p.x < 0 && p.y > 0 ? padRed : p.x > 0 && p.y > 0 ? padBlue : padWhite;
                if (p.y > 0) Block(ring, "Protetor", _cube, pad, pos + new Vector3(-p.x * 0.08f, 0.13f, -p.y * 0.08f), new Vector3(0.2f, 1.0f, 0.2f));
            }

            // Cordas longas só no fundo: na frente não há nada entre a câmera e a luta.
            var ropeMats = new[] { ropeBlue, ropeWhite, ropeRed };
            for (int i = 0; i < RopeHeights.Length; i++)
            {
                float h = RopeHeights[i];
                Block(ring, "CordaFundo" + i, _cyl, ropeMats[i], new Vector3(0, h, RingHalfZ), new Vector3(0.045f, RingHalfX, 0.045f), new Vector3(0, 0, 90));
            }

            // Cordas laterais animáveis (rebote).
            var ropesComp = ring.gameObject.AddComponent<RingRopes>();
            ropesComp.SideX = RingHalfX;
            ropesComp.HalfDepth = RingHalfZ;
            ropesComp.Heights = (float[])RopeHeights.Clone();
            ropesComp.Left = new LineRenderer[RopeHeights.Length];
            ropesComp.Right = new LineRenderer[RopeHeights.Length];
            for (int i = 0; i < RopeHeights.Length; i++)
            {
                ropesComp.Left[i] = Line(ring, "CordaEsq" + i, ropeMats[i], -RingHalfX, RopeHeights[i]);
                ropesComp.Right[i] = Line(ring, "CordaDir" + i, ropeMats[i], RingHalfX, RopeHeights[i]);
            }

            // Escada decorativa no canto do fundo.
            for (int s = 0; s < 3; s++)
                Block(ring, "Degrau" + s, _cube, metal, new Vector3(RingHalfX + 0.75f + s * 0.38f, -0.25f - s * 0.38f, RingHalfZ + 0.3f), new Vector3(0.4f, 0.12f, 0.9f));

            // Arquibancadas e estrutura -------------------------------------------
            var hall = new GameObject("Galpao").transform;
            hall.SetParent(root, false);
            Block(hall, "Piso", _cube, floor, new Vector3(0, -1.5f, 8f), new Vector3(70, 0.1f, 46), default, false);

            var seats = new List<Vector3>();
            for (int row = 0; row < 6; row++)
            {
                float z = 5.2f + row * 1.25f;
                float y = -1.45f + row * 0.55f;
                Block(hall, "Arquibancada" + row, _cube, bleacher, new Vector3(0, y - 0.05f, z), new Vector3(30f, 0.55f, 1.2f), default, false);
                for (int c = 0; c < 26; c++)
                {
                    float x = -14.4f + c * 1.15f + (row % 2) * 0.5f;
                    if ((c * 7 + row * 3) % 9 == 0) continue; // lugares vazios
                    seats.Add(new Vector3(x, y + 0.22f, z));
                }
            }
            // Laterais (perto da câmera não há público para não cobrir a luta).
            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 3; row++)
                {
                    float x = side * (7.4f + row * 1.2f);
                    float y = -1.45f + row * 0.55f;
                    Block(hall, "ArquibancadaLateral", _cube, bleacher, new Vector3(x, y - 0.05f, 2.5f), new Vector3(1.1f, 0.55f, 8f), default, false);
                    for (int c = 0; c < 6; c++) seats.Add(new Vector3(x, y + 0.22f, -0.5f + c * 1.2f));
                }

            var crowdGo = new GameObject("Publico");
            crowdGo.transform.SetParent(hall, false);
            var crowd = crowdGo.AddComponent<CrowdRenderer>();
            crowd.BodyMesh = _capsule;
            crowd.HeadMesh = _sphere;
            crowd.Seats = seats;
            var crowdColors = new[] { new Color(0.55f, 0.1f, 0.12f), new Color(0.12f, 0.2f, 0.45f), new Color(0.25f, 0.25f, 0.27f), new Color(0.5f, 0.45f, 0.15f) };
            crowd.BodyMaterials = new Material[crowdColors.Length];
            for (int i = 0; i < crowdColors.Length; i++)
                crowd.BodyMaterials[i] = mats("Publico_" + i, crowdColors[i], 0f, false);
            crowd.HeadMaterial = mats("Publico_Pele", new Color(0.55f, 0.4f, 0.32f), 0f, false);
            foreach (var m in crowd.BodyMaterials) if (m != null) m.enableInstancing = true;
            if (crowd.HeadMaterial != null) crowd.HeadMaterial.enableInstancing = true;

            // Telão e placas.
            var scr = new GameObject("Telao").transform;
            scr.SetParent(hall, false);
            scr.localPosition = new Vector3(0, 6.6f, 14f);
            Block(scr, "Moldura", _cube, screenFrame, Vector3.zero, new Vector3(9.4f, 4.0f, 0.2f), default, false);
            Block(scr, "Tela", _cube, screenMat, new Vector3(0, 0, -0.12f), new Vector3(9f, 3.6f, 0.1f), default, false);
            var screen = scr.gameObject.AddComponent<ArenaScreen>();
            screen.Title = Text3D(scr, "TelaoTitulo", "ÚLTIMO ROUND", new Vector3(0, 0.6f, -0.2f), Vector3.zero, 9f, new Color(1f, 0.85f, 0.4f), font);
            screen.Subtitle = Text3D(scr, "TelaoSub", "TORNEIO DOS RIVAIS", new Vector3(0, -0.75f, -0.2f), Vector3.zero, 5f, new Color(0.7f, 0.9f, 1f), font);

            Block(hall, "PlacaEsq", _cube, signA, new Vector3(-12f, 3.6f, 13f), new Vector3(5f, 1.1f, 0.15f), default, false);
            Text3D(hall, "PlacaEsqTexto", "RIVAIS", new Vector3(-12f, 3.6f, 12.9f), Vector3.zero, 6f, Color.white, font);
            Block(hall, "PlacaDir", _cube, signB, new Vector3(12f, 3.6f, 13f), new Vector3(5f, 1.1f, 0.15f), default, false);
            Text3D(hall, "PlacaDirTexto", "NOCAUTE", new Vector3(12f, 3.6f, 12.9f), Vector3.zero, 6f, Color.white, font);

            // Treliça e refletores.
            Block(hall, "TrelicaFrente", _cube, truss, new Vector3(0, 7.2f, -2.5f), new Vector3(14f, 0.3f, 0.3f), default, false);
            Block(hall, "TrelicaFundo", _cube, truss, new Vector3(0, 7.2f, 3.5f), new Vector3(14f, 0.3f, 0.3f), default, false);
            for (int i = -2; i <= 2; i++)
            {
                Block(hall, "Refletor", _cyl, lamp, new Vector3(i * 2.6f, 6.85f, -2.5f), new Vector3(0.45f, 0.2f, 0.45f), new Vector3(-35, 0, 0), false);
                Block(hall, "Refletor", _cyl, lamp, new Vector3(i * 2.6f, 6.85f, 3.5f), new Vector3(0.45f, 0.2f, 0.45f), new Vector3(35, 0, 0), false);
            }

            // Luzes ----------------------------------------------------------------
            var lights = new GameObject("Luzes").transform;
            lights.SetParent(root, false);
            var key = NewLight(lights, "SpotPrincipal", LightType.Spot, new Vector3(0, 8.5f, -4.5f), new Color(1f, 0.95f, 0.86f), 105f, 18f);
            key.transform.LookAt(new Vector3(0, 0, 0.3f));
            key.spotAngle = 64f;
            key.innerSpotAngle = 40f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;
            var fill = NewLight(lights, "SpotPreenchimento", LightType.Spot, new Vector3(0, 7.5f, 4f), new Color(0.75f, 0.82f, 1f), 70f, 18f);
            fill.transform.LookAt(new Vector3(0, 0, -0.5f));
            fill.spotAngle = 75f;
            fill.shadows = LightShadows.None;
            var dir = NewLight(lights, "Ambiente", LightType.Directional, new Vector3(0, 10, 0), new Color(0.5f, 0.58f, 0.85f), 0.8f, 0f);
            dir.transform.rotation = Quaternion.Euler(40, -25, 0);
            dir.shadows = LightShadows.None;
            var p1 = NewLight(lights, "TelaoBrilho", LightType.Point, new Vector3(0, 5f, 12.5f), new Color(1f, 0.6f, 0.25f), 12f, 8f);
            p1.shadows = LightShadows.None;
            var p2 = NewLight(lights, "PublicoBrilho", LightType.Point, new Vector3(0, 4f, 7.5f), new Color(0.7f, 0.68f, 1f), 22f, 10f);
            p2.shadows = LightShadows.None;

            return root.gameObject;
        }

        static Light NewLight(Transform parent, string name, LightType type, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            if (range > 0) l.range = range;
            return l;
        }

        static LineRenderer Line(Transform parent, string name, Material mat, float x, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 7;
            lr.widthMultiplier = 0.05f;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.numCapVertices = 2;
            for (int p = 0; p < 7; p++)
                lr.SetPosition(p, new Vector3(x, h, Mathf.Lerp(-RingHalfZ, RingHalfZ, p / 6f)));
            return lr;
        }

        public static void ApplyEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.17f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.1f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.035f, 0.04f, 0.065f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 85f;
            RenderSettings.skybox = null;
        }
    }
}
