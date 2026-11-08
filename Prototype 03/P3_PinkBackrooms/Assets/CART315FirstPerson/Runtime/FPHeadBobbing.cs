using UnityEngine;

namespace Cart315.FirstPerson
{
    [RequireComponent(typeof(FPController))]
    public class FPHeadBobbing : MonoBehaviour
    {
        [Tooltip("Uncheck to keep a steady camera. Safe to change during Play.")]
        [SerializeField] private bool _enableHeadBob = true;
        [Tooltip("Vertical movement in metres. A small value is more comfortable.")]
        [SerializeField, Range(0, 0.08f)] private float _intensity = 0.025f;
        [Tooltip("Bobs per second at walking speed. Sprint cadence follows actual speed.")]
        [SerializeField, Range(0.1f, 4)] private float _frequency = 1.6f;

        private FPController _player;
        private Transform _cameraHolder;
        private Vector3 _restPosition;
        private float _phase;

        private void Start()
        {
            _player = GetComponent<FPController>();
            _cameraHolder = _player.CameraHolder;
            if (_cameraHolder != null) _restPosition = _cameraHolder.localPosition;
        }

        private void LateUpdate()
        {
            if (_cameraHolder == null) return;
            Vector3 target = _restPosition;
            if (_enableHeadBob && _player.isActiveAndEnabled && _player.IsGrounded && _player.IsInputMoving)
            {
                _phase += _player.GroundDistanceThisFrame / Mathf.Max(0.1f, _player.WalkSpeed)
                    * _frequency * Mathf.PI * 2;
                target.y += Mathf.Sin(_phase) * _intensity;
            }
            else _phase = 0;

            _cameraHolder.localPosition = Vector3.Lerp(_cameraHolder.localPosition, target,
                1f - Mathf.Exp(-14f * Time.deltaTime));
        }

        private void OnDisable()
        {
            if (_cameraHolder != null) _cameraHolder.localPosition = _restPosition;
            _phase = 0;
        }
    }
}
