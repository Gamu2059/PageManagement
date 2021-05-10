using Cysharp.Threading.Tasks;

namespace com.Gamu2059.PageManagement {
    public interface ISwitchSceneEvent {
        UniTask WaitShowEvent();
        UniTask WaitHideEvent();
        void Cancel();
    }
}