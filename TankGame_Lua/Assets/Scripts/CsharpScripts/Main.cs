using Csharp.Manager;
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
        
        }
    }
}
