using UnityEngine;
using WorldTour.Game.Audio;
using WorldTour.Game.Prefs;

namespace WorldTour.Game.Ui
{
    /// <summary>
    /// The Main scene's entry point: applies the settings, puts the first screen on the stack and logs
    /// "WT_MAIN_READY screens=1 sound=on motion=auto reduced=false theme=tour-pink". Task 2.4 swaps the placeholder
    /// home for the tour map. Main opened straight in the Editor (no Boot) runs on default settings.
    /// </summary>
    public sealed class MainRoot : MonoBehaviour
    {
        public const string ReadyLog = "WT_MAIN_READY";

        [SerializeField] ScreenStack stack;
        [SerializeField] GameScreen home;

        void Start()
        {
            if (Boot.Store != null) GameSettings.Bind(Boot.Store);
            else GameSettings.Apply();
            Sfx.Prepare();
            ThemeSkin.Attach(gameObject, stack);   // decor sprites follow the theme (palette colours already do)
            gameObject.AddComponent<WorldTour.Game.Save.PlayTimer>();   // "time played" for the manager's office (progress.js timer)
            if (stack != null && home != null) stack.SetRoot(home);
            else Debug.LogWarning("[ui] MainRoot needs its screen stack and home screen assigned");
            // the web header's sound toggle and footer's no-save notice, plus Tab navigation, on every screen
            Canvas canvas = stack != null ? stack.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                Hud hud = Hud.Create(canvas.transform, Boot.Store);
                gameObject.AddComponent<FocusNavigator>().Init(stack, hud);
            }
            Debug.Log(ReadyLog + " screens=" + (stack != null ? stack.Count : 0)
                + " sound=" + (GameSettings.Sound ? "on" : "off")
                + " motion=" + MotionModes.ToSave(GameSettings.Motion)
                + " reduced=" + (GameSettings.ReducedMotion ? "true" : "false")
                + " theme=" + GameSettings.ThemeId);
        }

        void OnApplicationFocus(bool focused)
        {
            // The device's reduce-motion setting can change while the tab is in the background.
            if (focused) GameSettings.Apply();
        }
    }
}
