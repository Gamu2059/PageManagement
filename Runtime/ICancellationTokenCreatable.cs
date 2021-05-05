using System.Threading;

namespace com.Gamu2059.PageManagement {
    public interface ICancellationTokenCreatable {
        CancellationToken GetCt();
    }
}