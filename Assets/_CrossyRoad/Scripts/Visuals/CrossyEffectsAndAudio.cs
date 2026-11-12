using System.Collections.Generic;
using UnityEngine;
using CrossyRoad.Player;

namespace CrossyRoad.Visuals
{
    /// <summary>
    /// Handles procedural voxel particles (feathers, water splashes, coin sparkles),
    /// the swooping Eagle animation, and synthesized arcade sound effects.
    /// </summary>
    public class CrossyEffectsAndAudio : MonoBehaviour
    {
        private class VoxelParticle
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            public float Lifetime;
            public float MaxLifetime;
        }

        private readonly List<VoxelParticle> _particles = new List<VoxelParticle>();

        // Eagle swoop state
        private GameObject _eagleObject;
        private bool _eagleActive;
        private float _eagleTimer;
        private Vector3 _eagleTargetPos;

        // Synthesized audio
        private AudioSource _sfxSource;
        private AudioClip _hopClip;
        private AudioClip _coinClip;
        private AudioClip _splatClip;
        private AudioClip _splashClip;
        private AudioClip _eagleClip;
        private AudioClip _trainBellClip;

        public void Initialize()
        {
            _sfxSource = gameObject.GetComponent<AudioSource>();
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
            }
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            _sfxSource.volume = 0.65f;

            GenerateProceduralAudioClips();

