using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class PortalTrigger : CompositeTrigger {
        [SerializeField] public Portal portal;
        [SerializeField] public TriggerFunction function;

        public enum TriggerFunction {
            Teleport,
            SpawnClone,
        }
    }
}
