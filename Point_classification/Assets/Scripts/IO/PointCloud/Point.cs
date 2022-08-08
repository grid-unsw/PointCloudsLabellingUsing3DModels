using UnityEngine;

namespace VoxelSystem.PointCloud
{
    public class Point
    {
        public Vector3 point { get; }

        public Point(Vector3 Point)
        {
            point = Point;
        }

        public Point(float x, float  y, float z)
        {
            point = new Vector3(x,y,z);
        }
    }
}