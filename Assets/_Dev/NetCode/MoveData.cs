using Unity.Netcode;
using Checkers.Model;

namespace Checkers.Netcode
{
    public struct MoveData : INetworkSerializable
    {
        public int FromRow;
        public int FromCol;
        public int ToRow;
        public int ToCol;

        public MoveData(int fromRow, int fromCol, int toRow, int toCol)
        {
            FromRow = fromRow;
            FromCol = fromCol;
            ToRow = toRow;
            ToCol = toCol;
        }

        public Move ToMove()
        {
            return new Move(
                new Position(FromRow, FromCol),
                new Position(ToRow, ToCol));
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref FromRow);
            serializer.SerializeValue(ref FromCol);
            serializer.SerializeValue(ref ToRow);
            serializer.SerializeValue(ref ToCol);
        }
    }
}
