using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;
using System;
using System.Reflection;

public class PortalClone : MonoBehaviour {
    public Transform target;
    public Portal portal;
    public bool isDepthMasker = false;

    void Awake() {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody) {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }
    }

	void LateUpdate () {
        if (isDepthMasker) {
            this.transform.position = target.transform.position;
            this.transform.rotation = target.transform.rotation;
            this.transform.localScale = target.transform.localScale;
        } else {
            portal.ApplyWorldToPortalTransform(this.transform, target);
        }

        CopyAnimations();
    }

    void CopyAnimations() {
        Animator src = target.GetComponent<Animator>();
        if (!src) {
            return;
        }
        Animator dst = this.GetComponent<Animator>();
        if (!dst) {
            return;
        }

        //for (int i = 0; i < src.layerCount; i++) {
        //    AnimatorStateInfo srcInfo = src.GetCurrentAnimatorStateInfo(i);
        //    AnimatorStateInfo srcInfoNext = src.GetNextAnimatorStateInfo(i);
        //    AnimatorTransitionInfo srcTransitionInfo = src.GetAnimatorTransitionInfo(i);

        //    //dst.Play(srcInfo.fullPathHash, i, srcInfo.normalizedTime);
        //}

        for (int i = 0; i < src.parameterCount; i++) {
            AnimatorControllerParameter parameter = src.parameters[i];
            if (src.IsParameterControlledByCurve(parameter.nameHash)) {
                continue;
            }
            switch (parameter.type) {
                case AnimatorControllerParameterType.Float:
                    dst.SetFloat(parameter.name, src.GetFloat(parameter.name));
                    break;
                case AnimatorControllerParameterType.Int:
                    dst.SetInteger(parameter.name, src.GetInteger(parameter.name));
                    break;
                case AnimatorControllerParameterType.Bool:
                    dst.SetBool(parameter.name, src.GetBool(parameter.name));
                    break;
                case AnimatorControllerParameterType.Trigger:
                    // TODO: figure out how to set triggers
                    //dst.SetTrigger(parameter.nameHash, parameter.);
                    break;
                default:
                    break;
            }
        }

        dst.speed = src.speed;
    }
}
