using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UltimoRound.EditorTools
{
    /// <summary>
    /// Executa os testes pelo editor aberto (sem iniciar outra instância em batch) e grava um resumo em
    /// Logs/UltimoRound_TestResults.txt para consulta por ferramentas externas.
    /// </summary>
    public static class TestRunnerBridge
    {
        public const string ResultPath = "Logs/UltimoRound_TestResults.txt";

        [MenuItem("Último Round/Testes/Rodar EditMode")]
        public static void RunEditMode() => Run(TestMode.EditMode);

        [MenuItem("Último Round/Testes/Rodar PlayMode")]
        public static void RunPlayMode() => Run(TestMode.PlayMode);

        public static void Run(TestMode mode)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ResultPath, "RUNNING " + mode + "\n");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(api));
            // EditMode síncrono: não depende de atualizações do editor (útil com o editor sem foco/MCP).
            api.Execute(new ExecutionSettings(new Filter { testMode = mode }) { runSynchronously = mode == TestMode.EditMode });
        }

        sealed class Callbacks : ICallbacks
        {
            readonly TestRunnerApi _api;
            readonly StringBuilder _failures = new StringBuilder();
            public Callbacks(TestRunnerApi api) { _api = api; }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"DONE status={result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount} duration={result.Duration:F2}s");
                sb.Append(_failures);
                File.WriteAllText(ResultPath, sb.ToString());
                Debug.Log("[Testes] " + sb);
                _api.UnregisterCallbacks(this);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                if (result.TestStatus == TestStatus.Failed)
                    _failures.AppendLine($"FAIL {result.FullName}\n  {result.Message?.Trim()}\n");
                else
                    _failures.AppendLine($"{result.TestStatus.ToString().ToUpperInvariant()} {result.Name}");
            }
        }
    }
}
