# Warband

**[English](#english) · [Português](#português)**

---

## English

A party for you and your friends. Invite them, and you see each other's health, stamina, eitr, active effects and weapon in a HUD you can move, resize and reorder. Allies behind walls, trees or hills show as a coloured silhouette, and each one has their own coloured marker with their initial on the Nav Compass bar.

### Features

- **Client-side.** The server needs nothing. **Everyone in the party needs the mod.**
- **Invite:** look at a player and press **J**, or use the party panel.
- **Party panel (Shift+P):**
  - **Players**: everyone on the server, with *Invite* and *Add friend*.
  - **Friends**: your friends list (kept on your computer). Add by name, see who is online, invite them. Optionally accept invites from friends on their own.
  - **Party**: the members. The leader can remove someone or hand over the lead. Leave the party. Quick switches for the HUD, silhouette and compass.
- **Accept / decline** an invite with **Y / N**. Invites expire after 30 seconds.
- **Party HUD:**
  - A health bar with current / max in the middle, a stamina bar, and an eitr bar for allies who have eitr.
  - Active effects (rested, wet, meads, set bonuses...) with the time left.
  - The weapon in the ally's hand, the distance, and their colour with their initial.
  - Drawn on the same background as the game's windows; the background opacity is adjustable.
  - When an ally dies, their part of the HUD shakes until they respawn.
  - **F8** = edit mode: drag the bar on top to move it, drag an ally to change the order, scroll to resize, F8 again to finish.
- **Silhouette:** only the hidden parts of an ally are coloured; what you see directly stays as it is. Fill or outline style; colour, opacity and maximum distance can be set.
- **Compass:** with [Nav Compass](https://thunderstore.io/c/valheim/p/Ab5oluteZer0/NavCompass/) installed, each ally shows on the compass bar as a circle in their colour with their initial. Can be switched off, with a maximum distance and icon size.
- **Party chat:** type `/p message` (or `/party message`) in the normal chat. Only your party sees it, with a [Party] tag and your colour; it also shows under your part of their party HUD for a few seconds.
- If the party leader leaves or disconnects, the next member takes over.

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
| PanelKey | Shift+P | Opens the party panel. |
| InviteKey | J | Invites the player you are looking at. |
| AcceptKey / DeclineKey | Y / N | Answer a party invite. |
| EditHudKey | F8 | Move, resize and reorder the party HUD. |
| AutoAcceptFriends | off | Accept invites from friends on their own. |
| MaxMembers | 10 | Most players in one party. |
| HUD Enabled | on | The party HUD. |
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
| Chat Color | light blue | Colour of party chat messages. |
| Chat ShowInHud / HudSeconds | on / 10 s | Last message under each ally in the HUD, and for how long. |
| DetailedLog | off | Writes details to the BepInEx log (for bug reports). |

### Notes

- If you use **BetterUI**, turn off its party (`zenox.betterui.cfg` → `[Party]` → `Enabled = false`) so you don't get two party HUDs and two uses of J.
- Players without Warband can't be invited (the invite just goes unanswered).

---

## Português

Uma party para você e seus amigos. Convide-os e vocês veem a vida, a stamina, o eitr, os efeitos ativos e a arma uns dos outros num HUD que dá para mover, redimensionar e reordenar. Aliados atrás de paredes, árvores ou morros aparecem como uma silhueta colorida, e cada um tem seu próprio marcador colorido com a inicial na barra do Nav Compass.

### Recursos

- **Client-side.** O servidor não precisa de nada. **Todos da party precisam do mod.**
- **Convidar:** olhe para um jogador e aperte **J**, ou use o painel da party.
- **Painel da party (Shift+P):**
  - **Players**: todos no servidor, com *Invite* (convidar) e *Add friend* (adicionar amigo).
  - **Friends**: sua lista de amigos (salva no seu computador). Adicione pelo nome, veja quem está online e convide. Opcionalmente aceita sozinho os convites de amigos.
  - **Party**: os membros. O líder pode remover alguém ou passar a liderança. Sair da party. Atalhos para ligar e desligar o HUD, a silhueta e a bússola.
- **Aceitar / recusar** um convite com **Y / N**. Convites expiram em 30 segundos.
- **HUD da party:**
  - Barra de vida com atual / total no meio, barra de stamina e barra de eitr para quem tem eitr.
  - Efeitos ativos (descansado, molhado, hidromel, bônus de conjunto...) com o tempo restante.
  - A arma na mão do aliado, a distância, e a cor dele com a inicial.
  - Desenhado com o mesmo fundo das janelas do jogo; a opacidade do fundo é ajustável.
  - Quando um aliado morre, a parte dele no HUD treme até ele renascer.
  - **F8** = modo de edição: arraste a barra de cima para mover, arraste um aliado para mudar a ordem, use a rodinha para mudar o tamanho, F8 de novo para terminar.
- **Silhueta:** só as partes escondidas do aliado ficam coloridas; o que você vê diretamente fica normal. Estilo preenchido ou contorno; cor, opacidade e distância máxima configuráveis.
- **Bússola:** com o [Nav Compass](https://thunderstore.io/c/valheim/p/Ab5oluteZer0/NavCompass/) instalado, cada aliado aparece na barra da bússola como um círculo na cor dele com a inicial. Dá para desligar, com distância máxima e tamanho do ícone.
- **Chat de party:** digite `/p mensagem` (ou `/party mensagem`) no chat normal. Só a sua party vê, com a tag [Party] e a sua cor; também aparece embaixo do seu bloco no HUD deles por alguns segundos.
- Se o líder sair ou cair, o próximo membro assume.

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
