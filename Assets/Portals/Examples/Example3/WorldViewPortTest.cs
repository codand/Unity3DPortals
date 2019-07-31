using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldViewPortTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(Camera.main.WorldToViewportPoint(transform.position));
    }
}
