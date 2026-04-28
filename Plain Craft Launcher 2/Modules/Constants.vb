''' <summary>
''' 社区资源的类型。
''' </summary>
<Flags> Public Enum ResourceTypes
    ''' <summary> 
    ''' Mod。
    ''' </summary>
    [Mod] = 1
    ''' <summary>
    ''' 整合包。
    ''' </summary>
    ModPack = 2
    ''' <summary>
    ''' 资源包。
    ''' </summary>
    ResourcePack = 4
    ''' <summary>
    ''' 光影包。
    ''' </summary>
    Shader = 8
    ''' <summary>
    ''' 数据包。
    ''' </summary>
    DataPack = 16
    ''' <summary>
    ''' 服务端插件。
    ''' </summary>
    Plugin = 32
    ''' <summary>
    ''' 同时包含数据包以及 Mod。
    ''' </summary>
    ModOrDataPack = [Mod] Or DataPack
    ''' <summary>
    ''' 允许任意种类，或种类未知。
    ''' </summary>
    Any = [Mod] Or ModPack Or ResourcePack Or Shader Or DataPack Or Plugin
End Enum

''' <summary>
''' 社区资源的来源平台。
''' </summary>
<Flags> Public Enum ResourcePlatforms
    CurseForge = 1
    Modrinth = 2
    Any = CurseForge Or Modrinth
End Enum

''' <summary>
''' Mod 加载器的类型。
''' </summary>
Public Enum ModLoaderTypes
    'https://docs.curseforge.com/rest-api/#tocS_ModLoaderType
    Any = 0
    Forge = 1
    LiteLoader = 3
    Fabric = 4
    Quilt = 5
    NeoForge = 6
End Enum

''' <summary>
''' 在爱发电中的赞助等级。
''' </summary>
Public Enum DonationRanks
    None = 0
    Rank6 = 6
    Rank12 = 12
    Rank23 = 23
    Rank54 = 54
    Rank98 = 98
End Enum

Public Module Constants
    ''' <summary>
    ''' 土豆码版本号。
    ''' </summary>
    Public Const POTATO_VERSION As Char = "1"c
End Module