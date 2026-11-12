using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrossyRoad.CameraSystem;
using CrossyRoad.Core;
using CrossyRoad.Player;
using CrossyRoad.Visuals;
using CrossyRoad.World;

namespace CrossyRoad.EditorTools
{
    public static class CrossySceneSetupEditor
    {
        [MenuItem("Crossy Road/Setup Scene Now")]
        public static void SetupScene()
        {
            // 1. Configure Directional Light
            Light dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.transform.rotation = Quaternion.Euler(52f, -36f, 0f);
                dirLight.color = new Color(1.0f, 0.97f, 0.90f);
                dirLight.intensity = 1.35f;
                dirLight.shadows = LightShadows.Soft;
            }

            // 2. Configure Main Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
            }

            mainCam.orthographic = false;
            mainCam.fieldOfView = 36f;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.52f, 0.84f, 0.98f);
            mainCam.transform.position = new Vector3(2.8f, 11.4f, -6.8f);
            mainCam.transform.rotation = Quaternion.Euler(48f, -16f, 0f);

            CrossyCameraController camCtrl = mainCam.GetComponent<CrossyCameraController>();
            if (camCtrl == null)
            {
                camCtrl = mainCam.gameObject.AddComponent<CrossyCameraController>();
            }

            // 3. Create or find CrossyGameManager
            CrossyGameManager gm = Object.FindAnyObjectByType<CrossyGameManager>();
            if (gm == null)
            {
                GameObject gmGo = new GameObject("CrossyGameManager");
                gm = gmGo.AddComponent<CrossyGameManager>();
            }

            // 4. Create or find CrossyWorldGenerator
            CrossyWorldGenerator worldGen = Object.FindAnyObjectByType<CrossyWorldGenerator>();
            if (worldGen == null)
            {
                GameObject wgGo = new GameObject("CrossyWorldGenerator");
                worldGen = wgGo.AddComponent<CrossyWorldGenerator>();
            }

            // 5. Create or find CrossyEffectsAndAudio
            CrossyEffectsAndAudio fx = Object.FindAnyObjectByType<CrossyEffectsAndAudio>();
            if (fx == null)
            {
                GameObject fxGo = new GameObject("CrossyEffectsAndAudio");
                fx = fxGo.AddComponent<CrossyEffectsAndAudio>();
            }

            // 6. Create or find CrossyPlayerController and build preview Chicken in scene
            CrossyPlayerController player = Object.FindAnyObjectByType<CrossyPlayerController>();
            if (player == null)
            {
                GameObject playerGo = new GameObject("CrossyPlayer_Chicken");
                player = playerGo.AddComponent<CrossyPlayerController>();
            }
            player.transform.position = Vector3.zero;

            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(player.transform.GetChild(i).gameObject);
            }
            GameObject tempChicken = CrossyVoxelBuilder.CreateChicken(out Transform pivot);
            pivot.SetParent(player.transform, false);
            Object.DestroyImmediate(tempChicken);

            // Wire references on GameManager
            gm.WorldGenerator = worldGen;
            gm.Player = player;
            gm.CameraController = camCtrl;
            gm.EffectsAndAudio = fx;

            // Frame SceneView camera nicely on the Chicken
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 0.5f, 2f), Quaternion.Euler(42f, -20f, 0f), 14f);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[CrossyRoad] Escena SampleScene configurada y guardada con éxito.");
        }
    }
}
