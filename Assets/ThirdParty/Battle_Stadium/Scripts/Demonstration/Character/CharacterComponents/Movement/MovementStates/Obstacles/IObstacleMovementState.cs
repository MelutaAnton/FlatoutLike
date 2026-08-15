using System;

namespace ithappy.Battle_Stadium
{
    public interface IObstacleMovementState
    {
        public void Overcome(ObstacleInfo obstacles, Action<bool> callback);
    }
}
