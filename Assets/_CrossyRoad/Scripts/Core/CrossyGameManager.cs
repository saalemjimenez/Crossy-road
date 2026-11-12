using UnityEngine;
using UnityEngine.InputSystem;
using CrossyRoad.CameraSystem;
using CrossyRoad.Player;
using CrossyRoad.Visuals;
using CrossyRoad.World;

namespace CrossyRoad.Core
{
    /// <summary>
    /// Master coordinator for the Crossy Road replica.
    /// Manages game flow, score, high score, coins, arcade HUD, and automatic scene wiring.
    /// </summary>
    public class CrossyGameManager : MonoBehaviour
    {
        private const string HighScoreKey = "CrossyRoad_HighScore";
        private const string CoinsKey = "CrossyRoad_TotalCoins";

        [Header("References")]
        public CrossyWorldGenerator WorldGenerator;
        public CrossyPlayerController Player;
        public CrossyCameraController CameraController;
        public CrossyEffectsAndAudio EffectsAndAudio;

        public int CurrentScore { get; private set; }
        public int HighScore { get; private set; }
        public int TotalCoins { get; private set; }
        public bool NewRecordThisRun { get; private set; }

        private DeathType _lastDeathCause;
        private float _gameOverTimer;
        private GUIStyle _scoreStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _buttonStyle;
        private Texture2D _darkOverlayTex;
        private Texture2D _goldBadgeTex;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            TotalCoins = PlayerPrefs.GetInt(CoinsKey, 0);

            SetupLighting();
            EnsureSystems();
        }

        private void Start()
        {
            StartNewGame();
        }

        private void OnEnable()
        {
            CrossyLane.OnTrainWarningStarted += HandleTrainWarning;
        }

        private void OnDisable()
        {
            CrossyLane.OnTrainWarningStarted -= HandleTrainWarning;
        }

