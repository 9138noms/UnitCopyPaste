using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnitCopyPaste
{
    [BepInPlugin("com.noms.unitcopypaste", "UnitCopyPaste", "2.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony harmony;
        private static EditorInputHandler handlerInstance;

        void Awake()
        {
            Log = Logger;
            harmony = new Harmony("com.noms.unitcopypaste");
            harmony.PatchAll();

            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo("UnitCopyPaste v2.1.0 loaded");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"[UCP] Scene loaded: {scene.name}, creating handler...");
            if (handlerInstance == null)
            {
                var go = new GameObject("[UnitCopyPasteHandler]");
                handlerInstance = go.AddComponent<EditorInputHandler>();
                Object.DontDestroyOnLoad(go);
                Log.LogInfo("[UCP] Handler created");
            }
            else
            {
                Log.LogInfo($"[UCP] Handler already exists, active={handlerInstance.gameObject.activeSelf}");
            }
        }
    }

    internal class EditorInputHandler : MonoBehaviour
    {
        private int logCounter = 0;

        void Update()
        {
            logCounter++;
            if (logCounter % 600 == 0)
            {
                bool editorExists = SceneSingleton<MissionEditor>.i != null;
                Plugin.Log.LogInfo($"[UCP] heartbeat frame={logCounter} editor={editorExists}");
            }

            if (SceneSingleton<MissionEditor>.i == null) return;

            try
            {
                if (InputFieldChecker.InsideInputField) return;
            }
            catch { }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl) return;

            if (Input.GetKeyDown(KeyCode.C))
            {
                Plugin.Log.LogInfo("[UCP] Ctrl+C pressed");
                GroupCopyPaste.CopySelectedGroup();
                Input.ResetInputAxes();
            }
            else if (Input.GetKeyDown(KeyCode.V))
            {
                Plugin.Log.LogInfo("[UCP] Ctrl+V pressed");
                GroupCopyPaste.PasteGroupAtCursor();
                Input.ResetInputAxes();
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                Plugin.Log.LogInfo("[UCP] Ctrl+D pressed");
                GroupCopyPaste.DuplicateInPlace();
                Input.ResetInputAxes();
            }
        }
    }
}
