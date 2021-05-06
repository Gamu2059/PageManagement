using UnityEngine;

namespace com.Gamu2059.PageManagement.Editor.Attribute {
    public class PageTypeAttribute : PropertyAttribute {
        public PageType PageType { get; }

        public PageTypeAttribute(PageType pageType) {
            PageType = pageType;
        }
    }
}