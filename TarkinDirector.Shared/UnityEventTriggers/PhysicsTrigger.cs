using System;
using UnityEngine;
using UnityEngine.Events;

namespace tarkin.Director
{
    public class PhysicsTrigger : MonoBehaviour
#if EFT_RUNTIME
        , IPhysicsTrigger
#endif
    {
        public string Description => nameof(PhysicsTrigger);

        [SerializeField] private UnityEvent unityEventEnter;
        [SerializeField] private UnityEvent unityEventExit;


        void OnValidate()
        {
            gameObject.layer = 13; // Triggers
            GetComponent<Collider>().isTrigger = true;
        }

        public void OnTriggerEnter(Collider other)
        {
            unityEventEnter.Invoke();
        }

        public void OnTriggerExit(Collider other)
        {
            unityEventExit.Invoke();
        }
    }
}
