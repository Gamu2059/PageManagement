using System.Threading;
using Cysharp.Threading.Tasks;

namespace com.Gamu2059.PageManagement {
    public interface ISwitchSceneEvent {
        UniTask WaitShowEvent(CancellationToken ct);
        UniTask WaitHideEvent(CancellationToken ct);
        void Cancel();
    }
}