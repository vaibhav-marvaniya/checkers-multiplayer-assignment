# DesignDoc – Online Checkers Prototype

## 1. Overview

This document describes the **gameplay rules**, **system design**, and **coin logic** for the Online Turn-Based Checkers prototype built in Unity.

The goal is to provide a minimal but scalable architecture supporting:

- A configurable Checkers board (default 6x6)
- Offline two-player mode (local hot-seat)
- Online 2 or 4 player matches
- Deterministic rules, host-authoritative networking
- Basic reconnect with board state snapshot
- A simple persistent coin system

---

## 2. Gameplay Design

### 2.1 Board & Pieces

- **Default board size:** 6 rows × 6 columns (`GameConfig.rows`, `GameConfig.cols`)
- **Rows per side with starting pieces:** `GameConfig.rowsPerSide` (default 2)
- **Piece types:**
  - `None`
  - `P1_Man`, `P2_Man` (and optionally future king types)

Pieces are set up using `StandardBoardLayout`:

- Player 1 pieces occupy the top `rowsPerSide` rows on dark tiles.
- Player 2 pieces occupy the bottom `rowsPerSide` rows on dark tiles.

### 2.2 Turn Flow

1. Game starts with `CurrentPlayer = Player1`.
2. On a player’s turn:
   - Player selects a tile with one of their pieces.
   - All valid moves from that tile are calculated and highlighted.
   - Player selects a destination tile; if it matches a valid move, the move is applied.
3. Turn switches to the **other team** (`Player1 ↔ Player2`), even in 4-seat configuration (seats 1&3 vs 2&4).
4. Events from `GameModel` notify UI:
   - `OnMoveApplied`
   - `OnTurnChanged`
   - `OnScoreChanged`
   - `OnGameOver`
   - `OnBoardReset`

### 2.3 Movement Rules (StandardCheckersRuleSet)

- Regular pieces move diagonally forward:
  - Player 1 moves “down” the board.
  - Player 2 moves “up” the board.
- A *non-capture* move:
  - One diagonal step into an empty tile.
- A *capture* move:
  - Jump over exactly one enemy piece diagonally into an empty tile.
  - The captured piece is removed.
- `StandardCheckersRuleSet` exposes:
  - `GetValidMovesFrom(BoardState board, Position from, PlayerId player)`
  - `IsWin(BoardState board, PlayerId currentPlayer, out PlayerId winner)`

(If kinging / multi-jumps are added, they plug into this rule set.)

### 2.4 Win / Lose Conditions

`IsWin` considers:

- A player **loses** if they:
  - Have **no remaining pieces**, or
  - Have **no valid moves** while the other player still has at least one valid move.
- Game over state:
  - Sets `_isGameOver = true` in controllers.
  - Triggers `OnGameOver(winner)` event → updates Game Over UI.
  - In offline, awards coins to Player 1 when they win.

---

## 3. Game Modes

### 3.1 Offline Mode – Local Hot-Seat

- Scene: `GameSceneOffline`
- Orchestrated by `GameController`.
- Both players take turns on a single device:
  - `BoardView` + `TileView` handle user input and selection.
  - `GameModel` enforces rules and raises events.
- No network code involved.
- Coin rewards:
  - If `winner == PlayerId.Player1`, `PlayerProgress.AddCoins(gameConfig.coins)`.

### 3.2 Online Mode – 2 or 4 Players

- Scene: `GameSceneOnline`
- Uses:
  - `NetworkManager` (Unity Netcode for GameObjects)
  - `NetworkBootstrap` (connection UI, host/client flow)
  - `NetworkGameController` (networked game logic)

#### 3.2.1 Player Seats and Teams

- `PlayerId` enum: `Player1`, `Player2`, `Player3`, `Player4`.
- Seat-to-team mapping:
  - Team A: `Player1` + `Player3`
  - Team B: `Player2` + `Player4`
- The game logic (`GameModel`) still treats this as **two logical players** (two teams), with seats as visual/ownership roles.

#### 3.2.2 Network Flow (High-Level)

1. Host:
   - Presses **Host** in `NetworkBootstrap`.
   - Selects 2 or 4 players.
   - Starts the game.
2. Clients:
   - Press **Client**.
   - Connect to host via Unity Transport (LAN).
3. On host:
   - Creates the `BoardState` and initial layout via `StandardBoardLayout`.
   - Creates `GameModel` with `StandardCheckersRuleSet`.
   - Marks the match as **started**.
4. On clients:
   - `NetworkGameController` requests a board snapshot if joining late.
   - Applies snapshot to reconstruct local `GameModel` state.

#### 3.2.3 Moves & Authority

- Clients:
  - Handle local input (select tile, choose destination).
  - Pack move into `MoveData`.
  - Send move to server via `ServerRpc`.
- Host:
  - Validates the move using `GameModel.GetValidMovesFrom`.
  - Applies it if valid.
  - Broadcasts resulting state changes to all clients:
    - Either via granular updates, or
    - Via `BoardSnapshot` for full sync.

This ensures **deterministic** and centralised decision-making on the host.

---

## 4. System Architecture

### 4.1 Model Layer (Pure Game Logic)

Namespace: `Checkers.Model`

- **BoardState**
  - Stores a 2D array of `PieceType`.
  - Exposes `GetPiece`, `SetPiece`, `IsInsideBoard`.
