using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleLightingOnKeyPress : MonoBehaviour
{
    [SerializeField] private Light _light;
    [SerializeField] private KeyCode _key = KeyCode.L;
    
    void Update()
    {
        if (Input.GetKeyDown(_key)) {
            _light.enabled = !_light.enabled;
        }
    }
}
