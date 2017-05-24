using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderColor : MonoBehaviour {
    public Camera leftCam;
    public Camera rightCam;

	void OnWillRenderObject() {
        if (Camera.current == leftCam) {
            Debug.Log("Left 1");
            rightCam.Render();

            GetComponent<Renderer>().material.SetTexture("_MainTex", rightCam.targetTexture);
            GetComponent<Renderer>().material.color = Color.white;

            Debug.Log("Left 2");
        }

        if (Camera.current == rightCam) {
            Debug.Log("Right 1");
            GetComponent<Renderer>().material.color = Color.green;

            Debug.Log("Right 2");
        }
    }
}
