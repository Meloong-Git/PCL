using MeloongCore;
namespace PCLCS;
public static class Configs {

    /// <summary>当前 Java 相关配置的版本。0 代表尚未从老的 Settings 系统中迁移。</summary>
    public static readonly ConfigEntry<int> JavaConfigVersion = new(nameof(JavaConfigVersion), 0);
    public static readonly ConfigEntry<ConcurrentList<Java>> JavaList = new(nameof(JavaList), []);
    public static readonly ConfigEntry<List<string>> JavaRemovedList = new(nameof(JavaRemovedList), []);
    public static readonly DynamicConfigEntry<bool> InstanceMigratedJava = new(nameof(InstanceMigratedJava), false);
    public static readonly DynamicConfigEntry<Java> InstanceForcedJava = new(nameof(InstanceForcedJava), null);

}
