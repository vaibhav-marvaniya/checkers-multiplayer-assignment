using System;
using System.Collections.Generic;

namespace Checkers.Model
{
    public class GameModel
    {
        public BoardState Board { get; private set; }
        public PlayerId CurrentPlayer { get; private set; }

        private readonly IRuleSet _ruleSet;

        public int ScorePlayer1 { get; private set; }
        public int ScorePlayer2 { get; private set; }

        public event Action<Move> OnMoveApplied;
        public event Action<PlayerId> OnTurnChanged;
        public event Action<PlayerId> OnGameOver;
        public event Action OnBoardReset;

        public event Action<int, int> OnScoreChanged;

        public GameModel(BoardState board, IRuleSet ruleSet)
        {
            Board = board;
            _ruleSet = ruleSet;
            CurrentPlayer = PlayerId.Player1;
        }

        public void ResetBoard(IBoardLayout layout)
        {
            layout.SetupInitialBoard(Board);
            CurrentPlayer = PlayerId.Player1;

            ScorePlayer1 = 0;
            ScorePlayer2 = 0;

            OnBoardReset?.Invoke();
            OnTurnChanged?.Invoke(CurrentPlayer);

            OnScoreChanged?.Invoke(ScorePlayer1, ScorePlayer2);
        }

        public List<Move> GetValidMovesFrom(Position from)
        {
            return _ruleSet.GetValidMovesFrom(Board, from, CurrentPlayer);
        }

        public bool TryApplyMove(Move move)
        {
            var validMoves = _ruleSet.GetValidMovesFrom(Board, move.From, CurrentPlayer);
            var chosen = validMoves.Find(m => m.To.Row == move.To.Row && m.To.Col == move.To.Col);

            if (chosen == null)
                return false;

            ApplyMoveInternal(chosen);

            if (chosen.IsCapture)
            {
                if (CurrentPlayer == PlayerId.Player1)
                {
                    ScorePlayer1++;
                }
                else
                {
                    ScorePlayer2++;
                }

                OnScoreChanged?.Invoke(ScorePlayer1, ScorePlayer2);
            }

            PromoteIfNeeded(chosen.To);

            OnMoveApplied?.Invoke(chosen);

            if (_ruleSet.IsWin(Board, CurrentPlayer, out PlayerId winner))
            {
                OnGameOver?.Invoke(winner);
            }
            else
            {
                CurrentPlayer = (CurrentPlayer == PlayerId.Player1) ? PlayerId.Player2 : PlayerId.Player1;
                OnTurnChanged?.Invoke(CurrentPlayer);
            }

            return true;
        }

        private void ApplyMoveInternal(Move move)
        {
            var piece = Board.GetPiece(move.From);
            Board.SetPiece(move.From, PieceType.None);
            Board.SetPiece(move.To, piece);

            if (move.IsCapture && move.CapturedPosition.HasValue)
            {
                Board.SetPiece(move.CapturedPosition.Value, PieceType.None);
            }
        }

        private void PromoteIfNeeded(Position pos)
        {
            var piece = Board.GetPiece(pos);
            if (piece == PieceType.P1_Man && pos.Row == Board.Rows - 1)
            {
                Board.SetPiece(pos, PieceType.P1_King);
            }
            else if (piece == PieceType.P2_Man && pos.Row == 0)
            {
                Board.SetPiece(pos, PieceType.P2_King);
            }
        }

        public bool PlayerHasAnyMove(PlayerId player)
        {
            for (int r = 0; r < Board.Rows; r++)
            {
                for (int c = 0; c < Board.Cols; c++)
                {
                    var pos = new Position(r, c);
                    var piece = Board.GetPiece(pos);

                    if (!OwnsPiece(player, piece))
                        continue;

                    var moves = _ruleSet.GetValidMovesFrom(Board, pos, player);
                    if (moves.Count > 0)
                        return true;
                }
            }

            return false;
        }

        private bool OwnsPiece(PlayerId player, PieceType piece)
        {
            if (player == PlayerId.Player1)
                return piece == PieceType.P1_Man || piece == PieceType.P1_King;
            if (player == PlayerId.Player2)
                return piece == PieceType.P2_Man || piece == PieceType.P2_King;
            return false;
        }
    }
}
