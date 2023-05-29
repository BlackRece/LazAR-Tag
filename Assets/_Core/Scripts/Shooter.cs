using Google.XR.ARCoreExtensions;

namespace BlackRece.LaSARTag
{
    using System;
    using UnityEngine;
    
    using Geospatial;

    [RequireComponent(typeof(Camera))]
    public class Shooter : MonoBehaviour
    {
        [SerializeField] private bool _Debug;
        [SerializeField] private GameObject _ProjectilePrefab;

        [SerializeField] private float _FFingerDelayMax = 1f;
        [SerializeField] private float _MaxLifeTime = 10f;
        [SerializeField] private float _FFingerDelay;
        private bool _bIsFingerDown;
        
        private Camera _camera;
        private bool _isAnchorSet;
        private ARGeospatialAnchor _anchor;
        private ProjectilePooler.ProjectilePooler _pooler;

        private void Awake()
        {
            _isAnchorSet = false;
                
            _camera = GetComponent<Camera>();
            _pooler = GetComponent<ProjectilePooler.ProjectilePooler>();
        }

        private void Start()
        {
            _pooler.Init(_ProjectilePrefab);
        }

        private void Update()
        {
            HandleTouches();
            //
            // if (GeospatialController._IsEarthTracking)
            // {
            //     if (!_isAnchorSet)
            //     {
            //         _anchor = GeospatialController._GetGeoPosition;
            //         _isAnchorSet = true;
            //     }
            // }
        }

        private void HandleTouches()
        {
            if(Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        EmitProjectile();
                        _bIsFingerDown = true;
                        //OnFingerDown();
                        break;
                    case TouchPhase.Ended:
                        _bIsFingerDown = false;
                        break;
                    case TouchPhase.Moved: break;
                    case TouchPhase.Stationary: break;
                    case TouchPhase.Canceled: break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        private void EmitProjectile()
        {
            _pooler
                .GetGameObject()
                .GetComponent<Projectile>()
                .Init(_anchor.transform);
        }
    }
}
