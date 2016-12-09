using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public static class Helpers {
        public static float VectorInternalAverage(Vector3 vec) {
            return (vec.x + vec.y + vec.z) / 3;
        }

        public static float VectorInternalProduct(Vector3 vec) {
            return vec.x * vec.y * vec.z;
        }
    }
}
