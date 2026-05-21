namespace FlatOutLike.Services.Meta
{
    public sealed class MetaProgressService : IMetaProgressService
    {
        private readonly IProfileStorage _storage;

        public IPlayerProfile Profile { get; }

        public MetaProgressService(IProfileStorage storage)
        {
            _storage = storage;
            Profile = storage.TryLoad(out var loaded) ? loaded : new PlayerProfile { Coins = 0 };
        }

        public void Save()
        {
            _storage.Save(Profile);
        }
    }
}
