using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GPUInstancedGrid : MonoBehaviour
{
    public static event Action<int> OnChangedLimitSize;

    [Header("Grid Settings")]
    public float cellSize = 0.5f;

    public int viewRange = 10;
    public Material lineMaterial;
    public Mesh lineMesh;

    private Camera cam;
    private List<Matrix4x4> normalMatrices;
    private List<Matrix4x4> thickMatrices;

    private Matrix4x4[] normalMatricesArray;
    private Matrix4x4[] thickMatricesArray;
    private int normalCount;
    private int thickCount;

    private MaterialPropertyBlock normalPropertyBlock;
    private MaterialPropertyBlock thickPropertyBlock;

    private HashSet<int> squareOfFive;

    private int minX, maxX;
    private int minZ, maxZ;

    private const int DEFAULT_TILE_PER_BLOCK = 5;
    private const int STEP_SIZE = 10;
    private const int MAX_LIMIT = 50;

    private int previousSize = 0;
    private int limitSize = 1;

    void Start()
    {
        cam = Camera.main;
        normalPropertyBlock = new MaterialPropertyBlock();
        thickPropertyBlock = new MaterialPropertyBlock();
        if (lineMesh == null)
        {
            lineMesh = CreateLineMesh();
        }

        normalPropertyBlock.SetColor("_Color", new Color(0.1f, 0.1f, 0.1f, .4f));
        thickPropertyBlock.SetColor("_Color", new Color(1f, 1f, 1f, .1f));

        minX = -viewRange / 2;
        maxX = viewRange / 2;
        minZ = -viewRange / 2;
        maxZ = viewRange / 2;

        int lineCount = (maxX - minX + 1) * (maxZ - minZ + 1) * 2;

        thickMatrices = new List<Matrix4x4>();
        normalMatrices = new List<Matrix4x4>();

        squareOfFive = new HashSet<int>()
        {
            1, 5, 25, 125, 625
        };
        InitializeArrays();
    }

    private void InitializeArrays()
    {
        int count = (viewRange + 1) * (viewRange + 1) * 2;
        normalMatricesArray = new Matrix4x4[count];
        thickMatricesArray = new Matrix4x4[count];

        batchPool = new Matrix4x4[poolSize][];
        batchPoolInUse = new bool[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            batchPool[i] = new Matrix4x4[BATCH_SIZE];
            batchPoolInUse[i] = false; // Initially all arrays are free
        }
    }

    void Update()
    {
        RenderGridWithInstancing();
        RenderGrid();
    }

    private void CalculateVisibleBounds()
    {
        // Tính toán bounds dựa trên camera position và orthographic size
        Vector3 camPos = cam.transform.position;
        float orthoSize = cam.orthographicSize;
        float aspect = cam.aspect;

        // Mở rộng bounds một chút để tránh pop-in
        float buffer = cellSize * 2;

        minX = Mathf.FloorToInt((camPos.x - orthoSize * aspect) / cellSize) - Mathf.CeilToInt(buffer / cellSize);
        maxX = Mathf.CeilToInt((camPos.x + orthoSize * aspect) / cellSize) + Mathf.CeilToInt(buffer / cellSize);
        minZ = Mathf.FloorToInt((camPos.z - orthoSize) / cellSize) - Mathf.CeilToInt(buffer / cellSize);
        maxZ = Mathf.CeilToInt((camPos.z + orthoSize) / cellSize) + Mathf.CeilToInt(buffer / cellSize);
    }

    private void RenderGridWithInstancing()
    {
        int normalIndex = 0;
        int thickIndex = 0;


        // Generate matrices for all visible lines

        int level = Mathf.FloorToInt(cam.orthographicSize / STEP_SIZE);

        int bestFitLevel = GetBestFitLevel(CalculatorLimitSize(level));

        limitSize = CalculatorLimitSize(bestFitLevel);
        int previousLevel = Mathf.Min(bestFitLevel - 1, 1);
        var previousLimitSize = CalculatorLimitSize(previousLevel);

        if (limitSize == 0) return;
        // đảm bảo limit size là kết quả của 5 mũ n (chỉ check tới n = 5)
        if (previousSize != limitSize && squareOfFive.Contains(limitSize))
        {
            OnChangedLimitSize?.Invoke(level);
            Debug.Log($"Thay đổi grid size {limitSize} {previousLimitSize}");
            // đảm bảo vẽ một lần
            previousSize = limitSize;
            DrawGrid(limitSize, previousLimitSize);
        }


        // Render all lines in batches
        // Debug.Log($"Othographics size" + cam.orthographicSize);
    }

    private Matrix4x4[][] batchPool;
    private bool[] batchPoolInUse;
    private int poolSize = 30; // Có thể adjust dựa trên needs
    private const int BATCH_SIZE = 1023;

    private int GetBestFitLevel(int target)
    {
        int bestFit = 1; // fallback nếu không có cái nào phù hợp

        foreach (int x in squareOfFive)
        {
            if (x <= target)
            {
                bestFit = x; // update kết quả nếu phù hợp
            }
            else
            {
                break; // vì mảng tăng dần → nếu x > target, thì không còn x nào phù hợp nữa
            }
        }

        return bestFit;
    }

    private void DrawGrid(int limitSize, int previousLimitSize)
    {
        normalCount = 0;
        thickCount = 0;
        normalMatrices.Clear();
        thickMatrices.Clear();

        int verticalCounting = 0;
        int horizontalCounting = 0;
        for (int z = minZ; z <= maxZ; z++)
        {
            verticalCounting = 0;
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 hPos = new Vector3(x * cellSize + cellSize * 0.5f, 0, z * cellSize);
                Vector3 vPos = new Vector3(x * cellSize, 0, z * cellSize + cellSize * 0.5f);

                var hDraw = Matrix4x4.TRS(hPos, Quaternion.identity, new Vector3(cellSize, 1, 0.02f));
                var vDraw = Matrix4x4.TRS(vPos, Quaternion.Euler(0, 90, 0), new Vector3(cellSize, 1, 0.02f));

                if (horizontalCounting % limitSize == 0)
                {
                    normalMatricesArray[normalCount++] = hDraw;
                    // normalMatrices.Add(hDraw);
                    Debug.DrawLine(hPos - Vector3.right * (0.5f * cellSize),
                        hPos + Vector3.right * (0.5f * cellSize),
                        Color.red);
                }
                else if (horizontalCounting % previousLimitSize == 0)
                {
                    thickMatricesArray[thickCount++] = hDraw;

                    // thickMatrices.Add(hDraw);
                }


                if (verticalCounting % limitSize == 0)
                {
                    normalMatricesArray[normalCount++] = vDraw;
                    // normalMatrices.Add(vDraw);
                    Debug.DrawLine(vPos - Vector3.forward * (0.5f * cellSize),
                        vPos + Vector3.forward * (0.5f * cellSize),
                        Color.red);
                }
                else if (verticalCounting % previousLimitSize == 0)
                {
                    thickMatricesArray[thickCount++] = vDraw;
                    // thickMatrices.Add(vDraw);
                }


                verticalCounting++;
            }

            horizontalCounting++;
        }
    }

    private int CalculatorLimitSize(int level)
    {
        return Mathf.Clamp(level * DEFAULT_TILE_PER_BLOCK, 1, MAX_LIMIT);
    }

    private void RenderGrid()
    {
        RenderInstanced(normalMatricesArray, normalCount, normalPropertyBlock);
        RenderInstanced(thickMatricesArray, thickCount, thickPropertyBlock);
    }

    private void RenderInstanced(Matrix4x4[] matrices, int totalCount, MaterialPropertyBlock block)
    {
        int batchSize = 1023; // Unity's limit for Graphics.DrawMeshInstanced
        int startIndex = 0;
        int batchPoolIndex = 0;
        List<int> poolInUse = new();
        for (int i = 0; i < totalCount; i += batchSize)
        {
            // find free pool
            batchPoolIndex = -1;
            for (int j = 0; j < batchPoolInUse.Length; j++)
            {
                if (batchPoolInUse[j] == false)
                {
                    batchPoolIndex = j;
                    batchPoolInUse[j] = true;
                    poolInUse.Add(j);
                    break;
                }
            }


            int count = Mathf.Min(batchSize, totalCount - i);
            Matrix4x4[] batch = batchPoolIndex == -1 ? new Matrix4x4[count] : batchPool[batchPoolIndex];
            System.Array.Copy(matrices, i, batch, 0, count);

            Graphics.DrawMeshInstanced(lineMesh, 0, lineMaterial, batch, count, block, ShadowCastingMode.Off, false);
        }

        // release
        foreach (var item in poolInUse)
        {
            batchPoolInUse[item] = false;
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