- **GameModel**
  - Wraps `BoardState` + `IRuleSet`.
  - Maintains scores: `ScorePlayer1`, `ScorePlayer2`.
  - Maintains `CurrentPlayer`.
  - Validates moves and applies them to the board.
  - Emits events for UI and controllers.
- **GameTypes**
  - `PlayerId`, `PieceType`, `Move`, `Position`.
- **Rules**
  - `IBoardLayout`: `SetupInitialBoard(BoardState board)`.
  - `IRuleSet`: `GetValidMovesFrom`, `IsWin`.
  - `StandardBoardLayout`: sets initial pieces.
  - `StandardCheckersRuleSet`: implements checkers rules.

### 4.2 View Layer

Namespace: `Checkers.View`

- **BoardView**
  - Owns containers for tiles and pieces (`tilesParent`, `piecesParent`).
  - Uses `BoardFactory` to instantiate tiles and pieces.
  - Translates grid coordinates (`Position`) to world positions.
  - Maintains arrays of `TileView` and `PieceView`.
  - Highlights valid moves and selected tiles.
  - Delegates clicks to an `IBoardInputHandler` (controller).

- **TileView**
  - Handles click interaction.
  - Stores `GridPosition`.
  - Toggles visual state: base / selected / move-highlight.

- **PieceView**
  - Stores `PieceType`.
  - Updates sprite/visuals based on the piece type.

### 4.3 Controller Layer

Namespace: `Checkers.Controller`

- **GameController (Offline)**
  - Implements `IBoardInputHandler`.
  - Listens to tile clicks from `BoardView`.
  - Manages selection and move attempts.
  - Subscribes to `GameModel` events:
    - Updates score text, turn text, game-over text.
  - Awards coins via `PlayerProgress` when Player 1 wins.
  - Handles restart (back to `MainMenu`).

- **MainMenu**
  - Simple scene controller:
    - `PlayOffline()` → loads `GameSceneOffline`.
    - `PlayOnline()` → loads `GameSceneOnline`.

### 4.4 Netcode Layer

Namespace: `Checkers.Netcode`

- **NetworkBootstrap**
  - Displays host/client buttons and status label.
  - Handles player count selection (2 or 4 players).
  - Starts/stops hosting or client sessions.
  - Manages some connection/discovery logic and status messages.

- **NetworkGameController**
  - `NetworkBehaviour` implementing `IBoardInputHandler`.
  - Host responsibilities:
    - Initialises `BoardState` + `GameModel`.
    - Owns the authoritative instance of the game.
    - Handles all validated moves and rules.
    - Builds and sends `BoardSnapshot` to clients.
  - Client responsibilities:
    - Sends move commands to host.
    - Applies snapshots / updates.
    - Reflects game state visually (board, scores, turn).
  - Maintains mapping from `ClientId` to `PlayerId` seat.
  - Uses `BoardSnapshot` and `MoveData` structs for network serialization.

- **BoardSnapshot**
  - Contains:
    - Board dimensions
    - Flattened `PieceType[]`
    - Scores
    - `CurrentPlayer`
    - Game status flags (`IsGameOver`, `IsGameStarted`)
  - Network-serializable via `INetworkSerializable`.

- **MoveData**
  - Minimal move representation:
    - `FromRow`, `FromCol`, `ToRow`, `ToCol`.
  - Converted to/from `Move` for `GameModel`.

### 4.5 Progress & Coins

Namespace: `Checkers.Progress`

- **PlayerProgress**
  - Static class wrapping `PlayerPrefs`.
  - Key: `"Checkers_Coins"`.
  - `Coins` property:
    - `get`: reads from PlayerPrefs with default `0`.
    - `set`: writes and saves PlayerPrefs.
  - `AddCoins(int amount)`:
    - Ignores non-positive values.
    - Adds to existing `Coins`.
  - Used by:
    - `GameController` / `NetworkGameController` to award coins.

### 4.6 Configuration

Namespace: `Checkers.Config`

- **GameConfig (ScriptableObject)**
  - Fields:
    - `rows`, `cols`
    - `rowsPerSide`
    - `coins` (reward per win)
  - Stored as `CurrentGameConfig.asset` in `Data/`.
  - Used by controllers to:
    - Determine board size.
    - Determine initial rows of pieces.
    - Set coins awarded to winners.

---

## 5. Logging & Error Handling

- Logging uses `Debug.Log` extensively in `NetworkGameController` and `NetworkBootstrap` for:
  - Connection events
  - Player seat assignments
  - Snapshot application
  - Errors (e.g., missing `NetworkManager`)
- Failure cases:
  - If `NetworkManager.Singleton` is missing → show UI status and log error.
  - If invalid moves are received from clients → they are ignored on host side.

Future improvement: move repeated log messages to a small logging utility and add log levels.

---

## 6. Determinism & Sync Strategy

- **Deterministic Logic:**
  - All game rules are based on `BoardState` (C# only).
  - No physics or frame-rate dependent logic in core decisions.
- **Host Authority:**
  - Only host applies rules and updates the board.
  - Clients do not simulate the board independently.
- **Snapshots:**
  - On join/reconnect, a full snapshot is sent to ensure correct state.
  - Reduces risk of desyncs compared to incremental-only updates.

---

## 7. Future Extensions

- Visual kinging, multi-jump capture rules, and advanced rule variants.
- Proper online matchmaking / lobby system.
- Store UI for spending coins (cosmetics, themes).
- Deeper analytics for win rates and behaviour.
- More robust error screens and user feedback for disconnects.