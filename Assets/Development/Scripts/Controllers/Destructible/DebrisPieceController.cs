using System;
using UnityEngine;

namespace FlatOutLike.Controllers.Destructible
{
    /// <summary>
    /// Контроллер одного обломка. После окончания времени жизни вызывает
    /// колбэк, через который <see cref="Factories.Destructible.DestructibleFactory"/>
    /// возвращает объект в пул. Зеркалит паттерн <c>ParticleController</c>.
    /// </summary>
    public sealed class DebrisPieceController : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;

        private Action<DebrisPieceController> _onFinished;
        private float _remainingLifetime;
        private bool  _active;

        // Явный запуск из фабрики (не Start/OnEnable — инъекция уже отработала до вызова)
        public void Launch(
            Vector3 pos,
            Quaternion rot,
            Vector3 impulse,
            float lifetime,
            Action<DebrisPieceController> onFinished)
        {
            if (_rigidbody == null)
            {
                Debug.LogWarning($"[DebrisPieceController] {name}: Rigidbody не назначен. Немедленный возврат в пул.");
                onFinished?.Invoke(this);
                return;
            }

            // Выставляем позу и сбрасываем накопленные скорости с прошлого использования
            transform.SetPositionAndRotation(pos, rot);
            _rigidbody.linearVelocity   = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // Импульс разлёта
            _rigidbody.AddForce(impulse, ForceMode.VelocityChange);

            _onFinished       = onFinished;
            _remainingLifetime = lifetime;
            _active           = true;
        }

        // Таймер обратного отсчёта через Update — без Invoke (тот вызывается даже при SetActive=false)
        private void Update()
        {
            if (!_active) return;

            _remainingLifetime -= Time.deltaTime;
            if (_remainingLifetime > 0f) return;

            // Сначала снимаем флаг, затем вызываем колбэк — исключает повторный вызов
            _active = false;
            var callback = _onFinished;
            _onFinished  = null;
            callback?.Invoke(this);
        }
    }
}
