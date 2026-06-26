using UnityEngine;

namespace FlatOutLike.Data.Configs.Combat
{
    [CreateAssetMenu(fileName = "DamageConfig", menuName = "FlatOutLike/Combat/DamageConfig")]
    public sealed class DamageConfig : ScriptableObject
    {
        [SerializeField] private float _damageScale = 1f;
        [SerializeField] private float _minImpactSpeed = 3f;
        [SerializeField] private float _wallMassRatio = 1f;

        public float DamageScale => _damageScale;
        public float MinImpactSpeed => _minImpactSpeed;
        public float WallMassRatio => _wallMassRatio;
    }
}
