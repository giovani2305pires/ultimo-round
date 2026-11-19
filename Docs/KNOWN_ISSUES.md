# Problemas e limitações conhecidos

## Arte e áudio
- **Os visuais vêm de pacotes genéricos e têm estilos diferentes:** há personagens texturizados ao lado de bonecos de blocos e de cor única. Os lutadores não têm aparência exclusiva. Ver `Docs/ASSETS.md`.
- **O corpo do modelo não coincide exatamente com as áreas de acerto:** os volumes da simulação não mudaram, então um braço pode atravessar o oponente ou um golpe pode acertar sem o modelo encostar. Foi visto com Brutus "Pesado" contra Giovani.
- **Golpes reaproveitam clipes dos pacotes:** o tempo do clipe é ajustado aos quadros do golpe, o que pode acelerar ou alongar o movimento.
- As armas são só decoração e não têm efeito no combate. O público ainda é feito de cápsulas.
- Sem locução em português: “Round”, “Lutem!”, K.O. e vitória usam texto + sinal sonoro sintetizado.
- O agarrão não tem animação sincronizada de corpo a corpo (atacante e alvo trocam de pose; o alvo é arremessado pela simulação).
- Não há retratos ilustrados: HUD e seleção usam selo com a inicial e as cores do lutador, além da prévia 3D na seleção.

## Jogo
- Balanceamento é hipótese inicial; nenhum ajuste fino com jogadores reais foi feito.
- A CPU é simples (utilidade com atraso de reação); não aprende nem varia muito a estratégia.
- Empates consecutivos repetem o round indefinidamente (regra do briefing); não há limite de rounds extras.
- Ao perder o foco da janela durante a luta, a partida pausa (só com jogadores humanos).
- Dois jogadores no mesmo teclado não são suportados (opcional no briefing).
- Jogo pela internet, ranking, matchmaking, campanha e editor de personagens: **não implementados** (backlog em `Docs/PLAN.md`). A simulação em passo fixo **não** foi validada para determinismo entre máquinas nem para rollback.

## Rede local
- **Só testada em um computador.** Duas cópias conversaram em 127.0.0.1, inclusive com perda e atraso simulados. **Nunca rodou em dois computadores nem em Wi-Fi real**, e o aviso real do firewall não foi visto.
- **Quem entra joga com atraso; quem hospeda, não.** Não há previsão nem rollback. Na medição, o tempo de ida e volta ficou em torno de 20 ms em um só computador, somado à fila de exibição e à latência da rede real.
- **O atraso não é igual para os dois.** Em jogo competitivo, isso dá vantagem a quem hospeda.
- **Redes que isolam aparelhos** (faculdade, empresa, convidados) impedem a conexão, mesmo com o jogo correto.
- Se a conexão cair, a luta continua por até 8 s no anfitrião, com o outro lutador parado, até aparecer o aviso.
- Se o anfitrião fechar o jogo, a partida termina; não há reconexão nem troca de anfitrião.
- A lista de partidas não funciona sem difusão na rede; nesse caso, é preciso digitar o endereço, o que exige teclado.
- Quem hospeda em um computador lento atrasa os dois jogadores.

## Técnico
- A build tem ~258 MB descompactada (112 MB em ZIP; era 130/52 MB antes dos pacotes de arte). Além da arte, os pacotes `com.unity.ai.assistant`/`com.unity.ai.inference` (usados pelo MCP do editor) incluem shaders Sentis em `Resources`. Não foram removidos porque o MCP do usuário depende deles.
- Avisos na build vêm desses shaders do pacote, não do código do jogo. O Player.log mostra que efeitos de pós-processamento não usados (profundidade de campo, Panini) foram removidos da build — esperado.
- **O instalador não tem assinatura digital.** O Windows pode alertar sobre aplicativo desconhecido, principalmente em cópias baixadas; quem instala precisa escolher "Mais informações" e "Executar assim mesmo".
- Desinstalar mantém as preferências em `%USERPROFILE%\AppData\LocalLow\Ultimo Round Dev`.
- O ZIP inclui a pasta de dados; não inclui `UltimoRound_BackUpThisFolder_ButDontShipItWithYourGame`.
- Os roteiros de verificação do editor (`DevFlowDriver`) alteram temporariamente as configurações do Input System durante a execução e restauram ao final; não fazem parte da build.
- Os avisos `TLS Allocator ... unfreed allocations` no console aparecem durante os roteiros, com `EditorApplication.Step()` dentro de `delayCall`. Não foram investigados a fundo; o `Player.log` do executável não os mostra.
- O Survivalist foi importado em parte para evitar scripts do pacote; o modelo ocupa 199 MB em `Assets`, embora só uma variação seja usada.
- No editor sem foco, o Play Mode não avança sozinho (comportamento da Unity); por isso os roteiros usam `EditorApplication.Step()`.
- Fonte Liberation Sans não tem reticências nem setas diagonais: textos usam truncamento e o histórico mostra diagonais como “↓→”.
