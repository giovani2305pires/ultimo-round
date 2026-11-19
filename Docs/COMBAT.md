# Combate — regras, dados iniciais e decisões

Valores são **hipóteses de balanceamento** (fonte: `Assets/_Game/Scripts/Data/DefaultRoster.cs`; em runtime valem os assets em `Assets/_Game/Data`).

## Simulação
- Passo fixo de **60 ticks/s** (`FightSimulation`), sem `Time`, Animator ou física da Unity. A apresentação só lê estado e eventos.
- Entrada capturada **a cada quadro de renderização** (`InputAccumulator`) e consumida **por tick**: toques curtos entre ticks não se perdem; botão segurado não repete golpe.
- Ordem de um tick: entrada → congelamento/hitstop → orientação → agarrões em andamento → estados → integração → pushbox/limites → **golpes avaliados simultaneamente** → agarrões novos → fim de combos.
- `FixedStepClock` limita picos (máx. 6 ticks por quadro). Teste `Replay_ResultadoEstavelEm30_60_120Fps` confirma o mesmo resultado em 30/60/120/144 FPS **no mesmo ambiente**. Isso **não** é determinismo entre máquinas nem prepara rollback.
- Hitstop: contador interno que congela os dois lutadores; o **cronômetro do round também pausa** nesses ticks e no congelamento da assinatura. Não se usa `Time.timeScale`.

## Convenção de frame data
`startup` = ticks antes do primeiro tick ativo (o “primeiro quadro ativo” na notação usual é `startup+1`). Vantagem na defesa = `blockstun − (active − 1 + recovery)`. Golpes fortes e especiais têm vantagem ≤ −5 (puníveis — teste `Elenco_AtaquesFortesPuniveisQuandoDefendidos`).

## Volumes
- **Hurtboxes** por postura (em pé com corpo+cabeça, agachado, no ar). **Hitbox** por golpe; durante ativo/recuperação o membro estendido também é hurtbox (permite punir golpes errados).
- **Pushbox**: largura por lutador; no ar a base sobe 0,9 m (pernas recolhidas), permitindo cruzar por cima. Quem aterrissa sobre o outro é empurrado para o lado de onde veio. Nas cordas, o lado livre absorve o empurrão (sem prender/atravessar).
- Cada execução marca os índices de hit já aplicados (`HitMask`): **um dano por hit**, mesmo tocando várias hurtboxes. Multi-hit só com `Hits`/`HitInterval` explícitos.

## Defesa
Segurar **Defesa** em estado neutro (em pé ou agachado). Médio/alto: defendidos em pé ou agachado; **baixo** exige agachado; **overhead** (inclui todos os aéreos) exige em pé; altos passam por cima de agachados pela geometria. Especiais e assinatura causam 12% de dano de raspão na defesa (nunca nocauteiam).

## Agarrão
- Curto alcance (entre bordas dos pushboxes), vence defesa. Falha contra alvo no ar, derrubado/levantando, KO, em hitstun/blockstun e por 6 ticks após sair de stun/levantar (sem agarrão garantido em combo).
- **Escape**: apertar Agarrão até 10 ticks após ser agarrado (vale também se apertado até 6 ticks antes — buffer). Ambos entram em recuperação de 16 ticks e se afastam 1,3 m. Agarrões simultâneos resultam em escape.
- Golpe e agarrão no mesmo tick: o golpe vence (quem foi atingido não agarra nem é agarrado).

## Golpes simultâneos
Os dois lados são verificados antes de aplicar: se ambos acertam no mesmo tick há **troca** (os dois recebem dano).

## Combos, limites e chão
- Cancelamentos só para golpes listados em `CancelInto`, após contato (acerto ou defesa), até 6 ticks depois do fim da janela ativa. O grafo de cancelamento é acíclico (teste).
- Redução de dano: hits 1–2 = 100%, depois −10% por hit, **piso 30%**.
- Hitstun decresce a partir do 6º hit (−1 tick/hit, mínimo 8). No **12º hit** o alvo é derrubado (forçado). Juggle: no máximo 3 acertos no ar; depois o alvo fica imune até cair.
- Derrubado (40 ticks) e levantando (22 ticks) são **invulneráveis** a golpes e agarrões: sem dano em loop no chão.
- **Rebote nas cordas**: golpe marcado (`RopeBounce`) que acerta (não na defesa, não em KO) lança o alvo em trajetória horizontal até a corda; volta com rebote curto e fica vulnerável a juggle. **No máximo 1 por combo**; repetido no mesmo combo vira derrubada normal. Indicador “REBOTE NAS CORDAS!”.

