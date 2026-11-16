# Warband

**[English](#english) · [Português](#português)**

---

## English

A guild for you and your friends. Once your friends are in it they stay in it: when anyone comes back to the game, the guild is together again with no new invite. You see each other's health, stamina, eitr, active effects and weapon in a HUD you can move, resize and reorder. Allies behind walls, trees or hills show as a coloured silhouette, and each one has their own coloured marker with their initial on the Nav Compass bar.

### Features

- **Client-side.** The server needs nothing. **Everyone in the guild needs the mod, on the same version.**
- **Invite:** look at a player and press **J**, or use the guild panel. Only the leader and officers can invite (the first invite founds the guild and you lead it).
- **Guild panel (Shift+P):**
  - **Players**: everyone on the server, with *Invite* and *Add friend*.
  - **Friends**: your friends list (kept on your computer). Add by name, see who is online, invite them. Optionally accept invites from friends on their own.
  - **Guild**: all members, online and offline, with their role (leader, officer, member). The leader can promote or demote officers, remove anyone, hand over the lead and rename the guild; officers can remove plain members, even offline ones. Leave the guild. Quick switches for the HUD, silhouette and compass. Remove, Make leader and Leave ask for a second click.
- **Accept / decline** an invite with **Y / N**. Invites expire after 30 seconds. A guild member who comes online is recognised by their platform id: no invite is needed again.
- **Guild HUD:**
  - A health bar with current / max in the middle, a stamina bar, and an eitr bar for allies who have eitr.
  - Active effects (rested, wet, meads, set bonuses...) with the time left.
  - The weapon in the ally's hand, the distance, and their colour with their initial.
  - Drawn on the same background as the game's windows; the background opacity is adjustable.
  - When an ally dies, their part of the HUD shakes until they respawn.
  - **F8** = edit mode: drag the bar on top to move it, drag an ally to change the order, scroll to resize, F8 again to finish.
- **Silhouette:** only the hidden parts of an ally are coloured; what you see directly stays as it is. Fill or outline style; colour, opacity and maximum distance can be set.
- **Compass:** with [Nav Compass](https://thunderstore.io/c/valheim/p/Ab5oluteZer0/NavCompass/) installed, each ally shows on the compass bar as a circle in their colour with their initial. Can be switched off, with a maximum distance and icon size.
- **Guild chat:** type `/g message` (`/p` and `/party` work too) in the normal chat. Only your online guild members see it, with a [Guild] tag and your colour; it also shows under your part of their guild HUD for a few seconds.
  - **Chat tabs:** *All* and *Guild* buttons above the chat. In the Guild tab you only see guild messages and what you type goes to the guild. `/g` (or `/p`) alone opens the Guild tab; `/all` (or `/a`) goes back to normal chat; `/all message` goes back and sends that message to everyone.
- **Find guild members on the map:** a button for each online ally on the large map. Click a name and the map zooms out, glides to that ally and zooms back in, with a ring and their name in the middle, even if they hide from the map. A greyed name (?) means their position isn't known yet.
- **Send items:** with your bag open, hover an item and press **P**, then pick a guild member nearby (30 m). They get a **Y / N** prompt, and the item leaves your bag only once they accept. A full bag declines on its own.
- **The guild is saved** in `BepInEx/config/Warband/guild.txt` on each member's computer. Members who were offline catch up on changes (new members, removals, new roles) the next time they meet someone from the guild. Leaders and officers can edit while the leader is offline; if two edits clash, the newer one of each member wins. Guilds made with Warband 1.x parties are not carried over.

### Requirements

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- Optional: [Nav Compass](https://thunderstore.io/c/valheim/p/Ab5oluteZer0/NavCompass/) for the compass markers.

### Installation

**r2modman / Thunderstore Mod Manager:** click *Install*. To install a downloaded zip, go to Settings → *Import local mod*.

**Nexus Mods / Vortex:** also on [Nexus Mods](https://www.nexusmods.com/valheim/mods/4900) (named **Shieldmates** there).

**Manual:**
1. Extract the zip.
2. Copy the `plugins/Warband` folder into `Valheim/BepInEx/plugins/`.
   Result: `Valheim/BepInEx/plugins/Warband/Warband.dll`.
3. Launch the game.

### Configuration

Press **F1** in game (requires Configuration Manager) or edit `BepInEx/config/felih.Warband.cfg`.

| Option | Default | What it does |
|---|---|---|
| PanelKey | Shift+P | Opens the guild panel. |
| InviteKey | J | Invites the player you are looking at. |
| AcceptKey / DeclineKey | Y / N | Answer a guild invite. |
| EditHudKey | F8 | Move, resize and reorder the guild HUD. |
| SendItemKey | P | With your bag open, send the item under the mouse to a guild member. |
| SendItemRange | 30 m | How close a guild member must be to send them an item. |
| AutoAcceptFriends | off | Accept invites from friends on their own. |
| MaxMembers | 10 | Most players in one guild. |
| HUD Enabled | on | The guild HUD. |
| Position / Scale | | Where the HUD is and how big (easier with F8). |
| ShowStamina / ShowEitr / ShowBuffs / ShowWeapon / ShowDistance | on | Parts of the HUD. |
| MaxBuffs | 8 | Most effect icons per ally. |
| BackgroundOpacity | 0.85 | Opacity of the HUD background (0 = none). |
| Silhouette Enabled | on | Silhouette of allies behind objects. |
| Silhouette Color / Opacity | yellow / 0.5 | Its colour and opacity. |
| Silhouette MaxDistance | 80 m | Farther allies get no silhouette. |
| Silhouette Style | Fill | Fill = hidden parts filled, Outline = only their edge, AlwaysOnTop = the whole ally always. |
| OutlineWidth | 3 | Outline thickness in pixels. |
| VisibleMargin | 15 cm | Tolerance for parts seen directly. Raise it if feet touching the ground get coloured; lower it if parts just behind a wall don't show. |
| TestOnYourself | off | Draws the silhouette on your own character too, to try it alone. |
| Compass Enabled | on | Allies on the Nav Compass bar. |
| Compass MaxDistance | 0 | Farther allies are not shown (0 = always). |
| Compass IconSize | 20 | Size of the ally circle. |
| Chat Color | light blue | Colour of guild chat messages. |
| Chat ShowInHud / HudSeconds | on / 10 s | Last message under each ally in the HUD, and for how long. |
| FindPartyButton | on | The ally buttons on the large map. |
| ButtonPosition | -30, 80 | Where the button sits (negative = from the right / bottom edge). |
| ZoomOnAlly / FlightTime | 0.035 / 1 | How close the map zooms in on the ally, and how long the trip takes. |
| DetailedLog | off | Writes details to the BepInEx log (for bug reports). |

### Notes

- If you use **BetterUI**, turn off its party (`zenox.betterui.cfg` → `[Party]` → `Enabled = false`) so you don't get two party HUDs and two uses of J.
- Players without Warband can't be invited (the invite just goes unanswered).

---

## Português

Uma guilda para você e seus amigos. Quem entra na guilda continua nela: quando alguém volta ao jogo, a guilda se junta de novo sem novo convite. Vocês veem a vida, a stamina, o eitr, os efeitos ativos e a arma uns dos outros num HUD que dá para mover, redimensionar e reordenar. Aliados atrás de paredes, árvores ou morros aparecem como uma silhueta colorida, e cada um tem seu próprio marcador colorido com a inicial na barra do Nav Compass.

### Recursos

- **Client-side.** O servidor não precisa de nada. **Todos da guilda precisam do mod, na mesma versão.**
- **Convidar:** olhe para um jogador e aperte **J**, ou use o painel da guilda. Só o líder e os oficiais convidam (o primeiro convite funda a guilda e você a lidera).
- **Painel da guilda (Shift+P):**
  - **Players**: todos no servidor, com *Invite* (convidar) e *Add friend* (adicionar amigo).
  - **Friends**: sua lista de amigos (salva no seu computador). Adicione pelo nome, veja quem está online e convide. Opcionalmente aceita sozinho os convites de amigos.
  - **Guild**: todos os membros, online e offline, com o cargo (líder, oficial, membro). O líder promove ou rebaixa oficiais, remove qualquer um, passa a liderança e renomeia a guilda; oficiais removem membros comuns, mesmo offline. Sair da guilda. Atalhos para ligar e desligar o HUD, a silhueta e a bússola. Remover, Passar liderança e Sair pedem um segundo clique.
- **Aceitar / recusar** um convite com **Y / N**. Convites expiram em 30 segundos. Quem é da guilda é reconhecido pelo id da plataforma ao ficar online: não precisa de novo convite.
- **HUD da guilda:**
  - Barra de vida com atual / total no meio, barra de stamina e barra de eitr para quem tem eitr.
  - Efeitos ativos (descansado, molhado, hidromel, bônus de conjunto...) com o tempo restante.
  - A arma na mão do aliado, a distância, e a cor dele com a inicial.
  - Desenhado com o mesmo fundo das janelas do jogo; a opacidade do fundo é ajustável.
  - Quando um aliado morre, a parte dele no HUD treme até ele renascer.
  - **F8** = modo de edição: arraste a barra de cima para mover, arraste um aliado para mudar a ordem, use a rodinha para mudar o tamanho, F8 de novo para terminar.
- **Silhueta:** só as partes escondidas do aliado ficam coloridas; o que você vê diretamente fica normal. Estilo preenchido ou contorno; cor, opacidade e distância máxima configuráveis.
- **Bússola:** com o [Nav Compass](https://thunderstore.io/c/valheim/p/Ab5oluteZer0/NavCompass/) instalado, cada aliado aparece na barra da bússola como um círculo na cor dele com a inicial. Dá para desligar, com distância máxima e tamanho do ícone.
- **Chat da guilda:** digite `/g mensagem` (`/p` e `/party` também funcionam) no chat normal. Só os membros da guilda que estão online veem, com a tag [Guild] e a sua cor; também aparece embaixo do seu bloco no HUD deles por alguns segundos.
  - **Abas do chat:** botões *All* e *Guild* acima do chat. Na aba Guild você só vê as mensagens da guilda e o que digita vai para ela. `/g` (ou `/p`) sozinho abre a aba Guild; `/all` (ou `/a`) volta ao chat normal; `/all mensagem` volta e manda a mensagem para todos.
- **Encontrar membros da guilda no mapa:** um botão para cada aliado online no mapa grande. Clique num nome e o mapa se afasta, desliza até esse aliado e aproxima de novo, com um anel e o nome dele no meio, mesmo se ele estiver escondido no mapa. Um nome cinza (?) quer dizer que a posição dele ainda não é conhecida.
- **Enviar itens:** com a bolsa aberta, passe o mouse num item e aperte **P**, depois escolha um membro da guilda por perto (30 m). Ele recebe um pedido **Y / N**, e o item só sai da sua bolsa quando ele aceita. Bolsa cheia recusa sozinha.
- **A guilda fica salva** em `BepInEx/config/Warband/guild.txt` no computador de cada membro. Quem estava offline recebe as mudanças (novos membros, remoções, novos cargos) quando encontra alguém da guilda. Líder e oficiais editam mesmo com o líder offline; se duas edições se chocam, vale a mais nova de cada membro. Parties da versão 1.x não são migradas.

### Requisitos

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- Opcional: [Nav Compass](https://thunderstore.io/c/valheim/p/Ab5oluteZer0/NavCompass/) para os marcadores na bússola.

### Instalação

**r2modman / Thunderstore Mod Manager:** clique em *Install*. Para instalar um zip baixado, vá em Settings → *Import local mod*.

**Nexus Mods / Vortex:** também está no [Nexus Mods](https://www.nexusmods.com/valheim/mods/4900) (lá ele se chama **Shieldmates**).

**Manual:**
1. Extraia o zip.
2. Copie a pasta `plugins/Warband` para `Valheim/BepInEx/plugins/`.
   Resultado: `Valheim/BepInEx/plugins/Warband/Warband.dll`.
3. Abra o jogo.

### Configuração

Aperte **F1** no jogo (precisa do Configuration Manager) ou edite `BepInEx/config/felih.Warband.cfg`. As opções são as da tabela em inglês acima.

### Observações

- Se você usa o **BetterUI**, desligue a party dele (`zenox.betterui.cfg` → `[Party]` → `Enabled = false`) para não ter dois HUDs de party e dois usos do J.
- Jogadores sem o Warband não podem ser convidados (o convite fica sem resposta).
