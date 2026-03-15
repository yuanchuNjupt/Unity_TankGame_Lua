using System;
using UnityEngine;

namespace CsharpScripts
{
    public class UnityObjectLifeCycleBridge : MonoBehaviour
    {


        public Action OnAwakeBridge;
        public Action OnStartBridge;
        public Action OnUpdateBridge;
        public Action OnEnableBridge;
        public Action OnDisableBridge;
        public Action OnDestroyBridge;
    
        public Action<Collision> OnCollisionEnterBridge;
        public Action<Collision> OnCollisionStayBridge;
        public Action<Collision> OnCollisionExitBridge;
        public Action<Collider> OnTriggerEnterBridge;
        public Action<Collider> OnTriggerStayBridge;
        public Action<Collider> OnTriggerExitBridge;


        void Awake()
        {
            OnAwakeBridge?.Invoke();            
        }

        private void OnEnable()
        {
            OnEnableBridge?.Invoke();
        }

        void Start()
        {
            OnStartBridge?.Invoke();
        }
        
        void Update()
        {
            OnUpdateBridge?.Invoke();
        }

        private void OnDisable()
        {
            OnDisableBridge?.Invoke();
        }

        private void OnDestroy()
        {
            OnDestroyBridge?.Invoke();
        }


        private void OnCollisionEnter(Collision other)
        {
            OnCollisionEnterBridge?.Invoke(other);
        }
        
        private void OnCollisionStay(Collision other)
        {
            OnCollisionStayBridge?.Invoke(other);
        }
        
        private void OnCollisionExit(Collision other)
        {
            OnCollisionExitBridge?.Invoke(other);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            OnTriggerEnterBridge?.Invoke(other);
        }
        
        private void OnTriggerStay(Collider other)
        {
            OnTriggerStayBridge?.Invoke(other);
        }
        

        private void OnTriggerExit(Collider other)
        {
            OnTriggerExitBridge?.Invoke(other);
        }
    }
}
