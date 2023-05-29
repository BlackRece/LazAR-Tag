
namespace BlackRece.BlackRece
{
    using System.Collections;
    
    using UnityEngine;
    using UnityEngine.Android;
    
    public class GPSLocation : MonoBehaviour
    {
        [SerializeField] private float _distanceAccuracy = 0.1f;
        [SerializeField] private float _updateDistance = 0.1f;
        [SerializeField] private int _maxWait = 20;
        
        private HUD.InfoHUD.GPSData _location;
        private int _maxWaitCount;
        
        private LocationPermission _permission;
        
        private void Start()
        {
            _location = new HUD.InfoHUD.GPSData();
            _maxWaitCount = _maxWait;

            _permission = new LocationPermission();
            
            StartCoroutine(GetLocation());
        }
        
        private IEnumerator GetLocation()
        {
            if (!Input.location.isEnabledByUser)
            {
                _location.Log = "User has not enabled GPS";
                yield break;
            }
            else
            {
                if (!_permission.IsGranted)
                {
                    _permission.GetPermission();
                    yield return new WaitForSeconds(1);    
                }
            }
            
            Input.location.Start(_distanceAccuracy, _updateDistance);

            int maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                _location.Log = "Initializing GPS ... (" + maxWait + ")";
                yield return new WaitForSeconds(1);
                maxWait--;
            }
            
            if (maxWait < 1)
            {
                _location.Log = "Timed out";
                yield break;
            }
            
            if (Input.location.status == LocationServiceStatus.Failed)
            {
                _location.Log = "Unable to determine device location";
                yield break;
            }
            else
            {
                _location.Log = "Running";
                
                LocationInfo newLocation = Input.location.lastData;
                _location.Latitude =  newLocation.latitude;
                _location.Longitude = newLocation.longitude;
                _location.Altitude = newLocation.altitude;
                _location.HorizontalAccuracy = newLocation.horizontalAccuracy;
                _location.Timestamp = newLocation.timestamp;
                
                HUD.InfoHUD.Location = _location;
            }

            //yield return ProcessLocation();
            
            Input.location.Stop();
        }
        
        private IEnumerator ProcessLocation()
        {
            switch (Input.location.status)
            {
                case LocationServiceStatus.Stopped:
                    _location.Log = "GPS Stopped";
                    break;
                case LocationServiceStatus.Initializing:
                    _location.Log = "GPS Initializing";
                    
                    while (Input.location.status == LocationServiceStatus.Initializing && _maxWaitCount > 0)
                    {
                        _location.Log = "Initializing GPS ... (" + _maxWaitCount + ")";
                        yield return new WaitForSeconds(1);
                        _maxWaitCount--;
                    }
            
                    if (_maxWaitCount < 1)
                    {
                        _location.Log = "Timed out";
                        yield break;
                    }
                    break;
                case LocationServiceStatus.Failed:
                    _location.Log = "GPS Failed : Unable to determine device location";
                    break;
                case LocationServiceStatus.Running:
                default:
                    _location.Log = "GPS Running";
                    
                    LocationInfo newLocation = Input.location.lastData;
                    _location.Latitude =  newLocation.latitude;
                    _location.Longitude = newLocation.longitude;
                    _location.Altitude = newLocation.altitude;
                    _location.HorizontalAccuracy = newLocation.horizontalAccuracy;
                    _location.Timestamp = newLocation.timestamp;
                
                    HUD.InfoHUD.Location = _location;
                    break;
            }
            yield return null;
        }
    }

    public class LocationPermission
    {
        private PermissionCallbacks m_Callbacks;

        private bool m_bIsGranted;
        public bool IsGranted => m_bIsGranted;

        public LocationPermission()
        {
            m_Callbacks = new PermissionCallbacks();
            m_Callbacks.PermissionGranted += PermitGranted;
            m_Callbacks.PermissionDenied += PermitDenied;
            m_Callbacks.PermissionDeniedAndDontAskAgain += PermitDeniedDontAsk;
            
            m_bIsGranted = Permission.HasUserAuthorizedPermission(Permission.FineLocation);
        }

        ~LocationPermission()
        {
            m_Callbacks.PermissionGranted -= PermitGranted;
            m_Callbacks.PermissionDenied -= PermitDenied;
            m_Callbacks.PermissionDeniedAndDontAskAgain -= PermitDeniedDontAsk;
        }
        
        public void GetPermission()
        {
            Permission.RequestUserPermission(Permission.FineLocation, m_Callbacks);
        }

        private void PermitDeniedDontAsk(string sPermissionName)
        {
            m_bIsGranted = false;
            Debug.Log($"{sPermissionName} is denied and don't ask again");
        }

        private void PermitDenied(string sPermissionName)
        {
            m_bIsGranted = false;
            Debug.Log($"{sPermissionName} is denied");
        }

        private void PermitGranted(string sPermissionName)
        {
            m_bIsGranted = true;
            Debug.Log($"{sPermissionName} is granted");
        }
    }
}