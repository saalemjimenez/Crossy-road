using System.Collections.Generic;
using UnityEngine;

namespace CrossyRoad.World
{
    /// <summary>
    /// Procedural endless lane manager for Crossy Road.
    /// Generates biome clusters ahead of the player and recycles lanes behind the camera.
    /// </summary>
    public class CrossyWorldGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        public int InitialBackLanes = 6;
        public int LookAheadLanes = 28;
        public int CleanupBehindLanes = 12;

        private readonly Dictionary<int, CrossyLane> _activeLanes = new Dictionary<int, CrossyLane>();
        private readonly Queue<LaneType> _upcomingBiomeQueue = new Queue<LaneType>();
        private int _highestGeneratedZ = -999;
        private LaneType _lastLaneType = LaneType.Grass;
        private readonly HashSet<int> _lastRiverSafeCols = new HashSet<int>();

        public void ResetWorld()
        {
            foreach (var kvp in _activeLanes)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _activeLanes.Clear();
            _upcomingBiomeQueue.Clear();
            _lastRiverSafeCols.Clear();

            _highestGeneratedZ = -InitialBackLanes - 1;
            _lastLaneType = LaneType.Grass;

            EnsureLanesUpTo(LookAheadLanes);
        }

        public void UpdateWorld(int playerZ, float cameraZ)
        {
            EnsureLanesUpTo(playerZ + LookAheadLanes);
            CleanupOldLanes(Mathf.FloorToInt(cameraZ) - CleanupBehindLanes);
        }

        private void EnsureLanesUpTo(int targetZ)
        {
            while (_highestGeneratedZ < targetZ)
            {
                int nextZ = _highestGeneratedZ + 1;
                LaneType type = DetermineNextLaneType(nextZ);
                SpawnLane(nextZ, type);
                _highestGeneratedZ = nextZ;
            }
        }

        private LaneType DetermineNextLaneType(int zIndex)
        {
            // Starting safe zone is always grass
            if (zIndex <= 2)
                return LaneType.Grass;

            if (_upcomingBiomeQueue.Count == 0)
            {
                EnqueueNextBiomeCluster(zIndex);
            }

            return _upcomingBiomeQueue.Dequeue();
        }

        private void EnqueueNextBiomeCluster(int zIndex)
        {
            float roll = Random.value;

            if (roll < 0.45f)
            {
                // Road cluster (1 to 4 lanes) followed by 1-2 grass lanes
                int count = Random.Range(1, 5);
                for (int i = 0; i < count; i++) _upcomingBiomeQueue.Enqueue(LaneType.Road);
                int grassAfter = Random.Range(1, 3);
                for (int i = 0; i < grassAfter; i++) _upcomingBiomeQueue.Enqueue(LaneType.Grass);
            }
            else if (roll < 0.72f && zIndex > 5)
            {
                // River cluster (1 to 3 lanes) followed by 1-2 grass lanes
                int count = Random.Range(1, 4);
                for (int i = 0; i < count; i++) _upcomingBiomeQueue.Enqueue(LaneType.River);
                int grassAfter = Random.Range(1, 3);
                for (int i = 0; i < grassAfter; i++) _upcomingBiomeQueue.Enqueue(LaneType.Grass);
            }
            else if (roll < 0.88f && zIndex > 8)
            {
                // Railroad cluster (1 to 2 tracks) followed by 1-2 grass lanes
                int count = (Random.value < 0.7f) ? 1 : 2;
                for (int i = 0; i < count; i++) _upcomingBiomeQueue.Enqueue(LaneType.Railroad);
                int grassAfter = Random.Range(1, 3);
                for (int i = 0; i < grassAfter; i++) _upcomingBiomeQueue.Enqueue(LaneType.Grass);
            }
            else
            {
                // Extra Grass cluster
                int count = Random.Range(2, 4);
                for (int i = 0; i < count; i++) _upcomingBiomeQueue.Enqueue(LaneType.Grass);
            }
        }

        private void SpawnLane(int zIndex, LaneType type)
        {
            GameObject laneObj = new GameObject($"Lane_{zIndex:D3}_{type}");
            laneObj.transform.SetParent(transform, false);

            CrossyLane lane = laneObj.AddComponent<CrossyLane>();
            float difficulty = 1.0f + Mathf.Clamp01(Mathf.Max(0, zIndex) / 160f) * 0.65f;
            bool prevSame = (type == _lastLaneType) && (zIndex > -InitialBackLanes);

            lane.Initialize(zIndex, type, prevSame, difficulty, _lastRiverSafeCols);

            _activeLanes[zIndex] = lane;
            _lastLaneType = type;
        }

        private void CleanupOldLanes(int minZToKeep)
        {
            List<int> toRemove = null;
            foreach (var kvp in _activeLanes)
            {
                if (kvp.Key < minZToKeep)
                {
                    if (toRemove == null) toRemove = new List<int>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    int z = toRemove[i];
                    if (_activeLanes.TryGetValue(z, out CrossyLane lane) && lane != null)
                    {
                        Destroy(lane.gameObject);
                    }
                    _activeLanes.Remove(z);
                }
            }
        }

        public CrossyLane GetLane(int zIndex)
        {
            _activeLanes.TryGetValue(zIndex, out CrossyLane lane);
            return lane;
        }

        public bool IsTileBlocked(int colX, int rowZ)
        {
            if (colX < CrossyLane.MinPlayableX || colX > CrossyLane.MaxPlayableX)
                return true;

            if (_activeLanes.TryGetValue(rowZ, out CrossyLane lane) && lane != null)
            {
                return lane.IsColumnBlocked(colX);
            }
            return false;
        }
    }
}
