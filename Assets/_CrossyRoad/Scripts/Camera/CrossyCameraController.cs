using UnityEngine;
using CrossyRoad.Player;

namespace CrossyRoad.CameraSystem
{
    /// <summary>
    /// Isometric-style angled follow camera for Crossy Road.
    /// Continuously creeps forward and tracks the player's progress, triggering the Eagle if left behind.
    /// </summary>
    public class CrossyCameraController : MonoBehaviour
    {
        [Header("Camera Framing")]
        public Vector3 FollowOffset = new Vector3(2.8f, 11.4f, -7.8f);
        public Vector3 CameraEuler = new Vector3(48f, -16f, 0f);
        public float FieldOfView = 36f;

        [Header("Scrolling & Tracking")]
        public float BaseCreepSpeed = 1.05f;
        public float CatchUpLerpSpeed = 6.5f;
        public float BehindLimitRows = 4.2f;

        public float CurrentFocusZ { get; private set; }
        public float CurrentFocusX { get; private set; }

        private CrossyPlayerController _player;
        private Camera _cam;
        private float _shakeTimer;
        private float _shakeIntensity;

        public void Initialize(CrossyPlayerController player)
        {
            _player = player;
            _cam = GetComponent<Camera>();
            if (_cam == null)
            {
                _cam = Camera.main;
            }

            if (_cam != null)
            {
                _cam.orthographic = false;
                _cam.fieldOfView = FieldOfView;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.52f, 0.84f, 0.98f);
            }

            ResetCamera();
        }

        public void ResetCamera()
        {
            CurrentFocusZ = 1.0f;
            CurrentFocusX = 0f;
            _shakeTimer = 0f;
            ApplyCameraTransform(0f);
        }

        public void TriggerShake(float intensity = 0.28f, float duration = 0.22f)
        {
            _shakeIntensity = intensity;
            _shakeTimer = duration;
        }

        private void LateUpdate()
        {
            if (_player == null)
                return;

            float dt = Time.deltaTime;

            if (_player.IsAlive)
            {
                if (_player.HasStartedMoving)
                {
                    float speedBoost = 1f + Mathf.Clamp01(_player.MaxRowReached / 120f) * 0.55f;
                    CurrentFocusZ += BaseCreepSpeed * speedBoost * dt;
                }

                float desiredZ = _player.CurrentRowZ + 1.0f;
                if (desiredZ > CurrentFocusZ)
                {
                    CurrentFocusZ = Mathf.Lerp(CurrentFocusZ, desiredZ, dt * CatchUpLerpSpeed);
                }

                float desiredX = Mathf.Clamp(_player.CurrentX * 0.48f, -2.8f, 2.8f);
                CurrentFocusX = Mathf.Lerp(CurrentFocusX, desiredX, dt * 6f);

                // Check if player fell too far behind camera
                if (_player.HasStartedMoving && _player.CurrentRowZ < (CurrentFocusZ - BehindLimitRows))
                {
                    _player.TriggerOutOfCameraDeath();
                }
            }

            ApplyCameraTransform(dt);
        }

        private void ApplyCameraTransform(float dt)
        {
            Vector3 focusPoint = new Vector3(CurrentFocusX, 0f, CurrentFocusZ);
            Vector3 targetPos = focusPoint + FollowOffset;

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= dt;
                targetPos += Random.insideUnitSphere * _shakeIntensity;
            }

            transform.position = targetPos;
            transform.rotation = Quaternion.Euler(CameraEuler);
        }
    }
}
