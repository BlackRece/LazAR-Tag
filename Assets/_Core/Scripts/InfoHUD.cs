namespace BlackRece.HUD
{
    using UnityEngine;
    using TMPro;

    public class InfoHUD : MonoBehaviour
    {
        public struct GPSData
        {
            public float Latitude;
            public float Longitude;
            public float Altitude;
            public float HorizontalAccuracy;
            public double Timestamp;

            public string Log;
            
            public override string ToString()
            {
                return
                    // "GPS Data:" +
                    "\n Lat: " + Latitude + 
                    "\n Lon: " + Longitude +
                    "\n Alt: " + Altitude +
                    "\n HorizontalAccuracy: " + HorizontalAccuracy + 
                    "\n TimeStamp: " + Timestamp;
            }
        }
        
        [SerializeField] private bool _DEBUG = false;
        
        private Camera _camera;
        private TMP_Text _statusText;
        
        public static GPSData Location { get; set; }
        
        public static string DeviceName { get; set; }
        public static Vector2 TextureSize { get; set; }
        public static Vector2 OverlaySize { get; set; }
        public static Vector2 PreScale { get; set; }
        public static Vector2 PostScale { get; set; }
        public static int KeyPoints { get; set; }
        
        public static bool IsFingerDown { get; set; }
        public static bool HasFingerTriggered { get; set; }
        
        //public static OpenCvSharp.Unity.TextureConversionParams TextureParams { get; set; }
        public static bool IsParamsUpdated;
        public static bool IsTextureUpdated;
        public static bool HasTexture;
        public static string DebugMessage { get; set; }
        public static string AnchorMessage { get; set; }

        public static bool IsLibEnabled { get; set; }

        private void Awake()
        {
            // get camera
            _camera = Camera.main;
            
            // get text
            _statusText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            //TextureParams = new OpenCvSharp.Unity.TextureConversionParams();
            IsParamsUpdated = false;
        }

        private void Update()
        {
            if (!_DEBUG)
                return;
            
            string sGPSLog = string.IsNullOrEmpty(Location.Log) ? "No GPS Log" : Location.Log;
            string sParamsUpdated = IsParamsUpdated ? "true" : "false";
            // string sFlipVert = TextureParams.FlipVertically ? "true" : "false";
            // string sFlipHorz = TextureParams.FlipHorizontally ? "true" : "false";
            string sHasTexture = HasTexture ? "true" : "false";
            string sIsTextureUpdated = IsTextureUpdated ? "true" : "false";
            
            string sFingerTriggered = HasFingerTriggered ? "true" : "false";
            
            string sLibEnabled = IsLibEnabled ? "true" : "false";
            
            Transform camTransform = _camera.transform;
            
            _statusText.text =
                //$"Camera Device: {DeviceName}" +
                $"Camera Device: {_camera.name}" +
                $"\nCamera position: {camTransform.position}" +
                $"\nCamera rotation: {camTransform.rotation}" +
                "\n ---" +
                $"\nGPS Log: {sGPSLog}" +
                $"\nGPS Data: {Location.ToString()}" +
                "\n ---" +
                $"\nTexture Info:" +
                // $"\nTexture Params Updated: {sParamsUpdated}" +
                $"\nTexture size: {TextureSize}" + 
                // $"\nTexture FlipVert: {sFlipVert}" +
                // $"\nTexture FlipHorz: {sFlipHorz}" +
                // $"\nTexture Rotation: {TextureParams.RotationAngle}" +
                // $"\nTexture Updated: {sIsTextureUpdated}" +
                $"\nTexture Exists: {sHasTexture}" +
                "\n ---" +
                $"\nKeyPoints: {KeyPoints}" +
                // $"\nPreScale size: {PreScale}" +
                // $"\nPostScale size: {PostScale}" +
                // $"\nOverlay size: {OverlaySize}" +
                "\n ---" +
                $"\nTrigger Finger: {sFingerTriggered}" +
                // "\n ---" +
                // $"\nOpenCV Library Enabled: {sLibEnabled}" +
                "\n ---" +
                $"\nDebug Message: {DebugMessage}" +
                $"\nAnchor Message: {AnchorMessage}";
        }
    }
}
