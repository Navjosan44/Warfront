using System.Collections.Generic;
using UnityEngine;

namespace Warfront.Game
{
    /// <summary>
    /// Reusable GameObject pool. Pooled objects are created once, parked inactive and rented out.
    /// This is what keeps combat free of per-shot Instantiate/Destroy churn.
    /// </summary>
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly Stack<GameObject> _available;
        private readonly HashSet<GameObject> _rented;
        private readonly int _maxSize;
        private readonly bool _expandable;

        public ObjectPool(GameObject prefab, Transform root, int prewarm, int maxSize)
        {
            _prefab = prefab;
            _root = root;
            _maxSize = Mathf.Max(1, maxSize);
            _expandable = true;
            _available = new Stack<GameObject>(Mathf.Max(1, prewarm));
            _rented = new HashSet<GameObject>();

            for (int i = 0; i < prewarm; i++)
            {
                GameObject instance = CreateInstance();
                instance.SetActive(false);
                _available.Push(instance);
            }
        }

        public int AvailableCount { get { return _available.Count; } }
        public int RentedCount { get; private set; }
        public int TotalCreated { get; private set; }

        private GameObject CreateInstance()
        {
            GameObject instance = Object.Instantiate(_prefab, _root);
            instance.name = _prefab.name;
            TotalCreated++;
            return instance;
        }

        public GameObject Rent()
        {
            GameObject instance = null;
            while (_available.Count > 0 && instance == null)
            {
                instance = _available.Pop();
            }

            if (instance == null)
            {
                if (!_expandable || TotalCreated >= _maxSize) return null;
                instance = CreateInstance();
            }

            instance.SetActive(true);
            _rented.Add(instance);
            RentedCount = _rented.Count;
            return instance;
        }

        public GameObject Rent(Vector3 position, Quaternion rotation)
        {
            GameObject instance = Rent();
            if (instance == null) return null;
            Transform t = instance.transform;
            t.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>Returns an object to the pool. Safe to call twice.</summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            if (!_rented.Remove(instance)) return;
            instance.SetActive(false);
            if (_root != null) instance.transform.SetParent(_root, false);
            _available.Push(instance);
            RentedCount = _rented.Count;
        }

        /// <summary>Safety net for mission teardown: reclaims anything left checked out.</summary>
        public void ReturnAll()
        {
            if (_rented.Count == 0) return;
            GameObject[] snapshot = new GameObject[_rented.Count];
            _rented.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++) Return(snapshot[i]);
        }
    }

    /// <summary>Pools keyed by id, created lazily from prefabs built at runtime.</summary>
    public class PoolRegistry
    {
        private readonly Dictionary<string, ObjectPool> _pools = new Dictionary<string, ObjectPool>(16);
        private readonly Transform _root;

        public PoolRegistry(Transform root)
        {
            _root = root;
        }

        public void Register(string id, GameObject prefab, int prewarm, int maxSize)
        {
            if (string.IsNullOrEmpty(id) || prefab == null) return;
            if (_pools.ContainsKey(id)) return;
            GameObject container = new GameObject("Pool_" + id);
            container.transform.SetParent(_root, false);
            _pools[id] = new ObjectPool(prefab, container.transform, prewarm, maxSize);
        }

        public bool Has(string id)
        {
            return !string.IsNullOrEmpty(id) && _pools.ContainsKey(id);
        }

        public GameObject Rent(string id, Vector3 position, Quaternion rotation)
        {
            ObjectPool pool;
            if (string.IsNullOrEmpty(id) || !_pools.TryGetValue(id, out pool)) return null;
            return pool.Rent(position, rotation);
        }

        public void Return(string id, GameObject instance)
        {
            ObjectPool pool;
            if (string.IsNullOrEmpty(id) || !_pools.TryGetValue(id, out pool)) return;
            pool.Return(instance);
        }

        public int TotalCreated
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, ObjectPool> kv in _pools) total += kv.Value.TotalCreated;
                return total;
            }
        }

        public int TotalRented
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, ObjectPool> kv in _pools) total += kv.Value.RentedCount;
                return total;
            }
        }

        /// <summary>Returns every rented object of every pool (mission teardown safety net).</summary>
        public void ReturnAll()
        {
            foreach (KeyValuePair<string, ObjectPool> kv in _pools) kv.Value.ReturnAll();
        }
    }
}
