using System;

namespace BlackRece.ProjectilePooler
{
    using System.Collections.Generic;

    using UnityEngine;

    public class ProjectilePooler : MonoBehaviour
    {
        [SerializeField] private int _batchAmount = 10;
        private GameObject _prefab;
        private GameObject _container;
        private List<GameObject> _pool;

        private GameObject CreatePrefabInstance() {
            var instance = Instantiate(_prefab, _container.transform);
            instance.SetActive(false);
            return instance;
        }
        
        public GameObject GetGameObject(bool bHasAlreadyAddedObjects = false) {
            foreach (GameObject inactiveObject in _pool) 
                if (!inactiveObject.activeSelf) 
                    return inactiveObject;

            if (bHasAlreadyAddedObjects)
                throw new Exception("ERROR: Can't return an inactive object!");
                
            IncreasePool();
            return GetGameObject(!bHasAlreadyAddedObjects);
        }
        
        private void IncreasePool()
        {
            for (var i = 0; i < _batchAmount; i++)
                _pool.Add(CreatePrefabInstance());
        }
        
        public void Init(GameObject prefab) 
        {
            _container = new GameObject();
            _pool = new List<GameObject>();

            _prefab = prefab;
            
            _container.name = "Projectiles";

            IncreasePool();
        }
        
        private void OnDestroy()
        {
            for (int i = 0; i < _pool.Count; i++)
                Destroy(_pool[i]);
            
            _pool.Clear();

            Destroy(_container);
        }
        
    }
}
