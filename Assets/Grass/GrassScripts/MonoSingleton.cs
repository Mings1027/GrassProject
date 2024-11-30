using UnityEngine;

namespace Grass.GrassScripts
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T instance;
        private static bool isQuitting;

        public static T Instance
        {
            get
            {
                if (isQuitting || !Application.isPlaying)
                {
                    return null;
                }

                if (instance == null)
                {
                    instance = FindAnyObjectByType<T>();

                    if (instance == null)
                    {
                        var go = new GameObject($"[Singleton] {typeof(T)}");
                        instance = go.AddComponent<T>();
                    }

                    instance.OnSingletonAwake();
                }

                return instance;
            }
        }
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnApplicationQuit()
        {
            isQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}