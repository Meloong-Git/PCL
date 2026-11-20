using System.Globalization;

namespace PCLCS;

/// <summary>
/// Minecraft 版本号和组件信息
/// </summary>
public sealed class McVersion {
    /// <summary>
    /// 原版版本名，如 26.1、26.1-snapshot-1、1.12.2、16w01a。
    /// </summary>
    public string VanillaName {
        get => _vanillaName;
        set {
            _vanillaName = value ?? "Unknown";
            Vanilla = NameToVersion(_vanillaName);
        }
    }
    private string _vanillaName = "Unknown";
    /// <summary>
    /// 可比较的三段式原版版本号。
    /// 旧格式会将次版本号移到主版本号，例如 1.20.1 会转换为 20.0.1，若没有版本号，例如旧快照，则为 9999.0.0。
    /// </summary>
    public Version Vanilla = new(9999, 0, 0);
    /// <summary> 原版版本号是否可靠（不是通过猜测获取）。</summary>
    public bool Reliable;
    /// <summary> 原版版本号是否有效。</summary>
    public bool Valid => Vanilla.Major < 1000;
    /// <summary> 可供比较的原版 Drop 序数，例如 26.3.2 为 263，1.21.5 为 210；若没有版本号，例如旧快照，则直接指定为 209。</summary>
    public int Drop => Valid ? Vanilla.Major * 10 + Vanilla.Minor : 209;
    /// <summary> 已安装的 Mod Loader，可能有多个。未安装时为 <see cref="ModLoaders.None"/>。</summary>
    public ModLoaders ModLoaders => ModLoaderVersions.Keys.Aggregate(ModLoaders.None, (result, loader) => result | loader);
    /// <summary> 已安装的各个 Mod Loader 及其版本；无法确定精确版本时为“未知版本”。</summary>
    public Dictionary<ModLoaders, string> ModLoaderVersions { get; } = [];
    /// <summary> OptiFine 版本名，如 C8、C9_pre10；未识别到时为 null。</summary>
    public string? OptiFine;
    /// <summary> OptiFine 是否以 Mod 形式安装。</summary>
    public bool OptiFineAsMod;
    /// <summary> 版本的发布时间。</summary>
    public DateTime ReleaseTime = new(1970, 1, 1, 15, 0, 0);
    /// <summary> 是否为愚人节版本。</summary>
    public bool IsFool;
    /// <summary> 是否为快照、预发布版、发布候选或实验性版本。</summary>
    public bool IsSnapshot;
    /// <summary> 是否识别到已安装的 OptiFine。</summary>
    public bool HasOptiFine => OptiFine is not null;
    /// <summary>
    /// 可供比较的 OptiFine 版本序数。
    /// </summary>
    public int OptiFineCode {
        get {
            if (OptiFine is null or "未知版本" || OptiFine.Length == 0) return 0;
            // 字母编号，如 G2 中的 G（7）
            int result = char.ToUpperInvariant(OptiFine[0]) - 'A' + 1;
            // 末尾数字，如 C5 beta4 中的 5
            result = result * 100 + (int.TryParse(OptiFine.Substring(1).RegexSeek(@"\d+"), out int mainVersion) ? mainVersion : 0);
            // 测试标记（正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）
            result *= 100;
            string? preview = OptiFine.RegexSeek(@"(?i)(pre|beta)\d*");
            if (preview is null) return result + 99;
            int sequence = int.TryParse(preview.RegexSeek(@"\d+"), out int value) ? value : 1; // 为 pre 或 beta 结尾，视作 1
            return result + (preview.StartsWithF("pre", true) ? 50 : 0) + sequence;
        }
    }
    /// <summary>
    /// 可供比较的类 Forge 版本序数。
    /// </summary>
    public int ForgelikeCode {
        get {
            if (!ModLoaderVersions.TryGetValue(ModLoaders.Forge, out string? version)) {
                ModLoaderVersions.TryGetValue(ModLoaders.NeoForge, out version);
            }
            if (version is null or "未知版本") return 0;
            // 提取所有数字段，以兼容 beta、alpha 和 snapshot 等非数字后缀。
            int[] segments = version.RegexSearch(@"\d+")
                .Select(segment => int.TryParse(segment, out int value) ? value : 0).ToArray();
            return segments.Length switch {
                >= 4 => segments[0] * 1000000 + segments[1] * 10000 + segments[3],
                3 => segments[0] * 1000000 + segments[1] * 10000 + segments[2],
                2 => segments[0] * 1000000 + segments[1] * 10000,
                1 => segments[0] * 1000000,
                _ => 0
            };
        }
    }

