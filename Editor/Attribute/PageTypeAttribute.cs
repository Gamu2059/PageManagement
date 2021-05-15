using UnityEngine;

namespace com.Gamu2059.PageManagement.Editor.Attribute {
    internal class PageTypeAttribute : PropertyAttribute {
        public PageType PageType { get; }

        public PageTypeAttribute(PageType pageType) {
            PageType = pageType;
        }
    }
}