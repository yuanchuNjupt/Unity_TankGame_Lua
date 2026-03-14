using System;
using System.Collections.Generic;
using UnityEngine.Events;
using XLua;

namespace CsharpScripts
{
    public static class CsharpCallLuaList
    {
        [CSharpCallLua] public static List<Type> List = new List<Type>()
        {
            typeof(UnityAction)
            


        };
    }
}