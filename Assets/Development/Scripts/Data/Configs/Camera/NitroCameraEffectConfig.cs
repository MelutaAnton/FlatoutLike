using UnityEngine;

namespace FlatOutLike.Data.Configs.Camera
{
    /// <summary>
    /// Параметры кинематографического отклика камеры на нитро игрока: FOV-кик chase-камеры
    /// и радиальный блюр по краям экрана. Единственный источник тюнинга для
    /// <c>NitroCameraEffectController</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "NitroCameraEffectConfig", menuName = "FlatOutLike/Camera/NitroCameraEffectConfig")]
    public sealed class NitroCameraEffectConfig : ScriptableObject
    {
        [SerializeField] private float _fovBoost     = 8f;    // добавка к FOV линзы на пике эффекта
        [SerializeField] private float _blurStrength = 0.35f; // 0..1, множитель интенсивности блюра
        [SerializeField] private float _attackTime   = 0.15f; // сек до полной интенсивности
        [SerializeField] private float _releaseTime  = 0.35f; // сек затухания

        public float FovBoost     => _fovBoost;
        public float BlurStrength => _blurStrength;
        public float AttackTime   => _attackTime;
        public float ReleaseTime  => _releaseTime;
    }
}
