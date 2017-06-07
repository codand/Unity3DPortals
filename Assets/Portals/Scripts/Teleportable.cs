using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class Teleportable : MonoBehaviour {

        void OnPortalEnter(Portal portal) {
            Debug.Log("Entered portal " + portal.name);
        }
    }
}
