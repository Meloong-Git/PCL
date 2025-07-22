Public Module ModDownload

#Region "DlClient* | Minecraft 客户端"

    ''' <summary>
    ''' 返回某 Minecraft 版本对应的原版主 Jar 文件的下载信息，要求对应依赖版本已存在。
    ''' 失败则抛出异常，不需要下载则返回 Nothing。
    ''' </summary>
    Public Function DlClientJarGet(Version As McVersion, ReturnNothingOnFileUseable As Boolean) As NetFile
        '获取底层继承版本
        Try
            Do While Not String.IsNullOrEmpty(Version.InheritVersion)
                Version = New McVersion(Version.InheritVersion)
            Loop
        Catch ex As Exception
            Log(ex, "获取底层继承版本失败")
        End Try
        '检查 Json 是否标准
        If Version.JsonObject("downloads") Is Nothing OrElse Version.JsonObject("downloads")("client") Is Nothing OrElse Version.JsonObject("downloads")("client")("url") Is Nothing Then
            Throw New Exception("底层版本 " & Version.Name & " 中无 jar 文件下载信息")
        End If
        '检查文件
        Dim Checker As New FileChecker(MinSize:=1024, ActualSize:=If(Version.JsonObject("downloads")("client")("size"), -1), Hash:=Version.JsonObject("downloads")("client")("sha1"))
        If ReturnNothingOnFileUseable AndAlso Checker.Check(Version.Path & Version.Name & ".jar") Is Nothing Then Return Nothing '通过校验
        '返回下载信息
        Dim JarUrl As String = Version.JsonObject("downloads")("client")("url")
        Return New NetFile(DlSourceLauncherOrMetaGet(JarUrl), Version.Path & Version.Name & ".jar", Checker)
    End Function

    ''' <summary>
    ''' 返回某 Minecraft 版本对应的原版主 AssetIndex 文件的下载信息，要求对应依赖版本已存在。
    ''' 若未找到，则会返回 Legacy 资源文件或 Nothing。
    ''' </summary>
    Public Function DlClientAssetIndexGet(Version As McVersion) As NetFile
        '获取底层继承版本
        Do While Not String.IsNullOrEmpty(Version.InheritVersion)
            Version = New McVersion(Version.InheritVersion)
        Loop
        '获取信息
        Dim IndexInfo = McAssetsGetIndex(Version, True, True)
        Dim IndexAddress As String = PathMcFolder & "assets\indexes\" & IndexInfo("id").ToString & ".json"
        Log("[Download] 版本 " & Version.Name & " 对应的资源文件索引为 " & IndexInfo("id").ToString)
        Dim IndexUrl As String = If(IndexInfo("url"), "")
        If IndexUrl = "" Then
            Return Nothing
        Else
            Return New NetFile(DlSourceLauncherOrMetaGet(IndexUrl), IndexAddress, New FileChecker(CanUseExistsFile:=False, IsJson:=True))
        End If
    End Function

    ''' <summary>
    ''' 构造补全某 Minecraft 版本的所有文件的加载器列表。失败会抛出异常。
    ''' </summary>
    Public Function DlClientFix(Version As McVersion, CheckAssetsHash As Boolean, AssetsIndexBehaviour As AssetsIndexExistsBehaviour) As List(Of LoaderBase)
        Dim Loaders As New List(Of LoaderBase)

#Region "下载支持库文件"
        Dim LoadersLib As New List(Of LoaderBase) From {
            New LoaderTask(Of String, List(Of NetFile))("分析缺失支持库文件", Sub(Task As LoaderTask(Of String, List(Of NetFile))) Task.Output = McLibFix(Version)) With {.ProgressWeight = 1},
            New LoaderDownload("下载支持库文件", New List(Of NetFile)) With {.ProgressWeight = 15}
        }
        '构造加载器
        Loaders.Add(New LoaderCombo(Of String)("下载支持库文件（主加载器）", LoadersLib) With {.Block = False, .Show = False, .ProgressWeight = 16})
#End Region

#Region "下载资源文件"
        If ShouldIgnoreFileCheck(Version) Then
            Log("[Download] 已跳过所有 Assets 检查")
        Else
            Dim LoadersAssets As New List(Of LoaderBase)
            '获取资源文件索引地址
            LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))("分析资源文件索引地址",
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                Try
                    Dim IndexFile = DlClientAssetIndexGet(Version)
                    Dim IndexFileInfo As New FileInfo(IndexFile.LocalPath)
                    If AssetsIndexBehaviour <> AssetsIndexExistsBehaviour.AlwaysDownload AndAlso IndexFile.Check.Check(IndexFile.LocalPath) Is Nothing Then
                        Task.Output = New List(Of NetFile)
                    Else
                        Task.Output = New List(Of NetFile) From {IndexFile}
                    End If
                Catch ex As Exception
                    Throw New Exception("分析资源文件索引地址失败", ex)
                End Try
            End Sub) With {.ProgressWeight = 0.5, .Show = False})
            '下载资源文件索引
            LoadersAssets.Add(New LoaderDownload("下载资源文件索引", New List(Of NetFile)) With {.ProgressWeight = 2})
            '要求独立更新索引
            If AssetsIndexBehaviour = AssetsIndexExistsBehaviour.DownloadInBackground Then
                Dim LoadersAssetsUpdate As New List(Of LoaderBase)
                Dim TempAddress As String = Nothing
                Dim RealAddress As String = Nothing
                LoadersAssetsUpdate.Add(New LoaderTask(Of String, List(Of NetFile))("后台分析资源文件索引地址",
                Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                    Dim BackAssetsFile As NetFile = DlClientAssetIndexGet(Version)
                    RealAddress = BackAssetsFile.LocalPath
                    TempAddress = PathTemp & "Cache\" & BackAssetsFile.LocalName
                    BackAssetsFile.LocalPath = TempAddress
                    Task.Output = New List(Of NetFile) From {BackAssetsFile}
                    '检查是否需要更新：每天只更新一次
                    If File.Exists(RealAddress) AndAlso Math.Abs((File.GetLastWriteTime(RealAddress).Date - Now.Date).TotalDays) < 1 Then
                        Log("[Download] 无需更新资源文件索引，取消")
                        Task.Abort()
                    End If
                End Sub))
                LoadersAssetsUpdate.Add(New LoaderDownload("后台下载资源文件索引", New List(Of NetFile)))
                LoadersAssetsUpdate.Add(New LoaderTask(Of List(Of NetFile), String)("后台复制资源文件索引",
                Sub(Task As LoaderTask(Of List(Of NetFile), String))
                    CopyFile(TempAddress, RealAddress)
                    McLaunchLog("后台更新资源文件索引成功：" & TempAddress)
                End Sub))
                Dim Updater As New LoaderCombo(Of String)("后台更新资源文件索引", LoadersAssetsUpdate)
                Log("[Download] 开始后台检查资源文件索引")
                Updater.Start()
            End If
            '获取资源文件地址
            LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))("分析缺失资源文件",
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                Task.Output = McAssetsFixList(Version, CheckAssetsHash, Task)
            End Sub) With {.ProgressWeight = 3})
            '下载资源文件
            LoadersAssets.Add(New LoaderDownload("下载资源文件", New List(Of NetFile)) With {.ProgressWeight = 25})
            '构造加载器
            Loaders.Add(New LoaderCombo(Of String)("下载资源文件（主加载器）", LoadersAssets) With {.Block = False, .Show = False, .ProgressWeight = 30.5})
        End If
#End Region

        Return Loaders
    End Function
    Public Enum AssetsIndexExistsBehaviour
        ''' <summary>
        ''' 如果文件存在，则不进行下载。
        ''' </summary>
        DontDownload
        ''' <summary>
        ''' 如果文件存在，则启动新的下载加载器进行独立的更新。
        ''' </summary>
        DownloadInBackground
        ''' <summary>
        ''' 如果文件存在，也同样进行下载。
        ''' </summary>
        AlwaysDownload
    End Enum

