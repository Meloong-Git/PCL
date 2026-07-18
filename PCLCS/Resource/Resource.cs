namespace PCLCS;
public static class Resource {

    /// <summary>
    /// 将 CurseForge 的 ModLoaderType 映射为 <see cref="ModLoaders"/> 枚举。
    /// </summary>
    public static ModLoaders FromCurseForgeModLoaderType(int modLoaderType) => modLoaderType switch {
        // https://docs.curseforge.com/rest-api/#tocS_ModLoaderType
        1 => ModLoaders.Forge,
        3 => ModLoaders.LiteLoader,
        4 => ModLoaders.Fabric,
        5 => ModLoaders.Quilt,
        6 => ModLoaders.NeoForge,
        _ => ModLoaders.None,
    };

    /// <summary>
    /// 将 <see cref="ModLoaders"/> 映射为 CurseForge 的单一 ModLoaderType。
    /// </summary>
    public static int ToCurseForgeModLoaderType(ModLoaders loaders) {
        if (loaders == ModLoaders.None) return 0;
        if (!loaders.Flags().IsSingle()) throw new ArgumentException($"传入的枚举值 {loaders} 并非单个 Mod Loader", nameof(loaders));
        return loaders switch {
            ModLoaders.Forge => 1,
            ModLoaders.LiteLoader => 3,
            ModLoaders.Fabric => 4,
            ModLoaders.Quilt => 5,
            ModLoaders.NeoForge => 6,
            _ => 0,
        };
    }

}
