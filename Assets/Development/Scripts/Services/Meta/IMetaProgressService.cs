namespace FlatOutLike.Services.Meta
{
    public interface IMetaProgressService
    {
        IPlayerProfile Profile { get; }
        void Save();
    }
}