            if (_eagleObject == null)
            {
                _eagleObject = CrossyVoxelBuilder.CreateEagle();
                _eagleObject.transform.SetParent(transform, false);
                _eagleObject.transform.localScale = Vector3.one * 1.35f;
                _eagleObject.SetActive(false);
            }
        }

        public void ClearEffects()
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                if (_particles[i].Transform != null)
                {
                    Destroy(_particles[i].Transform.gameObject);
                }
            }
            _particles.Clear();

            _eagleActive = false;
            if (_eagleObject != null)
            {
                _eagleObject.SetActive(false);
            }
        }

        public void PlayHopSound()
        {
            if (_sfxSource != null && _hopClip != null)
            {
                _sfxSource.pitch = Random.Range(0.94f, 1.08f);
                _sfxSource.PlayOneShot(_hopClip, 0.55f);
            }
        }

        public void PlayCoinEffect(Vector3 worldPos)
        {
            if (_sfxSource != null && _coinClip != null)
            {
                _sfxSource.pitch = 1.0f;
                _sfxSource.PlayOneShot(_coinClip, 0.75f);
            }

            Color gold = new Color(1.0f, 0.85f, 0.15f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 vel = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3.2f, 5.8f), Random.Range(-2.5f, 2.5f));
                SpawnVoxelParticle(worldPos + Vector3.up * 0.35f, new Vector3(0.11f, 0.11f, 0.11f), gold, vel, 0.55f, true);
            }
        }

        public void PlayTrainBellIfClose(int laneZ, int playerZ)
        {
            if (Mathf.Abs(laneZ - playerZ) <= 7 && _sfxSource != null && _trainBellClip != null)
            {
                _sfxSource.pitch = 1.0f;
                _sfxSource.PlayOneShot(_trainBellClip, 0.45f);
            }
        }

        public void HandlePlayerDeath(DeathType cause, Vector3 deathPos)
        {
            if (_sfxSource != null)
            {
                _sfxSource.pitch = 1.0f;
            }

            if (cause == DeathType.Splat)
            {
                if (_sfxSource != null && _splatClip != null)
                    _sfxSource.PlayOneShot(_splatClip, 0.8f);

                SpawnFeathers(deathPos, 16);
            }
            else if (cause == DeathType.Water)
            {
                if (_sfxSource != null && _splashClip != null)
                    _sfxSource.PlayOneShot(_splashClip, 0.8f);

                SpawnWaterSplash(deathPos);
            }
            else if (cause == DeathType.Eagle)
            {
                if (_sfxSource != null && _eagleClip != null)
                    _sfxSource.PlayOneShot(_eagleClip, 0.85f);

                _eagleActive = true;
                _eagleTimer = 0f;
                _eagleTargetPos = deathPos;
                if (_eagleObject != null)
                {
                    _eagleObject.SetActive(true);
                    _eagleObject.transform.rotation = Quaternion.Euler(15f, 180f, 0f);
                }
            }
        }

        private void SpawnFeathers(Vector3 origin, int count)
        {
            Color white = new Color(0.97f, 0.97f, 0.99f);
            for (int i = 0; i < count; i++)
            {
                Vector3 vel = new Vector3(Random.Range(-4.2f, 4.2f), Random.Range(2.8f, 6.5f), Random.Range(-4.2f, 4.2f));
                SpawnVoxelParticle(origin + Vector3.up * 0.25f, new Vector3(0.14f, 0.06f, 0.20f), white, vel, Random.Range(0.7f, 1.2f));
            }
        }

        private void SpawnWaterSplash(Vector3 origin)
        {
            for (int i = 0; i < 20; i++)
            {
                Color col = (i % 3 == 0) ? CrossyVoxelBuilder.WaterFoam : CrossyVoxelBuilder.WaterBright;
                Vector3 vel = new Vector3(Random.Range(-3.2f, 3.2f), Random.Range(3.5f, 7.2f), Random.Range(-3.2f, 3.2f));
                SpawnVoxelParticle(origin, new Vector3(0.15f, 0.15f, 0.15f), col, vel, Random.Range(0.55f, 0.95f));
            }
        }

        private void SpawnVoxelParticle(Vector3 pos, Vector3 scale, Color col, Vector3 velocity, float lifetime, bool emissive = false)
        {
            GameObject pObj = CrossyVoxelBuilder.AddBox(transform, "VoxelParticle", pos, scale, col, emissive);
            _particles.Add(new VoxelParticle
            {
                Transform = pObj.transform,
                Velocity = velocity,
                AngularVelocity = new Vector3(Random.Range(-360f, 360f), Random.Range(-360f, 360f), Random.Range(-360f, 360f)),
                Lifetime = 0f,
                MaxLifetime = lifetime
            });
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Update voxel particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                VoxelParticle p = _particles[i];
                if (p.Transform == null)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                p.Lifetime += dt;
                if (p.Lifetime >= p.MaxLifetime)
                {
                    Destroy(p.Transform.gameObject);
                    _particles.RemoveAt(i);
                    continue;
                }

                p.Velocity.y -= 13.5f * dt;
                p.Transform.position += p.Velocity * dt;
                p.Transform.Rotate(p.AngularVelocity * dt, Space.Self);

                float remain = 1f - (p.Lifetime / p.MaxLifetime);
                if (remain < 0.35f)
                {
                    p.Transform.localScale *= 0.92f;
                }
            }

            // Update Eagle swoop
            if (_eagleActive && _eagleObject != null)
            {
                float prevT = _eagleTimer;
                _eagleTimer += dt;

                // At t=0.32s the eagle passes right through _eagleTargetPos
                float normalized = (_eagleTimer - 0.32f) / 0.32f; // -1 at start, 0 at snatch, +1 at exit
                Vector3 pos = _eagleTargetPos + new Vector3(0f, Mathf.Abs(normalized) * 7.5f + 0.45f, -normalized * 12.0f);
                _eagleObject.transform.position = pos;

                if (prevT < 0.32f && _eagleTimer >= 0.32f)
                {
                    SpawnFeathers(_eagleTargetPos, 12);
                }

                if (_eagleTimer > 0.95f)
                {
                    _eagleActive = false;
                    _eagleObject.SetActive(false);
                }
            }
        }

        private void GenerateProceduralAudioClips()
        {
            int sampleRate = 44100;

            // 1. Hop Clip (short rising pop)
            _hopClip = CreateSynthesizedClip("Hop", 0.08f, sampleRate, (t, dur) =>
            {
                float progress = t / dur;
                float freq = Mathf.Lerp(260f, 580f, progress);
                float env = 1f - progress;
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            });

            // 2. Coin Clip (two-tone B5 -> E6 arcade chime)
            _coinClip = CreateSynthesizedClip("Coin", 0.24f, sampleRate, (t, dur) =>
            {
                float freq = (t < 0.08f) ? 987.77f : 1318.51f;
                float env = 1f - (t / dur);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
            });

            // 3. Splat Clip (low crunch + car horn)
            _splatClip = CreateSynthesizedClip("Splat", 0.32f, sampleRate, (t, dur) =>
            {
                float env = 1f - (t / dur);
                float noise = (Mathf.PerlinNoise(t * 900f, 0f) * 2f - 1f) * 0.5f;
                float horn = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 185f * t)) * 0.35f;
                return (noise + horn) * env;
            });

            // 4. Water Splash Clip
            _splashClip = CreateSynthesizedClip("Splash", 0.36f, sampleRate, (t, dur) =>
            {
                float env = 1f - (t / dur);
                float noise = (Mathf.PerlinNoise(t * 1400f, 1.5f) * 2f - 1f) * 0.6f;
                float bubble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 140f, t / dur) * t) * 0.3f;
                return (noise + bubble) * env;
            });

            // 5. Eagle Screech Clip
            _eagleClip = CreateSynthesizedClip("Eagle", 0.45f, sampleRate, (t, dur) =>
            {
                float env = Mathf.Sin((t / dur) * Mathf.PI);
                float freq = Mathf.Lerp(1650f, 820f, t / dur);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
            });

            // 6. Train Bell Clip
            _trainBellClip = CreateSynthesizedClip("TrainBell", 0.28f, sampleRate, (t, dur) =>
            {
                float localT = t % 0.14f;
                float env = 1f - (localT / 0.14f);
                return Mathf.Sin(2f * Mathf.PI * 1120f * t) * env * 0.5f;
            });
        }

        private AudioClip CreateSynthesizedClip(string clipName, float duration, int sampleRate, System.Func<float, float, float> sampleFunc)
        {
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Clamp(sampleFunc(t, duration), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
