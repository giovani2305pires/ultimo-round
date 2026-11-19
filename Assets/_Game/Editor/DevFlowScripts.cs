using System.IO;
using System.Linq;
using UltimoRound.Audio;
using UltimoRound.Core;
using UltimoRound.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using D = UltimoRound.EditorTools.DevFlowDriver;

namespace UltimoRound.EditorTools
{
    /// <summary>
    /// Roteiros de verificação em Play Mode (executados por etapas via menu/MCP). Resultado em Logs/UltimoRound_Flow.txt
    /// e capturas em Logs/Capturas. Requer Play Mode iniciado na cena MainMenu.
    /// </summary>
    public static class DevFlowScripts
    {
        const string LogPath = "Logs/UltimoRound_Flow.txt";
        const string ShotDir = "Logs/Capturas";

        static void Begin()
        {
            Directory.CreateDirectory(ShotDir);
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Inicie o Play Mode na cena MainMenu.");
        }

        static void Flush(string step)
        {
            File.AppendAllText(LogPath, $"== {step} ==\n{D.Log}\n");
            D.Log.Clear();
        }

        static string Census()
        {
            int services = Object.FindObjectsByType<GameServices>(FindObjectsInactive.Include).Length;
            int audio = Object.FindObjectsByType<AudioService>(FindObjectsInactive.Include).Length;
            int views = Object.FindObjectsByType<FighterView>(FindObjectsInactive.Include).Length;
            int canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Count(c => c.isRootCanvas);
            int sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
            int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include).Length;
            int es = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include).Length;
            return $"GameServices={services} AudioService={audio} FighterViews={views} Canvas={canvases} AudioSources={sources} AudioListeners={listeners} EventSystems={es}";
        }

        [MenuItem("Último Round/Dev/Diagnóstico de entrada")]
        public static void InputDiag()
        {
            Begin();
            D.Setup(ShotDir);
            var kb = D.Kb;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Keyboard.current={Keyboard.current?.name} virtual={kb.name} added={kb.added} enabled={kb.enabled} canRunInBackground={kb.canRunInBackground}");
            sb.AppendLine($"updateMode={InputSystem.settings.updateMode} bg={InputSystem.settings.backgroundBehavior} editorBeh={InputSystem.settings.editorInputBehaviorInPlayMode} focus={Application.isFocused}");
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Enter));
            int f0 = Time.frameCount;
            D.Step(1);
            sb.AppendLine($"após step: frame {f0}->{Time.frameCount} enter.isPressed={kb.enterKey.isPressed} wasPressed={kb.enterKey.wasPressedThisFrame}");
            InputSystem.Update();
            sb.AppendLine($"após InputSystem.Update: enter.isPressed={kb.enterKey.isPressed} wasPressed={kb.enterKey.wasPressedThisFrame}");
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.Update();
            D.Step(1);
            sb.AppendLine($"solto: isPressed={kb.enterKey.isPressed}");
            InputSystem.Update();
            InputState.Change(kb, new KeyboardState(Key.Enter));
            sb.AppendLine($"InputState.Change: isPressed={kb.enterKey.isPressed} wasPressed={kb.enterKey.wasPressedThisFrame}");
            EditorApplication.Step();
            sb.AppendLine($"após Step: isPressed={kb.enterKey.isPressed} wasPressed={kb.enterKey.wasPressedThisFrame}");
            InputState.Change(kb, new KeyboardState());
            var pad = D.Pad;
            InputState.Change(pad, new GamepadState().WithButton(GamepadButton.South));
            sb.AppendLine($"pad south={pad.buttonSouth.isPressed} was={pad.buttonSouth.wasPressedThisFrame}");
            InputState.Change(pad, new GamepadState());
            sb.AppendLine($"devices: {string.Join(", ", System.Linq.Enumerable.Select(InputSystem.devices, d => d.name + (d.enabled ? "" : "(off)")))}");
            File.WriteAllText(LogPath, sb.ToString());
        }

        [MenuItem("Último Round/Dev/Fluxo 1 - Menu até seleção (Versus)")]
        public static void Flow1()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            D.Setup(ShotDir);
            D.Step(20);
            D.Shot("f01_abertura");
            D.Key(Key.Enter);
            D.Step(8);
            D.Shot("f02_menu");
            D.Key(Key.Enter); // Versus Local
            D.Step(4);
            D.Key(Key.Enter); // J1: teclado
            D.Step(2);
            D.PadButton(GamepadButton.South); // J2: gamepad
            D.Step(2);
            D.Shot("f03_associacao");
            var gs = D.Services;
            D.Note($"J1={gs.Input.Slots[0].Device?.name} J2={gs.Input.Slots[1].Device?.name} modo={gs.Session.Mode}");
            D.Step(40);
            D.Shot("f04_selecao");
            D.Note(Census());
            Flush("Fluxo 1");
        }

        [MenuItem("Último Round/Dev/Fluxo 2 - Confirmar seleção e carregar luta")]
        public static void Flow2()
        {
            Begin();
            D.Key(Key.Enter);                       // J1 confirma Giovani
            D.Step(2);
            D.PadButton(GamepadButton.DpadRight);   // J2 vai para Brutus
            D.Step(2);
            D.Shot("f05_selecao_j2");
            D.PadButton(GamepadButton.South);       // J2 confirma
            D.Step(8);
            D.Shot("f06_prontos");
            D.Step(80);
            D.Note("cena após seleção=" + D.Scene);
            D.Step(20);
            D.Shot("f07_versus");
            var gs = D.Services;
            D.Note($"lutadores={gs.Session.Fighters[0]?.Id} x {gs.Session.Fighters[1]?.Id} cores={gs.Session.Palettes[0]}/{gs.Session.Palettes[1]}");
            D.Note(Census());
            Flush("Fluxo 2");
        }

        [MenuItem("Último Round/Dev/Fluxo 3 - Luta com entradas e pausa")]
        public static void Flow3()
        {
            Begin();
            D.Key(Key.Enter);   // pula VS
            D.Step(5);
            D.Key(Key.Enter);   // pula introdução
            var f = D.Fight;
            WaitTicks(f, 0, () => f.Match.Phase == Combat.MatchPhase.Fighting);
            D.Note($"fase={f.Match.Phase} round={f.Match.RoundNumberShown}");
            D.Shot("f08_luta_inicio");
            // J1 anda para frente e ataca; J2 (gamepad) defende segurando RT.
            int t0 = f.Match.Sim.Tick;
            int guard = 0;
            while (f.Match.Sim.Tick - t0 < 45 && guard++ < 3000)
            {
                D.SetKeys(Key.D);
                D.SetPad(new GamepadState { rightTrigger = 1f });
                D.Step(1);
            }
            D.SetKeys();
            D.Step(1);
            D.Keys(Key.J);
            WaitTicks(f, 8, null);
            D.Keys(Key.K);
            WaitTicks(f, 10, null);
            D.Shot("f09_luta_ataques");
            WaitTicks(f, 30, null);
            var sim = f.Match.Sim;
            D.Note($"vida={sim[0].Health}/{sim[1].Health} energia={sim[0].Energy}/{sim[1].Energy} estados={sim[0].State}/{sim[1].State} x={sim[0].Pos.x:F2}/{sim[1].Pos.x:F2}");
            int tickBefore = sim.Tick;
            D.Key(Key.Escape);
            D.Step(30);
            D.Shot("f10_pausa");
            D.Note($"pausa: tick antes={tickBefore} depois={sim.Tick} (deve ficar igual)");
            D.Key(Key.Escape);  // retoma
            D.Step(5);
            D.Note($"retomado: tick={sim.Tick}");
            D.SetPad(new GamepadState());
            D.Step(1);
            Flush("Fluxo 3");
        }

        /// <summary>Avança quadros até a simulação andar N ticks (e/ou a condição ser satisfeita).</summary>
        static void WaitTicks(FightSceneController f, int ticks, System.Func<bool> until)
        {
            int t0 = f.Match.Sim.Tick;
            int guard = 0;
            while (guard++ < 5000)
            {
                bool ticksOk = f.Match.Sim.Tick - t0 >= ticks;
                bool condOk = until == null || until();
                if (ticksOk && condOk) break;
                D.Step(1);
            }
        }

        [MenuItem("Último Round/Dev/Fluxo 4 - Fim de luta, resultado e revanche")]
        public static void Flow4()
        {
            Begin();
            var f = D.Fight;
            var m = f.Match;
            for (int round = 0; round < 3 && m.Phase != Combat.MatchPhase.MatchOver; round++)
            {
                WaitTicks(f, 0, () => m.Phase == Combat.MatchPhase.Fighting || m.Phase == Combat.MatchPhase.MatchOver);
                if (m.Phase == Combat.MatchPhase.MatchOver) break;
                m.Sim[1].Health = 1;
                // J1 acerta um soco de perto
                m.Sim[0].Pos = new Vector2(-0.5f, 0);
                m.Sim[1].Pos = new Vector2(0.5f, 0);
                D.Keys(Key.J);
                WaitTicks(f, 30, null);
                if (round == 0) D.Shot("f11_ko");
                WaitTicks(f, 0, () => m.Phase != Combat.MatchPhase.Fighting);
            }
            D.Note($"fase={m.Phase} placar={m.Wins[0]}x{m.Wins[1]} vencedor={m.MatchWinner}");
            WaitTicks(f, 0, () => f.IsInResults);
            D.Step(20);
            D.Shot("f12_resultado");
            D.Note("resultado aberto=" + f.IsInResults);
            D.Note("antes da revanche: " + Census());
            D.Key(Key.Enter); // Revanche
            D.Step(30);
            D.Note($"revanche: fase={m.Phase} placar={m.Wins[0]}x{m.Wins[1]} vida={m.Sim[0].Health}/{m.Sim[1].Health}");
            D.Note("depois da revanche: " + Census());
            Flush("Fluxo 4");
        }

        [MenuItem("Último Round/Dev/Fluxo 5 - Sair para o menu (confirmação)")]
        public static void Flow5()
        {
            Begin();
            WaitTicks(D.Fight, 0, () => D.Fight.Match.Phase == Combat.MatchPhase.Fighting);
            D.Key(Key.Escape);  // pausa
            D.Step(3);
            for (int i = 0; i < 4; i++) { D.Key(Key.DownArrow); D.Step(1); }
            D.Shot("f13_pausa_sair");
            D.Key(Key.Enter);   // Sair para o menu
            D.Step(3);
            D.Shot("f14_confirmacao");
            D.Key(Key.DownArrow);
            D.Key(Key.Enter);   // Sim
            D.Step(30);
            D.Note("cena=" + D.Scene);
            D.Step(20);
            D.Shot("f15_menu_volta");
            D.Note(Census());
            D.Teardown();
            Flush("Fluxo 5");
        }

        static void Down(int n) { for (int i = 0; i < n; i++) { D.Key(Key.DownArrow); D.Step(1); } }
        static void PadDown(int n) { for (int i = 0; i < n; i++) { D.PadButton(GamepadButton.DpadDown); D.Step(1); } }

        static void WaitScene(string name)
        {
            int guard = 0;
            while (D.Scene != name && guard++ < 600) D.Step(1);
            D.Step(5);
        }

        [MenuItem("Último Round/Dev/Fluxo 6 - Treino (Faísca x Vértice)")]
        public static void Flow6()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            D.Setup(ShotDir);
            D.Step(10);
            D.Step(40);
            D.Key(Key.Enter);           // abertura
            D.Step(4);
            Down(2);                    // Treino
            D.Key(Key.Enter);
            D.Step(3);
            D.Key(Key.Enter);           // J1 entra com teclado
            D.Step(40);
            D.Key(Key.RightArrow); D.Step(1); D.Key(Key.RightArrow); D.Step(1);   // Faísca
            D.Key(Key.Enter); D.Step(2);
            D.Key(Key.RightArrow); D.Step(1); D.Key(Key.RightArrow); D.Step(1);   // boneco: Vértice
            D.Shot("t01_selecao_treino");
            D.Key(Key.Enter);
            WaitScene("Fight");
            var f = D.Fight;
            D.Note($"modo={D.Services.Session.Mode} lutadores={f.Match.Sim[0].Def.Id} x {f.Match.Sim[1].Def.Id} fase={f.Match.Phase} treino={f.Match.Training}");
            WaitTicks(f, 60, null);
            // Pausa (menu de treino): Boneco -> Defendendo; Volumes de colisão -> visíveis
            D.Key(Key.Escape); D.Step(2);
            Down(3); D.Key(Key.RightArrow); D.Step(1);            // Boneco: Defendendo
            Down(3); D.Key(Key.RightArrow); D.Step(1);            // Volumes: visíveis
            D.Shot("t02_menu_treino");
            D.Note($"boneco={D.Services.Session.Dummy} caixas={D.Services.Session.TrainingShowBoxes}");
            D.Key(Key.Escape); D.Step(3);                         // retoma
            // J1 ataca o boneco que defende: baixo deve acertar? (boneco escolhe altura correta) -> deve defender
            f.Match.Sim[0].Pos = new Vector2(-0.45f, 0); f.Match.Sim[1].Pos = new Vector2(0.45f, 0);
            D.Keys(Key.K); WaitTicks(f, 4, null);
            D.Shot("t03_volumes_colisao");
            WaitTicks(f, 30, null);
            D.SetKeys(Key.S); D.Step(1); D.SetKeys(Key.S, Key.K); D.Step(1); D.SetKeys(); D.Step(1);
            WaitTicks(f, 40, null);
            D.Note($"boneco defendendo: vida={f.Match.Sim[1].Health}/{f.Match.Sim[1].Def.MaxHealth} energiaJ1={f.Match.Sim[0].Energy}");
            // Demonstração de combo pela lista de golpes
            D.Key(Key.Escape); D.Step(2);
            Down(1); D.Key(Key.Enter); D.Step(3);                 // Lista de golpes
            D.Shot("t04_lista_golpes");
            D.Key(Key.Enter); D.Step(2);                          // Demonstrar combo 1
            WaitTicks(f, 12, null);
            D.Shot("t05_demonstracao");
            WaitTicks(f, 90, null);
            D.Note($"após demo: combo/vida boneco={f.Match.Sim[1].Health} (vida infinita repõe depois)");
            // Reset (R)
            f.Match.Sim[0].Pos = new Vector2(3f, 0);
            D.Key(Key.R); D.Step(2);
            D.Note($"reset: posições={f.Match.Sim[0].Pos.x:F2}/{f.Match.Sim[1].Pos.x:F2} vida={f.Match.Sim[0].Health}/{f.Match.Sim[1].Health}");
            // Sair do treino (sem confirmação)
            D.Key(Key.Escape); D.Step(2);
            Down(9); D.Shot("t06_sair_treino"); D.Key(Key.Enter);
            WaitScene("MainMenu");
            D.Note("cena=" + D.Scene + " | " + Census());
            D.Note($"treino não vaza: sessão.Mode={D.Services.Session.Mode} (flags de treino só são lidas em Treino)");
            Flush("Fluxo 6");
        }

        [MenuItem("Último Round/Dev/Fluxo 8 - Opções: remapear e restaurar")]
        public static void Flow8()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            D.Setup(ShotDir);
            D.Step(40);
            D.Key(Key.Enter); D.Step(3);                          // abertura -> menu
            Down(3); D.Key(Key.Enter); D.Step(3);                 // Opções
            D.Shot("o01_opcoes");
            Down(10); D.Key(Key.Enter); D.Step(2);                // Soco (teclado) -> escutando
            D.Shot("o02_escutando");
            D.Key(Key.H); D.Step(2);
            D.Shot("o03_remapeado");
            string path = Path.Combine(Application.persistentDataPath, "settings.json");
            string json = File.ReadAllText(path);
            D.Note("arquivo: " + path);
            D.Note("soco = H salvo: " + json.Contains("\"h\"") + " | J removido: " + !json.Contains("\"j\""));
            D.Note("leitor ativo usa H: " + (D.Services.Settings.Bindings.Keyboard[(int)Input.GameAction.Punch] == "h"));
            Down(12); D.Key(Key.Enter); D.Step(2);                // Restaurar teclado padrão
            json = File.ReadAllText(path);
            D.Note("restaurado: J presente=" + json.Contains("\"j\"") + " H ausente=" + !json.Contains("\"h\""));
            // Arquivo inválido -> padrões seguros
            File.WriteAllText(path, "{ isto não é json válido");
            var loaded = GameSettings.Load();
            D.Note($"arquivo inválido -> padrões: volume={loaded.MasterVolume} soco={loaded.Bindings.Keyboard[(int)Input.GameAction.Punch]}");
            D.Services.SaveSettings();
            D.Key(Key.Escape); D.Step(2);
            D.Teardown();
            Flush("Fluxo 8");
        }

        [MenuItem("Último Round/Dev/Fluxo 7 - Contra CPU (Difícil) e Opções")]
        public static void Flow7()
        {
            Begin();
            D.Setup(ShotDir);
            D.Step(10);
            D.Step(40);
            D.PadButton(GamepadButton.South); D.Step(4);          // abertura (controle)
            PadDown(1); D.PadButton(GamepadButton.South); D.Step(3);   // Contra CPU
            D.PadButton(GamepadButton.South); D.Step(40);         // J1 entra com controle
            D.Shot("c01_dificuldade");
            PadDown(2); D.PadButton(GamepadButton.South); D.Step(3);   // Difícil
            for (int i = 0; i < 3; i++) { D.PadButton(GamepadButton.DpadRight); D.Step(1); }  // Vértice
            D.PadButton(GamepadButton.South); D.Step(2);
            D.PadButton(GamepadButton.South); D.Step(2);          // CPU: Brutus (cursor inicial)
            WaitScene("Fight");
            var f = D.Fight;
            D.PadButton(GamepadButton.South); D.Step(3);
            D.PadButton(GamepadButton.South); D.Step(3);
            WaitTicks(f, 0, () => f.Match.Phase == Combat.MatchPhase.Fighting);
            WaitTicks(f, 600, null);
            var sim = f.Match.Sim;
            D.Shot("c02_luta_cpu");
            D.Note($"CPU {D.Services.Session.Difficulty}: {sim[0].Def.Id} vida={sim[0].Health} x {sim[1].Def.Id} vida={sim[1].Health} fase={f.Match.Phase}");
            D.PadButton(GamepadButton.Start); D.Step(2);
            PadDown(2); D.PadButton(GamepadButton.South); D.Step(3);   // Opções
            D.Shot("c03_opcoes_pausa");
            D.PadButton(GamepadButton.East); D.Step(2);           // fecha opções -> pausa
            D.PadButton(GamepadButton.East); D.Step(2);           // retoma
            D.Note($"retomado: pausado={f.Match.Phase}");
            D.PadButton(GamepadButton.Start); D.Step(2);
            PadDown(4); D.PadButton(GamepadButton.South); D.Step(2);   // Sair -> confirmação
            PadDown(1); D.PadButton(GamepadButton.South);
            WaitScene("MainMenu");
            D.Note("cena=" + D.Scene);
            // Opções pelo menu principal: remapear Soco do teclado para H e restaurar
            D.Step(40);         // a abertura ignora entradas nos primeiros 0,2 s
            D.Key(Key.Enter); D.Step(3);
            Down(3); D.Key(Key.Enter); D.Step(3);                 // Opções
            Down(10); D.Key(Key.Enter); D.Step(2);                // Soco (teclado) -> escutando
            D.Key(Key.H); D.Step(2);
            D.Shot("c04_remapeado");
            string json = File.ReadAllText(Path.Combine(Application.persistentDataPath, "settings.json"));
            D.Note("soco remapeado salvo em settings.json: " + json.Contains("\"h\""));
            Down(12); D.Key(Key.Enter); D.Step(2);                // Restaurar teclado padrão
            json = File.ReadAllText(Path.Combine(Application.persistentDataPath, "settings.json"));
            D.Note("restaurado (j presente): " + json.Contains("\"j\"") + " h ausente: " + !json.Contains("\"h\""));
            D.Key(Key.Escape); D.Step(2);
            D.Teardown();
            Flush("Fluxo 7");
        }

        [MenuItem("Último Round/Dev/Fluxo 9 - Desconexão e proporções de tela")]
        public static void Flow9()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            D.Setup(ShotDir);
            D.Step(40);
            D.Key(Key.Enter); D.Step(3);
            D.Key(Key.Enter); D.Step(3);                          // Versus
            D.Key(Key.Enter); D.Step(2);                          // J1 teclado
            D.PadButton(GamepadButton.South); D.Step(40);         // J2 gamepad
            D.Key(Key.Enter); D.Step(2);
            D.PadButton(GamepadButton.South); D.Step(2);
            WaitScene("Fight");
            var f = D.Fight;
            D.Key(Key.Enter); D.Step(3); D.Key(Key.Enter); D.Step(3);
            WaitTicks(f, 0, () => f.Match.Phase == Combat.MatchPhase.Fighting);
            WaitTicks(f, 30, null);
            // Proporções de tela (HUD sem cortes/distorção)
            DevTools.CaptureGame(Path.Combine(ShotDir, "r01_16x9.png"), 1600, 900);
            DevTools.CaptureGame(Path.Combine(ShotDir, "r02_16x10.png"), 1440, 900);
            DevTools.CaptureGame(Path.Combine(ShotDir, "r03_21x9.png"), 2100, 900);
            D.Note("capturas 16:9, 16:10 e 21:9");
            // Desconecta o gamepad do J2 no meio da luta
            var pad = D.Pad;
            int tick = f.Match.Sim.Tick;
            InputSystem.RemoveDevice(pad);
            D.Step(5);
            var slot = D.Services.Input.Slots[1];
            D.Note($"desconectado: perdido={slot.Lost} tick antes={tick} depois={f.Match.Sim.Tick}");
            D.Shot("r04_desconectado");
            D.Step(30);
            D.Note($"30 quadros depois: tick={f.Match.Sim.Tick} (deve continuar igual)");
            InputSystem.AddDevice(pad);
            D.Step(3);
            D.Note($"reconectado: perdido={slot.Lost} aguardando={slot.AwaitingConfirm} mesmo dispositivo={slot.Device == pad}");
            D.Shot("r05_reconectado");
            D.Key(Key.Enter); D.Step(3);                          // outro jogador não pode confirmar por ele
            D.Note($"após Enter do J1: aguardando={slot.AwaitingConfirm}");
            D.PadButton(GamepadButton.South); D.Step(3);          // J2 confirma
            D.Note($"após A do J2: aguardando={slot.AwaitingConfirm} (pausa aberta para retomar)");
            D.Shot("r06_pausa_apos_reconexao");
            D.PadButton(GamepadButton.East); D.Step(3);           // retoma
            int t1 = f.Match.Sim.Tick;
            WaitTicks(f, 10, null);
            D.Note($"retomado: tick {t1} -> {f.Match.Sim.Tick}");
            D.Teardown();
            Flush("Fluxo 9");
        }

        [MenuItem("Último Round/Dev/Fluxo 0 - Abertura aceita qualquer tecla")]
        public static void Flow0()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            D.Setup(ShotDir);
            D.Step(40);
            D.Note("antes: na abertura=" + D.Menu.IsOnTitle);
            D.Key(Key.A); D.Step(3);
            D.Note("após tecla A: na abertura=" + D.Menu.IsOnTitle + " (deve ser False)");
            D.Teardown();
            Flush("Fluxo 0");
        }

        static void StartVersus()
        {
            D.Setup(ShotDir);
            D.Step(40);
            D.Key(Key.Enter); D.Step(3);
            D.Key(Key.Enter); D.Step(3);                          // Versus
            D.Key(Key.Enter); D.Step(2);                          // J1 teclado
            D.PadButton(GamepadButton.South); D.Step(40);         // J2 gamepad
        }

        [MenuItem("Último Round/Dev/Fluxo 10 - Modelos, golpes e efeitos")]
        public static void Flow10()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            StartVersus();
            D.Shot("m01_selecao");
            D.Key(Key.Enter); D.Step(2);
            D.PadButton(GamepadButton.South); D.Step(2);
            WaitScene("Fight");
            var f = D.Fight;
            f.EnableAutoPlay();
            WaitTicks(f, 0, () => f.Match.Phase == Combat.MatchPhase.Fighting);
            D.Shot("m02_inicio");
            var seen = new System.Collections.Generic.HashSet<string>();
            int shots = 0, guard = 0;
            while (shots < 8 && guard++ < 6000)
            {
                D.Step(1);
                var sim = f.Match.Sim;
                for (int i = 0; i < 2; i++)
                {
                    var s = sim[i];
                    if (s.State != Combat.FighterStateId.Attack || s.Move == null || s.MoveTick != s.Move.Startup) continue;
                    string key = s.Def.Id + "_" + s.Move.Slot;
                    if (!seen.Add(key)) continue;
                    D.Shot($"m1{shots}_{key}");
                    D.Note($"{key}: {s.Move.DisplayName} oponente={sim[1 - i].State}");
                    shots++;
                    break;
                }
            }
            D.Note($"fase={f.Match.Phase} vida={f.Match.Sim[0].Health}/{f.Match.Sim[1].Health} placar={f.Match.Wins[0]}x{f.Match.Wins[1]}");
            // Até o fim da partida (com CPU nos dois lados a revanche é automática: a tela de resultado não abre).
            bool koShot = false;
            guard = 0;
            while (f.Match.Phase != Combat.MatchPhase.MatchOver && guard++ < 40000)
            {
                D.Step(1);
                if (!koShot && f.Match.Sim[0].State == Combat.FighterStateId.KO || !koShot && f.Match.Sim[1].State == Combat.FighterStateId.KO)
                {
                    D.Step(30);
                    D.Shot("m90_nocaute");
                    koShot = true;
                }
            }
            D.Step(60);
            D.Note($"fase={f.Match.Phase} quadros={guard} placar={f.Match.Wins[0]}x{f.Match.Wins[1]}");
            D.Shot("m91_vitoria");
            D.Note(Census());
            D.Teardown();
            Flush("Fluxo 10");
        }

        [MenuItem("Último Round/Dev/Fluxo 11 - Rolagem de Opções e Lista de golpes")]
        public static void Flow11()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            StartVersus();
            D.Key(Key.Enter); D.Step(2);
            D.PadButton(GamepadButton.South); D.Step(2);
            WaitScene("Fight");
            var f = D.Fight;
            D.Key(Key.Enter); D.Step(3); D.Key(Key.Enter); D.Step(3);
            WaitTicks(f, 0, () => f.Match.Phase == Combat.MatchPhase.Fighting);
            D.Key(Key.Escape); D.Step(2);
            // Lista de golpes
            Down(1); D.Key(Key.Enter); D.Step(3);
            var moves = f.MoveList;
            D.Note($"golpes: início={moves.ScrollPosition:F0} aberta={moves.IsOpen}");
            D.SetKeys(Key.DownArrow); D.Step(20); D.SetKeys(); D.Step(1);
            D.Note($"golpes: segurando baixo={moves.ScrollPosition:F0} (deve aumentar)");
            float afterHold = moves.ScrollPosition;
            D.Shot("s01_golpes_rolados");
            D.Wheel(-1f);
            D.Note($"golpes: roda p/ baixo={moves.ScrollPosition:F0} (deve aumentar, era {afterHold:F0})");
            D.Wheel(1f);
            D.Wheel(1f);
            D.Note($"golpes: roda p/ cima x2={moves.ScrollPosition:F0} (deve diminuir)");
            D.SetKeys(Key.DownArrow); D.Step(600); D.SetKeys(); D.Step(1);
            D.Note($"golpes: fim={moves.ScrollPosition:F0}");
            D.Shot("s02_golpes_fim");
            D.Key(Key.Escape); D.Step(3);                         // volta à pausa
            // Opções
            Down(2); D.Key(Key.Enter); D.Step(3);
            var opts = f.Options;
            D.Note($"opções: início={opts.ListScroll} aberta={opts.IsOpen}");
            D.Wheel(-1f); D.Wheel(-1f); D.Wheel(-1f);
            D.Note($"opções: roda p/ baixo x3={opts.ListScroll} (deve aumentar)");
            D.Shot("s03_opcoes_roda");
            D.Wheel(1f);
            D.Note($"opções: roda p/ cima={opts.ListScroll} (deve diminuir)");
            Down(25);
            D.Note($"opções: teclado 25x baixo={opts.ListScroll} (deve chegar aos controles)");
            D.Shot("s04_opcoes_controles");
            D.Key(Key.Escape); D.Step(2);
            D.Teardown();
            Flush("Fluxo 11");
        }

        [MenuItem("Último Round/Dev/Fluxo 12 - Galeria de visuais")]
        public static void Flow12()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            StartVersus();
            int[] counts = { 3, 3, 3, 4 };
            string[] ids = { "giovani", "brutus", "faisca", "vertice" };
            for (int f = 0; f < 4; f++)
            {
                for (int v = 0; v < counts[f]; v++)
                {
                    D.Step(12);
                    D.Shot($"g_{ids[f]}_{v}");
                    var menu = Object.FindAnyObjectByType<Core.MainMenuController>();
                    var pal = (int[])typeof(Core.MainMenuController).GetField("_palette", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu);
                    D.Note($"{ids[f]} v{v}: visual {pal[0]}");
                    D.Key(Key.Tab); D.Step(3);
                }
                D.Key(Key.RightArrow); D.Step(3);
            }
            D.Note(Census());
            D.Teardown();
            Flush("Fluxo 12");
        }

        [MenuItem("Último Round/Dev/Fluxo 13 - Efeitos especiais")]
        public static void Flow13()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            StartVersus();
            D.Key(Key.Enter); D.Step(2);
            D.PadButton(GamepadButton.South); D.Step(2);
            WaitScene("Fight");
            var f = D.Fight;
            WaitTicks(f, 0, () => f.Match.Phase == Combat.MatchPhase.Fighting);
            D.Step(90);
            foreach (Presentation.VfxKind kind in System.Enum.GetValues(typeof(Presentation.VfxKind)))
            {
                f.Vfx.Play(kind, new Vector3(0f, kind == Presentation.VfxKind.Dust ? 0.05f : 1.1f, -0.3f));
                D.Step(9);
                D.Shot("v_" + kind);
                D.Step(60);
            }
            D.Note(Census());
            D.Teardown();
            Flush("Fluxo 13");
        }

        [MenuItem("Último Round/Dev/Fluxo 14 - Resultado, revanche e saída com modelos")]
        public static void Flow14()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            StartVersus();
            D.Key(Key.Enter); D.Step(2);
            D.PadButton(GamepadButton.South); D.Step(2);
            WaitScene("Fight");
            var f = D.Fight;
            D.Key(Key.Enter); D.Step(3); D.Key(Key.Enter); D.Step(3);
            // Usa a tecla configurada: o jogador pode ter remapeado o soco.
            string path = D.Services.Settings.Bindings.Keyboard[(int)Input.GameAction.Punch];
            var punch = ((UnityEngine.InputSystem.Controls.KeyControl)D.Kb[path]).keyCode;
            var m = f.Match;
            for (int round = 0; round < 3 && m.Phase != Combat.MatchPhase.MatchOver; round++)
            {
                WaitTicks(f, 0, () => m.Phase == Combat.MatchPhase.Fighting || m.Phase == Combat.MatchPhase.MatchOver);
                if (m.Phase == Combat.MatchPhase.MatchOver) break;
                m.Sim[1].Health = 1;
                m.Sim[0].Pos = new Vector2(-0.5f, 0);
                m.Sim[1].Pos = new Vector2(0.5f, 0);
                D.Keys(punch);
                WaitTicks(f, 30, null);
                WaitTicks(f, 0, () => m.Phase != Combat.MatchPhase.Fighting);
            }
            D.Note($"fase={m.Phase} placar={m.Wins[0]}x{m.Wins[1]} vencedor={m.MatchWinner}");
            WaitTicks(f, 0, () => f.IsInResults);
            D.Step(20);
            D.Shot("r01_resultado");
            D.Note("resultado aberto=" + f.IsInResults);
            D.Note("antes da revanche: " + Census());
            D.Key(Key.Enter);
            D.Step(30);
            D.Note($"revanche: fase={m.Phase} placar={m.Wins[0]}x{m.Wins[1]} vida={m.Sim[0].Health}/{m.Sim[1].Health}");
            D.Note("depois da revanche: " + Census());
            WaitTicks(f, 0, () => m.Phase == Combat.MatchPhase.Fighting);
            D.Key(Key.Escape); D.Step(3);
            for (int i = 0; i < 4; i++) { D.Key(Key.DownArrow); D.Step(1); }
            D.Key(Key.Enter); D.Step(3);
            D.Key(Key.DownArrow);
            D.Key(Key.Enter);
            D.Step(50);
            D.Note("cena=" + D.Scene);
            D.Shot("r02_menu");
            D.Note(Census());
            D.Teardown();
            Flush("Fluxo 14");
        }

        [MenuItem("Último Round/Dev/Fluxo 15 - Telas de rede local")]
        public static void Flow15()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            D.Setup(ShotDir);
            D.Step(40);
            D.Key(Key.Enter); D.Step(3);
            D.Shot("n00_menu");
            Down(1);
            D.Key(Key.Enter); D.Step(3);                // Rede Local
            D.Key(Key.Enter); D.Step(40);               // associa o teclado
            D.Shot("n01_rede");
            var net = D.Services.Net;
            D.Note("modo=" + D.Services.Session.Mode);
            D.Key(Key.Enter); D.Step(5);                // Hospedar
            D.Note("hospedando=" + net.Status + " porta=" + net.LocalPort);
            D.Step(10);
            D.Shot("n02_aguardando");
            D.Key(Key.Escape); D.Step(5);               // Cancelar
            D.Note("cancelado=" + net.Status);
            Down(1);
            D.Key(Key.Enter); D.Step(30);               // Entrar
            D.Note("procurando=" + net.Status + " partidas=" + net.Hosts.Count);
            D.Shot("n03_procurando");
            D.Key(Key.Enter); D.Step(5);                // Digitar endereço
            foreach (char c in "192.168") D.Kb.OnTextInput(c);
            D.Step(2);
            D.Key(Key.Enter); D.Step(3);
            D.Shot("n04_invalido");
            foreach (char c in ".0.10") D.Kb.OnTextInput(c);
            D.Step(2);
            D.Shot("n05_endereco");
            D.Key(Key.Backspace); D.Step(2);
            D.Key(Key.Escape); D.Step(5);
            D.Note("voltou=" + net.Status);
            D.Key(Key.Escape); D.Step(5);
            D.Key(Key.Escape); D.Step(5);
            D.Note("fim=" + net.Status);
            D.Note(Census());
            D.Teardown();
            Flush("Fluxo 15");
        }

        static void OpenNetMenu()
        {
            D.Setup(ShotDir);
            D.Step(40);
            D.Key(Key.Enter); D.Step(3);
            Down(1);
            D.Key(Key.Enter); D.Step(3);
            D.Key(Key.Enter); D.Step(40);
        }

        static bool StepUntil(System.Func<bool> done, int guard = 3000)
        {
            for (int i = 0; i < guard; i++)
            {
                if (done()) return true;
                D.Step(1);
            }
            return done();
        }

        /// <summary>Teclas reais do jogador local até o golpe aparecer na luta, e o menu que não pausa a luta em rede.</summary>
        static void PlayNetFight(string tag)
        {
            var net = D.Services.Net;
            StepUntil(() => D.Scene == "Fight");
            var f = D.Fight;
            D.Note($"{tag}: luta carregada papel={net.Role} ativo={net.Active}");
            StepUntil(() => f.Match.Phase == Combat.MatchPhase.Fighting);
            D.Note($"{tag}: fase={f.Match.Phase} rtt={net.RttMs:F0}ms");
            D.Shot(tag + "_luta");

            string path = D.Services.Settings.Bindings.Keyboard[(int)Input.GameAction.Punch];
            var punch = ((UnityEngine.InputSystem.Controls.KeyControl)D.Kb[path]).keyCode;
            int local = net.LocalIndex;
            var me = f.Match.Sim[local];
            bool attacked = false;
            for (int i = 0; i < 40 && !attacked; i++)
            {
                D.Keys(punch);
                attacked = StepUntil(() => me.State == Combat.FighterStateId.Attack && me.Move != null && me.Move.Slot == Combat.MoveSlot.Punch, 25);
            }
            D.Note($"{tag}: soco pelo teclado virou golpe do lutador {local}: {attacked}");
            D.Shot(tag + "_soco");

            D.Key(Key.Escape); D.Step(5);
            int before = f.Match.Sim.Tick;
            int timer = f.Match.TimerTicks;
            D.Shot(tag + "_menu");
            D.Step(120);
            D.Note($"{tag}: menu aberto, tick {before}->{f.Match.Sim.Tick} cronômetro {timer}->{f.Match.TimerTicks} (a luta deve continuar)");
            D.Key(Key.Escape); D.Step(5);
            D.Key(Key.Escape); D.Step(5);
            Down(3);
            D.Key(Key.Enter); D.Step(5);
            D.Key(Key.DownArrow);
            D.Key(Key.Enter); D.Step(60);
            D.Note($"{tag}: saiu; cena={D.Scene} rede={net.Status}");
            D.Note(Census());
            D.Teardown();
        }

        [MenuItem("Último Round/Dev/Fluxo 16 - Rede: editor como convidado (exe com -net-host)")]
        public static void Flow16()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            OpenNetMenu();
            var net = D.Services.Net;
            Down(1);
            D.Key(Key.Enter); D.Step(5);
            bool found = StepUntil(() => net.Hosts.Count > 0);
            D.Step(5);
            D.Note($"convidado: partida encontrada={found} {(found ? net.Hosts[0].Name + " " + net.Hosts[0].Address : "")}");
            D.Shot("c01_lista");
            D.Key(Key.Enter); D.Step(3);
            bool joined = StepUntil(() => net.Active);
            D.Step(20);
            D.Note($"convidado: conectado={joined} {net.FailReason}");
            D.Shot("c02_selecao");
            D.Key(Key.RightArrow); D.Step(3);
            D.Key(Key.Enter); D.Step(3);
            PlayNetFight("c");
            Flush("Fluxo 16");
        }

        [MenuItem("Último Round/Dev/Fluxo 17 - Rede: editor como anfitrião (exe com -net-join)")]
        public static void Flow17()
        {
            Begin();
            File.WriteAllText(LogPath, "");
            OpenNetMenu();
            var net = D.Services.Net;
            D.Key(Key.Enter); D.Step(5);
            bool joined = StepUntil(() => net.Active, 6000);
            D.Step(20);
            D.Note($"anfitrião: convidado conectou={joined} de {net.PeerAddress}");
            D.Shot("h01_selecao");
            D.Key(Key.Enter); D.Step(3);
            PlayNetFight("h");
            Flush("Fluxo 17");
        }
    }
}
