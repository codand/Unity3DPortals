using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    /// <summary>
    /// This class exists because Unity does not expose an analogous method for OnWillRenderObject.
    /// OnRenderObject is called on ALL objects by ALL cameras regardless to whether or not they were
    /// seen by that camera. So this class keeps track of whether or not this GameObject was seen,
    /// and reports that by calling PreRender and PostRender. Camera.current still contains the currently
    /// rendering camera
    /// </summary>
    public abstract class RenderedBehaviour : MonoBehaviour {
        private HashSet<Camera> _seenBy = new HashSet<Camera>();

        void OnRenderObject() {
            if (_seenBy.Contains(Camera.current)) {
                PostRender();
                _seenBy.Remove(Camera.current);
            }
        }

        void OnWillRenderObject() {
            if (!_seenBy.Contains(Camera.current)) {
                _seenBy.Add(Camera.current);
                PreRender();
            }
        }

        protected abstract void PreRender();
        protected abstract void PostRender();
    }
}
