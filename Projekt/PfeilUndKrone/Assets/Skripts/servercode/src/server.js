// server.js  –  kompakte Logs (default) | verbose mit LOG_LEVEL=debug

const WebSocket = require('ws');
const http      = require('http');
const PORT      = 8080;
const IP      = "localhost";
const { createInitialGameState, sendTo} = require("./util")
const { joinRandom, createPrivateLobby, joinLobbyById } = require("./lobby")
const {log, startNewRound, evaluatePaths} = require("./util")




const lobbies = new Map();
const httpServer = http.createServer();
const wss        = new WebSocket.Server({ server: httpServer });


/* ───────── Client lifecycle ───────── */
wss.on('connection', ws => {
  log('info', `[+] Client connected (${wss.clients.size})`);

  ws.on('message', raw => {
    let msg;
    try { msg = JSON.parse(raw); } catch { return ws.send(`{"type":"error","payload":"Bad JSON"}`); }

    if (!ws.lobbyId) {                     // first message
      if (msg.type === 'join_random')  return joinRandom(ws, lobbies);
      if (msg.type === 'create_lobby') return createPrivateLobby(ws, lobbies);
      if (msg.type === 'join_lobbyById')   return joinLobbyById(ws, msg.payload?.lobby_id, lobbies);
      return ws.send(`{"type":"error","payload":"First message must be join_*"}`);
    } 

    if (msg.type === 'join_random') {
      const lid  = ws.lobbyId || 'none';
      const role = ws.role || 'unknown';
      const turn = lobbies.get(ws.lobbyId)?.gameState?.turn || 'n/a';
      let payloadStr;
      try { payloadStr = JSON.stringify(msg.payload); } catch { payloadStr = '[unserializable]'; }

      log('warn', `[Lobby ${lid}] [Duplicate join_random] role=${role} turn=${turn} payload=${payloadStr}`);
      ws.send(`{"type":"info","payload":"Already in a lobby; ignoring join_random"}`);
      return; // don't fall into handleGameMessage
    }
    
    //Overview: 
    //lobby.players : list of websockets
    //ws.role : "Bandit" or "King"
    //lobby.gs -> gameState
    //gamestate: .turnNumber, .bandit.submittedAmbushes
    const lobby = lobbies.get(ws.lobbyId);
    if (!lobby) return ws.send(`{"type":"error","payload":"Lobby vanished"}`);
    handleGameMessage(ws, msg, lobby);
  });

  ws.on('close', () => handleDisconnect(ws, lobbies));
});


function handleDisconnect(ws, lobbies) {
  const id = ws.lobbyId;
  if (!id) return;
  const lobby = lobbies.get(id);
  if (!lobby) return;

  // If the game is already over, don't reset the lobby for the survivor.
  if (lobby.gameState.isGameOver) {
    lobby.players = lobby.players.filter(p => p !== ws);
    if (lobby.players.length === 0) {
      lobbies.delete(id);
      log('info', `[Lobby ${id}] deleted (empty after game over).`);
    }
    return;
  }

  lobby.players = lobby.players.filter(p => p !== ws);
  if (lobby.players.length === 0) {
    lobbies.delete(id);
    log('info', `[Lobby ${id}] deleted (empty).`);
    return;
  }

  const survivor = lobby.players[0];
  lobby.gameState = createInitialGameState();
  survivor.role='King';
  survivor.send(`{"type":"info","payload":"Opponent left – waiting…"}`);
  log('info', `[Lobby ${id}] player left, waiting for replacement.`);
}

/* ───────── Game flow ───────── */


function startBanditTurn(lobby) {
  lobby.gameState.turn='BANDIT_PLANNING';
  sendTo(lobby,'Bandit',{ type:'bandit_turn_start', payload:{message:'Place ambushes'}} );
  sendTo(lobby,'King',  { type:'turn_status', payload:'Waiting for Bandit to place ambushes...'} );
  sendTo(lobby,'Bandit',{ type:'turn_status', payload:'Your turn: Place your ambushes!'} );
  
  // Clear InfoText for both players
  sendTo(lobby,'King',{ type:'info', payload:'' });
  sendTo(lobby,'Bandit',{ type:'info', payload:'' });
}

