using Checkers.Model;

namespace Checkers.Controller
{
    public interface IBoardInputHandler
    {
        void HandleTileClicked(Position pos);
    }
}
