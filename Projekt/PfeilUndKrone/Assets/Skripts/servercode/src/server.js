// server.js  –  kompakte Logs (default) | verbose mit LOG_LEVEL=debug

const WebSocket = require('ws');
const http = require('http');
const PORT = 8080;
const IP = "localhost";
const { createInitialGameState, sendTo } = require("./util")
const { createPrivateLobby, joinLobbyById } = require("./lobby")
const { log, startNewRound, evaluatePaths } = require("./util")




const lobbies = new Map();
const httpServer = http.createServer();
const wss = new WebSocket.Server({ server: httpServer });


/* ───────── Game Logic Functions ───────── */
function checkLoseConditions(gs) {
  // Check if King loses: no workers alive AND can't buy new workers
  const kingWorkerCost = 20 + 8; // grain + wood
  const kingHasNoWorkers = gs.king.purchasedWorkers <= 0;
  const kingCantBuyWorkers = gs.king.resources.grain < 20 || gs.king.resources.wood < 8;
  
  if (kingHasNoWorkers && kingCantBuyWorkers) {
    return {
      hasLoser: true,
      loser: 'King',
      reason: `King ran out of workers and cannot afford new ones (need ${kingWorkerCost} resources).`
    };
  }

  // Check if Bandit loses: can't afford any ambushes
  const ambushCost = 12;
  const banditHighestResource = Math.max(gs.bandit.resources.wood, gs.bandit.resources.grain);
  const banditCantBuyAmbushes = banditHighestResource < ambushCost;
  
  if (banditCantBuyAmbushes) {
    return {
      hasLoser: true,
      loser: 'Bandit',
      reason: `Bandit cannot afford any ambushes (need ${ambushCost} wood or grain, has ${gs.bandit.resources.wood} wood and ${gs.bandit.resources.grain} grain).`
    };
  }

  return { hasLoser: false, loser: null, reason: null };
}

