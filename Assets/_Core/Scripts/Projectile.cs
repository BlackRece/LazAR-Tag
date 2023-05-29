namespace BlackRece
{
    using UnityEngine;
    
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 20.0f;
        [SerializeField] private float _maxLifeTime = 5.0f;
        private float _lifeTime;
        
        private Vector3 _vDir;
        
        public void Init(Transform tParent)
        {
            Transform tTransform = transform;
            tTransform.position = tParent.position;
            tTransform.rotation = tParent.rotation;
            transform.Translate(tTransform.forward * 2, Space.World);
            
            _lifeTime = _maxLifeTime;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            transform.Translate(transform.forward * _speed * Time.deltaTime, Space.World);

            _lifeTime -= Time.deltaTime;
            if (_lifeTime <= 0)
                gameObject.SetActive(false);
        }
    }
}
