# GDD — Último Round: Torneio dos Rivais

| | |
|---|---|
| **Autor** | Giovani Nogueira Pires |
| **Disciplina** | Desenvolvimento de Jogos Digitais — Prof. Luis Filipe Grael Tinós |
| **Versão do jogo** | 1.0.0 |
| **Engine** | Unity 6000.6.7f1 (URP) |
| **Plataforma** | Windows 10/11, 64 bits |
| **Gênero** | Luta 1 contra 1, 2,5D |
| **Jogadores** | 1 (contra a CPU ou no treino) ou 2 (mesmo computador ou rede local) |
| **Classificação sugerida** | 12 anos (luta sem sangue) |

---

## 1. Visão geral

### Conceito
*Último Round* é um jogo de luta curto e direto: dois lutadores se enfrentam em um ringue, e vence quem ganhar dois rounds. Cada lutador tem um estilo próprio e uma fraqueza clara, para que ler o adversário importe tanto quanto apertar botões.

### Frase de venda
"Quatro rivais, um ringue e três rounds para provar quem é o melhor."

### Pilares de design
1. **Leitura e reação:** ataque, defesa e agarrão formam um "pedra, papel e tesoura". Cada escolha tem resposta.
2. **Clareza:** golpes legíveis, impacto forte (congelamento, som, efeito e tremor de câmera) e HUD simples.
3. **Partidas curtas:** um round dura até 60 segundos, e uma partida inteira leva poucos minutos.
4. **Justiça:** a CPU não trapaceia, combos têm limite e quem está no chão não apanha.

### Público
Jogadores casuais e iniciantes no gênero, de 12 anos em diante. O modo Treino e o lutador equilibrado (Giovani) servem de porta de entrada.

### Referências
Clássicos de luta em 2D e 2,5D, nos quais a câmera é lateral, os personagens são 3D e o combate acontece em um plano.

---

## 2. Escopo

**Dentro do escopo (entregue na versão 1.0.0):**
- 4 lutadores com 13 visuais
- 4 modos: Versus Local, Rede Local, Contra CPU (3 dificuldades) e Treino
- Uma arena
- Menus, opções, remapeamento de controles e instalador

**Fora do escopo:**
- Jogo pela internet e ranking
- Modo história
- Mais arenas
- Personagens com arte exclusiva
- Narração em português

---

## 3. Jogabilidade

### Ciclo principal

```text
Escolher modo e lutador → Round (até 60 s) → Nocaute ou fim do tempo → Próximo round
        ↑                                                                    ↓
   Revanche ou trocar de lutador ←───────── Vencedor de 2 rounds ←───────────┘
```

Dentro do round, o ciclo é: **aproximar → atacar ou esperar o erro do outro → acertar e continuar em combo → afastar ou pressionar**.

### Condições de vitória e derrota

| Situação | Resultado |
|---|---|
| Vida do adversário chega a 0 | Nocaute: vence o round |
| Tempo acaba | Vence quem tem a maior porcentagem de vida |
| Duplo nocaute ou vida igual no fim do tempo | Empate: o round é repetido |
| 2 rounds vencidos | Vence a partida |

### Ações do lutador

| Ação | Descrição |
|---|---|
| Andar, agachar, pular | Movimentação básica. Pulo tem impulso para frente ou para trás |
| Avanço e recuo | Toque duplo para frente ou para trás |
| Soco, Chute, Forte | Golpes básicos, do mais rápido ao mais forte |
| Golpes por postura | Agachado: soco baixo, rasteira e antiaéreo. No ar: golpe aéreo |
| Defesa | Segurar o botão. Em pé defende golpes altos, médios e aéreos; agachado, médios e baixos |
| Agarrão | Curto alcance; vence a defesa. Pode ser escapado apertando Agarrão logo depois |
| Especiais | Dois por lutador (neutro e com frente) |
| Assinatura | Golpe mais forte, que exige a barra de energia cheia |

### Sistemas

| Sistema | Regra |
|---|---|
| **Combos** | Alguns golpes podem ser cancelados em outros depois de acertar. A partir do 3º acerto, o dano cai 10% por golpe, até o mínimo de 30%. No 12º acerto, o adversário é derrubado à força |
| **Energia** | Sobe ao acertar e ao defender. Também sobe para quem apanha. Golpe no vazio não gera energia |
| **Congelamento no impacto** | O jogo para por alguns quadros a cada acerto, para dar peso ao golpe. O cronômetro para junto |
| **Rebote nas cordas** | Alguns golpes lançam o adversário contra a corda; ele volta vulnerável. Só uma vez por combo |
| **Chão** | Quem cai fica invulnerável até levantar, então não existe dano repetido no chão |

