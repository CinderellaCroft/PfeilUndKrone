const { genId, createInitialGameState, log, startMatch } = require("./util");

const newLobby = id => ({ id, players: [], gameState: createInitialGameState() });
const WebSocket = require('ws');


function createPrivateLobby(ws, lobbies) {
  const lobbyId = genId();
  const lobby   = newLobby(lobbyId);
  lobby.players.push(ws);
  ws.role='King'; ws.lobbyId=lobbyId;
  lobbies.set(lobbyId, lobby);

  log('info', `[createPrivateLobby] Lobby ${lobbyId} created (private), current lobbies: ${[...lobbies.keys()]}`);
  ws.send(JSON.stringify({ 
    type: 'lobby_created', 
    payload: { lobby_id: lobbyId } 
  }));
}

function joinLobbyById(ws, lobbyId, lobbies) {
  log('info', `joinLobbyById: ${lobbyId}, current lobbies: ${[...lobbies.keys()]}`);
  const lobby = lobbies.get(lobbyId);
  if (!lobby || lobby.players.length >= 2)
    return ws.send(`{"type":"error","payload":"Cannot join"}`);

  lobby.players.push(ws);
  ws.role='Bandit'; ws.lobbyId=lobbyId;
  ws.send(JSON.stringify({ type:'lobby_joinedById', payload:{ lobby_id:lobbyId, queued:false } }));

  log('info', `[Lobby ${lobbyId}] ${lobby.players.length}/2 players`);
  if (lobby.players.length === 2) startMatch(lobby);
}


module.exports = { createPrivateLobby, joinLobbyById };