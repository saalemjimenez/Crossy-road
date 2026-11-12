using System.Collections.Generic;
using UnityEngine;
using CrossyRoad.Visuals;

namespace CrossyRoad.World
{
    public enum LaneType
    {
        Grass,
        Road,
        River,
        Railroad
    }

    public class MovingEntity
    {
        public Transform Transform;
        public float HalfLength;
    }

    /// <summary>
    /// Represents a single horizontal row (at integer Z) in Crossy Road.
    /// Handles obstacles, moving vehicles, floating logs, lilypads, railroad signals, and coins.
    /// </summary>
    public class CrossyLane : MonoBehaviour
    {
        public const int MinPlayableX = -6;
        public const int MaxPlayableX = 6;
        public const int OuterExtentX = 16;
        public const float WrapExtentX = 18.5f;

        public int ZIndex { get; private set; }
        public LaneType Type { get; private set; }
        public int Direction { get; private set; }
        public float Speed { get; private set; }
        public bool IsLilyPadRiver { get; private set; }

        private readonly HashSet<int> _blockedColumns = new HashSet<int>();
        private readonly HashSet<int> _lilyPadColumns = new HashSet<int>();
        private readonly Dictionary<int, GameObject> _coinsByColumn = new Dictionary<int, GameObject>();
        private readonly List<MovingEntity> _movers = new List<MovingEntity>();

        // Railroad state
        private Transform _trainTransform;
        private MeshRenderer _signalLampA;
        private MeshRenderer _signalLampB;
        private float _trainTimer;
        private float _trainCooldown;
        private bool _trainWarningActive;
        private bool _trainPassing;

        public static event System.Action<int> OnTrainWarningStarted;

        public void Initialize(int zIndex, LaneType type, bool prevWasSameType, float difficultyMultiplier, HashSet<int> prevRiverSafeCols = null)
        {
            ZIndex = zIndex;
            Type = type;
            transform.position = new Vector3(0f, 0f, zIndex);

            switch (type)
            {
                case LaneType.Grass:
                    BuildGrassLane(zIndex);
                    break;
                case LaneType.Road:
                    BuildRoadLane(zIndex, prevWasSameType, difficultyMultiplier);
                    break;
                case LaneType.River:
                    BuildRiverLane(zIndex, prevWasSameType, difficultyMultiplier, prevRiverSafeCols);
                    break;
                case LaneType.Railroad:
                    BuildRailroadLane(zIndex, difficultyMultiplier);
                    break;
            }
        }

        private void BuildGrassLane(int zIndex)
        {
            bool even = (Mathf.Abs(zIndex) % 2 == 0);
            Color playGrass = even ? CrossyVoxelBuilder.GrassLight : CrossyVoxelBuilder.GrassDark;

            // Playable strip (-6 to +6)
            CrossyVoxelBuilder.AddBox(transform, "GrassPlayable", new Vector3(0f, -0.15f, 0f), new Vector3(13f, 0.30f, 1f), playGrass);

            // Outer border strips
            CrossyVoxelBuilder.AddBox(transform, "GrassLeftBorder", new Vector3(-11.5f, -0.10f, 0f), new Vector3(10f, 0.40f, 1f), CrossyVoxelBuilder.GrassBorder);
            CrossyVoxelBuilder.AddBox(transform, "GrassRightBorder", new Vector3(11.5f, -0.10f, 0f), new Vector3(10f, 0.40f, 1f), CrossyVoxelBuilder.GrassBorder);

            // Outer dense trees so player sees boundary
            for (int x = -15; x <= 15; x++)
            {
                if (x < MinPlayableX || x > MaxPlayableX)
                {
                    if (Mathf.Abs(x) <= 10 || Random.value < 0.65f)
                    {
                        GameObject borderTree = CrossyVoxelBuilder.CreateTree(Mathf.Abs(x + zIndex));
                        borderTree.transform.SetParent(transform, false);
                        borderTree.transform.localPosition = new Vector3(x, 0f, 0f);
                    }
                }
            }

            // Starting safe area (-3 to +2 Z) has fewer obstacles and center is always clear
            if (zIndex <= 1 && zIndex >= -2)
            {
                for (int x = MinPlayableX; x <= MaxPlayableX; x++)
                {
                    if (Mathf.Abs(x) >= 4 && Random.value < 0.35f)
                    {
                        SpawnObstacleAt(x, zIndex);
                    }
                }
                return;
            }

            // Guarantee at least 3 open columns in playable area
            int guaranteedOpenA = Random.Range(-4, 0);
            int guaranteedOpenB = Random.Range(0, 5);

            for (int x = MinPlayableX; x <= MaxPlayableX; x++)
            {
                if (x == guaranteedOpenA || x == guaranteedOpenB)
                    continue;

                float roll = Random.value;
                if (roll < 0.28f)
                {
                    SpawnObstacleAt(x, zIndex);
                }
                else if (roll > 0.93f && zIndex > 2)
                {
                    SpawnCoinAt(x, 0f);
                }
            }
        }

