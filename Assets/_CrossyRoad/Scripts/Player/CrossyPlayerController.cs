using System;
using UnityEngine;
using UnityEngine.InputSystem;
using CrossyRoad.Visuals;
using CrossyRoad.World;

namespace CrossyRoad.Player
{
    public enum DeathType
    {
        Splat,
        Water,
        Eagle
    }

    /// <summary>
    /// Controls the Crossy Road Chicken: grid hopping, squash & stretch animations,
    /// riding floating river logs, collecting coins, and hazard collisions.
    /// </summary>
    public class CrossyPlayerController : MonoBehaviour
    {
        [Header("Hop Settings")]
        public float HopDuration = 0.115f;
        public float HopArcHeight = 0.55f;
        public float IdleEagleTimeout = 7.5f;

        public int CurrentRowZ { get; private set; }
        public float CurrentX { get; private set; }
        public int MaxRowReached { get; private set; }
        public bool IsAlive { get; private set; }
        public bool HasStartedMoving { get; private set; }
        public float TimeSinceLastAdvance { get; private set; }

        private CrossyWorldGenerator _world;
        private Transform _visualPivot;

        // Hop state
        private bool _isHopping;
        private float _hopTimer;
        private Vector3 _hopStartPos;
        private Vector3 _hopEndPos;
        private Quaternion _targetRotation = Quaternion.identity;
        private Vector2Int _queuedDir = Vector2Int.zero;

        // Bump (blocked by tree) state
        private bool _isBumping;
        private float _bumpTimer;
        private Vector3 _bumpDir;

        // Post-land squash recovery
        private float _landSquashTimer;

        // Death animation state
        private DeathType _deathType;
        private float _deathAnimTimer;

        public event Action<int> OnScoreChanged;
        public event Action OnCoinCollected;
        public event Action OnHopStarted;
        public event Action<DeathType, Vector3> OnPlayerDied;

        public void Initialize(CrossyWorldGenerator world)
        {
            _world = world;

            if (_visualPivot == null)
            {
                // Clear any existing children and build the voxel chicken
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(transform.GetChild(i).gameObject);
                }

                GameObject chickenModel = CrossyVoxelBuilder.CreateChicken(out _visualPivot);
                _visualPivot.SetParent(transform, false);
                Destroy(chickenModel);
            }

            ResetPlayer();
        }

        public void ResetPlayer()
        {
            CurrentRowZ = 0;
            CurrentX = 0f;
            MaxRowReached = 0;
            IsAlive = true;
            HasStartedMoving = false;
            TimeSinceLastAdvance = 0f;

            _isHopping = false;
            _isBumping = false;
            _queuedDir = Vector2Int.zero;
            _landSquashTimer = 0f;
            _targetRotation = Quaternion.identity;

            transform.position = Vector3.zero;
            if (_visualPivot != null)
            {
                _visualPivot.gameObject.SetActive(true);
                _visualPivot.localPosition = Vector3.zero;
                _visualPivot.localRotation = Quaternion.identity;
                _visualPivot.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (_world == null)
                return;

            if (!IsAlive)
            {
                UpdateDeathAnimation(Time.deltaTime);
                return;
            }

            ReadInput();

            float dt = Time.deltaTime;

            if (HasStartedMoving)
            {
                TimeSinceLastAdvance += dt;
                if (TimeSinceLastAdvance >= IdleEagleTimeout && !_isHopping)
                {
                    Die(DeathType.Eagle);
                    return;
                }
            }

            if (_isBumping)
            {
                UpdateBump(dt);
            }
            else if (_isHopping)
            {
                UpdateHop(dt);
            }
            else
            {
                // Try executing queued input
                if (_queuedDir != Vector2Int.zero)
                {
                    Vector2Int dir = _queuedDir;
                    _queuedDir = Vector2Int.zero;
                    TryStartHop(dir);
                }
            }

            // Smooth rotation of VisualPivot
            if (_visualPivot != null)
            {
                _visualPivot.localRotation = Quaternion.Slerp(_visualPivot.localRotation, _targetRotation, dt * 24f);

                if (!_isHopping && _landSquashTimer > 0f)
                {
                    _landSquashTimer -= dt * 8f;
                    float s = Mathf.Clamp01(_landSquashTimer);
                    _visualPivot.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.18f, 0.78f, 1.18f), s);
                }
            }

