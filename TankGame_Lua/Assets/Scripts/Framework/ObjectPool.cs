using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Framework
{
    public enum PoolEvictionPolicy
    {
        LRU,
        LFU,
        FIFO
    }

    public class PoolStatistics
    {
        public string PrefabPath { get; set; }
        public int AvailableCount { get; set; }
        public int ActiveCount { get; set; }
        public int TotalCount { get; set; }
        public long AccessCount { get; set; }
        public float UtilizationRate { get; set; }
    }

    public class ObjectPool : MonoBehaviour
    {
        private static ObjectPool _instance;

        public static ObjectPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ObjectPool>();
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject("ObjectPool");
                        _instance = singletonObject.AddComponent<ObjectPool>();
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, Stack<GameObject>> _poolDictionary = new Dictionary<string, Stack<GameObject>>();
        private readonly Dictionary<string, HashSet<GameObject>> _activeDictionary = new Dictionary<string, HashSet<GameObject>>();
        private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, int> _warmPoolConfig = new Dictionary<string, int>();

        // Global indexes
        private readonly HashSet<GameObject> _idleGlobal = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, string> _objectToPrefab = new Dictionary<GameObject, string>();

        // Metrics
        private readonly Dictionary<GameObject, long> _accessCount = new Dictionary<GameObject, long>();
        private readonly Dictionary<GameObject, long> _lastAccessTime = new Dictionary<GameObject, long>();

        // FIFO with O(1) remove
        private readonly LinkedList<GameObject> _creationOrderList = new LinkedList<GameObject>();
        private readonly Dictionary<GameObject, LinkedListNode<GameObject>> _creationNodeMap = new Dictionary<GameObject, LinkedListNode<GameObject>>();

        private Transform _poolContainer;
        private PoolEvictionPolicy _evictionPolicy = PoolEvictionPolicy.LRU;

        private int _maxTotalPoolSize = 1000;
        private int _totalCount = 0;
        private long _globalTimestamp = 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _poolContainer = transform;
        }

        public void SetEvictionPolicy(PoolEvictionPolicy policy)
        {
            _evictionPolicy = policy;
            Debug.Log($"Pool eviction policy changed to: {policy}");
        }

        public void SetMaxPoolSize(int maxSize)
        {
            _maxTotalPoolSize = Mathf.Max(1, maxSize);
            Debug.Log($"Global pool size limit set to: {_maxTotalPoolSize}");
        }

        public void ConfigureWarmPool(string prefabPath, int warmCount)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("Prefab path cannot be null or empty");
                return;
            }

            _warmPoolConfig[prefabPath] = Mathf.Max(0, warmCount);
        }

        public void InitializeWarmPool()
        {
            foreach (var kv in _warmPoolConfig)
            {
                Preload(kv.Key, kv.Value);
                Debug.Log($"Warm pool initialized for {kv.Key}: {kv.Value} objects");
            }
        }

        public GameObject Get(string prefabPath, Vector3 position = default, Quaternion rotation = default)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("Prefab path cannot be null or empty");
                return null;
            }

            GameObject obj = null;

            if (_poolDictionary.TryGetValue(prefabPath, out var idleStack) && idleStack.Count > 0)
            {
                obj = idleStack.Pop();
                RemoveFromIdleIndexes(obj);
            }
            else
            {
                if (_totalCount >= _maxTotalPoolSize)
                {
                    bool evicted = TryEvictOneIdleObject();
                    if (!evicted)
                    {
                        Debug.LogWarning($"Pool is full ({_maxTotalPoolSize}) and no idle object can be evicted. Get({prefabPath}) returns null.");
                        return null;
                    }
                }

                obj = CreateNewObject(prefabPath);
                if (obj == null)
                {
                    return null;
                }
            }

            EnsureActiveSet(prefabPath).Add(obj);

            obj.transform.SetParent(null, true);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            Touch(obj);
            return obj;
        }

        public void Return(string prefabPath, GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogError("Return failed: obj is null");
                return;
            }

            // Prefer trusted reverse mapping; fallback to param for compatibility.
            if (!_objectToPrefab.TryGetValue(obj, out var actualPath))
            {
                actualPath = prefabPath;
                if (string.IsNullOrEmpty(actualPath))
                {
                    Debug.LogError("Return failed: cannot resolve prefab path.");
                    return;
                }
                _objectToPrefab[obj] = actualPath;
            }

            if (_activeDictionary.TryGetValue(actualPath, out var activeSet))
            {
                activeSet.Remove(obj);
            }

            obj.SetActive(false);
            obj.transform.SetParent(_poolContainer, false);

            EnsureIdleStack(actualPath).Push(obj);
            AddToIdleIndexes(obj);
        }

        public void Preload(string prefabPath, int count)
        {
            if (string.IsNullOrEmpty(prefabPath) || count <= 0)
            {
                Debug.LogError("Invalid prefab path or count");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (_totalCount >= _maxTotalPoolSize)
                {
                    bool evicted = TryEvictOneIdleObject();
                    if (!evicted)
                    {
                        // No room and cannot evict (all active), stop preloading.
                        break;
                    }
                }

                var obj = CreateNewObject(prefabPath);
                if (obj == null) break;

                obj.SetActive(false);
                obj.transform.SetParent(_poolContainer, false);

                EnsureIdleStack(prefabPath).Push(obj);
                AddToIdleIndexes(obj);
            }
        }

        public void Clear(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return;

            if (_poolDictionary.TryGetValue(prefabPath, out var idleStack))
            {
                while (idleStack.Count > 0)
                {
                    var obj = idleStack.Pop();
                    DestroyPooledObject(obj);
                }
                _poolDictionary.Remove(prefabPath);
            }

            if (_activeDictionary.TryGetValue(prefabPath, out var activeSet))
            {
                foreach (var obj in activeSet.ToList())
                {
                    DestroyPooledObject(obj);
                }
                _activeDictionary.Remove(prefabPath);
            }

            _prefabCache.Remove(prefabPath);
            _warmPoolConfig.Remove(prefabPath);
        }

        public void ClearAll()
        {
            foreach (var stack in _poolDictionary.Values)
            {
                while (stack.Count > 0)
                {
                    DestroyPooledObject(stack.Pop());
                }
            }
            _poolDictionary.Clear();

            foreach (var set in _activeDictionary.Values)
            {
                foreach (var obj in set.ToList())
                {
                    DestroyPooledObject(obj);
                }
            }
            _activeDictionary.Clear();

            _prefabCache.Clear();
            _warmPoolConfig.Clear();

            _idleGlobal.Clear();
            _objectToPrefab.Clear();
            _accessCount.Clear();
            _lastAccessTime.Clear();
            _creationOrderList.Clear();
            _creationNodeMap.Clear();

            _totalCount = 0;
        }

        public int GetAvailableCount(string prefabPath)
        {
            return _poolDictionary.TryGetValue(prefabPath, out var stack) ? stack.Count : 0;
        }

        public int GetActiveCount(string prefabPath)
        {
            return _activeDictionary.TryGetValue(prefabPath, out var set) ? set.Count : 0;
        }

        public int GetTotalCount(string prefabPath)
        {
            return GetAvailableCount(prefabPath) + GetActiveCount(prefabPath);
        }

        public PoolStatistics GetStatistics(string prefabPath)
        {
            int available = GetAvailableCount(prefabPath);
            int active = GetActiveCount(prefabPath);
            int total = available + active;

            long access = 0;
            if (_activeDictionary.TryGetValue(prefabPath, out var activeSet))
            {
                foreach (var obj in activeSet)
                {
                    if (_accessCount.TryGetValue(obj, out var c)) access += c;
                }
            }
            if (_poolDictionary.TryGetValue(prefabPath, out var idleStack))
            {
                foreach (var obj in idleStack)
                {
                    if (_accessCount.TryGetValue(obj, out var c)) access += c;
                }
            }

            return new PoolStatistics
            {
                PrefabPath = prefabPath,
                AvailableCount = available,
                ActiveCount = active,
                TotalCount = total,
                AccessCount = access,
                UtilizationRate = total > 0 ? (float)active / total : 0f
            };
        }

        public void PrintPoolStatistics()
        {
            Debug.Log("========== Object Pool Statistics ==========");
            Debug.Log($"Global Total: {_totalCount}, Max: {_maxTotalPoolSize}, IdleGlobal: {_idleGlobal.Count}");

            var allPrefabs = new HashSet<string>();
            allPrefabs.UnionWith(_poolDictionary.Keys);
            allPrefabs.UnionWith(_activeDictionary.Keys);

            foreach (var prefabPath in allPrefabs)
            {
                var stats = GetStatistics(prefabPath);
                Debug.Log($"[{prefabPath}] Available: {stats.AvailableCount}, Active: {stats.ActiveCount}, Total: {stats.TotalCount}, Access: {stats.AccessCount}, Utilization: {stats.UtilizationRate:P}");
            }

            Debug.Log("===========================================");
        }

        private GameObject CreateNewObject(string prefabPath)
        {
            if (!_prefabCache.TryGetValue(prefabPath, out var prefab))
            {
                prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"Failed to load prefab: {prefabPath}");
                    return null;
                }
                _prefabCache[prefabPath] = prefab;
            }

            var obj = Instantiate(prefab, _poolContainer);
            obj.name = prefab.name;

            _objectToPrefab[obj] = prefabPath;
            _accessCount[obj] = 0;

            _globalTimestamp++;
            _lastAccessTime[obj] = _globalTimestamp;

            var node = _creationOrderList.AddLast(obj);
            _creationNodeMap[obj] = node;

            _totalCount++;
            return obj;
        }

        private bool TryEvictOneIdleObject()
        {
            if (_idleGlobal.Count == 0) return false;

            GameObject victim = null;
            switch (_evictionPolicy)
            {
                case PoolEvictionPolicy.LRU:
                    victim = SelectIdleVictimLRU();
                    break;
                case PoolEvictionPolicy.LFU:
                    victim = SelectIdleVictimLFU();
                    break;
                case PoolEvictionPolicy.FIFO:
                    victim = SelectIdleVictimFIFO();
                    break;
            }

            if (victim == null) return false;
            DestroyPooledObject(victim);
            return true;
        }

        private GameObject SelectIdleVictimLRU()
        {
            GameObject victim = null;
            long minTs = long.MaxValue;

            foreach (var obj in _idleGlobal)
            {
                var ts = _lastAccessTime.TryGetValue(obj, out var t) ? t : long.MinValue;
                if (ts < minTs)
                {
                    minTs = ts;
                    victim = obj;
                }
            }
            return victim;
        }

        private GameObject SelectIdleVictimLFU()
        {
            GameObject victim = null;
            long minHit = long.MaxValue;
            long oldestTs = long.MaxValue; // Tie-breaker

            foreach (var obj in _idleGlobal)
            {
                long hit = _accessCount.TryGetValue(obj, out var h) ? h : 0;
                long ts = _lastAccessTime.TryGetValue(obj, out var t) ? t : 0;

                if (hit < minHit || (hit == minHit && ts < oldestTs))
                {
                    minHit = hit;
                    oldestTs = ts;
                    victim = obj;
                }
            }
            return victim;
        }

        private GameObject SelectIdleVictimFIFO()
        {
            var node = _creationOrderList.First;
            while (node != null)
            {
                var obj = node.Value;
                if (obj != null && _idleGlobal.Contains(obj))
                {
                    return obj;
                }
                node = node.Next;
            }
            return null;
        }

        private void DestroyPooledObject(GameObject obj)
        {
            if (obj == null) return;

            if (_objectToPrefab.TryGetValue(obj, out var prefabPath))
            {
                if (_poolDictionary.TryGetValue(prefabPath, out var idleStack))
                {
                    RemoveFromStack(idleStack, obj);
                }
                if (_activeDictionary.TryGetValue(prefabPath, out var activeSet))
                {
                    activeSet.Remove(obj);
                }
            }

            RemoveFromIdleIndexes(obj);

            _accessCount.Remove(obj);
            _lastAccessTime.Remove(obj);
            _objectToPrefab.Remove(obj);

            if (_creationNodeMap.TryGetValue(obj, out var node))
            {
                _creationOrderList.Remove(node);
                _creationNodeMap.Remove(obj);
            }

            _totalCount = Mathf.Max(0, _totalCount - 1);
            Destroy(obj);
        }

        private void AddToIdleIndexes(GameObject obj)
        {
            _idleGlobal.Add(obj);
        }

        private void RemoveFromIdleIndexes(GameObject obj)
        {
            _idleGlobal.Remove(obj);
        }

        private void Touch(GameObject obj)
        {
            _globalTimestamp++;
            _lastAccessTime[obj] = _globalTimestamp;

            if (!_accessCount.ContainsKey(obj))
            {
                _accessCount[obj] = 0;
            }
            _accessCount[obj]++;
        }

        private HashSet<GameObject> EnsureActiveSet(string prefabPath)
        {
            if (!_activeDictionary.TryGetValue(prefabPath, out var set))
            {
                set = new HashSet<GameObject>();
                _activeDictionary[prefabPath] = set;
            }
            return set;
        }

        private Stack<GameObject> EnsureIdleStack(string prefabPath)
        {
            if (!_poolDictionary.TryGetValue(prefabPath, out var stack))
            {
                stack = new Stack<GameObject>();
                _poolDictionary[prefabPath] = stack;
            }
            return stack;
        }

        private static void RemoveFromStack(Stack<GameObject> stack, GameObject target)
        {
            if (stack == null || stack.Count == 0) return;

            var temp = new Stack<GameObject>(stack.Count);
            bool removed = false;

            while (stack.Count > 0)
            {
                var item = stack.Pop();
                if (!removed && item == target)
                {
                    removed = true;
                    continue;
                }
                temp.Push(item);
            }

            while (temp.Count > 0)
            {
                stack.Push(temp.Pop());
            }
        }
    }
}