        private void SetupLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.68f, 0.74f, 0.82f);

            Light dirLight = FindAnyObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.transform.rotation = Quaternion.Euler(52f, -36f, 0f);
                dirLight.color = new Color(1.0f, 0.97f, 0.90f);
                dirLight.intensity = 1.35f;
                dirLight.shadows = LightShadows.Soft;
            }
        }

        private void EnsureSystems()
        {
            if (WorldGenerator == null)
            {
                WorldGenerator = FindAnyObjectByType<CrossyWorldGenerator>();
                if (WorldGenerator == null)
                {
                    GameObject wgObj = new GameObject("CrossyWorldGenerator");
                    WorldGenerator = wgObj.AddComponent<CrossyWorldGenerator>();
                }
            }

            if (EffectsAndAudio == null)
            {
                EffectsAndAudio = FindAnyObjectByType<CrossyEffectsAndAudio>();
                if (EffectsAndAudio == null)
                {
                    GameObject fxObj = new GameObject("CrossyEffectsAndAudio");
                    EffectsAndAudio = fxObj.AddComponent<CrossyEffectsAndAudio>();
                }
            }
            EffectsAndAudio.Initialize();

            if (Player == null)
            {
                Player = FindAnyObjectByType<CrossyPlayerController>();
                if (Player == null)
                {
                    GameObject playerObj = new GameObject("CrossyPlayer_Chicken");
                    Player = playerObj.AddComponent<CrossyPlayerController>();
                }
            }

            Player.OnHopStarted -= HandlePlayerHop;
            Player.OnScoreChanged -= HandleScoreChanged;
            Player.OnCoinCollected -= HandleCoinCollected;
            Player.OnPlayerDied -= HandlePlayerDied;

            Player.OnHopStarted += HandlePlayerHop;
            Player.OnScoreChanged += HandleScoreChanged;
            Player.OnCoinCollected += HandleCoinCollected;
            Player.OnPlayerDied += HandlePlayerDied;

            if (CameraController == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam == null)
                {
                    GameObject camObj = new GameObject("Main Camera");
                    camObj.tag = "MainCamera";
                    mainCam = camObj.AddComponent<Camera>();
                }

                CameraController = mainCam.GetComponent<CrossyCameraController>();
                if (CameraController == null)
                {
                    CameraController = mainCam.gameObject.AddComponent<CrossyCameraController>();
                }
            }
        }

        public void StartNewGame()
        {
            CurrentScore = 0;
            NewRecordThisRun = false;
            _gameOverTimer = 0f;

            EffectsAndAudio.ClearEffects();
            WorldGenerator.ResetWorld();
            Player.Initialize(WorldGenerator);
            CameraController.Initialize(Player);
        }

        private void Update()
        {
            if (WorldGenerator != null && Player != null && CameraController != null)
            {
                WorldGenerator.UpdateWorld(Player.CurrentRowZ, CameraController.CurrentFocusZ);
            }

            if (Player != null && !Player.IsAlive)
            {
                _gameOverTimer += Time.deltaTime;

                Keyboard kb = Keyboard.current;
                if (_gameOverTimer > 0.55f && kb != null)
                {
                    if (kb.spaceKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                    {
                        StartNewGame();
                    }
                }
            }
        }

        private void HandlePlayerHop()
        {
            EffectsAndAudio.PlayHopSound();
        }

        private void HandleScoreChanged(int newScore)
        {
            CurrentScore = newScore;
            if (CurrentScore > HighScore)
            {
                HighScore = CurrentScore;
                NewRecordThisRun = true;
                PlayerPrefs.SetInt(HighScoreKey, HighScore);
                PlayerPrefs.Save();
            }
        }

        private void HandleCoinCollected()
        {
            TotalCoins++;
            PlayerPrefs.SetInt(CoinsKey, TotalCoins);
            PlayerPrefs.Save();

            if (Player != null)
            {
                EffectsAndAudio.PlayCoinEffect(Player.transform.position);
            }
        }

        private void HandlePlayerDied(DeathType cause, Vector3 deathPos)
        {
            _lastDeathCause = cause;
            _gameOverTimer = 0f;

            if (CameraController != null)
            {
                CameraController.TriggerShake(0.32f, 0.25f);
            }

            if (EffectsAndAudio != null)
            {
                EffectsAndAudio.HandlePlayerDeath(cause, deathPos);
            }
        }

        private void HandleTrainWarning(int laneZ)
        {
            if (Player != null && EffectsAndAudio != null)
            {
                EffectsAndAudio.PlayTrainBellIfClose(laneZ, Player.CurrentRowZ);
            }
        }

        private void InitStyles()
        {
            if (_scoreStyle != null)
                return;

            _scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 54,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _scoreStyle.normal.textColor = Color.white;

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _subHeaderStyle.normal.textColor = new Color(1f, 0.92f, 0.35f);

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = Color.white;

            _darkOverlayTex = new Texture2D(1, 1);
            _darkOverlayTex.SetPixel(0, 0, new Color(0.08f, 0.11f, 0.16f, 0.88f));
            _darkOverlayTex.Apply();

            _goldBadgeTex = new Texture2D(1, 1);
            _goldBadgeTex.SetPixel(0, 0, new Color(0.96f, 0.68f, 0.12f, 1f));
            _goldBadgeTex.Apply();

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _darkOverlayTex;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _buttonStyle.normal.background = _goldBadgeTex;
            _buttonStyle.normal.textColor = new Color(0.12f, 0.12f, 0.15f);
        }

        private void DrawShadowedText(Rect rect, string text, GUIStyle style, Color mainColor)
        {
            Color prev = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);
            style.normal.textColor = mainColor;
            GUI.Label(rect, text, style);
            style.normal.textColor = prev;
        }

        private void OnGUI()
        {
            InitStyles();

            // Top-left Score & High Score
            DrawShadowedText(new Rect(28f, 18f, 320f, 70f), CurrentScore.ToString(), _scoreStyle, Color.white);
            DrawShadowedText(new Rect(30f, 82f, 320f, 36f), $"TOP  {HighScore}", _subHeaderStyle, new Color(1f, 0.90f, 0.30f));

            // Top-right Coins
            GUIStyle rightStyle = new GUIStyle(_subHeaderStyle) { alignment = TextAnchor.UpperRight, fontSize = 28 };
            DrawShadowedText(new Rect(Screen.width - 280f, 24f, 250f, 45f), $"$ {TotalCoins}", rightStyle, new Color(1f, 0.86f, 0.18f));

            // Start Screen Prompt (before first hop)
            if (Player != null && Player.IsAlive && !Player.HasStartedMoving)
            {
                float pulse = Mathf.Sin(Time.time * 4.5f) * 4f;
                DrawShadowedText(new Rect(0f, 65f + pulse, Screen.width, 70f), "CROSSY ROAD", _titleStyle, Color.white);

                GUIStyle hintStyle = new GUIStyle(_subHeaderStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                DrawShadowedText(
                    new Rect(0f, Screen.height - 110f, Screen.width, 45f),
                    "PRESIONA [W / A / S / D] O [FLECHAS] O [ESPACIO] PARA SALTAR",
                    hintStyle,
                    new Color(1f, 0.96f, 0.65f)
                );
            }

            // Game Over Overlay
            if (Player != null && !Player.IsAlive && _gameOverTimer >= 0.55f)
            {
                float panelW = 460f;
                float panelH = 290f;
                float px = (Screen.width - panelW) * 0.5f;
                float py = (Screen.height - panelH) * 0.5f;

                GUI.Box(new Rect(px, py, panelW, panelH), GUIContent.none, _boxStyle);

                string deathTitle = "¡FIN DEL JUEGO!";
                if (_lastDeathCause == DeathType.Splat) deathTitle = "¡APLASTADO EN LA VÍA!";
                else if (_lastDeathCause == DeathType.Water) deathTitle = "¡CAÍSTE AL AGUA!";
                else if (_lastDeathCause == DeathType.Eagle) deathTitle = "¡TE LLEVÓ EL ÁGUILA!";

                GUIStyle goHeader = new GUIStyle(_titleStyle) { fontSize = 32 };
                DrawShadowedText(new Rect(px, py + 22f, panelW, 45f), deathTitle, goHeader, new Color(1f, 0.42f, 0.38f));

                GUIStyle centerStat = new GUIStyle(_subHeaderStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 26 };
                DrawShadowedText(new Rect(px, py + 82f, panelW, 38f), $"PUNTOS:  {CurrentScore}", centerStat, Color.white);

                string recordLabel = NewRecordThisRun ? $"¡NUEVO RÉCORD!:  {HighScore}" : $"MEJOR RÉCORD:  {HighScore}";
                DrawShadowedText(new Rect(px, py + 124f, panelW, 38f), recordLabel, centerStat, new Color(1f, 0.88f, 0.25f));

                if (GUI.Button(new Rect(px + 65f, py + 195f, panelW - 130f, 56f), "JUGAR DE NUEVO [ESPACIO]", _buttonStyle))
                {
                    StartNewGame();
                }
            }
        }
    }
}