## Energia
Máx. 100. Ganho do atacante = `EnergyGain` no acerto, 50% na defesa; defensor ganha 35% ao sofrer dano. **Golpe no vazio não gera energia.** Assinatura exige barra cheia, consome tudo, pode errar ou ser defendida; congelamento curto de 24 ticks.

## Rounds
Melhor de 3, 60 s. KO = vida 0. Tempo esgotado: vence a maior **porcentagem** de vida (comparada em milésimos). **Duplo KO ou igualdade exata: empate, round repetido sem pontuar** (vida, energia, posições, buffers e flags resetados). Após o vencedor ser decidido, dano e novos golpes ficam bloqueados; eventos de encerramento não se repetem.

## Prioridade de comandos simultâneos
Assinatura > Especial > Agarrão > Forte > Chute > Soco. A direção considerada é a do **momento do aperto** (guardada no buffer de 6 ticks). Avanço/recuo rápido: dois toques para frente/trás em até 12 ticks.

## Controles
| Ação | Gamepad (Xbox) | Teclado |
|---|---|---|
| Mover | Analógico esquerdo / direcional | A / D |
| Agachar | Baixo | S |
| Pular | A ou cima | W ou Espaço |
| Soco / Chute / Forte | X / Y / B | J / K / L |
| Defesa | RT | I |
| Especial (+frente) | RB (+ frente) | U (+ frente) |
| Agarrão | LB | O |
| Assinatura | LT | P |
| Pausa | Menu/Start | Esc |
| Treino: resetar | View/Select | R ou Backspace |

Comandos por postura: ↓+Soco, ↓+Chute (**baixo**), ↓+Forte (**antiaéreo**, imune a aéreos no início), no ar: Soco/Chute/Forte (**aéreo**, overhead), Frente+Forte (golpe extra, se existir).

## Elenco (frame data inicial)
Formato: início/ativo/recuperação — dano — nível.

### Giovani — O Desafiante (kickboxing equilibrado, vida 1000)
Jab 5/2/8 — 35 — alto · Chute médio 8/3/14 — 60 — médio · Cruzado 11/3/20 — 95 — médio · ↓Soco 5/2/9 — 30 — médio · ↓Chute 8/3/16 — 50 — **baixo** · Uppercut 7/4/22 — 85 — antiaéreo, lança · Chute voador 6/6/8 — 60 — overhead · Clinch e joelhada (agarrão) 110 · **Chute giratório** (Especial) 14/3/22 — 110 — derruba, rebote · **Avanço Desafiante** (Especial+frente) 10/5/18 — 90 — avança · **Último Round** (assinatura) 4 hits × 70.
Combos: *Pressão Desafiante* Soco → Chute → Especial+frente; *Rasteiro* ↓Soco → ↓Chute.
Fraqueza: sem ferramentas extremas; especiais puníveis.

### Brutus — O Brigador (briga pesada, vida 1150)
Direto pesado 7/3/11 — 50 · Pisão frontal 10/3/16 — 75 · Gancho de ferro 14/4/24 — 125, derruba · **Marretada** (Frente+Forte) 19/3/20 — 90 — **overhead** · ↓Soco 7/3/12 — 45 · Rasteira bruta 12/4/24 — 70 — baixo, derruba · Cabeçada ascendente 9/5/24 — 100 — antiaéreo · Cotovelada aérea 8/6/10 — 80 · **Suplex** (agarrão, alcance maior, troca de lado) 165 · **Abraço de Urso** (Especial, agarrão) 190, recuperação enorme ao errar · **Investida de ombro** (Especial+frente) 15/6/24 — 110 — derruba, rebote · **Terremoto** (assinatura) 260.
Combos: *Rolo Compressor* Soco → Chute → Especial+frente; *Varredura* ↓Soco → ↓Chute.
Fraqueza: lento, corpo grande, recuperação longa.

