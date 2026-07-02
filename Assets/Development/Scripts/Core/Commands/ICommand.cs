namespace FlatOutLike.Core.Commands
{
    /// <summary>
    /// Базовый интерфейс команды. Контроллеры вызывают <see cref="CanExecute"/> перед
    /// <see cref="Execute"/> — сама команда не решает, вызывать её или нет.
    /// </summary>
    public interface ICommand
    {
        bool CanExecute();
        void Execute();
    }
}
