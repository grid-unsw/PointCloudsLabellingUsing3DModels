using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VoxelSystem.PointCloud;

public class Manager : MonoBehaviour
{
    public string pointCloudsPath;
    public Material material;
    public string exportFilePath;
    private BoundsOctree<BuildingComponent> buildingComponentBoundsOctree;
    private float _searchingRadius = 0.1f;
    // Start is called before the first frame update
    void Start()
    {
        UpdateBoundsOctree(_searchingRadius);
        //visualise some points to check if they match with the 3d model
        //var pointCloud = ImportPC.ReadPts<Point>(pointCloudsPath, ' ', true, 1000, 100000);
        //CreatePointsInScene(pointCloud, 1000, 0.1f);

        var outputFile = new StreamWriter(exportFilePath, true);
        //in case you want to label only points between two numbers. For example, you can use offset and number of following elements
        //foreach (var pointCloudPoint in ImportPC.ReadPtsOneByOnePoint<Point>(pointCloudsPath, ' ',true, 100,10000))
        foreach (var pointCloudPoint in ImportPC.ReadPtsOneByOnePoint<Point>(pointCloudsPath, ' ', true))
        {
            var closestObjectId = FindClosestObjectId(pointCloudPoint.point, _searchingRadius);
            // it just appends to 
            outputFile.WriteLine(closestObjectId);
            //in case you want to export point coordinates and closest object id at the same time
            //outputFile.WriteLine($"{pointCloudPoint.point.x },{pointCloudPoint.point.z},{pointCloudPoint.point.y},{closestObjectId}");
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
            sphere.GetComponent<MeshRenderer>().material = material;
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

        buildingComponentBoundsOctree =
            new BoundsOctree<BuildingComponent>(largestBoundsSize, sceneBounds.center, octreeNodeSize, 1.25f);
        //update octree
        foreach (var meshFilter in meshFilters)
        {
            var bounds = meshFilter.gameObject.GetComponent<Renderer>().bounds;

            var buildingComponent = new BuildingComponent(meshFilter, meshFilter.GetInstanceID());

            buildingComponentBoundsOctree.Add(buildingComponent,bounds);
        }
    }

    private int FindClosestObjectId(Vector3 point, float searchingRadius)
    {
        var pointBounds = new Bounds(point, new Vector3(searchingRadius, searchingRadius, searchingRadius));
        var collidingWith = new List<BuildingComponent>();
        buildingComponentBoundsOctree.GetColliding(collidingWith, pointBounds);

        var closestObjectId = 0;
        var closestDistance = searchingRadius;
        foreach (var buildingComponent in collidingWith)
        {
            var closestPoint = buildingComponent.MeshF.GetComponent<MeshCollider>().ClosestPoint(point);
            var distance = Vector3.Distance(point, closestPoint);
            if (closestDistance > distance)
            {
                closestDistance = distance;
                closestObjectId = buildingComponent.Id;
            }
        }
        //Debug.Log(closestDistance);
        return closestObjectId;
    }
}

public class BuildingComponent
{
    public MeshFilter MeshF { get; }
    public int Id { get; }

    public BuildingComponent(MeshFilter meshFilter, int id)
    {
        MeshF = meshFilter;
        Id = id;
    }
}