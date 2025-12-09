using UnityEngine;
using Checkers.Model;

namespace Checkers.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PieceView : MonoBehaviour
    {
        [Header("Colors")]
        [SerializeField] private Color p1ManColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color p1KingColor = new Color(0.6f, 0f, 0f);
        [SerializeField] private Color p2ManColor = new Color(0.2f, 0.2f, 0.9f);
        [SerializeField] private Color p2KingColor = new Color(0f, 0f, 0.6f);

        private SpriteRenderer _renderer;
        private PieceType _pieceType;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void SetPieceType(PieceType pieceType)
        {
            _pieceType = pieceType;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_renderer == null) return;

            switch (_pieceType)
            {
                case PieceType.P1_Man:
                    _renderer.enabled = true;
                    _renderer.color = p1ManColor;
                    break;

                case PieceType.P1_King:
                    _renderer.enabled = true;
                    _renderer.color = p1KingColor;
                    break;

                case PieceType.P2_Man:
                    _renderer.enabled = true;
                    _renderer.color = p2ManColor;
                    break;

                case PieceType.P2_King:
                    _renderer.enabled = true;
                    _renderer.color = p2KingColor;
                    break;

                case PieceType.None:
                default:
                    _renderer.enabled = false;
                    break;
            }
        }
    }
}
