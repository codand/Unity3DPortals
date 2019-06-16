using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PortalExtensions {
    public static class ComponentExtensions {
        public static T AddComponentWithConstructor<T>(this GameObject go) where T : Component{
            return go.AddComponent<T>();
        }
    }
}