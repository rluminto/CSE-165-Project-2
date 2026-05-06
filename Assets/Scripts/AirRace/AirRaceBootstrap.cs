using UnityEngine;

namespace AirRace
{
    public class AirRaceBootstrap : MonoBehaviour
    {
        static bool s_Bootstrapped;

        [Header("Track loading")]
        public string externalTrackPath = "";

        [Header("Environment")]
        public string machuPicchuResourcePath = "Environment/MachuPicchu/machu_picchu_2";
        public string sceneMachuPicchuObjectName = "machu_picchu_2";
        public bool disableTestFloor = true;

        [Header("Flight tuning")]
        public float maxSpeed = 42f;
        public float minSpeed = 0f;
        public float turnSmoothing = 8f;
        public float throttleSmoothing = 6f;
        public float maxTurnDegreesPerSecond = 95f;
        public float accelerationMetersPerSecondSquared = 18f;
        public float brakingMetersPerSecondSquared = 24f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureBootstrapExists()
        {
            if (s_Bootstrapped || FindAnyObjectByType<AirRaceBootstrap>() != null)
                return;

            new GameObject("AirRaceBootstrap").AddComponent<AirRaceBootstrap>();
        }

        void Awake()
        {
            if (s_Bootstrapped)
            {
                Destroy(gameObject);
                return;
            }

            s_Bootstrapped = true;
            BuildRaceRuntime();
        }

        void BuildRaceRuntime()
        {
            if (disableTestFloor)
            {
                var testFloor = GameObject.Find("TestFloor");
                if (testFloor != null)
                    testFloor.SetActive(false);
            }

            var runtimeRoot = new GameObject("Air Race Runtime");
            runtimeRoot.transform.SetParent(transform, false);

            SetupMachuPicchu(runtimeRoot.transform);

            var droneRoot = new GameObject("Drone Root");
            droneRoot.transform.SetParent(runtimeRoot.transform, false);

            var rb = droneRoot.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var trigger = droneRoot.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;

            var flight = droneRoot.AddComponent<DroneFlightController>();
            flight.maxSpeed = maxSpeed;
            flight.minSpeed = minSpeed;
            flight.turnSmoothing = turnSmoothing;
            flight.throttleSmoothing = throttleSmoothing;
            flight.maxTurnDegreesPerSecond = maxTurnDegreesPerSecond;
            flight.accelerationMetersPerSecondSquared = accelerationMetersPerSecondSquared;
            flight.brakingMetersPerSecondSquared = brakingMetersPerSecondSquared;

            var xrOrigin = FindXrOrigin();
            var mainCamera = Camera.main;
            if (xrOrigin != null)
            {
                DisableStarterLocomotion(xrOrigin);
                xrOrigin.transform.SetParent(droneRoot.transform, false);
                xrOrigin.transform.localPosition = Vector3.zero;
                xrOrigin.transform.localRotation = Quaternion.identity;
            }

            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.SetParent(droneRoot.transform, false);
            }

            var visuals = CreateDroneVisuals(droneRoot.transform);
            var cockpit = CreateCockpitVisuals(droneRoot.transform);

            var input = droneRoot.AddComponent<HandFlightInput>();
            input.Configure(xrOrigin != null ? xrOrigin.transform : droneRoot.transform, flight);

            var track = runtimeRoot.AddComponent<TrackManager>();
            track.externalTrackPath = externalTrackPath;
            track.defaultTrack = Resources.Load<TextAsset>("DefaultTrack");

            var hud = runtimeRoot.AddComponent<AirRaceHud>();
            hud.Configure(mainCamera.transform);

            var wayfinding = runtimeRoot.AddComponent<WayfindingDisplay>();
            wayfinding.Configure(droneRoot.transform, mainCamera.transform);

            var viewModes = droneRoot.AddComponent<ViewModeController>();
            viewModes.Configure(input, xrOrigin != null ? xrOrigin.transform : mainCamera.transform, visuals, cockpit);

            var race = runtimeRoot.AddComponent<RaceManager>();
            race.Configure(track, flight, wayfinding, hud, droneRoot.transform);
            flight.Configure(race);
        }

