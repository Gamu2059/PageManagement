using System;
using UniRx;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    internal class PageManagerHelper {
        private static PageManagerHelper instance;

        public static PageManagerHelper Instance {
            get {
                if (instance == null) {
                    instance = new PageManagerHelper();
                }

                return instance;
            }
        }

        public bool CanSetUpOnDefault { get; private set; }

        private PageManagerHelper() {
            Application.quitting += Dispose;
            CanSetUpOnDefault = true;
        }

        private void Dispose() {
            instance = null;
        }

        /// <summary>
        /// 実行時に最初から存在していたシーンのセットを出来なくする。
        /// </summary>
        public void DisposeSetUpOnDefault() {
            CanSetUpOnDefault = false;
        }
    }
}