/* ───────── Client lifecycle ───────── */
wss.on('connection', ws => {
  log('info', `[+] Client connected (${wss.clients.size})`);

  ws.on('message', raw => {
    let msg;
    try { msg = JSON.parse(raw); } catch { return ws.send(`{"type":"error","payload":"Bad JSON"}`); }

    const payload = (typeof msg.payload === 'string') ? JSON.parse(msg.payload) : msg.payload;

    if (!ws.lobbyId) {                     // first message
      if (msg.type === 'create_lobby') return createPrivateLobby(ws, lobbies);
      if (msg.type === 'join_lobbyById') return joinLobbyById(ws, payload?.lobby_id, lobbies);
      return ws.send(`{"type":"error","payload":"First message must be join_*"}`);
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
  survivor.role = 'King';
  survivor.send(`{"type":"info","payload":"Opponent left – waiting…"}`);
  log('info', `[Lobby ${id}] player left, waiting for replacement.`);
}

/* ───────── Game flow ───────── */


function startBanditTurn(lobby) {
  lobby.gameState.turn = 'BANDIT_PLANNING';

  // Extract resource field locations from King's submitted paths for bandit visibility
  const workerLocations = [];
  if (lobby.gameState.king.submittedPaths && lobby.gameState.king.submittedPaths.length > 0) {
    for (const pathData of lobby.gameState.king.submittedPaths) {
      workerLocations.push({
        resourceFieldQ: pathData.resourceFieldQ,
        resourceFieldR: pathData.resourceFieldR,
        isWagonWorker: pathData.isWagonWorker || false
      });
    }
  }

  log('info', `[Lobby ${lobby.id}] Sending ${workerLocations.length} worker locations to bandit: ${JSON.stringify(workerLocations)}`);
  sendTo(lobby, 'Bandit', {
    type: 'bandit_turn_start', payload: {
      message: 'Place ambushes',
      workerLocations: workerLocations
    }
  });
  sendTo(lobby, 'King', { type: 'turn_status', payload: 'Waiting for Bandit to place ambushes...' });
  sendTo(lobby, 'Bandit', { type: 'turn_status', payload: 'Your turn: Place your ambushes!' });

  // Clear InfoText for both players
  sendTo(lobby, 'King', { type: 'info', payload: '' });
  sendTo(lobby, 'Bandit', { type: 'info', payload: '' });
}

function handleGameMessage(ws, msg, lobby) {
  const gs = lobby.gameState
  const role = ws.role;

  const payload = (typeof msg.payload === 'string') ? JSON.parse(msg.payload) : msg.payload;

  log("info", `Payload: ${payload}`)

  if(msg.type === "quit_game"){
    if (gs.isGameOver) return;
    var gameWinner = role === "King" ? "Bandit" : "King";
    var gameLoser = gameWinner === "King" ? "Bandit" : "King"
    var gameEndReason = `${role} quit the game! Winner: ${gameWinner}`
    log("info", `[Lobby${lobby.id}] ${gameEndReason}`);
    gs.isGameOver = true;
    sendTo(lobby, gameWinner, {type: "game_over", payload : { winner: gameWinner, reason: gameEndReason, amIWinner : true }});
    sendTo(lobby, gameLoser, {type: "game_over", payload : { winner: gameWinner, reason: gameEndReason, amIWinner : false }});
    return;
  }

  if (role === 'King' && gs.turn === 'KING_PLANNING') {

    if (msg.type === 'buy_worker') {
      const grainCost = payload.grainCost || 20;
      const woodCost = payload.woodCost || 8;

      if (gs.king.resources.grain >= grainCost && gs.king.resources.wood >= woodCost) {
        gs.king.resources.grain -= grainCost;
        gs.king.resources.wood -= woodCost;
        gs.king.purchasedWorkers += 1;
        sendTo(lobby, 'King', { type: 'worker_approved', payload: { workerCount: gs.king.purchasedWorkers, wagonWorkers: gs.king.wagonWorkers } });
        sendTo(lobby, 'King', { type: 'resource_update', payload: gs.king.resources });
        log('info', `[Lobby ${lobby.id}] King bought worker #${gs.king.purchasedWorkers} (grain ${gs.king.resources.grain}, wood ${gs.king.resources.wood})`);
      } else {
        sendTo(lobby, 'King', { type: 'worker_denied', payload: { reason: 'Insufficient resources' } });
      }
      return;
    }

    if (msg.type === 'upgrade_worker_wagon') {
      const woodCost = 25;
      const availableWorkers = gs.king.purchasedWorkers - gs.king.wagonWorkers;

      if (gs.king.resources.wood >= woodCost && availableWorkers > 0) {
        gs.king.resources.wood -= woodCost;
        gs.king.wagonWorkers += 1;
        sendTo(lobby, 'King', { type: 'wagon_upgrade_approved', payload: { wagonWorkers: gs.king.wagonWorkers, workerCount: gs.king.purchasedWorkers } });
        sendTo(lobby, 'King', { type: 'resource_update', payload: gs.king.resources });
        log('info', `[Lobby ${lobby.id}] King upgraded worker to wagon #${gs.king.wagonWorkers} (wood ${gs.king.resources.wood})`);
      } else {
        const reason = gs.king.resources.wood < woodCost ? 'Insufficient wood' : 'No available workers to upgrade';
        sendTo(lobby, 'King', { type: 'wagon_upgrade_denied', payload: { reason: reason } });
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

    if (msg.type === 'delete_path') {
      const grainCost = payload.grainCost || 20;
      const woodCost = payload.woodCost || 8;
      const wagonWoodCost = payload.wagonWoodCost || 25;
      const isWagonWorker = payload.isWagonWorker || false;
      
      log('info', `[Lobby ${lobby.id}] King delete_path: grainCost=${grainCost}, woodCost=${woodCost}, wagonWoodCost=${wagonWoodCost}, isWagon=${isWagonWorker}, king grain=${gs.king.resources.grain}, wood=${gs.king.resources.wood}`);
      
      // Refund worker costs
      gs.king.resources.grain += grainCost;
      gs.king.resources.wood += woodCost;
      
      // Refund additional wagon cost if it was a wagon worker
      if (isWagonWorker) {
        gs.king.resources.wood += wagonWoodCost;
      }
      
      sendTo(lobby, 'King', { type: 'delete_path_approved', payload: payload });
      sendTo(lobby, 'King', { type: 'resource_update', payload: gs.king.resources });
      
      const totalWoodRefund = woodCost + (isWagonWorker ? wagonWoodCost : 0);
      log('info', `[Lobby ${lobby.id}] King deleted path, refunded ${grainCost} grain + ${totalWoodRefund} wood (new totals: grain ${gs.king.resources.grain}, wood ${gs.king.resources.wood})`);
      return;
    }
  }

  if (role === 'Bandit' && gs.turn === 'BANDIT_PLANNING') {

    if (msg.type === 'buy_ambush') {
      const cost = payload.cost || 12;
      
      // Calculate which resource should be used (highest amount) - server-side validation
      const serverCalculatedResourceType = gs.bandit.resources.wood >= gs.bandit.resources.grain ? 'wood' : 'grain';
      const resourceType = payload.resourceType || serverCalculatedResourceType;
      
      log('info', `[Lobby ${lobby.id}] Bandit buy_ambush: cost=${cost}, clientResourceType=${payload.resourceType}, serverCalc=${serverCalculatedResourceType}, using=${resourceType}, bandit wood=${gs.bandit.resources.wood}, grain=${gs.bandit.resources.grain}`);
      
      let canAfford = false;
      let insufficientReason = '';
      
      if (resourceType === 'wood') {
        canAfford = gs.bandit.resources.wood >= cost;
        if (canAfford) {
          gs.bandit.resources.wood -= cost;
        } else {
          insufficientReason = 'Insufficient wood';
        }
      } else if (resourceType === 'grain') {
        canAfford = gs.bandit.resources.grain >= cost;
        if (canAfford) {
          gs.bandit.resources.grain -= cost;
        } else {
          insufficientReason = 'Insufficient grain';
        }
      }
      
      if (canAfford) {
        sendTo(lobby, 'Bandit', { type: 'ambush_approved', payload: payload });
        sendTo(lobby, 'Bandit', { type: 'resource_update', payload: gs.bandit.resources });
        log('info', `[Lobby ${lobby.id}] Bandit bought ambush using ${cost} ${resourceType} (remaining: wood ${gs.bandit.resources.wood}, grain ${gs.bandit.resources.grain})`);
      } else {
        sendTo(lobby, 'Bandit', { type: 'ambush_denied', payload: { reason: insufficientReason } });
      }
      return;
    }

    if (msg.type === 'delete_ambush') {
      const cost = payload.cost || 12;
      
      // Calculate which resource should be refunded (same logic as buying)
      const serverCalculatedResourceType = gs.bandit.resources.wood >= gs.bandit.resources.grain ? 'wood' : 'grain';
      const resourceType = payload.resourceType || serverCalculatedResourceType;
      
      log('info', `[Lobby ${lobby.id}] Bandit delete_ambush: cost=${cost}, clientResourceType=${payload.resourceType}, serverCalc=${serverCalculatedResourceType}, using=${resourceType}, bandit wood=${gs.bandit.resources.wood}, grain=${gs.bandit.resources.grain}`);
      
      // Always refund (no validation needed for deletion)
      if (resourceType === 'wood') {
        gs.bandit.resources.wood += cost;
      } else if (resourceType === 'grain') {
        gs.bandit.resources.grain += cost;
      }
      
      sendTo(lobby, 'Bandit', { type: 'delete_ambush_approved', payload: payload });
      sendTo(lobby, 'Bandit', { type: 'resource_update', payload: gs.bandit.resources });
      log('info', `[Lobby ${lobby.id}] Bandit deleted ambush, refunded ${cost} ${resourceType} (new totals: wood ${gs.bandit.resources.wood}, grain ${gs.bandit.resources.grain})`);
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
  sendTo(lobby, role, { type: 'error', payload: 'Invalid action/turn' });
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

  const { outcome, winner, workersLost, lostWorkerPaths } = evaluatePaths(gs);


  log('info', 'Kings Resources :', JSON.stringify(lobby.gameState.king.resources, null, 2));
  log('info', 'Bandits Resources :', JSON.stringify(lobby.gameState.bandit.resources, null, 2));


  broadcast(lobby, {
    type: 'execute_round',
    payload: {
      kingPaths: gs.king.submittedPaths,
      banditAmbushes: gs.bandit.submittedAmbushes,
      banditBonus: { ...gs.bandit.resources },
      kingBonus: { ...gs.king.resources },
      outcome: outcome,
      workersLost: workersLost,
      lostWorkerPaths: lostWorkerPaths,
      kingWorkerCount: gs.king.purchasedWorkers,
      kingWagonWorkerCount: gs.king.wagonWorkers
    }
  });

  if (workersLost > 0) {
    sendTo(lobby, 'King', {
      type: 'workers_lost',
      payload: {
        count: workersLost,
        pathsAffected: lostWorkerPaths,
        remainingWorkers: gs.king.purchasedWorkers
      }
    });
    log('info', `[Lobby ${lobby.id}] Sent worker loss notification to King: ${workersLost} workers lost`);
  }

  // --- Check for game end conditions (win/lose) after updating resources ---
  const KING_GOLD_WIN_CONDITION = 200;
  const BANDIT_GOLD_WIN_CONDITION = 100;
  let gameWinner = null;
  let gameEndReason = null;

  // Check win conditions first
  if (gs.king.resources.gold >= KING_GOLD_WIN_CONDITION) {
    gameWinner = 'King';
    gameEndReason = `King reached the gold limit of ${KING_GOLD_WIN_CONDITION}.`;
  } else if (gs.bandit.resources.gold >= BANDIT_GOLD_WIN_CONDITION) {
    gameWinner = 'Bandit';
    gameEndReason = `Bandit reached the gold limit of ${BANDIT_GOLD_WIN_CONDITION}.`;
  }
  // Check lose conditions if no winner yet
  else {
    const { hasLoser, loser, reason } = checkLoseConditions(gs);
    if (hasLoser) {
      gameWinner = loser === 'King' ? 'Bandit' : 'King'; // Other player wins
      gameEndReason = reason;
    }
  }

  // After the animation delay, either end the game or start a new round
  const roundEndDelay = 10000; // 10 seconds for animation
  if (gameWinner) {
    log('info', `[Lobby ${lobby.id}] GAME OVER! Winner: ${gameWinner}`);
    gs.isGameOver = true;
    var gameLoser = gameWinner === "King" ? "Bandit" : "King";
    sendTo(lobby, gameWinner, {type: "game_over", payload : { winner: gameWinner, reason: gameEndReason, amIWinner : true }});
    sendTo(lobby, gameLoser, {type: "game_over", payload : { winner: gameWinner, reason: gameEndReason, amIWinner : false }});
  } else {
    // No winner yet, start the next round after the delay
    setTimeout(() => startNewRound(lobby), roundEndDelay);
  }
}



const broadcast = (lobby, m) => lobby.players.forEach(p => p.readyState === WebSocket.OPEN && p.send(JSON.stringify(m)));




/* ───────── Start server ───────── */
httpServer.listen(PORT, () => log('info', `Game server listening on ws://localhost:${PORT}`));
