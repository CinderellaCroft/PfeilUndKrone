const { LOG_LEVEL } = require("./config")
const { generateResourceMap } = require("./GenerateResourceMap")
const WebSocket = require('ws');

const genId = () => `${Math.floor(100000 + Math.random() * 900000)}`;

function createInitialGameState() {
  return {
    turn: 'SETUP',
    turnNumber: 0,
    isGameOver: false,
    king: { ws: null, resources: { gold: 0, wood: 20, grain: 50 }, submittedPaths: [], purchasedWorkers: 0, wagonWorkers: 0 },
    bandit: { ws: null, resources: { gold: 0, wood: 30, grain: 20 }, submittedAmbushes: [] }
  };
}


function startMatch(lobby) {
  const [k, b] = lobby.players;
  lobby.gameState = createInitialGameState();
  lobby.gameState.king.ws = k;
  lobby.gameState.bandit.ws = b;
  k.role = 'King'; b.role = 'Bandit';

  const { seed, banditMap, kingsMap } = generateResourceMap(); // { seed, reserved, kingsMap: [{q,r,resource:string}] }
  const kingsResourceMap = { seed, map: kingsMap }
  const banditsResourceMap = { seed, map: banditMap }
  // im Gamestate speichern, map ist nun global verfügbar
  lobby.gameState.resourceMap = kingsMap;

  // ---- Logging ----
  console.log(`[Lobby ${lobby.id}] Generated kingsResourceMap seed=${kingsResourceMap.seed}, entries=${kingsResourceMap.map.length}`);
  kingsResourceMap.map.forEach(rd => {
    console.log(`Hex(${rd.q},${rd.r}) -> ${rd.resource}`);
  });

  // summary counts
  const counts = {};
  for (const rd of kingsResourceMap.map) {
    counts[rd.resource] = (counts[rd.resource] || 0) + 1;
  }
  console.log("Resource counts:", counts);
  // -----------------

  // 1) roles first
  sendTo(lobby, 'King', { type: 'match_created', payload: { role: 'King' } });
  sendTo(lobby, 'Bandit', { type: 'match_created', payload: { role: 'Bandit' } });

  // 2) map next (same object to both)
  sendTo(lobby, 'King', { type: 'resource_map', payload: kingsResourceMap });
  sendTo(lobby, 'Bandit', { type: 'resource_map', payload: banditsResourceMap });

  // 3) Clear InfoText for both players when match starts
  sendTo(lobby, 'King', { type: 'info', payload: '' });
  sendTo(lobby, 'Bandit', { type: 'info', payload: '' });

  log('info', `[Lobby ${lobby.id}] match started`);
  setTimeout(() => startNewRound(lobby), 2000);
}





function startNewRound(lobby) {
  const gs = lobby.gameState;
  gs.turnNumber++; gs.turn = 'KING_PLANNING';
  gs.king.submittedPaths = []; gs.bandit.submittedAmbushes = [];

  log('info', `--- Round ${gs.turnNumber} (${lobby.id}) ---`);
  sendTo(lobby, 'King', { type: 'new_round', payload: { roundNumber: gs.turnNumber, resources: gs.king.resources, workers: gs.king.purchasedWorkers, wagonWorkers: gs.king.wagonWorkers } });
  sendTo(lobby, 'Bandit', { type: 'new_round', payload: { roundNumber: gs.turnNumber, resources: gs.bandit.resources, units: {} } });
  sendTo(lobby, 'King', { type: 'king_turn_start', payload: { message: "Place paths" } });
  sendTo(lobby, 'Bandit', { type: 'turn_status', payload: "Waiting for King to place paths..." });
  sendTo(lobby, 'King', { type: 'turn_status', payload: "Your turn: Place your path!" });

  // Clear InfoText for both players
  sendTo(lobby, 'King', { type: 'info', payload: '' });
  sendTo(lobby, 'Bandit', { type: 'info', payload: '' });
}