#End Region

#Region "DlClientList | Minecraft 客户端 版本列表"

    '主加载器
    Public Structure DlClientListResult
        ''' <summary>
        ''' 数据来源名称，如“Mojang”，“BMCLAPI”。
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' 是否为官方的实时数据。
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' 获取到的 Json 数据。
        ''' </summary>
        Public Value As JObject
        '''' <summary>
        '''' 官方源的失败原因。若没有则为 Nothing。
        '''' </summary>
        'Public OfficialError As Exception
    End Structure
    ''' <summary>
    ''' Minecraft 客户端 版本列表，主加载器。
    ''' 若要求镜像源必须包含某个版本，则将该版本 ID 作为输入（#5195）。
    ''' </summary>
    Public DlClientListLoader As New LoaderTask(Of String, DlClientListResult)("DlClientList Main", AddressOf DlClientListMain)
    Private Sub DlClientListMain(Loader As LoaderTask(Of String, DlClientListResult))
        Select Case Setup.Get("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListMojangLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListMojangLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListMojangLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of String, DlClientListResult), Integer)(DlClientListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    '各个下载源的分加载器
    ''' <summary>
    ''' Minecraft 客户端 版本列表，Mojang 官方源加载器。
    ''' </summary>
    Public DlClientListMojangLoader As New LoaderTask(Of String, DlClientListResult)("DlClientList Mojang", AddressOf DlClientListMojangMain)
    Private IsNewClientVersionHinted As Boolean = False
    Private Sub DlClientListMojangMain(Loader As LoaderTask(Of String, DlClientListResult))
        Dim StartTime As Long = GetTimeTick()
        Dim Json As JObject = NetGetCodeByRequestRetry("https://launchermeta.mojang.com/mc/game/version_manifest.json", IsJson:=True)
        Try
            Dim Versions As JArray = Json("versions")
            If Versions.Count < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Json.ToString & "）")
            '确定官方源是否可用
            If Not DlPreferMojang Then
                Dim DeltaTime = GetTimeTick() - StartTime
                DlPreferMojang = DeltaTime < 4000
                Log($"[Download] Mojang 官方源加载耗时：{DeltaTime}ms，{If(DlPreferMojang, "可优先使用官方源", "不优先使用官方源")}")
            End If
            '添加 PCL 特供项
            If File.Exists(PathTemp & "Cache\download.json") Then Versions.Merge(GetJson(ReadFile(PathTemp & "Cache\download.json")))
            '返回
            Loader.Output = New DlClientListResult With {.IsOfficial = True, .SourceName = "Mojang 官方源", .Value = Json}
            '解析更新提示（Release）
            Dim Version As String = Json("latest")("release")
            If Setup.Get("ToolUpdateRelease") AndAlso Not Setup.Get("ToolUpdateReleaseLast") = "" AndAlso Version IsNot Nothing AndAlso Not Setup.Get("ToolUpdateReleaseLast") = Version Then
                McDownloadClientUpdateHint(Version, Json)
                IsNewClientVersionHinted = True
            End If
            McVersionHighest = Version.Split(".")(1)
            Setup.Set("ToolUpdateReleaseLast", Version)
            '解析更新提示（Snapshot）
            Version = Json("latest")("snapshot")
            If Setup.Get("ToolUpdateSnapshot") AndAlso Not Setup.Get("ToolUpdateSnapshotLast") = "" AndAlso Version IsNot Nothing AndAlso Not Setup.Get("ToolUpdateSnapshotLast") = Version AndAlso Not IsNewClientVersionHinted Then
                McDownloadClientUpdateHint(Version, Json)
            End If
            Setup.Set("ToolUpdateSnapshotLast", If(Version, "Nothing"))
        Catch ex As Exception
            Throw New Exception("Minecraft 官方源版本列表解析失败", ex)
        End Try
    End Sub
    ''' <summary>
    ''' Minecraft 客户端 版本列表，BMCLAPI 源加载器。
    ''' </summary>
    Public DlClientListBmclapiLoader As New LoaderTask(Of String, DlClientListResult)("DlClientList Bmclapi", AddressOf DlClientListBmclapiMain)
    Private Sub DlClientListBmclapiMain(Loader As LoaderTask(Of String, DlClientListResult))
        Dim Json As JObject = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", IsJson:=True)
        Try
            Dim Versions As JArray = Json("versions")
            If Versions.Count < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Json.ToString & "）")
            '添加 PCL 特供项
            If File.Exists(PathTemp & "Cache\download.json") Then Versions.Merge(GetJson(ReadFile(PathTemp & "Cache\download.json")))
            '检查是否有要求的版本（#5195）
            If Not String.IsNullOrEmpty(Loader.Input) Then
                Dim Id = Loader.Input
                If DlClientListLoader.Output.Value IsNot Nothing AndAlso Not DlClientListLoader.Output.Value("versions").Any(Function(v) v("id") = Id) Then
                    Throw New Exception("BMCLAPI 源未包含目标版本 " & Id)
                End If
            End If
            '返回
            Loader.Output = New DlClientListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Json}
        Catch ex As Exception
            Throw New Exception("Minecraft BMCLAPI 版本列表解析失败（" & Json.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' 获取某个版本的 Json 下载地址，若失败则返回 Nothing。必须在工作线程执行。
    ''' </summary>
    Public Function DlClientListGet(Id As String)
        Try
            '确认版本格式标准
            Id = Id.Replace("_", "-") '1.7.10_pre4 在版本列表中显示为 1.7.10-pre4
            If Id <> "1.0" AndAlso Id.EndsWithF(".0") Then Id = Left(Id, Id.Length - 2) 'OptiFine 1.8 的下载会触发此问题，显示版本为 1.8.0
            '获取 Minecraft 版本列表
            Select Case DlClientListLoader.State
                Case LoadState.Finished
                    '从当前的结果获取目标版本…
                    For Each Version As JObject In DlClientListLoader.Output.Value("versions")
                        If Version("id") = Id Then Return Version("url").ToString
                    Next
                    '…如果没有，则重新尝试获取（在版本刚更新时可能出现这种情况，#5195）
                    DlClientListLoader.WaitForExit(Id, IsForceRestart:=True)
                Case LoadState.Loading
                    DlClientListLoader.WaitForExit(Id)
                Case LoadState.Failed, LoadState.Aborted, LoadState.Waiting
                    DlClientListLoader.WaitForExit(Id, IsForceRestart:=True)
            End Select
            '重新查找版本
            For Each Version As JObject In DlClientListLoader.Output.Value("versions")
                If Version("id") = Id Then Return Version("url").ToString
            Next
            Log($"未发现版本 {Id} 的 json 下载地址，版本列表返回为：{vbCrLf}{DlClientListLoader.Output.Value.ToString}", LogLevel.Debug)
            Return Nothing
        Catch ex As Exception
            Log(ex, $"获取版本 {Id} 的 json 下载地址失败")
            Return Nothing
        End Try
    End Function

#End Region

#Region "DlOptiFineList | OptiFine 版本列表"

    Public Structure DlOptiFineListResult
        ''' <summary>
        ''' 数据来源名称，如“Official”，“BMCLAPI”。
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' 是否为官方的实时数据。
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' 获取到的数据。
        ''' </summary>
        Public Value As List(Of DlOptiFineListEntry)
    End Structure

    Public Class DlOptiFineListEntry
        ''' <summary>
        ''' 显示名称，已去除 HD_U 字样，如“1.12.2 C8”。
        ''' </summary>
        Public NameDisplay As String
        ''' <summary>
        ''' 原始文件名称，如“preview_OptiFine_1.11_HD_U_E1_pre.jar”。
        ''' </summary>
        Public NameFile As String
        ''' <summary>
        ''' 对应的版本名称，如“1.13.2-OptiFine_HD_U_E6”。
        ''' </summary>
        Public NameVersion As String
        ''' <summary>
        ''' 是否为测试版。
        ''' </summary>
        Public IsPreview As Boolean
        ''' <summary>
        ''' 对应的 Minecraft 版本，如“1.12.2”。
        ''' </summary>
        Public Property Inherit As String
            Get
                Return _inherit
            End Get
            Set(value As String)
                If value.EndsWithF(".0") Then value = Left(value, value.Length - 2)
                _inherit = value
            End Set
        End Property
        Private _inherit As String
        ''' <summary>
        ''' 发布时间，格式为“yyyy/mm/dd”。OptiFine 源无此数据。
        ''' </summary>
        Public ReleaseTime As String
        ''' <summary>
        ''' 需要的最低 Forge 版本。空字符串为无限制，Nothing 为不兼容，“28.1.56” 表示版本号，“1161” 表示版本号的最后一位。
        ''' </summary>
        Public RequiredForgeVersion As String
    End Class

    ''' <summary>
    ''' OptiFine 版本列表，主加载器。
    ''' </summary>
    Public DlOptiFineListLoader As New LoaderTask(Of Integer, DlOptiFineListResult)("DlOptiFineList Main", AddressOf DlOptiFineListMain)
    Private Sub DlOptiFineListMain(Loader As LoaderTask(Of Integer, DlOptiFineListResult))
        Select Case Setup.Get("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlOptiFineListResult), Integer)(DlOptiFineListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' OptiFine 版本列表，官方源。
    ''' </summary>
    Public DlOptiFineListOfficialLoader As New LoaderTask(Of Integer, DlOptiFineListResult)("DlOptiFineList Official", AddressOf DlOptiFineListOfficialMain)
    Private Sub DlOptiFineListOfficialMain(Loader As LoaderTask(Of Integer, DlOptiFineListResult))
        Dim Result As String = NetGetCodeByClient("https://optifine.net/downloads", Encoding.Default)
        If Result.Length < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Result & "）")
        Try
            '获取所有版本信息
            Dim Forge As List(Of String) = RegexSearch(Result, "(?<=colForge'>)[^<]*")
            Dim ReleaseTime As List(Of String) = RegexSearch(Result, "(?<=colDate'>)[^<]+")
            Dim Name As List(Of String) = RegexSearch(Result, "(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar"")")
            If Not ReleaseTime.Count = Name.Count Then Throw New Exception("版本与发布时间数据无法对应")
            If Not Forge.Count = Name.Count Then Throw New Exception("版本与 Forge 兼容数据无法对应")
            If ReleaseTime.Count < 10 Then Throw New Exception("获取到的版本数量不足（" & Result & "）")
            '转化为列表输出
            Dim Versions As New List(Of DlOptiFineListEntry)
            For i = 0 To ReleaseTime.Count - 1
                Name(i) = Name(i).Replace("_", " ")
                Dim Entry As New DlOptiFineListEntry With {
                             .NameDisplay = Name(i).Replace("HD U ", "").Replace(".0 ", " "),
                             .ReleaseTime = Join({ReleaseTime(i).Split(".")(2), ReleaseTime(i).Split(".")(1), ReleaseTime(i).Split(".")(0)}, "/"),
                             .IsPreview = Name(i).ContainsF("pre", True),
                             .Inherit = Name(i).ToString.Split(" ")(0),
                             .NameFile = If(Name(i).ContainsF("pre", True), "preview_", "") & "OptiFine_" & Name(i).Replace(" ", "_") & ".jar",
                             .RequiredForgeVersion = Forge(i).Replace("Forge ", "").Replace("#", "")}
                If Entry.RequiredForgeVersion.Contains("N/A") Then Entry.RequiredForgeVersion = Nothing
                Entry.NameVersion = Entry.Inherit & "-OptiFine_" & Name(i).ToString.Replace(" ", "_").Replace(Entry.Inherit & "_", "")
                Versions.Add(Entry)
            Next
            Loader.Output = New DlOptiFineListResult With {.IsOfficial = True, .SourceName = "OptiFine 官方源", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("OptiFine 官方源版本列表解析失败（" & Result & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' OptiFine 版本列表，BMCLAPI。
    ''' </summary>
    Public DlOptiFineListBmclapiLoader As New LoaderTask(Of Integer, DlOptiFineListResult)("DlOptiFineList Bmclapi", AddressOf DlOptiFineListBmclapiMain)
    Private Sub DlOptiFineListBmclapiMain(Loader As LoaderTask(Of Integer, DlOptiFineListResult))
        Dim Json As JArray = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/optifine/versionList", IsJson:=True)
        Try
            Dim Versions As New List(Of DlOptiFineListEntry)
            For Each Token As JObject In Json
                Dim Entry As New DlOptiFineListEntry With {
                             .NameDisplay = (Token("mcversion").ToString & Token("type").ToString.Replace("HD_U", "").Replace("_", " ") & " " & Token("patch").ToString).Replace(".0 ", " "),
                             .ReleaseTime = "",
                             .IsPreview = Token("patch").ToString.ContainsF("pre", True),
                             .Inherit = Token("mcversion").ToString,
                             .NameFile = Token("filename").ToString,
                             .RequiredForgeVersion = If(Token("forge"), "").ToString.Replace("Forge ", "").Replace("#", "")
                         }
                If Entry.RequiredForgeVersion.Contains("N/A") Then Entry.RequiredForgeVersion = Nothing
                Entry.NameVersion = Entry.Inherit & "-OptiFine_" & (Token("type").ToString & " " & Token("patch").ToString).Replace(".0 ", " ").Replace(" ", "_").Replace(Entry.Inherit & "_", "")
                Versions.Add(Entry)
            Next
            Loader.Output = New DlOptiFineListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("OptiFine BMCLAPI 版本列表解析失败（" & Json.ToString & "）", ex)
        End Try
    End Sub

#End Region

#Region "DlForgeList | Forge Minecraft 版本列表"

    Public Structure DlForgeListResult
        ''' <summary>
        ''' 数据来源名称，如“Official”，“BMCLAPI”。
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' 是否为官方的实时数据。
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' 获取到的数据。
        ''' </summary>
        Public Value As List(Of String)
    End Structure

    ''' <summary>
    ''' Forge 版本列表，主加载器。
    ''' </summary>
    Public DlForgeListLoader As New LoaderTask(Of Integer, DlForgeListResult)("DlForgeList Main", AddressOf DlForgeListMain)
    Private Sub DlForgeListMain(Loader As LoaderTask(Of Integer, DlForgeListResult))
        Select Case Setup.Get("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlForgeListResult), Integer)(DlForgeListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' Forge 版本列表，官方源。
    ''' </summary>
    Public DlForgeListOfficialLoader As New LoaderTask(Of Integer, DlForgeListResult)("DlForgeList Official", AddressOf DlForgeListOfficialMain)
    Private Sub DlForgeListOfficialMain(Loader As LoaderTask(Of Integer, DlForgeListResult))
        Dim Result As String = NetGetCodeByRequestRetry("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", Encoding.Default, "text/html", UseBrowserUserAgent:=True)
        If Result.Length < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Result & "）")
        '获取所有版本信息
        Dim Names As List(Of String) = RegexSearch(Result, "(?<=a href=""index_)[0-9.]+(_pre[0-9]?)?(?=.html)")
        Names.Add("1.2.4") '1.2.4 不会被匹配上
        If Names.Count < 10 Then Throw New Exception("获取到的版本数量不足（" & Result & "）")
        Loader.Output = New DlForgeListResult With {.IsOfficial = True, .SourceName = "Forge 官方源", .Value = Names}
    End Sub

    ''' <summary>
    ''' Forge 版本列表，BMCLAPI。
    ''' </summary>
    Public DlForgeListBmclapiLoader As New LoaderTask(Of Integer, DlForgeListResult)("DlForgeList Bmclapi", AddressOf DlForgeListBmclapiMain)
    Private Sub DlForgeListBmclapiMain(Loader As LoaderTask(Of Integer, DlForgeListResult))
        Dim Result As String = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/forge/minecraft", Encoding.Default)
        If Result.Length < 200 Then Throw New Exception("获取到的版本列表长度不足（" & Result & "）")
        '获取所有版本信息
        Dim Names As List(Of String) = RegexSearch(Result, "[0-9.]+(_pre[0-9]?)?")
        If Names.Count < 10 Then Throw New Exception("获取到的版本数量不足（" & Result & "）")
        Loader.Output = New DlForgeListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Names}
    End Sub

#End Region

#Region "DlForgeVersion | Forge 版本列表"

    Public MustInherit Class DlForgelikeEntry
        Public IsNeoForge As Boolean
        ''' <summary>
        ''' 加载器名称。Forge 或 NeoForge。
        ''' </summary>
        Public ReadOnly Property LoaderName As String
            Get
                Return If(IsNeoForge, "NeoForge", "Forge")
            End Get
        End Property
        ''' <summary>
        ''' 文件扩展名。不以小数点开头。
        ''' </summary>
        Public ReadOnly Property FileExtension As String
            Get
                If IsNeoForge Then
                    Return "jar"
                Else
                    Return If(CType(Me, DlForgeVersionEntry).Category = "installer", "jar", "zip")
                End If
            End Get
        End Property
        ''' <summary>
        ''' Forge：MC 版本是否小于 1.13。
        ''' NeoForge：MC 版本是否为 1.20.1。
        ''' </summary>
        Public ReadOnly Property IsLegacy As Boolean
            Get
                '虽然很抽象，但确实可以这样判断
                'Forge：1.13+ 的版本号首位都大于 20
                'NeoForge：1.20.1 的版本号首位人为规定为 19 开头
                Return Version.Major < 20
            End Get
        End Property
        ''' <summary>
        ''' 标准化后的版本号，仅可用于比较与排序。
        ''' 格式：Major.Minor.Build.Revision
        ''' Forge：如 “50.1.9.0”（最后一位固定为 0）、“14.22.1.2478”（Legacy）。
        ''' NeoForge：如 “20.4.30.0”（最后一位固定为 0）、“19.47.1.99”（Legacy：第一位固定为 19）。
        ''' </summary>
        Public Version As Version
        ''' <summary>
        ''' 可对玩家显示的非格式化版本名。
        ''' Forge：如 “50.1.9”、“14.22.1.2478”（Legacy）。
        ''' NeoForge：如 “20.4.30-beta”、“47.1.99”（Legacy）。
        ''' </summary>
        Public VersionName As String
        ''' <summary>
        ''' 对应的 Minecraft 版本，如“1.12.2”。
        ''' </summary>
        Public Inherit As String
    End Class

    Public Class DlForgeVersionEntry
        Inherits DlForgelikeEntry
        ''' <summary>
        ''' 发布时间，格式为“yyyy/MM/dd HH:mm”。
        ''' </summary>
        Public ReleaseTime As String
        ''' <summary>
        ''' 文件的 MD5 或 SHA1（BMCLAPI 的老版本是 MD5，新版本是 SHA1；官方源总是 MD5）。
        ''' </summary>
        Public Hash As String = Nothing
        ''' <summary>
        ''' 是否为推荐版本。
        ''' </summary>
        Public IsRecommended As Boolean
        ''' <summary>
        ''' 安装类型。有 installer、client、universal 三种。
        ''' </summary>
        Public Category As String
        ''' <summary>
        ''' 用于下载的文件版本名。可能在 Version 的基础上添加了分支。
        ''' </summary>
        Public FileVersion As String

        Public Sub New(Version As String, Branch As String, Inherit As String)
            '司马版本的特殊处理
            If Version = "11.15.1.2318" OrElse Version = "11.15.1.1902" OrElse Version = "11.15.1.1890" Then Branch = "1.8.9"
            If Branch Is Nothing AndAlso Inherit = "1.7.10" AndAlso Version.Split(".")(3) >= 1300 Then Branch = "1.7.10"
            '为 DlForgelikeEntry 提供所有信息
            IsNeoForge = False
            VersionName = Version
            Me.Version = New Version(Version)
            Me.Inherit = Inherit
            FileVersion = Version & If(Branch Is Nothing, ""， "-" & Branch)
        End Sub
    End Class

    ''' <summary>
    ''' Forge 版本列表，主加载器。
    ''' </summary>
    Public Sub DlForgeVersionMain(Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)))
        Dim DlForgeVersionOfficialLoader As New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion Official", AddressOf DlForgeVersionOfficialMain)
        Dim DlForgeVersionBmclapiLoader As New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion Bmclapi", AddressOf DlForgeVersionBmclapiMain)
        Select Case Setup.Get("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of String, List(Of DlForgeVersionEntry)), Integer)(DlForgeVersionBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' Forge 版本列表，官方源。
    ''' </summary>
    Public Sub DlForgeVersionOfficialMain(Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)))
        Dim Result As String
        Try
            Result = NetGetCodeByLoader("https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_" &
                                          Loader.Input.Replace("-", "_") & '兼容 Forge 1.7.10-pre4，#4057
                                          ".html", UseBrowserUserAgent:=True)
        Catch ex As Exception
            If GetExceptionSummary(ex).Contains("(404)") Then
                Throw New Exception("不可用")
            Else
                Throw
            End If
        End Try
        If Result.Length < 1000 Then Throw New Exception("获取到的版本列表长度不足（" & Result & "）")
        Dim Versions As New List(Of DlForgeVersionEntry)
        Try
            '分割版本信息
            Dim VersionCodes = Mid(Result, 1, Result.LastIndexOfF("</table>")).Split("<td class=""download-version")
            '获取所有版本信息
            For i = 1 To VersionCodes.Count - 1
                Dim VersionCode = VersionCodes(i)
                Try
                    '基础信息获取
                    Dim Name As String = RegexSeek(VersionCode, "(?<=[^(0-9)]+)[0-9\.]+")
                    Dim IsRecommended As Boolean = VersionCode.Contains("fa promo-recommended")
                    Dim Inherit As String = Loader.Input
                    '分支获取
                    Dim Branch As String = RegexSeek(VersionCode, $"(?<=-{Name}-)[^-""]+(?=-[a-z]+.[a-z]{{3}})")
                    If String.IsNullOrWhiteSpace(Branch) Then Branch = Nothing
                    '发布时间获取
                    Dim ReleaseTimeOriginal = RegexSeek(VersionCode, "(?<=""download-time"" title="")[^""]+")
                    Dim ReleaseTimeSplit = ReleaseTimeOriginal.Split(" -:".ToCharArray) '原格式："2021-02-15 03:24:02"
                    Dim ReleaseDate As New Date(ReleaseTimeSplit(0), ReleaseTimeSplit(1), ReleaseTimeSplit(2), '年月日
                                                ReleaseTimeSplit(3), ReleaseTimeSplit(4), ReleaseTimeSplit(5), '时分秒
                                                0, DateTimeKind.Utc) '以 UTC 时间作为标准
                    Dim ReleaseTime As String = ReleaseDate.ToLocalTime.ToString("yyyy'/'MM'/'dd HH':'mm") '时区与格式转换
                    '分类与 MD5 获取
                    Dim MD5 As String, Category As String
                    If VersionCode.Contains("classifier-installer""") Then
                        '类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("installer.jar"))
                        MD5 = RegexSeek(VersionCode, "(?<=MD5:</strong> )[^<]+")
                        Category = "installer"
                    ElseIf VersionCode.Contains("classifier-universal""") Then
                        '类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("universal.zip"))
                        MD5 = RegexSeek(VersionCode, "(?<=MD5:</strong> )[^<]+")
                        Category = "universal"
                    ElseIf VersionCode.Contains("client.zip") Then
                        '类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("client.zip"))
                        MD5 = RegexSeek(VersionCode, "(?<=MD5:</strong> )[^<]+")
                        Category = "client"
                    Else
                        '没有任何下载（1.6.4 有一部分这种情况）
                        Continue For
                    End If
                    '添加进列表
                    Versions.Add(New DlForgeVersionEntry(Name, Branch, Inherit) With {.Category = Category, .IsRecommended = IsRecommended, .Hash = MD5.Trim(vbCr, vbLf), .ReleaseTime = ReleaseTime})
                Catch ex As Exception
                    Throw New Exception("Forge 官方源版本信息提取失败（" & VersionCode & "）", ex)
                End Try
            Next
        Catch ex As Exception
            Throw New Exception("Forge 官方源版本列表解析失败（" & Result & "）", ex)
        End Try
        If Not Versions.Any() Then Throw New Exception("不可用")
        Loader.Output = Versions
    End Sub

    ''' <summary>
    ''' Forge 版本列表，BMCLAPI。
    ''' </summary>
    Public Sub DlForgeVersionBmclapiMain(Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)))
        Dim Json As JArray = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/forge/minecraft/" &
                                                      Loader.Input.Replace("-", "_"), '兼容 Forge 1.7.10-pre4，#4057
                                                      IsJson:=True)
        Dim Versions As New List(Of DlForgeVersionEntry)
        Try
            Dim Recommended As String = McDownloadForgeRecommendedGet(Loader.Input)
            For Each Token As JObject In Json
                '分类与 Hash 获取
                Dim Hash As String = Nothing, Category As String = "unknown", Proi As Integer = -1
                For Each File As JObject In Token("files")
                    Select Case File("category").ToString
                        Case "installer"
                            If File("format").ToString = "jar" Then
                                '类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                                Hash = File("hash")
                                Category = "installer"
                                Proi = 2
                            End If
                        Case "universal"
                            If Proi <= 1 AndAlso File("format").ToString = "zip" Then
                                '类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                                Hash = File("hash")
                                Category = "universal"
                                Proi = 1
                            End If
                        Case "client"
                            If Proi <= 0 AndAlso File("format").ToString = "zip" Then
                                '类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                                Hash = File("hash")
                                Category = "client"
                                Proi = 0
                            End If
                    End Select
                Next
                '获取 Entry
                Dim Branch As String = Token("branch")
                Dim Name As String = Token("version")
                '基础信息获取
                Dim Entry = New DlForgeVersionEntry(Name, Branch, Loader.Input) With {.Hash = Hash, .Category = Category, .IsRecommended = Recommended = Name}
                Dim TimeSplit = Token("modified").ToString.Split("-"c, "T"c, ":"c, "."c, " "c, "/"c)
                Entry.ReleaseTime = Token("modified").ToObject(Of Date).ToLocalTime.ToString("yyyy'/'MM'/'dd HH':'mm")
                '添加项
                Versions.Add(Entry)
            Next
        Catch ex As Exception
            Throw New Exception("Forge BMCLAPI 版本列表解析失败（" & Json.ToString & "）", ex)
        End Try
        If Not Versions.Any() Then Throw New Exception("不可用")
        Loader.Output = Versions
    End Sub

