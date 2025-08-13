using TMPro;
using UnityEngine;

public partial class FurnitureItem
{
    public class LineDistance
    {
        public LineDistance(LineRenderer lr, TextMeshPro tmp, GameObject start, GameObject end)
        {
            lineRenderer = lr;
            textMeshPro = tmp;
            this.start = start;
            this.end = end;

            DrawingTool.Instance.SetupLine(lineRenderer);
            UpdateLine();
        }

        public void UpdateLine()
        {
            Vector3 startPosition = start.transform.position;
            Vector3 endPosition = end.transform.position;
            Debug.Log($"Start Position {startPosition} End Position {endPosition}");
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);
            DrawingTool.Instance.UpdateLine(lineRenderer, textMeshPro, startPosition, endPosition, 0.5f);
        }

        public LineRenderer lineRenderer;
        public TextMeshPro textMeshPro;
        public GameObject start;
        public GameObject end;
        public float width = 0.1f;
    }
}