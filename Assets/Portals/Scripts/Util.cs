using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public static class Util {
        public static void SafeDestroy(Object obj) {
            if (Application.isPlaying) {
                GameObject.Destroy(obj);
            } else {
                GameObject.DestroyImmediate(obj);
            }
        }
    }
}
