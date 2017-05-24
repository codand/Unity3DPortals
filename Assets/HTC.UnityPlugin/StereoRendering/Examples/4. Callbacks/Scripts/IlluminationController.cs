//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

using UnityEngine;
using HTC.UnityPlugin.StereoRendering;

public class IlluminationController : MonoBehaviour
{
    public Light targetLight;

    private Color normalColor = new Color(1.0f, 1.0f, 1.0f);
    private Color scaryColor = new Color(0.0f, 1.0f, 0.0f);

    void Awake()
    {
        StereoRenderManager.Instance.AddPreRenderListener(OnBeforeRender);
        StereoRenderManager.Instance.AddPostRenderListener(OnAfterRender);
    }

    void OnDestroy()
    {
        StereoRenderManager.Instance.RemovePreRenderListener(OnBeforeRender);
        StereoRenderManager.Instance.RemovePostRenderListener(OnAfterRender);
    }

    void OnBeforeRender()
    {
        targetLight.color = scaryColor;
    }

    void OnAfterRender()
    {
        targetLight.color = normalColor;
    }
}
