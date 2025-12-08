using UnityEngine;
using Checkers.Model;

namespace Checkers.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class TileView : MonoBehaviour
    {
        public Position GridPosition { get; private set; }

        [Header("Highlight Colors")]
        [SerializeField] private Color moveHighlightColor = new Color(1f, 1f, 0.5f);
        [SerializeField] private Color selectedColor = new Color(0.8f, 0.8f, 0.2f);

        private SpriteRenderer _renderer;
        private Color _baseColor;
        private bool _isMoveHighlighted;
        private bool _isSelected;

        private BoardView _boardView;

        public void Init(Position gridPos, BoardView boardView, Color baseColor)
        {
            GridPosition = gridPos;
            _boardView = boardView;

            _renderer = GetComponent<SpriteRenderer>();
            _baseColor = baseColor;

            UpdateColor();
        }

        private void OnMouseDown()
        {
            _boardView.OnBoardPositionClicked(GridPosition);
        }


        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            UpdateColor();
        }

        public void SetMoveHighlight(bool highlighted)
        {
            _isMoveHighlighted = highlighted;
            UpdateColor();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateColor();
        }

        private void UpdateColor()
        {
            if (_renderer == null) return;

            Color final = _baseColor;

            if (_isMoveHighlighted)
                final = moveHighlightColor;

            if (_isSelected)
                final = selectedColor;

            _renderer.color = final;
        }
    }
}
