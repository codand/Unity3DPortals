Vive Stereo Rendering Toolkit - v1.0.4
Copyright 2016, HTC Corporation. All rights reserved.


Introduction

Vive Stereo Rendering Toolkit provides drag-and-drop components for developers to create stereoscopic rendering effects in a few minutes. 
With this toolkit, effects such as mirrors or portal doors can be easily achieved in your VR application.


System Requirements

1. Unity 5.3.5 or higher.
   This toolkit is compatible with native VR rendering introduced in Unity 5.4.

2. SteamVR Unity plugin, version 1.0.8 or higher


For tutorial and API reference, please see the attached Developer's Guide.

============================================================================================================================
Change log:

v1.0.4
 - support double-sided StereoRenderer
 - bug fix: fix right-eye rendering of mirrors (for Unity 5.4+)

v1.0.3
 - Unity 5.5 compatibility
 - enable creation of StereoRenderer at runtime
 - add parameter for tweaking resolution of rendertextures
 - bug fix: fix "drifting" when entering portals (for Unity 5.4)
 - bug fix: fix rendering of near-anchor objects (for Unity 5.4+)

v1.0.2
 - bug fix: fix wrong shadow in canvases when using directional light (for Unity 5.4)

v1.0.1
 - add functions to query the up, forward and right vectors of canvas origin
 - support moving or rotating mirrors at run time
 - support mirrors of arbitrary orientation
 - bug fix: only remove StereoRenderers from manager when their host object is destroyed