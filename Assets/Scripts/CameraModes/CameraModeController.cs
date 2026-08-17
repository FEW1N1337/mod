using UnityEngine;
using DreamCar.Car;
using DreamCar.Vehicle;

namespace DreamCar.CameraModes
{
    // Chase / Hood / Bumper / Interior / Free-look / Cinematic mod geçişi.
    public class CameraModeController : MonoBehaviour
    {
        public enum Mode { Chase, Hood, Bumper, Interior, Free, Cinematic }
        public Mode current = Mode.Chase;

        public Transform target;
        public Transform hoodAnchor;
        public Transform bumperAnchor;
        public Transform interiorAnchor;
        public InteriorCamera interior;
        public CarCameraFollow follow;
        public KeyCode cycleKey = KeyCode.V;

        public Vector3 chaseOffset = new Vector3(0f, 3f, -6f);
        public float freeRotSpeed = 90f;
        public float cinematicRadius = 8f;
        public float cinematicHeight = 3f;
        public float cinematicSpeed = 0.3f;

        float _cineT;

        void Update() { if (Input.GetKeyDown(cycleKey)) Cycle(); }

        void LateUpdate()
        {
            if (!target || !follow) return;
            follow.target = target;
            if (interior) interior.SetActive(current == Mode.Interior);

            switch (current)
            {
                case Mode.Chase:
                    follow.enabled = true;
                    follow.offset = chaseOffset;
                    break;
                case Mode.Hood:
                    follow.enabled = false;
                    if (hoodAnchor) transform.SetPositionAndRotation(hoodAnchor.position, hoodAnchor.rotation);
                    break;
                case Mode.Bumper:
                    follow.enabled = false;
                    if (bumperAnchor) transform.SetPositionAndRotation(bumperAnchor.position, bumperAnchor.rotation);
                    break;
                case Mode.Interior:
                    follow.enabled = false;
                    if (interiorAnchor) transform.SetPositionAndRotation(interiorAnchor.position, interiorAnchor.rotation);
                    break;
                case Mode.Free:
                    follow.enabled = false;
                    float mx = Input.GetAxis("Mouse X") * freeRotSpeed * Time.deltaTime;
                    transform.RotateAround(target.position, Vector3.up, mx);
                    break;
                case Mode.Cinematic:
                    follow.enabled = false;
                    _cineT += Time.deltaTime * cinematicSpeed;
                    Vector3 orbit = new Vector3(Mathf.Cos(_cineT) * cinematicRadius, cinematicHeight, Mathf.Sin(_cineT) * cinematicRadius);
                    transform.position = target.position + orbit;
                    transform.LookAt(target.position + Vector3.up * 0.5f);
                    break;
            }
        }

        public void Cycle() => current = (Mode)(((int)current + 1) % 6);
    }
}
