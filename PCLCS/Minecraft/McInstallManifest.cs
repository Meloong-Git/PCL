namespace PCLCS;

/// <summary>
/// MC 版本安装清单及 PCL 管理文件清单。
/// </summary>
/// <param name="SchemaVersion"> 清单格式版本；无版本的旧清单为 0。</param>
/// <param name="Components"> 安装目标及受管理组件版本。</param>
/// <param name="LastModifiedUtc"> 最后修改时间（UTC）。</param>
/// <param name="ManagedFiles"> 受管理文件；没有文件时为空列表。</param>
public record McInstallManifest(int SchemaVersion, McComponents Components, DateTime LastModifiedUtc, IReadOnlyList<McInstallManifest.ManagedFile> ManagedFiles) {
    /// <summary>
    /// 清理以 Mod 形式安装的、已删除或修改的组件的记录，并更新清单。
    /// </summary>
    public McInstallManifest RefreshModComponents(string gameFolder) {
        List<ManagedFile> invalidFiles = [];
        foreach (ManagedFile entry in ManagedFiles) {
            if (entry.Component is null) continue; // 不检查非组件文件
            FileInfo file = new(Path.Combine(gameFolder, entry.RelativePath));
            if (!file.Exists || file.LastWriteTimeUtc != entry.LastWriteTimeUtc.ToUniversalTime()) invalidFiles.Add(entry);
        }
        if (!invalidFiles.Any()) return this;
        return this with {
            Components = Components with {
                OptiFineEntry = invalidFiles.Any(file => file.Component == "OptiFine") ? null : Components.OptiFineEntry,
                FabricApiVersion = invalidFiles.Any(file => file.Component == "FabricApi") ? null : Components.FabricApiVersion,
                OptiFabricVersion = invalidFiles.Any(file => file.Component == "OptiFabric") ? null : Components.OptiFabricVersion
            },
            ManagedFiles = ManagedFiles.Where(file => !invalidFiles.Contains(file)).ToList(),
            LastModifiedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 由 PCL 管理并记录的版本内文件。
    /// </summary>
    /// <param name="RelativePath"> 相对于游戏目录的文件路径。确保路径为全小写且可比较。</param>
    /// <param name="Sha1"> 小写的 SHA1。</param>
    /// <param name="LastWriteTimeUtc"> 目标文件的最后修改时间（UTC）。</param>
    /// <param name="Component"> 安装器组件名；为空时表示文件由整合包管理。</param>
    public record ManagedFile(string RelativePath, string Sha1, DateTime LastWriteTimeUtc,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? Component = null);
}

/// <param name="MinecraftVersion"> 目标 Minecraft 版本。</param>
/// <param name="ModLoaderVersions"> 目标中各个 Mod Loader 的版本；原版为空字典。</param>
/// <param name="OptiFineEntry"> OptiFine 条目信息，不安装时为 <see langword="null"/>。</param>
/// <param name="FabricApiVersion"> 由 PCL 管理的 Fabric API 版本；不安装时为 <see langword="null"/>。</param>
/// <param name="OptiFabricVersion"> 由 PCL 管理的 OptiFabric 版本；不安装时为 <see langword="null"/>。</param>
[method: JsonConstructor]
public record McComponents(string MinecraftVersion, Dictionary<ModLoaders, string> ModLoaderVersions,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] JObject? OptiFineEntry = null,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? FabricApiVersion = null,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? OptiFabricVersion = null) {
    [JsonIgnore]
    public string? OptiFineVersion => OptiFineEntry?["InstanceName"]?.Value<string>()?.AfterLast("-OptiFine_");
    public McComponents(string minecraftVersion, JObject? optiFineEntry, string? forgeVersion,
        string? neoForgeVersion, string? fabricVersion, string? fabricApiVersion, string? optiFabricVersion, string? liteLoaderVersion)
        : this(minecraftVersion, [], optiFineEntry, fabricApiVersion, optiFabricVersion) {
        if (forgeVersion is not null) ModLoaderVersions[ModLoaders.Forge] = forgeVersion;
        if (neoForgeVersion is not null) ModLoaderVersions[ModLoaders.NeoForge] = neoForgeVersion;
        if (fabricVersion is not null) ModLoaderVersions[ModLoaders.Fabric] = fabricVersion;
        if (liteLoaderVersion is not null) ModLoaderVersions[ModLoaders.LiteLoader] = liteLoaderVersion;
    }
    public bool IsEquivalentTo(McComponents other) =>
        MinecraftVersion == other.MinecraftVersion &&
        OptiFineVersion == other.OptiFineVersion &&
        FabricApiVersion == other.FabricApiVersion &&
        OptiFabricVersion == other.OptiFabricVersion &&
        ModLoaderVersions.Count == other.ModLoaderVersions.Count &&
        ModLoaderVersions.All(entry => other.ModLoaderVersions.TryGetValue(entry.Key, out string? version) && version == entry.Value);
}
