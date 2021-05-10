using System.Collections.Generic;
using System.Linq;
using System.Threading;
using com.Gamu2059.PageManagement.Extension;
using com.Gamu2059.PageManagement.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UniRx;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    /// <summary>
    /// ウィンドウレイヤーのページ。
    /// </summary>
    public abstract class WindowPage : MonoBehaviour, ICancellationTokenCreatable, ISequenceCreatable {
        #region Define

        private class PageRequest {
            public RequestType type;
            public ScreenPage screenPagePrefab;
            public IScreenPageParam screenPageParam;
        }

        #endregion

        [SerializeField]
        private Transform screenRoot;

        private CancellationToken sceneCt;
        private CancellationTokenSource windowCts;

        /// <summary>
        /// ActivateAsyncからDeactivateAsyncまでのスコープで利用できるDisposable。
        /// </summary>
        protected CompositeDisposable ScopedDisposable { get; private set; }

        /// <summary>
        /// このウィンドウが遷移処理などで稼働中かどうか。
        /// 稼働中であれば遷移リクエストはキューに積まれる。
        /// </summary>
        private bool isBusy;

        /// <summary>
        /// このウィンドウが遷移リクエストを受け付けるかどうか。
        /// </summary>
        private bool isReservableRequest;

        private Queue<PageRequest> requests;
        private Stack<ScreenPage> screens;

        /// <summary>
        /// 現在有効なスクリーン。
        /// </summary>
        protected ScreenPage CurrentScreenPage { get; private set; }

        /// <summary>
        /// ウィンドウに紐づいたctを取得する。
        /// ウィンドウのCleanUp系メソッドが呼ばれるとキャンセルされる。
        /// </summary>
        public CancellationToken GetCt() {
            return windowCts?.Token ?? this.GetCancellationTokenOnDestroy();
        }

        #region Transition Sequence Method

        /// <summary>
        /// シーンに紐づいたctをセットする。
        /// </summary>
        public void SetSceneCt(CancellationToken ct) {
            sceneCt = ct;
        }

        /// <summary>
        /// 進み遷移での初期化処理。
        /// </summary>
        public async UniTask SetUpForwardInAsync(
            ScreenPage screenPagePrefab,
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            isReservableRequest = true;
            isBusy = true;

            requests = new Queue<PageRequest>();
            screens = new Stack<ScreenPage>();

            windowCts = windowCts.Rebuild(sceneCt, this.GetCancellationTokenOnDestroy());
            await OnSetUpForwardInAsync(windowPageParam, ct);

            // 最初からスクリーンが存在している時は、それらを全て破棄する
            var defaultScreens = GetComponentsInChildren<ScreenPage>();
            defaultScreens.ForEach(s => Destroy(s.gameObject));

            CurrentScreenPage = Instantiate(screenPagePrefab, screenRoot);
            CurrentScreenPage.SetWindowCt(GetCt());
            await CurrentScreenPage.SetUpForwardInAsync(screenPageParam, ct);
        }

        /// <summary>
        /// 戻り遷移での初期化処理。
        /// </summary>
        public async UniTask SetUpBackInAsync(
            IWindowPageParam windowPageParam,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            isReservableRequest = true;
            isBusy = true;

            requests?.Clear();

            windowCts = windowCts.Rebuild(sceneCt, this.GetCancellationTokenOnDestroy());
            await OnSetUpBackInAsync(windowPageParam, ct);

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.SetUpBackInAsync(screenPageParam, ct);
            }
        }

        /// <summary>
        /// 実行時に最初から存在していた時の初期化処理。
        /// </summary>
        public async UniTask SetUpOnDefaultAsync(CancellationToken ct) {
            isReservableRequest = true;
            isBusy = true;

            requests = new Queue<PageRequest>();
            screens = new Stack<ScreenPage>();

            windowCts = windowCts.Rebuild(sceneCt, this.GetCancellationTokenOnDestroy());
            await OnSetUpForwardInAsync(null, ct);

            CurrentScreenPage = GetComponentInChildren<ScreenPage>();
            if (CurrentScreenPage != null) {
                CurrentScreenPage.SetWindowCt(GetCt());
                await CurrentScreenPage.SetUpForwardInAsync(null, ct);
            }
        }

        /// <summary>
        /// ウィンドウ自体は残る時の破棄処理。
        /// </summary>
        public async UniTask CleanUpForwardOutAsync(CancellationToken ct) {
            if (CurrentScreenPage != null) {
                await CurrentScreenPage.CleanUpForwardOutAsync(ct);
            }

            await OnCleanUpForwardOutAsync(ct);
            if (!windowCts.IsCancellationRequested) {
                windowCts.CancelAndDispose();
            }
        }

        /// <summary>
        /// ウィンドウ自体も破棄される時の破棄処理。
        /// </summary>
        public async UniTask CleanUpDestroyOutAsync(CancellationToken ct) {
            if (CurrentScreenPage != null) {
                await CurrentScreenPage.CleanUpDestroyOutAsync(ct);
            }

            await OnCleanUpDestroyOutAsync(ct);
            if (!windowCts.IsCancellationRequested) {
                windowCts.CancelAndDispose();
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// ウィンドウオブジェクトの有効化処理。
        /// </summary>
        public void ActivateObject() {
            gameObject.SetActive(true);
            OnActivateObject();

            if (CurrentScreenPage != null) {
                CurrentScreenPage.ActivateObject();
            }
        }

        /// <summary>
        /// ウィンドウオブジェクトの無効化処理。
        /// </summary>
        public void DeactivateObject() {
            if (CurrentScreenPage != null) {
                CurrentScreenPage.DeactivateObject();
            }

            gameObject.SetActive(false);
            OnDeactivateObject();
        }

        /// <summary>
        /// ウィンドウの有効化処理。
        /// </summary>
        public async UniTask ActivateAsync(CancellationToken ct) {
            await OnActivateAsync(ct);

            await DequeueRequestWhiteEmptyAsync(ct);

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.ActivateAsync(ct);
            }

            isBusy = false;
        }

        /// <summary>
        /// ウィンドウの無効化処理。
        /// </summary>
        public async UniTask DeactivateAsync(CancellationToken ct) {
            isReservableRequest = false;

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.DeactivateAsync(ct);
            }

            await OnDeactivateAsync(ct);
        }

        #endregion

        #region Transition Sequence Virtual Method

        /// <summary>
        /// 進み遷移での初期化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnSetUpForwardInAsync(IWindowPageParam windowPageParam, CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 戻り遷移での初期化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnSetUpBackInAsync(IWindowPageParam windowPageParam, CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// ウィンドウ自体は残る時の破棄処理。拡張用。
        /// </summary>
        protected virtual UniTask OnCleanUpForwardOutAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// ウィンドウ自体も破棄される時の破棄処理。拡張用。
        /// </summary>
        protected virtual UniTask OnCleanUpDestroyOutAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// ウィンドウオブジェクトの有効化処理。拡張用。
        /// </summary>
        protected virtual void OnActivateObject() {
        }

        /// <summary>
        /// ウィンドウオブジェクトの無効化処理。拡張用。
        /// </summary>
        protected virtual void OnDeactivateObject() {
        }

        /// <summary>
        /// ウィンドウの有効化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnActivateAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// ウィンドウの無効化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnDeactivateAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 表示シーケンスを生成する。
        /// </summary>
        public virtual Sequence CreateShowSequence() {
            if (CurrentScreenPage != null) {
                return CurrentScreenPage.CreateShowSequence();
            }

            return null;
        }

        /// <summary>
        /// 非表示シーケンスを生成する。
        /// </summary>
        public virtual Sequence CreateHideSequence() {
            if (CurrentScreenPage != null) {
                return CurrentScreenPage.CreateHideSequence();
            }

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
        private void EnqueueRequest(RequestType type, ScreenPage screenPagePrefab, IScreenPageParam screenPageParam) {
            if (isReservableRequest) {
                var request = new PageRequest {
                    type = type,
                    screenPagePrefab = screenPagePrefab,
                    screenPageParam = screenPageParam,
                };

                requests.Enqueue(request);
            }
        }

        /// <summary>
        /// 遷移リクエストをキューから取り出して処理する。
        /// </summary>
        private async UniTask DequeueRequestAsync(CancellationToken ct) {
            if (!ExistRequest()) {
                return;
            }

            var request = requests.Dequeue();
            switch (request.type) {
                case RequestType.Forward:
                    await ProcessForwardScreenAsync(request.screenPagePrefab, request.screenPageParam, ct);
                    return;
                case RequestType.Switch:
                    await ProcessSwitchScreenAsync(request.screenPagePrefab, request.screenPageParam, ct);
                    return;
                case RequestType.Back:
                    await ProcessBackScreenAsync(request.screenPageParam, ct);
                    return;
            }
        }

        /// <summary>
        /// 遷移リクエストがキューから無くなるまで処理する。
        /// </summary>
        private async UniTask DequeueRequestWhiteEmptyAsync(CancellationToken ct) {
            while (ExistRequest()) {
                await DequeueRequestAsync(ct);
            }
        }

        /// <summary>
        /// スクリーンを進める。
        /// </summary>
        public async UniTask ForwardScreenAsync(
            ScreenPage screenPagePrefab,
            IScreenPageParam screenPageParam) {
            if (screenPagePrefab == null) {
                Debug.LogError("[WindowPage.ForwardScreenAsync] ScreenPagePrefabがnullです。");
                return;
            }

            if (!isReservableRequest) {
                Debug.LogWarning("[WindowPage.ForwardScreenAsync] 今は遷移リクエストを受け付けられません。");
                return;
            }

            if (isBusy) {
                EnqueueRequest(RequestType.Forward, screenPagePrefab, screenPageParam);
                return;
            }

            isBusy = true;

            var cts = this.CreateCts();
            var ct = cts.Token;
            await ProcessForwardScreenAsync(screenPagePrefab, screenPageParam, ct);
            await DequeueRequestWhiteEmptyAsync(ct);

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.ActivateAsync(ct);
            }

            cts.CancelAndDispose();

            isBusy = false;

            Debug.Log($"{GetType().Name} Done Forward Request. Stack count : {screens.Count}");
        }

        /// <summary>
        /// スクリーンを進める遷移処理。
        /// </summary>
        private async UniTask ProcessForwardScreenAsync(
            ScreenPage screenPagePrefab,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            if (screenPagePrefab == null) {
                Debug.LogError("[WindowPage.ForwardScreenAsync] ScreenPagePrefabがnullです。");
                return;
            }

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.DeactivateAsync(ct);
            }

            var nextScreen = Instantiate(screenPagePrefab, screenRoot);
            nextScreen.SetWindowCt(GetCt());
            await nextScreen.SetUpForwardInAsync(screenPageParam, ct);

            nextScreen.ActivateObject();

            await PageTransitionUtility.PlaySequenceAsync(nextScreen, CurrentScreenPage, ct);

            if (CurrentScreenPage != null) {
                CurrentScreenPage.DeactivateObject();
                await CurrentScreenPage.CleanUpForwardOutAsync(ct);
                screens.Push(CurrentScreenPage);
            }

            CurrentScreenPage = nextScreen;
        }

        /// <summary>
        /// スクリーンを切り替える。
        /// </summary>
        public async UniTask SwitchScreenAsync(
            ScreenPage screenPagePrefab,
            IScreenPageParam screenPageParam) {
            if (screenPagePrefab == null) {
                Debug.LogError("[WindowPage.SwitchScreenAsync] ScreenPagePrefabがnullです。");
                return;
            }

            if (!isReservableRequest) {
                Debug.LogWarning("[WindowPage.SwitchScreenAsync] 今は遷移リクエストを受け付けられません。");
                return;
            }

            if (isBusy) {
                EnqueueRequest(RequestType.Switch, screenPagePrefab, screenPageParam);
                return;
            }

            isBusy = true;

            var cts = this.CreateCts();
            var ct = cts.Token;
            await ProcessSwitchScreenAsync(screenPagePrefab, screenPageParam, ct);
            await DequeueRequestWhiteEmptyAsync(ct);

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.ActivateAsync(ct);
            }

            cts.CancelAndDispose();

            isBusy = false;
        }

        /// <summary>
        /// スクリーンを切り替える遷移処理。
        /// </summary>
        private async UniTask ProcessSwitchScreenAsync(
            ScreenPage screenPagePrefab,
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            if (screenPagePrefab == null) {
                Debug.LogError("[WindowPage.SwitchScreenAsync] ScreenPagePrefabがnullです。");
                return;
            }

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.DeactivateAsync(ct);
            }

            var nextScreen = Instantiate(screenPagePrefab, screenRoot);
            nextScreen.SetWindowCt(GetCt());
            await nextScreen.SetUpForwardInAsync(screenPageParam, ct);

            nextScreen.ActivateObject();

            await PageTransitionUtility.PlaySequenceAsync(nextScreen, CurrentScreenPage, ct);

            if (CurrentScreenPage != null) {
                CurrentScreenPage.DeactivateObject();
                await CurrentScreenPage.CleanUpDestroyOutAsync(ct);
            }

            foreach (var screen in screens) {
                await screen.CleanUpDestroyOutAsync(ct);
            }

            screens.Clear();
            CurrentScreenPage = nextScreen;
        }

        /// <summary>
        /// スクリーンを戻す。
        /// </summary>
        public async UniTask BackScreenAsync(IScreenPageParam screenPageParam) {
            if (!isReservableRequest) {
                Debug.LogWarning("[WindowPage.BackScreenAsync] 今は遷移リクエストを受け付けられません。");
                return;
            }

            // スタックにスクリーンがないなら戻れないので終了
            if (!screens.Any()) {
                Debug.LogWarning("[WindowPage.BackWindowAsync] スタックが空です。");
                return;
            }

            if (isBusy) {
                EnqueueRequest(RequestType.Back, null, screenPageParam);
                return;
            }

            isBusy = true;

            var cts = this.CreateCts();
            var ct = cts.Token;
            await ProcessBackScreenAsync(screenPageParam, ct);
            await DequeueRequestWhiteEmptyAsync(ct);

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.ActivateAsync(ct);
            }

            cts.CancelAndDispose();

            isBusy = false;
        }

        /// <summary>
        /// スクリーンを戻す遷移処理。
        /// </summary>
        private async UniTask ProcessBackScreenAsync(
            IScreenPageParam screenPageParam,
            CancellationToken ct) {
            // スタックにスクリーンがないなら戻れないので終了
            if (!screens.Any()) {
                Debug.LogWarning("[WindowPage.BackWindowAsync] スタックが空です。");
                return;
            }

            if (CurrentScreenPage != null) {
                await CurrentScreenPage.DeactivateAsync(ct);
            }

            var previousScreen = screens.Pop();
            await previousScreen.SetUpBackInAsync(screenPageParam, ct);

            previousScreen.ActivateObject();

            await PageTransitionUtility.PlaySequenceAsync(previousScreen, CurrentScreenPage, ct);

            if (CurrentScreenPage != null) {
                CurrentScreenPage.DeactivateObject();
                await CurrentScreenPage.CleanUpDestroyOutAsync(ct);
            }

            CurrentScreenPage = previousScreen;
        }

        #endregion
    }
}