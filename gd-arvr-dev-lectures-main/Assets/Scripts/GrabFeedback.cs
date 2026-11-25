using UnityEngine;

public class GrabFeedback : MonoBehaviour
{
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void Grabbed()
    {
        transform.localScale = originalScale * 1.08f;
    }

    public void Released()
    {
        transform.localScale = originalScale;
    }
}