### Estados do lutador

```text
Parado ⇄ Andando ⇄ Agachado ⇄ Defendendo
   ↓            ↓                ↓
 Ataque      Pulo / Ar      Atordoado na defesa
   ↓
Acerto no adversário → Atordoado / Lançado / Rebote nas cordas / Derrubado → Levantando
                                                    ↓
                                               Nocaute → Vitória do outro
```

São 21 estados no total (`FighterStateId`), entre eles Agarrando, Agarrado e Escape de agarrão. A partida tem as fases **Pré-round → Luta → Fim do round → Fim da partida**.

---

## 4. Personagens

| Lutador | Título | Estilo | Vida | Alcance | Velocidade | Força | Fraqueza |
|---|---|---|---:|---:|---:|---:|---|
| **Giovani** | O Desafiante | Kickboxing equilibrado | 1000 | 3 | 3 | 3 | Sem ferramentas extremas; especiais puníveis |
| **Brutus** | O Brigador | Briga pesada | 1150 | 3 | 1 | 5 | Lento e com recuperação longa |
| **Faísca** | A Relâmpago | Pressão ágil | 900 | 2 | 5 | 2 | Pouca vida e alcance curto |
| **Vértice** | A Lâmina | Especialista em chutes | 980 | 5 | 3 | 3 | Fraco de perto; chutes longos são puníveis |

Cada lutador tem:
- 7 ou 8 golpes normais;
- um agarrão;
- 2 especiais;
- um golpe de assinatura;
- 2 combos sugeridos.

A ficha completa, com os tempos de cada golpe, está em [COMBAT.md](COMBAT.md). Cada lutador também tem de 3 a 4 visuais, trocados na tela de seleção.

### Exemplos de identidade
- **Giovani:** *Chute giratório* derruba e faz rebote nas cordas; *Último Round* é uma sequência de 4 golpes.
- **Brutus:** *Abraço de Urso* é um agarrão especial devastador, mas fica muito exposto se errar.
- **Faísca:** *Faísca rasante* é um avanço baixo que derruba; *Rajada de socos* acerta 4 vezes.
- **Vértice:** *Leque* alcança quase meio ringue; *Machado* é um chute de cima que pega quem defende agachado.

---

## 5. Modos de jogo

| Modo | Descrição |
|---|---|
| **Versus Local** | Dois jogadores no mesmo computador, cada um com um dispositivo |
| **Rede Local** | Dois computadores na mesma rede. Quem hospeda roda a luta; o outro envia comandos e recebe o resultado |
| **Contra CPU** | Fácil, Normal ou Difícil |
| **Treino** | Boneco parado, defendendo, agachado ou controlado pela CPU; vida e energia infinitas; histórico de comandos; caixas de colisão visíveis; demonstração de combos |

### Inteligência artificial
A CPU escolhe ações por pontuação de utilidade (atacar, defender, punir, antiaéreo, aproximar). Ela vê o adversário com atraso de reação, não lê os botões do jogador e usa os mesmos comandos de um humano.

| Dificuldade | Reação | Chance de defender | Chance de erro |
|---|---:|---:|---:|
| Fácil | 26 ticks | 25% | 30% |
| Normal | 16 ticks | 55% | 12% |
| Difícil | 10 ticks | 78% | 5% |

---

## 6. Controles

| Ação | Teclado | Controle |
|---|---|---|
| Mover / agachar / pular | A, D / S / W ou Espaço | Analógico ou direcional / baixo / A |
| Soco / Chute / Forte | J / K / L | X / Y / B |
| Defesa | I | RT |
| Especial | U | RB |
| Agarrão | O | LB |
| Assinatura | P | LT |
| Pausa | Esc | Start |

Todos os botões podem ser remapeados, e a configuração fica salva. Cada jogador usa um dispositivo. Se um controle desconectar, a luta pausa até ele voltar.

---

## 7. Interface e feedback

