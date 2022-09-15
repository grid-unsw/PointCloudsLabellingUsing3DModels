using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CGALDotNet;
using CGALDotNet.Polyhedra;
using CGALDotNetGeometry.Numerics;
using g3;
using UnityEngine;
using VoxelSystem.PointCloud;
using Vector3d = g3.Vector3d;

public class Manager : MonoBehaviour
{
    public string PcPath;
    public Color PcColor;
    public bool VisualisePoints;
    public int VisualiseNPoints = 1000;
    public float MaxDistToObject = 0.1f;
    public string ExportPointsFilePath;
    public string ExportSurfaceFilePath;
    public bool ClearExportFiles;
    public int PointOffset = 0;
    public int LastPointToRead = 0;
    public bool Distance;
    public bool Coordinates;
    public bool SurfaceId;
    private BoundsOctree<BuildingComponentTriangle> _buildingComponentTriangleBoundsOctree;
    // Start is called before the first frame update
    async Task Start()
    {
        if (ClearExportFiles)
        {
            File.WriteAllText(ExportPointsFilePath, string.Empty);
            File.WriteAllText(ExportSurfaceFilePath, string.Empty);
        }

        var boxSize = MaxDistToObject * 2;
        await UpdateBoundsOctree(boxSize);
        Debug.Log($"Surfaces are stored in file!");
        //visualise some points to check if they match with the 3d model
        if (VisualisePoints)
        {
            var pointCloud = ImportPC.ReadPts<Point>(PcPath, ' ', true, VisualiseNPoints, 10000);
            CreatePointsInScene(pointCloud, VisualiseNPoints, 0.1f);
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
                if (Coordinates && SurfaceId && Distance)
                {
                    pointsToExport[i] =
                        $"{pointCloudPoint.x},{pointCloudPoint.z},{pointCloudPoint.y},{closestObject.Item1},{closestObject.Item2}";
                }
                else if (SurfaceId && Distance)
                {
                    pointsToExport[i] = $"{closestObject.Item1},{closestObject.Item2}";
                }
                else if (Coordinates && SurfaceId)
                {
                    pointsToExport[i] = $"{pointCloudPoint.x},{pointCloudPoint.z},{pointCloudPoint.y},{closestObject.Item1}";
                }
                else if (SurfaceId)
                {
                    pointsToExport[i] = $"{closestObject.Item1}";
                }
            }

            await AsyncWriting(ExportPointsFilePath, pointsToExport);
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

    private async Task UpdateBoundsOctree(float octreeNodeSize)
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

        var objectsSurfacesText = new List<string>();

        //update octree
        foreach (var meshFilter in meshFilters)
        {
            var polyhedron = CGALMeshExtensions.ToCGALPolyhedron3<EIK>(meshFilter.sharedMesh);

            if (!polyhedron.IsValid)
            {
                Debug.Log($"{meshFilter.name} is not valid");
                continue;
            }

            var id = meshFilter.GetInstanceID();
            var surfaces = new List<Polyhedron3<EIK>>();
            polyhedron.Split(surfaces);

            var i = 0;
            foreach (var surface in surfaces)
            {
                var surfaceId = $"{id}_{i}";
                var box3D = surface.FindBoundingBox();
                var normals = new CGALDotNetGeometry.Numerics.Vector3d[1];
                surface.GetFaceNormals(normals, 1);
                var normal = normals[0];
                var point = new Vector3((float)normal.x, (float)normal.y, (float)normal.z);
                var pointMinTranslated = meshFilter.transform.TransformPoint(Point3dToVector3(box3D.Min));
                var pointMaxTranslated = meshFilter.transform.TransformPoint(Point3dToVector3(box3D.Max));
                var pointMin = Vector3.Min(pointMinTranslated, pointMaxTranslated);
                var pointMax = Vector3.Max(pointMinTranslated, pointMaxTranslated);
                var objSurfaceText = $"{surfaceId}, {SwitchYZAxes(pointMin)}, {SwitchYZAxes(pointMax)}, {SwitchYZAxes(point)}";
                objectsSurfacesText.Add(objSurfaceText);

                var buildingComponentTriangles = BuildingComponentTriangles(meshFilter, surface, surfaceId);

                foreach (var triangle in buildingComponentTriangles)
                {
                    var triangleBounds = GetBounds(new Vector3[] { Vector3dToVector(triangle.Triangle.V0), Vector3dToVector(triangle.Triangle.V1), Vector3dToVector(triangle.Triangle.V2) });

                    _buildingComponentTriangleBoundsOctree.Add(triangle, triangleBounds);
                }

                i++;
            }
        }
        await AsyncWriting(ExportSurfaceFilePath, objectsSurfacesText.ToArray());
    }

    private (string,float) FindClosestObjectIdNDistance(Vector3 point, float squaredMaxDistanceToObject, float boxSize)
    {
        var pointBounds = new Bounds(point, new Vector3(boxSize, boxSize, boxSize));
        var collidingWithTriangles = new List<BuildingComponentTriangle>();
        _buildingComponentTriangleBoundsOctree.GetColliding(collidingWithTriangles, pointBounds);

        var closestSurfaceId = "";
        var closestDistance = squaredMaxDistanceToObject;
        foreach (var triangle in collidingWithTriangles)
        {
            var distance = new DistPoint3Triangle3(new Vector3d(point.x,point.y,point.z), triangle.Triangle).Compute();
            if (closestDistance > distance.DistanceSquared)
            {
                closestDistance = (float)distance.DistanceSquared;
                closestSurfaceId = triangle.SurfaceId;
            }
        }

        return (closestSurfaceId, Mathf.Sqrt(closestDistance));
    }

    private async Task AsyncWriting(string filePath, string[] points)
    {
        using (FileStream stream = new FileStream(filePath, FileMode.Append, FileAccess.Write))
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

    private BuildingComponentTriangle[] BuildingComponentTriangles(MeshFilter meshFilter, Polyhedron3 polyhedron, string id)
    {
        var triangles = new CGALDotNetGeometry.Shapes.Triangle3d[polyhedron.FaceCount];
        polyhedron.GetTriangles(triangles, polyhedron.FaceCount);

        var buildingComponentTriangles = new BuildingComponentTriangle[polyhedron.FaceCount];

        for (var i = 0; i < polyhedron.FaceCount; i++)
        {
            var triangle = triangles[i];
            var pointA = meshFilter.transform.TransformPoint(Point3dToVector3(triangle.A));
            var pointB = meshFilter.transform.TransformPoint(Point3dToVector3(triangle.B));
            var pointC = meshFilter.transform.TransformPoint(Point3dToVector3(triangle.C));
            buildingComponentTriangles[i] = new BuildingComponentTriangle(new Triangle3d(VectorToVector3d(pointA), VectorToVector3d(pointB),
                VectorToVector3d(pointC)), id);
        }

        return buildingComponentTriangles;
    }

    private static Vector3d VectorToVector3d(Vector3 vector)
    {
        return new Vector3d(vector.x, vector.y, vector.z);
    }

    private static Vector3 Point3dToVector3(Point3d point3d)
    {
        return new Vector3((float)point3d.x, (float)point3d.y, (float)point3d.z);
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

    private static Vector3 SwitchYZAxes(Vector3 vector)
    {
        return new Vector3(vector.x, vector.z, vector.y);
    }
}

public class BuildingComponentTriangle
{
    public Triangle3d Triangle { get; }
    public string SurfaceId { get; }

    public BuildingComponentTriangle(Triangle3d triangle, string id)
    {
        Triangle = triangle;
        SurfaceId = id;
    }
}