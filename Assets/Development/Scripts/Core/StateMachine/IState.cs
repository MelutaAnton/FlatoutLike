namespace FlatOutLike.Core.StateMachine
{
    public interface IState
    {
        void Enter();
        void Exit();
        void Tick();
    }
}