    /// <summary>
    /// 创建默认 Minecraft 版本。
    /// </summary>
    /// <param name="vanillaName">原版版本名。</param>
    /// <param name="reliable">版本名是否来自可靠依据。</param>
    public McVersion(string vanillaName, bool reliable = true) {
        Reliable = reliable;
        VanillaName = vanillaName;
        IsFool = IsFoolName(VanillaName);
        IsSnapshot = IsSnapshotName(VanillaName);
    }

    /// <summary>
    /// 通过 Components 创建 Minecraft 版本信息。
    /// </summary>
    public McVersion(McComponents components) : this(components.MinecraftVersion) {
        OptiFine = components.OptiFineVersion?.AfterLast("HD_").AfterFirst("_");
        foreach (var loader in components.ModLoaderVersions) ModLoaderVersions[loader.Key] = loader.Value;
    }

    /// <summary>
    /// 通过 JSON 识别 Minecraft 版本和已安装组件。
    /// </summary>
    /// <param name="instanceName">实例文件夹名。</param>
    /// <param name="json">展开的版本 JSON。</param>
    /// <param name="jarVersionJson">版本 JAR 中的 version.json。</param>
    public McVersion(string instanceName, JObject json, JObject? jarVersionJson = null) {
        string jsonText = json.ToString(Formatting.None);
        DateTime releaseTime = new(1970, 1, 1, 15, 0, 0); // 未知版本也可能显示为 1970 年
        try {
            if (json["releaseTime"] is JToken releaseTimeToken) releaseTime = releaseTimeToken.ToObject<DateTime>();
        } catch {
        }
        bool reliable = true;
        // 获取游戏版本
        string vanillaName = (GetVanillaName(instanceName, json, jarVersionJson, releaseTime, ref reliable) ?? "Unknown").Replace("_unobfuscated", "").Replace(" Unobfuscated", "");
        Reliable = reliable;
        VanillaName = vanillaName;
        ReleaseTime = releaseTime;
        // 确定版本分类
        string jsonType = json.Value<string>("type") ?? "";
        // OptiFine
        OptiFine = jsonText.ContainsIgnoreCase("optifine") ? jsonText.RegexSeek(@"(?<=HD_U_)[^\"":/]+") ?? "未知版本": null;
        // 愚人节与快照版本
        DateTime fixedReleaseTime = releaseTime.ToUniversalTime().AddHours(2);
        IsFool = jsonType == "fool" || IsFoolName(vanillaName) || (fixedReleaseTime.Month == 4 && fixedReleaseTime.Day == 1 && jsonType == "snapshot");
        IsSnapshot = IsSnapshotName(vanillaName) || instanceName.ContainsIgnoreCase("combat") || jsonType is "snapshot" or "pending" or "fool";
        if (!Reliable) Logger.Info($"无法完全确认 MC 版本号的版本：{VanillaName}");
        // LiteLoader
        if (jsonText.ContainsIgnoreCase("liteloader")) {
            ModLoaderVersions[ModLoaders.LiteLoader] = jsonText.RegexSeek(@"(?<=com\.mumfrey:liteloader:)[^\"":/]+") ?? "未知版本";
        }
        // Fabric、Forge、NeoForge
        if (jsonText.ContainsIgnoreCase("net.fabricmc:fabric-loader") || jsonText.ContainsIgnoreCase("org.quiltmc:quilt-loader")) {
            ModLoaderVersions[ModLoaders.Fabric] = jsonText.RegexSeek(@"(?<=(net\.fabricmc:fabric-loader:|org\.quiltmc:quilt-loader:))[0-9\.]+(\+build.[0-9]+)?")?
                                                        .Replace("+build", "") ?? "未知版本";
        } else if (jsonText.ContainsIgnoreCase("net.neoforge")) {
            // 1.20.1 JSON 范例："--fml.forgeVersion", "47.1.99"
            // 1.20.2+ JSON 范例："--fml.neoForgeVersion", "20.6.119-beta"
            ModLoaderVersions[ModLoaders.NeoForge] = FindArgumentValue(json, "--fml.neoForgeVersion") ?? FindArgumentValue(json, "--fml.forgeVersion") ?? "未知版本";
        } else if (jsonText.ContainsIgnoreCase("minecraftforge")) {
            ModLoaderVersions[ModLoaders.Forge] = jsonText.RegexSeek(@"(?<=forge:[0-9\.]+(?:_pre[0-9]*)?-)[0-9\.]+")
                                                    ?? jsonText.RegexSeek(@"(?<=net\.minecraftforge:minecraftforge:)[0-9\.]+")
                                                    ?? jsonText.RegexSeek(@"(?<=net\.minecraftforge:fmlloader:[0-9\.]+-)[0-9\.]+") ?? "未知版本";
        }
    }

