using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cart315.FirstPerson
{
    [DisallowMultipleComponent]
    public class FPInputManager : MonoBehaviour
    {
        [Tooltip("Edit bindings in this asset. Each player makes its own runtime copy.")]
        [SerializeField] private InputActionAsset _actions;

        public Vector2 MovementInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintInput { get; private set; }
        public event Action OnJumpPressed;
        public bool HasControl => CursorCapture.IsCaptured;
        internal ICursorCapture CursorCapture { get; set; } = new UnityCursorCapture();

        private InputActionAsset _runtimeActions;
        private InputAction _move, _look, _sprint, _jump, _capture, _release;
        private bool _hadControl;

        private void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError("Assign CART315Controls to Input Actions on the player prefab.", this);
                enabled = false;
                return;
            }
            _runtimeActions = Instantiate(_actions);
            _move = _runtimeActions.FindAction("Player/Move", true);
            _look = _runtimeActions.FindAction("Player/Look", true);
            _sprint = _runtimeActions.FindAction("Player/Sprint", true);
            _jump = _runtimeActions.FindAction("Player/Jump", true);
            _capture = _runtimeActions.FindAction("Player/CaptureCursor", true);
            _release = _runtimeActions.FindAction("Player/ReleaseCursor", true);
        }

        private void OnEnable() => _runtimeActions?.Enable();

        // Called by the movement component, so input is read before movement every frame.
        internal void ReadInput()
        {
            ClearInput();
            if (!isActiveAndEnabled || _runtimeActions == null) return;

            if (_release.WasPressedThisFrame()) ReleaseCursor();
            else if (_capture.WasPressedThisFrame() && CursorCapture.IsFocused)
            {
                CursorCapture.Capture();
            }

            if (!HasControl || Time.timeScale == 0f)
            {
                _hadControl = false;
                return;
            }
            // Ignore the capture frame's mouse delta to avoid a sudden camera turn.
            if (!_hadControl)
            {
                _hadControl = true;
                return;
            }
            MovementInput = _move.ReadValue<Vector2>();
            LookInput = _look.ReadValue<Vector2>();
            SprintInput = _sprint.IsPressed();
            if (_jump.WasPressedThisFrame()) OnJumpPressed?.Invoke();
        }

        public void ReleaseCursor()
        {
            CursorCapture.Release();
            _hadControl = false;
            ClearInput();
        }

        private void ClearInput()
        {
            MovementInput = Vector2.zero;
            LookInput = Vector2.zero;
            SprintInput = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ReleaseCursor();
        }

        private void OnDisable()
        {
            _runtimeActions?.Disable();
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            if (_runtimeActions != null) Destroy(_runtimeActions);
        }
    }

    // Native cursor ownership is a platform boundary, separate from input/movement logic.
    internal interface ICursorCapture
    {
        bool IsCaptured { get; }
        bool IsFocused { get; }
        void Capture();
        void Release();
    }

    internal sealed class UnityCursorCapture : ICursorCapture
    {
        public bool IsCaptured => Cursor.lockState == CursorLockMode.Locked;
        public bool IsFocused => Application.isFocused;
        public void Capture()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        public void Release()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
