using FlatOutLike.Models;
using UnityEngine;

namespace FlatOutLike.Services.Vfx
{
    public interface IVfxService
    {
        void PlayEffect(VfxEffectType type, Vector3 pos, Quaternion rot);
    }
}
