using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UltimoRound.EditorTools
{
    /// <summary>Gera o executável Windows x64 e o ZIP de distribuição com todos os arquivos necessários.</summary>
    public static class BuildTool
    {
        public const string Version = "1.0.0";
        public const string BuildDir = "Builds/Windows";
        public const string ExeName = "UltimoRound.exe";
        public const string DistDir = "Dist";

        [MenuItem("Último Round/Build/Windows x64 (+ ZIP)")]
        public static void BuildWindowsMenu() => BuildWindows();

        public static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Último Round";
            PlayerSettings.companyName = "Ultimo Round Dev";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true; // a luta pausa sozinha ao perder o foco (OnApplicationFocus)
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SplashScreen.show = false;
        }

        public static string BuildWindows()
        {
            ConfigurePlayer();
            Directory.CreateDirectory(BuildDir);
            var scenes = new[] { SceneBuilder.MenuScenePath, SceneBuilder.FightScenePath };
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(BuildDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string msg = $"Build {s.result}: {s.totalSize / (1024f * 1024f):F1} MB, {s.totalTime.TotalSeconds:F0}s, erros={s.totalErrors}, avisos={s.totalWarnings}";
            Debug.Log("[Build] " + msg);
            File.WriteAllText("Logs/UltimoRound_BuildResult.txt", msg + "\n");
            if (s.result != BuildResult.Succeeded) return msg;
            msg += "\nZIP: " + Zip();
            File.WriteAllText("Logs/UltimoRound_BuildResult.txt", msg + "\n");
            return msg;
        }

        /// <summary>Compacta a pasta do build (sem a pasta de símbolos de depuração do Burst).</summary>
        public static string Zip()
        {
            Directory.CreateDirectory(DistDir);
            string folder = Path.Combine(DistDir, "UltimoRound_Windows_x64");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            CopyDir(BuildDir, folder);
            File.WriteAllText(Path.Combine(folder, "LEIA-ME.txt"),
                "Último Round — Torneio dos Rivais (versão " + Version + ")\r\n" +
                "Giovani Nogueira Pires — Desenvolvimento de Jogos Digitais\r\n\r\n" +
                "Para jogar: abra UltimoRound.exe. Não requer internet, Unity ou outros programas.\r\n" +
                "Controles: teclado (A/D mover, W pular, S agachar, J soco, K chute, L forte, I defesa, U especial, O agarrão, P assinatura, Esc pausa)\r\n" +
                "ou gamepad (analógico/direcional, A pular, X soco, Y chute, B forte, RT defesa, RB especial, LB agarrão, LT assinatura, Start pausa).\r\n" +
                "Rede local: um jogador escolhe Rede Local > Hospedar partida; o outro, na mesma rede, escolhe Entrar em partida. Permita o acesso quando o Windows perguntar.\r\n" +
                "Configurações salvas em %USERPROFILE%\\AppData\\LocalLow\\Ultimo Round Dev\\Último Round\\settings.json\r\n");
            string zip = Path.Combine(DistDir, $"UltimoRound_Windows_x64_v{Version}.zip");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, false);
            return Path.GetFullPath(zip);
        }

        [MenuItem("Último Round/Build/Versão de entrega (build + ZIP + instalador)")]
        public static void BuildReleaseMenu() => BuildRelease();

        /// <summary>Build, ZIP e instalador. Também roda sem o editor aberto:
        /// Unity.exe -batchmode -projectPath . -executeMethod UltimoRound.EditorTools.BuildTool.BuildReleaseBatch</summary>
        public static string BuildRelease()
        {
            string msg = BuildWindows();
            if (!msg.Contains("Succeeded")) return msg;
            return msg + "\n" + BuildInstaller();
        }

        public static void BuildReleaseBatch()
        {
            string msg = BuildRelease();
            EditorApplication.Exit(msg.Contains("Succeeded") && !msg.Contains("ERRO") ? 0 : 1);
        }

        [MenuItem("Último Round/Build/Instalador Windows (usa a última build)")]
        public static void BuildInstallerMenu() => BuildInstaller();

        /// <summary>Gera o instalador com o Inno Setup a partir da pasta copiada para Dist por Zip().</summary>
        public static string BuildInstaller()
        {
            string msg = RunInnoSetup();
            Debug.Log("[Instalador] " + msg);
            File.WriteAllText("Logs/UltimoRound_InstallerResult.txt", msg + "\n");
            return msg;
        }

        static string RunInnoSetup()
        {
            string compiler = FindInnoCompiler();
            if (compiler == null) return "ERRO: Inno Setup 6 não encontrado. Instale com: winget install --id JRSoftware.InnoSetup -e --scope user";
            if (!File.Exists(Path.Combine(DistDir, "UltimoRound_Windows_x64", ExeName))) return "ERRO: gere a build antes (Build > Windows x64).";
            var info = new System.Diagnostics.ProcessStartInfo(compiler, $"/Qp /DAppVersion={Version} \"{Path.GetFullPath("Installer/UltimoRound.iss")}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using (var process = System.Diagnostics.Process.Start(info))
            {
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) return $"ERRO: Inno Setup terminou com código {process.ExitCode}. {errors}";
            }
            string setup = Path.GetFullPath(Path.Combine(DistDir, $"UltimoRound_Setup_v{Version}.exe"));
            return $"Instalador: {setup} ({new FileInfo(setup).Length / (1024f * 1024f):F1} MB)";
        }

        static string FindInnoCompiler()
        {
            foreach (var root in new[]
            {
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "/Programs",
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
            })
            {
                string path = Path.Combine(root, "Inno Setup 6", "ISCC.exe");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src))
            {
                string name = Path.GetFileName(d);
                if (name.EndsWith("_DoNotShip") || name.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame")) continue;
                CopyDir(d, Path.Combine(dst, name));
            }
        }
    }
}
