using System.Collections;
using UnityEngine;

public class ItemIndicator : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Vector3 itemPosition = Vector3.zero;

    [SerializeField] private float rotateSpeed;
    [SerializeField] private float rotateAngle;
    [SerializeField] private float radius;

    private int dotCount = 0;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        dotCount = lineRenderer.positionCount;
        ArrangeLine();
    }

    private void OnEnable()
    {
        itemPosition = transform.parent.position;
        ArrangeLine();

    }

    // Update is called once per frame
    void Update()
    {

        transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);

    }

    private void ArrangeLine()
    {
        float dotAngle = rotateAngle / dotCount;

        float y = itemPosition.y;

        for(int i = 0; i <  dotCount; i++)
        {
            float angle = i * dotAngle * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 nextPosition  = new Vector3(x, y, z);

            lineRenderer.SetPosition(i, nextPosition);
        }
    }

}
