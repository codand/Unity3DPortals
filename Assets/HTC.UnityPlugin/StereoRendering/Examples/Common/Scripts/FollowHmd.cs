//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

using UnityEngine;
using System.Collections;

public class FollowHmd : MonoBehaviour
{
    public GameObject hmdCamera;
    
    void Update()
    {
#if UNITY_5_4_OR_NEWER
        transform.position = hmdCamera.transform.position;
#else
        transform.position = hmdCamera.transform.parent.transform.position;
#endif
    }
}
