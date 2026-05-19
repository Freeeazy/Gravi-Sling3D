using UnityEngine;

public class DeliveryPopupBillboard : MonoBehaviour
{
    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null)
                return;
        }

        Vector3 direction = transform.position - _cam.transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction, _cam.transform.up);
    }
}