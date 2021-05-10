using System;
using UniRx;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    public class PageManagerHelper {
        private static PageManagerHelper instance;

        public static PageManagerHelper Instance {
            get {
                if (instance == null) {
                    instance = new PageManagerHelper();
                }

                return instance;
            }
        }

        private ReactiveProperty<ScenePage> setUpOnDefaultSceneObservable;

        /// <summary>
        /// 実行時に最初から存在していたシーンがセットされた時に通知する。
        /// </summary>
        public IObservable<ScenePage> SetUpOnDefaultSceneObservable => setUpOnDefaultSceneObservable;

        private PageManagerHelper() {
            Application.quitting += Dispose;
            setUpOnDefaultSceneObservable = new ReactiveProperty<ScenePage>();
        }

        private void Dispose() {
            setUpOnDefaultSceneObservable?.Dispose();
            setUpOnDefaultSceneObservable = null;

            instance = null;
        }

        /// <summary>
        /// 実行時に最初から存在していたシーンとしてセットする。
        /// </summary>
        public void SetUpOnDefault(ScenePage scenePage) {
            setUpOnDefaultSceneObservable.SetValueAndForceNotify(scenePage);
        }
    }
}