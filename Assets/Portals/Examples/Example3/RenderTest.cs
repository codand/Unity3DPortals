using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderTest : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        StartCoroutine(Render());
    }

    IEnumerator Render() {
        yield return new WaitForEndOfFrame();
        GetComponent<Camera>().Render();
    }
}
