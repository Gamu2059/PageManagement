#pragma warning disable 4014

using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace com.Gamu2059.PageManagement.Utility {
    public static class PageTransitionUtility {
        /// <summary>
        /// 表示/非表示シーケンスを再生する。
        /// </summary>
        public static async UniTask PlaySequenceAsync(ISequenceCreatable show, ISequenceCreatable hide, CancellationToken ct) {
            var sequence = DOTween.Sequence();

            if (hide != null) {
                sequence.Append(hide.CreateHideSequence());
            }

            if (show != null) {
                sequence.Join(show.CreateShowSequence());
            }

            await sequence.Play().ToUniTask(TweenCancelBehaviour.Kill, ct);
        }
    }
}