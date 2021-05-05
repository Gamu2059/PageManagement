using System;
using System.Linq;
using System.Threading;
using com.Gamu2059.PageManagement.Extension;
using com.Gamu2059.PageManagement.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.Gamu2059.PageManagement {
    public class PageManager<TScene, TWindow, TScreen> : ICancellationTokenCreatable
        where TScene : Enum where TWindow : Enum where TScreen : Enum {
        #region Define

        private class SceneParamSet {
            public TScene sceneType;
            public TWindow windowType;
            public TScreen screenType;
            public IScenePageParam scenePageParam;
            public IWindowPageParam windowPageParam;
            public IScreenPageParam screenPageParam;
        }

        #endregion

        private static PageManager<TScene, TWindow, TScreen> instance;

        public static PageManager<TScene, TWindow, TScreen> Instance {
            get {
                if (instance == null) {
                    SetUp();
                }

                return instance;
            }
        }

        private PageBinder<TScene, TWindow, TScreen> pageBinder;

        private CancellationTokenSource pageManagerCts;

        private bool isBusy;
        private CancellationTokenSource switchSceneCts;

        private ScenePage currentScenePage;

        #region SetUp & Dispose PageManager

        /// <summary>
        /// 初期化を行う
        /// </summary>
        private static void SetUp() {
            if (instance != null) {
                Debug.LogError("PageManager is already exist.");
                return;
            }

            instance = new PageManager<TScene, TWindow, TScreen>();
        }

        protected PageManager() {
            Application.quitting += Dispose;

            SetUpPageFinder();

            pageManagerCts = new CancellationTokenSource();

            isBusy = false;
            currentScenePage = null;
        }

        private void Dispose() {
            pageManagerCts?.Cancel();
            pageManagerCts?.Dispose();
            pageManagerCts = null;

            pageBinder = null;
        }

        private void SetUpPageFinder() {
            var binder = FindPageBinder();
            if (binder == null) {
#if UNITY_EDITOR
                EditorApplication.isPlaying = false;
#else
                Application.Quit(1);
#endif
                return;
            }

            pageBinder = binder;
        }

        private PageBinder<TScene, TWindow, TScreen> FindPageBinder() {
            var binders = Resources.LoadAll<PageBinder<TScene, TWindow, TScreen>>("");
            if (binders == null || !binders.Any()) {
                var scene = typeof(TScene).Name;
                var window = typeof(TWindow).Name;
                var screen = typeof(TScreen).Name;
                Debug.LogError(
                    "PageBinderがResourcesフォルダー内にありません。\n" +
                    $"PageBinder<{scene}, {window}, {screen}>を継承したScriptableObjectをResourcesフォルダー直下に配置して下さい。");
                return null;
            }

            return binders.FirstOrDefault(binder => binder != null);
        }

        #endregion

        /// <summary>
        /// ページマネージャに紐づいたctを取得する。
        /// ゲームが終了するとキャンセルされる。
        /// </summary>
        public CancellationToken GetCt() {
            return pageManagerCts.Token;
        }

        #region Transition Operating Method

        /// <summary>
        /// シーンを切り替える。
        /// </summary>
        /// <param name="sceneType">遷移先のシーン</param>
        /// <param name="windowType">遷移先のウィンドウ</param>
        /// <param name="screenType">遷移先のスクリーン</param>
        /// <param name="scenePageParam">遷移先のシーンに渡すパラメータ</param>
        /// <param name="windowPageParam">遷移先のウィンドウに渡すパラメータ</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask SwitchSceneAsync(
            TScene sceneType,
            TWindow windowType,
            TScreen screenType,
            IScenePageParam scenePageParam,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam) {
            // もしシーン遷移中にシーン遷移リクエストが割り込んできたら、今の遷移処理をキャンセルして最新のリクエストを処理する
            if (isBusy) {
                switchSceneCts.CancelAndDispose();
            }

            isBusy = true;

            switchSceneCts = this.CreateCts();
            await ProcessSwitchSceneAsync(sceneType, windowType, screenType,
                scenePageParam, windowPageParam, screenPageParam, switchSceneCts.Token);

            switchSceneCts.CancelAndDispose();

            isBusy = false;
        }

        /// <summary>
        /// シーンを切り替える遷移処理。
        /// </summary>
        private async UniTask ProcessSwitchSceneAsync(
            TScene sceneType,
            TWindow windowType,
            TScreen screenType,
            IScenePageParam scenePageParam,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            if (pageBinder == null) {
                Debug.LogError("PageBinderがありません。");
                return;
            }

            var scene = pageBinder.GetScene(sceneType);
            var windowPagePrefab = pageBinder.GetWindow(windowType);
            var screenPagePrefab = pageBinder.GetScreen(screenType);
            if (string.IsNullOrEmpty(scene) || windowPagePrefab == null || screenPagePrefab == null) {
                Debug.LogError(
                    "シーン、ウィンドウ、スクリーンのいずれかが見つかりませんでした。\n" +
                    "PageBinderに対応するシーンやプレハブがアタッチされているか確認して下さい。" +
                    $"scene {scene} window {windowPagePrefab} screen {screenPagePrefab}");
                return;
            }

            var loadSceneAsync = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            loadSceneAsync.allowSceneActivation = false;
            await loadSceneAsync.ToUniTask(null, PlayerLoopTiming.Update, ct);

            var loadScene = SceneManager.GetSceneByName(scene);
            var nextScene = FindScenePage(loadScene);
            if (nextScene == null) {
                await SceneManager.UnloadSceneAsync(loadScene);
                return;
            }

            if (currentScenePage != null) {
                await currentScenePage.DeactivateAsync(ct);
            }

            // シーン遷移用のctではなく、親階層のctを渡す
            // シーン遷移用のctのスコープは遷移処理が完了するまで、親階層のctのスコープは親が破棄されるまで
            nextScene.SetPageManagerCt(GetCt());
            await nextScene.SetUpAsync(windowPagePrefab, screenPagePrefab,
                scenePageParam, windowPageParam, screenPageParam, ct);

            loadSceneAsync.allowSceneActivation = true;
            SceneManager.SetActiveScene(loadScene);
            nextScene.ActivateObject();

            await PageTransitionUtility.PlaySequenceAsync(nextScene, currentScenePage, ct);

            if (currentScenePage != null) {
                currentScenePage.DeactivateObject();
                await currentScenePage.CleanUpAsync(ct);
            }

            currentScenePage = nextScene;
        }

        /// <summary>
        /// シーンの中からScenePageを継承するコンポーネントを取得する。
        /// </summary>
        private ScenePage FindScenePage(Scene scene) {
            var roots = scene.GetRootGameObjects();
            if (roots == null || !roots.Any()) {
                return null;
            }

            return roots
                .Select(r => r.GetComponentInChildren<ScenePage>())
                .FirstOrDefault(page => page != null);
        }

        /// <summary>
        /// ウィンドウを進める。
        /// </summary>
        /// <param name="windowType">遷移先のウィンドウ</param>
        /// <param name="screenType">遷移先のスクリーン</param>
        /// <param name="windowPageParam">遷移先のウィンドウに渡すパラメータ</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask ForwardWindowAsync(
            TWindow windowType,
            TScreen screenType,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam) {
            if (currentScenePage == null) {
                return;
            }

            if (pageBinder == null) {
                Debug.LogError("PageBinderがありません。");
                return;
            }

            var windowPagePrefab = pageBinder.GetWindow(windowType);
            var screenPagePrefab = pageBinder.GetScreen(screenType);
            if (windowPagePrefab == null || screenPagePrefab == null) {
                Debug.LogError(
                    "ウィンドウ、スクリーンのいずれかが見つかりませんでした。\n" +
                    "PageBinderに対応するシーンやプレハブがアタッチされているか確認して下さい。" +
                    $"window {windowPagePrefab} screen {screenPagePrefab}");
                return;
            }

            await currentScenePage.ForwardWindowAsync(
                windowPagePrefab, screenPagePrefab, windowPageParam, screenPageParam);
        }

        /// <summary>
        /// ウィンドウを切り替える。
        /// </summary>
        /// <param name="windowType">遷移先のウィンドウ</param>
        /// <param name="screenType">遷移先のスクリーン</param>
        /// <param name="windowPageParam">遷移先のウィンドウに渡すパラメータ</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask SwitchWindowAsync(
            TWindow windowType,
            TScreen screenType,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam) {
            if (currentScenePage == null) {
                return;
            }

            if (pageBinder == null) {
                Debug.LogError("PageBinderがありません。");
                return;
            }

            var windowPagePrefab = pageBinder.GetWindow(windowType);
            var screenPagePrefab = pageBinder.GetScreen(screenType);
            if (windowPagePrefab == null || screenPagePrefab == null) {
                Debug.LogError(
                    "ウィンドウ、スクリーンのいずれかが見つかりませんでした。\n" +
                    "PageBinderに対応するシーンやプレハブがアタッチされているか確認して下さい。" +
                    $"window {windowPagePrefab} screen {screenPagePrefab}");
                return;
            }

            await currentScenePage.SwitchWindowAsync(
                windowPagePrefab, screenPagePrefab, windowPageParam, screenPageParam);
        }

        /// <summary>
        /// ウィンドウを戻す。
        /// </summary>
        /// <param name="windowPageParam">遷移先のウィンドウに渡すパラメータ</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask BackWindowAsync(IWindowPageParam windowPageParam, IScreenPageParam screenPageParam) {
            if (currentScenePage != null) {
                await currentScenePage.BackWindowAsync(windowPageParam, screenPageParam);
            }
        }

        /// <summary>
        /// スクリーンを切り替える。
        /// </summary>
        /// <param name="screenType">遷移先のスクリーン</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask ForwardScreenAsync(TScreen screenType, IScreenPageParam screenPageParam) {
            if (currentScenePage == null) {
                return;
            }

            if (pageBinder == null) {
                Debug.LogError("PageBinderがありません。");
                return;
            }

            var screenPagePrefab = pageBinder.GetScreen(screenType);
            if (screenPagePrefab == null) {
                Debug.LogError(
                    "スクリーンが見つかりませんでした。\n" +
                    "PageBinderに対応するシーンやプレハブがアタッチされているか確認して下さい。" +
                    $"screen {screenPagePrefab}");
                return;
            }

            await currentScenePage.ForwardScreenAsync(screenPagePrefab, screenPageParam);
        }

        /// <summary>
        /// スクリーンを切り替える。
        /// </summary>
        /// <param name="screenType">遷移先のスクリーン</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask SwitchScreenAsync(TScreen screenType, IScreenPageParam screenPageParam) {
            if (currentScenePage == null) {
                return;
            }

            if (pageBinder == null) {
                Debug.LogError("PageBinderがありません。");
                return;
            }

            var screenPagePrefab = pageBinder.GetScreen(screenType);
            if (screenPagePrefab == null) {
                Debug.LogError(
                    "スクリーンが見つかりませんでした。\n" +
                    "PageBinderに対応するシーンやプレハブがアタッチされているか確認して下さい。" +
                    $"screen {screenPagePrefab}");
                return;
            }

            await currentScenePage.SwitchScreenAsync(screenPagePrefab, screenPageParam);
        }

        /// <summary>
        /// スクリーンを戻す。
        /// </summary>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask BackScreenAsync(IScreenPageParam screenPageParam) {
            if (currentScenePage != null) {
                await currentScenePage.BackScreenAsync(screenPageParam);
            }
        }

        #endregion
    }
}