### Faísca — A Relâmpago (pressão ágil, vida 900)
Jab relâmpago 4/2/7 — 25 · Chute rápido 6/3/10 — 40 · Cotovelada giratória 9/3/17 — 70 · ↓Soco 4/2/8 — 22 · Rasteira veloz 7/3/14 — 40 — baixo · Mortal ascendente 6/5/20 — 70 — antiaéreo · Joelhada aérea 5/6/6 — 45 · Chave de braço (agarrão, troca de lado) 100 · **Rajada de socos** (Especial) 4 hits × 22 · **Faísca rasante** (Especial+frente) 9/8/18 — 65 — **baixo**, derruba · **Curto-circuito** (assinatura) 6 hits × 45.
Combos: *Choque em cadeia* Soco → Chute → Especial; *Curto e rasteiro* ↓Soco → ↓Chute → Especial+frente.
Fraqueza: pouca vida, dano baixo por golpe, alcance curto.

### Vértice — A Lâmina (especialista em chutes, vida 980)
Jab de contenção 6/2/10 — 30 · Chute frontal longo 9/3/16 — 65 · Chute alto giratório 13/3/24 — 105 — **alto**, derruba, rebote · **Machado** (Frente+Forte) 21/3/22 — 95 — **overhead** · Chute agachado curto 6/2/10 — 30 · Chute rasteiro longo 10/3/20 — 55 — baixo · Chute meia-lua 8/5/24 — 85 — antiaéreo · Voadora 7/7/8 — 65 · Tesoura (agarrão, troca de lado) 105 · **Leque** (Especial) 15/4/26 — 100 — alcance enorme, derruba, rebote · **Passo cortante** (Especial+frente) 12/4/20 — 80 · **Vórtice** (assinatura) 3 hits × 90.
Combos: *Corte de distância* ↓Soco → ↓Chute → Especial+frente; *Leque final* Soco → Chute → Especial.
Fraqueza: socos lentos/fracos de perto; chutes longos com recuperação grande e membro estendido punível.

## CPU
Utilidade simples (`CpuBrain`): percebe posição/estado/golpe visível do oponente com atraso de reação; nunca lê a entrada do jogador nem recebe dano extra; usa o mesmo `InputFrame`. Não age fora da fase de luta.
| Dificuldade | Reação | Chance de defender | Antiaéreo | Punição | Continua combo | Erro |
|---|---|---|---|---|---|---|
| Fácil | 26 ticks | 25% | 15% | 20% | 25% | 30% |
| Normal | 16 ticks | 55% | 45% | 55% | 60% | 12% |
| Difícil | 10 ticks | 78% | 70% | 80% | 85% | 5% |

## Treino
Boneco parado, defendendo (escolhe a altura correta da defesa — ajuda de treino), agachado ou CPU; vida infinita (reposta 40 ticks após o fim do combo; o dano nunca nocauteia), energia infinita opcional, cronômetro desligado, reset (R/Backspace ou View/Select), histórico de comandos, contador de combo/dano, lista de golpes com demonstração dos combos sugeridos (o boneco fica parado durante a demonstração) e overlay de volumes/estado/tick/fases. Essas opções só existem no modo Treino.

## Rede local
Dois computadores na mesma rede, por UDP (porta 47777), sem servidor nem pacote externo. **A luta roda só no anfitrião**, que é sempre o lutador da esquerda.

| Dado | Sentido | Como chega |
|---|---|---|
| Comandos (`InputFrame`) | Convidado → anfitrião, por tick | Cada pacote repete os 8 últimos; atraso acumulado é encurtado juntando os toques, para nenhum se perder |
| Estado da partida | Anfitrião → convidado, por tick | Só o mais novo importa; pacote perdido é substituído pelo seguinte |
| Eventos (golpes, sons, efeitos) | Anfitrião → convidado | Numerados e reenviados até a confirmação; aplicados uma vez, na ordem |
| Seleção e fluxo | Nos dois sentidos, 10 vezes por segundo | Estado completo repetido |

- **O convidado não simula.** Ele aplica o estado em uma réplica e a apresentação funciona como em uma luta local. Por isso a rede não depende de determinismo entre máquinas.
- **Fila do convidado:** exibe de 1 a 6 ticks de atraso. Cresce quando falta estado e encolhe após 10 s estáveis.
- **Sem pausa:** em rede o menu não para a luta; o lutador de quem abriu o menu fica sem comandos.
- **Queda:** a mensagem "Bye" encerra na hora; sem ela, a conexão cai após 8 s de silêncio na luta e 20 s fora dela (a carga da cena trava o laço por alguns segundos).
- **Versões diferentes** são recusadas na entrada. Revanche e volta à seleção são decisão do anfitrião.
- **Sem previsão nem rollback:** quem hospeda joga sem atraso e quem entra sente o tempo de ida e volta somado à fila. Essa é a principal limitação do modelo.