        private void SpawnObstacleAt(int x, int zIndex)
        {
            _blockedColumns.Add(x);
            GameObject obs = (Random.value < 0.78f)
                ? CrossyVoxelBuilder.CreateTree(Random.Range(0, 6) + Mathf.Abs(zIndex))
                : CrossyVoxelBuilder.CreateBoulder();

            obs.transform.SetParent(transform, false);
            obs.transform.localPosition = new Vector3(x, 0f, 0f);
        }

        private void BuildRoadLane(int zIndex, bool prevWasRoad, float difficultyMultiplier)
        {
            CrossyVoxelBuilder.AddBox(transform, "Asphalt", new Vector3(0f, -0.18f, 0f), new Vector3(OuterExtentX * 2f + 4f, 0.30f, 1f), CrossyVoxelBuilder.RoadAsphalt);

            // Side curbs / trees outside playable area
            CrossyVoxelBuilder.AddBox(transform, "SideLeft", new Vector3(-11.5f, -0.14f, 0f), new Vector3(10f, 0.34f, 1f), CrossyVoxelBuilder.GrassBorder);
            CrossyVoxelBuilder.AddBox(transform, "SideRight", new Vector3(11.5f, -0.14f, 0f), new Vector3(10f, 0.34f, 1f), CrossyVoxelBuilder.GrassBorder);

            // Dashed white lines between adjacent road lanes
            if (prevWasRoad)
            {
                for (int x = -6; x <= 6; x += 2)
                {
                    CrossyVoxelBuilder.AddBox(transform, "Dash", new Vector3(x, -0.025f, -0.48f), new Vector3(0.75f, 0.02f, 0.08f), CrossyVoxelBuilder.RoadStripe);
                }
            }

            Direction = (Random.value < 0.5f) ? 1 : -1;
            bool isTruckLane = Random.value < 0.32f;
            float baseSpeed = isTruckLane ? Random.Range(2.4f, 4.0f) : Random.Range(3.2f, 6.4f);
            Speed = baseSpeed * difficultyMultiplier;

            int vehicleCount = isTruckLane ? Random.Range(2, 4) : Random.Range(2, 5);
            float totalSpan = WrapExtentX * 2f;
            float spacing = totalSpan / vehicleCount;
            float startOffset = Random.Range(0f, spacing);
            int colorSeed = Random.Range(0, 10);

            for (int i = 0; i < vehicleCount; i++)
            {
                float xPos = -WrapExtentX + startOffset + i * spacing + Random.Range(-0.8f, 0.8f);
                GameObject vehicle = isTruckLane
                    ? CrossyVoxelBuilder.CreateTruck(colorSeed + i)
                    : CrossyVoxelBuilder.CreateCar(colorSeed + i);

                vehicle.transform.SetParent(transform, false);
                vehicle.transform.localPosition = new Vector3(xPos, 0f, 0f);
                if (Direction < 0)
                {
                    vehicle.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }

                _movers.Add(new MovingEntity
                {
                    Transform = vehicle.transform,
                    HalfLength = isTruckLane ? 1.05f : 0.62f
                });
            }

            if (Random.value < 0.18f)
            {
                SpawnCoinAt(Random.Range(MinPlayableX + 1, MaxPlayableX), 0f);
            }
        }

