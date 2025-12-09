using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Checkers.Model;
using Checkers.View;
using Checkers.Config;
using Checkers.Progress;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Checkers.Controller
{
    public class GameController : MonoBehaviour, IBoardInputHandler
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
        [SerializeField] private Button restartButton;

        private GameModel _gameModel;
        private Position? _selectedPosition;
        private List<Move> _currentValidMoves = new List<Move>();
        private bool _isGameOver; 
        
        [SerializeField] private TMP_Text coinsText;


        private void Start()
        {
            restartButton.onClick.AddListener(OnRestartButtonClicked);
            SetupGame();
        }

        private void SetupGame()
        {
            int r = gameConfig != null ? gameConfig.rows : rows;
            int c = gameConfig != null ? gameConfig.cols : cols;

            var boardState = new BoardState(r, c);
            var layout = new StandardBoardLayout(
                rowsPerSide: (gameConfig != null ? gameConfig.rowsPerSide : 2)
            );
            var rules = new StandardCheckersRuleSet();

            _gameModel = new GameModel(boardState, rules);
            _gameModel.OnMoveApplied += OnMoveApplied;
            _gameModel.OnTurnChanged += OnTurnChanged;
            _gameModel.OnGameOver += OnGameOver;
            _gameModel.OnBoardReset += OnBoardReset;
            _gameModel.OnScoreChanged += OnScoreChanged;

            _isGameOver = false;

            _boardView.Init(boardState, this);
            _boardView.SetPerspectiveForLocalPlayer(PlayerId.Player1);

            _gameModel.ResetBoard(layout);
            UpdateCoinsUI();
        }


        public void HandleTileClicked(Position pos)
        {
            if (_isGameOver)
                return;

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
                    if (_gameModel.TryApplyMove(move))
                    {
                        Deselect();
                    }
                    else
                    {
                        Deselect();
                    }
                }
                else
                {
                    TrySelectPiece(pos);
                }
            }
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

            _currentValidMoves = _gameModel.GetValidMovesFrom(pos);
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

        private void OnMoveApplied(Move move)
        {
            _boardView.RebuildPieces();
        }


        private void OnTurnChanged(PlayerId player)
        {
            if (turnText != null)
                turnText.text = $"Turn: {player}";

            if (_isGameOver)
                return;

            if (!_gameModel.PlayerHasAnyMove(player))
            {
                _isGameOver = true;
                var winner = (player == PlayerId.Player1) ? PlayerId.Player2 : PlayerId.Player1;

                if (gameOverText != null)
                    gameOverText.text = $"Winner: {winner} (no moves for {player})";
            }
        }

        private void OnGameOver(PlayerId winner)
        {
            _isGameOver = true;

            if (gameOverText != null)
                gameOverText.text = $"Winner: {winner}";

            if (winner == PlayerId.Player1)
            {
                PlayerProgress.AddCoins(gameConfig.coins);
                UpdateCoinsUI();
            }
        }


        private void OnBoardReset()
        {
            _boardView.RebuildPieces();
            if (gameOverText != null)
                gameOverText.text = "";
        }

        private void OnScoreChanged(int scoreP1, int scoreP2)
        {
            if (scoreText != null)
                scoreText.text = $"{scoreP1} : {scoreP2}";
        }

        public void RestartGame()
        {
            int rowsPerSide = gameConfig != null ? gameConfig.rowsPerSide : 2;
            var layout = new StandardBoardLayout(rowsPerSide: rowsPerSide);

            _gameModel.ResetBoard(layout);
            Deselect();
            _isGameOver = false;

            if (gameOverText != null)
                gameOverText.text = "";
        }

        private void UpdateCoinsUI()
        {
            if (coinsText != null)
                coinsText.text = $"Coins: {PlayerProgress.Coins}";
        }
        public void OnRestartButtonClicked()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
