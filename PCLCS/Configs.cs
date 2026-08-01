namespace PCLCS;
public static class Configs {

    /// <summary>当前 Java 相关配置的版本。0 代表尚未从老的 Settings 系统中迁移。</summary>
    public static readonly ConfigEntry<int> JavaConfigVersion = new("JavaConfigVersion", 0);
    public static readonly ConfigEntry<ConcurrentList<Java>> JavaList = new("JavaList", []);
    public static readonly ConfigEntry<List<string>> JavaRemovedList = new("JavaRemovedList", []);
    public static readonly DynamicConfigEntry<bool> JavaMigrated = new("InstanceMigratedJava", false);
    public static readonly DynamicConfigEntry<Java> JavaForced = new("InstanceForcedJava", null);

    static Configs() {
        /// 当 JavaForced 获取到一个不在 JavaList 中的值时，将其重置为 null。
        JavaForced.PreviewGet += (args, provider) => {
            if (args.Value is not null && !JavaList.Get()!.Contains(args.Value)) {
                args.Value = null;
                JavaForced.Reset(provider);
            }
        };
    }
}