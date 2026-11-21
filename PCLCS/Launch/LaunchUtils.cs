namespace PCLCS;

public static class LaunchUtils {

    /// <summary>
    /// 释放补丁文件并返回完整文件路径。
    /// </summary>
    /// <param name="patchFilePath">补丁文件路径
    /// <param name="outputDirectory">补丁文件的释放目录。</param>
    public static string ExtractPatch(string patchFilePath, string outputDirectory) {
        Logger.Info($"选定的资源 {patchFilePath} 输出路径：{outputDirectory}");
        lock (ExtractPatchLock) { // 避免 OptiFine 和 Forge 安装时同时释放导致冲突
            try {
                FileUtils.ExtractResources(patchFilePath, Path.Combine(outputDirectory, PathUtils.GetLastPart(patchFilePath)), typeof(LaunchUtils));
            } catch (Exception ex) {
                if (!FileUtils.Exists(outputDirectory)) throw new FileNotFoundException($"释放 {patchFilePath} 失败", ex);
                Logger.Warn(ex, $"{patchFilePath} 文件重新释放失败，将尝试更换文件名重新生成");
                patchFilePath = Path.Combine(outputDirectory, $"{PathUtils.GetFileNameWithoutExtension(patchFilePath)}2.{PathUtils.GetExtension(patchFilePath)}");
                try {
                    FileUtils.ExtractResources(patchFilePath, Path.Combine(outputDirectory, PathUtils.GetLastPart(patchFilePath)), typeof(LaunchUtils));
                } catch (Exception ex2) {
                    throw new FileNotFoundException($"释放 {patchFilePath} 最终尝试失败", ex2);
                }
            }
        }
        return Path.Combine(outputDirectory, PathUtils.GetLastPart(patchFilePath));
    }
    private static readonly object ExtractPatchLock = new();

}
