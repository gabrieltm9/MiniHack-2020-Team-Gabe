using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaintingAreaTrigger : MonoBehaviour
{
    public GameController gc;

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Player")
        {
            gc.EToAccess.SetActive(true);
            gc.canPaint = true;
            gc.inPaintRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        gc.EToAccess.SetActive(false);
        gc.canPaint = false;
        gc.inPaintRange = false;
    }
}