        void SetupMachuPicchu(Transform parent)
        {
            var model = GameObject.Find(sceneMachuPicchuObjectName) ?? GameObject.Find("Machu Picchu Environment");
            if (model == null)
            {
                var prefab = Resources.Load<GameObject>(machuPicchuResourcePath);
                if (prefab == null)
                {
                    Debug.LogError($"AirRace: Could not find a scene Machu Picchu object and could not load Resources/{machuPicchuResourcePath}.");
                    return;
                }

                model = Instantiate(prefab, parent);
            }

            model.name = "Machu Picchu Environment";
            if (model.transform.parent == null)
                model.transform.SetParent(parent, true);

            model.isStatic = true;

            foreach (var meshFilter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (meshFilter.sharedMesh == null)
                    continue;

                var collider = meshFilter.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = meshFilter.gameObject.AddComponent<MeshCollider>();

                collider.convex = false;
                collider.isTrigger = false;
                if (collider.sharedMesh == null)
                    collider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        static GameObject FindXrOrigin()
        {
            var named = GameObject.Find("XR Origin (XR Rig)");
            if (named != null)
                return named;

            var camera = Camera.main;
            if (camera == null)
                return null;

            var root = camera.transform.root;
            return root != null ? root.gameObject : camera.gameObject;
        }

        static void DisableStarterLocomotion(GameObject xrOrigin)
        {
            foreach (var behaviour in xrOrigin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var type = behaviour.GetType();
                var fullName = type.FullName ?? type.Name;
                if (fullName.Contains("Locomotion") ||
                    fullName.Contains("Teleport") ||
                    fullName.Contains("ContinuousMove") ||
                    fullName.Contains("ContinuousTurn") ||
                    fullName.Contains("SnapTurn") ||
                    fullName.Contains("GrabMove") ||
                    fullName.Contains("TwoHandedGrabMove") ||
                    type.Name.Contains("MoveProvider") ||
                    type.Name.Contains("TurnProvider") ||
                    type.Name.Contains("ControllerInputActionManager"))
                {
                    behaviour.enabled = false;
                }
            }

            foreach (var controller in xrOrigin.GetComponentsInChildren<CharacterController>(true))
                controller.enabled = false;
        }

        static GameObject CreateDroneVisuals(Transform parent)
        {
            var root = new GameObject("Drone Visuals");
            root.transform.SetParent(parent, false);

            var bodyMaterial = MakeMaterial("Drone Blue", new Color(0.1f, 0.45f, 0.95f, 1f));
            var darkMaterial = MakeMaterial("Drone Dark", new Color(0.04f, 0.06f, 0.08f, 1f));

            var body = CreatePrimitive("Drone Body", PrimitiveType.Cube, root.transform, bodyMaterial);
            body.transform.localScale = new Vector3(1.4f, 0.35f, 2.2f);

            var nose = CreatePrimitive("Drone Nose", PrimitiveType.Sphere, root.transform, bodyMaterial);
            nose.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            nose.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);

            for (var i = 0; i < 4; i++)
            {
                var x = i < 2 ? -1.1f : 1.1f;
                var z = i % 2 == 0 ? -0.8f : 0.8f;
                var arm = CreatePrimitive($"Drone Arm {i + 1}", PrimitiveType.Cube, root.transform, darkMaterial);
                arm.transform.localPosition = new Vector3(x * 0.5f, 0f, z * 0.5f);
                arm.transform.localScale = new Vector3(1.6f, 0.08f, 0.08f);
                arm.transform.localRotation = Quaternion.Euler(0f, i % 2 == 0 ? 35f : -35f, 0f);

                var rotor = CreatePrimitive($"Rotor {i + 1}", PrimitiveType.Cylinder, root.transform, darkMaterial);
                rotor.transform.localPosition = new Vector3(x, 0.06f, z);
                rotor.transform.localScale = new Vector3(0.55f, 0.03f, 0.55f);
            }

            root.SetActive(false);
            return root;
        }

        static GameObject CreateCockpitVisuals(Transform parent)
        {
            var root = new GameObject("Virtual Cockpit");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 1.05f, 0.45f);

            var frameMaterial = MakeMaterial("Cockpit Frame", new Color(0.03f, 0.04f, 0.05f, 1f));
            var glassMaterial = MakeTransparentMaterial("Cockpit Glass", new Color(0.25f, 0.8f, 1f, 0.22f));

            var dashboard = CreatePrimitive("Dashboard", PrimitiveType.Cube, root.transform, frameMaterial);
            dashboard.transform.localPosition = new Vector3(0f, -0.35f, 0.55f);
            dashboard.transform.localScale = new Vector3(1.6f, 0.12f, 0.35f);

            var glass = CreatePrimitive("Windshield", PrimitiveType.Cube, root.transform, glassMaterial);
            glass.transform.localPosition = new Vector3(0f, 0.05f, 0.72f);
            glass.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            glass.transform.localScale = new Vector3(1.65f, 0.04f, 0.95f);

            var leftRail = CreatePrimitive("Left Cockpit Rail", PrimitiveType.Cube, root.transform, frameMaterial);
            leftRail.transform.localPosition = new Vector3(-0.85f, 0f, 0.35f);
            leftRail.transform.localScale = new Vector3(0.08f, 0.08f, 1.3f);

            var rightRail = CreatePrimitive("Right Cockpit Rail", PrimitiveType.Cube, root.transform, frameMaterial);
            rightRail.transform.localPosition = new Vector3(0.85f, 0f, 0.35f);
            rightRail.transform.localScale = new Vector3(0.08f, 0.08f, 1.3f);

            root.SetActive(false);
            return root;
        }

        static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return obj;
        }

        static Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }

        static Material MakeTransparentMaterial(string name, Color color)
        {
            var material = MakeMaterial(name, color);
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }
    }
}