    private static string? GetVanillaName(string instanceName, JObject json, JObject? jarVersionJson, DateTime releaseTime, ref bool reliable) {
        // 获取发布时间并判断是否为老版本
        if (releaseTime.Year is > 2000 and < 2013) return "Old";
        // 实验性快照
        if (json.Value<string>("type") == "pending") return "pending";
        // 从 PCL 下载的版本信息中获取版本号
        if (json["clientVersion"] is JToken clientVersion) return clientVersion.ToString();
        // 从 HMCL 下载的版本信息中获取版本号
        if (json["patches"] is JArray patches) {
            JToken? gameVersion = patches.OfType<JObject>().FirstOrDefault(patch => patch.Value<string>("id") == "game")?["version"];
            if (gameVersion is not null) return gameVersion.ToString();
        }
        // 从 Forge / NeoForge Arguments 中获取版本号
        if (FindArgumentValue(json, "--fml.mcVersion") is { Length: > 0 } fmlVersion) return fmlVersion;
        // 从继承版本中获取版本号
        string inherit = json["inheritsFrom"]?.ToString() ?? "";
        if (inherit.Length > 0) {
            string jar = json["jar"]?.ToString() ?? ""; // LiteLoader 优先使用 Jar
            return jar.Length > 0 ? jar : inherit;
        }
        // 从下载地址中获取版本号
        string? download = json["downloads"]?.ToString(Formatting.None).RegexSeek(@"(?<=launcher.mojang.com/mc/game/)[^/]*");
        if (download is not null) return download;
        // 从 Forge/OptiFine/Fabric 版本中获取版本号
        string librariesText = json["libraries"]!.ToString();
        string[] libraryPatterns = [
            @"(?<=net.minecraftforge:forge:)[0-9]{1,2}.[0-9+.]+",
            @"(?<=net.minecraftforge:fmlloader:)[0-9]{1,2}.[0-9+.]+",
            @"(?<=optifine:OptiFine:)[0-9]{1,2}.[0-9+.]+",
            @"(?<=((fabricmc)|(quiltmc)):intermediary:)[^\""]*"
        ];
        foreach (string pattern in libraryPatterns) {
            if (librariesText.RegexSeek(pattern) is string match) return match;
        }
        // 从 jar 项中获取版本号
        if (json["jar"] is JToken jarToken) return jarToken.ToString();
        // 从 jar 文件的 version.json 中获取版本号
        if (jarVersionJson?["name"] is JToken jarNameToken && jarNameToken.ToString() is { Length: < 32 } jarName) { // 因为 wiki 说这玩意儿可能是个 hash，虽然我没发现
            Logger.Info($"从版本 jar 中的 version.json 获取到版本号：{jarName}");
            return jarName;
        }
        string VersionPattern = @"(([1-9][0-9]w[0-9]{2}[a-g])|((1|[2-9][0-9])\.[0-9]+(\.[0-9]+)?(-(pre|rc|snapshot-?)[1-9]*| Pre-Release( [1-9])?)?))(_unobfuscated)?";
        // 从 JSON 的 ID 中获取
        if (json["id"]!.ToString().RegexSeek(VersionPattern, RegexOptions.IgnoreCase) is string idMatch) return idMatch;
        // 非准确的版本判断警告
        Logger.Info($"无法完全确认 MC 版本号的版本：{instanceName}");
        reliable = false;
        // 从文件夹名中获取
        if (instanceName.RegexSeek(VersionPattern, RegexOptions.IgnoreCase) is string folderMatch) return folderMatch;
        // 从 JSON 出现的版本号中获取
        JObject withoutLibraries = (JObject) json.DeepClone();
        withoutLibraries.Remove("libraries");
        return withoutLibraries.ToString(Formatting.None).RegexSeek(VersionPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsFoolName(string name) {
        name = name.Lower();
        return name.StartsWithF("2.0") || name is "15w14a" or "1.rv-pre1" or
            "3d shareware v1.34" or "20w14∞" or "22w13oneblockatatime" or "23w13a_or_b" or
            "24w14potato" or "25w14craftmine" or "26w14a" || name.StartsWithF("20w14inf");
    }

    private static bool IsSnapshotName(string name) =>
        new[] { "w", "snapshot", "rc", "pre", "experimental", "-" }.Any(name.ContainsIgnoreCase);

    /// <summary>
    /// 读取 arguments 中指定参数的字符串值。
    /// </summary>
    private static string? FindArgumentValue(JObject json, string name) {
        JArray? arguments = json["arguments"]?["game"] as JArray;
        if (arguments is null) return null;
        for (int i = 0; i + 1 < arguments.Count; i++) {
            if (arguments[i]?.ToString() == name) return arguments[i + 1]?.ToString();
        }
        return null;
    }

    /// <summary>
    /// 判断版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。
    /// </summary>
    public static bool IsStandardFormat(string? version) {
        if (version is null) return false;
        if (Regex.IsMatch(version, @"^1\.\d")) return true;
        return double.TryParse(version.RegexSeek(@"^[2-9]\d\.\d+"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double value) && value >= 26;
    }

    /// <summary>
    /// 将版本名转换为 Drop 序数；无法转换时返回 209。
    /// </summary>
    public static int VersionToDrop(string? vanillaName) {
        if (vanillaName is null) return 209;
        Version version = NameToVersion(vanillaName);
        return version.Major >= 1000 ? 209 : version.Major * 10 + version.Minor;
    }

    /// <summary>
    /// 将 Drop 序数转换为版本字符串。
    /// </summary>
    public static string DropToVersion(int drop) {
        if (drop >= 250) return $"{drop / 10}.{drop % 10}";
        if (drop != 209) return $"1.{drop / 10}";
        Logger.Warn(new Exception("尝试将旧快照版 Drop 序数转换为版本字符串，这不应该发生！"));
        return "1.20";
    }

    /// <summary>
    /// 将版本名转换为可比较的三段式原版版本号。
    /// 对老版本格式，例如 1.20.3，会被转换为 20.0.3。
    /// 对愚人节版本，返回其基于的正式版版本号。
    /// 若没有版本号，例如旧快照，则为 9999.0.0。
    /// </summary>
    public static Version NameToVersion(string? name) {
        if (name is null) return new Version(9999, 0, 0);
        name = name.Lower().Replace("_unobfuscated", "").Replace(" unobfuscated", "");
        // 愚人节版本
        if (name.StartsWithF("2.0")) return NameToVersion("1.5.1");
        if (name == "15w14a") return NameToVersion("1.8.3");
        if (name.ContainsIgnoreCase(".rv-pre")) return NameToVersion("1.9.2");
        if (name.ContainsIgnoreCase("shareware")) return NameToVersion("1.13.2");
        if (name.StartsWithF("20w14") && name != "20w14a") return NameToVersion("1.15.2");
        if (name.ContainsIgnoreCase("oneblockatatime")) return NameToVersion("1.18.2");
        if (name.ContainsIgnoreCase("23w13a") && name != "20w13a") return NameToVersion("1.19.4");
        if (name == "24w14potato") return NameToVersion("1.20.4");
        if (name == "25w14craftmine") return NameToVersion("1.21.4");
        if (name == "26w14a") return NameToVersion("26.1.1");
        // 其余版本
        static int ParseSegment(string[] segments, int index) {
            if (index >= segments.Length) return 0;
            // 只读取该段开头连续的数字，忽略其后的预览版标记。
            return int.TryParse(segments[index].RegexSeek(@"^\d+"), out int value) ? value : 0;
        }
        string[] segments = name.Split([' ', '_', '-', '.'], StringSplitOptions.None);
        if (name.StartsWithF("1.")) {
            return new Version(ParseSegment(segments, 1), 0, ParseSegment(segments, 2));
        } else if (name.RegexCheck(@"^[2-9][0-9]\.")) {
            return new Version(ParseSegment(segments, 0), ParseSegment(segments, 1), ParseSegment(segments, 2));
        } else {
            return new Version(9999, 0, 0);
        }
    }
}
