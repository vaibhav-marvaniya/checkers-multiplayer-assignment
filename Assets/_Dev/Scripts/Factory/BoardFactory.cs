using UnityEngine;
using Checkers.Model;
using Checkers.View;

namespace Checkers.Factory
{
    public class BoardFactory : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private GameObject piecePrefab;

        public TileView CreateTile(Vector2 worldPos, Transform parent, Position gridPos, BoardView boardView, Color baseColor)
        {
            var go = Instantiate(tilePrefab, worldPos, Quaternion.identity, parent);
            var tileView = go.GetComponent<TileView>();
            tileView.Init(gridPos, boardView, baseColor);
            return tileView;
        }

        public PieceView CreatePiece(Vector2 worldPos, Transform parent, PieceType type)
        {
            var go = Instantiate(piecePrefab, worldPos, Quaternion.identity, parent);
            var pieceView = go.GetComponent<PieceView>();
            pieceView.SetPieceType(type);
            return pieceView;
        }
    }
}
