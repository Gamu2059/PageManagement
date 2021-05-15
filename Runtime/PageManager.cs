using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using com.Gamu2059.PageManagement.Extension;
using com.Gamu2059.PageManagement.Utility;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace com.Gamu2059.PageManagement {
    public abstract class PageManager<T, TScene, TWindow, TScreen> : ICancellationTokenCreatable
        where T : PageManager<T, TScene, TWindow, TScreen>, new()
        where TScene : Enum
        where TWindow : Enum
        where TScreen : Enum {
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

        private static T instance;

        public static T Instance {
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

        protected ScenePage CurrentScenePage { get; private set; }

        private ISwitchSceneEvent switchSceneEvent;

        private IDisposable setUpOnDefaultDisposable;

        private Subject<Unit> beginSceneLoadObservable;
        public IObservable<Unit> BeginSceneLoadObservable => beginSceneLoadObservable;

        private Subject<Unit> completeSceneLoadObservable;
        public IObservable<Unit> CompleteSceneLoadObservable => completeSceneLoadObservable;

        private Subject<Unit> cancelSceneLoadObservable;
        public IObservable<Unit> CancelSceneLoadObservable => cancelSceneLoadObservable;

        #region SetUp & Dispose PageManager

        /// <summary>
        /// 初期化を行う
        /// </summary>
        private static void SetUp() {
            if (instance != null) {
                Debug.LogError("PageManager is already exist.");
                return;
            }

            instance = new T();
        }

        protected PageManager() {
            Application.quitting += Dispose;

            pageManagerCts = new CancellationTokenSource();

            isBusy = false;
            CurrentScenePage = null;

            beginSceneLoadObservable = new Subject<Unit>();
            completeSceneLoadObservable = new Subject<Unit>();
            cancelSceneLoadObservable = new Subject<Unit>();

            SetUpPageFinder();
            SetUpOnDefaultScene();
        }

        private void Dispose() {
            cancelSceneLoadObservable?.Dispose();
            completeSceneLoadObservable?.Dispose();
            beginSceneLoadObservable?.Dispose();

            pageManagerCts?.Cancel();
            pageManagerCts?.Dispose();
            pageManagerCts = null;

            pageBinder = null;

            instance = null;
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

        private void SetUpOnDefaultScene() {
            PageManagerHelper.Instance.DisposeSetUpOnDefault();
            var scene = SceneManager.GetActiveScene();
            var scenePage = FindScenePage(scene);
            if (scenePage != null) {
                CurrentScenePage = scenePage;
                scenePage.SetPageManagerCt(GetCt());
                scenePage.SetUpOnDefaultAsync(GetCt()).Forget();
            }
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
        /// <param name="switchScene">シーン切り替え演出処理</param>
        public async UniTask SwitchSceneAsync(
            TScene sceneType,
            TWindow windowType,
            TScreen screenType,
            IScenePageParam scenePageParam,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            ISwitchSceneEvent switchScene = null) {
            // もしシーン遷移中にシーン遷移リクエストが割り込んできたら、今の遷移処理をキャンセルして最新のリクエストを処理する
            if (isBusy) {
                switchSceneCts.CancelAndDispose();
                cancelSceneLoadObservable?.OnNext(Unit.Default);
                switchSceneEvent?.Cancel();
            }

            isBusy = true;

            switchSceneEvent = switchScene;
            switchSceneCts = this.CreateCts();
            await ProcessSwitchSceneAsync(sceneType, windowType, screenType,
                scenePageParam, windowPageParam, screenPageParam, switchSceneCts.Token);
            switchSceneEvent = null;

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

            if (CurrentScenePage != null) {
                await CurrentScenePage.DeactivateAsync(ct);
            }

            beginSceneLoadObservable?.OnNext(Unit.Default);

            if (switchSceneEvent == null) {
                await CleanUpAndLoadSceneAsync(scene, ct);
            } else {
                await UniTask.WhenAll(
                    CleanUpAndLoadSceneAsync(scene, ct),
                    switchSceneEvent.WaitShowEvent(ct));
            }

            // 同名のシーンの場合、読み込まれているシーンはインデックスが早いのでLastで取得する
            var loadScene = GetScenesByName(scene).Last();
            var nextScene = FindScenePage(loadScene);
            if (nextScene == null) {
                Debug.LogError("[PageManager.SwitchSceneAsync] 読み込んだシーンにScenePageが存在しません。");
                return;
            }

            // シーン遷移用のctではなく、親階層のctを渡す
            // シーン遷移用のctのスコープは遷移処理が完了するまで、親階層のctのスコープは親が破棄されるまで
            nextScene.SetPageManagerCt(GetCt());
            await nextScene.SetUpAsync(windowPagePrefab, screenPagePrefab,
                scenePageParam, windowPageParam, screenPageParam, ct);

            SceneManager.SetActiveScene(loadScene);
            nextScene.ActivateObject();

            completeSceneLoadObservable?.OnNext(Unit.Default);

            if (switchSceneEvent == null) {
                await PageTransitionUtility.PlaySequenceAsync(nextScene, null, ct);
            } else {
                await UniTask.WhenAll(
                    PageTransitionUtility.PlaySequenceAsync(nextScene, null, ct),
                    switchSceneEvent.WaitHideEvent(ct));
            }

            CurrentScenePage = nextScene;
            await CurrentScenePage.ActivateAsync(ct);
        }

        private async UniTask CleanUpAndLoadSceneAsync(string scene, CancellationToken ct) {
            await PageTransitionUtility.PlaySequenceAsync(null, CurrentScenePage, ct);

            if (CurrentScenePage != null) {
                CurrentScenePage.DeactivateObject();
                await CurrentScenePage.CleanUpAsync(ct);
            }

            var loadSceneAsync = SceneManager.LoadSceneAsync(scene);
            await loadSceneAsync.ToUniTask(null, PlayerLoopTiming.Update, ct);
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
            if (CurrentScenePage == null) {
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

            await CurrentScenePage.ForwardWindowAsync(
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
            if (CurrentScenePage == null) {
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

            await CurrentScenePage.SwitchWindowAsync(
                windowPagePrefab, screenPagePrefab, windowPageParam, screenPageParam);
        }

        /// <summary>
        /// ウィンドウを戻す。
        /// </summary>
        /// <param name="windowPageParam">遷移先のウィンドウに渡すパラメータ</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask BackWindowAsync(IWindowPageParam windowPageParam, IScreenPageParam screenPageParam) {
            if (CurrentScenePage != null) {
                await CurrentScenePage.BackWindowAsync(windowPageParam, screenPageParam);
            }
        }

        /// <summary>
        /// スクリーンを進める。
        /// </summary>
        /// <param name="screenType">遷移先のスクリーン</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask ForwardScreenAsync(TScreen screenType, IScreenPageParam screenPageParam) {
            if (CurrentScenePage == null) {
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

            await CurrentScenePage.ForwardScreenAsync(screenPagePrefab, screenPageParam);
        }

        /// <summary>
        /// スクリーンを切り替える。
        /// </summary>
        /// <param name="screenType">遷移先のスクリーン</param>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask SwitchScreenAsync(TScreen screenType, IScreenPageParam screenPageParam) {
            if (CurrentScenePage == null) {
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

            await CurrentScenePage.SwitchScreenAsync(screenPagePrefab, screenPageParam);
        }

        /// <summary>
        /// スクリーンを戻す。
        /// </summary>
        /// <param name="screenPageParam">遷移先のスクリーンに渡すパラメータ</param>
        public async UniTask BackScreenAsync(IScreenPageParam screenPageParam) {
            if (CurrentScenePage != null) {
                await CurrentScenePage.BackScreenAsync(screenPageParam);
            }
        }

        #endregion

        #region Scene Loading Help Method

        /// <summary>
        /// 現在読み込まれているシーンの中から、指定した名前に該当するものを全て取得する。
        /// </summary>
        private List<Scene> GetScenesByName(string name) {
            var scenes = new List<Scene>();
            var count = SceneManager.sceneCount;
            for (var i = 0; i < count; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name == name) {
                    scenes.Add(scene);
                }
            }

            return scenes;
        }

        /// <summary>
        /// Additiveシーンを読み込む。
        /// </summary>
        public AsyncOperation LoadAdditiveSceneAsync(string scene) {
            return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        }

        /// <summary>
        /// Additiveシーンを読み込む。
        /// </summary>
        public AsyncOperation LoadAdditiveSceneAsync(SceneObject scene) {
            return LoadAdditiveSceneAsync(scene.ToString());
        }

        /// <summary>
        /// Additiveシーンを読み込む。
        /// </summary>
        public AsyncOperation[] LoadAdditiveScenesAsync(string[] scenes) {
            return scenes.Select(LoadAdditiveSceneAsync).ToArray();
        }

        /// <summary>
        /// Additiveシーンを読み込む。
        /// </summary>
        public AsyncOperation[] LoadAdditiveScenesAsync(SceneObject[] scenes) {
            return scenes.Select(LoadAdditiveSceneAsync).ToArray();
        }

        /// <summary>
        /// シーンを破棄する。
        /// </summary>
        public AsyncOperation UnloadSceneAsync(Scene scene) {
            if (!scene.IsValid()) {
                return null;
            }

            return SceneManager.UnloadSceneAsync(scene);
        }

        /// <summary>
        /// シーンを破棄する。
        /// </summary>
        public AsyncOperation[] UnloadScenesAsync(Scene[] scenes) {
            if (scenes == null) {
                return null;
            }

            return scenes.Select(UnloadSceneAsync).ToArray();
        }

        /// <summary>
        /// 指定したシーン以外の全てのシーンを破棄する。
        /// </summary>
        /// <param name="needScenes">破棄しないでほしいシーン名</param>
        public async UniTask UnloadAllScenes(CancellationToken ct, params string[] needScenes) {
            var sceneIdxes = new List<int>();
            if (needScenes != null) {
                sceneIdxes.AddRange(needScenes
                    .Where(scene => !string.IsNullOrEmpty(scene))
                    .SelectMany(GetScenesByName)
                    .Where(scene => scene.IsValid())
                    .Select(scene => scene.buildIndex));
            }

            var unloadScenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!sceneIdxes.Contains(scene.buildIndex)) {
                    unloadScenes.Add(scene);
                }
            }

            await UniTask.WhenAll(
                unloadScenes
                    .Select(SceneManager.UnloadSceneAsync)
                    .Select(o => o.ToUniTask(cancellationToken: ct))
            );
        }

        #endregion
    }
}