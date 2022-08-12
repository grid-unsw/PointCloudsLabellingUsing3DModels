using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using g3;
using UnityEngine;
using VoxelSystem.PointCloud;

public class Manager : MonoBehaviour
{
    public string PcPath;
    public Color PcColor;
    public bool VisualisePoints;
    public int VisualiseNPoints = 1000;
    public float MaxDistToObject = 0.1f;
    public string ExportFilePath;
    public bool ClearExportFile;
    public int PointOffset = 0;
    public int LastPointToRead = 0;
    public bool Distance;
    public bool Coordinates;
    public bool ObjectId;
    private BoundsOctree<BuildingComponentTriangle> _buildingComponentTriangleBoundsOctree;
    // Start is called before the first frame update
    async Task Start()
    {
        var boxSize = MaxDistToObject * 2;
        UpdateBoundsOctree(boxSize);
        //visualise some points to check if they match with the 3d model
        if (VisualisePoints)
        {
            var pointCloud = ImportPC.ReadPts<Point>(PcPath, ' ', true, VisualiseNPoints, 10000);
            CreatePointsInScene(pointCloud, VisualiseNPoints, 0.1f);
        }

        if (ClearExportFile)
        {
            System.IO.File.WriteAllText(ExportFilePath, string.Empty);
        }

        var squaredMaxDistanceToObject = MaxDistToObject * MaxDistToObject;
        var pointsProcessed = 0;
        foreach (var points in ImportPC.ReadPtsNPoints(PcPath, ' ', true, PointOffset, LastPointToRead))
        {
            var pointsToExport = new string[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var pointCloudPoint = points[i];
                var closestObject = FindClosestObjectIdNDistance(pointCloudPoint, squaredMaxDistanceToObject, boxSize);
                if (Coordinates && ObjectId && Distance)
                {
                    pointsToExport[i] =
                        $"{pointCloudPoint.x},{pointCloudPoint.z},{pointCloudPoint.y},{closestObject.Item1},{closestObject.Item2}";
                }
                else if (ObjectId && Distance)
                {
                    pointsToExport[i] = $"{closestObject.Item1},{closestObject.Item2}";
                }
                else if (Coordinates && ObjectId)
                {
                    pointsToExport[i] = $"{pointCloudPoint.x},{pointCloudPoint.z},{pointCloudPoint.y},{closestObject.Item1}";
                }
                else if (ObjectId)
                {
                    pointsToExport[i] = $"{closestObject.Item1}";
                }
            }

            await AsyncWriting(pointsToExport);
            pointsProcessed += points.Length;
            Debug.Log($"{pointsProcessed} points are processed!");
        }
    }

    private void CreatePointsInScene(PointCloud<Point> pointCloud, int number, float pointSize)
    {
        var parentContainer = new GameObject("Point cloud");
        for (var i = 0; i < number; i++)
        {
            var point = pointCloud.points[i];
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(pointSize, pointSize, pointSize);
            sphere.transform.position = new Vector3(point.point.x, point.point.y, point.point.z);
            sphere.GetComponent<MeshRenderer>().material.color = PcColor;
            sphere.transform.parent = parentContainer.transform;
        }
    }

    private void UpdateBoundsOctree(float octreeNodeSize)
    {
        var meshFilters = GameObject.FindObjectsOfType<MeshFilter>();
        //identify scene bounds needed for the octree
        var sceneBounds = new Bounds();
        foreach (var meshFilter in meshFilters)
        {
            var bounds = meshFilter.gameObject.GetComponent<Renderer>().bounds;
            sceneBounds.Encapsulate(bounds);
        }
        //initialise octree
        var largestBoundsSize = Mathf.Max(sceneBounds.size.x, Mathf.Max(sceneBounds.size.y, sceneBounds.size.z));

        _buildingComponentTriangleBoundsOctree =
            new BoundsOctree<BuildingComponentTriangle>(largestBoundsSize, sceneBounds.center, octreeNodeSize, 1.25f);
        //update octree
        foreach (var meshFilter in meshFilters)
        {
            var buildingComponentTriangles = BuildingComponentTriangles(meshFilter, meshFilter.GetInstanceID());

            foreach (var triangle in buildingComponentTriangles)
            {
                var triangleBounds = GetBounds(new Vector3[] { Vector3dToVector(triangle.Triangle.V0), Vector3dToVector(triangle.Triangle.V1), Vector3dToVector(triangle.Triangle.V2) });

                _buildingComponentTriangleBoundsOctree.Add(triangle, triangleBounds);
            }
        }
    }

