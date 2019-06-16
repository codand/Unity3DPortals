using System;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;
using System.Collections.Generic;

namespace Portals.Examples
{
    public class FreeLookCam : UnityStandardAssets.Cameras.PivotBasedCameraRig
    {
        // This script is designed to be placed on the root object of a camera rig,
        // comprising 3 gameobjects, each parented to the next:

        // 	Camera Rig
        // 		Pivot
        // 			Camera

        [SerializeField] private float m_MoveSpeed = 1f;                      // How fast the rig will move to keep up with the target's position.
        [Range(0f, 10f)] [SerializeField] private float m_TurnSpeed = 1.5f;   // How fast the rig will rotate from user input.
        [SerializeField] private float m_TurnSmoothing = 0.0f;                // How much smoothing to apply to the turn input, to reduce mouse-turn jerkiness
        [SerializeField] private float m_TiltMax = 75f;                       // The maximum value of the x axis rotation of the pivot.
        [SerializeField] private float m_TiltMin = 45f;                       // The minimum value of the x axis rotation of the pivot.
        [SerializeField] private bool m_LockCursor = false;                   // Whether the cursor should be hidden and locked.
        [SerializeField] private bool m_VerticalAutoReturn = false;           // set wether or not the vertical axis should auto return

        private float m_LookAngle;                    // The rig's y axis rotation.
        private float m_TiltAngle;                    // The pivot's x axis rotation.
        private const float k_LookDistance = 100f;    // How far in front of the pivot the character's look target is.
		private Vector3 m_PivotEulers;
		private Quaternion m_PivotTargetRot;
		private Quaternion m_TransformTargetRot;

        private List<Portal> m_Portals = new List<Portal>();
        private Teleportable m_Teleportable;
        // private float m_CameraDistance;

        protected override void Awake()
        {
            base.Awake();
            // Lock or unlock the cursor.
            Cursor.lockState = m_LockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !m_LockCursor;
			m_PivotEulers = m_Pivot.rotation.eulerAngles;

	        m_PivotTargetRot = m_Pivot.transform.localRotation;
			m_TransformTargetRot = transform.localRotation;

            m_Teleportable = m_Target.GetComponent<Teleportable>();
            //m_CameraDistance = m_Cam.localPosition.z;
        }


        protected void Update()
        {
            HandleRotationMovement();
            if (m_LockCursor && Input.GetMouseButtonUp(0))
            {
                Cursor.lockState = m_LockCursor ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !m_LockCursor;
            }
        }

        private void OnEnable() {
            if (m_Teleportable) {
                m_Teleportable.OnTeleport += OnTargetTeleported;
            }
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (m_Teleportable) {
                m_Teleportable.OnTeleport -= OnTargetTeleported;
            }
        }

        private void OnTargetTeleported(Teleportable sender, Portal portal) {
            if (m_Portals.Count > 0 && portal.ExitPortal == m_Portals[m_Portals.Count - 1]) {
                m_Portals.RemoveAt(m_Portals.Count - 1);
            } else {
                m_Portals.Add(portal);
            }
        }

        protected override void FollowTarget(float deltaTime)
        {
            if (m_Target == null) return;
            // Move the rig towards target position.

            Vector3 targetPosition = m_Target.position;
            foreach(Portal portal in m_Portals) {
                targetPosition = portal.InverseTeleportPoint(targetPosition);
            }
            transform.position = Vector3.Lerp(transform.position, targetPosition, deltaTime*m_MoveSpeed);
        }


