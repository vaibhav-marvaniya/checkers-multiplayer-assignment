# Online Checkers Prototype

A minimal 6x6 online turn-based Checkers prototype built with Unity and C#.  
Supports **offline local play** and **online 2–4 players** using Unity Netcode, with basic reconnect/state sync and a simple coin currency.

---

## 1. Project Info

- **Game:** Online Turn-Based Checkers
- **Engine:** Unity **6000.0.55f1 LTS** (required target version)
- **Scripting Backend:** C#
- **Platforms:** Android (primary), PC editor for development
- **Multiplayer:** Unity Netcode for GameObjects + Unity Transport

### Main Features

- 6x6 Checkers board (configurable via `GameConfig` ScriptableObject)
- **Offline mode**: local hot-seat (two human players on one device)
- **Online mode**: **2 or 4 remote players** in one match
- Clear win/lose conditions (no pieces / no legal moves)
- Centralised game logic in pure C# model (deterministic)
- Host-authoritative netcode:
  - Clients send move requests
  - Host validates and applies rules
- **State snapshot + reconnect**:
  - Newly joined / reconnected clients request a full board snapshot
  - Board, scores, current player, and game-over state are synced
- Local **coin currency**:
  - Stored in `PlayerPrefs` (`Checkers_Coins`)
  - Coins awarded when Player 1 wins (offline / host win path)
- Scriptable configuration using `GameConfig` (board size, rows per side, coins per win)

---

## 2. Folder / Code Structure

Project root (Unity `Assets`):

- `Scenes/`
  - `MainMenu.unity`
  - `GameSceneOffline.unity`
  - `GameSceneOnline.unity`
- `Scripts/`
  - `Config/`
    - `GameConfig.cs`
  - `Controller/`
    - `GameController.cs` (offline game flow)
    - `MainMenu.cs` (menu + scene loading)
  - `Factory/`
    - `BoardFactory.cs` (instantiates tiles and pieces)
  - `Model/`
    - `BoardState.cs` (board grid & piece positions)
    - `GameModel.cs` (turn logic, scores, events)
    - `GameTypes.cs` (PlayerId, PieceType, Move, Position)
    - `Rules.cs` (board layout + standard checkers rules)
  - `NetCode/`
    - `NetworkGameController.cs` (host/client game logic)
    - `NetworkBootstrap.cs` (host/client UI & discovery)
    - `BoardSnapshot.cs` (full board state over network)
    - `MoveData.cs` (network-serializable move)
  - `Progress/`
    - `PlayerProgress.cs` (coin persistence using `PlayerPrefs`)
  - `View/`
    - `BoardView.cs` (visual board grid / piece rendering)
    - `TileView.cs` (clickable tile with highlighting)
    - `PieceView.cs` (piece sprite & appearance)
- `Data/`
  - `CurrentGameConfig.asset` (instance of `GameConfig`)

---

## 3. How to Open & Run

### 3.1 Prerequisites

- Unity **6000.0.55f1 LTS** (or compatible 6.x editor)
- Android SDK / NDK components installed via Unity Hub
- Git (if pulling from a repository)

### 3.2 Opening the Project

1. Open Unity Hub.
2. Click **Add** and select the project folder.
3. Open the project using **6000.0.55f1 LTS**.
4. Load the `MainMenu` scene:
   - `Assets/Scenes/MainMenu.unity`

---

## 4. Play Instructions

### 4.1 Main Menu

- **Offline Play** → `GameSceneOffline`
- **Online Play** → `GameSceneOnline`
- **Quit** → exits application (on desktop / Android back behaviour)

### 4.2 Controls (Common)

- Tap / Click on a piece to select it.
- All valid destination tiles for that piece are highlighted.
- Tap / Click on a highlighted tile to move.
- Turns alternate between players; UI shows `Turn: PlayerX`.
- Game-over text shows when a winner is decided.

### 4.3 Offline Mode

- Scene: `GameSceneOffline`
- Behaviour:
  - Two local players share the same screen.
  - Both take turns on the same device.
- When **Player 1** wins:
  - Coins are awarded via `PlayerProgress.AddCoins(gameConfig.coins)`.
  - Coin count is displayed in the UI (e.g., `Coins: 20`).

### 4.4 Online Mode (2–4 Players)

- Scene: `GameSceneOnline`
- The scene contains:
  - `NetworkManager` (Unity Netcode)
  - `NetworkBootstrap` (UI + connection)
  - `NetworkGameController` (game logic)
- Flow:
  1. One device selects **Host**, others select **Client**.
  2. Host chooses **2 players** or **4 players** (team layout is handled internally).
  3. Clients connect using Unity Transport (LAN/IPv4 discovery; configurable).
  4. Once enough players are connected and the host starts the match:
     - Each client is assigned a **seat** (Player1–Player4).
     - Seats are mapped to two teams (1+3 vs 2+4).
  5. Only the host applies game rules; clients send `MoveData` to host.
  6. Host broadcasts validated updates via `ClientRpc` and `BoardSnapshot`.

- **Reconnect / Late Join:**
  - When a client connects to an ongoing match, it sends `RequestSnapshotServerRpc`.
  - Host builds a `BoardSnapshot` from current `GameModel` state.
  - Client applies the snapshot and fully syncs the board, scores, and turn info.

---

## 5. Build Instructions (Android APK)

1. Open **File → Build Settings…**
2. Switch platform to **Android**.
3. Add the following scenes to **Scenes In Build** (in order):
   1. `MainMenu`
   2. `GameSceneOffline`
   3. `GameSceneOnline`
4. Recommended build settings:
   - **Scripting Backend:** IL2CPP
   - **Target Architectures:** ARMv7 + ARM64
   - Enable **Strip Engine Code**
5. (Optional but recommended for profiling):
   - Tick **Development Build**
   - Tick **Autoconnect Profiler**
6. Click **Build**, choose an output folder.
7. Install the APK on device (via `adb install` or drag-and-drop in Android File Transfer).

---

## 6. Coin / Currency & IAP Test Details

- **Type:** Local soft currency only (no real-money IAP).
- **Storage:** `PlayerPrefs` under key `Checkers_Coins`.
- **Earning:**
  - Offline mode: Player 1 earns `GameConfig.coins` on each win.
  - Online mode (host path): can be extended similarly (currently basic).
- **Spending:** Not implemented in this prototype (coins are accumulated only).

---

## 7. Known Limitations / Future Work

- No visual indication of multi-jump moves or kinging (if not implemented).
- Matchmaking is LAN/manual only; no lobby service backend.
- Limited device/platform coverage tested so far.
