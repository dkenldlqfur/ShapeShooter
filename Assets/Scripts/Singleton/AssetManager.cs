using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ShapeShooter
{
    /// <summary>
    /// Addressables를 이용한 백그라운드 에셋 로딩 및 캐싱을 전담하는 중앙 관리자입니다.
    /// 중복 로드를 방지하고, 에셋 해제(Release)를 추적할 수 있도록 딕셔너리로 래핑합니다.
    /// </summary>
    public class AssetManager : Singleton<AssetManager>
    {
        private Dictionary<string, AsyncOperationHandle> handleCache = new();
        public bool IsInitComplete { get; private set; } = false;

        protected override void Init()
        {
            InitAsync().Forget();
        }

        private async UniTaskVoid InitAsync()
        {
            await Addressables.InitializeAsync().Task;
            IsInitComplete = true;
        }

        /// <summary>
        /// 단일 어드레서블 에셋을 비동기로 로드하며 캐싱합니다.
        /// </summary>
        public async UniTask<T> LoadAssetAsync<T>(string key) where T : Object
        {
            if (handleCache.TryGetValue(key, out var cachedHandle))
            {
                if (cachedHandle.IsValid())
                    return cachedHandle.Result as T;
            }

            var handle = Addressables.LoadAssetAsync<T>(key);
            handleCache[key] = handle;
            
            var result = await handle.Task;
            return result;
        }

        /// <summary>
        /// 특정 Label에 속한 복수의 어드레서블 에셋들을 비동기로 로드하며 캐싱합니다.
        /// </summary>
        public async UniTask<IList<T>> LoadAssetsAsync<T>(string label) where T : Object
        {
            if (handleCache.TryGetValue(label, out var cachedHandle))
            {
                if (cachedHandle.IsValid())
                    return cachedHandle.Result as IList<T>;
            }

            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            handleCache[label] = handle;

            var result = await handle.Task;
            return result;
        }

        /// <summary>
        /// 캐싱된 에셋의 메모리 점유를 해제합니다.
        /// </summary>
        public void ReleaseAsset(string key)
        {
            if (handleCache.TryGetValue(key, out var handle))
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                
                handleCache.Remove(key);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var handle in handleCache.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            handleCache.Clear();
        }
    }
}
