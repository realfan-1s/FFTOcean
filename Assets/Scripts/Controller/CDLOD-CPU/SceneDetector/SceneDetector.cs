using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 固定长度范围的包围盒
public class SceneDetector : MonoBehaviour, IDetector
{
    public Vector3 position => transform.position;
    public Vector3 boxSize;
    private Bounds m_bound;

    public DetectDir GetDetectedCode(float x, float y, float z)
    {
        throw new System.NotImplementedException();
    }

    public bool isDetected(Bounds bounds)
    {
        throw new System.NotImplementedException();
    }
}
