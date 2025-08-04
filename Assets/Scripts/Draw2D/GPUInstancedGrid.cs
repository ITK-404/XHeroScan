using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GPUInstancedGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 0.5f;

    public int viewRange = 10;
    public Material lineMaterial;
    public Mesh lineMesh;

    private Camera cam;
    private List<Matrix4x4> normalMatrices;
    private List<Matrix4x4> thickMatrices;
    private MaterialPropertyBlock normalPropertyBlock;
    private MaterialPropertyBlock thickPropertyBlock;

    void Start()
    {
        cam = Camera.main;
        normalPropertyBlock = new MaterialPropertyBlock();
        thickPropertyBlock = new MaterialPropertyBlock();
        // Create a simple line mesh if none provided
        if (lineMesh == null)
        {
            lineMesh = CreateLineMesh();
        }

        // propertyBlock.SetColor("_BaseColor",Color.green);
        normalPropertyBlock.SetColor("_Color", new Color(0.5f, 0.5f, 0.5f, 1f));
        thickPropertyBlock.SetColor("_Color", new Color(0, 0, 0, 1f));

        Vector3 camPos = cam.transform.position;
        minX = -viewRange / 2;
        maxX = viewRange / 2;
        minZ = -viewRange / 2;
        maxZ = viewRange / 2;

        int lineCount = (maxX - minX + 1) * (maxZ - minZ + 1) * 2;
        // if (normalMatrices == null || normalMatrices.Length != lineCount)
        // {
        //     normalMatrices = new Matrix4x4[lineCount / 2];
        // }
        //
        // if (thickMatrices == null || thickMatrices.Length != lineCount)
        // {
        //     thickMatrices = new Matrix4x4[lineCount / 2];
        // }
        thickMatrices = new List<Matrix4x4>();
        normalMatrices = new List<Matrix4x4>();
        Debug.Log("Line Count: " + lineCount);
        Debug.Log("thick matrices Count: " + thickMatrices.Count);
        Debug.Log("normal matrices Count: " + normalMatrices.Count);
    }

    private int minX, maxX;
    private int minZ, maxZ;

    void Update()
    {
        RenderGridWithInstancing();
    }

    [SerializeField] private int DEFAULT_TILE_PER_BLOCK = 5;
    private const int STEP_SIZE = 10;
    private const int MAX_LIMIT = 50;

    private int previousSize = 0;
    private int limitSize = 1;

    private void RenderGridWithInstancing()
    {
        int normalIndex = 0;
        int thickIndex = 0;

        int verticalCounting = 0;
        int horizontalCounting = 0;
        // Generate matrices for all visible lines
        int level = Mathf.FloorToInt(cam.orthographicSize / STEP_SIZE);
        // tăng theo số mũ
        limitSize = Mathf.Clamp(level * DEFAULT_TILE_PER_BLOCK, 1, MAX_LIMIT);

        if (previousSize != limitSize)
        {
            previousSize = limitSize;

            normalMatrices.Clear();
            thickMatrices.Clear();


            for (int z = minZ; z <= maxZ; z++)
            {
                verticalCounting = 0;
                for (int x = minX; x <= maxX; x++)
                {
                    if (horizontalCounting % limitSize == 0)
                    {
                        Vector3 hPos = new Vector3(x * cellSize + cellSize * 0.5f, 0, z * cellSize);
                        normalMatrices.Add(Matrix4x4.TRS(hPos, Quaternion.identity, new Vector3(cellSize, 1, 0.02f)));
                        // normalMatrices[normalIndex++] =
                        //     Matrix4x4.TRS(hPos, Quaternion.identity, new Vector3(cellSize, 1, 0.02f));
                        Debug.DrawLine(hPos - Vector3.right * (0.5f * cellSize),
                            hPos + Vector3.right * (0.5f * cellSize),
                            Color.red);

                        // if (horizontalCounting == limitSize)
                        // {
                        //     horizontalCounting = 0;
                        // }
                    }


                    if (verticalCounting % limitSize == 0)
                    {
                        Vector3 vPos = new Vector3(x * cellSize, 0, z * cellSize + cellSize * 0.5f);

                        normalMatrices.Add(Matrix4x4.TRS(vPos, Quaternion.Euler(0, 90, 0),
                            new Vector3(cellSize, 1, 0.02f)));

                        // thickMatrices[thickIndex++] =
                        //     Matrix4x4.TRS(vPos, Quaternion.Euler(0, 90, 0), new Vector3(cellSize, 1, 0.02f));
                        Debug.DrawLine(vPos - Vector3.forward * (0.5f * cellSize),
                            vPos + Vector3.forward * (0.5f * cellSize),
                            Color.red);
                    }

                    verticalCounting++;
                }

                horizontalCounting++;
            }
        }


        // Render all lines in batches
        RenderInstanced(normalMatrices.ToArray(), thickPropertyBlock);
        RenderInstanced(thickMatrices.ToArray(), thickPropertyBlock);
        // Debug.Log($"Othographics size" + cam.orthographicSize);
    }

    private void RenderInstanced(Matrix4x4[] matrices, MaterialPropertyBlock block)
    {
        int batchSize = 1023; // Unity's limit for Graphics.DrawMeshInstanced
        for (int i = 0; i < matrices.Length; i += batchSize)
        {
            int count = Mathf.Min(batchSize, matrices.Length - i);
            Matrix4x4[] batch = new Matrix4x4[count];
            System.Array.Copy(matrices, i, batch, 0, count);

            Graphics.DrawMeshInstanced(lineMesh, 0, lineMaterial, batch, count, block, ShadowCastingMode.Off, false);
        }
    }


    private Mesh CreateLineMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0, 0),
            new Vector3(0.5f, 0, 0)
        };
        mesh.SetIndices(new int[] { 0, 1 }, MeshTopology.Lines, 0);
        return mesh;
    }

    private Mesh CreateQuadLineMesh()
    {
        Mesh mesh = new Mesh();

        float width = 0.02f; // Độ dày của line
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, -width / 2, 0),
            new Vector3(-0.5f, width / 2, 0),
            new Vector3(0.5f, width / 2, 0),
            new Vector3(0.5f, -width / 2, 0)
        };

        int[] triangles = new int[] { 0, 1, 2, 0, 2, 3 };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }
}