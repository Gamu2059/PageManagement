using System;
using System.Collections.Generic;

namespace com.Gamu2059.PageManagement.Extension {
    public static class IEnumerableExtension {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action) {
            if (source == null) {
                return;
            }

            foreach (var s in source) {
                action?.Invoke(s);
            }
        }
    }
}