namespace FlatOutLike.Services.Meta
{
    public interface IProfileStorage
    {
        void Save(IPlayerProfile profile);
        bool TryLoad(out IPlayerProfile profile);
    }
}
