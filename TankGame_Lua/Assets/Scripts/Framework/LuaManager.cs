
using UnityEngine;
using XLua;
namespace Csharp.Manager
{
    public class LuaManager
    {

        public static readonly LuaManager Instance = new LuaManager();
        
        private LuaManager() {}
        
        
        private LuaEnv _luaEnv = null;
        private string _requireLoadPath = null;

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