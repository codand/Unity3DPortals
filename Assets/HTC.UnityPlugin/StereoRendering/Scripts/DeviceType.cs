using UnityEngine;
using UnityEngine.VR;

namespace HTC.UnityPlugin.StereoRendering
{
    // enum of supported device types
    public enum HmdType { Unsupported, SteamVR };

    public class StereoRenderDevice
    {
        public static HmdType GetHmdType()
        {
            HmdType type = HmdType.Unsupported;

            #if UNITY_5_3
                string deviceName = VRSettings.loadedDevice.ToString();
                
                // there is no "official" Vive support in Unity 5.3, thus refer to SteamVR plugin for checking hmd type           
                if (deviceName == "None")
                {
                    var steamCam = Object.FindObjectOfType<SteamVR_Camera>();
                    if(steamCam != null)
                        type = HmdType.SteamVR;
                }
            #else
                string deviceName = VRSettings.loadedDeviceName;

                if (deviceName == "OpenVR")
                {
                    type = HmdType.SteamVR;
                }
            #endif

            return type;
        }

        public static IDeviceParamFactory InitParamFactory(HmdType hmdType)
        {
            if (hmdType == HmdType.SteamVR)
            {
                return new SteamVRParamFactory();
            }

            return null;
        }
    }
}