            // Check current lane hazards and floating logs
            CheckLaneInteractions(dt);
        }

        private void ReadInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            Vector2Int inputDir = Vector2Int.zero;

            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                inputDir = new Vector2Int(0, 1);
            }
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                inputDir = new Vector2Int(0, -1);
            }
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            {
                inputDir = new Vector2Int(-1, 0);
            }
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                inputDir = new Vector2Int(1, 0);
            }

            if (inputDir != Vector2Int.zero)
            {
                if (!_isHopping && !_isBumping)
                {
                    TryStartHop(inputDir);
                }
                else
                {
                    // Buffer 1 upcoming hop for fast arcade tapping
                    _queuedDir = inputDir;
                }
            }
        }

        private void TryStartHop(Vector2Int dir)
        {
            HasStartedMoving = true;

            if (dir.x > 0) _targetRotation = Quaternion.Euler(0f, 90f, 0f);
            else if (dir.x < 0) _targetRotation = Quaternion.Euler(0f, -90f, 0f);
            else if (dir.y < 0) _targetRotation = Quaternion.Euler(0f, 180f, 0f);
            else _targetRotation = Quaternion.identity;

            int targetZ = CurrentRowZ + dir.y;
            if (targetZ < -3)
                return;

            CrossyLane targetLane = _world.GetLane(targetZ);
            bool targetIsFloatingLogRiver = targetLane != null && targetLane.Type == LaneType.River && !targetLane.IsLilyPadRiver;

            // Snap X to integer grid unless hopping onto/along a floating log river
            float nextX = targetIsFloatingLogRiver
                ? (CurrentX + dir.x)
                : Mathf.Round(CurrentX + dir.x);

            int checkCol = Mathf.RoundToInt(nextX);

            // Prevent jumping out of horizontal bounds or into a tree/boulder
            if (checkCol < CrossyLane.MinPlayableX || checkCol > CrossyLane.MaxPlayableX || _world.IsTileBlocked(checkCol, targetZ))
            {
                _isBumping = true;
                _bumpTimer = 0f;
                _bumpDir = new Vector3(dir.x, 0f, dir.y) * 0.22f;
                return;
            }

            _isHopping = true;
            _hopTimer = 0f;
            _hopStartPos = transform.position;
            _hopEndPos = new Vector3(nextX, 0f, targetZ);

            CurrentRowZ = targetZ;
            CurrentX = nextX;

            OnHopStarted?.Invoke();
        }

        private void UpdateHop(float dt)
        {
            _hopTimer += dt;
            float t = Mathf.Clamp01(_hopTimer / HopDuration);

            Vector3 flatPos = Vector3.Lerp(_hopStartPos, _hopEndPos, t);
            float arcY = Mathf.Sin(t * Mathf.PI) * HopArcHeight;
            transform.position = new Vector3(flatPos.x, arcY, flatPos.z);

            if (_visualPivot != null)
            {
                float stretch = Mathf.Sin(t * Mathf.PI);
                _visualPivot.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.84f, 1.24f, 0.84f), stretch);
            }

            if (t >= 1f)
            {
                _isHopping = false;
                _landSquashTimer = 1f;
                transform.position = _hopEndPos;

                if (CurrentRowZ > MaxRowReached)
                {
                    MaxRowReached = CurrentRowZ;
                    TimeSinceLastAdvance = 0f;
                    OnScoreChanged?.Invoke(MaxRowReached);
                }

                // Coin collection check
                CrossyLane lane = _world.GetLane(CurrentRowZ);
                if (lane != null && lane.TryCollectCoin(Mathf.RoundToInt(CurrentX)))
                {
                    OnCoinCollected?.Invoke();
                }
            }
        }

        private void UpdateBump(float dt)
        {
            _bumpTimer += dt;
            float t = Mathf.Clamp01(_bumpTimer / 0.09f);
            float offset = Mathf.Sin(t * Mathf.PI);
            if (_visualPivot != null)
            {
                _visualPivot.localPosition = _bumpDir * offset;
                _visualPivot.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.15f, 0.85f, 0.85f), offset);
            }

            if (t >= 1f)
            {
                _isBumping = false;
                if (_visualPivot != null)
                {
                    _visualPivot.localPosition = Vector3.zero;
                    _visualPivot.localScale = Vector3.one;
                }
            }
        }

        private void CheckLaneInteractions(float dt)
        {
            CrossyLane lane = _world.GetLane(CurrentRowZ);
            if (lane == null)
                return;

            // Vehicle / Train collision check (even mid-hop if low enough)
            if (lane.CheckVehicleCollision(transform.position.x))
            {
                Die(DeathType.Splat);
                return;
            }

            // River safety & log riding (checked when landed on the lane)
            if (!_isHopping && lane.Type == LaneType.River)
            {
                if (lane.CheckRiverSafety(CurrentX, out float logVelX, out float snappedX))
                {
                    if (lane.IsLilyPadRiver)
                    {
                        CurrentX = snappedX;
                    }
                    else
                    {
                        CurrentX += logVelX * dt;
                    }

                    transform.position = new Vector3(CurrentX, 0.05f, CurrentRowZ);

                    // Carried off-screen by a floating log
                    if (Mathf.Abs(CurrentX) > CrossyLane.MaxPlayableX + 1.1f)
                    {
                        Die(DeathType.Water);
                    }
                }
                else
                {
                    Die(DeathType.Water);
                }
            }
        }

        public void TriggerOutOfCameraDeath()
        {
            if (IsAlive)
            {
                Die(DeathType.Eagle);
            }
        }

        private void Die(DeathType cause)
        {
            if (!IsAlive)
                return;

            IsAlive = false;
            _isHopping = false;
            _isBumping = false;
            _deathType = cause;
            _deathAnimTimer = 0f;

            if (_visualPivot != null)
            {
                _visualPivot.localPosition = Vector3.zero;
                if (cause == DeathType.Splat)
                {
                    // Flattened pancake on road/track
                    _visualPivot.localScale = new Vector3(1.60f, 0.06f, 1.60f);
                    transform.position = new Vector3(transform.position.x, 0.02f, CurrentRowZ);
                }
            }

            OnPlayerDied?.Invoke(cause, transform.position);
        }

        private void UpdateDeathAnimation(float dt)
        {
            _deathAnimTimer += dt;
            if (_deathType == DeathType.Water)
            {
                // Sink into the river
                Vector3 pos = transform.position;
                pos.y = Mathf.MoveTowards(pos.y, -1.2f, dt * 4.5f);
                transform.position = pos;
            }
            else if (_deathType == DeathType.Eagle && _deathAnimTimer > 0.32f)
            {
                // Snatched away by the swooping Eagle
                if (_visualPivot != null)
                {
                    _visualPivot.gameObject.SetActive(false);
                }
            }
        }
    }
}
