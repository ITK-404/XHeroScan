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
            
            // Debug.Log($"Start Position {startPosition} End Position {endPosition}");
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

    public class FurnitureVisuals
    {
        public FurnitureVisuals(FurnitureItem furnitureItem)
        {
            this.furnitureItem = furnitureItem;
        }
        
        private FurnitureItem furnitureItem;
        private float currentRotation => furnitureItem.currentRotation;
        
        public void Recalculator(Transform point, CheckpointType type, Bounds bounds, Vector3 offset)
        {
            Vector3 newPosition = point.transform.localPosition;
            float xExtend = Mathf.Max(bounds.extents.x, furnitureItem.minSizeX);
            float yExtend = Mathf.Max(bounds.extents.z, furnitureItem.minSizeZ);

            if (type == CheckpointType.Left || type == CheckpointType.TopLeft || type == CheckpointType.BottomLeft)
            {
                offset += new Vector3(-xExtend, 0, 0);
            }

            // Right
            if (type == CheckpointType.Right || type == CheckpointType.TopRight || type == CheckpointType.BottomRight)
            {
                offset += new Vector3(xExtend, 0, 0);
            }

            // Top (positive Z in unrotated local)
            if (type == CheckpointType.Top || type == CheckpointType.TopLeft || type == CheckpointType.TopRight)
            {
                offset += new Vector3(0, 0, yExtend);
            }

            // Bottom (negative Z in unrotated local)
            if (type == CheckpointType.Bottom || type == CheckpointType.BottomLeft || type == CheckpointType.BottomRight)
            {
                offset += new Vector3(0, 0, -yExtend);
            }

            offset = Quaternion.Euler(0, currentRotation, 0) * offset;
            newPosition = bounds.center + offset;
            point.transform.localPosition = newPosition;
        }

        public Vector3 ClampPointToBounds(Vector3 dragLocalUnrot, CheckpointType type)
        {
            // Giới hạn vị trí kéo điểm đến các giới hạn của đối tượng
            // dragLocalUnrot là vị trí hiện tại của điểm kéo, type là loại điểm
            // kéo (Left, Right, Top, Bottom, v.v.)
            // Left
            float minX = -furnitureItem.minSizeX;
            float maxX = furnitureItem.minSizeX;
            float minZ = -furnitureItem.minSizeZ;
            float maxZ = furnitureItem.minSizeZ;
            
            if (type == CheckpointType.Left || type == CheckpointType.TopLeft || type == CheckpointType.BottomLeft)
            {
                if (dragLocalUnrot.x > -LIMIT_SIZE) dragLocalUnrot.x = -LIMIT_SIZE;
            }

            // Right
            if (type == CheckpointType.Right || type == CheckpointType.TopRight || type == CheckpointType.BottomRight)
            {
                if (dragLocalUnrot.x < LIMIT_SIZE) dragLocalUnrot.x = LIMIT_SIZE;
            }

            // Top (positive Z in unrotated local)
            if (type == CheckpointType.Top || type == CheckpointType.TopLeft || type == CheckpointType.TopRight)
            {
                if (dragLocalUnrot.z < LIMIT_SIZE) dragLocalUnrot.z = LIMIT_SIZE;
            }

            // Bottom (negative Z in unrotated local)
            if (type == CheckpointType.Bottom || type == CheckpointType.BottomLeft || type == CheckpointType.BottomRight)
            {
                if (dragLocalUnrot.z > -LIMIT_SIZE) dragLocalUnrot.z = -LIMIT_SIZE;
            }

            return dragLocalUnrot;
        }

        public Vector3 ClampSizeToBounds(Vector3 sizeLocal,ResizeAxis type, Vector3 dragLocalUnrot, Vector3 anchorLocalUnrot)
        {
            // Giới hạn kích thước tối thiểu
            // Nếu kích thước nhỏ hơn LIMIT_SIZE, đặt lại về LIMIT_SIZE
            // dragLocalUnrot là vị trí hiện tại của điểm kéo, anchorLocalUnrot là vị trí neo (anchor) của điểm kéo
            // sizeLocal là kích thước hiện tại của đối tượng
            switch (type)
            {
                case ResizeAxis.X:
                    sizeLocal.x = Mathf.Abs(dragLocalUnrot.x - anchorLocalUnrot.x);
                    break;
                case ResizeAxis.Z:
                    sizeLocal.z = Mathf.Abs(dragLocalUnrot.z - anchorLocalUnrot.z);
                    break;
                case ResizeAxis.XZ:
                    sizeLocal.x = Mathf.Abs(dragLocalUnrot.x - anchorLocalUnrot.x);
                    sizeLocal.z = Mathf.Abs(dragLocalUnrot.z - anchorLocalUnrot.z);
                    break;
            }

            return sizeLocal;
        }
    }
}