        private void BuildRiverLane(int zIndex, bool prevWasRiver, float difficultyMultiplier, HashSet<int> prevRiverSafeCols)
        {
            CrossyVoxelBuilder.AddBox(transform, "WaterBed", new Vector3(0f, -0.28f, 0f), new Vector3(OuterExtentX * 2f + 4f, 0.28f, 1f), CrossyVoxelBuilder.WaterBright);

            // Foam details
            for (int x = -6; x <= 6; x += 3)
            {
                CrossyVoxelBuilder.AddBox(transform, "Foam", new Vector3(x + Random.Range(-0.3f, 0.3f), -0.13f, -0.38f), new Vector3(0.45f, 0.03f, 0.08f), CrossyVoxelBuilder.WaterFoam);
            }

            // Decide if LilyPad lane or Floating Log lane (avoid two LilyPad lanes in a row unless aligned)
            IsLilyPadRiver = !prevWasRiver && (Random.value < 0.35f);

            if (IsLilyPadRiver)
            {
                Speed = 0f;
                Direction = 0;

                // Ensure at least 3-4 lilypads in playable zone
                int count = Random.Range(3, 6);
                for (int i = 0; i < count; i++)
                {
                    int col = Random.Range(MinPlayableX + 1, MaxPlayableX);
                    if (_lilyPadColumns.Add(col))
                    {
                        GameObject pad = CrossyVoxelBuilder.CreateLilyPad(Random.value < 0.35f);
                        pad.transform.SetParent(transform, false);
                        pad.transform.localPosition = new Vector3(col, -0.14f, 0f);
                    }
                }

                // Always guarantee at least one near center
                if (_lilyPadColumns.Count == 0)
                {
                    _lilyPadColumns.Add(0);
                    GameObject pad = CrossyVoxelBuilder.CreateLilyPad(true);
                    pad.transform.SetParent(transform, false);
                    pad.transform.localPosition = new Vector3(0f, -0.14f, 0f);
                }

                if ( prevRiverSafeCols != null )
                {
                    prevRiverSafeCols.Clear();
                    foreach (int c in _lilyPadColumns) prevRiverSafeCols.Add(c);
                }
            }
            else
            {
                Direction = prevWasRiver ? -1 : ((Random.value < 0.5f) ? 1 : -1);
                if (Random.value < 0.5f) Direction *= -1;

                int logTileLength = Random.Range(2, 5); // 2, 3, or 4 tiles long
                Speed = Random.Range(1.6f, 3.3f) * difficultyMultiplier;

                int logCount = (logTileLength >= 4) ? 3 : 4;
                float totalSpan = WrapExtentX * 2f;
                float spacing = totalSpan / logCount;
                float startOffset = Random.Range(0f, spacing);

                for (int i = 0; i < logCount; i++)
                {
                    float xPos = -WrapExtentX + startOffset + i * spacing;
                    GameObject log = CrossyVoxelBuilder.CreateLog(logTileLength);
                    log.transform.SetParent(transform, false);
                    log.transform.localPosition = new Vector3(xPos, -0.10f, 0f);

                    _movers.Add(new MovingEntity
                    {
                        Transform = log.transform,
                        HalfLength = logTileLength * 0.5f
                    });
                }
            }
        }

        private void BuildRailroadLane(int zIndex, float difficultyMultiplier)
        {
            CrossyVoxelBuilder.AddBox(transform, "GravelBed", new Vector3(0f, -0.18f, 0f), new Vector3(OuterExtentX * 2f + 4f, 0.28f, 1f), CrossyVoxelBuilder.RailGravel);

            // Wooden ties
            for (int x = -14; x <= 14; x++)
            {
                CrossyVoxelBuilder.AddBox(transform, "Tie", new Vector3(x, -0.03f, 0f), new Vector3(0.32f, 0.04f, 0.82f), CrossyVoxelBuilder.RailTie);
            }

            // Steel rails
            CrossyVoxelBuilder.AddBox(transform, "RailA", new Vector3(0f, 0.01f, -0.26f), new Vector3(OuterExtentX * 2f + 4f, 0.06f, 0.08f), CrossyVoxelBuilder.RailMetal);
            CrossyVoxelBuilder.AddBox(transform, "RailB", new Vector3(0f, 0.01f, 0.26f), new Vector3(OuterExtentX * 2f + 4f, 0.06f, 0.08f), CrossyVoxelBuilder.RailMetal);

            // Signal post at side of track
            GameObject signal = CrossyVoxelBuilder.CreateRailSignal(out _signalLampA, out _signalLampB);
            signal.transform.SetParent(transform, false);
            signal.transform.localPosition = new Vector3(-4.5f, 0f, -0.42f);

            Direction = (Random.value < 0.5f) ? 1 : -1;
            Speed = 30.0f * Mathf.Clamp(difficultyMultiplier, 1f, 1.35f);

            GameObject train = CrossyVoxelBuilder.CreateTrain();
            train.transform.SetParent(transform, false);
            train.transform.localPosition = new Vector3(-Direction * 32f, 0f, 0f);
            train.SetActive(false);
            _trainTransform = train.transform;

            _trainCooldown = Random.Range(3.2f, 6.5f);
            _trainTimer = Random.Range(0.5f, _trainCooldown);
        }

        private void SpawnCoinAt(int x, float yOffset)
        {
            if (_blockedColumns.Contains(x) || _coinsByColumn.ContainsKey(x))
                return;

            GameObject coin = CrossyVoxelBuilder.CreateCoin();
            coin.transform.SetParent(transform, false);
            coin.transform.localPosition = new Vector3(x, yOffset, 0f);
            _coinsByColumn[x] = coin;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Spin coins
            foreach (var kvp in _coinsByColumn)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.transform.Rotate(0f, 120f * dt, 0f, Space.Self);
                }
            }

