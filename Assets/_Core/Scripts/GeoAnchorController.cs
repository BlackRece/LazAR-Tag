namespace BlackRece.LaSARTag
{
    using System;
    using System.Linq;
    using System.Collections;
    using System.Collections.Generic;
    
    using UnityEngine;
    using UnityEngine.Android;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;
    
    using Google.XR.ARCoreExtensions;
    
    using EmitProjectile;
    using Geospatial;
    using TMPro;
    
    public class GeoAnchorController : MonoBehaviour
    {
        [Header ("UI Elements")]
        [SerializeField] private Button m_AddAnchorsButton;
        [SerializeField] private Button m_ClearAnchorsButton;
        [SerializeField] private TMP_Text m_AnchorCountText;
        
        [Header("Prefab")]
        [SerializeField] private GameObject m_GeospatialPrefab;
        [SerializeField] private GameObject m_TerrainPrefab;

        [Header("AR Session")]
        [SerializeField] private ARSessionOrigin m_sessionOrigin;
        
        
        private ARAnchorManager m_anchorManager;
        private AREarthManager m_earthManager;
        private ARCoreExtensions m_coreExtensions;
        private ARRaycastManager m_raycastManager;
        
        private EmitObject m_EmitObject;
        
        private const float m_errorDisplaySeconds = 3;
        private const double m_yawAccuracyThreshold = 25;
        private const double m_horizontalAccuracyThreshold = 25;
        private const float _timeoutSeconds = 180;
        
        private GeospatialAnchorHistoryCollection m_historyCollection = null;

        private List<GameObject> m_anchorObjects = new List<GameObject>();
        private List<GameObject> m_AnchorObjects
        {
            get
            {
                m_AnchorCountText.text = m_anchorObjects.Count.ToString();
                return m_anchorObjects;
            }
            
            set
            {
                m_anchorObjects = value;
                bool showButton = m_anchorObjects.Count > 0;
                m_ClearAnchorsButton.gameObject.SetActive(showButton);
                m_AnchorCountText.text = m_anchorObjects.Count.ToString();
            }
        }
        
        private bool m_isLocalizing;

        private IEnumerator m_AsyncCheck;
        private IEnumerator m_startLocationService;
        
        private bool m_WaitingForLocationService;
        private bool m_IsReturning;
        private bool m_IsInARView;
        private float m_configurePrepareTime;
        private bool m_EnablingGeospatial;
        private bool _shouldResolvingHistory = false;

        private float _localizationPassedTime = 0f;
        private TMP_Text m_AddButtonText;

        private bool m_isAddingAnchors;
        private bool m_IsAddingAnchors
        {
            get => m_isAddingAnchors;
            set
            {
                m_isAddingAnchors = value;
                m_AddButtonText.text = m_isAddingAnchors ? "Fight Mode" : "Build Mode";
            }
        }

        private bool m_IsSessionTracking =>
            ARSession.state == ARSessionState.SessionTracking;

        private bool m_SessionState =>
            ARSession.state != ARSessionState.SessionInitializing &&
            !m_IsSessionTracking;

        private bool m_IsSessionReady =>
            m_IsSessionTracking &&
            Input.location.status == LocationServiceStatus.Running;
        
        private bool m_IsEarthTracking =>
            m_earthManager.EarthTrackingState == TrackingState.Tracking;

        #region Unity Events
        private void OnEnable()
        {
            m_startLocationService = Co_StartLocationService();
            StartCoroutine(m_startLocationService);

            m_IsReturning = false;
            m_EnablingGeospatial = false;

            m_isLocalizing = true;
            SwitchToARView(true);
        }
        
        private void OnDisable()
        {
            StopCoroutine(m_startLocationService);
            m_startLocationService = null;
            Input.location.Stop();
        }

        private void Awake()
        {
            if (m_sessionOrigin == null)
            {
                Debug.LogError("Session Origin is null");
                QuitApplication();
            }

            m_EmitObject = m_sessionOrigin.GetComponent<EmitObject>();
            
            m_AddButtonText = m_AddAnchorsButton.GetComponentInChildren<TMP_Text>();
            
            m_earthManager = m_sessionOrigin.GetComponent<AREarthManager>();
            m_coreExtensions = m_sessionOrigin.GetComponent<ARCoreExtensions>();
            m_anchorManager = m_sessionOrigin.GetComponent<ARAnchorManager>();
            m_raycastManager = m_sessionOrigin.GetComponent<ARRaycastManager>();

            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            if (m_GeospatialPrefab == null)
            {
                m_GeospatialPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m_GeospatialPrefab.GetComponent<Renderer>().material.color = Color.red;
                m_GeospatialPrefab.SetActive(false);
            }

            if (m_TerrainPrefab == null)
            {
                m_TerrainPrefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                m_TerrainPrefab.GetComponent<Renderer>().material.color = Color.cyan;
                m_TerrainPrefab.SetActive(false);
            }
        }

        private void Update()
        {
            if(!m_IsInARView)
                return;

            if (m_IsReturning)
                return;
            else
                LifeCycleUpdate();
            
            if (m_SessionState)
                return;

            if (!IsFeatureSupported())
                return;
            
            // Waiting for new configuration taking effect.
            if (m_EnablingGeospatial)
            {
                m_configurePrepareTime -= Time.deltaTime;
                if (m_configurePrepareTime < 0)
                    m_EnablingGeospatial = false;
                else
                    return;
            }

            if (!IsEarthEnabled())
                return;
            
            GeospatialPose pose = m_IsEarthTracking
                ? m_earthManager.CameraGeospatialPose
                : new GeospatialPose();
            if (!m_IsSessionReady || !m_IsEarthTracking ||
                pose.OrientationYawAccuracy > m_yawAccuracyThreshold ||
                pose.HorizontalAccuracy > m_horizontalAccuracyThreshold)
            {
                // Lost localization during the session.
                if (!m_isLocalizing)
                {
                    m_isLocalizing = true;
                    _localizationPassedTime = 0f;
                    Debug.Log("Localizing...");
                    
                    // TODO: Hide UI
                    foreach (GameObject anchorObject in m_AnchorObjects)
                        anchorObject.SetActive(false);
                }
                
                _localizationPassedTime += Time.deltaTime;
                if (_localizationPassedTime > _timeoutSeconds)
                {
                    Debug.LogError("Geospatial sample localization passed timeout.");
                    ReturnWithReason(InfoMessages._localizationFailureMessage);
                }
            }
            else if (m_isLocalizing)
            {
                // Finished localization.
                m_isLocalizing = false;
                _localizationPassedTime = 0f;
                
                foreach (var go in m_AnchorObjects)
                {
                    TerrainAnchorState terrainState = go
                        .GetComponent<ARGeospatialAnchor>()
                        .terrainAnchorState;
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
            else if (Input.touchCount > 0 && 
                     Input.GetTouch(0).phase == TouchPhase.Began &&
                     !EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                // Set anchor on screen tap.
                PlaceAnchorByScreenTap(Input.GetTouch(0).position);
            }
        }
        
        #endregion //  Unity Events
        
        #region Coroutines
        
        private IEnumerator Co_AvailabilityCheck()
        {
            switch (ARSession.state)
            {
                case ARSessionState.None:
                    yield return ARSession.CheckAvailability();
                    break;
                case ARSessionState.NeedsInstall:
                    yield return ARSession.Install();
                    break;
                case ARSessionState.CheckingAvailability:
                case ARSessionState.Installing:
                    // Waiting...
                    yield return null;
                    break;
            }

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

            while (m_WaitingForLocationService)
                yield return null;

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning(
                    "Location service is not running. VPS availability check is not available.");
                yield break;
            }

            // Update event is executed before coroutines so it checks the latest error states.
            if (m_IsReturning)
                yield break;

            var location = Input.location.lastData;
            var vpsAvailabilityPromise =
                AREarthManager.CheckVpsAvailability(location.latitude, location.longitude);
            yield return vpsAvailabilityPromise;

            Debug.LogFormat("VPS Availability at ({0}, {1}): {2}",
                location.latitude, location.longitude, vpsAvailabilityPromise.Result);
            //VPSCheckCanvas.SetActive(vpsAvailabilityPromise.Result != VpsAvailability.Available);
        }
        
        private IEnumerator Co_StartLocationService()
        {
            if (!Input.location.isEnabledByUser)
            {
                Debug.Log("User has not enabled GPS");
                yield break;
            }

            Input.location.Start();
            int maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }

            if (maxWait < 1)
            {
                Debug.Log("Timed out");
                yield break;
            }

            if (Input.location.status == LocationServiceStatus.Failed)
            {
                Debug.Log("Unable to determine device location");
                yield break;
            }

            Debug.Log("Location: " + Input.location.lastData.latitude + " " +
                      Input.location.lastData.longitude + " " +
                      Input.location.lastData.altitude + " " +
                      Input.location.lastData.horizontalAccuracy + " " +
                      Input.location.lastData.timestamp);
        }
        
        private IEnumerator Co_CheckTerrainAnchorState(ARGeospatialAnchor anchor)
        {
            if (anchor == null || m_AnchorObjects == null)
                yield break;

            int retry = 0;
            while (anchor.terrainAnchorState == TerrainAnchorState.TaskInProgress)
            {
                if (m_AnchorObjects.Count == 0 ||
                    !m_AnchorObjects.Contains(anchor.gameObject))
                {
                    Debug.LogFormat(
                        "{0} has been removed, exist terrain anchor state check.",
                        anchor.trackableId);
                    yield break;
                }

                if (retry == 100 && m_AnchorObjects.Last().Equals(anchor.gameObject))
                {
                    // SnackBarText.text = _resolvingTimeoutMessage;
                }

                yield return new WaitForSeconds(0.1f);
                retry = Math.Min(retry + 1, 100);
            }

            anchor.gameObject.SetActive(
                !m_isLocalizing && anchor.terrainAnchorState == TerrainAnchorState.Success);
            if (m_AnchorObjects.Last().Equals(anchor.gameObject))
            {
                // SnackBarText.text = $"Terrain anchor state: {anchor.terrainAnchorState}";
            }

            yield break;
        }
        
        #endregion // Coroutines

        #region UI Events

        public void OnAddAnchors_Clicked()
        {
            m_IsAddingAnchors = !m_IsAddingAnchors;
            m_EmitObject._isEmittingEnabled = !m_IsAddingAnchors;
        }

        public void OnClearAnchors_Clicked()
        {
            foreach (GameObject anchorObject in m_AnchorObjects)
                Destroy(anchorObject);
            
            m_AnchorObjects.Clear();
            // m_AddAnchorsButton
            // m_DelAnchorsButton
            m_ClearAnchorsButton.gameObject.SetActive(false);
        }

        #endregion // UI Events
        
        private void SwitchToARView(bool enable)
        {
            m_IsInARView = enable;
            switch (enable)
            {
                // SessionOrigin.gameObject.SetActive(enable);
                // Session.gameObject.SetActive(enable);
                // ARCoreExtensions.gameObject.SetActive(enable);
                // ARViewCanvas.SetActive(enable);
                // PrivacyPromptCanvas.SetActive(!enable);
                // VPSCheckCanvas.SetActive(false);
                case true when m_AsyncCheck == null:
                    m_AsyncCheck = Co_AvailabilityCheck();
                    StartCoroutine(m_AsyncCheck);
                    break;
                case false when m_AsyncCheck != null:
                    StopCoroutine(m_AsyncCheck);
                    m_AsyncCheck = null;
                    break;
            }
        }
        
        private void LifeCycleUpdate()
        {
            // Pressing 'back' button quits the app.
            if (Input.GetKeyUp(KeyCode.Escape))
                Application.Quit();

            // Only allow the screen to sleep when not tracking.
            Screen.sleepTimeout = ARSession.state != ARSessionState.SessionTracking
                ? SleepTimeout.SystemSetting
                : SleepTimeout.NeverSleep;

            // Quit the app if ARSession is in an error status.
            string returningReason = string.Empty;
            if (ARSession.state != ARSessionState.CheckingAvailability &&
                ARSession.state != ARSessionState.Ready &&
                ARSession.state != ARSessionState.SessionInitializing &&
                ARSession.state != ARSessionState.SessionTracking)
            {
                returningReason = string.Format(
                    "Geospatial sample encountered an ARSession error state {0}.\n" +
                    "Please start the app again.",
                    ARSession.state);
            }
            else if (Input.location.status == LocationServiceStatus.Failed)
            {
                returningReason =
                    "Geospatial sample failed to start location service.\n" +
                    "Please start the app again and grant precise location permission.";
            }
            else if (m_coreExtensions == null)
            {
                returningReason = string.Format(
                    "Geospatial sample failed with missing AR Components.");
            }

            ReturnWithReason(returningReason);
        }

        private void ReturnWithReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return;
            }

            // SetAnchorButton.gameObject.SetActive(false);
            // TerrainToggle.gameObject.SetActive(false);
            // ClearAllButton.gameObject.SetActive(false);
            // InfoPanel.SetActive(false);

            Debug.LogError(reason);
            m_IsReturning = true;
            Invoke(nameof(QuitApplication), m_errorDisplaySeconds);
        }

        private void QuitApplication() => Application.Quit();
        
        private void ShowEarthState()
        {
            string debugMsg = "No Message...";
            EarthState earthState = m_earthManager.EarthState;
            switch (earthState)
            {
                case EarthState.Enabled:
                    debugMsg = "Geospatial functionalities are enabled.";
                    break;
                case EarthState.ErrorInternal:
                    debugMsg = "Geospatial functionalities are not enabled due to an internal error.";
                    break;
                case EarthState.ErrorGeospatialModeDisabled:
                    debugMsg = "Geospatial functionalities are not enabled because the Geospatial Mode is disabled.";
                    break;
                case EarthState.ErrorNotAuthorized:
                    debugMsg =
                        "Geospatial functionalities are not enabled because the user has not authorized the application.";
                    break;
                case EarthState.ErrorResourcesExhausted:
                    debugMsg =
                        "Geospatial functionalities are not enabled because the device does not have enough resources.";
                    break;
                case EarthState.ErrorPackageTooOld:
                    debugMsg = "Geospatial functionalities are not enabled because the application package is too old.";
                    break;
                case EarthState.ErrorEarthNotReady:
                    debugMsg = "Geospatial functionalities are not enabled because the Earth is not ready.";
                    break;
                case EarthState.ErrorSessionNotReady:
                    debugMsg = "Geospatial functionalities are not enabled because the session is not ready.";
                    break;
            }

            HUD.InfoHUD.DebugMessage = debugMsg;
        }
        
        private bool IsFeatureSupported()
        {
            bool result = true;
            
            // Check feature support and enable Geospatial API when it's supported.
            switch (m_earthManager.IsGeospatialModeSupported(GeospatialMode.Enabled))
            {
                case FeatureSupported.Unknown:
                    result = false;
                    break;
                case FeatureSupported.Unsupported:
                    ReturnWithReason("Geospatial API is not supported by this devices.");
                    result = false;
                    break;
                case FeatureSupported.Supported:
                    if (m_coreExtensions.ARCoreExtensionsConfig.GeospatialMode == GeospatialMode.Disabled)
                    {
                        Debug.Log("Geospatial sample switched to GeospatialMode.Enabled.");
                        m_coreExtensions.ARCoreExtensionsConfig.GeospatialMode = GeospatialMode.Enabled;
                        m_configurePrepareTime = 3.0f;
                        m_EnablingGeospatial = true;
                        result = false;
                        break;
                    }
                    break;
            }
                    
            return result;
        }
        
        private bool IsEarthEnabled()
        {
            bool result = true;
            
            // Check earth state.
            EarthState earthState = m_earthManager.EarthState;
            if (earthState == EarthState.ErrorEarthNotReady)
            {
                result = false;
            }
            else if (earthState != EarthState.Enabled)
            {
                string errorMessage =
                    "Geospatial sample encountered an EarthState error: " + earthState;
                Debug.LogWarning(errorMessage);
                result = false;
            }
            
            return result;
        }
        
        private void ResolveHistory()
        {
            if (!_shouldResolvingHistory)
            {
                return;
            }

            _shouldResolvingHistory = false;
            foreach (var history in m_historyCollection.Collection)
            {
                PlaceGeospatialAnchor(history);
            }

            // ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
            // SnackBarText.text = string.Format("{0} anchor(s) set from history.",
            //     _anchorObjects.Count);
        }
        
        private void PlaceAnchorByScreenTap(Vector2 position)
        {
            List<ARRaycastHit> hitResults = new List<ARRaycastHit>();
            m_raycastManager.Raycast(
                position,
                hitResults,
                /*TrackableType.Planes |*/ TrackableType.FeaturePoint);
            if (hitResults.Count > 0)
            {
                GeospatialPose geospatialPose = m_earthManager.Convert(hitResults[0].pose);
                GeospatialAnchorHistory history = new GeospatialAnchorHistory(
                    geospatialPose.Latitude, 
                    geospatialPose.Longitude,
                    geospatialPose.Altitude,
                    geospatialPose.EunRotation);
                ARGeospatialAnchor anchor = PlaceGeospatialAnchor(history);
                if (anchor != null)
                    m_historyCollection.Collection.Add(history);

                // ClearAllButton.gameObject.SetActive(_anchorObjects.Count > 0);
                // SaveGeospatialAnchorHistory();
            }
        }
        
        private ARGeospatialAnchor PlaceGeospatialAnchor(GeospatialAnchorHistory history)
        {
            Quaternion eunRotation = history.EunRotation;
            if (eunRotation == Quaternion.identity)
            {
                // This history is from a previous app version and EunRotation was not used.
                eunRotation =
                    Quaternion.AngleAxis(180f - (float)history.Heading, Vector3.up);
            }

            ARGeospatialAnchor anchor = m_anchorManager.ResolveAnchorOnTerrain(
                    history.Latitude,
                    history.Longitude,
                    0,
                    eunRotation);
            
            if (anchor != null)
            {
                GameObject anchorGO = Instantiate(m_TerrainPrefab, anchor.transform);
                m_AnchorObjects.Add(anchor.gameObject);

                StartCoroutine(Co_CheckTerrainAnchorState(anchor));
            }
            else
            {
                // SnackBarText.text = string.Format(
                //     "Failed to set {0}!", terrain ? "a terrain anchor" : "an anchor");
            }

            return anchor;
        }
    }
}
