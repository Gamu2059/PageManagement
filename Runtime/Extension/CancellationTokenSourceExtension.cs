using System.Linq;
using System.Threading;

namespace com.Gamu2059.PageManagement.Extension {
    public static class CancellationTokenSourceExtension {
        /// <summary>
        /// CancelしてDisposeする
        /// </summary>
        public static void CancelAndDispose(this CancellationTokenSource cts) {
            if (cts != null && !cts.IsCancellationRequested) {
                cts.Cancel();
                cts.Dispose();
            }
        }

        /// <summary>
        /// 指定したCancellationTokenを使って再生成する
        /// </summary>
        public static CancellationTokenSource Rebuild(this CancellationTokenSource cts, params CancellationToken[] tokens) {
            cts.CancelAndDispose();

            if (tokens == null || !tokens.Any()) {
                cts = new CancellationTokenSource();
            } else {
                cts = CancellationTokenSource.CreateLinkedTokenSource(tokens);
            }

            return cts;
        }
    }
}