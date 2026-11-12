using System.Collections.Generic;
using UnityEngine;

namespace CrossyRoad.Visuals
{
    /// <summary>
    /// Procedural generator for authentic Crossy Road style voxel models and shared URP materials.
    /// </summary>
    public static class CrossyVoxelBuilder
    {
        private static readonly Dictionary<Color32, Material> MaterialCache = new Dictionary<Color32, Material>();
        private static Shader _cachedShader;
        private static Mesh _cubeMesh;

        // Crossy Road Color Palette
        public static readonly Color GrassLight = new Color(0.49f, 0.84f, 0.29f);
        public static readonly Color GrassDark = new Color(0.44f, 0.78f, 0.25f);
        public static readonly Color GrassBorder = new Color(0.35f, 0.65f, 0.20f);

        public static readonly Color RoadAsphalt = new Color(0.32f, 0.34f, 0.38f);
        public static readonly Color RoadStripe = new Color(0.88f, 0.90f, 0.92f);

        public static readonly Color WaterBright = new Color(0.22f, 0.72f, 0.96f);
        public static readonly Color WaterFoam = new Color(0.78f, 0.94f, 1.00f);

        public static readonly Color RailGravel = new Color(0.45f, 0.42f, 0.40f);
        public static readonly Color RailTie = new Color(0.38f, 0.25f, 0.16f);
        public static readonly Color RailMetal = new Color(0.72f, 0.75f, 0.78f);

        private static Shader GetShader()
        {
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
                if (_cachedShader == null)
                    _cachedShader = Shader.Find("Standard");
            }
            return _cachedShader;
        }

        public static Material GetMaterial(Color color, float smoothness = 0.15f, bool emissive = false)
        {
            Color32 key = color;
            if (emissive)
            {
                key.a = 200; // Distinguish emissive in cache
            }

            if (MaterialCache.TryGetValue(key, out Material mat) && mat != null)
            {
                return mat;
            }

            mat = new Material(GetShader());
            mat.name = $"M_Voxel_{ ColorUtility.ToHtmlStringRGB(color) }";
            mat.enableInstancing = true;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", smoothness);

            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", color * 2.2f);
            }

