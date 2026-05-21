using FlatOutLike.Models;

namespace FlatOutLike.Services.Flow
{
    public interface IGameFlowService
    {
        GameState Current { get; }
        void ChangeState(GameState next);
        void Push(GameState next);
        void Pop();
        void Tick();
    }
}
