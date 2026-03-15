using System;
using Framework;
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
        }

        private void Update()
        {
            LuaManager.Instance.OnUpdate();
        }
    }
}
