using UnityEngine;

public class ObjectResizer : MonoBehaviour
{
    public Renderer renderer;

    public void Resize()
    {
        return;
        if (renderer is SpriteRenderer sprite)
        {
            Resize2D(sprite);
        }
        else if (renderer is MeshRenderer mesh)
        {
            Resize3D(mesh);
        }
    }

    private void Resize3D(MeshRenderer mesh)
    {
        float x = 1 / renderer.bounds.size.x;
        float y = 1 / renderer.bounds.size.y;
        float z = 1 / renderer.bounds.size.z;

        renderer.transform.localScale = new Vector3(x, y, z);
    }

    private void Resize2D(SpriteRenderer model2D)
    {
        float x = 1 / model2D.sprite.bounds.size.x;
        float y = 1 / model2D.sprite.bounds.size.y;
        model2D.transform.localScale = new Vector3(x, y, 1);
    }
}