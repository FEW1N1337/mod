using UnityEngine;

namespace DreamCar.Car
{
    public class CarCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 3f, -6f);
        public float positionLerp = 8f;
        public float rotationLerp = 5f;
        public float lookAhead = 2f;

        void LateUpdate()
        {
            if (!target) return;

            Vector3 desiredPos = target.position + target.rotation * offset;
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * positionLerp);

            Quaternion desiredRot = Quaternion.LookRotation(
                (target.position + target.forward * lookAhead) - transform.position,
                Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * rotationLerp);
        }
    }
}
