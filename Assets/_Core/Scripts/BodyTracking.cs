
namespace BlackRece.LaSARTag.BodyTracking
{
    using System;
    using System.IO;
    using System.Numerics;
    using System.Runtime.InteropServices;

    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    using UnityEngine;
    using UnityEngine.UI;
    
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;

    // using OpenCvSharp;
    // using OpenCvSharp.Tracking;

    [RequireComponent(typeof(ARCameraManager))]
    public class BodyTracking : MonoBehaviour
    {
        //https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.0/manual/features/Camera/image-capture.html
        [SerializeField] private GameObject _DebugImageObj;
        private RawImage _debugRawImage;

        [SerializeField] private GameObject _DetectedImageObj;
        private RawImage _detectedRawImage;
        
        private RenderTexture _renderTexture;
        private Camera _camera;
        private ARCameraManager _camManager;

        private Texture2D _camTexture;
        private XRCpuImage.Transformation _transformation;
        private const TextureFormat DefaultTextureFormat = TextureFormat.RGBA32;

        //private CascadeClassifier _body_cascade;
        // private HOGDescriptor _hog;
        // private GCHandle _pixelHandle;
        
        [SerializeField] private string _modelAssetName = "frozen_inference_graph.pb";
        [SerializeField] private string _configAssetName = "pipeline.config";
        [SerializeField] private string _bundleName = "tensorflowmodels";
        
        #region Unity Events
        
        protected void Awake()
        {
            _debugRawImage = _DebugImageObj.GetComponent<RawImage>();
            Debug.Assert(_DebugImageObj == null, "DebugImageObj is null");

            _detectedRawImage = _DetectedImageObj.GetComponent<RawImage>();
            Debug.Assert(_DetectedImageObj == null, "DetectedImageObj is null");
            
            _camManager = GetComponent<ARCameraManager>();

            _camTexture = null;
            //_hog.SetSVMDetector(HOGDescriptor.GetDefaultPeopleDetector());
        }

        private void OnEnable() 
            => _camManager.frameReceived += OnCameraFrameReceived;
        
        private void OnDisable()
            => _camManager.frameReceived -= OnCameraFrameReceived;

        private void OnApplicationQuit()
        {
            // _pixelHandle.Free();
        }

        private void Start()
        {
            _transformation = XRCpuImage.Transformation.None;
        }
        
        private void Update()
        {
            DetectKeypoints();

            //DetectBodies();
            
            //UpdateDetectImage();
        }
        
        private void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs = rhs;
            rhs = temp;
        }
        
        #endregion // Unity Events
        
        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
            => UpdateCameraImage();
        
        private void DetectBodies()
        {
            // Vector<OpenCvSharp.Rect> vecFound;
            // Color32[] pixel32 = _camTexture.GetPixels32();
            // _pixelHandle = GCHandle.Alloc(pixel32, GCHandleType.Pinned);
            // IntPtr pixelPtr = _pixelHandle.AddrOfPinnedObject();
            //
            // Mat currentFrame = new Mat(_camTexture.height, _camTexture.width, MatType.CV_8UC4/*, pixelPtr*/);
            // Debug.Log("currentFrame: " + currentFrame.Size().Width + "x" + currentFrame.Size().Height);
            // //Cv2.Resize(pixel32, currentFrame, new OpenCvSharp.Size(_camTexture.width, _camTexture.height));
            // Mat argb_img = new Mat();
            // Cv2.CvtColor(currentFrame, argb_img, ColorConversionCodes.BGRA2BGR);
            // Mat[] bgra;
            // Cv2.Split(argb_img, out bgra);
            // Swap<Mat>(ref bgra[0], ref bgra[3]);
            // Swap(ref bgra[1], ref bgra[2]);
            // Mat bgra_img = new Mat();
            // Cv2.Merge(bgra, bgra_img);
            // Debug.Log("bgra_img: " + bgra_img.Size().Width + "x" + bgra_img.Size().Height);
            // _detectedRawImage.texture = Unity.MatToTexture(bgra_img);
        }
        
        private void DetectKeypoints()
        {
            if(_camTexture == null)
                return;
            
            // get camera frame and store as OpenCvSharp Mat
            //Mat camMat = Unity.TextureToMat(_camTexture);
            // Mat camMat = Mat_UnityMethods.TextureToMat(_camTexture);
            // HUD.InfoHUD.TextureSize = new UnityEngine.Vector2(camMat.Size().Width, camMat.Size().Height);
            //
            // // detect keypoints
            // KeyPoint[] keyPoints = _detector.Detect(camMat);
            // HUD.InfoHUD.KeyPoints = keyPoints.Length;
            //
            // // create empty Mat to draw keypoints on
            // using (Mat markedMat = new Mat(camMat.Size(), MatType.CV_8SC4))
            // {
            //     // draw keypoints on empty Mat
            //     Cv2.DrawKeypoints(
            //         camMat,
            //         keyPoints,
            //         markedMat,
            //         Scalar.Red,
            //         DrawMatchesFlags.DrawRichKeypoints
            //     );
            //     
            //     // convert Mat to Texture2D
            //     Texture2D resultTexture = Unity.MatToTexture(markedMat);
            //     
            //     // set result texture to display image
            //     _detectedRawImage.texture = resultTexture;
            // }
            // camMat.Dispose();
        }
        
        private void UpdateDetectImage()
        {
            if (_camTexture == null)
                return;
            
            
            
            _detectedRawImage.texture = _camTexture;
        }
        private unsafe void UpdateCameraImage()
        {
            if (!_camManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
                return;

            if (
                _camTexture == null ||
                _camTexture.width != cpuImage.width ||
                _camTexture.height != cpuImage.height
            )
            {
                _camTexture = new Texture2D(
                    cpuImage.width,
                    cpuImage.height,
                    DefaultTextureFormat,
                    false
                );
            }
            
            XRCpuImage.ConversionParams conversionParams = new XRCpuImage.ConversionParams(
                cpuImage,
                DefaultTextureFormat,
                _transformation
            );
            
            NativeArray<byte> rawTextureData = _camTexture.GetRawTextureData<byte>();
            try
            {
                cpuImage.Convert(
                    conversionParams,
                    new IntPtr(rawTextureData.GetUnsafePtr()),
                    rawTextureData.Length
                );
            }
            finally
            {
                cpuImage.Dispose();
            }

            _camTexture.Apply();

            _debugRawImage.texture = _camTexture;
        }

        private void LoadAssetBundle()
        {
            // load model
            AssetBundle localAssetBundle = AssetBundle
                .LoadFromFile(Path.Combine(Application.streamingAssetsPath, _bundleName));

            if (localAssetBundle == null)
            {
                Debug.LogError("Failed to load AssetBundle!");
                return;
            }

            // convert model from bundle to byte array
            //var modelAsset = localAssetBundle.LoadAsset<TextAsset>(_modelAssetName);
            TextAsset modelAsset = localAssetBundle.LoadAsset<TextAsset>(_modelAssetName);
            TextAsset configAsset = localAssetBundle.LoadAsset<TextAsset>(_configAssetName);

            //var net = OpenCvSharp.Dnn.ReadFromTensorflow(modelAsset.bytes, configAsset.bytes); 
            //var net = OpenCvSharp.CvObject.Dnn.ReadFromTensorflow(modelAsset.bytes, configAsset.bytes); 
            //CvDnn.modelAsset.bytes
            localAssetBundle.Unload(false);
        }
    }
}
