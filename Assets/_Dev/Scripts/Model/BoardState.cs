using System;

namespace Checkers.Model
{
    public class BoardState
    {
        public int Rows { get; private set; }
        public int Cols { get; private set; }

        private PieceType[,] _cells;

        public BoardState(int rows, int cols)
        {
            if (rows <= 0 || cols <= 0)
                throw new ArgumentException("Board size must be positive");

            Rows = rows;
            Cols = cols;
            _cells = new PieceType[rows, cols];
        }

        public PieceType GetPiece(Position pos)
        {
            return _cells[pos.Row, pos.Col];
        }

        public void SetPiece(Position pos, PieceType type)
        {
            _cells[pos.Row, pos.Col] = type;
        }

        public bool IsInsideBoard(Position pos)
        {
            return pos.Row >= 0 && pos.Row < Rows &&
                   pos.Col >= 0 && pos.Col < Cols;
        }
    }
}
