using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Checkers.Model;
using Checkers.View;
using Checkers.Controller;
using Checkers.Config;
using Checkers.Progress;
using UnityEngine.SceneManagement;

namespace Checkers.Netcode
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkGameController : NetworkBehaviour, IBoardInputHandler
    {
        [Header("Config")]
        [SerializeField] private GameConfig gameConfig;

        [Header("Board Settings")]
        [SerializeField] private int rows = 6;
        [SerializeField] private int cols = 6;

        [Header("References")]
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text roleText;

        [Header("Multiplayer Settings")]
        [SerializeField] private int defaultMaxPlayers = 2;
        private int _maxActivePlayers = 2;

        private GameModel _gameModel;

        private BoardState _clientBoardState;

        private PlayerId _localPlayerId = PlayerId.Player1;
        private PlayerId _currentTurn = PlayerId.Player1;
        private bool _isSpectator;
        private bool _isGameOver;
        private bool _isGameStarted;

        private Position? _selectedPosition;
        private readonly List<Move> _currentValidMoves = new List<Move>();

        private readonly Dictionary<ulong, PlayerId> _serverSeats = new Dictionary<ulong, PlayerId>();

        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text statusText;


        private PlayerId GetTeamForSeat(PlayerId seat)
        {
            if (seat == PlayerId.Player1 || seat == PlayerId.Player3)
                return PlayerId.Player1; 
            if (seat == PlayerId.Player2 || seat == PlayerId.Player4)
                return PlayerId.Player2;  

            return PlayerId.Player1;
        }

        private PlayerId NextSeatClockwise(PlayerId seat)
        {
            switch (seat)
            {
                case PlayerId.Player1: return PlayerId.Player2;
                case PlayerId.Player2:
                    return (_maxActivePlayers == 2) ? PlayerId.Player1 : PlayerId.Player3;
                case PlayerId.Player3: return PlayerId.Player4;
                case PlayerId.Player4: return PlayerId.Player1;
                default: return PlayerId.Player1;
            }
        }

        private bool IsSeatOccupied(PlayerId seat)
        {
            foreach (var kvp in _serverSeats)
            {
                if (kvp.Value == seat)
                    return true;
            }
            return false;
        }

        private PlayerId ResolveNextSeatForTeam(PlayerId team)
        {
            var startSeat = _currentTurn;
            if (_currentTurn != PlayerId.Player1 &&
                _currentTurn != PlayerId.Player2 &&
                _currentTurn != PlayerId.Player3 &&
                _currentTurn != PlayerId.Player4)
            {
                startSeat = PlayerId.Player1;
            }

            PlayerId candidate = startSeat;
            for (int i = 0; i < 4; i++)
            {
                candidate = NextSeatClockwise(candidate);

                if (GetTeamForSeat(candidate) != team)
                    continue;

                if (!IsSeatOccupied(candidate))
                    continue;

                return candidate;
            }

            foreach (var kvp in _serverSeats)
            {
                if (GetTeamForSeat(kvp.Value) == team)
                    return kvp.Value;
            }

            return PlayerId.Player1;
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NGC] OnNetworkSpawn on client {NetworkManager.Singleton.LocalClientId}, IsServer={IsServer}, IsOwner={IsOwner}");

            _isGameStarted = false;
            _isGameOver = false;
            _selectedPosition = null;
            _currentValidMoves.Clear();
            _isSpectator = false;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                if (IsServer)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                }
            }

            if (IsServer)
            {
                // Multiplayer mode: read from NetworkBootstrap if available
                _maxActivePlayers = defaultMaxPlayers;
                try
                {
                    // In case NetworkBootstrap doesn't exist in some scenes, we guard with try
                    _maxActivePlayers = Mathf.Clamp(NetworkBootstrap.SelectedPlayerCount, 2, 4);
                }
                catch
                {
                    // Fallback to inspector value
                    _maxActivePlayers = Mathf.Clamp(defaultMaxPlayers, 2, 4);
                }

                Debug.Log($"[NGC] Host using maxActivePlayers = {_maxActivePlayers}");

                _localPlayerId = PlayerId.Player1;
                _currentTurn = PlayerId.Player1;
                _isSpectator = false;

                _serverSeats.Clear();
                if (NetworkManager.Singleton != null)
                {
                    ulong hostId = NetworkManager.Singleton.LocalClientId;
                    _serverSeats[hostId] = PlayerId.Player1;
                }

                SetupGameAsHost();

                if (gameOverText != null)
                    gameOverText.text = "Waiting for Player 2 to join.";

                UpdateRoleText();
            }
            else
            {
                _localPlayerId = PlayerId.Player2;
                _currentTurn = PlayerId.Player1;
                _isSpectator = true;

                SetupBoardAsClient();
                if (gameOverText != null)
                    gameOverText.text = "Waiting for host to start...";

                UpdateRoleText();
                RequestSnapshotServerRpc();
            }
            UpdateCoinsUI();
        }


        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void SetupGameAsHost()
        {
            Debug.Log("[NGC] SetupGameAsHost");

            int r = gameConfig != null ? gameConfig.rows : rows;
            int c = gameConfig != null ? gameConfig.cols : cols;
            int rowsPerSide = gameConfig != null ? gameConfig.rowsPerSide : 2;

            var boardState = new BoardState(r, c);
            var layout = new StandardBoardLayout(rowsPerSide: rowsPerSide);
            var rules = new StandardCheckersRuleSet();

            _gameModel = new GameModel(boardState, rules);
            _gameModel.OnMoveApplied += OnMoveAppliedHost;
            _gameModel.OnTurnChanged += OnTurnChangedHost;
            _gameModel.OnGameOver += OnGameOverHost;
            _gameModel.OnBoardReset += OnBoardResetHost;
            _gameModel.OnScoreChanged += OnScoreChangedHost;

            _isGameOver = false;

            _boardView.Init(boardState, this);
            _boardView.SetPerspectiveForLocalPlayer(PlayerId.Player1);
            _gameModel.ResetBoard(layout);
        }

        private void SetupBoardAsClient()
        {
            Debug.Log("[NGC] SetupBoardAsClient (client visual-only board)");

            int r = gameConfig != null ? gameConfig.rows : rows;
            int c = gameConfig != null ? gameConfig.cols : cols;
            int rowsPerSide = gameConfig != null ? gameConfig.rowsPerSide : 2;

            _clientBoardState = new BoardState(r, c);
            var layout = new StandardBoardLayout(rowsPerSide: rowsPerSide);
            layout.SetupInitialBoard(_clientBoardState);

            _boardView.Init(_clientBoardState, this);
            _isGameOver = false;

            if (turnText != null)
                turnText.text = $"Turn: {PlayerId.Player1}";
            if (scoreText != null)
                scoreText.text = "0 : 0";
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer || NetworkManager.Singleton == null)
                return;

            if (clientId == NetworkManager.Singleton.LocalClientId)
                return;

            CleanupServerSeats();
            LogSeatMap($"[NGC] Host: after cleanup, before assigning client {clientId}");

            Debug.Log($"[NGC] Host: client {clientId} connected.");

            bool isSpectatorForClient = false;
            PlayerId assignedSeat = PlayerId.Player2; // default

            // Decide which seats are allowed for this match
            List<PlayerId> allowedSeats = new List<PlayerId> { PlayerId.Player2 };
            if (_maxActivePlayers >= 4)
            {
                allowedSeats.Add(PlayerId.Player3);
                allowedSeats.Add(PlayerId.Player4);
            }

            // Find first free allowed seat
            PlayerId freeSeat = PlayerId.Player1;
            bool foundSeat = false;
            foreach (var seat in allowedSeats)
            {
                if (!IsSeatOccupied(seat))
                {
                    freeSeat = seat;
                    foundSeat = true;
                    break;
                }
            }

            if (foundSeat)
            {
                assignedSeat = freeSeat;
                _serverSeats[clientId] = assignedSeat;
                isSpectatorForClient = false;

                Debug.Log($"[NGC] Host: assigning client {clientId} as seat {assignedSeat} (team {GetTeamForSeat(assignedSeat)}).");
            }
            else
            {
                // All active seats full => spectator
                isSpectatorForClient = true;
                Debug.Log($"[NGC] Host: assigning client {clientId} as Spectator (all player seats full).");
            }

            LogSeatMap($"[NGC] Host: after assigning client {clientId}");
            AssignRoleClientRpc(clientId, assignedSeat, isSpectatorForClient);

            // Determine if we have at least one opponent (team 2) seated -> start game if not already
            if (!_isGameStarted && !isSpectatorForClient)
            {
                bool shouldStart = false;

                if (_maxActivePlayers <= 2)   // 2-player mode
                {
                    // Start as soon as we have at least one Team2 seat
                    bool hasTeam2 = false;
                    foreach (var kvp in _serverSeats)
                    {
                        if (GetTeamForSeat(kvp.Value) == PlayerId.Player2)
                        {
                            hasTeam2 = true;
                            break;
                        }
                    }

                    shouldStart = hasTeam2;
                }
                else // 4-player mode
                {
                    // Start only when all four seats Player1..Player4 are occupied
                    bool seat1 = IsSeatOccupied(PlayerId.Player1);
                    bool seat2 = IsSeatOccupied(PlayerId.Player2);
                    bool seat3 = IsSeatOccupied(PlayerId.Player3);
                    bool seat4 = IsSeatOccupied(PlayerId.Player4);

                    // Host is always seat1, but we keep the check for safety
                    bool allSeatsFilled = seat1 && seat2 && seat3 && seat4;
                    shouldStart = allSeatsFilled;

                    Debug.Log($"[NGC] Host: check 4-player start -> P1={seat1}, P2={seat2}, P3={seat3}, P4={seat4}, allFilled={allSeatsFilled}");
                }

                if (shouldStart)
                {
                    _isGameStarted = true;
                    Debug.Log("[NGC] Host: required players joined, game is starting.");

                    GameStartedClientRpc();

                    if (gameOverText != null)
                        gameOverText.text = "";
                }
                else
                {
                    Debug.Log("[NGC] Host: not enough players yet, waiting.");
                }
            }
            else if (_isGameStarted)
            {
                Debug.Log("[NGC] Host: late joiner detected, re-sending GameStartedClientRpc.");
                GameStartedClientRpc();
            }

        }


        private void OnClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null)
                return;

            if (IsServer)
            {
                if (clientId == NetworkManager.Singleton.LocalClientId)
                    return;

                if (_serverSeats.TryGetValue(clientId, out var seatPlayer))
                {
                    Debug.Log($"[NGC] Host: client {clientId} with role {seatPlayer} disconnected, freeing slot.");
                    _serverSeats.Remove(clientId);

                    int countTeam1 = 0;
                    int countTeam2 = 0;
                    foreach (var kvp in _serverSeats)
                    {
                        var team = GetTeamForSeat(kvp.Value);
                        if (team == PlayerId.Player1) countTeam1++;
                        else if (team == PlayerId.Player2) countTeam2++;
                    }

                    if (countTeam2 == 0)
                    {
                        _isGameStarted = false;
                        _isGameOver = false;
                        _selectedPosition = null;
                        _currentValidMoves.Clear();

                        if (gameOverText != null)
                            gameOverText.text = "Waiting for Player 2 to join.";
                        UpdateStatusText("Opponent disconnected, waiting for Player 2 to join.");

                        WaitingForPlayer2ClientRpc();
                    }
                    else
                    {
                        Debug.Log($"[NGC] Host: still have {countTeam2} Team2 humans, game continues.");
                    }

                }
                else
                {
                    Debug.Log($"[NGC] Host: spectator client {clientId} disconnected.");
                }

                return;
            }

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[NGC] Client: disconnected from host, shutting down and loading MainMenu.");

                if (NetworkManager.Singleton.IsListening ||
                    NetworkManager.Singleton.IsServer ||
                    NetworkManager.Singleton.IsClient)
                {
                    NetworkManager.Singleton.Shutdown();
                }

                Object.Destroy(NetworkManager.Singleton.gameObject);

                _isGameStarted = false;
                _isGameOver = false;
                _selectedPosition = null;
                _currentValidMoves.Clear();

                SceneManager.LoadScene("MainMenu");
            }
        }

        [ClientRpc]
        private void WaitingForPlayer2ClientRpc()
        {
            if (IsServer)
                return;

            _isGameStarted = false;
            _isGameOver = false;
            _selectedPosition = null;
            _currentValidMoves.Clear();
            _currentTurn = PlayerId.Player1;

            if (gameOverText != null)
                gameOverText.text = "Waiting for Player 2 to join...";
            UpdateStatusText("Waiting for Player 2 to join...");
        }


        [ClientRpc]
        private void AssignRoleClientRpc(ulong targetClientId, PlayerId role, bool isSpectator)
        {
            if (NetworkManager.Singleton == null)
                return;

            if (NetworkManager.Singleton.LocalClientId != targetClientId)
                return;

            Debug.Log($"[NGC] Client {NetworkManager.Singleton.LocalClientId}: assigned role={role}, isSpectator={isSpectator}");

            _localPlayerId = role;
            _isSpectator = isSpectator;
            if (!_isSpectator)
            {
                _boardView.SetPerspectiveForLocalPlayer(_localPlayerId);
            }
            UpdateRoleText();
        }

        [ClientRpc]
        private void GameStartedClientRpc()
        {
            if (IsServer)
                return;

            Debug.Log($"[NGC] Client {NetworkManager.Singleton.LocalClientId}: GameStartedClientRpc received.");

            _isGameStarted = true;

            if (gameOverText != null)
                gameOverText.text = "";
            UpdateStatusText("Game started.");
        }

        private void UpdateRoleText()
        {
            if (roleText == null)
                return;

            if (_isSpectator)
            {
                roleText.text = "You are: Spectator";
                return;
            }

            switch (_localPlayerId)
            {
                case PlayerId.Player1:
                    roleText.text = "You are: Player 1";
                    break;
                case PlayerId.Player2:
                    roleText.text = "You are: Player 2";
                    break;
                case PlayerId.Player3:
                    roleText.text = "You are: Player 3";
                    break;
                case PlayerId.Player4:
                    roleText.text = "You are: Player 4";
                    break;
                default:
                    roleText.text = "You are: Unknown";
                    break;
            }
        }


        public void HandleTileClicked(Position pos)
        {
            if (_isGameOver)
                return;

            if (!_isGameStarted)
            {
                Debug.Log("[NGC] Click ignored: game not started yet (waiting for other player).");
                return;
            }

            if (_isSpectator)
            {
                Debug.Log("[NGC] Click ignored: you are a spectator.");
                return;
            }

            if (_localPlayerId != _currentTurn)
            {
                Debug.Log($"[NGC] Click ignored: local player={_localPlayerId}, but it is {_currentTurn}'s turn.");
                return;
            }

            if (IsServer)
            {
                HandleTileClickedAsServer(pos, NetworkManager.Singleton.LocalClientId);
            }
            else
            {
                HandleTileClickedAsClient(pos);
            }
        }

        private void HandleTileClickedAsServer(Position pos, ulong senderClientId)
        {
            if (_gameModel == null)
                return;

            if (!TryGetPlayerForClient(senderClientId, out var playerForClient))
            {
                Debug.Log($"[NGC] Server: client {senderClientId} has no player seat (spectator?), ignoring input.");
                return;
            }

            if (playerForClient != _gameModel.CurrentPlayer)
            {
                Debug.LogWarning( $"[NGC] Potential desync: client {senderClientId} mapped to {playerForClient}, " + $"but current turn is {_gameModel.CurrentPlayer}. Ignoring move.");
                return;
            }


            if (!_selectedPosition.HasValue)
            {
                TrySelectPiece(pos);
            }
            else
            {
                if (pos.Row == _selectedPosition.Value.Row && pos.Col == _selectedPosition.Value.Col)
                {
                    Deselect();
                    return;
                }

                var move = _currentValidMoves.Find(m => m.To.Row == pos.Row && m.To.Col == pos.Col);
                if (move != null)
                {
                    Debug.Log($"[NGC] Server: trying move from {_selectedPosition.Value.Row},{_selectedPosition.Value.Col} to {pos.Row},{pos.Col}");
                    if (_gameModel.TryApplyMove(move))
                    {
                        Deselect();
                    }
                    else
                    {
                        Debug.Log("[NGC] Server: TryApplyMove failed (invalid move).");
                        Deselect();
                    }
                }
                else
                {
                    TrySelectPiece(pos);
                }
            }
        }

        private bool TryGetPlayerForClient(ulong clientId, out PlayerId playerId)
        {
            playerId = PlayerId.Player1;
            if (!IsServer)
                return false;

            return _serverSeats.TryGetValue(clientId, out playerId);
        }

        private void HandleTileClickedAsClient(Position pos)
        {
            if (_localPlayerId != _currentTurn)
            {
                Debug.Log($"[NGC] Client {_localPlayerId}: click ignored, not your turn. CurrentTurn={_currentTurn}");
                return;
            }

            if (_clientBoardState == null)
                return;

            if (!_selectedPosition.HasValue)
            {
                var piece = _clientBoardState.GetPiece(pos);
                var localTeam = GetTeamForSeat(_localPlayerId);
                if (!OwnsPiece(localTeam, piece))
                {
                    Debug.Log($"[NGC] Client {_localPlayerId}: cannot select this piece (belongs to other player or empty).");
                    return;
                }

                _selectedPosition = pos;
                _boardView.ClearHighlights();
                _boardView.SelectTile(pos);
                _currentValidMoves.Clear();

                var ruleSet = new StandardCheckersRuleSet();
                var localTeamForRules = GetTeamForSeat(_localPlayerId);
                _currentValidMoves.AddRange(
                    ruleSet.GetValidMovesFrom(_clientBoardState, pos, localTeamForRules)
                );

                var moveTargets = new List<Position>();
                foreach (var m in _currentValidMoves)
                    moveTargets.Add(m.To);

                _boardView.HighlightPositions(moveTargets);
            }
            else
            {
                if (pos.Row == _selectedPosition.Value.Row && pos.Col == _selectedPosition.Value.Col)
                {
                    Deselect();
                    return;
                }

                var from = _selectedPosition.Value;
                var moveData = new MoveData(from.Row, from.Col, pos.Row, pos.Col);

                Debug.Log($"[NGC] Client {NetworkManager.Singleton.LocalClientId} sending RequestMoveServerRpc: " +
                          $"{from.Row},{from.Col} -> {pos.Row},{pos.Col} (localPlayer={_localPlayerId}, currentTurn={_currentTurn})");

                RequestMoveServerRpc(moveData);

                Deselect();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestMoveServerRpc(MoveData moveData, ServerRpcParams rpcParams = default)
        {
            if (_isGameOver || _gameModel == null)
                return;

            if (!_isGameStarted)
                return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryGetPlayerForClient(senderClientId, out var playerForClient))
            {
                Debug.Log($"[NGC] ServerRpc from client {senderClientId} ignored: no player seat (spectator?).");
                return;
            }

            Debug.Log($"[NGC] ServerRpc from client {senderClientId}: " +
                      $"{moveData.FromRow},{moveData.FromCol} -> {moveData.ToRow},{moveData.ToCol} " +
                      $"mapped to player {playerForClient}, current player is {_gameModel.CurrentPlayer}");

            var teamForClient = GetTeamForSeat(playerForClient);
            if (teamForClient != _gameModel.CurrentPlayer)
            {
                Debug.LogWarning($"[NGC] Potential desync: client {senderClientId} mapped to seat {playerForClient} (team {teamForClient}), " +
                                 $"but current team turn is {_gameModel.CurrentPlayer}. Ignoring move.");
                return;
            }


            var move = moveData.ToMove();

            if (_gameModel.TryApplyMove(move))
            {
                Debug.Log("[NGC] Server: move accepted by GameModel.");
            }
            else
            {
                Debug.Log("[NGC] Server: move rejected by GameModel.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSnapshotServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer || _gameModel == null)
                return;

            var snapshot = BuildSnapshotOnHost();

            ApplySnapshotClientRpc(snapshot);
        }

        [ClientRpc]
        private void ApplySnapshotClientRpc(BoardSnapshot snapshot)
        {
            if (IsServer)
                return;

            Debug.Log($"[NGC] Client {NetworkManager.Singleton.LocalClientId}: ApplySnapshotClientRpc received.");
            ApplySnapshotOnClient(snapshot);
        }

        private void TrySelectPiece(Position pos)
        {
            var piece = _gameModel.Board.GetPiece(pos);
            if (!OwnsPiece(_gameModel.CurrentPlayer, piece))
            {
                return;
            }

            _selectedPosition = pos;
            _boardView.SelectTile(pos);

            _currentValidMoves.Clear();
            _currentValidMoves.AddRange(_gameModel.GetValidMovesFrom(pos));

            var moveTargets = new List<Position>();
            foreach (var m in _currentValidMoves)
                moveTargets.Add(m.To);

            _boardView.HighlightPositions(moveTargets);
        }

        private void Deselect()
        {
            _selectedPosition = null;
            _currentValidMoves.Clear();
            _boardView.ClearHighlights();
            _boardView.ClearSelection();
        }

        private bool OwnsPiece(PlayerId player, PieceType piece)
        {
            if (player == PlayerId.Player1)
                return piece == PieceType.P1_Man || piece == PieceType.P1_King;
            if (player == PlayerId.Player2)
                return piece == PieceType.P2_Man || piece == PieceType.P2_King;
            return false;
        }

        private BoardSnapshot BuildSnapshotOnHost()
        {
            int r = _gameModel != null ? _gameModel.Board.Rows : rows;
            int c = _gameModel != null ? _gameModel.Board.Cols : cols;

            var snapshot = new BoardSnapshot
            {
                Rows = r,
                Cols = c,
                ScorePlayer1 = _gameModel != null ? _gameModel.ScorePlayer1 : 0,
                ScorePlayer2 = _gameModel != null ? _gameModel.ScorePlayer2 : 0,
                CurrentPlayer = _currentTurn,
                IsGameOver = _isGameOver,
                IsGameStarted = _isGameStarted
            };

            int total = r * c;
            snapshot.Pieces = new PieceType[total];

            int hash = ComputeBoardHash(snapshot.Rows, snapshot.Cols, snapshot.Pieces);
            Debug.Log($"[NGC] Host: BuildSnapshotOnHost -> hash={hash}, " +
                      $"ScoreP1={snapshot.ScorePlayer1}, ScoreP2={snapshot.ScorePlayer2}, " +
                      $"CurrentPlayer={snapshot.CurrentPlayer}, IsStarted={snapshot.IsGameStarted}, IsOver={snapshot.IsGameOver}");

            if (_gameModel != null)
            {
                for (int row = 0; row < r; row++)
                {
                    for (int col = 0; col < c; col++)
                    {
                        int idx = row * c + col;
                        snapshot.Pieces[idx] = _gameModel.Board.GetPiece(new Position(row, col));
                    }
                }
            }

            return snapshot;
        }

        private void ApplySnapshotOnClient(BoardSnapshot snapshot)
        {
            if (_clientBoardState == null ||
                _clientBoardState.Rows != snapshot.Rows ||
                _clientBoardState.Cols != snapshot.Cols)
            {
                _clientBoardState = new BoardState(snapshot.Rows, snapshot.Cols);
            }

            for (int r = 0; r < snapshot.Rows; r++)
            {
                for (int c = 0; c < snapshot.Cols; c++)
                {
                    int idx = r * snapshot.Cols + c;
                    var piece = snapshot.Pieces != null && idx < snapshot.Pieces.Length
                        ? snapshot.Pieces[idx]
                        : PieceType.None;

                    _clientBoardState.SetPiece(new Position(r, c), piece);
                }
            }

            _currentTurn = snapshot.CurrentPlayer;
            _isGameOver = snapshot.IsGameOver;
            _isGameStarted = snapshot.IsGameStarted;

            if (turnText != null)
                turnText.text = $"Turn: {_currentTurn}";

            if (scoreText != null)
                scoreText.text = $"{snapshot.ScorePlayer1} : {snapshot.ScorePlayer2}";

            if (gameOverText != null)
            {
                if (_isGameOver)
                {
                    var winningTeam = GetTeamForSeat(_currentTurn);
                    gameOverText.text = $"Winner: {winningTeam}";
                }
                else
                {
                    gameOverText.text = "";
                }
            }


            _boardView.RebuildPieces(); 
            int hash = ComputeBoardHash(snapshot.Rows, snapshot.Cols, snapshot.Pieces);
            Debug.Log($"[NGC] Client {NetworkManager.Singleton.LocalClientId}: Applied snapshot hash={hash}, " + $"CurrentPlayer={_currentTurn}, IsStarted={_isGameStarted}, IsOver={_isGameOver}");
            UpdateStatusText("Synced with host (snapshot applied).");
        }

        private void OnMoveAppliedHost(Move move)
        {
            Debug.Log("[NGC] Host: OnMoveAppliedHost, sending snapshot to clients.");

            _boardView.RebuildPieces();

            var snapshot = BuildSnapshotOnHost();
            ApplySnapshotClientRpc(snapshot);
        }

        private void OnTurnChangedHost(PlayerId player)
        {
            // 'player' here is the TEAM from GameModel (Player1 or Player2)
            Debug.Log($"[NGC] Host: OnTurnChangedHost (team) -> {player}");

            if (_isGameOver)
                return;

            // Decide which *seat* should play for this team now
            _currentTurn = ResolveNextSeatForTeam(player);

            if (turnText != null)
                turnText.text = $"Turn: {_currentTurn}";

            // Check if this team has any possible move
            if (!_gameModel.PlayerHasAnyMove(player))
            {
                _isGameOver = true;
                var winnerTeam = (player == PlayerId.Player1) ? PlayerId.Player2 : PlayerId.Player1;

                if (gameOverText != null)
                    gameOverText.text = $"Winner: {winnerTeam} (no moves for {player})";

                GameOverClientRpc(winnerTeam);
            }
            else
            {
                // Broadcast seat-turn to clients
                TurnChangedClientRpc(_currentTurn);
            }
        }


        private void OnGameOverHost(PlayerId winner)
        {
            Debug.Log($"[NGC] Host: OnGameOverHost -> {winner}");

            _isGameOver = true;

            if (gameOverText != null)
                gameOverText.text = $"Winner: {winner}";

            if (!_isSpectator && winner == GetTeamForSeat(_localPlayerId))
            {
                PlayerProgress.AddCoins(gameConfig.coins);
                UpdateCoinsUI();
            }

            GameOverClientRpc(winner);
        }


        private void OnBoardResetHost()
        {
            Debug.Log("[NGC] Host: OnBoardResetHost");

            _boardView.RebuildPieces();
            _isGameOver = false;
            _selectedPosition = null;
            _currentValidMoves.Clear();

            _currentTurn = PlayerId.Player1;

            if (turnText != null)
                turnText.text = "Turn: Player1";
            if (gameOverText != null)
                gameOverText.text = "";
            if (scoreText != null)
                scoreText.text = "0 : 0";

            BoardResetClientRpc();
        }

        private void OnScoreChangedHost(int scoreP1, int scoreP2)
        {
            Debug.Log($"[NGC] Host: OnScoreChangedHost -> {scoreP1} : {scoreP2}");

            if (scoreText != null)
                scoreText.text = $"{scoreP1} : {scoreP2}";

            ScoreChangedClientRpc(scoreP1, scoreP2);
        }

        private void BroadcastMoveToClients(Move move)
        {
            var data = new MoveData(move.From.Row, move.From.Col, move.To.Row, move.To.Col);
            Debug.Log("[NGC] Host: BroadcastMoveToClients");
            ApplyMoveClientRpc(data);
        }

        [ClientRpc]
        private void ApplyMoveClientRpc(MoveData moveData)
        {
            Debug.Log($"[NGC] ApplyMoveClientRpc on client {NetworkManager.Singleton.LocalClientId}, IsServer={IsServer}");

            if (IsServer)
                return;

            var move = moveData.ToMove();

            if (_clientBoardState != null)
            {
                var fromPos = move.From;
                var toPos = move.To;

                var piece = _clientBoardState.GetPiece(fromPos);

                _clientBoardState.SetPiece(fromPos, PieceType.None);

                if (Mathf.Abs(fromPos.Row - toPos.Row) == 2)
                {
                    int capRow = (fromPos.Row + toPos.Row) / 2;
                    int capCol = (fromPos.Col + toPos.Col) / 2;
                    _clientBoardState.SetPiece(new Position(capRow, capCol), PieceType.None);
                }

                if (piece == PieceType.P1_Man && toPos.Row == rows - 1)
                {
                    piece = PieceType.P1_King;
                }
                else if (piece == PieceType.P2_Man && toPos.Row == 0)
                {
                    piece = PieceType.P2_King;
                }

                _clientBoardState.SetPiece(toPos, piece);
            }

            _boardView.RebuildPieces();
        }

        [ClientRpc]
        private void TurnChangedClientRpc(PlayerId player)
        {
            Debug.Log($"[NGC] TurnChangedClientRpc on client {NetworkManager.Singleton.LocalClientId}, player={player}");

            if (IsServer)
                return;

            _currentTurn = player;

            if (turnText != null)
                turnText.text = $"Turn: {player}";
        }

        [ClientRpc]
        private void GameOverClientRpc(PlayerId winner)
        {
            Debug.Log($"[NGC] GameOverClientRpc on client {NetworkManager.Singleton.LocalClientId}, winner={winner}");

            if (IsServer)
                return;

            _isGameOver = true;

            if (gameOverText != null)
                gameOverText.text = $"Winner: {winner}";

            if (!_isSpectator && winner == _localPlayerId)
            {
                PlayerProgress.AddCoins(gameConfig.coins);
                UpdateCoinsUI();
            }
        }

        [ClientRpc]
        private void ScoreChangedClientRpc(int scoreP1, int scoreP2)
        {
            Debug.Log($"[NGC] ScoreChangedClientRpc on client {NetworkManager.Singleton.LocalClientId}, score={scoreP1}:{scoreP2}");

            if (IsServer)
                return;

            if (scoreText != null)
                scoreText.text = $"{scoreP1} : {scoreP2}";
        }

        [ClientRpc]
        private void BoardResetClientRpc()
        {
            Debug.Log($"[NGC] BoardResetClientRpc on client {NetworkManager.Singleton.LocalClientId}");

            if (IsServer)
                return;

            if (_clientBoardState == null)
            {
                _clientBoardState = new BoardState(rows, cols);
            }

            var layout = new StandardBoardLayout(rowsPerSide: 2);
            layout.SetupInitialBoard(_clientBoardState);

            _isGameOver = false;
            _selectedPosition = null;
            _currentValidMoves.Clear();
            _currentTurn = PlayerId.Player1;

            if (turnText != null)
                turnText.text = "Turn: Player1";
            if (gameOverText != null)
                gameOverText.text = "";
            if (scoreText != null)
                scoreText.text = "0 : 0";

            _boardView.RebuildPieces();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestReturnToMenuServerRpc(ServerRpcParams rpcParams = default)
        {
            Debug.Log("[NGC] Client requested return to menu. Sending ClientRpc to all clients.");
            ReturnToMenuClientRpc();
        }

        [ClientRpc]
        private void ReturnToMenuClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("[NGC] ReturnToMenuClientRpc received. Shutting down Netcode and loading MainMenu.");

            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsListening ||
                    NetworkManager.Singleton.IsServer ||
                    NetworkManager.Singleton.IsClient)
                {
                    NetworkManager.Singleton.Shutdown();
                }

                Destroy(NetworkManager.Singleton.gameObject);
            }

            _isGameStarted = false;
            _isGameOver = false;
            _selectedPosition = null;
            _currentValidMoves.Clear();

            SceneManager.LoadScene("MainMenu");
        }

        private void CleanupServerSeats()
        {
            if (!IsServer || NetworkManager.Singleton == null)
                return;

            var nm = NetworkManager.Singleton;

            var activeIds = new HashSet<ulong>(nm.ConnectedClientsIds);
            activeIds.Add(nm.LocalClientId);

            var toRemove = new List<ulong>();

            foreach (var kvp in _serverSeats)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    Debug.Log($"[NGC] CleanupServerSeats: removing stale seat for client {kvp.Key} (was {kvp.Value}).");
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _serverSeats.Remove(id);
            }
        }


        private void LogSeatMap(string context)
        {
            if (!IsServer)
                return;

            string msg = context + " seat map: ";
            if (_serverSeats.Count == 0)
            {
                msg += "(none)";
            }
            else
            {
                bool first = true;
                foreach (var kvp in _serverSeats)
                {
                    if (!first) msg += ", ";
                    msg += $"{kvp.Key}->{kvp.Value}";
                    first = false;
                }
            }

            Debug.Log(msg);
        }

        public void OnRestartButtonClicked()
        {
            Debug.Log("[NGC] Back to main menu clicked.");

            if (IsServer)
            {
                ReturnToMenuClientRpc();
            }
            else
            {
                RequestReturnToMenuServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRestartServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer || _gameModel == null)
                return;

            ulong sender = rpcParams.Receive.SenderClientId;
            Debug.Log($"[NGC] Host: Restart requested by client {sender}");

            HostRestartGame();
        }

        private void HostRestartGame()
        {
            if (_gameModel == null)
                return;

            Debug.Log("[NGC] Host: restarting game (same scene).");

            int rowsPerSide = gameConfig != null ? gameConfig.rowsPerSide : 2;
            var layout = new StandardBoardLayout(rowsPerSide: rowsPerSide);

            _gameModel.ResetBoard(layout);

            _isGameOver = false;
            _selectedPosition = null;
            _currentValidMoves.Clear();
            _isGameStarted = true;

            var snapshot = BuildSnapshotOnHost();
            ApplySnapshotClientRpc(snapshot);
        }

        private void UpdateCoinsUI()
        {
            if (coinsText != null)
                coinsText.text = $"Coins: {PlayerProgress.Coins}";
        }
        private void UpdateStatusText(string extra = "")
        {
            if (statusText == null) return;

            if (_isSpectator)
                statusText.text = $"Spectating. {extra}";
            else
                statusText.text = $"You are: {_localPlayerId} ({_currentTurn} to move). {extra}";
        }
        private int ComputeBoardHash(int rows, int cols, PieceType[] pieces)
        {
            if (pieces == null) return 0;

            int hash = 17;
            int len = rows * cols;

            for (int i = 0; i < len && i < pieces.Length; i++)
            {
                hash = hash * 31 + (int)pieces[i];
            }

            return hash;
        }

    }
}
