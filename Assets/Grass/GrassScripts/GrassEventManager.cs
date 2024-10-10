using System;
using System.Collections.Generic;

namespace Grass.GrassScripts
{
    public static class GrassEventManager
    {
        public static event Action<GrassInteractor> OnInteractorAdded;
        public static event Action<GrassInteractor> OnInteractorRemoved;

        private static HashSet<GrassInteractor> activeInteractors = new HashSet<GrassInteractor>();

        public static void AddInteractor(GrassInteractor interactor)
        {
            if (activeInteractors.Add(interactor))
            {
                OnInteractorAdded?.Invoke(interactor);
            }
        }

        public static void RemoveInteractor(GrassInteractor interactor)
        {
            if (activeInteractors.Remove(interactor))
            {
                OnInteractorRemoved?.Invoke(interactor);
            }
        }

        public static IReadOnlyCollection<GrassInteractor> GetActiveInteractors()
        {
            return activeInteractors;
        }
    }
}