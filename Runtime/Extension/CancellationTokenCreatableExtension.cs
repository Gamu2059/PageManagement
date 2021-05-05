using System;
using System.Linq;
using System.Threading;

namespace com.Gamu2059.PageManagement.Extension {
    public static class CancellationTokenCreatableExtension {
        public static CancellationTokenSource CreateCts(this ICancellationTokenCreatable creatable,
            params CancellationToken[] tokens) {
            var nullCreatable = creatable == null;
            var emptyTokens = tokens == null || !tokens.Any();

            if (nullCreatable && emptyTokens) {
                return new CancellationTokenSource();
            }

            if (nullCreatable) {
                return CancellationTokenSource.CreateLinkedTokenSource(tokens);
            }

            if (emptyTokens) {
                return CancellationTokenSource.CreateLinkedTokenSource(creatable.GetCt());
            }

            var newTokens = new CancellationToken[tokens.Length + 1];
            Array.Copy(tokens, newTokens, tokens.Length);
            newTokens[tokens.Length] = creatable.GetCt();
            return CancellationTokenSource.CreateLinkedTokenSource(newTokens);
        }
    }
}