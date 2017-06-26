using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class PortalTrigger : MonoBehaviour {
        // Spawn clones
        // Disable wall colliders
        // Teleport player
        // Handle raycasts
        // 

        [SerializeField] public Portal portal;
        [SerializeField] public TriggerFunction function;

        public enum TriggerFunction {
            DisableColliders,
            SpawnClone,
            Teleport,
        }
    }
}
