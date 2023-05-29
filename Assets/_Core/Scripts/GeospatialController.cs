namespace BlackRece.LaSARTag.Geospatial
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using UnityEngine;
    using UnityEngine.Android;
    using UnityEngine.EventSystems;
    using UnityEngine.Serialization;
    using UnityEngine.UI;
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;

    using Google.XR.ARCoreExtensions;
    using TMPro;

    /// <summary>
    /// Controller for Geospatial sample.
    /// </summary>
    // [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines",
    //     Justification = "Bypass source check.")]
    public class GeospatialController : MonoBehaviour
    {
        [FormerlySerializedAs("SessionOrigin")]
        [Header("AR Components")]

        /// <summary>
        /// The ARSessionOrigin used in the sample.
        /// </summary>
        [SerializeField] private ARSessionOrigin _SessionOrigin;

        /// <summary>
        /// The ARAnchorManager used in the sample.
        /// </summary>
        private ARAnchorManager _anchorManager;

        /// <summary>
        /// The ARRaycastManager used in the sample.
        /// </summary>
        private ARRaycastManager _raycastManager;

        /// <summary>
        /// The AREarthManager used in the sample.
        /// </summary>
        private AREarthManager _earthManager;

        /// <summary>
        /// The ARCoreExtensions used in the sample.
        /// </summary>
        private ARCoreExtensions _coreExtensions;

        [FormerlySerializedAs("TerrainPrefab")]
        [Header("UI Elements")]

        /// <summary>
        /// A 3D object that presents an Geospatial Terrain anchor.
        /// </summary>
        [SerializeField] private GameObject _TerrainPrefab;

        /// <summary>
        /// UI Element containing buttons and counters for anchors.
        /// </summary>
        [SerializeField] private GameObject _AnchorPanel;

        /// <summary>
        /// UI element for clearing all anchors, including history.
        /// </summary>
        [SerializeField] private Button _ClearAllButton;

        /// <summary>
        /// UI element for adding a new anchor at current location.
        /// </summary>
        [SerializeField] private Button _SetAnchorButton;
        
        [SerializeField] private TMP_Text _AnchorCountText;
        
        /// <summary>
        /// UI element for switching to battle mode.
        /// </summary>
        [SerializeField] private Button _BattleModeButton;
        [SerializeField] private GameObject _Reticule;

        /// <summary>
        /// Text displaying <see cref="GeospatialPose"/> information at runtime.
        /// </summary>
        [SerializeField] private TMP_Text _InfoText;

        /// <summary>
        /// Text displaying in a snack bar at the bottom of the screen.
        /// </summary>
        [SerializeField] private TMP_Text _SnackBarText;

        
        /// <summary>
        /// Text displaying debug information, only activated in debug build.
        /// </summary>
        [SerializeField] private TMP_Text _DebugTextBox;

        private string _vpsStatus;
        [SerializeField] private TMP_Text _LoggerTextBox;
        
        private List<string> _debugTextList = new List<string>();

        // Call this property to add a new string to the list and update the text object
        public string _DebugLogger
        {
            get
            {
                int startIndex = Mathf.Max(0, _debugTextList.Count - 10);
                return string.Join("\n",
                    _debugTextList
                        .GetRange(startIndex, _debugTextList.Count - startIndex)
                        .ToArray()
                );
            }
            set
            {
                _debugTextList.Add(value);
                if (_debugTextList.Count > 10)
                    _debugTextList.RemoveAt(0);

                if(_LoggerTextBox != null)
                    _LoggerTextBox.text = _DebugLogger;
            }
        }

        private struct LocalizationMessages
        {
            // Help message shows while localizing.
            public const string LOCALIZING_MESSAGE = "Localizing your device to set anchor.";
            // Help message shows while initializing Geospatial functionalities.
            public const string LOCALIZATION_INITIALIZING_MESSAGE = 
                "Initializing Geospatial functionalities.";
            // Help message shows when <see cref="AREarthManager.EarthTrackingState"/> is not
            // tracking or the pose accuracies are beyond thresholds.
            public const string LOCALIZATION_INSTRUCTION_MESSAGE = 
                "Point your camera at buildings, stores, and signs near you.";
            /// Help message shows when location fails or hits timeout.
            public const string LOCALIZATION_FAILURE_MESSAGE = 
                "Localization not possible.\nClose and open the app to restart the session.";
            public const string LOCALIZATION_SUCCESS_MESSAGE = "Localization completed.";
            public const string RESOLVING_TIMEOUT_MESSAGE = 
                "Still resolving the terrain anchor." +
                "\nPlease make sure you're in an area that has VPS coverage.";
        }

        #region Anchor Constants
        
        /// <summary>
        /// The timeout period waiting for localization to be completed.
        /// </summary>
        private const float _TIMEOUT_SECONDS = 180;

        /// <summary>
        /// Indicates how long a information text will display on the screen before terminating.
        /// </summary>
        private const float _ERROR_DISPLAY_SECONDS = 3;

        /// <summary>
        /// The key name used in PlayerPrefs which indicates whether the privacy prompt has
        /// displayed at least one time.
        /// </summary>
        private const string _HAS_DISPLAYED_PRIVACY_PROMPT_KEY = "HasDisplayedGeospatialPrivacyPrompt";

        /// <summary>
        /// The key name used in PlayerPrefs which stores geospatial anchor history data.
        /// The earliest one will be deleted once it hits storage limit.
        /// </summary>
        private const string _PERSISTENT_GEOSPATIAL_ANCHORS_STORAGE_KEY = "PersistentGeospatialAnchors";

        /// <summary>
        /// The limitation of how many Geospatial Anchors can be stored in local storage.
        /// </summary>
        private const int _STORAGE_LIMIT = 5;

        /// <summary>
        /// Accuracy threshold for orientation yaw accuracy in degrees that can be treated as
        /// localization completed.
        /// </summary>
        private const double _ORIENTATION_YAW_ACCURACY_THRESHOLD = 25;

        /// <summary>
        /// Accuracy threshold for heading degree that can be treated as localization completed.
        /// </summary>
        private const double _HEADING_ACCURACY_THRESHOLD = 25;

        /// <summary>
        /// Accuracy threshold for altitude and longitude that can be treated as localization
        /// completed.
        /// </summary>
        private const double _HORIZONTAL_ACCURACY_THRESHOLD = 20;

        #endregion // Anchor Constants
        
        private bool _IsSessionTracking =>
            ARSession.state == ARSessionState.SessionTracking;

        private bool _IsSessionInitialised =>
            ARSession.state != ARSessionState.SessionInitializing &&
            !_IsSessionTracking;

        private bool _IsSessionReady =>
            _IsSessionTracking &&
            Input.location.status == LocationServiceStatus.Running;
        
        public bool _IsEarthTracking =>
            _earthManager.EarthTrackingState == TrackingState.Tracking;
        
        private bool _waitingForLocationService = false;
        private bool _isReturning = false;
        private bool _isLocalizing = false;
        private bool _enablingGeospatial = false;
        private bool _shouldResolvingHistory = false;
        private float _localizationPassedTime = 0f;
        private float _configurePrepareTime = 3f;
        private GeospatialAnchorHistoryCollection _historyCollection = null;
        private List<GameObject> _anchorObjects = new List<GameObject>();
        private IEnumerator _startLocationService = null;
        private IEnumerator _asyncCheck = null;
        private bool _isBattleEnabled;

        private GeospatialAnchorHistory _CameraGeoAnchor
        {
            get
            {
                var cameraPose = _earthManager.CameraGeospatialPose;
                var eunRotation = cameraPose.EunRotation;
                var anchorHistory = new GeospatialAnchorHistory(
                    cameraPose.Latitude,
                    cameraPose.Longitude,
                    cameraPose.Altitude,
                    eunRotation
                );

                anchorHistory.EunRotation = eunRotation != Quaternion.identity
                    ? eunRotation
                    : Quaternion.AngleAxis(180f - (float)anchorHistory.Heading, Vector3.up);

                return anchorHistory;
            }
        }
        
        public ARGeospatialAnchor _GetGeoPosition 
            => _anchorManager.AddAnchor(
                _CameraGeoAnchor.Latitude,
                _CameraGeoAnchor.Longitude,
                _CameraGeoAnchor.Altitude,
                _CameraGeoAnchor.EunRotation
            );

        /// <summary>
        /// Callback handling "Get Started" button click event in Privacy Prompt.
        /// </summary>
        // public void OnGetStartedClicked()
        // {
        //     PlayerPrefs.SetInt(_hasDisplayedPrivacyPromptKey, 1);
        //     PlayerPrefs.Save();
        //     SwitchToARView(true);
        // }

        /// <summary>
        /// Callback handling "Learn More" Button click event in Privacy Prompt.
        /// </summary>
        // public void OnLearnMoreClicked() 
        //     => Application.OpenURL("https://developers.google.com/ar/data-privacy");

        #region Unity Events
        
        /// <summary>
        /// Unity's Awake() method.
        /// </summary>
        public void Awake()
        {
            // // Lock screen to portrait.
            // Screen.autorotateToLandscapeLeft = false;
            // Screen.autorotateToLandscapeRight = false;
            // Screen.autorotateToPortraitUpsideDown = false;
            // Screen.orientation = ScreenOrientation.Portrait;

            // Enable geospatial sample to target 60fps camera capture frame rate
            // on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;

            if (_SessionOrigin == null)
            {
                Debug.LogError("Cannot find ARSessionOrigin.");
            }
            else
            {
                _coreExtensions = _SessionOrigin.GetComponent<ARCoreExtensions>();
                _earthManager = _SessionOrigin.GetComponent<AREarthManager>();
                _anchorManager = _SessionOrigin.GetComponent<ARAnchorManager>();
                _raycastManager = _SessionOrigin.GetComponent<ARRaycastManager>();
            }

            if (_AnchorPanel == null)
                Debug.LogError("Cannot find AnchorPanel.");
            
            if (_coreExtensions == null)
            {
                Debug.LogError("Cannot find ARCoreExtensions.");
            }
        }

        /// <summary>
        /// Unity's OnEnable() method.
        /// </summary>
        public void OnEnable()
        {
            _startLocationService = StartLocationService();
            StartCoroutine(_startLocationService);

            _isReturning = false;
            _enablingGeospatial = false;
            _isBattleEnabled = false;
            _BattleModeButton.gameObject.SetActive(false);
            _Reticule.SetActive(false);
            _ClearAllButton.gameObject.SetActive(false);
            //DebugTextBox.gameObject.SetActive(Debug.isDebugBuild && EarthManager != null);

            _localizationPassedTime = 0f;
            _isLocalizing = true;
            _SnackBarText.text = LocalizationMessages.LOCALIZING_MESSAGE;

            LoadGeospatialAnchorHistory();
            _shouldResolvingHistory = _historyCollection.Collection.Count > 0;

            //SwitchToARView(PlayerPrefs.HasKey(_hasDisplayedPrivacyPromptKey));
        }

        /// <summary>
        /// Unity's OnDisable() method.
        /// </summary>
        public void OnDisable()
        {
            StopCoroutine(_asyncCheck);
            _asyncCheck = null;
            StopCoroutine(_startLocationService);
            _startLocationService = null;
            Debug.Log("Stop location services.");
            Input.location.Stop();

            foreach (var anchor in _anchorObjects)
                Destroy(anchor);

            _anchorObjects.Clear();
            SaveGeospatialAnchorHistory();
        }

        private void Start()
        {
            if (_asyncCheck == null)
            {
                _asyncCheck = AvailabilityCheck();
                StartCoroutine(_asyncCheck);
            }
            
            _AnchorPanel.SetActive(true);
        }

        /// <summary>
        /// Unity's Update() method.
        /// </summary>
        public void Update()
        {
            // Update debug text.
            UpdateDebugInfo();

            // Check session error status.
            LifecycleUpdate();
            if (_isReturning)
                return;

            if (!_IsSessionInitialised)
                return;
            
            // Check feature support and enable Geospatial API when it's supported.
            var featureSupport = _earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
            switch (featureSupport)
            {
                case FeatureSupported.Unknown:
                    return;
                case FeatureSupported.Unsupported:
                    ReturnWithReason("Geospatial API is not supported by this devices.");
                    return;
                case FeatureSupported.Supported:
                    if (_coreExtensions.ARCoreExtensionsConfig.GeospatialMode ==
                        GeospatialMode.Disabled)
                    {
                        Debug.Log("Geospatial sample switched to GeospatialMode.Enabled.");
                        _coreExtensions.ARCoreExtensionsConfig.GeospatialMode =
                            GeospatialMode.Enabled;
                        _configurePrepareTime = 3.0f;
                        _enablingGeospatial = true;
                        return;
                    }

                    break;
            }

            // Waiting for new configuration taking effect.
            if (_enablingGeospatial)
            {
                _configurePrepareTime -= Time.deltaTime;
                if (_configurePrepareTime < 0)
                    _enablingGeospatial = false;
                else
                    return;
            }

            // Check earth state.
            var earthState = _earthManager.EarthState;
            if (earthState == EarthState.ErrorEarthNotReady)
            {
                _SnackBarText.text = LocalizationMessages.LOCALIZATION_INITIALIZING_MESSAGE;
                return;
            }
            else if (earthState != EarthState.Enabled)
            {
                string errorMessage =
                    "Geospatial sample encountered an EarthState error: " + earthState;
                Debug.LogWarning(errorMessage);
                _SnackBarText.text = errorMessage;
                return;
            }
            
            var pose = _IsEarthTracking ? _earthManager.CameraGeospatialPose : new GeospatialPose();
            if (!_IsSessionReady || !_IsEarthTracking || IsOverThreshold(pose))
            {
                // Lost localization during the session.
                if (!_isLocalizing)
                {
                    _isLocalizing = true;
                    _localizationPassedTime = 0f;
                    _SetAnchorButton.gameObject.SetActive(false);
                    _ClearAllButton.gameObject.SetActive(false);

                    foreach (var go in _anchorObjects)
                        go.SetActive(false);
                }

                if (_localizationPassedTime > _TIMEOUT_SECONDS)
                {
                    Debug.LogError("Geospatial sample localization passed timeout.");
                    ReturnWithReason(LocalizationMessages.LOCALIZATION_FAILURE_MESSAGE);
                }
                else
                {
                    _localizationPassedTime += Time.deltaTime;
                    _SnackBarText.text = LocalizationMessages.LOCALIZATION_INSTRUCTION_MESSAGE;
                }
            }
            else if (_isLocalizing)
            {
                // Finished localization.
                _isLocalizing = false;
                _localizationPassedTime = 0f;
                _SetAnchorButton.gameObject.SetActive(true);
                _ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
                _SnackBarText.text = LocalizationMessages.LOCALIZATION_SUCCESS_MESSAGE;
                foreach (var go in _anchorObjects)
                {
                    var terrainState = go.GetComponent<ARGeospatialAnchor>().terrainAnchorState;
                    if (terrainState != TerrainAnchorState.None &&
                        terrainState != TerrainAnchorState.Success)
                    {
                        // Skip terrain anchors that are still waiting for resolving
                        // or failed on resolving.
                        continue;
                    }

                    go.SetActive(true);
                }

                ResolveHistory();
            }
            else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began
                && !EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                // Set anchor on screen tap.
                if(!_isBattleEnabled)
                    PlaceAnchorByScreenTap(Input.GetTouch(0).position);
            }

            UpdateInfoPanel(pose, _IsEarthTracking);
        }
        
        #endregion // Unity Events
        
        #region UI Events
        
        /// <summary>
        /// Callback handling "Clear All" button click event in AR View.
        /// </summary>
        public void OnClearAllClicked()
        {
            foreach (var anchor in _anchorObjects)
                Destroy(anchor);

            _anchorObjects.Clear();
            _AnchorCountText.text = _anchorObjects.Count.ToString();
            _historyCollection.Collection.Clear();
            _SnackBarText.text = "Anchor(s) cleared!";
            _ClearAllButton.gameObject.SetActive(false);
            SaveGeospatialAnchorHistory();
        }

        /// <summary>
        /// Callback handling "Set Anchor" button click event in AR View.
        /// </summary>
        public void OnSetAnchorClicked()
        {
            _DebugLogger = "Setting anchor...";
            var pose = _earthManager.CameraGeospatialPose;
            Quaternion eunRotation = pose.EunRotation;

            GeospatialAnchorHistory history = new GeospatialAnchorHistory(
                pose.Latitude,
                pose.Longitude, 
                pose.Altitude, 
                eunRotation
            );

            var anchor = PlaceGeospatialAnchor(history);
            if (anchor != null)
                _historyCollection.Collection.Add(history);

            _BattleModeButton.gameObject.SetActive(_anchorObjects.Count == 1);
            _ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            SaveGeospatialAnchorHistory();
        }

        public void OnBattleModeClicked()
        {
            if(_anchorObjects.Count != 1)
                return;

            _isBattleEnabled = true;
            _BattleModeButton.gameObject.SetActive(false);
            _AnchorPanel.SetActive(false);
            _Reticule.SetActive(true);
        }
        
        #endregion // UI Events

        #region AR Coroutines

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
                    _SnackBarText.text = LocalizationMessages.RESOLVING_TIMEOUT_MESSAGE;

                yield return new WaitForSeconds(0.1f);
                retry = Math.Min(retry + 1, 100);
            }

            anchor.gameObject.SetActive(
                !_isLocalizing && anchor.terrainAnchorState == TerrainAnchorState.Success);
            if (_anchorObjects.Last().Equals(anchor.gameObject))
                _SnackBarText.text = $"Terrain anchor state: {anchor.terrainAnchorState}";

            yield break;
        }
        
        private IEnumerator AvailabilityCheck()
        {
            if (ARSession.state == ARSessionState.None)
                yield return ARSession.CheckAvailability();

            // Waiting for ARSessionState.CheckingAvailability.
            yield return null;

            if (ARSession.state == ARSessionState.NeedsInstall)
                yield return ARSession.Install();

            // Waiting for ARSessionState.Installing.
            yield return null;

            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("Requesting camera permission.");
                _vpsStatus = "Requesting camera permission.";
                Permission.RequestUserPermission(Permission.Camera);
                yield return new WaitForSeconds(3.0f);
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                // User has denied the request.
                Debug.LogWarning(
                    "Failed to get camera permission. VPS availability check is not available.");
                _vpsStatus = "Failed to get camera permission.";
                yield break;
            }

            while (_waitingForLocationService)
                yield return null;

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning(
                    "Location service is not running. VPS availability check is not available.");
                _vpsStatus = "Location service is not running.";
                yield break;
            }

            // Update event is executed before coroutines so it checks the latest error states.
            if (_isReturning)
                yield break;

            var location = Input.location.lastData;
            var vpsAvailabilityPromise =
                AREarthManager.CheckVpsAvailability(location.latitude, location.longitude);
            yield return vpsAvailabilityPromise;

            Debug.LogFormat("VPS Availability at ({0}, {1}): {2}",
                location.latitude, location.longitude, vpsAvailabilityPromise.Result);
            _vpsStatus = $"VPS Availability at ({location.latitude}, {location.longitude}): {vpsAvailabilityPromise.Result}";
        }

        private IEnumerator StartLocationService()
        {
            _waitingForLocationService = true;
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.Log("Requesting fine location permission.");
                Permission.RequestUserPermission(Permission.FineLocation);
                yield return new WaitForSeconds(3.0f);
            }

            if (!Input.location.isEnabledByUser)
            {
                Debug.Log("Location service is disabled by User.");
                _waitingForLocationService = false;
                yield break;
            }

            Debug.Log("Start location service.");
            Input.location.Start();

            while (Input.location.status == LocationServiceStatus.Initializing)
            {
                yield return null;
            }

            _waitingForLocationService = false;
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarningFormat(
                    "Location service ends with {0} status.", Input.location.status);
                Input.location.Stop();
            }
        }

        #endregion // AR Coroutines
        
        #region AR Methods
        
        private void PlaceAnchorByScreenTap(Vector2 position)
        {
            List<ARRaycastHit> hitResults = new List<ARRaycastHit>();
            _raycastManager.Raycast(
                position,
                hitResults,
                TrackableType.Planes | TrackableType.FeaturePoint
            );

            if (hitResults.Count <= 0) return;
            
            var geospatialPose = _earthManager.Convert(hitResults[0].pose);
            var history = new GeospatialAnchorHistory(
                geospatialPose.Latitude,
                geospatialPose.Longitude,
                geospatialPose.Altitude,
                geospatialPose.EunRotation
            );
            var anchor = PlaceGeospatialAnchor(history);
            
            if (anchor != null)
                _historyCollection.Collection.Add(history);

            _BattleModeButton.gameObject.SetActive(_anchorObjects.Count == 1);
            _ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            SaveGeospatialAnchorHistory();
        }

        private ARGeospatialAnchor PlaceGeospatialAnchor(GeospatialAnchorHistory history)
        {
            var eunRotation = history.EunRotation;
            if (eunRotation == Quaternion.identity)
            {
                // This history is from a previous app version and EunRotation was not used.
                eunRotation =
                    Quaternion.AngleAxis(180f - (float)history.Heading, Vector3.up);
            }

            _DebugLogger = $"Anchor: {history.Latitude}, {history.Longitude}, {history.Altitude}";
            var anchor = _anchorManager.ResolveAnchorOnTerrain(
                history.Latitude, 
                history.Longitude,
                0,
                eunRotation
            );

            string anchorState = anchor == null ? "null" : anchor.terrainAnchorState.ToString();
            _DebugLogger = $"Anchor is {anchorState}";
            if (anchor != null)
            {
                _anchorObjects.Add(anchor.gameObject);
                _AnchorCountText.text = _anchorObjects.Count.ToString();

                StartCoroutine(CheckTerrainAnchorState(anchor));
                _SnackBarText.text = $"{_anchorObjects.Count} Anchor(s) Set!";
            }
            else
            {
                _SnackBarText.text = "Failed to set a terrain anchor.";
            }

            return anchor;
        }
        
        #endregion // AR Methods

        private void ResolveHistory()
        {
            if (!_shouldResolvingHistory)
                return;

            _shouldResolvingHistory = false;
            foreach (var history in _historyCollection.Collection)
                PlaceGeospatialAnchor(history);

            _BattleModeButton.gameObject.SetActive(false);
            _ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            _SnackBarText.text = $"{_anchorObjects.Count} anchor(s) set from history.";
        }

        #region Load/Save Anchor History
        private void LoadGeospatialAnchorHistory()
        {
            if (PlayerPrefs.HasKey(_PERSISTENT_GEOSPATIAL_ANCHORS_STORAGE_KEY))
            {
                _historyCollection = JsonUtility.FromJson<GeospatialAnchorHistoryCollection>(
                    PlayerPrefs.GetString(_PERSISTENT_GEOSPATIAL_ANCHORS_STORAGE_KEY));

                // Remove all records created more than 24 hours and update stored history.
                DateTime current = DateTime.Now;
                _historyCollection.Collection.RemoveAll(
                    data => current.Subtract(data.CreatedTime).Days > 0);
                PlayerPrefs.SetString(_PERSISTENT_GEOSPATIAL_ANCHORS_STORAGE_KEY,
                    JsonUtility.ToJson(_historyCollection));
                PlayerPrefs.Save();
            }
            else
            {
                _historyCollection = new GeospatialAnchorHistoryCollection();
            }
        }

        private void SaveGeospatialAnchorHistory()
        {
            // Sort the data from latest record to earliest record.
            _historyCollection.Collection.Sort((left, right) =>
                right.CreatedTime.CompareTo(left.CreatedTime));

            // Remove the earliest data if the capacity exceeds storage limit.
            if (_historyCollection.Collection.Count > _STORAGE_LIMIT)
            {
                _historyCollection.Collection.RemoveRange(
                    _STORAGE_LIMIT, _historyCollection.Collection.Count - _STORAGE_LIMIT);
            }

            PlayerPrefs.SetString(
                _PERSISTENT_GEOSPATIAL_ANCHORS_STORAGE_KEY, JsonUtility.ToJson(_historyCollection));
            PlayerPrefs.Save();
        }

        #endregion // Load/Save Anchor History
        
        private void LifecycleUpdate()
        {
            // Pressing 'back' button quits the app.
            if (Input.GetKeyUp(KeyCode.Escape))
                QuitApplication();

            if (_isReturning)
                return;

            // Only allow the screen to sleep when not tracking.
            int sleepTimeout = SleepTimeout.NeverSleep;
            if (ARSession.state != ARSessionState.SessionTracking)
                sleepTimeout = SleepTimeout.SystemSetting;

            Screen.sleepTimeout = sleepTimeout;

            // Quit the app if ARSession is in an error status.
            string returningReason = string.Empty;
            if (ARSession.state != ARSessionState.CheckingAvailability &&
                ARSession.state != ARSessionState.Ready &&
                ARSession.state != ARSessionState.SessionInitializing &&
                ARSession.state != ARSessionState.SessionTracking)
            {
                returningReason = 
                    $"Geospatial sample encountered an ARSession error state {ARSession.state}.\n" +
                    "Please start the app again.";
            }
            else if (Input.location.status == LocationServiceStatus.Failed)
            {
                returningReason =
                    "Geospatial sample failed to start location service.\n" +
                    "Please start the app again and grant precise location permission.";
            }
            else if (_SessionOrigin == null || _coreExtensions == null)
            {
                returningReason = 
                    "Geospatial sample failed with missing AR Components.";
            }

            ReturnWithReason(returningReason);
        }

        #region Utility Functions
        
        private void ReturnWithReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return;

            _SetAnchorButton.gameObject.SetActive(false);
            _ClearAllButton.gameObject.SetActive(false);

            Debug.LogError(reason);
            _SnackBarText.text = reason;
            _isReturning = true;
            Invoke(nameof(QuitApplication), _ERROR_DISPLAY_SECONDS);
        }

        private void QuitApplication() => Application.Quit();
        
        private bool IsOverThreshold(GeospatialPose pose) =>
            pose.OrientationYawAccuracy > _ORIENTATION_YAW_ACCURACY_THRESHOLD ||
            pose.HorizontalAccuracy > _HORIZONTAL_ACCURACY_THRESHOLD;
        
        #endregion // Utility Functions
        
        private void UpdateInfoPanel(GeospatialPose pose, bool isEarthTracking)
        {
            _InfoText.text = !isEarthTracking
                ? "GEOSPATIAL POSE: not tracking"
                : $"Latitude/Longitude: {pose.Latitude:F6}°, {pose.Longitude:F6}°\n" +
                  $"Horizontal Accuracy: {pose.HorizontalAccuracy:F6}m\n" +
                  $"Altitude: {pose.Altitude:F2}m\n" +
                  $"Vertical Accuracy: {pose.VerticalAccuracy:F2}m\n" +
                  $"Eun Rotation: {pose.EunRotation:F1}\n" +
                  $"Orientation Yaw Accuracy: {pose.OrientationYawAccuracy:F1}°";
        }
        
        private void UpdateDebugInfo()
        {
            if (!Debug.isDebugBuild || _earthManager == null)
                return;

            var pose = _earthManager.EarthState == EarthState.Enabled && _IsEarthTracking
                ? _earthManager.CameraGeospatialPose
                : new GeospatialPose();
            
            var supported = _earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled);
            _DebugTextBox.text =
                $"VPS: {_vpsStatus}\n" +
                $"IsReturning: {_isReturning}\n" +
                $"IsLocalizing: {_isLocalizing}\n" +
                $"SessionState: {ARSession.state}\n" +
                $"LocationServiceStatus: {Input.location.status}\n" +
                $"FeatureSupported: {supported}\n" +
                $"EarthState: {_earthManager.EarthState}\n" +
                $"EarthTrackingState: {_IsEarthTracking}\n" +
                $"  LAT/LNG: {pose.Latitude:F6}, {pose.Longitude:F6}\n" +
                $"  HorizontalAcc: {pose.HorizontalAccuracy:F6}\n" +
                $"  ALT: {pose.Altitude:F2}\n" +
                $"  VerticalAcc: {pose.VerticalAccuracy:F2}\n" +
                $". EunRotation: {pose.EunRotation:F2}\n" +
                $"  OrientationYawAcc: {pose.OrientationYawAccuracy:F2}";
        }
    }
}
