using System.Collections;
using System.Collections.Generic;
using IthacaLib;
using UnityEngine;

public class AdaptPointSize : MonoBehaviour
{

    public CameraController cameraController;
    private PointCloudPositionAndColorBinder binder;


    public float minSize = 0.001f;   
    public float maxSize = 0.02f;      

    void Update()
    {
        

        float distanceref = cameraController.distance;

       
        float normalizedDistance = Mathf.InverseLerp(0.8f, 8f, distanceref);

        
        float adjustedSize = Mathf.Lerp(minSize, maxSize, normalizedDistance);

        if(binder == null)
        {
            binder = GetComponent<PointCloudPositionAndColorBinder>();
        }

        if (binder != null)
        {
            binder.pointSize = adjustedSize;
            
        }
       
    }
}






