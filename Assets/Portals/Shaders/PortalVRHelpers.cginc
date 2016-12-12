#ifndef PORTAL_VR_HELPERS_INCLUDED
#define PORTAL_VR_HELPERS_INCLUDED

#define PORTAL_VR_EYE_LEFT -1
#define PORTAL_VR_EYE_RIGHT 1
#define PORTAL_VR_EYE_MONO 0

#define PORTAL_VR_CURRENT_EYE (unity_CameraProjection[0][2] < 0 ? PORTAL_VR_EYE_RIGHT : PORTAL_VR_EYE_LEFT)

#endif // PORTAL_VR_HELPERS_INCLUDED
