using System;
using UnityEngine;
using UnityEngine.UI;

namespace FlatOutLike.Views.UI.Hub
{
    /// <summary>
    /// World-space круг подтверждения запуска режима над активной зоной хаба (Шаг 11b).
    /// Компонент висит на КОРНЕ канваса, а гасится только дочерний _root: корень обязан
    /// оставаться активным, иначе Update не тикает и presenter не узнает о новой зоне.
    /// Только UI — где показывать и сколько заполнено, решает LaunchConfirmPresenter.
    /// </summary>
    public sealed class LaunchConfirmView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _fill;

        /// <summary>Тик Update — Unity-lifecycle, View имеет на него право (паттерн HighJumpHudView).</summary>
        public event Action OnTick;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false); // скрыт по умолчанию
        }

        private void Update() => OnTick?.Invoke();

        public void ShowAt(Vector3 worldPosition, float progress01)
        {
            if (_root != null && !_root.activeSelf) _root.SetActive(true);

            transform.position = worldPosition;

            if (_fill != null) _fill.fillAmount = progress01;
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
