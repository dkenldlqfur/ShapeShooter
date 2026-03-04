using UnityEngine;

namespace ShapeShooter
{
    /// <summary>
    /// MonoBehaviour 기반 싱글톤의 공통 생명주기(생성, 중복 제거, 씬 전환 유지)를 일원화하는 제네릭 추상 클래스입니다.
    /// 파생 클래스는 <see cref="Init"/>을 오버라이드하여 초기화 로직을 배치할 수 있습니다.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T instance;
        private static bool applicationIsQuitting = false;

        public static bool HasInstance => null != instance;

        public static T Instance
        {
            get
            {
                if (applicationIsQuitting)
                    return null;

                if (null == instance)
                {
                    instance = FindAnyObjectByType<T>();
                    if (null == instance)
                    {
                        var go = new GameObject(typeof(T).Name);
                        instance = go.AddComponent<T>();
                        instance.Init();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// Unity Awake 시점에서 중복 인스턴스를 파괴하고, 최초 인스턴스만 씬 전환 시에도 유지시킵니다.
        /// </summary>
        protected virtual void Awake()
        {
            if (null != instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = (T)this;
            DontDestroyOnLoad(gameObject);
            Init();
        }

        /// <summary>
        /// 파생 클래스에서 오버라이드하여 리소스 로딩, 풀 초기화 등의 초기화 로직을 배치합니다.
        /// </summary>
        protected virtual void Init() { }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
