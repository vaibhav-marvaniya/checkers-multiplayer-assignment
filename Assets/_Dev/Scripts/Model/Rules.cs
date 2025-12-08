using System;
using System.Collections.Generic;

namespace Checkers.Model
{
    public interface IBoardLayout
    {
        void SetupInitialBoard(BoardState board);
    }

    public interface IRuleSet
    {
        List<Move> GetValidMovesFrom(BoardState board, Position from, PlayerId player);
        bool IsWin(BoardState board, PlayerId currentPlayer, out PlayerId winner);
    }

    public class StandardBoardLayout : IBoardLayout
    {
        private readonly int _rowsPerSide;

        public StandardBoardLayout(int rowsPerSide = 2)
        {
            _rowsPerSide = rowsPerSide;
        }

        public void SetupInitialBoard(BoardState board)
        {
            int rows = board.Rows;
            int cols = board.Cols;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    board.SetPiece(new Position(r, c), PieceType.None);
                }
            }

            int rowsPerSide = Math.Min(_rowsPerSide, rows / 2);

            for (int r = 0; r < rowsPerSide; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (((r + c) % 2) == 1)
                    {
                        board.SetPiece(new Position(r, c), PieceType.P1_Man);
                    }
                }
            }

            for (int r = rows - rowsPerSide; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (((r + c) % 2) == 1)
                    {
                        board.SetPiece(new Position(r, c), PieceType.P2_Man);
                    }
                }
            }
        }
    }

    public class StandardCheckersRuleSet : IRuleSet
    {
        public List<Move> GetValidMovesFrom(BoardState board, Position from, PlayerId player)
        {
            var moves = new List<Move>();
            var piece = board.GetPiece(from);
            if (!OwnsPiece(player, piece)) return moves;

            bool isKing = piece == PieceType.P1_King || piece == PieceType.P2_King;
            int forwardDir = (player == PlayerId.Player1) ? 1 : -1;

            AddStepMoves(board, from, player, isKing, forwardDir, moves);

            AddCaptureMoves(board, from, player, isKing, forwardDir, moves);

            return moves;
        }

        public bool IsWin(BoardState board, PlayerId currentPlayer, out PlayerId winner)
        {
            bool p1HasPiece = false;
            bool p2HasPiece = false;
            bool p1HasMove = false;
            bool p2HasMove = false;

            for (int r = 0; r < board.Rows; r++)
            {
                for (int c = 0; c < board.Cols; c++)
                {
                    var pos = new Position(r, c);
                    var piece = board.GetPiece(pos);

                    if (piece == PieceType.P1_Man || piece == PieceType.P1_King)
                    {
                        p1HasPiece = true;
                        if (!p1HasMove && GetValidMovesFrom(board, pos, PlayerId.Player1).Count > 0)
                            p1HasMove = true;
                    }
                    else if (piece == PieceType.P2_Man || piece == PieceType.P2_King)
                    {
                        p2HasPiece = true;
                        if (!p2HasMove && GetValidMovesFrom(board, pos, PlayerId.Player2).Count > 0)
                            p2HasMove = true;
                    }
                }
            }

            if (!p1HasPiece && p2HasPiece)
            {
                winner = PlayerId.Player2;
                return true;
            }
            if (!p2HasPiece && p1HasPiece)
            {
                winner = PlayerId.Player1;
                return true;
            }

            if (!p1HasMove && p2HasMove)
            {
                winner = PlayerId.Player2;
                return true;
            }
            if (!p2HasMove && p1HasMove)
            {
                winner = PlayerId.Player1;
                return true;
            }

            winner = currentPlayer;
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

        private void AddStepMoves(BoardState board, Position from, PlayerId player,
                                  bool isKing, int forwardDir, List<Move> moves)
        {
            int[] rowDirs = isKing ? new[] { 1, -1 } : new[] { forwardDir };
            int[] colDirs = new[] { -1, 1 };

            foreach (int rd in rowDirs)
            {
                foreach (int cd in colDirs)
                {
                    var to = new Position(from.Row + rd, from.Col + cd);
                    if (!board.IsInsideBoard(to)) continue;
                    if (board.GetPiece(to) == PieceType.None)
                    {
                        moves.Add(new Move(from, to, false));
                    }
                }
            }
        }

        private void AddCaptureMoves(BoardState board, Position from, PlayerId player,
                                     bool isKing, int forwardDir, List<Move> moves)
        {
            int[] rowDirs = isKing ? new[] { 1, -1 } : new[] { forwardDir };
            int[] colDirs = new[] { -1, 1 };

            foreach (int rd in rowDirs)
            {
                foreach (int cd in colDirs)
                {
                    var mid = new Position(from.Row + rd, from.Col + cd);
                    var to = new Position(from.Row + 2 * rd, from.Col + 2 * cd);

                    if (!board.IsInsideBoard(mid) || !board.IsInsideBoard(to)) continue;

                    var midPiece = board.GetPiece(mid);
                    var toPiece = board.GetPiece(to);

                    if (toPiece == PieceType.None && IsEnemyPiece(player, midPiece))
                    {
                        moves.Add(new Move(from, to, true, mid));
                    }
                }
            }
        }

        private bool IsEnemyPiece(PlayerId player, PieceType piece)
        {
            if (player == PlayerId.Player1)
                return piece == PieceType.P2_Man || piece == PieceType.P2_King;
            if (player == PlayerId.Player2)
                return piece == PieceType.P1_Man || piece == PieceType.P1_King;
            return false;
        }
    }
}
