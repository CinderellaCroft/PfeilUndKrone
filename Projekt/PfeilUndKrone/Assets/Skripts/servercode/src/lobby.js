const { genId, createInitialGameState, log, startMatch } = require("./util");

const newLobby = id => ({ id, players: [], gameState: createInitialGameState() });
const WebSocket = require('ws');


function createPrivateLobby(ws, lobbies) {
  const lobbyId = genId('priv');
  const lobby   = newLobby(lobbyId);
  lobby.players.push(ws);
  ws.role='King'; ws.lobbyId=lobbyId;
  lobbies.set(lobbyId, lobby);

  log('info', `[Lobby ${lobbyId}] created (private)`);
  ws.send(JSON.stringify({ type:'lobby_created', lobbyID:lobbyId  }));
}


function joinRandom(ws, lobbies) {
  let lobbyId;
  for (const [id, lob] of lobbies)
    if (id.startsWith('rand-') && lob.players.length === 1) { lobbyId = id; break; }
  if (!lobbyId) lobbyId = genId('rand');

  const lobby = lobbies.get(lobbyId) ?? newLobby(lobbyId);
  lobby.players.push(ws);
  ws.role = lobby.players.length === 1 ? 'King' : 'Bandit';
  ws.lobbyId = lobbyId;
  lobbies.set(lobbyId, lobby);

  log('info', `[Lobby ${lobbyId}] ${lobby.players.length}/2 players`);
  ws.send(JSON.stringify({ type:'lobby_randomly_joined', payload:{ lobby_id:lobbyId, queued:lobby.players.length===1 } }));
  if (lobby.players.length === 2) startMatch(lobby);
}



function joinLobbyById(ws, lobbyId, lobbies) {
  const lobby = lobbies.get(lobbyId);
  if (!lobby || lobby.players.length >= 2)
    return ws.send(`{"type":"error","payload":"Cannot join"}`);

  lobby.players.push(ws);
  ws.role='Bandit'; ws.lobbyId=lobbyId;
  ws.send(JSON.stringify({ type:'lobby_joinedById', payload:{ lobby_id:lobbyId, queued:false } }));

  log('info', `[Lobby ${lobbyId}] ${lobby.players.length}/2 players`);
  if (lobby.players.length === 2) startMatch(lobby);
}


module.exports = { joinRandom, createPrivateLobby, joinLobbyById };