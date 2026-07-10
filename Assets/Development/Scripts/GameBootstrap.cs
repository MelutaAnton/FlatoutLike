using FlatOutLike.Core.Input;
using FlatOutLike.Services.Flow;
using FlatOutLike.Services.Meta;
using UnityEngine;
using VContainer;

namespace FlatOutLike
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private IGameFlowService _flow;
        private IInputProvider _input;
        private InputRouter _router;
        private IMetaProgressService _meta;
        private bool _initialized;

        [Inject]
        public void Construct(
            IGameFlowService flow,
            IInputProvider input,
            InputRouter router,
            IMetaProgressService meta)
        {
            _flow = flow;
            _input = input;
            _router = router;
            _meta = meta;

            // Свежий per-scene scope стартовое GameStateChangedEvent не публикует —
            // выравниваем input-контекст с начальным состоянием сцены явно
            // (иначе UI-сцены остались бы на Driving-дефолте провайдера).
            _router.SyncTo(_flow.Current);

            Debug.Log($"[FlatOutLike] Bootstrap initialized. State: {_flow.Current}, Coins: {_meta.Profile.Coins}");

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
                return;

            _router.Tick();
            _flow.Tick();
        }
    }
}
