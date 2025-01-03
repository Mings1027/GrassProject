using Grass.GrassScripts.EventBusSystem;
using UnityEngine;

namespace UIControl
{
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private int atk;
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetDir;
        [SerializeField] private float speed;

        [ContextMenu("Attack")]
        private void Attack()
        {
            EventBus<TargetingEvent>.Raise(new TargetingEvent
            {
                atk = atk,
                target = target,
                // targetDir = targetDir,
                speed = speed
            });
        }

        [ContextMenu("Get Register List")]
        private void GetRegisterList()
        {
            var targetingEventList = EventBus<TargetingEvent>.GetRegisteredList();
            for (int i = 0; i < targetingEventList.Count; i++)
            {
                Debug.Log($"{targetingEventList[i]}");
            }

            Debug.Log("===================================");
            var interactorAddedEventList = EventBus<InteractorAddedEvent>.GetRegisteredList();
            for (int i = 0; i < interactorAddedEventList.Count; i++)
            {
                Debug.Log($"{interactorAddedEventList[i]}");
            }

            Debug.Log("===================================");
            var interactorRemovedEventList = EventBus<InteractorRemovedEvent>.GetRegisteredList();
            for (int i = 0; i < interactorRemovedEventList.Count; i++)
            {
                Debug.Log($"{interactorRemovedEventList[i]}");
            }

            Debug.Log("===================================");
            var grassColorEventList = EventBus<GrassColorEvent>.GetRegisteredList();
            for (int i = 0; i < grassColorEventList.Count; i++)
            {
                Debug.Log($"{grassColorEventList[i]}");
            }
        }

        [ContextMenu("Get Register Count")]
        private void GetRegisterCount()
        {
            Debug.Log($"TargetingEvent Count : {EventBus<TargetingEvent>.GetRegisteredCount()}");
            Debug.Log($"InteractorAddedEvent Count : {EventBus<InteractorAddedEvent>.GetRegisteredCount()}");
            Debug.Log($"InteractorRemovedEvent Count : {EventBus<InteractorRemovedEvent>.GetRegisteredCount()}");
            Debug.Log($"GrassColorEvent Count : {EventBus<GrassColorEvent>.GetRegisteredCount()}");
        }
    }
}