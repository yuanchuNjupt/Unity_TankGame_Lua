using System.IO;
using UnityEditor;
using UnityEngine;

public class AssetBundleBuilder
{
    // 在顶部菜单栏增加一个“Tools/Build AssetBundles”的选项
    [MenuItem("Tools/Build AssetBundles")]
    static void BuildAllAssetBundles()
    {
        // 1. 定义输出路径，通常放在StreamingAssets下，方便本地测试
        string outputPath = Path.Combine(Application.streamingAssetsPath, "AssetBundles");
    
        // 2. 如果路径不存在，则创建它
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        // 3. 执行打包
        // 参数1：输出路径
        // 参数2：打包选项（推荐使用ChunkBasedCompression，即LZ4格式，支持按需解压）
        // 参数3：目标平台（直接获取当前编辑器激活的平台）
        BuildPipeline.BuildAssetBundles(outputPath, 
            BuildAssetBundleOptions.ChunkBasedCompression, 
            EditorUserBuildSettings.activeBuildTarget);
    
        // 刷新Project视图，显示打包好的文件
        AssetDatabase.Refresh();
    }
}