            if (Type == LaneType.Road || (Type == LaneType.River && !IsLilyPadRiver))
            {
                float moveDelta = Direction * Speed * dt;
                for (int i = 0; i < _movers.Count; i++)
                {
                    Transform t = _movers[i].Transform;
                    Vector3 pos = t.localPosition;
                    pos.x += moveDelta;

                    if (Direction > 0 && pos.x > WrapExtentX)
                        pos.x -= WrapExtentX * 2f;
                    else if (Direction < 0 && pos.x < -WrapExtentX)
                        pos.x += WrapExtentX * 2f;

                    t.localPosition = pos;
                }
            }
            else if (Type == LaneType.Railroad)
            {
                UpdateRailroad(dt);
            }
        }

        private void UpdateRailroad(float dt)
        {
            _trainTimer += dt;

            if (!_trainWarningActive && !_trainPassing)
            {
                if (_trainTimer >= _trainCooldown)
                {
                    _trainWarningActive = true;
                    _trainTimer = 0f;
                    OnTrainWarningStarted?.Invoke(ZIndex);
                }
            }
            else if (_trainWarningActive && !_trainPassing)
            {
                // Alternate blinking red lights every 0.15s for 1.8s before train blasts through
                bool phase = Mathf.FloorToInt(_trainTimer * 7f) % 2 == 0;
                Color brightRed = new Color(1.0f, 0.12f, 0.12f);
                Color darkRed = new Color(0.22f, 0.06f, 0.06f);

                if (_signalLampA != null)
                    _signalLampA.sharedMaterial = CrossyVoxelBuilder.GetMaterial(phase ? brightRed : darkRed, 0.2f, phase);
                if (_signalLampB != null)
                    _signalLampB.sharedMaterial = CrossyVoxelBuilder.GetMaterial(!phase ? brightRed : darkRed, 0.2f, !phase);

                if (_trainTimer >= 1.8f)
                {
                    _trainPassing = true;
                    _trainTimer = 0f;
                    _trainTransform.gameObject.SetActive(true);
                    _trainTransform.localPosition = new Vector3(-Direction * 28f, 0f, 0f);
                }
            }
            else if (_trainPassing)
            {
                Vector3 pos = _trainTransform.localPosition;
                pos.x += Direction * Speed * dt;
                _trainTransform.localPosition = pos;

                if ((Direction > 0 && pos.x > 28f) || (Direction < 0 && pos.x < -28f))
                {
                    _trainPassing = false;
                    _trainWarningActive = false;
                    _trainTransform.gameObject.SetActive(false);
                    _trainTimer = 0f;
                    _trainCooldown = Random.Range(4.0f, 7.2f);

                    Color darkRed = new Color(0.22f, 0.06f, 0.06f);
                    if (_signalLampA != null)
                        _signalLampA.sharedMaterial = CrossyVoxelBuilder.GetMaterial(darkRed);
                    if (_signalLampB != null)
                        _signalLampB.sharedMaterial = CrossyVoxelBuilder.GetMaterial(darkRed);
                }
            }
        }

        public bool IsColumnBlocked(int col)
        {
            if (col < MinPlayableX || col > MaxPlayableX)
                return true;
            return _blockedColumns.Contains(col);
        }

        public bool TryCollectCoin(int col)
        {
            if (_coinsByColumn.TryGetValue(col, out GameObject coin) && coin != null)
            {
                _coinsByColumn.Remove(col);
                Destroy(coin);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the given world X position collides with a vehicle or train on this lane.
        /// </summary>
        public bool CheckVehicleCollision(float playerX, float playerHalfWidth = 0.24f)
        {
            if (Type == LaneType.Road)
            {
                for (int i = 0; i < _movers.Count; i++)
                {
                    float vx = _movers[i].Transform.localPosition.x;
                    float half = _movers[i].HalfLength;
                    if (Mathf.Abs(playerX - vx) < (half + playerHalfWidth))
                        return true;
                }
            }
            else if (Type == LaneType.Railroad && _trainPassing && _trainTransform != null)
            {
                float tx = _trainTransform.localPosition.x;
                if (Mathf.Abs(playerX - tx) < (6.0f + playerHalfWidth))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// For River lanes, checks if the player at playerX is safely on a LilyPad or Floating Log.
        /// </summary>
        public bool CheckRiverSafety(float playerX, out float logVelocityX, out float snappedX)
        {
            logVelocityX = 0f;
            snappedX = playerX;

            if (Type != LaneType.River)
                return true;

            if (IsLilyPadRiver)
            {
                int rounded = Mathf.RoundToInt(playerX);
                if (_lilyPadColumns.Contains(rounded) && Mathf.Abs(playerX - rounded) < 0.45f)
                {
                    snappedX = rounded;
                    return true;
                }
                return false;
            }

            // Floating logs
            for (int i = 0; i < _movers.Count; i++)
            {
                float lx = _movers[i].Transform.localPosition.x;
                float half = _movers[i].HalfLength;
                if (Mathf.Abs(playerX - lx) <= (half + 0.12f))
                {
                    logVelocityX = Direction * Speed;
                    return true;
                }
            }

            return false;
        }
    }
}
