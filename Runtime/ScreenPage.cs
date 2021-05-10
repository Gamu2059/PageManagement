using System.Threading;
using com.Gamu2059.PageManagement.Extension;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UniRx;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    /// <summary>
    /// スクリーンレイヤーのページ
    /// </summary>
    public abstract class ScreenPage : MonoBehaviour, ICancellationTokenCreatable, ISequenceCreatable {
        private CancellationToken windowCt;
        private CancellationTokenSource screenCts;

        /// <summary>
        /// ActivateAsyncからDeactivateAsyncまでのスコープで利用できるDisposable。
        /// </summary>
        protected CompositeDisposable ScopedDisposable { get; private set; }

        /// <summary>
        /// スクリーンに紐づいたctを取得する。
        /// スクリーンのCleanUp系メソッドが呼ばれるとキャンセルされる。
        /// </summary>
        public CancellationToken GetCt() {
            return screenCts?.Token ?? this.GetCancellationTokenOnDestroy();
        }

        #region Transition Sequence Method

        /// <summary>
        /// ウィンドウに紐づいたctをセットする。
        /// </summary>
        public void SetWindowCt(CancellationToken ct) {
            windowCt = ct;
        }

        /// <summary>
        /// 進み遷移での初期化処理。
        /// </summary>
        public async UniTask SetUpForwardInAsync(IScreenPageParam screenPageParam, CancellationToken ct) {
            screenCts = screenCts.Rebuild(windowCt, this.GetCancellationTokenOnDestroy());
            ScopedDisposable = null;
            await OnSetUpForwardInAsync(screenPageParam, ct);
        }

        /// <summary>
        /// 戻り遷移での初期化処理。
        /// </summary>
        public async UniTask SetUpBackInAsync(IScreenPageParam screenPageParam, CancellationToken ct) {
            screenCts = screenCts.Rebuild(windowCt, this.GetCancellationTokenOnDestroy());
            ScopedDisposable = null;
            await OnSetUpBackInAsync(screenPageParam, ct);
        }

        /// <summary>
        /// スクリーン自体は残る時の破棄処理。
        /// </summary>
        public async UniTask CleanUpForwardOutAsync(CancellationToken ct) {
            await OnCleanUpForwardOutAsync(ct);
            screenCts.CancelAndDispose();
        }

        /// <summary>
        /// スクリーン自体も破棄される時の破棄処理。
        /// </summary>
        public async UniTask CleanUpDestroyOutAsync(CancellationToken ct) {
            await OnCleanUpDestroyOutAsync(ct);
            screenCts.CancelAndDispose();
            Destroy(gameObject);
        }

        /// <summary>
        /// スクリーンオブジェクトの有効化処理。
        /// </summary>
        public void ActivateObject() {
            gameObject.SetActive(true);
            OnActivateObject();
        }

        /// <summary>
        /// スクリーンオブジェクトの無効化処理。
        /// </summary>
        public void DeactivateObject() {
            gameObject.SetActive(false);
            OnDeactivateObject();
        }

        /// <summary>
        /// スクリーンの有効化処理。
        /// </summary>
        public async UniTask ActivateAsync(CancellationToken ct) {
            ScopedDisposable = new CompositeDisposable();
            await OnActivateAsync(ct);
        }

        /// <summary>
        /// スクリーンの無効化処理。
        /// </summary>
        public async UniTask DeactivateAsync(CancellationToken ct) {
            await OnDeactivateAsync(ct);
            ScopedDisposable?.Dispose();
            ScopedDisposable = null;
        }

        #endregion

        #region Transition Sequence Virtual Method

        /// <summary>
        /// 進み遷移での初期化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnSetUpForwardInAsync(IScreenPageParam screenPageParam, CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 戻り遷移での初期化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnSetUpBackInAsync(IScreenPageParam screenPageParam, CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// スクリーン自体は残る時の破棄処理。拡張用。
        /// </summary>
        protected virtual UniTask OnCleanUpForwardOutAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// スクリーン自体も破棄される時の破棄処理。拡張用。
        /// </summary>
        protected virtual UniTask OnCleanUpDestroyOutAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// スクリーンオブジェクトの有効化処理。拡張用。
        /// </summary>
        protected virtual void OnActivateObject() {
        }

        /// <summary>
        /// スクリーンオブジェクトの無効化処理。拡張用。
        /// </summary>
        protected virtual void OnDeactivateObject() {
        }

        /// <summary>
        /// スクリーンの有効化処理。拡張用。
        /// </summary>
        protected virtual UniTask OnActivateAsync(CancellationToken ct) {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// スクリーンの無効化処理。拡張用。
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
    }
}