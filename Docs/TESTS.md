# Testes

## Automatizados — EditMode (61/61 passando em 03/11/2026, Unity 6000.6.7f1)
Rodar: menu **Último Round › Testes › Rodar EditMode** (síncrono) ou Test Runner. Resultado em `Logs/UltimoRound_TestResults.txt`.

| # do briefing | Teste(s) | Resultado |
|---|---|---|
| 1 | `GolpeComum_AplicaDanoUmaVez_MesmoComVariasHurtboxes`, `MultiHit_UsaIndicesExplicitos` | Passou |
| 2 | `ForaDeAlcance_NaoAcerta`, `Startup_NaoAtivaHitbox_EAcertaNoPrimeiroTickAtivo`, `Recuperacao_NaoAtivaHitbox`, `JanelaAtiva_LimitesCorretos` | Passou |
| 3 | `Baixo_ExigeDefesaAgachada`, `Overhead_ExigeDefesaEmPe`, `Medio_DefendidoEmPeOuAgachado`, `Alto_PassaPorCimaDeAgachado`, `Agarrao_VenceDefesa…`, `Agarrao_EscapeNaJanela`, `Agarrao_FalhaForaDeAlcance_NoAr_EEmHitstun`, `Agarrao_SimultaneoResultaEmEscape` | Passou |
| 4 | `NaoAtacaEmKO_Derrubado_OuBloqueado`, `Derrubado_NaoRecebeDano`, `AposVencedor_NaoHaDanoNemNovosGolpes` | Passou |
| 5 | `Energia_GanhoEmAcertoDefesaENadaNoVazio`, `Assinatura_BloqueadaSemEnergia_ConsomeBarraCheia` | Passou |
| 6 | `Combo_ReducaoDeDanoComPiso`, `Combo_JanelaDeCancelamentoSoAposContato`, `Rebote_NoMaximoUmPorCombo`, `Rebote_NaoOcorreEmDefesa`, `Juggle_LimiteTornaAlvoImune`, `Combo_LimiteForcaDerrubada` | Passou |
| 7 | `KO_VenceRound_EMelhorDeTresEncerraUmaVez`, `TempoEsgotado_VenceQuemTemMaiorPorcentagem`, `TempoEsgotado_EmpateExato_RepeteRoundSemPontuar`, `DuploKO_EmpateSemPontuar`, `Cronometro_PausaDuranteHitstop` | Passou |
| 8 | `Reset_LimpaEstado`, `Revanche_StartMatchZeraPlacar` (+ contagem de objetos nos roteiros abaixo) | Passou |
| 9 | `Espelhado_NaoCompartilhaEstado` | Passou |
| 10 | `Orientacao_ComandosRelativos`, `TrocaDeLadoNoAr_OrientacaoMudaSoAoAterrissar`, `Pushbox_NaoAtravessaNoChao`, `Pushbox_NaCordaEmpurraSemPrender` | Passou |
| 11 | `Pausa_NaoInjetaComandosAoRetomar`, `Acumulador_NaoPerdeToqueCurtoEntreTicks`, `Pausa_MatchNaoEvoluiSemTicks` | Passou |
| 12 | `Replay_ResultadoEstavelEm30_60_120Fps` (30/60/120 e 144 com jitter — mesmo ambiente; **não** prova determinismo entre máquinas), `Relogio_LimitaPicosSemAcelerar` | Passou |
| Elenco | `Elenco_TodosOsSlotsObrigatorios`, `Elenco_GrafoDeCancelamentoSemCiclos`, `Elenco_AtaquesFortesPuniveisQuandoDefendidos`, `CombosSugeridos_ConectamACurtaDistancia` (cada combo contra os 4 adversários) | Passou |
| Smoke | `Smoke_TodosOsPares_EntradasAleatorias` (16 pares incl. 4 espelhados; limites de vida/energia/arena, sem sobreposição no chão, sem combo infinito), `Cpu_TodosOsParesEDificuldades_PartidaTermina` (48 partidas CPU×CPU completas) | Passou |
| Troca | `Troca_GolpesSimultaneosAcertamAmbos`, `Treino_SemCronometroNemFimDeRound` | Passou |

