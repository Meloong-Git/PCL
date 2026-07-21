namespace PCLCS;

/// <summary>
/// 社区资源的类型。
/// </summary>
[Flags]
public enum ResourceTypes {
    /// <summary>Mod。</summary>
    Mod = 1,
    /// <summary>整合包。</summary>
    ModPack = 2,
    /// <summary>资源包。</summary>
    ResourcePack = 4,
    /// <summary>光影包。</summary>
    Shader = 8,
    /// <summary>数据包。</summary>
    DataPack = 16,
    /// <summary>服务端插件。</summary>
    Plugin = 32,
    /// <summary>同时包含数据包以及 Mod。</summary>
    ModOrDataPack = Mod | DataPack,
    /// <summary>允许任意种类，或种类未知。</summary>
    Any = Mod | ModPack | ResourcePack | Shader | DataPack | Plugin,
}

/// <summary>
/// 社区资源的来源平台。
/// </summary>
[Flags]
public enum ResourcePlatforms {
    CurseForge = 1,
    Modrinth = 2,
    Any = CurseForge | Modrinth,
}

/// <summary>
/// Mod 加载器的类型。
/// </summary>
[Flags]
public enum ModLoaders {
    None = 0,
    Forge = 1,
    LiteLoader = 2,
    Fabric = 4,
    Quilt = 8,
    NeoForge = 16,
    All = Forge | LiteLoader | Fabric | Quilt | NeoForge,
}

/// <summary>
/// 在爱发电中的赞助等级。
/// </summary>
public enum DonationRank {
    None = 0,
    Rank6 = 6,
    Rank12 = 12,
    Rank23 = 23,
    Rank54 = 54,
    Rank98 = 98,
}

public static class Versions {
    /// <summary>
    /// 土豆码版本号。
    /// </summary>
    public const char PotatoVersion = '1';
    /// <summary>
    /// 版本管理中 Mod 信息缓存的版本号。
    /// </summary>
    public const int LocalModCacheVersion = 17;
    /// <summary>
    /// Minecraft 本地实例信息缓存的版本号。
    /// </summary>
    public const int McInstanceCacheVersion = 38;
    /// <summary>
    /// Java 相关配置的版本号。
    /// </summary>
    public const int JavaConfigVersion = 1;
}
