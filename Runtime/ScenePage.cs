using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using com.Gamu2059.PageManagement.Extension;
using com.Gamu2059.PageManagement.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace com.Gamu2059.PageManagement {
    /// <summary>
    /// シーンレイヤーのページ。
    /// </summary>
    public abstract class ScenePage : MonoBehaviour, ICancellationTokenCreatable, ISequenceCreatable {
        #region Define

        private class PageRequest {
            public RequestType type;
            public WindowPage windowPagePrefab;
            public ScreenPage screenPagePrefab;
            public IWindowPageParam windowPageParam;
            public IScreenPageParam screenPageParam;
        }

        #endregion

        [SerializeField]
        private Transform windowRoot;

        private CancellationToken pageManagerCt;
        private CancellationTokenSource sceneCts;
        private bool isSetUppedOnDefault;

        /// <summary>
        /// ActivateAsyncからDeactivateAsyncまでのスコープで利用できるDisposable。
        /// </summary>
        protected CompositeDisposable ScopedDisposable { get; private set; }

        private bool isBusy;
        private bool isReservableRequest;

        private Queue<PageRequest> requests;
        private Stack<WindowPage> windows;

        /// <summary>
        /// 現在有効なウィンドウ。
        /// </summary>
        protected WindowPage CurrentWindowPage { get; private set; }

        /// <summary>
        /// シーンに紐づいたctを取得する。
        /// シーンのCleanUp系メソッドが呼ばれるとキャンセルされる。
        /// </summary>
        public CancellationToken GetCt() {
            return sceneCts?.Token ?? this.GetCancellationTokenOnDestroy();
        }

        /// <summary>
        /// シーンのAdditiveシーンを取得する。
        /// </summary>
        public abstract string[] GetAdditiveScenes();

        #region Transition Sequence Method

        /// <summary>
        /// このシーンに対する遷移リクエストを受け付けられるかどうかをセットする。
        /// </summary>
        public void SetReservableRequest(bool reservableRequest) {
            isReservableRequest = reservableRequest;
        }

        /// <summary>
        /// ページマネージャに紐づいたctをセットする。
        /// </summary>
        public void SetPageManagerCt(CancellationToken ct) {
            pageManagerCt = ct;
        }

        /// <summary>
        /// 初期化処理。
        /// </summary>
        public async UniTask SetUpAsync(
            WindowPage windowPagePrefab,
            ScreenPage screenPagePrefab,
            IScenePageParam scenePageParam,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            isReservableRequest = false;
            
            requests = new Queue<PageRequest>();
            windows = new Stack<WindowPage>();
            isBusy = false;

            sceneCts = sceneCts.Rebuild(pageManagerCt, this.GetCancellationTokenOnDestroy());
            await OnSetUpAsync(scenePageParam, ct);

            CurrentWindowPage = Instantiate(windowPagePrefab, windowRoot);
            CurrentWindowPage.SetSceneCt(GetCt());
            await CurrentWindowPage.SetUpForwardInAsync(screenPagePrefab, windowPageParam, screenPageParam, ct);
        }

        /// <summary>
        /// 実行時に最初から存在していた時の初期化処理。
        /// </summary>
        public async UniTask SetUpOnDefaultAsync(CancellationToken ct) {
            // 既にデフォルトとしてセットアップしたことがあるなら、飛ばす
            if (isSetUppedOnDefault) {
                return;
            }

            isSetUppedOnDefault = true;
            requests = new Queue<PageRequest>();
            windows = new Stack<WindowPage>();
            isBusy = false;

            sceneCts = sceneCts.Rebuild(pageManagerCt, this.GetCancellationTokenOnDestroy());
            await OnSetUpAsync(null, ct);

            CurrentWindowPage = GetComponentInChildren<WindowPage>();
            if (CurrentWindowPage != null) {
                await CurrentWindowPage.SetUpOnDefaultAsync(ct);
            }
        }

        /// <summary>
        /// 破棄処理。
        /// </summary>
        public async UniTask CleanUpAsync(CancellationToken ct) {
            if (CurrentWindowPage != null) {
                await CurrentWindowPage.CleanUpDestroyOutAsync(ct);
            }

            await OnCleanUpAsync(ct);
            sceneCts.CancelAndDispose();
            await SceneManager.UnloadSceneAsync(gameObject.scene).ToUniTask(cancellationToken: ct);
        }

        /// <summary>
        /// シーンオブジェクトの有効化処理。
        /// </summary>
        public void ActivateObject() {
            gameObject.SetActive(true);
            OnActivateObject();

            if (CurrentWindowPage != null) {
                CurrentWindowPage.ActivateObject();
            }
        }

        /// <summary>
        /// シーンオブジェクトの無効化処理。
        /// </summary>
        public void DeactivateObject() {
            if (CurrentWindowPage != null) {
                CurrentWindowPage.DeactivateObject();
            }

            gameObject.SetActive(false);
            OnDeactivateObject();
        }

        /// <summary>
        /// シーンの有効化処理。
        /// </summary>
        public async UniTask ActivateAsync(CancellationToken ct) {
            await OnActivateAsync(ct);

            if (CurrentWindowPage != null) {
                await CurrentWindowPage.ActivateAsync(ct);
            }
        }

        /// <summary>
        /// シーンの無効化処理。
        /// </summary>
        public async UniTask DeactivateAsync(CancellationToken ct) {
            if (CurrentWindowPage != null) {
                await CurrentWindowPage.DeactivateAsync(ct);
            }

            await OnDeactivateAsync(ct);
        }

        #endregion

        #region Transition Sequence Virtual Method

        /// <summary>
        /// 初期化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnSetUpAsync(IScenePageParam scenePageParam, CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 破棄処理。拡張用。
        /// </summary>
        protected virtual UniTask OnCleanUpAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// シーンオブジェクトの有効化処理。拡張用。
        /// </summary>
        protected virtual void OnActivateObject() {
        }

        /// <summary>
        /// シーンオブジェクトの無効化処理。拡張用。
        /// </summary>
        protected virtual void OnDeactivateObject() {
        }

        /// <summary>
        /// シーンの有効化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnActivateAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// シーンの無効化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnDeactivateAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 表示シーケンスを生成する。
        /// </summary>
        public virtual Sequence CreateShowSequence() {
            return null;
        }

        /// <summary>
        /// 非表示シーケンスを生成する。
        /// </summary>
        public virtual Sequence CreateHideSequence() {
            return null;
        }

        #endregion

        #region Transition Operating Method

        /// <summary>
        /// 遷移リクエストがあるかどうか。
        /// </summary>
        private bool ExistRequest() {
            return requests.Any();
        }

        /// <summary>
        /// 遷移リクエストをキューにつめる。
        /// </summary>
        private void EnqueueRequest(
            RequestType type,
            WindowPage windowPagePrefab,
            ScreenPage screenPagePrefab,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam) {
            if (isReservableRequest) {
                var request = new PageRequest {
                    type = type,
                    windowPagePrefab = windowPagePrefab,
                    screenPagePrefab = screenPagePrefab,
                    windowPageParam = windowPageParam,
                    screenPageParam = screenPageParam,
                };

                requests.Enqueue(request);
            }
        }

        /// <summary>
        /// 遷移リクエストをキューから取り出して処理する。
        /// </summary>
        private async UniTask DequeueRequestAsync(CancellationToken ct) {
            if (ExistRequest()) {
                var request = requests.Dequeue();
                switch (request.type) {
                    case RequestType.Forward:
                        await ProcessForwardWindowAsync(request.windowPagePrefab, request.screenPagePrefab,
                            request.windowPageParam, request.screenPageParam, ct);
                        return;
                    case RequestType.Switch:
                        await ProcessSwitchWindowAsync(request.windowPagePrefab, request.screenPagePrefab,
                            request.windowPageParam, request.screenPageParam, ct);
                        return;
                    case RequestType.Back:
                        await ProcessBackWindowAsync(request.windowPageParam, request.screenPageParam, ct);
                        return;
                }
            }
        }

        /// <summary>
        /// ウィンドウを進める。
        /// </summary>
        public async UniTask ForwardWindowAsync(
            WindowPage windowPagePrefab,
            ScreenPage screenPagePrefab,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam) {
            if (windowPagePrefab == null || screenPagePrefab == null) {
                return;
            }

            if (isBusy) {
                EnqueueRequest(RequestType.Forward,
                    windowPagePrefab, screenPagePrefab, windowPageParam, screenPageParam);
                return;
            }

            isBusy = true;

            var cts = this.CreateCts();
            await ProcessForwardWindowAsync(windowPagePrefab, screenPagePrefab,
                windowPageParam, screenPageParam, cts.Token);
            while (ExistRequest()) {
                await DequeueRequestAsync(cts.Token);
            }

            if (CurrentWindowPage != null) {
                await CurrentWindowPage.ActivateAsync(cts.Token);
            }

            cts.CancelAndDispose();
            isBusy = false;
        }

        /// <summary>
        /// ウィンドウを進める遷移処理。
        /// </summary>
        private async UniTask ProcessForwardWindowAsync(
            WindowPage windowPagePrefab,
            ScreenPage screenPagePrefab,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            if (windowPagePrefab == null || screenPagePrefab == null) {
                return;
            }

            if (CurrentWindowPage != null) {
                await CurrentWindowPage.DeactivateAsync(ct);
            }

            var nextWindow = Instantiate(windowPagePrefab, windowRoot);
            nextWindow.SetSceneCt(GetCt());
            await nextWindow.SetUpForwardInAsync(screenPagePrefab, windowPageParam, screenPageParam, ct);

            nextWindow.ActivateObject();

            await PageTransitionUtility.PlaySequenceAsync(nextWindow, CurrentWindowPage, ct);

            if (CurrentWindowPage != null) {
                CurrentWindowPage.DeactivateObject();
                await CurrentWindowPage.CleanUpForwardOutAsync(ct);
            }

            windows.Push(CurrentWindowPage);
            CurrentWindowPage = nextWindow;
            CurrentWindowPage.SetReservableRequest(true);
        }

        /// <summary>
        /// ウィンドウを切り替える。
        /// </summary>
        public async UniTask SwitchWindowAsync(
            WindowPage windowPagePrefab,
            ScreenPage screenPagePrefab,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam) {
            if (windowPagePrefab == null || screenPagePrefab == null) {
                return;
            }

            if (isBusy) {
                EnqueueRequest(RequestType.Switch,
                    windowPagePrefab, screenPagePrefab, windowPageParam, screenPageParam);
                return;
            }

            isBusy = true;

            var cts = this.CreateCts();
            await ProcessSwitchWindowAsync(windowPagePrefab, screenPagePrefab,
                windowPageParam, screenPageParam, cts.Token);
            while (ExistRequest()) {
                await DequeueRequestAsync(cts.Token);
            }

            if (CurrentWindowPage != null) {
                await CurrentWindowPage.ActivateAsync(cts.Token);
            }

            cts.CancelAndDispose();
            isBusy = false;
        }

        /// <summary>
        /// ウィンドウを切り替える遷移処理。
        /// </summary>
        private async UniTask ProcessSwitchWindowAsync(
            WindowPage windowPagePrefab,
            ScreenPage screenPagePrefab,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            if (windowPagePrefab == null || screenPagePrefab == null) {
                return;
            }

            if (CurrentWindowPage != null) {
                await CurrentWindowPage.DeactivateAsync(ct);
            }

            var nextWindow = Instantiate(windowPagePrefab, windowRoot);
            nextWindow.SetSceneCt(GetCt());
            await nextWindow.SetUpForwardInAsync(screenPagePrefab, windowPageParam, screenPageParam, ct);

            nextWindow.ActivateObject();

            await PageTransitionUtility.PlaySequenceAsync(nextWindow, CurrentWindowPage, ct);

            if (CurrentWindowPage != null) {
                CurrentWindowPage.DeactivateObject();
                await CurrentWindowPage.CleanUpDestroyOutAsync(ct);
            }

            foreach (var window in windows) {
                await window.CleanUpDestroyOutAsync(ct);
            }

            windows.Clear();
            CurrentWindowPage = nextWindow;
        }

        /// <summary>
        /// ウィンドウを戻す。
        /// </summary>
        public async UniTask BackWindowAsync(IWindowPageParam windowPageParam, IScreenPageParam screenPageParam) {
            if (isBusy) {
                EnqueueRequest(RequestType.Back, null, null, windowPageParam, screenPageParam);
                return;
            }

            isBusy = true;

            var cts = this.CreateCts();
            await ProcessBackWindowAsync(windowPageParam, screenPageParam, cts.Token);
            while (ExistRequest()) {
                await DequeueRequestAsync(cts.Token);
            }

            if (CurrentWindowPage != null) {
                await CurrentWindowPage.ActivateAsync(cts.Token);
            }

            cts.CancelAndDispose();
            isBusy = false;
        }

        /// <summary>
        /// ウィンドウを戻す遷移処理。
        /// </summary>
        private async UniTask ProcessBackWindowAsync(
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            // スタックにウィンドウがないなら戻れないので終了
            if (!windows.Any()) {
                return;
            }

            if (CurrentWindowPage != null) {
                await CurrentWindowPage.DeactivateAsync(ct);
            }

            var previousWindow = windows.Pop();
            await previousWindow.SetUpBackInAsync(windowPageParam, screenPageParam, ct);

            previousWindow.ActivateObject();

            await PageTransitionUtility.PlaySequenceAsync(previousWindow, CurrentWindowPage, ct);

            if (CurrentWindowPage != null) {
                CurrentWindowPage.DeactivateObject();
                await CurrentWindowPage.CleanUpDestroyOutAsync(ct);
            }

            CurrentWindowPage = previousWindow;
        }

        /// <summary>
        /// スクリーンを進める。
        /// </summary>
        public async UniTask ForwardScreenAsync(
            ScreenPage screenPagePrefab,
            IScreenPageParam screenPageParam) {
            if (CurrentWindowPage != null) {
                await CurrentWindowPage.ForwardScreenAsync(screenPagePrefab, screenPageParam);
            }
        }

        /// <summary>
        /// スクリーンを切り替える。
        /// </summary>
        public async UniTask SwitchScreenAsync(
            ScreenPage screenPagePrefab,
            IScreenPageParam screenPageParam) {
            if (CurrentWindowPage != null) {
                await CurrentWindowPage.SwitchScreenAsync(screenPagePrefab, screenPageParam);
            }
        }

        /// <summary>
        /// スクリーンを戻す。
        /// </summary>
        public async UniTask BackScreenAsync(IScreenPageParam screenPageParam) {
            if (CurrentWindowPage != null) {
                await CurrentWindowPage.BackScreenAsync(screenPageParam);
            }
        }

        #endregion

        private void Start() {
            isSetUppedOnDefault = false;
            if (PageManagerHelper.Instance.CanSetUpOnDefault) {
                SetUpOnDefaultAsync(GetCt()).Forget();
            }
        }
    }
}