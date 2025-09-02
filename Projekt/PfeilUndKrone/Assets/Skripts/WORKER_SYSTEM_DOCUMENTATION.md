# Worker Buying and Placement System Documentation

## Overview
This document describes the complete worker buying and placement system implemented for the 2-player hex grid game where one player is the King and one is the Bandit.

## System Architecture

### Core Concept
- **Before**: King could select any resource field and create unlimited paths for free
- **After**: King must buy workers (30 grain + 10 wood each) and can only place paths using purchased workers
- **Workers persist across rounds** - once bought, they can be reused each round

## Implementation Details

### Server-Side Implementation

#### Files Modified:
- `Assets/Skripts/servercode/src/util.js`
- `Assets/Skripts/servercode/src/server.js`

#### Key Changes in `util.js`:
```javascript
function createInitialGameState() {
  return {
    turn: 'SETUP',
    turnNumber: 0,
    isGameOver: false,
    king: { 
      ws: null, 
      resources: { gold: 499, wood: 50, grain: 200 }, 
      submittedPaths: [], 
      purchasedWorkers: 0  // NEW: Track purchased workers
    },
    bandit: { ws: null, resources: { gold: 100, wood: 10, grain: 20 }, submittedAmbushes: [] }
  };
}

// Updated to send worker count in new round data
sendTo(lobby, 'King', { 
  type: 'new_round', 
  payload: { 
    roundNumber: gs.turnNumber, 
    resources: gs.king.resources, 
    workers: gs.king.purchasedWorkers  // NEW: Send worker count
  } 
});
```

#### Key Changes in `server.js`:
```javascript
// New worker buying handler
if (msg.type === 'buy_worker') {
  const grainCost = payload.grainCost || 30;
  const woodCost = payload.woodCost || 10;
  
  if (gs.king.resources.grain >= grainCost && gs.king.resources.wood >= woodCost) {
    gs.king.resources.grain -= grainCost;
    gs.king.resources.wood -= woodCost;
    gs.king.purchasedWorkers += 1;  // NEW: Increment worker count
    sendTo(lobby,'King',{ type:'worker_approved', payload: { workerCount: gs.king.purchasedWorkers } });
    sendTo(lobby,'King',{ type:'resource_update', payload: gs.king.resources });
    log('info', `[Lobby ${lobby.id}] King bought worker #${gs.king.purchasedWorkers}`);
  } else {
    sendTo(lobby,'King',{ type:'worker_denied', payload:{reason:'Insufficient resources'} });
  }
  return;
}
```

### Client-Side Implementation

#### Files Modified:
- `Assets/Skripts/Managers/InteractionManager.cs`
- `Assets/Skripts/Managers/UIManager.cs`
- `Assets/Skripts/Network/NetworkManager.cs`
- `Assets/Skripts/Network/Payloads/NetworkingDTOs.cs`

#### InteractionManager.cs - Core Logic:
```csharp
// NEW: Worker tracking variables
private int currentGrain = 0;
private int currentWood = 0;
private const int workerGrainCost = 30;
private const int workerWoodCost = 10;
private int purchasedWorkers = 0;
private int usedWorkers = 0;

// NEW: Worker buying methods
public bool CanBuyWorker()
public void BuyWorker()
public void OnWorkerPurchaseApproved()
public void OnWorkerPurchaseDenied(string reason)
public void RestorePurchasedWorkers(int workerCount)

// MODIFIED: Path creation now requires workers
public void StartNewPath() // Now checks GetAvailableWorkerCount() > 0
public void ConfirmCurrentPath() // Now increments usedWorkers
public bool CanCreateNewPath() // Now requires available workers
```

#### UIManager.cs - Separate Buttons:
```csharp
// NEW: Added separate confirm button
[SerializeField] private Button kingPathConfirmButton;
[SerializeField] private Button kingWorkerBuyButton;

// NEW: Separate button handlers
private void OnKingWorkerBuyButtonClicked()
private void OnKingPathButtonClicked() // Only handles path creation
private void OnKingPathConfirmButtonClicked() // Only handles path confirmation

// NEW: Button text update methods
public void UpdateKingWorkerBuyButtonText()
public void UpdateKingPathConfirmButtonText()
```

#### NetworkManager.cs - Network Handling:
```csharp
// NEW: Worker purchase message handlers
case "worker_approved":
    InteractionManager.Instance.OnWorkerPurchaseApproved();
    break;

