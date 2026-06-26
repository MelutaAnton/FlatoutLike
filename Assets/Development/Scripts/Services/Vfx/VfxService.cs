using FlatOutLike.Factories.Vfx;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Vfx
{
    public sealed class VfxService : IVfxService
    {
        private readonly IVfxFactory _factory;

        public VfxService(IVfxFactory factory)
        {
            _factory = factory;
        }

        public void PlayEffect(VfxEffectType type, Vector3 pos, Quaternion rot)
        {
            _factory.Spawn(type, pos, rot);
        }
    }
}
