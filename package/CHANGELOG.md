# Changelog

## 1.4.0
- Chat tabs: **All** and **Party** buttons above the chat. In the Party tab you only see party messages, and what you type goes straight to the party, no `/p` needed. `/p` alone opens the Party tab; `/all` (or `/a`) goes back; `/p message` and `/all message` send a single message. The chat explains these commands once per session.
- Abas no chat: botões **All** e **Party** acima do chat. Na aba Party você só vê as mensagens da party, e o que você digita vai direto para a party, sem `/p`. `/p` sozinho abre a aba Party; `/all` (ou `/a`) volta; `/p mensagem` e `/all mensagem` mandam uma mensagem só. O chat explica esses comandos uma vez por sessão.
- The party HUD hides while a window (a chest, the Storage window of other mods...) covers it, so it no longer draws on top of other windows.
- O HUD da party se esconde enquanto uma janela (um baú, a janela Storage de outros mods...) está por cima dele, então não aparece mais por cima de outras janelas.
- The ally buttons on the large map move down when the game's own "shared map" toggle is showing, so they don't cover it.
- Os botões de aliados no mapa grande descem quando o botão de "mapa compartilhado" do jogo aparece, para não ficarem por cima dele.
- The party HUD costs about half as much per frame.
- O HUD da party custa cerca da metade por quadro.

## 1.3.0
- One button per ally on the large map instead of a single "Find party" button: click a name to fly to that ally. A greyed name (?) means their position isn't known yet; the button glows gold while the map flies.
- Um botão por aliado no mapa grande em vez de um único botão "Find party": clique num nome para voar até esse aliado. Nome cinza (?) quer dizer que a posição ainda não é conhecida; o botão fica dourado enquanto o mapa voa.

## 1.2.0
- Send items to your party: with your bag open, hover an item and press P, pick a party member nearby (30 m), and they get a Y / N prompt. The item leaves your bag only once they accept; a full bag declines on its own, and the item comes back if they leave before it arrives. Items that can't go through portals can be sent too. Options `SendItemKey` and `SendItemRange`.
- Envie itens para a party: com a bolsa aberta, passe o mouse num item e aperte P, escolha um membro da party por perto (30 m) e ele recebe um pedido Y / N. O item só sai da sua bolsa quando ele aceita; bolsa cheia recusa sozinha, e o item volta se ele sair antes de chegar. Itens que não passam em portal também podem ser enviados. Opções `SendItemKey` e `SendItemRange`.

## 1.1.0
- Find your party on the map: a "Find party" button on the large map. Click it and the map zooms out, glides to an ally and zooms back in, with a ring and their name in the middle. Each click goes to the next ally. Works even when the ally hides from the map. Options in `7 - Map`.
- Encontre a party no mapa: um botão "Find party" no mapa grande. Clique e o mapa se afasta, desliza até um aliado e aproxima de novo, com um anel e o nome dele no meio. Cada clique vai para o próximo aliado. Funciona mesmo se o aliado estiver escondido no mapa. Opções em `7 - Map`.

## 1.0.2
- Party chat: type `/p message` (or `/party message`) in the chat to talk only to your party. It shows with a [Party] tag and the sender's colour, and for a few seconds under their part of the party HUD. Options in `6 - Chat`.
- Chat de party: digite `/p mensagem` (ou `/party mensagem`) no chat para falar só com a party. Aparece com a tag [Party] e a cor de quem falou, e por alguns segundos embaixo do bloco dele no HUD da party. Opções em `6 - Chat`.

## 1.0.1
- Silhouette rebuilt: only the parts of an ally hidden behind something are coloured. Parts behind the ally's own body (an arm behind the torso, the chest seen from behind) no longer light up, and it no longer flickers when the camera zooms.
- New silhouette styles: Fill (default), Outline (only the edge of the hidden part, drawn on the object in front) and AlwaysOnTop.
- New options: OutlineWidth, VisibleMargin, TestOnYourself (try the silhouette on your own character).
- Silhueta refeita: só as partes do aliado escondidas atrás de algo ficam coloridas. Partes atrás do próprio corpo (braço atrás do tronco, o peito visto de costas) não acendem mais, e ela não pisca mais com o zoom da câmera.
- Novos estilos de silhueta: Fill (padrão), Outline (só o contorno da parte escondida, desenhado sobre o objeto da frente) e AlwaysOnTop.
- Novas opções: OutlineWidth, VisibleMargin, TestOnYourself (testar a silhueta no seu próprio personagem).

## 1.0.0
- First release. / Primeira versão.
