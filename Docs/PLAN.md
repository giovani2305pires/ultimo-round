# Plano e status real (28/10/2026)

| Etapa | Critério | Status |
|---|---|---|
| A — Fundação e conexão | Projeto compila; instância correta controlável pelo MCP | **Concluída.** Unity 6000.6.7f1, URP 17.6, Input System 1.20.1, Test Framework 1.8.0; MCP (`com.unity.ai.assistant` 2.21.0-pre.2) no editor PID do projeto; prova mínima de criar/salvar/consultar objeto na cena sandbox; Git local |
| B — Combate provisório | Partida inteira sem depender de arte final | **Concluída.** Simulação 60 Hz, movimentos, pulo, ataques, defesa, vida, KO, rounds, reinício, overlay de volumes, testes |
| C — Jogo com 2 lutadores | Menu → luta → revanche; primeira build | **Concluída.** Giovani e Brutus, seleção, associação de dispositivos, combos, especiais, agarrões, energia, HUD, pausa, resultado, revanche; build Windows gerada |
| D — CPU, treino e acabamento | Controles legíveis, treino e CPU utilizáveis, sem T-pose/placeholder não declarado | **Concluída com pendência de arte.** CPU 3 dificuldades, treino completo, opções/remapeamento, arena, VFX, áudio, câmera, apresentação. Modelos/animações finais **pendentes** (placeholders declarados) |
| E — Elenco e cordas | Estilos distintos, regras com elenco inteiro | **Concluída.** Faísca e Vértice selecionáveis, 2 cores cada, rebote limitado, todos os pares testados por automação |
| F — QA e entrega | Build fora do editor, pendências declaradas | **Concluída** para o que é verificável aqui: testes, roteiros, build 0.9.0, ZIP, benchmark no executável. Itens com hardware físico: **não executados** (ver `TESTS.md`) |

## Backlog (expansões futuras — não implementadas)
- Personagens exclusivos e golpes animados sob medida, no lugar dos visuais de pacote (ver `ASSETS.md`).
- Locução em português.
- Jogo pela internet (a rede local existe desde a 0.11.0; pela internet o atraso exigiria avaliar previsão ou rollback), ranking, matchmaking.
- Campanha cinematográfica, editor de personagens, consoles, mobile.
- Dois jogadores no mesmo teclado.
- Mais arenas e seleção de cenário.
- Ajuste de balanceamento com sessões de jogo reais.
