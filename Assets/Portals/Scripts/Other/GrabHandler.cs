using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class GrabHandler : MonoBehaviour {
        #region Private Members
        // Anchor point that IS NOT affected by portals
        [SerializeField]
        private Transform _staticAnchor;

        // Anchor point that IS affected by portals
        [SerializeField]
        private Rigidbody _floatingAnchor;
        [SerializeField]
        private float _pickupRange = 2.0f;
        [SerializeField]
        private LayerMask _layer;

        private Camera _camera;
        private ConfigurableJoint _joint;
        private Portal _currentObjectPortal;
        private static GrabHandler _instance;
        #endregion

        #region Public Members
        public static GrabHandler instance {
            get {
                if (!_instance)
                    _instance = GameObject.FindObjectOfType<GrabHandler>();
                return _instance;
            }
        }

        public delegate void GrabEvent(GrabHandler grabHandler, GameObject obj);
        public event GrabEvent onObjectGrabbed;
        public event GrabEvent onObjectReleased;

        public GameObject heldObject;
        #endregion

        void Awake() {
            _camera = Camera.main;
            heldObject = null;
            _joint = _floatingAnchor.gameObject.GetComponent<ConfigurableJoint>();
            if (_joint == null) {
                Debug.LogError("Anchor object needs a ConfigurableJoint");
            }
        }

        void OnEnable() {
            //Portal.onPortalTeleportGlobal += HandleObjectCarriedThroughPortal;
        }

        void OnDisable() {
            //Portal.onPortalTeleportGlobal -= HandleObjectCarriedThroughPortal;
        }

        void HandleObjectCarriedThroughPortal(Portal portal, GameObject obj) {
            if (obj == this.gameObject) {
                // Player exited portal
                if (_currentObjectPortal || heldObject == null) {
                    // Current object already on other side, reset
                    _currentObjectPortal = null;
                } else {
                    // Current object on previous side, use new positioning
                    _currentObjectPortal = portal.ExitPortal;
                }
            } else if (obj == heldObject) {
                // Current object exited portal
                if (_currentObjectPortal) {
                    // Player already on this side, reset
                    _currentObjectPortal = null;
                } else {
                    // Player on opposite side
                    _currentObjectPortal = portal;
                }

                // Multiply joint values by portal scale
                float scaleMultiplier = portal.PortalScaleAverage;

                SoftJointLimit softJointLimit = _joint.linearLimit;
                softJointLimit.limit *= scaleMultiplier;
                _joint.linearLimit = softJointLimit;

                float cubeScaleMultiplier = scaleMultiplier * scaleMultiplier * scaleMultiplier;
                JointDrive linearDrive = _joint.xDrive;
                linearDrive.maximumForce *= cubeScaleMultiplier;
                linearDrive.positionDamper *= cubeScaleMultiplier;
                linearDrive.positionSpring *= cubeScaleMultiplier;
                _joint.xDrive = linearDrive;
                _joint.yDrive = linearDrive;
                _joint.zDrive = linearDrive;

                // Disable joint for one frame so it doesn't freak out
                _joint.gameObject.SetActive(false);
            }
        }

        void GrabObject(GameObject obj) {
            Rigidbody rigidbody = obj.GetComponent<Rigidbody>();
            if (rigidbody == null) {
                Debug.LogError("Cannot grab object without a Rigidbody");
                return;
            }

            rigidbody.useGravity = false;
            _joint.connectedBody = rigidbody;
            heldObject = obj;

            if (onObjectGrabbed != null) {
                onObjectGrabbed(this, obj);
            }
        }

        void ReleaseObject() {
            GameObject obj = heldObject;
            Rigidbody rigidbody = obj.GetComponent<Rigidbody>();
            if (rigidbody == null) {
                Debug.LogError("Cannot release object without a Rigidbody");
                return;
            }

            rigidbody.useGravity = true;
            _joint.connectedBody = null;
            _currentObjectPortal = null;
            heldObject = null;

            if (onObjectReleased != null) {
                onObjectReleased(this, obj);
            }
        }

        public bool CarryingObject() {
            return heldObject != null;
        }

        public void Grab() {
            if (!CarryingObject()) {
                RaycastHit hit;
                if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out hit, _pickupRange, _layer, QueryTriggerInteraction.UseGlobal)) {
                    GameObject obj = hit.collider.gameObject;
                    //Debug.Log(obj);
                    //PortalClone portalClone = obj.GetComponent<PortalClone>();
                    //if (portalClone) {
                    //    // This is a clone, we should grab the real object instead
                    //    GrabObject(portalClone.target.gameObject);
                    //} else {
                        // Just pickin it up
                    GrabObject(obj);
                    //}
                }
            } else {
                Debug.LogError("Grab() called while already holding an object");
            }
        }

        public void Release() {
            if (CarryingObject()) {
                ReleaseObject();
            } else {
                Debug.LogError("Release() called without an object held");
            }
        }

        void OnPortalTeleport(Portal portal) {
            _pickupRange *= portal.PortalScaleAverage;
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.E)) {
                if (CarryingObject()) {
                    Release();
                } else {
                    Grab();
                }
            }
        }

        void FixedUpdate() {
            if (_currentObjectPortal) {
                // Current object on other side of portal, let's warp our position
                _currentObjectPortal.TeleportTransform(_floatingAnchor.transform, _staticAnchor.transform);
            } else {
                _floatingAnchor.transform.position = _staticAnchor.transform.position;
                _floatingAnchor.transform.rotation = _staticAnchor.transform.rotation;
            }
            _joint.gameObject.SetActive(true);
        }
    }
}
