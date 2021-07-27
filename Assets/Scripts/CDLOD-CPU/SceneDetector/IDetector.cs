﻿using UnityEngine;
public enum DetectDir
{
    SouthWest = 1,
    NorthWest = 2,
    SouthEast = 4,
    NorthEast = 8
}
/// <summary>
/// 默认使用AABB和进行裁剪，在AABB盒内、超出AABB盒一段时间、在AABB盒外一段时间内
/// </summary>
public interface IDetector
{
    Vector3 position { get; }
    bool isDetected(Bounds bounds);

    /// <summary>
    /// 确定碰撞发生的位置，四叉树下碰撞代号
    /// |2|8
    /// |1|4
    /// </summary>
    /// <param name="x"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    int GetDetectedCode(float x, float z);
}