        private void HandleRotationMovement() {
            if (Time.timeScale < float.Epsilon)
                return;

            // Read the user input
            var x = CrossPlatformInputManager.GetAxis("Mouse X");
            var y = CrossPlatformInputManager.GetAxis("Mouse Y");

            // Adjust the look angle by an amount proportional to the turn speed and horizontal input.
            m_LookAngle += x * m_TurnSpeed;

            if (m_VerticalAutoReturn) {
                // For tilt input, we need to behave differently depending on whether we're using mouse or touch input:
                // on mobile, vertical input is directly mapped to tilt value, so it springs back automatically when the look input is released
                // we have to test whether above or below zero because we want to auto-return to zero even if min and max are not symmetrical.
                m_TiltAngle = y > 0 ? Mathf.Lerp(0, -m_TiltMin, y) : Mathf.Lerp(0, m_TiltMax, -y);
            } else {
                // on platforms with a mouse, we adjust the current angle based on Y mouse input and turn speed
                m_TiltAngle -= y * m_TurnSpeed;
                // and make sure the new value is within the tilt range
                m_TiltAngle = Mathf.Clamp(m_TiltAngle, -m_TiltMin, m_TiltMax);
            }

            // Rotate the rig (the root object) around Y axis only:
            m_TransformTargetRot = Quaternion.Euler(0f, m_LookAngle, 0f);

            // Tilt input around X is applied to the pivot (the child of this object)
            m_PivotTargetRot = Quaternion.Euler(m_TiltAngle, m_PivotEulers.y, m_PivotEulers.z);

            if (m_TurnSmoothing > 0) {
                m_Pivot.localRotation = Quaternion.Slerp(m_Pivot.localRotation, m_PivotTargetRot, m_TurnSmoothing * Time.deltaTime);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, m_TransformTargetRot, m_TurnSmoothing * Time.deltaTime);
            } else {
                m_Pivot.localRotation = m_PivotTargetRot;
                transform.localRotation = m_TransformTargetRot;
            }


            if (m_Portals.Count > 0) {
                Portal portal = m_Portals[0];

                float srcRot = Util.NormalizeAngle(transform.eulerAngles.y, -180, 180);
                float dstRot = ClampAngle(srcRot, portal, portal.transform.up);
                if (srcRot != dstRot) {
                    m_LookAngle = dstRot;
                    transform.localRotation = Quaternion.Euler(0, m_LookAngle, 0);
                }

                float srcTilt = Util.NormalizeAngle(m_Pivot.eulerAngles.x, -180, 180);
                float dstTilt = ClampAngle(srcTilt, portal, portal.transform.right);
                if (srcTilt != dstTilt) {
                    m_TiltAngle = dstTilt;
                    m_Pivot.localRotation = Quaternion.Euler(m_TiltAngle, 0, 0);
                }

                if (portal.Plane.GetSide(m_Cam.position)) {
                    transform.position = portal.TeleportPoint(transform.position);
                    transform.rotation = portal.TeleportRotation(transform.rotation);
                    m_Portals.RemoveAt(0);
                }
            }
        }


        // Returns true if we clamped, and sets the angle to 
        private float ClampAngle(float angle, Portal portal, Vector3 axis) {
            float doorScale = 0.5f;

            Vector3 cam = m_Cam.position;
            Vector3 pivot = m_Pivot.position;
            Vector3 direction = (cam - pivot).normalized;
            float enter = 0;
            float angleOffset = 0;
            if (portal.Plane.Raycast(new Ray(pivot, direction), out enter)) {
            //portal.Plane.Raycast(new Ray(pivot, direction), out enter);
            //if (Mathf.Abs(enter) > 0.1f) {
                Vector3 intersection = pivot + enter * direction;

                Vector3 localIntersection = portal.transform.InverseTransformPoint(intersection);
                Vector3 clampedLocalIntersection = Vector3.Min(new Vector3(0.5f, 0.5f, 0.0f) * doorScale, Vector3.Max(new Vector3(-0.5f, -0.5f, 0.0f) * doorScale, localIntersection));
                Vector3 clampedWorldIntersection = portal.transform.TransformPoint(clampedLocalIntersection);

                Vector3 newDirection = (clampedWorldIntersection - pivot).normalized;
                //Debug.DrawLine(pivot, intersection);
                //Debug.DrawLine(pivot, clampedWorldIntersection, Color.cyan);

                angleOffset = Util.SignedPlanarAngle(newDirection, direction, axis);
            }
            return Util.NormalizeAngle(angleOffset + angle, -180, 180);
        }
    }
}
