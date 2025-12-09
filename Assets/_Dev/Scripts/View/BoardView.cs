using System.Collections.Generic;
using UnityEngine;
using Checkers.Model;
using Checkers.Factory;
using Checkers.Controller;

namespace Checkers.View
{
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private Transform tilesParent;
        [SerializeField] private Transform piecesParent;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private BoardFactory boardFactory;

        [Header("Board Position")]
        [Tooltip("Offset added after centering the board (in world units).")]
        [SerializeField] private Vector2 boardOffset = Vector2.zero;

        [Header("Tile Colors")]
        [Tooltip("Colors used for board tiles in a repeating pattern. (r + c) % colors.Length")]
        [SerializeField]
        private Color[] tileBaseColors = new Color[]
        {
            new Color(0.8f, 0.8f, 0.8f),
            new Color(0.3f, 0.3f, 0.3f)
        };

        private TileView[,] _tiles;
        private PieceView[,] _pieces;

        private BoardState _boardState;
        private IBoardInputHandler _inputHandler;

        private List<TileView> _highlightedTiles = new List<TileView>();
        private TileView _selectedTile;
        private bool _flipVertically = false;

        public void Init(BoardState boardState, IBoardInputHandler inputHandler)
        {
            _boardState = boardState;
            _inputHandler = inputHandler;

            BuildBoard();
            RebuildPieces();
        }

        private void BuildBoard()
        {
            int rows = _boardState.Rows;
            int cols = _boardState.Cols;
            _tiles = new TileView[rows, cols];
            _pieces = new PieceView[rows, cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector2 worldPos = GetWorldPositionForTile(r, c);

                    Color baseColor = Color.white;
                    if (tileBaseColors != null && tileBaseColors.Length > 0)
                    {
                        int index = (r + c) % tileBaseColors.Length;
                        baseColor = tileBaseColors[index];
                    }

                    var tile = boardFactory.CreateTile(
                        worldPos,
                        tilesParent,
                        new Position(r, c),
                        this,
                        baseColor
                    );

                    _tiles[r, c] = tile;
                }
            }
        }

        private Vector2 GetWorldPositionForTile(int row, int col)
        {
            int rows = _boardState.Rows;
            int cols = _boardState.Cols;

            float centerX = (cols - 1) * tileSize * 0.5f;
            float centerY = (rows - 1) * tileSize * 0.5f;

            int visualRow = _flipVertically ? (rows - 1 - row) : row;

            float x = col * tileSize - centerX + boardOffset.x;
            float y = -visualRow * tileSize + centerY + boardOffset.y;

            return new Vector2(x, y);
        }

        public void RebuildPieces()
        {
            if (_pieces != null)
            {
                for (int r = 0; r < _boardState.Rows; r++)
                {
                    for (int c = 0; c < _boardState.Cols; c++)
                    {
                        if (_pieces[r, c] != null)
                            Destroy(_pieces[r, c].gameObject);
                        _pieces[r, c] = null;
                    }
                }
            }

            for (int r = 0; r < _boardState.Rows; r++)
            {
                for (int c = 0; c < _boardState.Cols; c++)
                {
                    var pos = new Position(r, c);
                    var pieceType = _boardState.GetPiece(pos);
                    if (pieceType == PieceType.None) continue;

                    var tile = _tiles[r, c];
                    var worldPos = tile.transform.position;
                    var piece = boardFactory.CreatePiece(worldPos, piecesParent, pieceType);
                    _pieces[r, c] = piece;
                }
            }
        }

        public void UpdatePiecePositions(Move move)
        {
            var from = move.From;
            var to = move.To;

            var pieceView = _pieces[from.Row, from.Col];
            _pieces[from.Row, from.Col] = null;

            var targetTile = _tiles[to.Row, to.Col];
            pieceView.transform.position = targetTile.transform.position;
            _pieces[to.Row, to.Col] = pieceView;

            if (move.IsCapture && move.CapturedPosition.HasValue)
            {
                var cap = move.CapturedPosition.Value;
                if (_pieces[cap.Row, cap.Col] != null)
                {
                    Destroy(_pieces[cap.Row, cap.Col].gameObject);
                    _pieces[cap.Row, cap.Col] = null;
                }
            }

            var newType = _boardState.GetPiece(to);
            pieceView.SetPieceType(newType);
        }

        public void OnBoardPositionClicked(Position pos)
        {
            _inputHandler?.HandleTileClicked(pos);
        }

        public void OnTileClicked(TileView tileView)
        {
            OnBoardPositionClicked(tileView.GridPosition);
        }

        public void ClearHighlights()
        {
            foreach (var t in _highlightedTiles)
            {
                t.SetMoveHighlight(false);
            }
            _highlightedTiles.Clear();
        }

        public void HighlightPositions(IEnumerable<Position> positions)
        {
            ClearHighlights();
            foreach (var pos in positions)
            {
                var tile = _tiles[pos.Row, pos.Col];
                tile.SetMoveHighlight(true);
                _highlightedTiles.Add(tile);
            }
        }

        public void SelectTile(Position pos)
        {
            ClearSelection();

            var tile = _tiles[pos.Row, pos.Col];
            tile.SetSelected(true);
            _selectedTile = tile;
        }

        public void ClearSelection()
        {
            if (_selectedTile != null)
            {
                _selectedTile.SetSelected(false);
                _selectedTile = null;
            }
        }

        public void SetPerspectiveForLocalPlayer(PlayerId localPlayerId)
        {
            bool isTeamA = (localPlayerId == PlayerId.Player1 || localPlayerId == PlayerId.Player3);
            bool shouldFlip = isTeamA;
            ApplyVerticalFlip(shouldFlip);
        }


        private void ApplyVerticalFlip(bool flip)
        {
            if (_boardState == null)
                return;

            if (_flipVertically == flip)
                return;

            _flipVertically = flip;

            int rows = _boardState.Rows;
            int cols = _boardState.Cols;

            if (_tiles != null)
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        var tile = _tiles[r, c];
                        if (tile == null) continue;

                        tile.transform.position = GetWorldPositionForTile(r, c);
                    }
                }
            }

            if (_pieces != null)
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        var piece = _pieces[r, c];
                        if (piece == null) continue;

                        var tile = _tiles[r, c];
                        if (tile == null) continue;

                        piece.transform.position = tile.transform.position;
                    }
                }
            }
        }
    }
}