    private (int,float) FindClosestObjectIdNDistance(Vector3 point, float squaredMaxDistanceToObject, float boxSize)
    {
        var pointBounds = new Bounds(point, new Vector3(boxSize, boxSize, boxSize));
        var collidingWithTriangles = new List<BuildingComponentTriangle>();
        _buildingComponentTriangleBoundsOctree.GetColliding(collidingWithTriangles, pointBounds);

        var closestObjectId = 0;
        var closestDistance = squaredMaxDistanceToObject;
        foreach (var triangle in collidingWithTriangles)
        {
            var distance = new DistPoint3Triangle3(new Vector3d(point.x,point.y,point.z), triangle.Triangle).Compute();
            if (closestDistance > distance.DistanceSquared)
            {
                closestDistance = (float)distance.DistanceSquared;
                closestObjectId = triangle.ObjectId;
            }
        }

        return (closestObjectId, Mathf.Sqrt(closestDistance));
    }

    private async Task AsyncWriting(string[] points)
    {
        using (FileStream stream = new FileStream(ExportFilePath, FileMode.Append, FileAccess.Write))
        {
            using StreamWriter outputFile = new(stream);
            {
                foreach (var point in points)
                {
                    await outputFile.WriteLineAsync(point);
                }
            }
        }

    }

    private BuildingComponentTriangle[] BuildingComponentTriangles(MeshFilter meshFilter, int id)
    {
        var buildingComponentTriangles = new BuildingComponentTriangle[meshFilter.sharedMesh.triangles.Length/3];

        var triangles = meshFilter.sharedMesh.triangles;
        var vertices = meshFilter.sharedMesh.vertices;
        var trans = meshFilter.transform;
        for (int i = 0, j=0; i < meshFilter.sharedMesh.triangles.Length; i+=3,j++)
        {
            var vertex0 = trans.TransformPoint(vertices[triangles[i]]);
            var vertex1 = trans.TransformPoint(vertices[triangles[i +1]]);
            var vertex2 = trans.TransformPoint(vertices[triangles[i +2]]);

            buildingComponentTriangles[j] = new BuildingComponentTriangle(new Triangle3d(VectorToVector3d(vertex0), VectorToVector3d(vertex1),
                VectorToVector3d(vertex2)),id);
        }

        return buildingComponentTriangles;
    }

    private static Vector3d VectorToVector3d(Vector3 vector)
    {
        return new Vector3d(vector.x, vector.y, vector.z);
    }

    private static Vector3 Vector3dToVector(Vector3d vector)
    {
        return new Vector3((float)vector.x, (float)vector.y, (float)vector.z);
    }

    private static Bounds GetBounds(Vector3[] points)
    {
        var minMax = MinMaxBounds(points);

        var center = (minMax.Item1 + minMax.Item2) / 2;
        var size = minMax.Item2 - minMax.Item1;

        return new Bounds(center, size);
    }

    private static (Vector3, Vector3) MinMaxBounds(Vector3[] points)
    {
        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;

        foreach (var point in points)
        {
            // update min and max
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min, max);
    }
}

public class BuildingComponentTriangle
{
    public Triangle3d Triangle { get; }
    public int ObjectId { get; }

    public BuildingComponentTriangle(Triangle3d triangle, int id)
    {
        Triangle = triangle;
        ObjectId = id;
    }
}