function handleGameMessage(ws, msg, lobby) {
  const gs = lobby.gameState
  role = ws.role;

  const payload = (typeof msg.payload === 'string') ? JSON.parse(msg.payload) : msg.payload;

  log("info", `Payload: ${payload}`)

  if (role === 'King' && gs.turn==='KING_PLANNING') {
    
    if (msg.type === 'buy_worker') {
      const grainCost = payload.grainCost || 30;
      const woodCost = payload.woodCost || 10;
      
      if (gs.king.resources.grain >= grainCost && gs.king.resources.wood >= woodCost) {
        gs.king.resources.grain -= grainCost;
        gs.king.resources.wood -= woodCost;
        gs.king.purchasedWorkers += 1;
        sendTo(lobby,'King',{ type:'worker_approved', payload: { workerCount: gs.king.purchasedWorkers } });
        sendTo(lobby,'King',{ type:'resource_update', payload: gs.king.resources });
        log('info', `[Lobby ${lobby.id}] King bought worker #${gs.king.purchasedWorkers} (grain ${gs.king.resources.grain}, wood ${gs.king.resources.wood})`);
      } else {
        sendTo(lobby,'King',{ type:'worker_denied', payload:{reason:'Insufficient resources'} });
      }
      return;
    }

    // Client sendet Liste an Listen von {q, r, direction} 
    // bzw. das verschachtelte DTO
    // PlaceWorkersPayload -> paths: SerializablePathData[] -> SerializableHexVertex[] -> {q, r, direction}
    if (msg.type === 'place_workers') {
      gs.king.submittedPaths = payload.paths || [];
      log('info', `✅ Path received from King`);
      log('info', `[Lobby ${lobby.id}] King paths:\n` + JSON.stringify(gs.king.submittedPaths, null, 2));
      return startBanditTurn(lobby);
    }
  }

  if (role === 'Bandit' && gs.turn==='BANDIT_PLANNING') {
    
    if (msg.type === 'buy_ambush') {
      if (gs.bandit.resources.gold >= 5) {
        gs.bandit.resources.gold -= 5;
        sendTo(lobby,'Bandit',{ type:'ambush_approved', payload: payload });
        log('info', `[Lobby ${lobby.id}] Bandit bought ambush (gold ${gs.bandit.resources.gold})` + JSON.stringify(payload, null, 2));
      } else {
        sendTo(lobby,'Bandit',{ type:'ambush_denied', payload:{reason:'Insufficient gold'} });
      }
      return;
    }
    if (msg.type === 'place_ambushes') {
      const ambushes = payload?.ambushes || [];
      log('info', `✅ Ambushes received from Bandit`);
      log('info', `[Lobby ${lobby.id}] ✅ Bandit submitted ${ambushes.length} ambushes:`);
      
      // Debug jede einzelne Ambush - fix property access for serialized format
      ambushes.forEach((ambush, index) => {
        try {
          log('info', `[Lobby ${lobby.id}] Ambush [${index}]: cornerA(${ambush.cornerA.q},${ambush.cornerA.r},${ambush.cornerA.direction}) <-> cornerB(${ambush.cornerB.q},${ambush.cornerB.r},${ambush.cornerB.direction})`);
        } catch (e) {
          log('info', `[Lobby ${lobby.id}] ❌ Error parsing ambush [${index}]: ${JSON.stringify(ambush)}`);
        }
      });
      
      gs.bandit.submittedAmbushes = ambushes;
      log('info', `[Lobby ${lobby.id}] ✅ Bandit finalized ${ambushes.length} ambushes successfully`);
      
      // Kleine Verzögerung vor Animation
      log('info', `Starting animation in 2 seconds...`);
      
      // Info an beide Spieler dass Animation startet
      sendTo(lobby, 'King', { type: 'info', payload: 'Animation starting...' });
      sendTo(lobby, 'Bandit', { type: 'info', payload: 'Animation starting...' });
      
      setTimeout(() => executeRound(lobby), 2000);
      return;
    }
  }
  log('info', `[Lobby ${lobby.id}] [Invalid Msg] type=${msg.type}, payload=${JSON.stringify(payload)}`);
  sendTo(lobby, role, { type:'error', payload:'Invalid action/turn' });
}



function executeRound(lobby) {
  const gs = lobby.gameState;
  gs.turn = 'EXECUTING';

  log('info', `[Lobby ${lobby.id}] Animation starting - executing round`);
  log('info', 'King Paths:', JSON.stringify(gs.king.submittedPaths, null, 2));
  log('info', 'Bandit Ambushes:', JSON.stringify(gs.bandit.submittedAmbushes, null, 2));

  //function: evaluatePaths()
  //call method that tells if the bandits hit a subpath of the kings path
  //if so, also return winner = "bandit" else "king"
  //for each subpath that is correct, grant a plus { gold: 20,  wood: 10,  grain: 20  }, resourceUpdate to the winners resources
  //calculate the new sum of resources, send the updated sum within winnerResourceUpdate
  //

  const { outcome, winner } = evaluatePaths(gs);

  
  log('info', 'Kings Resources :', JSON.stringify(lobby.gameState.king.resources, null, 2));
  log('info', 'Bandits Resources :', JSON.stringify(lobby.gameState.bandit.resources, null, 2));

  
  broadcast(lobby, {
    type: 'execute_round',
    payload: {
      kingPaths: gs.king.submittedPaths,
      banditAmbushes: gs.bandit.submittedAmbushes,
      banditBonus: { ...gs.bandit.resources },
      kingBonus: { ...gs.king.resources },
      outcome: outcome
    }
  });

  // --- NEW: Check for a game winner after updating resources ---
  const KING_GOLD_WIN_CONDITION = 500;
  const BANDIT_GOLD_WIN_CONDITION = 500;
  let gameWinner = null;

  if (gs.king.resources.gold >= KING_GOLD_WIN_CONDITION) {
    gameWinner = 'King';
  } else if (gs.bandit.resources.gold >= BANDIT_GOLD_WIN_CONDITION) {
    gameWinner = 'Bandit';
  }

  // After the animation delay, either end the game or start a new round
  const roundEndDelay = 10000; // 10 seconds for animation
  if (gameWinner) {
    log('info', `[Lobby ${lobby.id}] GAME OVER! Winner: ${gameWinner}`);
    gs.isGameOver = true;
    setTimeout(() => {
      broadcast(lobby, {
        type: 'game_over',
        payload: {
          winner: gameWinner,
          reason: `Reached the gold limit of ${gameWinner === 'King' ? KING_GOLD_WIN_CONDITION : BANDIT_GOLD_WIN_CONDITION}.`
        }
      });
      // Optionally, you could add logic here to clean up the lobby
    }, roundEndDelay);
  } else {
    // No winner yet, start the next round after the delay
    setTimeout(() => startNewRound(lobby), roundEndDelay);
  }
}



const broadcast = (lobby, m) => lobby.players.forEach(p => p.readyState===WebSocket.OPEN && p.send(JSON.stringify(m)));




/* ───────── Start server ───────── */
httpServer.listen(PORT, () => log('info', `Game server listening on ws://localhost:${PORT}`));
