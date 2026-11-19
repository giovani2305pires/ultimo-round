using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UltimoRound.EditorTools
{
    /// <summary>Utilitários de desenvolvimento usados também via MCP.</summary>
    public static class DevTools
    {
        /// <summary>
        /// Refresh síncrono e reimportação de scripts que existem no disco mas não entraram em nenhum assembly
        /// (acontece se o arquivo é criado durante um domain reload).
        /// </summary>
        [MenuItem("Último Round/Dev/Sincronizar scripts")]
        public static string SyncScripts()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var asmFiles = new HashSet<string>();
            foreach (var a in UnityEditor.Compilation.CompilationPipeline.GetAssemblies())
                if (a.name.StartsWith("UltimoRound"))
                    foreach (var f in a.sourceFiles) asmFiles.Add(f.Replace('\\', '/'));
            var missing = new List<string>();
            foreach (var dir in new[] { "Assets/_Game/Scripts", "Assets/_Game/Editor", "Assets/_Game/Tests" })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    var p = f.Replace('\\', '/');
                    if (asmFiles.Contains(p)) continue;
                    missing.Add(p);
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
            }
            string msg = missing.Count == 0 ? "ok" : "reimportados: " + string.Join(", ", missing);
            Debug.Log("[Dev] " + msg);
            return msg;
        }

        /// <summary>
        /// Captura a câmera principal com as interfaces (canvases overlay são renderizados temporariamente pela câmera).
        /// Útil quando o editor está sem foco e não gera quadros de Game View.
        /// </summary>
        public static string CaptureGame(string path, int width = 1600, int height = 900)
        {
            var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam == null) return "sem câmera";
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            var changed = new List<Canvas>();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            foreach (var c in canvases)
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f + 0.01f * (100 - c.sortingOrder);
                changed.Add(c);
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prevTarget;
            foreach (var c in changed)
            {
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.worldCamera = null;
            }
            ShaderUtil.allowAsyncCompilation = prevAsync;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            return path;
        }
    }
}
