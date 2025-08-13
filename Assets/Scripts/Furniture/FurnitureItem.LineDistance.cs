using TMPro;
using UnityEngine;

public partial class FurnitureItem
{
    public interface IUpdateWhenMove
    {
        void Update();
        void UpdateWhenCameraZoom();
    }

    public class Outline : IUpdateWhenMove
    {
        public Outline(LineRenderer lr, GameObject start, GameObject end)
        {
            lineRenderer = lr;
            this.start = start;
            this.end = end;

            DrawingTool.Instance.SetupLine(lineRenderer);
            Update();
        }

        public LineRenderer lineRenderer;
        public GameObject start;
        public GameObject end;
        public float width = 0.1f;
        private Vector3 startPosition;
        private Vector3 endPosition;
        public void Update()
        {
            startPosition = start.transform.position;
            endPosition = end.transform.position;

            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);
            
            Debug.Log($"Start Position {startPosition} End Position {endPosition}");
        }

        public void UpdateWhenCameraZoom()
        {
            lineRenderer.startWidth = FurnitureManager.Instance.ScaleByCameraZoom.Width;
            lineRenderer.endWidth = FurnitureManager.Instance.ScaleByCameraZoom.Width;
        }
    }

    public class TextDistance : IUpdateWhenMove
    {
        private Outline outline;
        private TextMeshPro tmp;

        public TextDistance(TextMeshPro tmp,Outline outline)
        {
            this.tmp = tmp;
            this.outline = outline;
        }


        public void Update()
        {
            Vector3 startPosition = outline.start.transform.position;
            Vector3 endPosition = outline.end.transform.position;
            DrawingTool.Instance.UpdateText(tmp, startPosition, endPosition, 0.3f);
        }

        public void UpdateWhenCameraZoom()
        {
        }
    }
}