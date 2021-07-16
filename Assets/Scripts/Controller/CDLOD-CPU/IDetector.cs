using UnityEngine;
public interface IDetector
{
    Vector3 Position { get; }
    bool isDectcted(Bounds bounds);
}