Smoke/CPU comprovam robustez das regras, **não** balanceamento.

## Roteiros de integração em Play Mode (editor, dispositivos virtuais)
Menu **Último Round › Dev › Fluxo 1…9** (iniciar Play Mode na cena MainMenu). Teclado e gamepad **virtuais** do Input System; estado aplicado com `InputState.Change` e quadros avançados com `EditorApplication.Step()`. Saída em `Logs/UltimoRound_Flow.txt` e capturas em `Logs/Capturas/`.

| Fluxo | Verificado | Resultado observado |
|---|---|---|
| 1–2 | Abertura → menu → Versus → J1 teclado + J2 gamepad → seleção → confronto | OK; espelhado com cores 0/1; 1 GameServices/AudioService/EventSystem |
| 3 | Luta, J2 defendendo, pausa | Energia por defesa correta (6); tick congelado na pausa (211→211) e retomado |
| 4 | KO ×2, resultado, revanche | Placar 2×0, resultado aberto, revanche zera placar/vida; contagem de objetos igual antes/depois |
| 5 | Sair com confirmação | Volta ao menu sem duplicar serviços/áudio/HUD |
| 6 | Treino Faísca × Vértice | Boneco defendendo (alto/baixo), overlay de volumes, lista de golpes, demonstração acerta, reset, sair |
| 7 | Contra CPU Difícil só com gamepad; opções na pausa | OK (CPU causa dano; menus navegáveis só com controle) |
| 8 | Remapear Soco→H, restaurar padrão, arquivo inválido | Salvo em `settings.json`; restaurado; arquivo inválido → padrões |
| 9 | Desconexão no meio da luta; reconexão; proporções 16:9/16:10/21:9 | Pausa automática (tick congelado); só o J2 confirma a retomada; HUD sem cortes |

Defeitos encontrados por esses roteiros e corrigidos: confirmação dupla na seleção de 1 jogador; demonstração bloqueada pelo boneco em defesa; captura de tecla do remapeamento; MonoBehaviours que viravam “script ausente”; menu de pausa aparecendo atrás do aviso de reconexão.

### Roteiros da versão 0.10.0 (03/11/2026)
Os Fluxos 0–9 acima são de 28/10/2026 e **não foram repetidos** na 0.10.0: os de luta dependem das teclas padrão e o `settings.json` local tem controles remapeados.

| Fluxo | Cobertura | Resultado |
|---|---|---|
| 10 | CPU × CPU com modelos até o fim da partida | 2×1 em 3.483 quadros; capturas de golpes, nocaute e vitória |
| 11 | Rolagem de Lista de golpes e Opções | Segurar baixo: 0→440→644 (fim). Roda do mouse virtual: 440→550→330. Opções: roda 0→6→4; teclado chega aos controles (índice 16) |
| 12 | Os 13 visuais na seleção | Todos renderizam, sem material rosa nem T-pose |
| 13 | Os 13 efeitos | Todos aparecem |
| 14 | Resultado, revanche e saída ao menu com a tecla configurada | 2×0; revanche zera placar/vida; contagem de objetos igual antes e depois; volta ao menu |

Limites: a roda do mouse foi simulada por dispositivo virtual e a **barra de rolagem arrastada não foi testada**. Clipes de pulo, avanço e agarrão não têm captura dedicada. Os visuais alternativos só foram vistos na seleção; em luta, apareceram os principais de Giovani e Brutus e o "Pesado".

Defeitos encontrados e corrigidos: fumaça preta e duradoura na poeira; flash apagava a textura do modelo; impacto pesado cobria o lutador; halos pretos dos efeitos; barris gigantes atrás do ringue; ponteiro parado roubava o foco dos menus.

