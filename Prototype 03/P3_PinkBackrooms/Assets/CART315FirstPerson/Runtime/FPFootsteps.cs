using System;
using UnityEngine;

namespace Cart315.FirstPerson
{
    [RequireComponent(typeof(FPController), typeof(AudioSource))]
    public class FPFootsteps : MonoBehaviour
    {
        [Tooltip("Leave empty for silent movement. Drag your own audio clips here.")]
        [SerializeField] private AudioClip[] _sounds = Array.Empty<AudioClip>();
        [Tooltip("Horizontal metres travelled between steps; sprinting produces faster steps.")]
        [SerializeField, Min(0.1f)] private float _stepDistance = 1.8f;
        [SerializeField, Range(0, 1)] private float _volume = 0.35f;

        public event Action FootstepPlayed;
        private FPController _player;
        private AudioSource _audioSource;
        private float _distance;
        private int _lastClip = -1;

        private void Awake()
        {
            _player = GetComponent<FPController>();
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
        }

        private void LateUpdate()
        {
            if (!_player.isActiveAndEnabled || !_player.IsGrounded || !_player.IsInputMoving)
            {
                _distance = 0;
                return;
            }
            _distance += _player.GroundDistanceThisFrame;
            if (_distance < Mathf.Max(0.1f, _stepDistance)) return;
            _distance %= Mathf.Max(0.1f, _stepDistance);
            if (_sounds == null || _sounds.Length == 0) return;

            // Skip empty slots, and avoid repeating the last clip when possible.
            int first = UnityEngine.Random.Range(0, _sounds.Length);
            int selected = -1;
            for (int i = 0; i < _sounds.Length; i++)
            {
                int index = (first + i) % _sounds.Length;
                if (_sounds[index] == null) continue;
                selected = index;
                if (index != _lastClip) break;
            }
            if (selected < 0) return;
            _audioSource.PlayOneShot(_sounds[selected], _volume);
            _lastClip = selected;
            FootstepPlayed?.Invoke();
        }

        private void OnDisable() => _distance = 0;
    }
}
