namespace BlackRece.EmitProjectile
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Google.XR.ARCoreExtensions;
    using UnityEngine;
    using UnityEngine.Android;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.EnhancedTouch;
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;
    using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
    using LaSARTag.Geospatial;
    using HUD;

    public struct BaseAnchor
    {
        //public ARGe anchor;
        public GameObject gameObject;
    }

    public struct InfoMessages
    {
        /// <summary>
        /// Help message shows while localizing.
        /// </summary>
        public const string _localizingMessage = "Localizing your device to set anchor.";

        /// <summary>
        /// Help message shows while initializing Geospatial functionalities.
        /// </summary>
        public const string _localizationInitializingMessage =
            "Initializing Geospatial functionalities.";

        /// <summary>
        /// Help message shows when <see cref="AREarthManager.EarthTrackingState"/> is not tracking
        /// or the pose accuracies are beyond thresholds.
        /// </summary>
        public const string _localizationInstructionMessage =
            "Point your camera at buildings, stores, and signs near you.";

        /// <summary>
        /// Help message shows when location fails or hits timeout.
        /// </summary>
        public const string _localizationFailureMessage =
            "Localization not possible.\n" +
            "Close and open the app to restart the session.";

        /// <summary>
        /// Help message shows when location success.
        /// </summary>
        public const string _localizationSuccessMessage = "Localization completed.";

        /// <summary>
        /// Help message shows when resolving takes too long.
        /// </summary>
        public const string _resolvingTimeoutMessage =
            "Still resolving the terrain anchor.\n" +
            "Please make sure you're in an area that has VPS coverage.";

        /// <summary>
        /// The key name used in PlayerPrefs which indicates whether the privacy prompt has
        /// displayed at least one time.
        /// </summary>
        public const string _hasDisplayedPrivacyPromptKey = "HasDisplayedGeospatialPrivacyPrompt";

        /// <summary>
        /// The key name used in PlayerPrefs which stores geospatial anchor history data.
        /// The earliest one will be deleted once it hits storage limit.
        /// </summary>
        public const string _persistentGeospatialAnchorsStorageKey = "PersistentGeospatialAnchors";
    }

    [RequireComponent(
        typeof(ARRaycastManager),
        typeof(ARPlaneManager),
        typeof(ARAnchorManager)
    )]
    public class EmitObject : MonoBehaviour
    {
        [SerializeField] private bool _DEBUG;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private GameObject _basePrefab;

        [SerializeField] private GameObject GeospatialPrefab;
        [SerializeField] private GameObject TerrainPrefab;

        [SerializeField] private InputActionReference _centerRotation;
        [SerializeField] private InputActionReference _centerPosition;

        [SerializeField] private float _fFingerDelayMax = 1f;

        public delegate void OnFire();

        public delegate void OnPlace();

        public event OnFire EmitObjectEvent;
        public event OnPlace PlaceObjectEvent;

        private Camera _camera;

        private Vector3 _vPos;
        private Vector3 _vDir;

        private ARRaycastManager _raycastManager;
        private ARAnchorManager _anchorManager;
        private AREarthManager _earthManager;

        private List<ARRaycastHit> _hits;
        private List<ARAnchor> _anchors;

        private ProjectilePooler.ProjectilePooler _pooler;
        private GameObject _ball;

        private bool _bIsFingerDown;
        private float _fFingerDelay;

        private GeospatialAnchorHistoryCollection _historyCollection = null;
        private List<GameObject> _anchorObjects = new List<GameObject>();
        private bool _isLocalizing;

        private const float _maxLifeTime = 10f;
        private Dictionary<GameObject, float> _testObjects = new Dictionary<GameObject, float>();

        private bool _waitingForLocationService = false;
        private IEnumerator _startLocationService;
        private IEnumerator _asyncCheck;
        private bool _isInARView;
        private bool _isQuitting;
        public bool _isEmittingEnabled;

        private void Awake()
        {
            _camera = Camera.main;

            if (_centerPosition != null)
                _centerPosition.action.Enable();

            if (_centerRotation)
                _centerRotation.action.Enable();

            _hits = new List<ARRaycastHit>();
            _raycastManager = GetComponent<ARRaycastManager>();
            _anchorManager = GetComponent<ARAnchorManager>();
            _earthManager = GetComponent<AREarthManager>();

            _pooler = GetComponent<ProjectilePooler.ProjectilePooler>();
            _bIsFingerDown = false;
            _fFingerDelay = 0f;
            _isEmittingEnabled = false;

            Application.targetFrameRate = 60;
        }

        private void OnEnable()
        {
            if (_DEBUG)
                TouchSimulation.Enable();

            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += OnFingerDown;
            Touch.onFingerUp += OnFingerUp;
            // TODO: use TouchPhase instead of FingerUp/Down events

            _isLocalizing = true;
            SwitchToARView(true);
        }

        private void OnDisable()
        {
            if (_DEBUG)
                TouchSimulation.Disable();

            EnhancedTouchSupport.Disable();
            Touch.onFingerDown -= OnFingerDown;
            Touch.onFingerUp -= OnFingerUp;
        }

        private void OnFingerDown(Finger finger)
        {
            if (_DEBUG)
                Debug.Log("Finger down");

            if (!_isEmittingEnabled) return;

            _bIsFingerDown = true;

            if (finger.index != 0)
                return;

            _pooler
                .GetGameObject()
                .GetComponent<Projectile>()
                .Init(_camera.transform);

            bool bHasHit = _raycastManager.Raycast(finger.currentTouch.screenPosition, _hits);
            if (!bHasHit) return;
            foreach (ARRaycastHit hit in _hits)
            {
                Pose pose = hit.pose;
                //GameObject obj = Instantiate(_projectilePrefab, pose.position, pose.rotation);
            }

            /*
            var ray = Camera.main.ScreenPointToRay(finger.ScreenPosition);
            var hits = Physics.RaycastAll(ray);
            
            foreach(var hit in hits)
            {
                if(_DEBUG)
                    Debug.Log("Hit: " + hit.collider.name);
                
                if(hit.collider.name == "Plane")
                {
                    if(_DEBUG)
                        Debug.Log("Hit plane");
                    
                    var plane = hit.collider.GetComponent<ARPlane>();
                    if(plane != null)
                    {
                        if(_DEBUG)
                            Debug.Log("Plane is not null");
                        
                        var planeCenter = plane.center;
                        var planeNormal = plane.normal;
                        var planeRotation = Quaternion.LookRotation(planeNormal);
                        
                        var projectile = Instantiate(_projectilePrefab, planeCenter, planeRotation);
                        projectile.GetComponent<Rigidbody>().AddForce(planeNormal * 1000);
                    }
                }
            }
            */
        }

        private void OnFingerUp(Finger finger) => _bIsFingerDown = false;

        private void OnPlaceObject(Vector2 tapPosition)
        {
            if (_DEBUG)
                Debug.Log($"OnPlaceObject: {tapPosition}");

            PlaceAnchorByScreenTap(tapPosition);
        }

        private void Start()
        {
            /*
            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ball.transform.Translate(_camera.transform.forward * 2);
            */

            _basePrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _basePrefab.GetComponent<Renderer>().material.color = Color.green;

            GeospatialPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GeospatialPrefab.GetComponent<Renderer>().material.color = Color.red;

            TerrainPrefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            TerrainPrefab.GetComponent<Renderer>().material.color = Color.cyan;

            _pooler.Init(_projectilePrefab);
        }

        private void Update()
        {
            _vPos = _centerPosition != null
                ? _centerPosition.action.ReadValue<Vector3>()
                : _camera.transform.position;

            _vDir = _centerRotation != null
                ? _centerRotation.action.ReadValue<Quaternion>() * Vector3.forward
                : _camera.transform.rotation * Vector3.forward;

            InfoHUD.IsFingerDown = _bIsFingerDown;
            if (_bIsFingerDown)
            {
                _fFingerDelay += Time.deltaTime;
                if (_fFingerDelay > _fFingerDelayMax)
                {
                    /* Activate Feature */
                    InfoHUD.HasFingerTriggered = true;

                    if (PlaceObjectEvent != null)
                        OnPlaceObject(Input.GetTouch(0).position);
                }
            }
            else
            {
                _fFingerDelay = 0f;
                InfoHUD.HasFingerTriggered = false;
            }

            for (int i = 0; i < _testObjects.Count; i++)
            {
                var tObj = _testObjects.ElementAt(i);
                var lifetime = tObj.Value - Time.deltaTime;
                _testObjects[tObj.Key] = lifetime;

                if (lifetime <= 0)
                {
                    _testObjects.Remove(tObj.Key);
                    Destroy(tObj.Key);
                }
            }

            /*
            // DEBUG: keep ball in view
            Transform camTransform = _camera.transform;
            _ball.transform.position = camTransform.position;
            _ball.transform.Translate(camTransform.forward * 2);
            */
        }

        private void PlaceAnchorByScreenTap(Vector2 position)
        {
            List<ARRaycastHit> hitResults = new List<ARRaycastHit>();
            _raycastManager.Raycast(
                position, hitResults, /*TrackableType.Planes |*/ TrackableType.FeaturePoint);
            if (hitResults.Count > 0)
            {
                GeospatialPose geospatialPose = _earthManager.Convert(hitResults[0].pose);
                GeospatialAnchorHistory history = new GeospatialAnchorHistory(
                    geospatialPose.Latitude, geospatialPose.Longitude, geospatialPose.Altitude,
                    geospatialPose.EunRotation);
                var anchor = PlaceGeospatialAnchor(history, true /*_usingTerrainAnchor*/);
                if (anchor != null)
                {
                    _historyCollection.Collection.Add(history);
                }

                // ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
                // SaveGeospatialAnchorHistory();
            }
        }

        private ARGeospatialAnchor PlaceGeospatialAnchor(
            GeospatialAnchorHistory history, bool terrain = false)
        {
            Quaternion eunRotation = history.EunRotation;
            if (eunRotation == Quaternion.identity)
            {
                // This history is from a previous app version and EunRotation was not used.
                eunRotation =
                    Quaternion.AngleAxis(180f - (float)history.Heading, Vector3.up);
            }

            var anchor = _anchorManager.ResolveAnchorOnTerrain(
                history.Latitude,
                history.Longitude,
                0,
                eunRotation
            );

            if (anchor != null)
            {
                GameObject anchorGO = Instantiate(TerrainPrefab, anchor.transform);
                anchor.transform.localPosition = Vector3.zero;
                anchor.transform.localScale = Vector3.one;
                anchor.gameObject.SetActive(true);
                _anchorObjects.Add(anchor.gameObject);

                _testObjects.Add(anchorGO, _maxLifeTime);

                StartCoroutine(CheckTerrainAnchorState(anchor));
            }
            else
            {
                InfoHUD.AnchorMessage = string.Format("Failed to set terrain anchor!");
            }

            return anchor;
        }

        private IEnumerator CheckTerrainAnchorState(ARGeospatialAnchor anchor)
        {
            if (anchor == null || _anchorObjects == null)
                yield break;

            int retry = 0;
            while (anchor.terrainAnchorState == TerrainAnchorState.TaskInProgress)
            {
                if (_anchorObjects.Count == 0 || !_anchorObjects.Contains(anchor.gameObject))
                {
                    Debug.LogFormat(
                        "{0} has been removed, exist terrain anchor state check.",
                        anchor.trackableId);
                    yield break;
                }

                if (retry == 100 && _anchorObjects.Last().Equals(anchor.gameObject))
                    InfoHUD.AnchorMessage = InfoMessages._resolvingTimeoutMessage;

                yield return new WaitForSeconds(0.1f);
                retry = Math.Min(retry + 1, 100);
            }

            anchor.gameObject.SetActive(
                !_isLocalizing && anchor.terrainAnchorState == TerrainAnchorState.Success);

            if (_anchorObjects.Last().Equals(anchor.gameObject))
                InfoHUD.AnchorMessage = $"Terrain anchor state: {anchor.terrainAnchorState}";

            yield break;
        }

        private void SwitchToARView(bool enable)
        {
            _isInARView = enable;
            // SessionOrigin.gameObject.SetActive(enable);
            // Session.gameObject.SetActive(enable);
            // ARCoreExtensions.gameObject.SetActive(enable);
            // ARViewCanvas.SetActive(enable);
            // PrivacyPromptCanvas.SetActive(!enable);
            // VPSCheckCanvas.SetActive(false);
            if (enable && _asyncCheck == null)
            {
                _asyncCheck = AvailabilityCheck();
                StartCoroutine(_asyncCheck);
            }
        }

        private IEnumerator AvailabilityCheck()
        {
            if (ARSession.state == ARSessionState.None)
            {
                yield return ARSession.CheckAvailability();
            }

            // Waiting for ARSessionState.CheckingAvailability.
            yield return null;

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                yield return ARSession.Install();
            }

            // Waiting for ARSessionState.Installing.
            yield return null;

            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("Requesting camera permission.");
                Permission.RequestUserPermission(Permission.Camera);
                yield return new WaitForSeconds(3.0f);
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                // User has denied the request.
                Debug.LogWarning(
                    "Failed to get camera permission. VPS availability check is not available.");
                yield break;
            }

            while (_waitingForLocationService)
            {
                yield return null;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning(
                    "Location service is not running. VPS availability check is not available.");
                yield break;
            }

            // Update event is executed before coroutines so it checks the latest error states.
            if (_isQuitting)
            {
                yield break;
            }

            var location = Input.location.lastData;
            var vpsAvailabilityPromise =
                AREarthManager.CheckVpsAvailability(location.latitude, location.longitude);
            yield return vpsAvailabilityPromise;

            Debug.LogFormat("VPS Availability at ({0}, {1}): {2}",
                location.latitude, location.longitude, vpsAvailabilityPromise.Result);
            // show VPS availability check result canvas
            // VPSCheckCanvas.SetActive(vpsAvailabilityPromise.Result != VpsAvailability.Available);
        }
    }
}