case "worker_denied":
    InteractionManager.Instance.OnWorkerPurchaseDenied("Not enough resources!");
    break;

// MODIFIED: New round handling with worker restoration
case "new_round":
    // ... existing code ...
    if (GameManager.Instance.MyRole == PlayerRole.King && roundPayload.workers > 0)
    {
        InteractionManager.Instance.RestorePurchasedWorkers(roundPayload.workers);
    }
```

#### NetworkingDTOs.cs - Data Transfer:
```csharp
[Serializable]
public class NewRoundPayload
{
    public int roundNumber;
    public ResourcePayload resources;
    public int workers; // NEW: For King's purchased workers
}

[Serializable]
public class BuyWorkerPayload
{
    public int grainCost;
    public int woodCost;
}
```

## Game Flow

### Worker Purchase Flow:
1. King clicks "Worker kaufen" button
2. Client sends `buy_worker` message to server
3. Server validates resources (30 grain + 10 wood)
4. Server deducts resources and increments `purchasedWorkers`
5. Server responds with `worker_approved` or `worker_denied`
6. Client updates UI and worker counts

### Path Creation Flow:
1. King clicks "Pfad erstellen" (only enabled if available workers > 0)
2. King selects resource field and creates path
3. King clicks "Pfad bestätigen" (separate button)
4. System increments `usedWorkers` and decrements available workers
5. King can repeat until no workers available or clicks "Done"

### Round Transition Flow:
1. Server sends `new_round` with `workers: gs.king.purchasedWorkers`
2. Client calls `ForceCompleteReset()` (clears visual elements)
3. Client calls `RestorePurchasedWorkers(workerCount)`:
   - Sets `purchasedWorkers = workerCount` (from server)
   - Resets `usedWorkers = 0` (fresh for new round)
4. King can use all purchased workers again

## UI Structure

### King's Turn Buttons:
- **"Worker kaufen"** - Buy new workers (30 grain + 10 wood each)
- **"Pfad erstellen"** - Start creating a path (requires available workers)
- **"Pfad bestätigen"** - Confirm completed path (consumes 1 worker)
- **"Done"** - End turn

### Button States:
- **Worker Buy Button**: Enabled when King has enough resources
- **Path Create Button**: Enabled when King has available workers and not currently creating
- **Path Confirm Button**: Enabled when path is complete and ready to confirm
- **Done Button**: Always enabled during King's turn

## Key Design Decisions

### Worker Persistence:
- Workers are **permanently owned** once purchased
- Workers **reset to full availability** each round (`usedWorkers = 0`)
- Server is the **source of truth** for worker count

### Separate Buttons:
- **Path Creation** and **Path Confirmation** are separate buttons
- Prevents confusion and makes workflow clearer
- Allows for better state management

### Resource Management:
- Server validates all purchases
- Client displays current state but server enforces limits
- Resources and worker counts are synchronized on new rounds

## Troubleshooting

### Common Issues:
1. **Workers disappearing**: Check if `RestorePurchasedWorkers()` is called in new round handler
2. **Cannot confirm path**: Ensure `UpdateKingPathConfirmButtonText()` is called when path completes
3. **Button states wrong**: Check if all button update methods are called after state changes
4. **Server/client desync**: Verify server sends worker count in new round payload

### Debug Logs to Check:
- `"Worker purchase request sent"`
- `"✅ Worker purchase approved!"`
- `"Workers restored: X purchased, Y used, Z available"`
- `"✅ Path completed! Ready to confirm."`

## Unity Setup Requirements

### Inspector Assignments Needed:
In UIManager GameObject, assign:
- `kingPathButton` - Button for creating paths
- `kingPathConfirmButton` - Button for confirming paths
- `kingWorkerBuyButton` - Button for buying workers

### Network Messages:
The system uses these new network message types:
- `buy_worker` (client → server)
- `worker_approved` (server → client)  
- `worker_denied` (server → client)

## Future Enhancements

### Possible Improvements:
- Add worker count display in UI
- Add worker purchase confirmation dialog
- Add bulk worker purchasing
- Add worker upgrade system
- Add worker specialization types

---

**Last Updated**: August 31, 2025
**Implemented By**: Claude Code Assistant
**Status**: Fully Implemented and Working