namespace BlackRece.LaSARTag
{
    using UnityEngine;
    
    public class GeoShooter : MonoBehaviour {
        public Camera _cam = null;
        
        [SerializeField] private GameObject _ProjectilePrefab;
        private ProjectilePooler.ProjectilePooler _pooler;
        
        [SerializeField] private float _FFingerDelayMax = 1f;
        [SerializeField] private float _MaxLifeTime = 10f;
        [SerializeField] private float _FFingerDelay;

        private void Awake() {
            _pooler = GetComponent<ProjectilePooler.ProjectilePooler>();
        }

        void Start()
        {
            _pooler.Init(_ProjectilePrefab);
        }

        // Update is called once per frame
        void Update()
        {
            if (_cam == null) {
                return;
                //transform.gameObject.SetActive(false);
            }
            
            var touch = Input.GetTouch(0);
            switch (touch.phase) {
                case TouchPhase.Began:
                    EmitProjectile();
                    break;
                case TouchPhase.Moved:
                    break;
                case TouchPhase.Stationary:
                    break;
                case TouchPhase.Ended:
                    break;
                case TouchPhase.Canceled:
                    break;
            }
        }

        private void EmitProjectile() {
            _pooler
                .GetGameObject()
                .GetComponent<Projectile>()
                .Init(_cam.transform);
        }
    }
}