#End Region

#Region "DlNeoForgeList | NeoForge 版本列表"

    Public Structure DlNeoForgeListResult
        ''' <summary>
        ''' 数据来源名称，如“Official”，“BMCLAPI”。
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' 是否为官方的实时数据。
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' 所有版本的列表。已经按从新到老排序。
        ''' </summary>
        Public Value As List(Of DlNeoForgeListEntry)
    End Structure

    Public Class DlNeoForgeListEntry
        Inherits DlForgelikeEntry
        ''' <summary>
        ''' 是否是 Beta 版。
        ''' </summary>
        Public IsBeta As Boolean
        ''' <summary>
        ''' API 使用的原始版本字符串，如 “20.4.30-beta”、“1.20.1-47.1.99”（Legacy）。
        ''' </summary>
        Public ApiName As String
        ''' <summary>
        ''' 文件在官网的基础地址，不包含后缀。
        ''' </summary>
        Public ReadOnly Property UrlBase As String
            Get
                Dim PackageName As String = If(IsLegacy, "forge", "neoforge")
                Return $"https://maven.neoforged.net/releases/net/neoforged/{PackageName}/{ApiName}/{PackageName}-{ApiName}"
            End Get
        End Property

        Public Sub New(ApiName As String)
            IsNeoForge = True
            Me.ApiName = ApiName
            IsBeta = ApiName.Contains("beta")
            If ApiName.Contains("1.20.1") Then '1.20.1-47.1.99
                VersionName = ApiName.Replace("1.20.1-", "")
                Version = New Version("19." & VersionName)
                Inherit = "1.20.1"
            Else '20.4.30-beta
                VersionName = ApiName
                Version = New Version(ApiName.BeforeFirst("-"))
                Inherit = $"1.{Version.Major}" & If(Version.Minor = 0, "", "." & Version.Minor)
            End If
        End Sub
    End Class

    ''' <summary>
    ''' NeoForge 版本列表，主加载器。
    ''' </summary>
    Public DlNeoForgeListLoader As New LoaderTask(Of Integer, DlNeoForgeListResult)("DlNeoForgeList Main", AddressOf DlNeoForgeListMain)
    Private Sub DlNeoForgeListMain(Loader As LoaderTask(Of Integer, DlNeoForgeListResult))
        Select Case Setup.Get("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlNeoForgeListResult), Integer)(DlNeoForgeListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' NeoForge 版本列表，官方源。
    ''' </summary>
    Public DlNeoForgeListOfficialLoader As New LoaderTask(Of Integer, DlNeoForgeListResult)("DlNeoForgeList Official", AddressOf DlNeoForgeListOfficialMain)
    Private Sub DlNeoForgeListOfficialMain(Loader As LoaderTask(Of Integer, DlNeoForgeListResult))
        '获取版本列表 JSON
        Dim ResultLatest As String = NetGetCodeByLoader("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", UseBrowserUserAgent:=True, IsJson:=True)
        Dim ResultLegacy As String = NetGetCodeByLoader("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", UseBrowserUserAgent:=True, IsJson:=True)
        If ResultLatest.Length < 100 OrElse ResultLegacy.Length < 100 Then Throw New Exception("获取到的版本列表长度不足（" & ResultLatest & "）")
        '解析
        Try
            Loader.Output = New DlNeoForgeListResult With {.IsOfficial = True, .SourceName = "NeoForge 官方源",
                .Value = GetNeoForgeEntries(ResultLatest, ResultLegacy)}
        Catch ex As Exception
            Throw New Exception("NeoForge 官方源版本列表解析失败（" & ResultLatest & vbCrLf & vbCrLf & ResultLegacy & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' NeoForge 版本列表，BMCLAPI。
    ''' </summary>
    Public DlNeoForgeListBmclapiLoader As New LoaderTask(Of Integer, DlNeoForgeListResult)("DlNeoForgeList Bmclapi", AddressOf DlNeoForgeListBmclapiMain)
    Public Sub DlNeoForgeListBmclapiMain(Loader As LoaderTask(Of Integer, DlNeoForgeListResult))
        '获取版本列表 JSON
        Dim ResultLatest As String = NetGetCodeByLoader("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge", UseBrowserUserAgent:=True, IsJson:=True)
        Dim ResultLegacy As String = NetGetCodeByLoader("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge", UseBrowserUserAgent:=True, IsJson:=True)
        If ResultLatest.Length < 100 OrElse ResultLegacy.Length < 100 Then Throw New Exception("获取到的版本列表长度不足（" & ResultLatest & "）")
        '解析
        Try
            Loader.Output = New DlNeoForgeListResult With {.IsOfficial = True, .SourceName = "BMCLAPI",
                .Value = GetNeoForgeEntries(ResultLatest, ResultLegacy)}
        Catch ex As Exception
            Throw New Exception("NeoForge BMCLAPI 版本列表解析失败（" & ResultLatest & vbCrLf & vbCrLf & ResultLegacy & "）", ex)
        End Try
    End Sub

    Private Function GetNeoForgeEntries(LatestJson As String, LatestLegacyJson As String) As List(Of DlNeoForgeListEntry)
        Dim VersionNames = RegexSearch(LatestLegacyJson & LatestJson,
            "(?<="")(1\.20\.1-)?\d+\.\d+\.\d+(-beta)?(?="")") '我寻思直接正则就行.jpg
        Dim Versions = VersionNames.
            Where(Function(name) name <> "47.1.82"). '这个版本虽然在版本列表中，但不能下载
            Select(Function(name) New DlNeoForgeListEntry(name)).ToList
        If Not Versions.Any() Then Throw New Exception("不可用")
        Versions = Versions.OrderByDescending(Function(a) a.Version).ToList
        Return Versions
    End Function

#End Region

#Region "DlLiteLoaderList | LiteLoader 版本列表"

    Public Structure DlLiteLoaderListResult
        ''' <summary>
        ''' 数据来源名称，如“Official”，“BMCLAPI”。
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' 是否为官方的实时数据。
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' 获取到的数据。
        ''' </summary>
        Public Value As List(Of DlLiteLoaderListEntry)
        ''' <summary>
        ''' 官方源的失败原因。若没有则为 Nothing。
        ''' </summary>
        Public OfficialError As Exception
    End Structure

    Public Class DlLiteLoaderListEntry
        ''' <summary>
        ''' 实际的文件名，如“liteloader-installer-1.12-00-SNAPSHOT.jar”。
        ''' </summary>
        Public FileName As String
        ''' <summary>
        ''' 是否为测试版。
        ''' </summary>
        Public IsPreview As Boolean
        ''' <summary>
        ''' 对应的 Minecraft 版本，如“1.12.2”。
        ''' </summary>
        Public Inherit As String
        ''' <summary>
        ''' 是否为 1.7 及更早的远古版。
        ''' </summary>
        Public IsLegacy As Boolean
        ''' <summary>
        ''' 发布时间，格式为“yyyy/mm/dd HH:mm”。
        ''' </summary>
        Public ReleaseTime As String
        ''' <summary>
        ''' 文件的 MD5。
        ''' </summary>
        Public MD5 As String
        ''' <summary>
        ''' 对应的 Json 项。
        ''' </summary>
        Public JsonToken As JToken
    End Class

    ''' <summary>
    ''' LiteLoader 版本列表，主加载器。
    ''' </summary>
    Public DlLiteLoaderListLoader As New LoaderTask(Of Integer, DlLiteLoaderListResult)("DlLiteLoaderList Main", AddressOf DlLiteLoaderListMain)
    Private Sub DlLiteLoaderListMain(Loader As LoaderTask(Of Integer, DlLiteLoaderListResult))
        Select Case Setup.Get("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlLiteLoaderListResult), Integer)(DlLiteLoaderListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' LiteLoader 版本列表，官方源。
    ''' </summary>
    Public DlLiteLoaderListOfficialLoader As New LoaderTask(Of Integer, DlLiteLoaderListResult)("DlLiteLoaderList Official", AddressOf DlLiteLoaderListOfficialMain)
    Private Sub DlLiteLoaderListOfficialMain(Loader As LoaderTask(Of Integer, DlLiteLoaderListResult))
        Dim Result As JObject = NetGetCodeByRequestRetry("https://dl.liteloader.com/versions/versions.json", IsJson:=True)
        Try
            Dim Json As JObject = Result("versions")
            Dim Versions As New List(Of DlLiteLoaderListEntry)
            For Each Pair As KeyValuePair(Of String, JToken) In Json
                If Pair.Key.StartsWithF("1.6") OrElse Pair.Key.StartsWithF("1.5") Then Continue For
                Dim RealEntry As JToken = If(Pair.Value("artefacts"), Pair.Value("snapshots"))("com.mumfrey:liteloader")("latest")
                Versions.Add(New DlLiteLoaderListEntry With {
                             .Inherit = Pair.Key,
                             .IsLegacy = Pair.Key.Split(".")(1) < 8,
                             .IsPreview = RealEntry("stream").ToString.ToLower = "snapshot",
                             .FileName = "liteloader-installer-" & Pair.Key & If(Pair.Key = "1.8" OrElse Pair.Key = "1.9", ".0", "") & "-00-SNAPSHOT.jar",
                             .MD5 = RealEntry("md5"),
                             .ReleaseTime = GetLocalTime(GetDate(RealEntry("timestamp"))).ToString("yyyy'/'MM'/'dd HH':'mm"),
                             .JsonToken = RealEntry
                         })
            Next
            Loader.Output = New DlLiteLoaderListResult With {.IsOfficial = True, .SourceName = "LiteLoader 官方源", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("LiteLoader 官方源版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' LiteLoader 版本列表，BMCLAPI。
    ''' </summary>
    Public DlLiteLoaderListBmclapiLoader As New LoaderTask(Of Integer, DlLiteLoaderListResult)("DlLiteLoaderList Bmclapi", AddressOf DlLiteLoaderListBmclapiMain)
    Private Sub DlLiteLoaderListBmclapiMain(Loader As LoaderTask(Of Integer, DlLiteLoaderListResult))
        Dim Result As JObject = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json", IsJson:=True)
        Try
            Dim Json As JObject = Result("versions")
            Dim Versions As New List(Of DlLiteLoaderListEntry)
            For Each Pair As KeyValuePair(Of String, JToken) In Json
                If Pair.Key.StartsWithF("1.6") OrElse Pair.Key.StartsWithF("1.5") Then Continue For
                Dim RealEntry As JToken = If(Pair.Value("artefacts"), Pair.Value("snapshots"))("com.mumfrey:liteloader")("latest")
                Versions.Add(New DlLiteLoaderListEntry With {
                             .Inherit = Pair.Key,
                             .IsLegacy = Pair.Key.Split(".")(1) < 8,
                             .IsPreview = RealEntry("stream").ToString.ToLower = "snapshot",
                             .FileName = "liteloader-installer-" & Pair.Key & If(Pair.Key = "1.8" OrElse Pair.Key = "1.9", ".0", "") & "-00-SNAPSHOT.jar",
                             .MD5 = RealEntry("md5"),
                             .ReleaseTime = GetLocalTime(GetDate(RealEntry("timestamp"))).ToString("yyyy'/'MM'/'dd HH':'mm"),
                             .JsonToken = RealEntry
                         })
            Next
            Loader.Output = New DlLiteLoaderListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Versions}
        Catch ex As Exception
            Throw New Exception("LiteLoader BMCLAPI 版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

#End Region

#Region "DlFabricList | Fabric 列表"

    Public Structure DlFabricListResult
        ''' <summary>
        ''' 数据来源名称，如“Official”，“BMCLAPI”。
        ''' </summary>
        Public SourceName As String
        ''' <summary>
        ''' 是否为官方的实时数据。
        ''' </summary>
        Public IsOfficial As Boolean
        ''' <summary>
        ''' 获取到的数据。
        ''' </summary>
        Public Value As JObject
    End Structure

    ''' <summary>
    ''' Fabric 列表，主加载器。
    ''' </summary>
    Public DlFabricListLoader As New LoaderTask(Of Integer, DlFabricListResult)("DlFabricList Main", AddressOf DlFabricListMain)
    Private Sub DlFabricListMain(Loader As LoaderTask(Of Integer, DlFabricListResult))
        Select Case Setup.Get("ToolDownloadVersion")
            Case 0
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListBmclapiLoader, 30),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListOfficialLoader, 30 + 60)
                }, Loader.IsForceRestarting)
            Case 1
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListOfficialLoader, 5),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListBmclapiLoader, 5 + 30)
                }, Loader.IsForceRestarting)
            Case Else
                DlSourceLoader(Loader, New List(Of KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)) From {
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListOfficialLoader, 60),
                    New KeyValuePair(Of LoaderTask(Of Integer, DlFabricListResult), Integer)(DlFabricListBmclapiLoader, 60 + 60)
                }, Loader.IsForceRestarting)
        End Select
    End Sub

    ''' <summary>
    ''' Fabric 列表，官方源。
    ''' </summary>
    Public DlFabricListOfficialLoader As New LoaderTask(Of Integer, DlFabricListResult)("DlFabricList Official", AddressOf DlFabricListOfficialMain)
    Private Sub DlFabricListOfficialMain(Loader As LoaderTask(Of Integer, DlFabricListResult))
        Dim Result As JObject = NetGetCodeByRequestRetry("https://meta.fabricmc.net/v2/versions", IsJson:=True)
        Try
            Dim Output = New DlFabricListResult With {.IsOfficial = True, .SourceName = "Fabric 官方源", .Value = Result}
            If Output.Value("game") Is Nothing OrElse Output.Value("loader") Is Nothing OrElse Output.Value("installer") Is Nothing Then Throw New Exception("获取到的列表缺乏必要项")
            Loader.Output = Output
        Catch ex As Exception
            Throw New Exception("Fabric 官方源版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Fabric 列表，BMCLAPI。
    ''' </summary>
    Public DlFabricListBmclapiLoader As New LoaderTask(Of Integer, DlFabricListResult)("DlFabricList Bmclapi", AddressOf DlFabricListBmclapiMain)
    Private Sub DlFabricListBmclapiMain(Loader As LoaderTask(Of Integer, DlFabricListResult))
        Dim Result As JObject = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/fabric-meta/v2/versions", IsJson:=True)
        Try
            Dim Output = New DlFabricListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Result}
            If Output.Value("game") Is Nothing OrElse Output.Value("loader") Is Nothing OrElse Output.Value("installer") Is Nothing Then Throw New Exception("获取到的列表缺乏必要项")
            Loader.Output = Output
        Catch ex As Exception
            Throw New Exception("Fabric BMCLAPI 版本列表解析失败（" & Result.ToString & "）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Fabric API 列表，官方源。
    ''' </summary>
    Public DlFabricApiLoader As New LoaderTask(Of Integer, List(Of CompFile))("Fabric API List Loader",
        Sub(Task As LoaderTask(Of Integer, List(Of CompFile))) Task.Output = CompFilesGet("fabric-api", False))

    ''' <summary>
    ''' OptiFabric 列表，官方源。
    ''' </summary>
    Public DlOptiFabricLoader As New LoaderTask(Of Integer, List(Of CompFile))("OptiFabric List Loader",
        Sub(Task As LoaderTask(Of Integer, List(Of CompFile))) Task.Output = CompFilesGet("322385", True))

#End Region

#Region "DlMod | Mod 镜像源请求"

    ''' <summary>
    ''' 对可能涉及 Mod 镜像源的请求进行处理，返回字符串或 JObject。
    ''' 调用 NetGetCodeByRequest，会进行重试。
    ''' </summary>
    Public Function DlModRequest(Url As String, Optional IsJson As Boolean = False) As Object
        Dim Urls As New List(Of KeyValuePair(Of String, Integer))
        Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
        Urls.Add(New KeyValuePair(Of String, Integer)(Url, 20))
        'Dim McimUrl As String = DlSourceModGet(Url)
        'If McimUrl <> Url Then
        '    Select Case Setup.Get("ToolDownloadMod")
        '        Case 0
        '            Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
        '            Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
        '            Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
        '        Case 1
        '            Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
        '            Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
        '            Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
        '            Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
        '        Case Else
        '            Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
        '            Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
        '            Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
        '    End Select
        'End If
        Dim Exs As String = ""
        For Each Source In Urls
            Try
                Return NetGetCodeByRequestOnce(Source.Key, Encode:=Encoding.UTF8, Timeout:=Source.Value * 1000, IsJson:=IsJson, UseBrowserUserAgent:=True)
            Catch ex As Exception
                Exs += ex.Message + vbCrLf
            End Try
        Next
        Throw New Exception(Exs)
    End Function

    ''' <summary>
    ''' 对可能涉及 Mod 镜像源的请求进行处理。
    ''' 调用 NetRequest，会进行重试。
    ''' </summary>
    Public Function DlModRequest(Url As String, Method As String, Data As String, ContentType As String) As String
        Dim Urls As New List(Of KeyValuePair(Of String, Integer))
        Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
        Urls.Add(New KeyValuePair(Of String, Integer)(Url, 20))
        'Dim McimUrl As String = DlSourceModGet(Url)
        'If McimUrl <> Url Then
        '   Select Case Setup.Get("ToolDownloadMod")
        '       Case 0
        '           Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
        '           Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
        '           Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
        '       Case 1
        '           Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
        '           Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 5))
        '           Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
        '           Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
        '       Case Else
        '           Urls.Add(New KeyValuePair(Of String, Integer)(Url, 5))
        '           Urls.Add(New KeyValuePair(Of String, Integer)(Url, 15))
        '           Urls.Add(New KeyValuePair(Of String, Integer)(McimUrl, 10))
        '   End Select
        'End If
        Dim Exs As String = ""
        For Each Source In Urls
            Try
                Return NetRequestOnce(Source.Key, Method, Data, ContentType, Timeout:=Source.Value * 1000)
            Catch ex As Exception
                Exs += ex.Message + vbCrLf
            End Try
        Next
        Throw New Exception(Exs)
    End Function

#End Region

#Region "DlSource | 镜像下载源"

    Private DlPreferMojang As Boolean = False
    ''' <summary>
    ''' 下载文件（而非获取版本列表）的时候，是否优先使用官方源。
    ''' </summary>
    Public ReadOnly Property DlSourcePreferMojang As Boolean
        Get
            Return Setup.Get("ToolDownloadSource") = 2 OrElse (Setup.Get("ToolDownloadSource") = 1 AndAlso DlPreferMojang)
        End Get
    End Property
    ''' <summary>
    ''' 下载文件（而非获取版本列表）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    ''' </summary>
    Public Function DlSourceOrder(OfficialUrls As IEnumerable(Of String), MirrorUrls As IEnumerable(Of String)) As IEnumerable(Of String)
        Return If(DlSourcePreferMojang, OfficialUrls.Union(MirrorUrls), MirrorUrls.Union(OfficialUrls))
    End Function
    ''' <summary>
    ''' 获取版本列表（而非下载文件）的时候，是否优先使用官方源。
    ''' </summary>
    Public ReadOnly Property DlVersionListPreferMojang As Boolean
        Get
            Return Setup.Get("ToolDownloadVersion") = 2 OrElse (Setup.Get("ToolDownloadVersion") = 1 AndAlso DlPreferMojang)
        End Get
    End Property
    ''' <summary>
    ''' 获取版本列表（而非下载文件）的时候，根据是否优先使用官方源决定使用 Url 的顺序。
    ''' </summary>
    Public Function DlVersionListOrder(OfficialUrls As IEnumerable(Of String), MirrorUrls As IEnumerable(Of String)) As IEnumerable(Of String)
        Return If(DlVersionListPreferMojang, OfficialUrls.Union(MirrorUrls), MirrorUrls.Union(OfficialUrls))
    End Function


    ''' <summary>
    ''' 下载 Assets 文件。
    ''' </summary>
    Public Function DlSourceAssetsGet(Original As String) As IEnumerable(Of String)
        Original = Original.Replace("http://resources.download.minecraft.net", "https://resources.download.minecraft.net")
        Return DlSourceOrder(
            {Original},
            {Original.
                Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/assets").
                Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/assets").
                Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets")
            })
    End Function
    ''' <summary>
    ''' 下载 Libraries 文件。
    ''' </summary>
    Public Function DlSourceLibraryGet(Original As String) As IEnumerable(Of String)
        If {"minecraftforge", "fabricmc", "neoforged"}.Any(Function(k) Original.Contains(k)) Then '不添加原版源
            Return {
                Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven"),
                Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries")
            }
        Else
            Return DlSourceOrder(
                {Original},
                {Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven"),
                 Original.
                    Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries").
                    Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries")
                })
        End If
    End Function
    ''' <summary>
    ''' 下载 Launcher 或 Meta 文件。
    ''' 不应使用它来获取版本列表（因为它只使用文件下载源设置来决定源顺序）。
    ''' </summary>
    Public Function DlSourceLauncherOrMetaGet(Original As String) As IEnumerable(Of String)
        If Original Is Nothing Then Throw New Exception("无对应的 json 下载地址")
        Return DlSourceOrder(
            {Original},
            {Original.
                Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com").
                Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com").
                Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com").
                Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
            })
    End Function

    'Mod 下载源
    Public Function DlSourceModGet(Original As String) As String
        Return Original
    End Function

    'Loader 自动切换
    Private Sub DlSourceLoader(Of InputType, OutputType)(MainLoader As LoaderTask(Of InputType, OutputType),
                                                         LoaderList As List(Of KeyValuePair(Of LoaderTask(Of InputType, OutputType), Integer)),
                                                         Optional IsForceRestart As Boolean = False)
        Dim WaitCycle As Integer = 0
        Do While True
            '检查状态
            Dim BeforeLoadersAllFailed As Boolean = True
            For Each SubLoader In LoaderList
                If WaitCycle = 0 Then '判断是否可以不加载，直接使用已经加载好的结果
                    If IsForceRestart Then Continue For '强制刷新，不行
                    If (SubLoader.Key.Input Is Nothing Xor MainLoader.Input Is Nothing) OrElse
                       (SubLoader.Key.Input IsNot Nothing AndAlso Not SubLoader.Key.Input.Equals(MainLoader.Input)) Then Continue For '父子加载器的输入不一样，也不行
                End If
                If SubLoader.Key.State <> LoadState.Failed Then BeforeLoadersAllFailed = False
                If SubLoader.Key.State = LoadState.Finished Then
                    '检查加载器成功
                    MainLoader.Output = SubLoader.Key.Output
                    DlSourceLoaderAbort(LoaderList)
                    Return
                ElseIf BeforeLoadersAllFailed Then
                    '此前的加载器全部失败，直接启动后续加载器
                    If WaitCycle < SubLoader.Value * 100 Then WaitCycle = SubLoader.Value * 100
                End If
            Next
            '第一轮时：既然不直接使用已经加载好的结果，那就启动第一个加载器
            If WaitCycle = 0 Then
                LoaderList.First.Key.Start(MainLoader.Input, IsForceRestart)
                For Each Loader In LoaderList.Skip(1)
                    Loader.Key.State = LoadState.Waiting '将其他源标记为未启动，以确保可以切换下载源（#184）
                Next
            End If
            '检查加载器失败或超时
            For i = 0 To LoaderList.Count - 1
                If WaitCycle <> LoaderList(i).Value * 100 Then Continue For
                If i < LoaderList.Count - 1 AndAlso Not LoaderList.All(Function(l) l.Key.State = LoadState.Failed) Then
                    '若还有下一个源，则启动下一个源
                    LoaderList(i + 1).Key.Start(MainLoader.Input, IsForceRestart)
                Else
                    '若没有，则失败
                    Dim ErrorInfo As Exception = Nothing
                    For ii = 0 To LoaderList.Count - 1
                        LoaderList(ii).Key.Input = Nothing '重置输入，以免以同样的输入“重试加载”时直接失败
                        If LoaderList(ii).Key.Error IsNot Nothing Then
                            If ErrorInfo Is Nothing OrElse LoaderList(ii).Key.Error.Message.Contains("不可用") Then
                                ErrorInfo = LoaderList(ii).Key.Error
                            End If
                        End If
                    Next
                    If ErrorInfo Is Nothing Then ErrorInfo = New TimeoutException("下载源连接超时")
                    DlSourceLoaderAbort(LoaderList)
                    Throw ErrorInfo
                End If
                Exit For
            Next
            '计时
            Thread.Sleep(10)
            WaitCycle += 1
            '检查父加载器中断
            If MainLoader.IsAborted Then
                DlSourceLoaderAbort(LoaderList)
                Return
            End If
        Loop
    End Sub
    Private Sub DlSourceLoaderAbort(Of InputType, OutputType)(LoaderList As List(Of KeyValuePair(Of LoaderTask(Of InputType, OutputType), Integer)))
        For Each Loader In LoaderList
            If Loader.Key.State = LoadState.Loading Then Loader.Key.Abort()
        Next
    End Sub

#End Region

End Module