## Executável (fora do editor)
- Build Windows x64 v0.10.0: sucesso, 0 erros, 488 avisos (a origem de cada aviso não foi revisada). 257,6 MB; ZIP de 112 MB.
- `UltimoRound.exe -benchmark -benchmark-seconds 30` (03/11/2026): abriu, lutou CPU×CPU (Giovani × Brutus), gravou o relatório e fechou com código 0. `Player.log` sem exceções; só os avisos esperados de pós-processamento removido.
- 16 capturas da janela do executável: modelos e efeitos renderizam, sem material rosa.
- **Medição (não é garantia de 60 FPS em outras máquinas):** AMD Ryzen 7 5700U + Radeon integrada, 10 GB RAM, Windows 11, 1920×1080 tela cheia sem bordas, VSync desligado.
  - 30 s: média **10,94 ms** (91,4 FPS), mediana 10,65, p95 11,78, p99 12,22, máx 245,93 ms; **0,11%** dos quadros acima de 16,67 ms.
  - 40 s: média **10,83 ms** (92,3 FPS), mediana 10,67, p95 11,80, p99 12,20, máx 24,68 ms; **0,05%** acima de 16,67 ms.
  - Só foi medido esse confronto. O pico de 245,93 ms não se repetiu na segunda execução e não foi investigado no Profiler.

- **Outras medições do mesmo binário (03/11/2026, à tarde, na bateria com 60% de carga, editor aberto, plano Equilibrado):** 30 s: 75,0, 78,1 e 79,2 FPS; cerca de 8% dos quadros acima de 16,67 ms; pior quadro entre 342 e 392 ms. Um teste curto de 8 s deu 56,7 FPS. Em todas as execuções houve cerca de 180 quadros lentos, o que sugere um trecho concentrado. A bateria e a carga da máquina são hipóteses: a causa não foi isolada, e os trechos lentos não foram investigados no Profiler.

## Versão de entrega 1.0.0 (10/11/2026)
- **Build pela linha de comando** (`BuildTool.BuildReleaseBatch`): sucesso, 0 erros; ZIP de 112 MB e instalador de 85,1 MB. Uma luta automática de 10 s rodou sem exceções.
- **Testes pela linha de comando:** 61/61.
- **Repositório público sem os pacotes da Asset Store**, aberto do zero em uma Unity limpa: 61/61 testes; cenas sem scripts ausentes; os 4 lutadores usam o corpo de reserva com 2 visuais cada; sons e efeitos de reserva. A build dessa cópia gerou 130,6 MB. Os 2 erros registrados vêm do pacote de renderização da Unity na primeira importação, não do código do jogo. Uma luta em rede entre duas cópias dessa build teve 21 de 21 verificações de estado iguais e nenhuma exceção.

## Rede local (03/11/2026)
**Automatizados** (`NetworkTests.cs`, 11 testes): réplica igual ao anfitrião a cada tick em partidas inteiras dos 16 confrontos; eventos entregues uma vez com 25% de perda e pacotes repetidos; toques preservados com perda e atraso; fila estável com 5% de perda; revanche; pacotes truncados e de outros programas; conexão, seleção, luta e saída por soquete real em 127.0.0.1; versão diferente, anfitrião ocupado, porta em uso, tempo limite e prazo de conexão.

**Duas cópias do executável em 127.0.0.1**, com CPUs jogando pelos dois lados (`-net-host` e `-net-join`, 170 s cada, com revanche e volta à seleção):

| Condição | Lutas | Estado (anfitrião × convidado) | Ida e volta | Falta de estado no convidado | Exceções |
|---|---:|---|---|---:|---:|
| Sem simulação | 3 | 71 de 71 verificações iguais | 17 a 34 ms | 0 | 0 |
| 5% de perda, 10 a 40 ms de atraso por sentido | 4 | 65 de 65 verificações iguais | 77 a 134 ms | 7 | 0 |

- **Queda brusca:** convidado encerrado à força na luta; o anfitrião mostrou o aviso em 9,7 s, sem exceções.
- **Comandos de teclado virtual com o editor como jogador** (Fluxos 16 e 17; a outra ponta é o executável): nos dois papéis, a partida foi encontrada ou recebida, o soco virou golpe na luta, o menu não pausou a luta e a saída encerrou o outro lado.
- **Telas de rede** (Fluxo 15): hospedar, cancelar, procurar, endereço inválido, digitar e voltar.