            MaterialCache[key] = mat;
            return mat;
        }

        private static Mesh GetCubeMesh()
        {
            if (_cubeMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(temp);
            }
            return _cubeMesh;
        }

        public static GameObject AddBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color, bool emissive = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetCubeMesh();

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetMaterial(color, 0.12f, emissive);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            return go;
        }

        /// <summary>
        /// Creates the iconic Crossy Road Chicken with a separate VisualPivot child for squash/stretch/hop animations.
        /// </summary>
        public static GameObject CreateChicken(out Transform visualPivot)
        {
            GameObject root = new GameObject("CrossyChicken");
            GameObject pivot = new GameObject("VisualPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = Vector3.zero;
            visualPivot = pivot.transform;

            Color white = new Color(0.97f, 0.97f, 0.99f);
            Color wingWhite = new Color(0.90f, 0.91f, 0.95f);
            Color orange = new Color(0.98f, 0.55f, 0.12f);
            Color red = new Color(0.92f, 0.16f, 0.22f);
            Color black = new Color(0.12f, 0.12f, 0.15f);

            // Legs & Feet
            AddBox(visualPivot, "FootL", new Vector3(-0.11f, 0.04f, 0.04f), new Vector3(0.08f, 0.08f, 0.18f), orange);
            AddBox(visualPivot, "FootR", new Vector3(0.11f, 0.04f, 0.04f), new Vector3(0.08f, 0.08f, 0.18f), orange);
            AddBox(visualPivot, "LegL", new Vector3(-0.11f, 0.13f, 0.0f), new Vector3(0.05f, 0.14f, 0.05f), orange);
            AddBox(visualPivot, "LegR", new Vector3(0.11f, 0.13f, 0.0f), new Vector3(0.05f, 0.14f, 0.05f), orange);

            // Main blocky body + head combined into classic Crossy silhouette
            AddBox(visualPivot, "BodyLower", new Vector3(0f, 0.34f, -0.04f), new Vector3(0.42f, 0.32f, 0.50f), white);
            AddBox(visualPivot, "HeadUpper", new Vector3(0f, 0.62f, 0.04f), new Vector3(0.38f, 0.34f, 0.36f), white);
            AddBox(visualPivot, "Tail", new Vector3(0f, 0.42f, -0.32f), new Vector3(0.24f, 0.20f, 0.12f), white);

            // Wings
            AddBox(visualPivot, "WingL", new Vector3(-0.24f, 0.35f, -0.04f), new Vector3(0.07f, 0.20f, 0.30f), wingWhite);
            AddBox(visualPivot, "WingR", new Vector3(0.24f, 0.35f, -0.04f), new Vector3(0.07f, 0.20f, 0.30f), wingWhite);

            // Beak & Wattle & Comb
            AddBox(visualPivot, "Beak", new Vector3(0f, 0.62f, 0.28f), new Vector3(0.16f, 0.12f, 0.16f), orange);
            AddBox(visualPivot, "Wattle", new Vector3(0f, 0.51f, 0.24f), new Vector3(0.10f, 0.11f, 0.10f), red);
            AddBox(visualPivot, "Comb", new Vector3(0f, 0.84f, 0.05f), new Vector3(0.10f, 0.12f, 0.22f), red);

            // Eyes
            AddBox(visualPivot, "EyeL", new Vector3(-0.195f, 0.66f, 0.11f), new Vector3(0.02f, 0.06f, 0.06f), black);
            AddBox(visualPivot, "EyeR", new Vector3(0.195f, 0.66f, 0.11f), new Vector3(0.02f, 0.06f, 0.06f), black);

            return root;
        }

        /// <summary>
        /// Creates a stylized tiered voxel tree in 1 of 4 height/color variants.
        /// </summary>
        public static GameObject CreateTree(int variant)
        {
            GameObject root = new GameObject("VoxelTree");

            Color trunkCol = new Color(0.38f, 0.24f, 0.14f);
            Color leafMain = (variant % 2 == 0)
                ? new Color(0.28f, 0.68f, 0.22f)
                : new Color(0.36f, 0.74f, 0.20f);
            Color leafLight = Color.Lerp(leafMain, new Color(0.85f, 0.96f, 0.35f), 0.25f);
            Color leafDark = Color.Lerp(leafMain, new Color(0.10f, 0.30f, 0.12f), 0.25f);

            int tiers = 2 + (variant % 3); // 2, 3, or 4 tiers
            AddBox(root.transform, "Trunk", new Vector3(0f, 0.28f, 0f), new Vector3(0.32f, 0.56f, 0.32f), trunkCol);

            float currentY = 0.55f;
            for (int i = 0; i < tiers; i++)
            {
                float width = 0.78f - i * 0.08f;
                float height = 0.36f;
                Color tierColor = (i == tiers - 1) ? leafLight : (i == 0 ? leafDark : leafMain);
                AddBox(root.transform, $"Crown_{i}", new Vector3(0f, currentY + height * 0.5f, 0f), new Vector3(width, height, width), tierColor);
                currentY += height * 0.88f;
            }

            return root;
        }

        /// <summary>
        /// Creates a voxel boulder obstacle for grass lanes.
        /// </summary>
        public static GameObject CreateBoulder()
        {
            GameObject root = new GameObject("VoxelBoulder");
            Color greyMain = new Color(0.56f, 0.60f, 0.64f);
            Color greyLight = new Color(0.68f, 0.72f, 0.76f);
            Color greyDark = new Color(0.44f, 0.47f, 0.52f);

            AddBox(root.transform, "Base", new Vector3(0f, 0.22f, 0f), new Vector3(0.74f, 0.44f, 0.70f), greyMain);
            AddBox(root.transform, "Top", new Vector3(-0.05f, 0.48f, 0.04f), new Vector3(0.48f, 0.22f, 0.46f), greyLight);
            AddBox(root.transform, "Side", new Vector3(0.22f, 0.16f, -0.15f), new Vector3(0.36f, 0.32f, 0.38f), greyDark);

            return root;
        }

        /// <summary>
        /// Creates a Crossy Road voxel car facing +X by default (rotated by spawner based on direction).
        /// </summary>
        public static GameObject CreateCar(int colorIndex)
        {
            GameObject root = new GameObject("VoxelCar");

            Color[] bodyColors =
            {
                new Color(0.92f, 0.22f, 0.22f), // Red
                new Color(0.20f, 0.58f, 0.95f), // Blue
                new Color(0.98f, 0.76f, 0.14f), // Yellow
                new Color(0.64f, 0.30f, 0.90f), // Purple
                new Color(0.24f, 0.82f, 0.42f)  // Green
            };

            Color bodyCol = bodyColors[Mathf.Abs(colorIndex) % bodyColors.Length];
            Color roofCol = new Color(0.95f, 0.96f, 0.98f);
            Color glassCol = new Color(0.18f, 0.24f, 0.32f);
            Color wheelCol = new Color(0.14f, 0.14f, 0.16f);
            Color headlightCol = new Color(1.0f, 0.95f, 0.55f);
            Color taillightCol = new Color(0.95f, 0.15f, 0.15f);

            // Chassis
            AddBox(root.transform, "Chassis", new Vector3(0f, 0.28f, 0f), new Vector3(1.25f, 0.36f, 0.72f), bodyCol);
            // Cabin
            AddBox(root.transform, "CabinGlass", new Vector3(-0.06f, 0.54f, 0f), new Vector3(0.68f, 0.24f, 0.64f), glassCol);
            AddBox(root.transform, "Roof", new Vector3(-0.06f, 0.67f, 0f), new Vector3(0.62f, 0.06f, 0.62f), roofCol);

            // Wheels
            AddBox(root.transform, "WheelFL", new Vector3(0.36f, 0.14f, 0.36f), new Vector3(0.26f, 0.26f, 0.10f), wheelCol);
            AddBox(root.transform, "WheelFR", new Vector3(0.36f, 0.14f, -0.36f), new Vector3(0.26f, 0.26f, 0.10f), wheelCol);
            AddBox(root.transform, "WheelBL", new Vector3(-0.36f, 0.14f, 0.36f), new Vector3(0.26f, 0.26f, 0.10f), wheelCol);
            AddBox(root.transform, "WheelBR", new Vector3(-0.36f, 0.14f, -0.36f), new Vector3(0.26f, 0.26f, 0.10f), wheelCol);

            // Lights (+X is front)
            AddBox(root.transform, "HeadlightL", new Vector3(0.63f, 0.28f, 0.22f), new Vector3(0.04f, 0.10f, 0.12f), headlightCol, true);
            AddBox(root.transform, "HeadlightR", new Vector3(0.63f, 0.28f, -0.22f), new Vector3(0.04f, 0.10f, 0.12f), headlightCol, true);
            AddBox(root.transform, "TaillightL", new Vector3(-0.63f, 0.28f, 0.22f), new Vector3(0.04f, 0.10f, 0.12f), taillightCol, true);
            AddBox(root.transform, "TaillightR", new Vector3(-0.63f, 0.28f, -0.22f), new Vector3(0.04f, 0.10f, 0.12f), taillightCol, true);

            return root;
        }

        /// <summary>
        /// Creates a longer Crossy Road semi-truck facing +X.
        /// </summary>
        public static GameObject CreateTruck(int colorIndex)
        {
            GameObject root = new GameObject("VoxelTruck");

            Color cabCol = (colorIndex % 2 == 0) ? new Color(0.20f, 0.52f, 0.90f) : new Color(0.88f, 0.26f, 0.20f);
            Color trailerCol = new Color(0.94f, 0.95f, 0.97f);
            Color stripeCol = cabCol;
            Color glassCol = new Color(0.18f, 0.24f, 0.32f);
            Color wheelCol = new Color(0.14f, 0.14f, 0.16f);
            Color headlightCol = new Color(1.0f, 0.95f, 0.55f);

            // Cab (front +X)
            AddBox(root.transform, "Cab", new Vector3(0.75f, 0.44f, 0f), new Vector3(0.72f, 0.68f, 0.76f), cabCol);
            AddBox(root.transform, "Windshield", new Vector3(0.95f, 0.56f, 0f), new Vector3(0.36f, 0.26f, 0.68f), glassCol);

            // Trailer (back -X)
            AddBox(root.transform, "Trailer", new Vector3(-0.35f, 0.54f, 0f), new Vector3(1.42f, 0.86f, 0.80f), trailerCol);
            AddBox(root.transform, "Stripe", new Vector3(-0.35f, 0.54f, 0f), new Vector3(1.44f, 0.16f, 0.82f), stripeCol);

            // Wheels
            float[] wheelX = { 0.80f, -0.05f, -0.75f };
            foreach (float wx in wheelX)
            {
                AddBox(root.transform, "WheelL", new Vector3(wx, 0.15f, 0.38f), new Vector3(0.28f, 0.28f, 0.12f), wheelCol);
                AddBox(root.transform, "WheelR", new Vector3(wx, 0.15f, -0.38f), new Vector3(0.28f, 0.28f, 0.12f), wheelCol);
            }

            AddBox(root.transform, "HeadlightL", new Vector3(1.12f, 0.28f, 0.24f), new Vector3(0.04f, 0.12f, 0.14f), headlightCol, true);
            AddBox(root.transform, "HeadlightR", new Vector3(1.12f, 0.28f, -0.24f), new Vector3(0.04f, 0.12f, 0.14f), headlightCol, true);

            return root;
        }

        /// <summary>
        /// Creates a high-speed train of length ~12 units facing +X.
        /// </summary>
        public static GameObject CreateTrain()
        {
            GameObject root = new GameObject("VoxelTrain");

            Color bodyCol = new Color(0.88f, 0.90f, 0.94f);
            Color stripeRed = new Color(0.90f, 0.18f, 0.20f);
            Color noseYellow = new Color(0.98f, 0.80f, 0.15f);
            Color darkBase = new Color(0.22f, 0.24f, 0.28f);
            Color windowCol = new Color(0.20f, 0.32f, 0.45f);
            Color lightCol = new Color(1.0f, 0.96f, 0.65f);

            float length = 12.0f;
            AddBox(root.transform, "Undercarriage", new Vector3(0f, 0.20f, 0f), new Vector3(length, 0.24f, 0.82f), darkBase);
            AddBox(root.transform, "Body", new Vector3(0f, 0.66f, 0f), new Vector3(length, 0.72f, 0.86f), bodyCol);
            AddBox(root.transform, "RedStripe", new Vector3(0f, 0.45f, 0f), new Vector3(length + 0.02f, 0.16f, 0.88f), stripeRed);

            // Front & Back Noses
            AddBox(root.transform, "FrontNose", new Vector3(length * 0.5f + 0.05f, 0.45f, 0f), new Vector3(0.12f, 0.46f, 0.84f), noseYellow);
            AddBox(root.transform, "BackNose", new Vector3(-length * 0.5f - 0.05f, 0.45f, 0f), new Vector3(0.12f, 0.46f, 0.84f), noseYellow);

            // Headlights on both ends
            AddBox(root.transform, "FrontLight", new Vector3(length * 0.5f + 0.12f, 0.82f, 0f), new Vector3(0.06f, 0.16f, 0.28f), lightCol, true);
            AddBox(root.transform, "BackLight", new Vector3(-length * 0.5f - 0.12f, 0.82f, 0f), new Vector3(0.06f, 0.16f, 0.28f), lightCol, true);

            // Windows along the train
            for (float x = -4.8f; x <= 4.8f; x += 1.2f)
            {
                AddBox(root.transform, "Win", new Vector3(x, 0.74f, 0f), new Vector3(0.75f, 0.26f, 0.90f), windowCol);
            }

            return root;
        }

        /// <summary>
        /// Creates a railroad crossing warning post with two light renderers for blinking before a train arrives.
        /// </summary>
        public static GameObject CreateRailSignal(out MeshRenderer lightA, out MeshRenderer lightB)
        {
            GameObject root = new GameObject("RailSignal");
            Color poleCol = new Color(0.28f, 0.30f, 0.34f);
            Color signWhite = new Color(0.95f, 0.95f, 0.97f);
            Color lampOff = new Color(0.25f, 0.08f, 0.08f);

            AddBox(root.transform, "Pole", new Vector3(0f, 0.65f, 0f), new Vector3(0.10f, 1.30f, 0.10f), poleCol);
            AddBox(root.transform, "CrossBar", new Vector3(0f, 1.05f, 0f), new Vector3(0.56f, 0.10f, 0.10f), poleCol);

            // Crossbuck X
            GameObject x1 = AddBox(root.transform, "X1", new Vector3(0f, 1.32f, 0f), new Vector3(0.46f, 0.09f, 0.06f), signWhite);
            x1.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            GameObject x2 = AddBox(root.transform, "X2", new Vector3(0f, 1.32f, 0f), new Vector3(0.46f, 0.09f, 0.06f), signWhite);
            x2.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);

            GameObject goA = AddBox(root.transform, "LampA", new Vector3(-0.20f, 1.05f, -0.06f), new Vector3(0.16f, 0.16f, 0.12f), lampOff);
            GameObject goB = AddBox(root.transform, "LampB", new Vector3(0.20f, 1.05f, -0.06f), new Vector3(0.16f, 0.16f, 0.12f), lampOff);

            lightA = goA.GetComponent<MeshRenderer>();
            lightB = goB.GetComponent<MeshRenderer>();

            return root;
        }

        /// <summary>
        /// Creates a floating wooden log of specified tile length (e.g. 2, 3, or 4).
        /// </summary>
        public static GameObject CreateLog(int tileLength)
        {
            GameObject root = new GameObject($"VoxelLog_{tileLength}");
            Color bark = new Color(0.42f, 0.25f, 0.13f);
            Color barkStripe = new Color(0.34f, 0.20f, 0.10f);
            Color woodEnd = new Color(0.78f, 0.58f, 0.34f);

            float len = tileLength - 0.18f;
            AddBox(root.transform, "BarkMain", new Vector3(0f, -0.02f, 0f), new Vector3(len, 0.32f, 0.68f), bark);
            AddBox(root.transform, "BarkTop", new Vector3(0f, 0.02f, 0f), new Vector3(len - 0.06f, 0.28f, 0.52f), barkStripe);
            AddBox(root.transform, "WoodEnds", new Vector3(0f, -0.02f, 0f), new Vector3(len + 0.04f, 0.24f, 0.54f), woodEnd);

            return root;
        }

        /// <summary>
        /// Creates a green lilypad on water.
        /// </summary>
        public static GameObject CreateLilyPad(bool withFlower = false)
        {
            GameObject root = new GameObject("VoxelLilyPad");
            Color padGreen = new Color(0.24f, 0.74f, 0.30f);
            Color padRim = new Color(0.18f, 0.60f, 0.24f);

            AddBox(root.transform, "PadBase", new Vector3(0f, 0.04f, 0f), new Vector3(0.72f, 0.08f, 0.72f), padRim);
            AddBox(root.transform, "PadTop", new Vector3(0f, 0.07f, 0f), new Vector3(0.62f, 0.06f, 0.62f), padGreen);

            if (withFlower)
            {
                Color pink = new Color(0.98f, 0.58f, 0.78f);
                AddBox(root.transform, "Lotus", new Vector3(0.08f, 0.14f, 0.05f), new Vector3(0.18f, 0.12f, 0.18f), pink);
            }

            return root;
        }

        /// <summary>
        /// Creates a spinning golden Crossy Road coin.
        /// </summary>
        public static GameObject CreateCoin()
        {
            GameObject root = new GameObject("CrossyCoin");
            Color gold = new Color(1.0f, 0.82f, 0.12f);
            Color goldBright = new Color(1.0f, 0.94f, 0.45f);

            AddBox(root.transform, "Outer", new Vector3(0f, 0.36f, 0f), new Vector3(0.34f, 0.34f, 0.10f), gold, true);
            AddBox(root.transform, "Inner", new Vector3(0f, 0.36f, 0f), new Vector3(0.14f, 0.20f, 0.13f), goldBright, true);

            return root;
        }

        /// <summary>
        /// Creates the giant swooping Eagle that snatches the player on timeout.
        /// </summary>
        public static GameObject CreateEagle()
        {
            GameObject root = new GameObject("VoxelEagle");
            Color brown = new Color(0.32f, 0.20f, 0.12f);
            Color white = new Color(0.96f, 0.96f, 0.98f);
            Color yellow = new Color(0.98f, 0.74f, 0.14f);

            AddBox(root.transform, "Body", new Vector3(0f, 0.4f, 0f), new Vector3(0.70f, 0.65f, 1.10f), brown);
            AddBox(root.transform, "Head", new Vector3(0f, 0.65f, 0.55f), new Vector3(0.52f, 0.48f, 0.52f), white);
            AddBox(root.transform, "Beak", new Vector3(0f, 0.58f, 0.90f), new Vector3(0.24f, 0.24f, 0.30f), yellow);
            AddBox(root.transform, "WingL", new Vector3(-1.35f, 0.55f, 0f), new Vector3(2.10f, 0.14f, 0.75f), brown);
            AddBox(root.transform, "WingR", new Vector3(1.35f, 0.55f, 0f), new Vector3(2.10f, 0.14f, 0.75f), brown);
            AddBox(root.transform, "Tail", new Vector3(0f, 0.45f, -0.70f), new Vector3(0.55f, 0.14f, 0.45f), white);

            return root;
        }
    }
}
