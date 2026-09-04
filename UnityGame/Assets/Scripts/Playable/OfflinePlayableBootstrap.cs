using UnityEngine;

namespace DreamCar.Playable
{
    public sealed class OfflinePlayableBootstrap : MonoBehaviour
    {
        [SerializeField] float trackLength = 320f;
        [SerializeField] float trackWidth = 18f;

        Car.CarController car;
        Camera cam;
        Vector3 spawn;
        GUIStyle label;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            BuildWorld();
            BuildCar();
            BuildCamera();
            BuildLighting();
        }

        void Update()
        {
            if (!car) return;
            float throttle = 0f, brake = 0f, steer = 0f;
            bool handbrake = false;
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) throttle = 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) brake = 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) steer -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) steer += 1f;
                handbrake = k.spaceKey.isPressed;
                if (k.rKey.wasPressedThisFrame) ResetCar();
            }
            car.Move(throttle, brake, steer, handbrake);
        }

        void LateUpdate()
        {
            if (!cam || !car) return;
            Vector3 target = car.transform.position + Vector3.up * 1.1f;
            Vector3 desired = car.transform.position - car.transform.forward * 7.5f + Vector3.up * 4.2f;
            cam.transform.position = Vector3.Lerp(cam.transform.position, desired, 1f - Mathf.Exp(-8f * Time.deltaTime));
            cam.transform.LookAt(target);
        }

        void BuildWorld()
        {
            Cube("Road", new Vector3(0, -0.15f, trackLength * .5f), new Vector3(trackWidth, .3f, trackLength), new Color(.12f,.12f,.14f));
            Cube("Ground", new Vector3(0, -.45f, trackLength * .5f), new Vector3(180,.4f,trackLength+100), new Color(.18f,.22f,.18f));
            Cube("LeftBarrier", new Vector3(-trackWidth*.5f-.6f,.5f,trackLength*.5f), new Vector3(.6f,1.2f,trackLength), new Color(.65f,.65f,.68f));
            Cube("RightBarrier", new Vector3(trackWidth*.5f+.6f,.5f,trackLength*.5f), new Vector3(.6f,1.2f,trackLength), new Color(.65f,.65f,.68f));
            for (int i=0;i<18;i++)
            {
                float z=12+i*17, side=i%2==0?-1f:1f;
                Cube("Building_"+i, new Vector3(side*(24+(i%3)*5),3,z), new Vector3(8+(i%2)*4,6+(i%4)*2,10), new Color(.28f+(i%3)*.04f,.30f,.34f));
            }
            Cube("StartLine", new Vector3(0,.02f,8), new Vector3(trackWidth,.03f,1.2f), new Color(.95f,.95f,.95f));
            spawn = new Vector3(0,.7f,14);
        }

        void BuildCar()
        {
            var root = new GameObject("PlayerCar");
            root.transform.position = spawn;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name="Body"; body.transform.SetParent(root.transform,false); body.transform.localPosition=new Vector3(0,.45f,0); body.transform.localScale=new Vector3(1.8f,.65f,4); Material(body,new Color(.08f,.32f,.78f));
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name="Cabin"; roof.transform.SetParent(root.transform,false); roof.transform.localPosition=new Vector3(0,.92f,-.15f); roof.transform.localScale=new Vector3(1.45f,.48f,1.9f); Material(roof,new Color(.07f,.09f,.12f));
            var rb=root.AddComponent<Rigidbody>(); rb.mass=1200; rb.linearDamping=.04f; rb.angularDamping=.45f; rb.centerOfMass=new Vector3(0,-.35f,0); rb.interpolation=RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            car=root.AddComponent<Car.CarController>(); car.maxMotorTorque=1750; car.maxBrakeTorque=3600; car.maxSteeringAngle=31; car.topSpeedKmh=190; car.downForce=65; car.steerFalloffSpeedKmh=135; car.minSteeringFactor=.38f;
            car.axles=new Car.CarController.AxleInfo[] { Axle(root.transform,"Front",1.32f,true,false), Axle(root.transform,"Rear",-1.32f,false,true) };
        }

        Car.CarController.AxleInfo Axle(Transform root,string name,float z,bool steering,bool motor)
        {
            var a=new Car.CarController.AxleInfo { steering=steering, motor=motor };
            a.leftWheel=Wheel(root,name+"_L",new Vector3(-.92f,0,z)); a.rightWheel=Wheel(root,name+"_R",new Vector3(.92f,0,z));
            a.leftMesh=WheelMesh(root,name+"_VisualL",a.leftWheel.transform.localPosition); a.rightMesh=WheelMesh(root,name+"_VisualR",a.rightWheel.transform.localPosition); return a;
        }

        WheelCollider Wheel(Transform root,string name,Vector3 pos)
        {
            var go=new GameObject(name); go.transform.SetParent(root,false); go.transform.localPosition=pos;
            var w=go.AddComponent<WheelCollider>(); w.radius=.36f; w.suspensionDistance=.22f; w.mass=28; w.wheelDampingRate=1.4f;
            var s=w.suspensionSpring; s.spring=36000; s.damper=5200; s.targetPosition=.5f; w.suspensionSpring=s; return w;
        }

        Transform WheelMesh(Transform root,string name,Vector3 pos)
        {
            var w=GameObject.CreatePrimitive(PrimitiveType.Cylinder); w.name=name; w.transform.SetParent(root,false); w.transform.localPosition=pos; w.transform.localRotation=Quaternion.Euler(0,0,90); w.transform.localScale=new Vector3(.36f,.16f,.36f); Material(w,new Color(.025f,.025f,.025f)); return w.transform;
        }

        void BuildCamera()
        {
            var go=new GameObject("Main Camera"); go.tag="MainCamera"; cam=go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); cam.fieldOfView=68; cam.transform.position=spawn-Vector3.forward*7.5f+Vector3.up*4.2f;
        }

        void BuildLighting()
        {
            var go=new GameObject("Sun"); var light=go.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.15f; go.transform.rotation=Quaternion.Euler(50,-30,0); RenderSettings.ambientIntensity=.8f;
        }

        void ResetCar()
        {
            var rb=car.GetComponent<Rigidbody>(); car.transform.SetPositionAndRotation(spawn,Quaternion.identity); rb.linearVelocity=Vector3.zero; rb.angularVelocity=Vector3.zero;
        }

        static GameObject Cube(string name,Vector3 pos,Vector3 scale,Color color)
        { var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=pos; go.transform.localScale=scale; Material(go,color); return go; }

        static void Material(GameObject go,Color color)
        {
            var r=go.GetComponent<Renderer>(); if(!r)return; var shader=Shader.Find("Universal Render Pipeline/Lit"); if(!shader)shader=Shader.Find("Standard"); var m=new Material(shader); m.color=color; r.sharedMaterial=m;
        }

        void OnGUI()
        {
            if(label==null) label=new GUIStyle(GUI.skin.label){fontSize=20};
            GUI.Label(new Rect(20,20,700,30),"DREAMCAR — LOCAL PLAYABLE MVP",label);
            GUI.Label(new Rect(20,50,900,30),"W/S veya ↑/↓: Gaz/Fren   A/D veya ←/→: Direksiyon   SPACE: El freni   R: Reset",label);
            if(car) GUI.Label(new Rect(20,85,300,30),$"Hız: {car.SpeedKmh:0} km/h",label);
        }
    }
}