**Defeitos encontrados nesses testes e corrigidos:** perda de pacote esvaziava a fila do convidado e aumentava o atraso (a reprodução passou a seguir o número do tick); prazo de conexão contado desde o início do programa; texto de várias linhas cortado nas telas de rede; a comparação do teste misturava ticks de lutas diferentes.

**NÃO EXECUTADO:** dois computadores, Wi-Fi real, aviso real do firewall, controle físico pela rede, pessoas jogando e avaliação da sensação de atraso. A simulação de perda e atraso não reproduz todo o comportamento de um Wi-Fi real.

## Instalador (03/11/2026)
`Dist/UltimoRound_Setup_v0.10.0.exe`, 85,1 MB, gerado pelo menu do editor.

| Verificação | Resultado |
|---|---|
| Instalação silenciosa com atalho na área de trabalho | Código 0, sem pedir administrador, em `%LOCALAPPDATA%\Programs\Último Round` |
| Arquivos instalados | 204 (202 do jogo + 2 do desinstalador), 261,3 MB |
| Atalhos e entrada em *Aplicativos* | Criados, como "Último Round 0.10.0" |
| Abrir o jogo instalado | Luta automática até o fim, código 0, sem exceções no `Player.log` |
| Desempenho instalado × pasta `Dist` | 75,0 e 79,2 × 78,1 FPS: sem diferença clara |
| Primeira tela do assistente | Em português, com acentos corretos |
| Desinstalação silenciosa | Código 0; pasta, atalhos e registro removidos; `settings.json` idêntico |

**Atualização (versão 0.11.0):** instalar a 0.10.0 e depois a 0.11.0 por cima terminou com uma só entrada em *Aplicativos*, na versão 0.11.0, e 204 arquivos. A cópia instalada hospedou uma luta de 55 s contra a cópia da pasta `Dist`, sem exceções. A desinstalação removeu tudo e manteve o `settings.json` idêntico.

**Não executado:** clicar em todas as telas do assistente, instalar em outro computador e testar uma cópia baixada da internet.

## NÃO EXECUTADO (depende de hardware/pessoa) — roteiro humano
0. **Rede local em dois computadores, na mesma versão:**
   1. Em casa, no mesmo Wi-Fi: hospedar em um, permitir o aviso do Windows e entrar pelo outro pela lista.
   2. Repetir digitando o endereço. Depois, trocar quem hospeda.
   3. Jogar uma partida completa, com revanche e volta à seleção, avaliando o atraso de cada lado.
   4. Conectar um computador por cabo e comparar.
   5. Desligar o Wi-Fi de um deles no meio da luta: o outro deve avisar em até cerca de 10 s.
   6. Testar com o roteador do celular e, se possível, no local da apresentação.
0. **Instalador:** instalar em outro computador, sem Unity, e abrir o jogo; conferir o alerta do Windows em uma cópia baixada; clicar em todas as telas do assistente.
0. **Sensação da 0.10.0:** rolar Opções e Lista de golpes com roda de mouse real e arrastando a barra; trocar visual e lutar com cada um.
1. **Dois gamepads físicos** e **teclado + gamepad físico**: Versus Local completo, uma partida inteira cada.
2. **Desconectar/reconectar** fisicamente o controle no meio do round: deve pausar, avisar o jogador afetado e só retomar após ele apertar A.
3. **Dez partidas/revanches** seguidas observando travamentos, sons repetidos ou objetos duplicados.
4. Menus **só com controle** e **só com teclado** (incluindo Opções › Controles e remapeamento de gamepad).
5. Abrir o `.exe` do ZIP em outra máquina, conferir som, opções (resolução, modo de tela, VSync/limite de FPS) e se as preferências persistem após fechar/abrir.
6. Avaliar sensação de jogo/balanceamento dos 16 pares (os valores são hipóteses).
7. Rodar o Profiler da Unity numa build de desenvolvimento no PC-alvo para confirmar o orçamento de 16,67 ms com folga e investigar o pico máximo.