| Tela | Conteúdo |
|---|---|
| Abertura | Logo e "pressione qualquer botão" |
| Menu principal | Modos, Opções, Créditos e Sair |
| Associar controles | Cada jogador escolhe o próprio dispositivo |
| Seleção | Cartões dos lutadores, ficha com atributos, fraqueza, prévia 3D e troca de visual |
| Confronto | Os dois lutadores antes da luta |
| HUD | Vida com marca do dano recente, energia, cronômetro, rounds vencidos e contador de combo |
| Pausa | Lista de golpes, opções e sair |
| Resultado | Vencedor, placar, revanche, voltar à seleção ou ao menu |

**Feedback de impacto:** congelamento de quadros, tremor do lutador e da câmera, brilho no corpo, efeitos de partículas, som variado por tipo de golpe, avisos ("REBOTE NAS CORDAS!", "ESCAPOU!") e reação do público.

---

## 8. Arte e áudio

- **Estilo:** 3D low poly e semirrealista, com câmera lateral e iluminação noturna de ringue.
- **Arena:** ringue com cordas animadas, arquibancadas com público, telão e uma base militar ao fundo (hangares, torres, contêineres e mesas com armas decorativas).
- **Personagens e animações:** pacotes gratuitos da Unity Asset Store. As animações tocam em sincronia com o tempo de cada golpe definido nas regras.
- **Sons:** 50 sons de luta de pacote. Músicas e sons de interface são sintetizados por código.
- **Efeitos:** 13 efeitos de partículas, entre eles um efeito de especial próprio de cada lutador.

Origem e licença de cada asset em [ASSETS.md](ASSETS.md).

---

## 9. Tecnologia

| Tema | Decisão |
|---|---|
| Engine | Unity 6, Universal Render Pipeline |
| Regras da luta | Simulação própria em passo fixo de 60 ticks por segundo, sem física nem animação da Unity |
| Movimento e colisão | Velocidade, gravidade e caixas retangulares (ataque, dano e empurrão) calculadas na simulação; o `Transform` só exibe |
| Animação | `PlayableGraph` com o tempo de cada clipe calculado a partir do tick da simulação |
| Entrada | Input System, captura a cada quadro e consumo por tick (toques curtos não se perdem) |
| Interface | uGUI e TextMeshPro, montados por código |
| Rede | UDP, sem servidor; a luta roda no anfitrião e o convidado aplica o estado recebido |
| Qualidade | 61 testes automáticos e roteiros que jogam sozinhos para validar menus e lutas |
| Distribuição | Executável Windows, ZIP e instalador (Inno Setup) |

**Por que separar regra e visual:** assim as regras são testáveis sem abrir o jogo, o resultado não muda com a taxa de quadros, a arte pôde ser trocada inteira sem mexer no combate e a rede local ficou simples (só o anfitrião decide).

---

## 10. Requisitos da disciplina

| Requisito do professor | Atendido por |
|---|---|
| Controle de entradas (teclado, mouse, controle) | Teclado e controle na luta; teclado, controle e mouse nos menus; remapeamento |
| Movimentação de personagens | Andar, pular, avançar, recuar, ser lançado e arremessado |
| Transform e/ou física | Física própria em passo fixo; `Transform` dos modelos atualizado a partir dela |
| Colisões e interações | Caixas de ataque, dano e empurrão; limites do ringue; agarrões; rebote nas cordas |
| Pontuação, vidas, vitória e derrota | Vida, energia, rounds, nocaute, fim do tempo, empate e placar |
| Controle de estados | 21 estados do lutador, fases da partida, fluxo de telas e CPU |
| Interface e feedback | HUD, menus, efeitos, sons e câmera |
| Jogo completo e funcional | Do menu à revanche; executável e instalador |

---

## 11. Histórico de desenvolvimento

| Versão | Entrega |
|---|---|
| Etapas A–C | Ambiente, regras da luta com testes e primeira versão jogável com 2 lutadores |
| Etapa D | CPU, Treino, opções e remapeamento |
| Etapa E | Faísca e Vértice; rebote nas cordas |
| Etapa F (0.9.0) | Testes, build, medição de desempenho e documentação |
| 0.10.0 | Modelos, animações, sons, efeitos e cenário dos pacotes; correção da rolagem dos menus; instalador |
| 0.11.0 | Rede local |
| 1.0.0 | Versão de entrega da disciplina |

O desenvolvimento usou apoio de inteligência artificial (Claude Code), permitido pelo professor.