function log(level, ...args) {
  const rank = { error: 0, info: 1, debug: 2 };
  if (rank[level] <= rank[LOG_LEVEL]) {
    console[level === 'error' ? 'error' : 'log'](...args);
  }
}

function sendTo(lobby, role, msg) {
  const ws = role === 'King' ? lobby.gameState.king.ws : lobby.gameState.bandit.ws;
  if (ws?.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
}

// Erstelle Kanten aus King's Pfaden und Ambush Koordinaten
function getEdgeKey(corner1, corner2) {
  const str1 = `${corner1.q},${corner1.r},${corner1.direction}`;
  const str2 = `${corner2.q},${corner2.r},${corner2.direction}`;
  return str1 < str2 ? `${str1}<->${str2}` : `${str2}<->${str1}`;
}


function evaluatePaths(gs) {
  /////////////////////
  ////// Logging //////
  /////////////////////
  log('info', '=== ✅ EVALUATING PATHS ===');

  log('info', 'Old Resources:');
  log('info', `  King:  ${JSON.stringify(gs.king.resources)}`);
  log('info', `  Bandit:${JSON.stringify(gs.bandit.resources)}`);

  // Logge King's Pfade
  log('info', `King submitted ${gs.king.submittedPaths.length} paths:`);
  gs.king.submittedPaths.forEach((pathObj, pathIndex) => {
    log('info', `  Path [${pathIndex}] with ${pathObj.path.length} vertices:`);
    pathObj.path.forEach((vertex, vertexIndex) => {
      log('info', `    [${vertexIndex}] Vertex(${vertex.q},${vertex.r},${vertex.direction})`);
    });
  });
  // Logge Bandit's Ambushes  
  log('info', `Bandit submitted ${gs.bandit.submittedAmbushes.length} ambushes:`);
  gs.bandit.submittedAmbushes.forEach((ambush, ambushIndex) => {
    try {
      // Fix property access for serialized format
      log('info', `  Ambush [${ambushIndex}]: A(${ambush.cornerA.q},${ambush.cornerA.r},${ambush.cornerA.direction}) <-> B(${ambush.cornerB.q},${ambush.cornerB.r},${ambush.cornerB.direction})`);
    } catch (e) {
      log('info', `  ❌ Fehler parsing ambush [${ambushIndex}]: ${JSON.stringify(ambush)}`);
    }
  });

  //ressourcenfelder aus kingspath auslesen
  // Use the resource field information sent directly from client instead of guessing from vertex position
  originResourceFields = gs.king.submittedPaths.map((pathObj, index) => {
    if (pathObj.resourceFieldQ !== undefined && pathObj.resourceFieldR !== undefined && pathObj.resourceType) {
      // Use the resource field data sent by client
      const field = {
        q: pathObj.resourceFieldQ,
        r: pathObj.resourceFieldR,
        resource: pathObj.resourceType
      };
      log("info", `Path ${index + 1}: King selected resource field (${field.q},${field.r}) of type ${field.resource}`);
      return field;
    } else {
      // Fallback to old logic for backward compatibility
      const firstVertex = pathObj.path[0];
      const field = gs.resourceMap.find(rf => rf.q == firstVertex.q && rf.r == firstVertex.r);
      log("info", `Path ${index + 1}: Fallback - guessed resource from vertex: ${field?.resource || 'Unknown'}`);
      return field;
    }
  });



  //const kingEdges = new Set();
  const kingEdges = []; //list of sets
  log('info', 'Creating edges from King paths:');

  log('info', 'Checking ambush hits:');
  const intercepts = []; //{resource, distance}
  const lostWorkers = [];

  for (let pathIndex = 0; pathIndex < gs.king.submittedPaths.length; pathIndex++) {
    const pathObj = gs.king.submittedPaths[pathIndex];
    const path = pathObj.path;
    intercepts.push({ resource: null, distance: null })
    log('info', `  Processing path [${pathIndex}]:`);
    kingEdges.push([]);//add list of {q, r, direction} to kingEdges list.

    for (let i = 0; i < path.length - 1; i++) {
      const edgeKey = getEdgeKey(path[i], path[i + 1]);
      kingEdges[pathIndex].push(edgeKey);
      log('info', `    Edge: ${path[i].q},${path[i].r},${path[i].direction} <-> ${path[i + 1].q},${path[i + 1].r},${path[i + 1].direction} -> Key: ${edgeKey}`);
    }

    outcomes = []

    gs.bandit.submittedAmbushes.forEach((ambush, index) => {
      try {
        const ambushEdgeKey = getEdgeKey(ambush.cornerA, ambush.cornerB);
        const pos = kingEdges[pathIndex].indexOf(ambushEdgeKey);
        const isNewIntercept = (pos != -1 && (intercepts[pathIndex].resource == null || pos < intercepts[pathIndex].distance))
        if (isNewIntercept) {
          const distance = kingEdges[pathIndex].length - pos
          log('info', `      HIT! Ambush (index:${index}, length: ${distance}) matches King's path`);
          intercepts[pathIndex] = { resource: originResourceFields[pathIndex], distance: distance };
          outcomes.push(ambush);

          if (!lostWorkers.includes(pathIndex)) {
            lostWorkers.push(pathIndex);
            log('info', `      Worker on path ${pathIndex} lost to ambush!`);
          }
        } else {
          log('info', `      MISS! Ambush [${index}] doesn't match any King path`);
        }
      } catch (e) {
        log('info', `    ❌ Fehler checking ambush [${index}]: ${e.message}`);
      }
    });

    if (intercepts[pathIndex].distance == null) {
      intercepts[pathIndex].distance = gs.king.submittedPaths.length;
    }

  }

  console.log("intercepts:");
  console.log(JSON.stringify(intercepts, null, 2));




  function getDistanceFromCenter(q, r) {
    return (Math.abs(q) + Math.abs(q + r) + Math.abs(r)) / 2;
  }

  function calculateResourceValue(distanceFromCenter, pathLength, isWagonWorker = false, decayPerEdge = 3) {
    const baseValue = distanceFromCenter * 15;
    const actualDecayPerEdge = isWagonWorker ? decayPerEdge / 2 : decayPerEdge;
    const decayAmount = pathLength * actualDecayPerEdge;
    return Math.max(8, baseValue - decayAmount);
  }

  //Belohnung berechnen
  banditBonus = { ore: 0, wood: 0, wheat: 0 }
  kingBonus = { ore: 0, wood: 0, wheat: 0 }

  intercepts.forEach((intercept, index) => {
    const originField = originResourceFields[index];
    if (!originField) {
      log('info', `⚠️ No origin field found for path ${index}`);
      return;
    }

    const distanceFromCenter = getDistanceFromCenter(originField.q, originField.r);

    const pathObj = gs.king.submittedPaths[index];
    const isWagonWorker = pathObj?.isWagonWorker || false;

    if (intercept.resource != null) {
      const interceptDistance = kingEdges[index].length - intercept.distance + 1;
      const banditValue = calculateResourceValue(distanceFromCenter, interceptDistance, isWagonWorker);

      resource = intercept.resource.resource;
      log('info', `Bandit intercept at distance ${interceptDistance} from origin (${originField.q},${originField.r}), base distance from center: ${distanceFromCenter}, ${isWagonWorker ? 'wagon' : 'regular'} worker, value: ${banditValue}`);

      if (resource != null) {
        switch (resource) {
          case "Wheat":
            banditBonus.wheat += banditValue;
            break;
          case "Ore":
            banditBonus.ore += banditValue;
            break;
          case "Wood":
            banditBonus.wood += banditValue;
            break;
        }
      }
    } else {
      const fullPathLength = kingEdges[index].length;
      const kingValue = calculateResourceValue(distanceFromCenter, fullPathLength, isWagonWorker);

      resource = originResourceFields[index].resource;
      log('info', `King completed path of length ${fullPathLength} from origin (${originField.q},${originField.r}), base distance from center: ${distanceFromCenter}, ${isWagonWorker ? 'wagon' : 'regular'} worker, value: ${kingValue}`);

      switch (resource) {
        case "Wheat":
          kingBonus.wheat += kingValue;
          break;
        case "Ore":
          kingBonus.ore += kingValue;
          break;
        case "Wood":
          kingBonus.wood += kingValue;
          break;
      }
    }
  });

  console.log("Bandit Bonus: ", JSON.stringify(banditBonus, null, 2));
  console.log("King Bonus: ", JSON.stringify(kingBonus, null, 2));





  gs.bandit.resources.gold += banditBonus.ore;
  gs.bandit.resources.wood += banditBonus.wood;
  gs.bandit.resources.grain += banditBonus.wheat;

  gs.king.resources.gold += kingBonus.ore;
  gs.king.resources.wood += kingBonus.wood;
  gs.king.resources.grain += kingBonus.wheat;

  const hits = intercepts.filter(item => item.resource != null).length;

  const workersLost = lostWorkers.length;
  let regularWorkersLost = 0;
  let wagonWorkersLost = 0;

  if (workersLost > 0) {
    const oldWorkerCount = gs.king.purchasedWorkers;
    const oldWagonWorkerCount = gs.king.wagonWorkers;

    lostWorkers.forEach(pathIndex => {
      const pathObj = gs.king.submittedPaths[pathIndex];
      const isWagonWorker = pathObj?.isWagonWorker || false;

      if (isWagonWorker) {
        wagonWorkersLost++;
      } else {
        regularWorkersLost++;
      }
    });

    gs.king.wagonWorkers = Math.max(0, gs.king.wagonWorkers - wagonWorkersLost);
    gs.king.purchasedWorkers = Math.max(0, gs.king.purchasedWorkers - workersLost);

    log('info', `${workersLost} workers lost! Regular: ${regularWorkersLost}, Wagon: ${wagonWorkersLost}`);
    log('info', `King's worker count: ${oldWorkerCount} -> ${gs.king.purchasedWorkers} (${oldWagonWorkerCount} -> ${gs.king.wagonWorkers} wagons)`);
  }

  // if (hits > 0) {
  //   winner = 'Bandit';
  //   resourceBonus = { gold: 30 * hits, wood: 15 * hits, grain: 30 * hits };
  //   gs.bandit.resources.gold += resourceBonus.gold;
  //   gs.bandit.resources.wood += resourceBonus.wood;
  //   gs.bandit.resources.grain += resourceBonus.grain;
  //   winnerResourceUpdate = { ...gs.bandit.resources };
  //   log('info', `✅ BANDIT WINS! ${hits} successful ambushes. Bonus: ${JSON.stringify(resourceBonus)}`);
  // } else {
  //   winner = 'King';
  //   const pathDistance = gs.king.submittedPaths.reduce((total, pathObj) => total + pathObj.path.length, 0);
  //   resourceBonus = { gold: 3 * pathDistance, wood: 7 * pathDistance, grain: 5 * pathDistance };
  //   gs.king.resources.gold += resourceBonus.gold;
  //   gs.king.resources.wood += resourceBonus.wood;
  //   gs.king.resources.grain += resourceBonus.grain;
  //   winnerResourceUpdate = { ...gs.king.resources };
  //   log('info', `✅ KING WINS! No successful ambushes. Path distance: ${pathDistance}. Bonus: ${JSON.stringify(resourceBonus)}`);
  // }

  winner = "King"

  log('info', `Winner: ${winner}`);
  log('info', '=== ✅ END EVALUATION ===');

  return {
    outcome: outcomes,
    winner,
    workersLost: workersLost,
    lostWorkerPaths: lostWorkers
  };
}


module.exports = {
  createInitialGameState, genId, log, startMatch,
  sendTo, startNewRound, evaluatePaths
};