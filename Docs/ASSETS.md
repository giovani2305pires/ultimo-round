# Assets — origem, licença e uso

## Pacotes externos (importados pelo usuário via Package Manager / Asset Store)
Fonte comum: Unity Asset Store, conta do usuário; cópia local em `%APPDATA%\Unity\Asset Store-5.x\`. Autor = publicador na loja.

| Pacote | Autor | Pasta | Licença | Como é usado |
|---|---|---|---|---|
| Low Poly Fighter Starter Pack | tt-3D | `Assets/tt-3d` | Termos da Asset Store (o pacote não traz licença própria) | 5 visuais: o principal de cada lutador e a "Lutadora livre" da Faísca |
| Fighter Pack Bundle FREE | Explosive | `Assets/ExplosiveLLC` | Termos da Asset Store | 4 visuais (Pugilista, Pesado, Atleta, Berserker) e clipes de guarda, soco e andar |
| Fighting Motions Vol.1 | Magicpot Inc | `Assets/FightingMotionsVolume1` | Termos da Asset Store | 2 visuais (Azul, Vermelho) e a base dos golpes, defesas, reações e queda |
| Survivalist character | Slayver | `Assets/Survivalist` | Termos da Asset Store | Visual "Sobrevivente" do Vértice e clipes de pulo/corrida do Starter Assets incluso. Importação **parcial** (sem os scripts do pacote) |
| Zombie Monster Animations FREE | Kevin Iglesias | `Assets/Kevin Iglesias` | Termos da Asset Store | Visual "Zumbi" do Brutus, com clipes próprios de dano, morte e ataque |
| Free Deadly Kombat | Daniel SoundsGood | `Assets/Deadly Kombat Free version` | Termos da Asset Store | Os 50 sons, no banco `Assets/_Game/Data/SoundBank.asset` |
| Free Quick Effects Vol.1 | Gabriel Aguiar Prod | `Assets/GabrielAguiarProductions` | Termos da Asset Store | 13 dos 30 efeitos (lista abaixo) |
| Military Base Pack | Tiny Teacup Studio (na loja: Coil Studio) | `Assets/Tiny Teacup Studio` | `LICENSE.txt` do pacote: uso comercial permitido; **proibido redistribuir os assets, mesmo modificados**; crédito opcional | Cenário ao redor do ringue |
| Low Poly Weapons VOL.1 | Simon Mercuzot | `Assets/Low Poly Weapons VOL.1` | Termos da Asset Store | Só decoração: mesas ao lado do ringue e equipamento do "Sobrevivente". Nenhuma arma tem efeito no combate |

> As licenças marcadas como "Termos da Asset Store" não foram lidas na página de cada produto: valem os termos gerais da loja, que permitem incluir os assets em um jogo publicado, mas não redistribuir os arquivos-fonte. Confirme na página do pacote antes de publicar.

### Repositório público
**Não publique os arquivos desses pacotes**: isso seria redistribuir os assets. O `.gitignore` já exclui as pastas dos pacotes e os arquivos gerados a partir deles:
- `Art/Materials/Convertidos`
- `Prefabs/Lutadores`
- `Data/Visuais`
- `Data/SoundBank.asset`

O repositório público tem só o código, os dados próprios, as cenas e a documentação. Sem os pacotes, o jogo roda com as reservas feitas por código: corpo geométrico, sons sintetizados e partículas simples. A versão completa é distribuída já compilada, na página Releases. O `LICENSE.txt` do Military Base Pack permite distribuir o jogo pronto.

### O que não foi usado
- **Efeitos (17 de 30):** Flamethrower_01/02, Heal_02, Hyperdrive_01, Lightning_02, LootDrop_01/02, MeteorRain_01/02, MuzzleFlash_01/02, Portal_01/02, Projectile_01/02, Rain_01/02.
- **Armas:** acessórios soltos (ANPEQ15, ELCAN, Flash, Smoke, SR_Scope_00, TAN_LR_Scope_01).
- **Cenário:** `Toilet` (ficaria sempre fora do enquadramento).
- **Survivalist:** as variações além da (1) e os scripts do pacote.

### Efeitos em uso
| Situação | Efeito |
|---|---|
| Golpe leve / pesado / defesa | `vfx_Sparks_01` / `vfx_Impact_01` / `vfx_Shield_01` |
| Poeira de queda e avanço | `vfx_Smoke_01` (clareada por código) |
| Escape de agarrão | `vfx_Shockwave_01` |
| Especial de Brutus / Giovani / Faísca / Vértice | `vfx_Explosion_01` / `vfx_Flames_01` / `vfx_Lightning_01` / `vfx_Tornado_01` |
| Assinatura | `vfx_Electricity_01` + `vfx_Implosion_01` |
| Nocaute / vitória | `vfx_Explosion_02` / `vfx_Heal_01` |

Os prefabs originais não são alterados: `VfxPool` encurta duração e vida das cópias em tempo de execução e desliga os halos pretos.

## Cópias e derivados gerados pelo projeto
Gerados por `Assets/_Game/Editor/AssetIntegrator.cs` (menu **Último Round › Assets**). Os arquivos originais dos pacotes não são editados, com uma exceção: a etapa 1 altera as **configurações de importação** dos clipes (raiz fixa).
- `Assets/_Game/Art/Materials/Convertidos`: cópias URP dos materiais.
- `Assets/_Game/Prefabs/Lutadores`: 13 visuais com altura normalizada e sem componentes do pacote.
- `Assets/_Game/Data/Visuais`: clipes de cada lutador.

## Próprios do projeto ou da Unity
| Asset | Origem | Autor | Licença | Uso |
|---|---|---|---|---|
| Liberation Sans (+ SDF do TextMesh Pro) | TMP Essential Resources (`com.unity.ugui` 2.6.0) | Red Hat / Liberation Fonts | SIL Open Font License 1.1 (`Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`) | Interface e textos 3D |
| Template URP (`Assets/Settings`) | Unity 6000.6.7f1 | Unity | Termos do Unity Editor | Pipeline gráfico |
| Ringue, arquibancadas, público, telão | Código (`ArenaBuilder.cs`) | Projeto | Próprio | Arte provisória estilizada; o público ainda é feito de cápsulas |
| Músicas e sons de interface; reserva dos sons de luta | Síntese (`SoundSynth.cs`) | Projeto | Próprio | Música e interface; golpes só se o banco faltar |
| Corpo e poses de reserva | Código (`ProceduralBody.cs`, `PoseLibrary.cs`) | Projeto | Próprio | Só se os dados de visual faltarem |

| Instalador (`Dist/UltimoRound_Setup_v<versão>.exe`) | Inno Setup 6.7.3 | Jordan Russell e Martijn Laan | Inno Setup License: uso permitido, inclusive comercial | Instalação no Windows; ferramenta instalada à parte, fora do repositório |

Nenhum asset de franquia comercial foi copiado.

## Pendências de arte e áudio
1. **Personagens exclusivos:** os visuais são de pacotes genéricos, então outros jogos podem ter os mesmos modelos. Giovani, Brutus, Faísca e Vértice não têm aparência própria.
2. **Golpes por personagem:** especiais e assinaturas reaproveitam clipes dos pacotes; não há animação feita sob medida.
3. **Agarrão sincronizado:** atacante e alvo tocam clipes independentes.
4. **Locução em português:** continua como texto na tela + sinal sonoro.
