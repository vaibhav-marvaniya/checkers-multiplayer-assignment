using System;
using System.Collections.Generic;

namespace Checkers.Model
{
    public enum PlayerId
    {
        Player1 = 1,
        Player2 = 2,
        Player3 = 3,
        Player4 = 4
    }

    public enum PieceType
    {
        None = 0,
        P1_Man,
        P1_King,
        P2_Man,
        P2_King
    }

    [Serializable]
    public struct Position
    {
        public int Row;
        public int Col;

        public Position(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public override string ToString() => $"({Row},{Col})";
    }

    [Serializable]
    public class Move
    {
        public Position From;
        public Position To;
        public bool IsCapture;
        public Position? CapturedPosition;

        public Move() { }

        public Move(Position from, Position to, bool isCapture = false, Position? capturedPos = null)
        {
            From = from;
            To = to;
            IsCapture = isCapture;
            CapturedPosition = capturedPos;
        }
    }
}
