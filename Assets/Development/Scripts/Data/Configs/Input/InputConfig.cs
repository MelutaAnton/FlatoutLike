using UnityEngine;

namespace FlatOutLike.Data.Configs.Input
{
    /// <summary>
    /// Единый ассет на весь проект — переключает схему управления (клавиатура/мобильный HUD)
    /// во всех сценах одной галочкой, без правки каждого GameLifetimeScope по отдельности.
    /// В мобильном билде не нужен: там переключение и так происходит через
    /// Application.isMobilePlatform.
    /// </summary>
    [CreateAssetMenu(fileName = "InputConfig", menuName = "FlatOutLike/Input/InputConfig")]
    public sealed class InputConfig : ScriptableObject
    {
        [Tooltip("Форсирует мобильный input-провайдер даже в редакторе/на десктопе — для теста мобильного HUD без сборки на устройство.")]
        [SerializeField] private bool _forceMobileInput;

        public bool ForceMobileInput => _forceMobileInput;
    }
}
