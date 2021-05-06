using System;

namespace com.Gamu2059.PageManagement.Attribute {
    [AttributeUsage(AttributeTargets.Enum)]
    public class PageTypeAttribute : System.Attribute {
        public PageType PageType { get; }

        public PageTypeAttribute(PageType pageType) {
            PageType = pageType;
        }
    }
}