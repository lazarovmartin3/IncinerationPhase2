using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Burst;
using UnityEngine.Jobs;
using static KDTree;

public class Spawner : MonoBehaviour
{
    public GameObject sphere_prefab;

    public int number_of_spheres = 10;
    public Rect boundaries = new Rect(0,0,9,9);
    public bool draw_Gizmos;
   
    private List<SphereInsance> spheres = new List<SphereInsance>();
    private bool are_spawned;

    private void SpawnSpheres()
    {
        for (int i = 0; i < number_of_spheres; i++)
        {
            GameObject sphere = Instantiate(sphere_prefab, new Vector3(Random.Range(boundaries.xMin, boundaries.xMax), 0.3f, Random.Range(boundaries.yMin, boundaries.yMax)),
                Quaternion.identity);
            sphere.transform.SetParent(transform);
            sphere.name = i.ToString();
            spheres.Add(new SphereInsance
            {
                sphere = sphere,
                z_speed = Random.Range(-2, 2),
                x_speed = Random.Range(-2, 2)
            });
        }
    }

    private void Update()
    {
        if (!are_spawned && Input.GetMouseButton(0))
        {
            SpawnSpheres();
            are_spawned = true;
        }

        TransformAccessArray transformAccessArray = new TransformAccessArray(spheres.Count);
        NativeArray<float> zSpeedArray = new NativeArray<float>(spheres.Count, Allocator.TempJob);
        NativeArray<float> xSpeedArray = new NativeArray<float>(spheres.Count, Allocator.TempJob);

        for (int i = 0; i < spheres.Count; i++)
        {
            transformAccessArray.Add(spheres[i].sphere.transform);
            zSpeedArray[i] = spheres[i].z_speed;
            xSpeedArray[i] = spheres[i].x_speed;
        }

        MoveSpheresJob moveSphereJob = new MoveSpheresJob
        {
            deltaTime = Time.deltaTime,
            boundaries = boundaries,
            z_speed_array = zSpeedArray,
            x_speed_array = xSpeedArray,
            random_dir = Random.Range(-2, 2)
        };

        JobHandle jobHandle = moveSphereJob.Schedule(transformAccessArray);
        jobHandle.Complete();

        for (int i = 0; i < spheres.Count; i++)
        {
            spheres[i].z_speed = zSpeedArray[i];
            spheres[i].x_speed = xSpeedArray[i];
        }

        FindClosest();

        transformAccessArray.Dispose();
        zSpeedArray.Dispose();
        xSpeedArray.Dispose();
    }

    private void FindClosest()
    {
        KDTree tree = new KDTree(number_of_spheres, Allocator.TempJob);
        for (int i = 0; i < spheres.Count; i++)
        {
            tree.AddEntry(i, spheres[i].sphere.transform.position);
        }
        JobHandle build_tree = tree.BuildTree(spheres.Count);
        build_tree.Complete();

        NativeArray<Neighbour> neighbours = new NativeArray<Neighbour>(spheres.Count, Allocator.TempJob);

        for (int i = 0; i < spheres.Count; i++)
        {
            int id = tree.GetEntriesInRange(spheres[i].sphere.transform.position, tree.m_MaxDepth, ref neighbours);
            int id_index = id - 1;
            float dist = (spheres[i].sphere.transform.position - spheres[id_index].sphere.transform.position).magnitude;

            if (dist < 3) Debug.DrawLine(spheres[i].sphere.transform.position, spheres[id_index].sphere.transform.position, Color.red);
            if (dist < 0.8f && i != id_index)
            {
                spheres[i].sphere.GetComponent<Renderer>().material.color = Color.red;
                spheres[id_index].sphere.GetComponent<Renderer>().material.color = Color.red;
            }
        }
        neighbours.Dispose();
        tree.Dispose();
    }

    private void OnDrawGizmos()
    {
        if (draw_Gizmos)
        {
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), transform.lossyScale);
            Gizmos.matrix = rotationMatrix;
            Gizmos.color = Color.red;
            Gizmos.DrawCube(boundaries.center, boundaries.size);
        }
    }

    public class SphereInsance
    {
        public GameObject sphere;
        public float z_speed;
        public float x_speed;
    }
}

[BurstCompile]
public struct MoveSpheresJob : IJobParallelForTransform
{
    public NativeArray<float> z_speed_array;
    public NativeArray<float> x_speed_array;
    public Rect boundaries;
    [ReadOnly] public float deltaTime;
    public int random_dir;

    public void Execute(int index, TransformAccess transform)
    {
        transform.position += new Vector3(x_speed_array[index] * deltaTime, 0, z_speed_array[index] * deltaTime);

        if (transform.position.z > boundaries.yMax)
        {
            z_speed_array[index] = -Mathf.Abs(z_speed_array[index]);
            x_speed_array[index] = random_dir;
        }
        if (transform.position.z < boundaries.yMin)
        {
            z_speed_array[index] = +Mathf.Abs(z_speed_array[index]);
            x_speed_array[index] = random_dir;
        }
        if (transform.position.x > boundaries.xMax)
        {
            x_speed_array[index] = -Mathf.Abs(x_speed_array[index]);
            z_speed_array[index] = random_dir;
        }
        if (transform.position.x < boundaries.xMin)
        {
            x_speed_array[index] = +Mathf.Abs(x_speed_array[index]);
            z_speed_array[index] = random_dir;
        }
    }
}