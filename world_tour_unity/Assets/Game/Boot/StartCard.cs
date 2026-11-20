using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using WorldTour.Core;
using WorldTour.Game.Audio;
using WorldTour.Game.Motion;
using WorldTour.Game.Prefs;
using WorldTour.Game.Ui;

namespace WorldTour.Game
{
    /// <summary>
    /// The "click to start" card in the Boot scene (PLAN.md, Scenes). Hidden until Boot has loaded the data and the save
    /// and calls <see cref="Show"/>. Its one button (Enter and Space work too; it gets focus) is the user gesture that lets
    /// the browser play sound (and, from phase 3, speech), then it loads Main once. Main is the only scene load in the game.
    /// Logs "WT_START_SHOWN" when it appears and "WT_STARTED" on the click, for the browser tests.
    /// </summary>
    public sealed class StartCard : MonoBehaviour
    {
        public const string ShownLog = "WT_START_SHOWN";
        public const string StartedLog = "WT_STARTED";
        public const string MainScene = "Main";

        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform card;
        [SerializeField] TMP_Text title;
        [SerializeField] StickerButton startButton;

        bool _shown, _started;

        /// <summary>
        /// Raised once, on the start click, inside the user gesture's frame. Speech (phase 3) hooks here to warm up
        /// speechSynthesis, which some browsers also gate behind a gesture.
        /// </summary>
        public static event Action Unlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Unlocked = null; }

        void Awake()
        {
            if (group != null) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
            if (startButton != null) startButton.OnClick.AddListener(Begin);
            // Make the sounds now, while the data loads, so the first click can already pop.
            Sfx.Prepare();
        }

        /// <summary>Shows the card. Boot calls this once its data and save are ready. Safe to call twice.</summary>
        public void Show()
        {
            if (_shown) return;
            _shown = true;
            if (title != null) title.SetLower(GameInfo.Title);
            if (group != null) { group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true; }
            if (card != null) Feedback.PeelOn(card, sound: false);
            if (startButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            Debug.Log(ShownLog);
        }

        void Begin()
        {
            if (_started) return;
            _started = true;
            if (startButton != null) startButton.interactable = false;
            Sfx.Unlock();
            GameSettings.Bind(Boot.Store);
            try { Unlocked?.Invoke(); }
            catch (Exception e) { Debug.LogException(e, this); }
            Debug.Log(StartedLog);
            StartCoroutine(LoadMain());
        }

        IEnumerator LoadMain()
        {
            AsyncOperation op = null;
            try { op = SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single); }
            catch (Exception e) { Debug.LogWarning("[boot] could not load " + MainScene + ": " + e.Message); }
            if (op == null)
            {
                Debug.LogWarning("[boot] the Main scene is not in the build list");
                _started = false;
                if (startButton != null) startButton.interactable = true;
                yield break;
            }
            while (!op.isDone) yield return null;
        }
    }
}
