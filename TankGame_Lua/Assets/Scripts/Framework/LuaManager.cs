
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using XLua;
namespace Csharp.Manager
{
    public class LuaManager
    {

        public static readonly LuaManager Instance = new LuaManager();
        
        private LuaManager() {}
        
        
        private LuaEnv _luaEnv = null;
        private string _requireLoadPath = null;
        private UnityAction _onUpdate = null;
        private Dictionary<string, UnityAction> _updateCallbacks = new Dictionary<string, UnityAction>();

        public void Init(string paths = null)
        {
            if (_luaEnv != null)
            {
                Debug.Log("LuaManager has been initialized!");
                return;
            }
            _luaEnv = new LuaEnv();
            
            //reload
            _requireLoadPath = paths;
            if (_requireLoadPath != null)
            {
                _luaEnv.AddLoader(CustomLoader);
            }
            // _luaEnv.AddLoader(CustomLoaderAB);
            
            
        }
        
        public LuaTable Global => _luaEnv.Global;
        
        
        /// <summary>
        /// 注册一个Lua函数对象到Update回调
        /// </summary>
        /// <param name="callbackKey">回调的唯一标识，用于后续取消订阅</param>
        /// <param name="luaFunc">Lua函数</param>
        public void RegisterUpdateFunc(string callbackKey, UnityAction luaFunc)
        {
            // 如果该key已存在，先移除旧的
            if (_updateCallbacks.ContainsKey(callbackKey))
            {
                UnregisterUpdateFunc(callbackKey);
            }
            
            _updateCallbacks[callbackKey] = luaFunc;
            _onUpdate += luaFunc;
        }

        /// <summary>
        /// 取消注册一个Lua函数
        /// </summary>
        /// <param name="callbackKey">回调的唯一标识</param>
        public void UnregisterUpdateFunc(string callbackKey)
        {
            if (_updateCallbacks.TryGetValue(callbackKey, out UnityAction luaFunc))
            {
                _onUpdate -= luaFunc;
                _updateCallbacks.Remove(callbackKey);
            }
        }

        public void OnUpdate()
        {
            _onUpdate?.Invoke();
        }

        public void DoString(string str)
        {
            _luaEnv.DoString(str);
        }
        
        public void DoFile(string filePath)
        {
            _luaEnv.DoString($"require('{filePath}')");
        }

        public void Tick()
        {
            _luaEnv.Tick();
        }
        
        public void Dispose()
        {
            _luaEnv.Dispose();
        }
        
        private byte[] CustomLoader(ref string filepath)
        {
            // 1. 尝试从自定义路径加载 .lua 文件
            string path = _requireLoadPath + filepath + ".lua";
            if (System.IO.File.Exists(path))
            {
                return System.IO.File.ReadAllBytes(path);
            }
            
            // 2. 尝试从自定义路径加载 .lua.txt 文件
            path = _requireLoadPath + filepath + ".lua.txt";
            if (System.IO.File.Exists(path))
            {
                return System.IO.File.ReadAllBytes(path);
            }
            
            // // 3. 尝试从 XLua Resources 路径加载（处理 xlua.util 等内置模块）
            // string xluaPath = Application.dataPath + "/XLua/Resources/" + filepath.Replace('.', '/') + ".lua.txt";
            // if (System.IO.File.Exists(xluaPath))
            // {
            //     return System.IO.File.ReadAllBytes(xluaPath);
            // }
            
            Debug.LogWarning("自定义加载未找到Lua:" + path);
            return null;
        }

        private byte[] CustomLoaderAB(ref string filePath)
        {
            //重定向从AB包中加载lua脚本
            string abPath = Application.streamingAssetsPath + "/lua";
            AssetBundle ab = AssetBundle.LoadFromFile(abPath);
            TextAsset textAsset = ab.LoadAsset<TextAsset>(filePath + ".lua");
            if (textAsset != null)
            {
                return textAsset.bytes;
            }
            else
            {
                Debug.LogWarning("未在AB包中找到Lua:" + abPath);
                return null;
            }
            
        }
    }
}