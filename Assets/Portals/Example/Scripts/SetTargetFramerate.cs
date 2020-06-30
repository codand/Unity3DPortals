using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Linq;

namespace CodJam {
	public class SetTargetFramerate : MonoBehaviour {
        enum UpdateMode {
            Start,
            Update
        }


        [SerializeField] private int targetFrameRate = 30;
        [SerializeField] UpdateMode updateMode = UpdateMode.Start;

        void Start() {
            if (updateMode == UpdateMode.Start) {
                Application.targetFrameRate = targetFrameRate;
            }
        }

        void Update() {
            if (updateMode == UpdateMode.Update) {
                Application.targetFrameRate = targetFrameRate;
            }
        }
    }
}
