using FlatOutLike.Controllers.Vfx;
using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Factories.Vfx
{
    public interface IVfxFactory
    {
        ParticleController Spawn(VfxEffectType type, Vector3 pos, Quaternion rot);
    }
}
