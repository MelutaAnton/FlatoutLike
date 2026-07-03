using FlatOutLike.Controllers.Driver;
using FlatOutLike.Services.Driver;
using UnityEngine;
using VContainer;

namespace FlatOutLike.Controllers.Stunts
{
    /// <summary>
    /// Триггер поля приземления «High Jump». Как только катапультированный водитель
    /// касается зоны — сообщает <see cref="IDriverFlightService"/>, что полёт завершён.
    /// Идемпотентность обеспечивает сама служба (<c>NotifyLanded</c> одноразовый).
    /// </summary>
    public sealed class LandingZone : MonoBehaviour
    {
        private IDriverFlightService _flight;
        private bool _initialized;

        [Inject]
        public void Construct(IDriverFlightService flight)
        {
            _flight = flight;
            _initialized = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized) return;

            var driver = other.GetComponentInParent<DriverController>();
            if (driver == null) return;
            if (!driver.IsEjected) return;

            _flight.NotifyLanded();
        }
    }
}
