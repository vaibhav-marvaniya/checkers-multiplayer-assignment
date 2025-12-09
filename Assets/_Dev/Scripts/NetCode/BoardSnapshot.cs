using Unity.Netcode;
using Checkers.Model;

namespace Checkers.Netcode
{
    public struct BoardSnapshot : INetworkSerializable
    {
        public int Rows;
        public int Cols;

        public PieceType[] Pieces;

        public int ScorePlayer1;
        public int ScorePlayer2;
        public PlayerId CurrentPlayer;
        public bool IsGameOver;
        public bool IsGameStarted;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Rows);
            serializer.SerializeValue(ref Cols);

            int len = Pieces != null ? Pieces.Length : 0;
            serializer.SerializeValue(ref len);

            if (serializer.IsReader)
            {
                Pieces = new PieceType[len];
            }

            for (int i = 0; i < len; i++)
            {
                int pieceInt = Pieces != null ? (int)Pieces[i] : 0;
                serializer.SerializeValue(ref pieceInt);

                if (serializer.IsReader)
                {
                    Pieces[i] = (PieceType)pieceInt;
                }
            }

            int currentPlayerInt = (int)CurrentPlayer;
            serializer.SerializeValue(ref currentPlayerInt);
            CurrentPlayer = (PlayerId)currentPlayerInt;

            serializer.SerializeValue(ref ScorePlayer1);
            serializer.SerializeValue(ref ScorePlayer2);
            serializer.SerializeValue(ref IsGameOver);
            serializer.SerializeValue(ref IsGameStarted);
        }
    }
}
