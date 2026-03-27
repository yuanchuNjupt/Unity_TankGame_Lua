using System;
using System.Threading;
using System.Threading.Tasks;
using Framework;
using UnityEditor;
using UnityEngine;
using XLua;

namespace CsharpScripts
{
    public class Main : MonoBehaviour
    {
        void Awake()
        {
            LuaManager.Instance.Init(Application.dataPath + "/Scripts/LuaScripts/");
            LuaManager.Instance.DoFile("LuaMain");
            
            DontDestroyOnLoad(gameObject);
            
            
            var ab = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/AssetBundles/tank");
            var go = ab.LoadAsset<GameObject>("Cannon");
            Instantiate(go);


            
        }

        bool TestFunc()
        {
            //自定义逻辑
            return true;
        }

        private void Update()
        {
            LuaManager.Instance.OnUpdate();
            
            
            
        }

    }
}
