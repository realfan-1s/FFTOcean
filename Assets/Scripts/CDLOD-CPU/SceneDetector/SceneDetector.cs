using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 固定长度范围的包围盒
public class SceneDetector : MonoBehaviour, IDetector
{
    public Vector3 position => transform.position;
    public Vector3 boxSize;
    private Bounds m_bound;

    public int GetDetectedCode(float x, float z)
    {
        UpdateBounds();
        int code = 0;
        float minX = m_bound.min.x, minZ = m_bound.min.z, maxX = m_bound.max.x, maxZ = m_bound.max.z;
        if (x >= minX && z >= minZ)
            code |= 1;
        if (x >= minX && z <= maxZ)
            code |= 2;
        if (x <= maxX && z >= minZ)
            code |= 4;
        if (x <= maxX && z <= maxZ)
            code |= 8;
        return code;
    }

    public bool isDetected(Bounds bounds)
    {
        UpdateBounds();
        return m_bound.Intersects(bounds);
    }
    private void UpdateBounds()
    {
        m_bound.center = transform.position;
        m_bound.size = boxSize;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(m_bound.center, m_bound.size);
    }
}
