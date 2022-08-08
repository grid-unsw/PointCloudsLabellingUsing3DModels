using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using VoxelSystem.PointCloud;

public static class ImportPC
{
    public static PointCloud<T> ReadPts<T>(string path, char delimiter, bool flipAxes = false, int maxPointsToRead = 0, int readEveryNthPoint = 0)
    {
        T[] points;
        var minX = Mathf.Infinity;
        var minY = Mathf.Infinity;
        var minZ = Mathf.Infinity;
        var maxX = Mathf.NegativeInfinity;
        var maxY = Mathf.NegativeInfinity;
        var maxZ = Mathf.NegativeInfinity;

        var fileStream = new FileStream(@path, FileMode.Open, FileAccess.Read);
        using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
        {
            var pointsText = streamReader.ReadLine();
            if (pointsText == null)
                throw new ArgumentException("File with no points!");

            var pointNum = int.Parse(pointsText);
            if (maxPointsToRead == 0)
            {
                points = new T[pointNum];
            }
            else
            {
                points = new T[maxPointsToRead];
            }
            

            var i = 0;
            var j = 0;
            var line = "";
            while ((line = streamReader.ReadLine()) != null && i < maxPointsToRead)
            {
                if (readEveryNthPoint != j)
                {
                    j++;
                    continue;
                }
                else
                {
                    j = 0;
                }

                var pointValues = line.Split(delimiter);

                var x = float.Parse(pointValues[0]);
                float y;
                float z;
                if (flipAxes)
                {
                    z = float.Parse(pointValues[1]);
                    y = float.Parse(pointValues[2]);
                }
                else
                {
                    y = float.Parse(pointValues[1]);
                    z = float.Parse(pointValues[2]);
                }

                if (typeof(T) == typeof(PointColour))
                {
                    var red = float.Parse(pointValues[3]);
                    var green = float.Parse(pointValues[4]);
                    var blue = float.Parse(pointValues[5]);
                    points[i] = (T)Activator.CreateInstance(typeof(T), x, y, z, red, green, blue);
                }
                else
                {
                    points[i] = (T)Activator.CreateInstance(typeof(T), x, y, z);
                }

                if (x < minX)
                    minX = x;
                if (y < minY)
                    minY = y;
                if (z < minZ)
                    minZ = z;
                if (x > maxX)
                    maxX = x;
                if (y > maxY)
                    maxY = y;
                if (z > maxZ)
                    maxZ = z;

                i++;
            }
        }
        var centre = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        var size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
        var bounds = new Bounds(centre, size);

        return new PointCloud<T>(bounds, points);
    }

    public static IEnumerable<T> ReadPtsOneByOnePoint<T>(string path, char delimiter, bool flipAxes = false, int offset = 0, int maxPointsToRead = 0)
    {
        var fileStream = new FileStream(@path, FileMode.Open, FileAccess.Read);
        using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
        {
            var pointsText = streamReader.ReadLine();
            if (pointsText == null)
                throw new ArgumentException("File with no points!");
            
            var pointNum = maxPointsToRead != 0 ? maxPointsToRead : int.Parse(pointsText);

            for (var i = offset; i < pointNum; i++)
            {
                var line = streamReader.ReadLine();
                var pointValues = line.Split(delimiter);

                var x = float.Parse(pointValues[0]);
                float y;
                float z;
                if (flipAxes)
                {
                    z = float.Parse(pointValues[1]);
                    y = float.Parse(pointValues[2]);
                }
                else
                {
                    y = float.Parse(pointValues[1]);
                    z = float.Parse(pointValues[2]);
                }

                yield return (T) Activator.CreateInstance(typeof(T), x, y, z);
            }
        }
    }
}
