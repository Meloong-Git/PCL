Public Module ModDownloadLib

#Region "Minecraft 下载"

    ''' <summary>
    ''' 获取下载某个 Minecraft 版本的加载器列表。
    ''' 它必须安装到 McFolderSelected，但是可以自定义版本名（不过自定义的版本名不会修改 Json 中的 id 项）。
    ''' </summary>
    Private Function McDownloadClientLoader(Id As String, Optional JsonUrl As String = Nothing, Optional InstanceName As String = Nothing) As List(Of LoaderBase)
        InstanceName = If(InstanceName, Id)
        Dim VersionFolder As String = McFolderSelected & "versions\" & InstanceName & "\"

        Dim Loaders As New List(Of LoaderBase)

        '下载版本 Json 文件
        If JsonUrl Is Nothing Then
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModDownloadLibTaskMcDownloadVanillaJsonUrl"),
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                Dim JsonAddress As String = DlClientListGet(Id)
                Task.Output = New List(Of NetFile) From {New NetFile(DlSourceLauncherOrMetaGet(JsonAddress), VersionFolder & InstanceName & ".json")}
            End Sub) With {.ProgressWeight = 2, .Show = False})
        End If
        Loaders.Add(New LoaderDownload(McDownloadClientJsonName, New List(Of NetFile) From {
            New NetFile(DlSourceLauncherOrMetaGet(If(JsonUrl, "")), VersionFolder & InstanceName & ".json", New FileChecker With {.IsJson = True})
        }) With {.ProgressWeight = 3})

        '下载支持库文件
        Dim LoadersLib As New List(Of LoaderBase)
        LoadersLib.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModDownloadLibTaskMcDownloadVanillaAnalysisLibSubLoader"),
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            Thread.Sleep(50) '等待 JSON 文件实际写入硬盘（#3710）
            Logger.Info($"开始分析原版支持库文件：{VersionFolder}")
            Task.Output = McLibNetFilesFromInstance(New McInstance(VersionFolder))
        End Sub) With {.ProgressWeight = 1, .Show = False})
        LoadersLib.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskMcDownloadVanillaDownloadLibSubLoader"), New List(Of NetFile)) With {.ProgressWeight = 13, .Show = False})
        Loaders.Add(New LoaderCombo(Of String)(McDownloadClientLibName, LoadersLib) With {.Block = False, .ProgressWeight = 14})

        '下载资源文件
        Dim LoadersAssets As New List(Of LoaderBase)
        LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModDownloadLibTaskMcDownloadAnalysisAssetsIndexSubLoader"),
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            Try
                Dim AssetIndex = DlClientAssetIndexGet(New McInstance(VersionFolder))
                Task.Output = If(AssetIndex Is Nothing, New List(Of NetFile), New List(Of NetFile) From {AssetIndex})
            Catch ex As Exception
                Throw New Exception(GetLang("LangModDownloadLibExceptionAnalysisAssetsIndexFail"), ex)
            End Try
            '顺手添加 Json 项目
            Try
                Dim InstanceJson As JObject = FileUtils.ReadAsJson(VersionFolder & InstanceName & ".json")
                InstanceJson.Add("clientVersion", Id)
                FileUtils.Write($"{VersionFolder}{InstanceName}.json", InstanceJson.ToString)
            Catch ex As Exception
                Throw New Exception(GetLang("LangModDownloadLibExceptionAddVersionInfoFail"), ex)
            End Try
        End Sub) With {.ProgressWeight = 1, .Show = False})
        LoadersAssets.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskDownloadAssetsIndexFail"), New List(Of NetFile)) With {.ProgressWeight = 3, .Show = False})
        LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModDownloadLibTaskAnalysisDownloadAssets"),
            Sub(Task) Task.Output = McAssetsFixList(New McInstance(VersionFolder), True, Task)) With {.ProgressWeight = 0.01, .Show = False})
        LoadersAssets.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskDownloadAssets"), New List(Of NetFile)) With {.ProgressWeight = 14, .Show = False})
        Loaders.Add(New LoaderCombo(Of String)(GetLang("LangPageSpeedRightDownloadVanillaResource"), LoadersAssets) With {.Block = False, .ProgressWeight = 18})

        Return Loaders

    End Function
    Private McDownloadClientLibName As String = GetLang("LangPageSpeedRightDownloadVanillaSupportLibrary")
    Private McDownloadClientJsonName As String = GetLang("LangPageSpeedRightDownloadVanillaJSON")

#End Region

#Region "Minecraft 下载菜单"

    Public Function McDownloadListItem(Entry As JObject, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            '确定图标
            Dim Logo As String
            Select Case Entry("type")
                Case "release"
                    Logo = PathImage & "Blocks/Grass.png"
                Case "snapshot"
                    Logo = PathImage & "Blocks/CommandBlock.png"
                Case "special"
                    Logo = PathImage & "Blocks/GoldBlock.png"
                Case Else
                    Logo = PathImage & "Blocks/CobbleStone.png"
            End Select
            '建立控件
            Dim NewItem As New MyListItem With {.Logo = Logo, .SnapsToDevicePixels = True, .Title = Entry("id").ToString, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry}
            If Entry("lore") Is Nothing Then
                NewItem.Info = GetLocalTimeFormat(Entry("releaseTime").Value(Of Date))
            Else
                NewItem.Info = Entry("lore").ToString
            End If
            If Entry("url").ToString.Contains("pcl") Then NewItem.Info = "[" & GetLang("LangDownloadPCLProvided") & "] " & NewItem.Info
            AddHandler NewItem.Click, OnClick
            '建立菜单
            If IsSaveOnly Then
                NewItem.ContentHandler = AddressOf McDownloadSaveMenuBuild
            Else
                NewItem.ContentHandler = AddressOf McDownloadMenuBuild
            End If
            '结束
            Return NewItem
        End Function) With {.Height = 42}
    End Function
    Private Sub McDownloadSaveMenuBuild(sender As Object, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf McDownloadMenuLog
        Dim BtnServer As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonServer, .ToolTip = GetLang("LangDownloadServer")}
        AddHandler BtnServer.Click, AddressOf McDownloadMenuSaveServer
        sender.Buttons = {BtnServer, BtnInfo}
    End Sub
    Private Sub McDownloadMenuBuild(sender As Object, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = GetLang("LangDownloadSaveAs")}
        AddHandler BtnSave.Click, AddressOf McDownloadMenuSave
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf McDownloadMenuLog
        Dim BtnServer As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonServer, .ToolTip = GetLang("LangDownloadServer")}
        AddHandler BtnServer.Click, AddressOf McDownloadMenuSaveServer
        sender.Buttons = {BtnSave, BtnInfo, BtnServer}
    End Sub
    Private Sub McDownloadMenuLog(sender As Object, e As RoutedEventArgs)
        Dim Version As JToken
        If sender.Tag IsNot Nothing Then
            Version = sender.Tag
        ElseIf sender.Parent.Tag IsNot Nothing Then
            Version = sender.Parent.Tag
        Else
            Version = sender.Parent.Parent.Tag
        End If
        McUpdateLogShow(Version)
    End Sub
    Private Sub McDownloadMenuSaveServer(sender As Object, e As RoutedEventArgs)
        Dim Version As MyListItem
        If TypeOf sender Is MyListItem Then
            Version = sender
        ElseIf TypeOf sender.Parent Is MyListItem Then
            Version = sender.Parent
        Else
            Version = sender.Parent.Parent
        End If
        Try
            Dim Id = Version.Title
            Dim JsonUrl = Version.Tag("url").ToString
            Dim VersionFolder As String = Dialogs.SelectFolder("选择目标文件夹", False).FirstOrDefault
            If VersionFolder Is Nothing Then Return
            VersionFolder = VersionFolder & Id & "\"

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> GetLang("LangModDownloadLibTaskDownloadServer", Id) Then Continue For
                Hint(GetLang("LangModDownloadLibTaskDownloadServerHintDownloading"), HintType.Red)
                Return
            Next

            Dim Loaders As New List(Of LoaderBase)
            '下载版本 JSON 文件
            Loaders.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskMcDownloadJson"), New List(Of NetFile) From {
                New NetFile(DlSourceLauncherOrMetaGet(JsonUrl), VersionFolder & Id & ".json", New FileChecker With {.IsJson = True})
            }) With {.ProgressWeight = 2})
            '构建服务端
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModDownloadLibTaskDownloadServerBuildServer"),
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                '分析服务端 JAR 文件下载地址
                Dim Instance As New McInstance(VersionFolder)
                If Instance.JsonObject("downloads") Is Nothing OrElse Instance.JsonObject("downloads")("server") Is Nothing OrElse Instance.JsonObject("downloads")("server")("url") Is Nothing Then
                    FileUtils.Delete(VersionFolder & Id & ".json")
                    If Not DirectoryUtils.GetInfo(VersionFolder).GetFileSystemInfos.Any() Then DirectoryUtils.Delete(VersionFolder)
                    Task.Output = New List(Of NetFile)
                    Hint(GetLang("LangModDownloadLibExceptionTaskDownloadServerNoResource", Id), HintType.Red)
                    Thread.Sleep(2000) '等玩家把上一个提示看完
                    Task.Cancel()
                    Return
                End If
                Dim JarUrl As String = Instance.JsonObject("downloads")("server")("url")
                Dim Checker As New FileChecker With {.MinSize = 1024, .ActualSize = If(Instance.JsonObject("downloads")("server")("size"), -1), .Hash = Instance.JsonObject("downloads")("server")("sha1")}
                Task.Output = New List(Of NetFile) From {New NetFile(DlSourceLauncherOrMetaGet(JarUrl), VersionFolder & Id & "-server.jar", Checker)}
                '添加启动脚本
                Dim Bat As String =
$"@echo off
title {Id} 原版服务端
echo 如果服务端立即停止，请右键编辑该脚本，将下一行开头的 java 替换为适合该 Minecraft 版本的完整 java.exe 的路径。
echo 你可以在 PCL 的 [设置 → 启动选项] 中查看已安装的 java，所需的 java.exe 一般在其中的 bin 文件夹下。
echo ------------------------------
echo 如果提示 ""You need to agree to the EULA in order to run the server""，请打开 eula.txt，按说明阅读并同意 Minecraft EULA 后，将该文件最后一行中的 eula=false 改为 eula=true。
echo ------------------------------
""java"" -server -XX:+UseG1GC -Xmx6144M -Xms1024M -jar {Id}-server.jar nogui
echo ----------------------
echo 服务端已停止。
pause"
                FileUtils.Write(VersionFolder & "Launch Server.bat", Bat,
                        encoding:=If(Encoding.Default.Equals(Encoding.UTF8), Encoding.UTF8, Encoding.GetEncoding("GB18030")))
                '删除版本 JSON
                FileUtils.Delete(VersionFolder & Id & ".json")
            End Sub) With {.ProgressWeight = 0.5, .Show = False})
            '下载服务端文件
            Loaders.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskDownloadServerDownloading"), New List(Of NetFile)) With {.ProgressWeight = 5})

            '启动
            Dim Loader As New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadServer", Id), Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(Id)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Logger.Error(ex, "开始 Minecraft 服务端下载失败")
        End Try
    End Sub
    Public Sub McDownloadMenuSave(sender As Object, e As RoutedEventArgs)
        Dim Version As MyListItem
        If TypeOf sender Is MyListItem Then
            Version = sender
        ElseIf TypeOf sender.Parent Is MyListItem Then
            Version = sender.Parent
        Else
            Version = sender.Parent.Parent
        End If
        Try
            Dim Id = Version.Title
            Dim JsonUrl = Version.Tag("url").ToString
            Dim VersionFolder As String = Dialogs.SelectFolder("选择目标文件夹", False).FirstOrDefault
            If VersionFolder Is Nothing Then Return
            VersionFolder = VersionFolder & Id & "\"

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> GetLang("LangModDownloadLibTaskMcDownload") Then Continue For
                Hint(GetLang("LangModDownloadLibHintInstanceDownloading"), HintType.Red)
                Return
            Next

            Dim Loaders As New List(Of LoaderBase)
            '下载版本 JSON 文件
            Loaders.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskMcDownloadJson"), New List(Of NetFile) From {
                New NetFile(DlSourceLauncherOrMetaGet(JsonUrl), VersionFolder & Id & ".json", New FileChecker With {.IsJson = True})
            }) With {.ProgressWeight = 2})
            '获取支持库文件地址
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModDownloadLibTaskMcDownloadJarUrl"),
                Sub(Task) Task.Output = New List(Of NetFile) From {DlClientJarGet(New McInstance(VersionFolder), False)}
            ) With {.ProgressWeight = 0.5, .Show = False})
            '下载支持库文件
            Loaders.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskMcDownloadJar"), New List(Of NetFile)) With {.ProgressWeight = 5})

            '启动
            Dim Loader As New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskMcDownload", Id), Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(Id)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Logger.Error(ex, "开始 Minecraft 下载失败")
        End Try
    End Sub
    ''' <summary>
    ''' 显示某 Minecraft 版本的更新日志。
    ''' </summary>
    ''' <param name="InstanceJson">在 version_manifest.json 中的对应项。</param>
    Public Sub McUpdateLogShow(InstanceJson As JToken)
        Dim Id As String = InstanceJson("id").ToString.Lower
        Dim WikiName As String = Id
        '本地化
        Dim subLocation As String = ""
        Select Case Lang
            Case "zh-CN", "zh-HK", "zh-TW"
                subLocation = "zh."
                If Id = "3d shareware v1.34" Then
                    WikiName = "3D_Shareware_v1.34"
                ElseIf Id = "20w14∞" Then
                    WikiName = "20w14infinite"
                ElseIf Id = "2.0" Then
                    WikiName = "Java版2.0"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "Java版1.RV-Pre1"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    WikiName = "Java版1.14.3_-_Combat_Test"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    WikiName = "Java版Combat_Test_2"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    WikiName = "Java版Combat_Test_3"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    WikiName = "Java版Combat_Test_4"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    WikiName = "Java版Combat_Test_5"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    WikiName = "Java版Combat_Test_6"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    WikiName = "Java版Combat_Test_7c"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "Java版Combat_Test_8b"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "Java版Combat_Test_8c"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "Java版1.0.0-rc2"
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Java版Deep_Dark_Experimental_Snapshot_")
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "Java版Beta_1.9_Prerelease_6"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "Java版Beta_1.9_Prerelease"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = If(Id.Contains("w"), "", "Java版") & Id.Replace(" Pre-Release ", "-pre")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = "Java版" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "Beta_")
                ElseIf Id.StartsWithF("a") Then
                    WikiName = "Java版" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "Alpha_v")
                ElseIf Id = "inf-20100618" Then
                    WikiName = "Java版Infdev_20100618"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "Java版Classic_0.30（生存模式）"
                ElseIf Id.StartsWithF("c0.31") Then
                    WikiName = "Java版Indev_0.31_20100130"
                ElseIf Id.StartsWithF("c") Then
                    WikiName = "Java版" & Id.Replace("c", "Classic_")
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "Java版Pre-classic_" & Id
                Else
                    Logger.Error($"未知的版本格式：{Id}。")
                    Return
                End If
            Case "lzh"
                subLocation = "lzh."
                If Id = "3d shareware v1.34" Then
                    subLocation = "zh."
                    WikiName = "3D_Shareware_v1.34"
                ElseIf Id = "2.0" Then
                    WikiName = "爪哇版爪哇版二點〇"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "爪哇版爪哇版一點真視之預一"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    subLocation = "zh."
                    WikiName = "Java版1.14.3_-_Combat_Test"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    subLocation = "zh."
                    WikiName = "Java版Combat_Test_2"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    subLocation = "zh."
                    WikiName = "Java版Combat_Test_3"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    subLocation = "zh."
                    WikiName = "Java版Combat_Test_4"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    subLocation = "zh."
                    WikiName = "Java版Combat_Test_5"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    subLocation = "zh."
                    WikiName = "Java版Combat_Test_6"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    subLocation = "zh."
                    WikiName = "Java版Combat_Test_7c"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "爪哇版爪哇版鬭測八乙"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "爪哇版爪哇版鬭測八丙"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "爪哇版一點〇點〇之候二"
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    subLocation = "zh." 'lzh 无此
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Java版Deep_Dark_Experimental_Snapshot_")
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "爪哇版Beta_1.9_Prerelease_6"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "爪哇版Beta_1.9_Prerelease"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = If(Id.Contains("w"), "", "爪哇版") & Id.Replace(" Pre-Release ", "-pre")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = "爪哇版" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "復測版")
                ElseIf Id.StartsWithF("a") Then
                    WikiName = "爪哇版" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "首測版")
                ElseIf Id = "inf-20100618" Then
                    WikiName = "爪哇版方製無垠版二〇一〇〇六一八"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "爪哇版爪哇版古典版〇點三〇"
                ElseIf Id.StartsWithF("c0.31") Then
                    subLocation = "zh." 'lzh 无此
                    WikiName = "Java版Indev_0.31_20100130"
                    Exit Select
                ElseIf Id.StartsWithF("c") Then
                    WikiName = "爪哇版" & Id.Replace("c", "古典版")
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "爪哇版" & Id
                Else
                    Logger.Info("[Error] 未知的版本格式：" & Id & "。", LogBehavior.Toast)
                    Exit Sub
                End If
                Dim keyWord As New Dictionary(Of String, String) From {
                      {"Java版", "爪哇版"}, {"Combat_Test", "鬭測"}, {"Beta", "復測版"}, {"rc", "候"}, {"rd-", "先典版璐璧鐺"}, {"RC", "候"}, {"pre", "預"}, {"prerelease", "之預"}, {".", "點"}, {"-", "之"}, {"1", "一"}, {"2", "二"}, {"3", "三"}, {"4", "四"}, {"5", "五"}, {"6", "六"}, {"7", "七"}, {"8", "八"}, {"9", "九"}, {"0", "〇"}, {"w", "週"}, {"a", "甲"}, {"b", "乙"}, {"c", "丙"}, {"_", ""}, {" ", ""}}
                For Each key In keyWord.Keys
                    WikiName = WikiName.Replace(key, keyWord(key))
                Next
            Case "en-US", "en-GB"
                subLocation = ""
                If Id = "3d shareware v1.34" Then
                    WikiName = "Java_Edition_3D_Shareware_v1.34"
                ElseIf Id = "2.0" Then
                    WikiName = "Java_Edition_2.0"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "Java_Edition_1.RV-Pre1"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    WikiName = "Java_Edition_1.14.3_-_Combat_Test"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    WikiName = "Java_Edition_Combat_Test_2"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    WikiName = "Java_Edition_Combat_Test_3"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    WikiName = "Java_Edition_Combat_Test_4"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    WikiName = "Java_Edition_Combat_Test_5"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    WikiName = "Java_Edition_Combat_Test_6"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    WikiName = "Java_Edition_Combat_Test_7c"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "Java_Edition_Combat_Test_8b"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "Java_Edition_Combat_Test_8c"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "Java_Edition_RC2"
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Java_Edition_Deep_Dark_Experimental_Snapshot_")
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "Java_Edition_Beta_1.9_Prerelease_6"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "Java_Edition_Beta_1.9_Prerelease"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = If(Id.Contains("w"), "", "Java_Edition_") & Id.Replace(" Pre-Release ", "-pre")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = "Java_Edition_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "Beta_")
                ElseIf Id.StartsWithF("a") Then
                    WikiName = "Java_Edition_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "Alpha_v")
                ElseIf Id = "inf-20100618" Then
                    WikiName = "Java_Edition_Infdev_20100618"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "Java_Edition_Classic_0.30"
                ElseIf Id.StartsWithF("c0.31") Then
                    WikiName = "Java_Edition_Indev_0.31_20100130"
                ElseIf Id.StartsWithF("c") Then
                    WikiName = "Java_Edition_" & Id.Replace("c", "Classic_")
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "Java_Edition_Pre-classic_" & Id
                Else
                    Logger.Info("[Error] 未知的版本格式：" & Id & "。", LogBehavior.Toast)
                    Exit Sub
                End If
                Dim keyWord As New Dictionary(Of String, String) From {
                      {"Java版", "Java Edition "}, {"-rc", "_Release_Candidate"}, {"-RC", "_Release_Candidate"}, {"-pre", "_Pre-release"}, {"pre-release", "Pre-release"}, {" ", "_"}}
                For Each key In keyWord.Keys
                    WikiName = WikiName.Replace(key, keyWord(key))
                Next
                WikiName = FormatWikiNameEndDigit(WikiName)
            Case "ko-KR"
                subLocation = "ko."
                If Id = "3d shareware v1.34" Then
                    WikiName = "Java_Edition_3D_Shareware_v1.34"
                ElseIf Id = "2.0" Then
                    WikiName = "Java_Edition_2.0"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "Java_Edition_1.RV-Pre1"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    WikiName = "Java_Edition_1.14.3_-_Combat_Test"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    WikiName = "Java_Edition_Combat_Test_2"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    WikiName = "Java_Edition_Combat_Test_3"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    WikiName = "Java_Edition_Combat_Test_4"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    WikiName = "Java_Edition_Combat_Test_5"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    WikiName = "Java_Edition_Combat_Test_6"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    WikiName = "Java_Edition_Combat_Test_7c"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "Java_Edition_Combat_Test_8b"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "Java_Edition_Combat_Test_8c"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "Java_Edition_RC2"
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Java_Edition_Deep_Dark_Experimental_Snapshot_")
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "Java_Edition_Beta_1.9_Prerelease_6"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "Java_Edition_Beta_1.9_Prerelease"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = If(Id.Contains("w"), "", "Java_Edition_") & Id.Replace(" Pre-Release ", "-pre")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = "Java_Edition_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "Beta_")
                ElseIf Id.StartsWithF("a") Then
                    WikiName = "Java_Edition_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "Alpha_v")
                ElseIf Id = "inf-20100618" Then
                    WikiName = "Java_Edition_Infdev_20100618"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "Java_Edition_Classic_0.30"
                ElseIf Id.StartsWithF("c0.31") Then
                    WikiName = "Java_Edition_Indev_0.31_20100130"
                ElseIf Id.StartsWithF("c") Then
                    WikiName = "Java_Edition_" & Id.Replace("c", "Classic_")
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "Java_Edition_Pre-classic_" & Id
                Else
                    Logger.Info("[Error] 未知的版本格式：" & Id & "。", LogBehavior.Toast)
                    Exit Sub
                End If
                Dim keyWord As New Dictionary(Of String, String) From {
                      {"Java版", "Java Edition "}, {"-rc", "_RC#"}, {"-RC", "_RC#"}, {"pre", "프리릴리스"}, {"pre-release", "프리릴리스"}, {" ", "_"}}
                For Each key In keyWord.Keys
                    WikiName = WikiName.Replace(key, keyWord(key))
                Next
                WikiName = FormatWikiNameEndDigit(WikiName)
            Case "ja-JP"
                subLocation = "ja."
                If Id = "3d shareware v1.34" Then
                    WikiName = "Java_Edition_3D_Shareware_v1.34"
                ElseIf Id = "2.0" Then
                    WikiName = "Java_Edition_2.0"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "Java_Edition_1.RV-Pre1"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    WikiName = "Java_Edition_1.14.3_-_Combat_Test"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    WikiName = "Java_Edition_Combat_Test_2"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    WikiName = "Java_Edition_Combat_Test_3"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    WikiName = "Java_Edition_Combat_Test_4"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    WikiName = "Java_Edition_Combat_Test_5"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    WikiName = "Java_Edition_Combat_Test_6"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    WikiName = "Java_Edition_Combat_Test_7c"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "Java_Edition_Combat_Test_8b"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "Java_Edition_Combat_Test_8c"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "Java_Edition_RC2"
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Java_Edition_Deep_Dark_Experimental_Snapshot_")
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "Java_Edition_Beta_1.9_Prerelease_6"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "Java_Edition_Beta_1.9_Prerelease"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = If(Id.Contains("w"), "", "Java_Edition_") & Id.Replace(" Pre-Release ", "-pre")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = "Java_Edition_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "Beta_")
                ElseIf Id.StartsWithF("a") Then
                    WikiName = "Java_Edition_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "Alpha_v")
                ElseIf Id = "inf-20100618" Then
                    WikiName = "Java_Edition_Infdev_20100618"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "Java_Edition_Classic_0.30"
                ElseIf Id.StartsWithF("c0.31") Then
                    WikiName = "Java_Edition_Indev_0.31_20100130"
                ElseIf Id.StartsWithF("c") Then
                    WikiName = "Java_Edition_" & Id.Replace("c", "Classic_")
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "Java_Edition_Pre-classic_" & Id
                Else
                    Logger.Info("[Error] 未知的版本格式：" & Id & "。", LogBehavior.Toast)
                    Exit Sub
                End If
                Dim keyWord As New Dictionary(Of String, String) From {
                      {"-rc", "_Release_Candidate"}, {"-RC", "_Release_Candidate"}, {"pre", "Pre-release"}, {"pre-release", "Pre-release"}, {" ", "_"}}
                For Each key In keyWord.Keys
                    WikiName = WikiName.Replace(key, keyWord(key))
                Next
                WikiName = FormatWikiNameEndDigit(WikiName)
            Case "ru-RU"
                subLocation = "ru."
                If Id = "3d shareware v1.34" Then
                    WikiName = "3D_Shareware_v1.34_(Java_Edition)"
                ElseIf Id = "22w13oneblockatatime" Then
                    WikiName = "22w13oneBlockAtATime_(Java_Edition)"
                ElseIf Id = "20w14∞" Then
                    WikiName = "20w14infinite_(Java_Edition)"
                ElseIf Id = "2.0" Then
                    WikiName = "2.0_(Java_Edition)"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "1.RV-Pre1_(Java_Edition)"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    WikiName = "1.14.3_-_Combat_Test_(Java_Edition)"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    WikiName = "Combat_Test_2_(Java_Edition)"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    WikiName = "Combat_Test_3_(Java_Edition)"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    WikiName = "Combat_Test_4_(Java_Edition)"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    WikiName = "Combat_Test_5_(Java_Edition)"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    WikiName = "Combat_Test_6_(Java_Edition)"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    WikiName = "Combat_Test_7c_(Java_Edition)"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "Combat_Test_8b_(Java_Edition)"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "Combat_Test_8c_(Java_Edition)"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "RC2_(Java_Edition)"
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Deep_Dark_Experimental_Snapshot_") & "_(Java_Edition)"
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "Beta_1.9_Prerelease_6_(Java_Edition)"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "Beta_1.9_Prerelease_(Java_Edition)"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = Id.Replace("-pre", "_Pre-release")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "Beta_") & "_(Java_Edition)"
                ElseIf Id.StartsWithF("a") Then
                    WikiName = Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "Alpha_v") & "_(Java_Edition)"
                ElseIf Id = "inf-20100618" Then
                    WikiName = "Infdev_20100618_(Java_Edition)"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "Classic_0.30_(Java_Edition)"
                ElseIf Id.StartsWithF("c0.31") Then
                    WikiName = "Indev_0.31_20100130_(Java_Edition)"
                ElseIf Id.StartsWithF("c") Then
                    WikiName = Id.Replace("c", "Classic_") & "_(Java_Edition)"
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "Pre-classic_" & Id & "_(Java_Edition)"
                Else
                    Logger.Info("[Error] 未知的版本格式：" & Id & "。", LogBehavior.Toast)
                    Exit Sub
                End If
                Dim keyWord As New Dictionary(Of String, String) From {
                    {"-rc", "_Release_Candidate"}, {"-RC", "_Release_Candidate"}, {"-pre", "_Pre-release"}, {"pre-release", "Pre-release"}, {" ", "_"}}
                For Each key In keyWord.Keys
                    WikiName = WikiName.Replace(key, keyWord(key))
                Next
                WikiName = FormatWikiNameEndDigit(WikiName)
                If Not WikiName.EndsWithF("_(Java_Edition)") Then WikiName += "_(Java_Edition)"
            Case "fr-FR"
                subLocation = "fr."
                If Id = "3d shareware v1.34" Then
                    WikiName = "Édition_Java_3D_Shareware_v1.34"
                ElseIf Id = "2.0" Then
                    WikiName = "Édition_Java_2.0"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "Édition_Java_1.RV-Pre1"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    WikiName = "Édition_Java_1.14.3_-_Combat_Test"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    WikiName = "Édition_Java_Combat_Test_2"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    WikiName = "Édition_Java_Combat_Test_3"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    WikiName = "Édition_Java_Combat_Test_4"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    WikiName = "Édition_Java_Combat_Test_5"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    WikiName = "Édition_Java_Combat_Test_6"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    WikiName = "Édition_Java_Combat_Test_7c"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "Édition_Java_Combat_Test_8b"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "Édition_Java_Combat_Test_8c"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "Édition_Java_RC2"
                    Exit Select
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Édition_Java_Deep_Dark_Experimental_Snapshot_")
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "Édition_Java_Beta_1.9_Prerelease_6"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "Édition_Java_Beta_1.9_Prerelease"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = If(Id.Contains("w"), "", "Édition_Java_") & Id.Replace(" Pre-Release ", "-pre")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = "Édition_Java_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "Beta_")
                ElseIf Id.StartsWithF("a") Then
                    WikiName = "Édition_Java_" & Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "Alpha_v")
                ElseIf Id = "inf-20100618" Then
                    WikiName = "Édition_Java_Infdev_20100618"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "Édition_Java_Classic_0.30"
                ElseIf Id.StartsWithF("c0.31") Then
                    WikiName = "Édition_Java_Indev_0.31_20100130"
                ElseIf Id.StartsWithF("c") Then
                    WikiName = "Édition_Java_" & Id.Replace("c", "Classic_")
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "Édition_Java_Pre-classic_" & Id
                Else
                    Logger.Info("[Error] 未知的版本格式：" & Id & "。", LogBehavior.Toast)
                    Exit Sub
                End If
                Dim keyWord As New Dictionary(Of String, String) From {
                       {"-rc", "_Release_Candidate"}, {"-RC", "_Release_Candidate"}, {"-pre", "_Pre-Release"}, {"pre-release", "Pre-Release"}, {" ", "_"}}
                For Each key In keyWord.Keys
                    WikiName = WikiName.Replace(key, keyWord(key))
                Next
                WikiName = FormatWikiNameEndDigit(WikiName)
            Case "es-ES"
                subLocation = "es."
                If Id = "3d shareware v1.34" Then
                    WikiName = "3D_Shareware_v1.34_(Java_Edition)"
                ElseIf Id = "22w13oneblockatatime" Then
                    WikiName = "22w13oneBlockAtATime_(Java_Edition)"
                ElseIf Id = "20w14∞" Then
                    WikiName = "20w14infinite_(Java_Edition)"
                ElseIf Id = "2.0" Then
                    WikiName = "2.0_(Java_Edition)"
                ElseIf Id = "1.rv-pre1" Then
                    WikiName = "1.RV-Pre1_(Java_Edition)"
                ElseIf Id = "combat test 1" OrElse Id.Contains("combat-1") OrElse Id.Contains("combat-212796") Then
                    WikiName = "1.14.3_-_Combat_Test_(Java_Edition)"
                ElseIf Id = "combat test 2" OrElse Id.Contains("combat-2") OrElse Id.Contains("combat-0") Then
                    WikiName = "Combat_Test_2_(Java_Edition)"
                ElseIf Id = "combat test 3" OrElse Id = "1.14_combat-3" Then
                    WikiName = "Combat_Test_3_(Java_Edition)"
                ElseIf Id = "combat test 4" OrElse Id = "1.15_combat-1" Then
                    WikiName = "Combat_Test_4_(Java_Edition)"
                ElseIf Id = "combat test 5" OrElse Id = "1.15_combat-6" Then
                    WikiName = "Combat_Test_5_(Java_Edition)"
                ElseIf Id = "combat test 6" OrElse Id = "1.16_combat-0" Then
                    WikiName = "Combat_Test_6_(Java_Edition)"
                ElseIf Id = "combat test 7c" OrElse Id = "1.16_combat-3" Then
                    WikiName = "Combat_Test_7c_(Java_Edition)"
                ElseIf Id = "combat test 8b" OrElse Id = "1.16_combat-5" Then
                    WikiName = "Combat_Test_8b_(Java_Edition)"
                ElseIf Id = "combat test 8c" OrElse Id = "1.16_combat-6" Then
                    WikiName = "Combat_Test_8c_(Java_Edition)"
                ElseIf Id = "1.0.0-rc2-2" Then
                    WikiName = "RC2_(Java_Edition)"
                ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
                    WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Deep_Dark_Experimental_Snapshot_") & "_(Java_Edition)"
                ElseIf Id = "b1.9-pre6" Then
                    WikiName = "Beta_1.9_Prerelease_6_(Java_Edition)"
                ElseIf Id.Contains("b1.9") Then
                    WikiName = "Beta_1.9_Prerelease_(Java_Edition)"
                ElseIf InstanceJson("type") = "release" OrElse InstanceJson("type") = "snapshot" OrElse InstanceJson("type") = "special" Then
                    WikiName = Id.Replace("-pre", "_Pre-release")
                ElseIf Id.StartsWithF("b") Then
                    WikiName = Id.TrimEnd("a", "b", "c", "d", "e").Replace("b", "Beta_") & "_(Java_Edition)"
                ElseIf Id.StartsWithF("a") Then
                    WikiName = Id.TrimEnd("a", "b", "c", "d", "e").Replace("a", "Alpha_v") & "_(Java_Edition)"
                ElseIf Id = "inf-20100618" Then
                    WikiName = "Infdev_20100618_(Java_Edition)"
                ElseIf Id = "c0.30_01c" OrElse Id = "c0.30_survival" OrElse Id.Contains("生存测试") Then
                    WikiName = "Classic_0.30_(Java_Edition)"
                ElseIf Id.StartsWithF("c0.31") Then
                    WikiName = "Indev_0.31_20100130_(Java_Edition)"
                ElseIf Id.StartsWithF("c") Then
                    WikiName = Id.Replace("c", "Classic_") & "_(Java_Edition)"
                ElseIf Id.StartsWithF("rd-") Then
                    WikiName = "Pre-classic_" & Id & "_(Java_Edition)"
                Else
                    Logger.Info("[Error] 未知的版本格式：" & Id & "。", LogBehavior.Toast)
                    Exit Sub
                End If
                Dim keyWord As New Dictionary(Of String, String) From {
                      {"Java版", "Java_Edition_"}, {"-rc", "_Release_Candidate"}, {"-RC", "_Release_Candidate"}, {"-pre", "_Pre-release"}, {"pre-release", "Pre-Release"}, {" ", "_"}}
                For Each key In keyWord.Keys
                    WikiName = WikiName.Replace(key, keyWord(key))
                Next
                WikiName = FormatWikiNameEndDigit(WikiName)
        End Select
        OpenWebsite($"https://{subLocation}minecraft.wiki/w/{WikiName.Replace("_experimental-snapshot-", "-exp")}")
    End Sub

    Private Function FormatWikiNameEndDigit(WikiName As String) As String
        If Char.IsDigit(WikiName(WikiName.Length - 1)) And Not {".", "_", " "}.Any(Function(i) i.Equals(WikiName(WikiName.Length - 2))) And Not Char.IsDigit(WikiName(WikiName.Length - 2)) Then
            Return WikiName.Insert(WikiName.Length - 1, "_")
        End If
        Return WikiName
    End Function

#End Region

#Region "OptiFine 下载"

    Private Sub McDownloadOptiFineSave(DownloadInfo As DlOptiFineListEntry)
        Try
            Dim Id As String = DownloadInfo.InstanceName
            Dim Target As String = Dialogs.SaveFile(GetLang("LangSaveAs"), DownloadInfo.FileName, filter:={("jar", "OptiFine Jar")})
            If Target Is Nothing Then Return

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> GetLang("LangModDownloadLibTaskOptiFineDownload", DownloadInfo.DisplayName) Then Continue For
                Hint(GetLang("LangModDownloadLibHintInstanceDownloading"), HintType.Red)
                Return
            Next

            Dim Loader As New LoaderCombo(Of DlOptiFineListEntry)(GetLang("LangModDownloadLibTaskOptiFineDownload", DownloadInfo.DisplayName), McDownloadOptiFineSaveLoader(DownloadInfo, Target)) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(DownloadInfo)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Logger.Error(ex, "开始 OptiFine 下载失败")
        End Try
    End Sub
    Private Sub McDownloadOptiFineInstall(BaseMcFolderHome As String, Target As String, Task As LoaderTask(Of List(Of NetFile), Boolean), UseJavaWrapper As Boolean)
        Dim JavaRange = ValueRange(Of Version).AtLeast(New Version(21, 0)) '默认值
        Try
            Using Installer = FileUtils.OpenZip(Target)
                Dim InstallerClass = Installer.GetEntry("optifine/Installer.class")
                If InstallerClass Is Nothing Then Throw New FileNotFoundException("未找到 optifine/Installer.class")
                Using Stream = InstallerClass.Open(), Reader As New BinaryReader(Stream)
                    Dim Header = Reader.ReadBytes(8)
                    If Header.Length < 8 Then Throw New IOException("Installer.class 长度不足")
                    If Header(0) <> &HCA OrElse Header(1) <> &HFE OrElse Header(2) <> &HBA OrElse Header(3) <> &HBE Then Throw New InvalidDataException("Installer.class 文件头无效")
                    Dim ClassVersion = Header(6) * &H100 + Header(7)
                    If ClassVersion < 49 Then Throw New InvalidDataException("Installer.class 版本过低")
                    If ClassVersion > 100 Then Throw New InvalidDataException("Installer.class 版本过高")
                    JavaRange = ValueRange(Of Version).AtLeast(New Version(ClassVersion - 44, 0))
                    Logger.Info($"OptiFine 安装器 class 版本：{ClassVersion}，需要 Java {JavaRange}")
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "读取 OptiFine 安装器 Java 版本失败")
        End Try
        '选择 Java
        Dim Java As Java = SelectOrDownloadJava(JavaRange, True, Task.CreateCancellationToken, Task.CreateSyncProgressProvider(0, 0.2))
        If Task.IsCanceled Then Return
        If Java Is Nothing Then Throw New OperationCanceledException
        '添加 Java Wrapper 作为主 Jar
        Dim Arguments As String
        If UseJavaWrapper AndAlso Not Settings.Get(Of Boolean)("LaunchAdvanceDisableJLW") Then
            Arguments = $"-Doolloo.jlw.tmpdir=""{PathPure.TrimEnd("\")}"" -Duser.home=""{BaseMcFolderHome.TrimEnd("\")}"" -cp ""{Target}"" -jar ""{ExtractPatch("JavaWrapper")}"" optifine.Installer"
        Else
            Arguments = $"-Duser.home=""{BaseMcFolderHome.TrimEnd("\")}"" -cp ""{Target}"" optifine.Installer"
        End If
        If Java.Version.Major >= 9 Then Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " & Arguments
        '开始启动
        SyncLock InstallSyncLock
            Dim Info = New ProcessStartInfo With {
                .FileName = Java.JavaExePath,
                .Arguments = Arguments,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardError = True,
                .RedirectStandardOutput = True,
                .WorkingDirectory = PathUtils.ToShortPath(BaseMcFolderHome)
            }
            If Info.EnvironmentVariables.ContainsKey("appdata") Then
                Info.EnvironmentVariables("appdata") = BaseMcFolderHome
            Else
                Info.EnvironmentVariables.Add("appdata", BaseMcFolderHome)
            End If
            Logger.Info($"开始安装 OptiFine：{Target}")
            Dim TotalLength As Integer = 0
            Dim process As New Process With {.StartInfo = Info}
            Dim LastResult As String = ""
            Using outputWaitHandle As New AutoResetEvent(False)
                Using errorWaitHandle As New AutoResetEvent(False)
                    AddHandler process.OutputDataReceived,
                    Function(sender, e)
                        Try
                            If e.Data Is Nothing Then
                                outputWaitHandle.[Set]()
                            Else
                                LastResult = e.Data
                                Logger.Trace(LastResult)
                                TotalLength += 1
                                Task.Progress += 0.7 / 7000
                            End If
                        Catch ex As ObjectDisposedException
                        Catch ex As Exception
                            Logger.Warn(ex, "读取 OptiFine 安装器信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Canceled AndAlso Not process.HasExited Then
                                Logger.Info("由于任务取消，已中止 OptiFine 安装")
                                process.Kill()
                            End If
                        Catch
                        End Try
                        Return Nothing
                    End Function
                    AddHandler process.ErrorDataReceived,
                    Function(sender, e)
                        Try
                            If e.Data Is Nothing Then
                                errorWaitHandle.[Set]()
                            Else
                                LastResult = e.Data
                                Logger.Trace(LastResult)
                                TotalLength += 1
                                Task.Progress += 0.7 / 7000
                            End If
                        Catch ex As ObjectDisposedException
                        Catch ex As Exception
                            Logger.Warn(ex, "读取 OptiFine 安装器错误信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Canceled AndAlso Not process.HasExited Then
                                Logger.Info("由于任务取消，已中止 OptiFine 安装")
                                process.Kill()
                            End If
                        Catch
                        End Try
                        Return Nothing
                    End Function
                    process.Start()
                    process.BeginOutputReadLine()
                    process.BeginErrorReadLine()
                    '等待
                    Do Until process.HasExited
                        Thread.Sleep(10)
                    Loop
                    '输出
                    outputWaitHandle.WaitOne(10000)
                    errorWaitHandle.WaitOne(10000)
                    process.Dispose()
                    If TotalLength < 1000 OrElse LastResult.Contains("at ") Then Throw New Exception(GetLang("LangModDownloadLibExceptionInstallerException", LastResult))
                End Using
            End Using
        End SyncLock
    End Sub

    ''' <summary>
    ''' 获取下载某个 OptiFine 版本的加载器列表。
    ''' 这不会补全所需的支持库文件，需要后续手动补全支持库。
    ''' </summary>
    Private Function McDownloadOptiFineLoader(DownloadInfo As DlOptiFineListEntry, McFolder As String, ClientDownloadLoader As LoaderCombo(Of String), ClientFolder As String) As List(Of LoaderBase)

        '参数初始化
        Dim IsCustomFolder As Boolean = McFolder <> McFolderSelected
        Dim Id As String = DownloadInfo.InstanceName
        Dim VersionFolder As String = McFolder & "versions\" & Id & "\"
        Dim IsNewerVersion As Boolean = DownloadInfo.Inherit.Contains("w") OrElse McVersion.VersionToDrop(DownloadInfo.Inherit) >= 140
        Dim Target As String = If(IsNewerVersion,
            $"{RequestTaskTempFolder()}OptiFine.jar",
            $"{McFolder}libraries\optifine\OptiFine\{DownloadInfo.FileName.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "")}\{DownloadInfo.FileName.Replace("OptiFine_", "OptiFine-").Replace("preview_", "")}")
        Dim Loaders As New List(Of LoaderBase)

        '获取下载地址
        Loaders.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangPageSpeedRightGetOptiFineDownloadAddress"),
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            Dim Sources As New List(Of String)
            'BMCLAPI 源
            Dim BmclapiInherit As String = DownloadInfo.Inherit
            If BmclapiInherit = "1.8" OrElse BmclapiInherit = "1.9" Then BmclapiInherit &= ".0" '#4281
            If DownloadInfo.IsPreview Then
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U_" & DownloadInfo.DisplayName.Replace(DownloadInfo.Inherit & " ", "").Replace(" ", "/"))
            Else
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U/" & DownloadInfo.DisplayName.Replace(DownloadInfo.Inherit & " ", ""))
            End If
            '官方源
            Dim PageData As String
            Try
                PageData = NetRequestByClientRetry("https://optifine.net/adloadx?f=" & DownloadInfo.FileName,
                    Encoding:=New UTF8Encoding(False), Accept:="text/html", SimulateBrowserHeaders:=True)
                Task.Progress = 0.8
                Sources.Add("https://optifine.net/" & PageData.RegexSearch("downloadx\?f=[^""']+").First)
                Logger.Info($"OptiFine {DownloadInfo.DisplayName} 官方下载地址：{Sources.Last}")
            Catch ex As Exception
                Logger.Warn(ex, $"获取 OptiFine {DownloadInfo.DisplayName} 官方下载地址失败")
            End Try
            '构造文件请求
            Task.Output = New List(Of NetFile) From {New NetFile(Sources.ToArray, Target, New FileChecker With {.MinSize = 300 * 1024})}
        End Sub) With {.ProgressWeight = 8})
        Loaders.Add(New LoaderDownload(GetLang("LangPageSpeedRightDownloadOptiFineMainFile"), New List(Of NetFile)) With {.ProgressWeight = 8})
        Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)(GetLang("LangModDownloadLibTaskOptiFineWaitForVanilla"),
        Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
            '等待原版文件下载完成
            Dim TargetLoaders As List(Of LoaderBase) =
               ClientDownloadLoader.GetLoaderList.Where(Function(l) l.Name = McDownloadClientLibName OrElse l.Name = McDownloadClientJsonName).
               Where(Function(l) l.State <> LoadState.Finished).ToList
            If TargetLoaders.Any Then Logger.Info("OptiFine 安装正在等待原版文件下载完成")
            Do While TargetLoaders.Any AndAlso Not Task.IsCanceled
                TargetLoaders = TargetLoaders.Where(Function(l) l.State <> LoadState.Finished).ToList
                Thread.Sleep(50)
            Loop
            If Task.IsCanceled Then Return
            '复制原版文件
            If Not IsCustomFolder Then Return
            SyncLock VanillaSyncLock
                Dim ClientName As String = PathUtils.GetLastPart(ClientFolder)
                DirectoryUtils.Create(McFolder & "versions\" & DownloadInfo.Inherit)
                If Not FileUtils.Exists(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".json") Then
                    FileUtils.Copy($"{ClientFolder}{ClientName}.json", $"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.json")
                End If
                If Not FileUtils.Exists(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar") Then
                    FileUtils.Copy($"{ClientFolder}{ClientName}.jar", $"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.jar")
                End If
            End SyncLock
        End Sub) With {.ProgressWeight = 0.1, .Show = False})

        '安装（新旧方式均需要原版 Jar 和 Json）
        If IsNewerVersion Then
            Logger.Info($"检测为新版 OptiFine：{DownloadInfo.Inherit}")
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)(GetLang("LangPageSpeedRightInstallOptiFineMethodA"),
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
                Dim BaseMcFolderHome As String = RequestTaskTempFolder()
                Dim BaseMcFolder As String = BaseMcFolderHome & ".minecraft\"
                Try
                    '准备安装环境
                    If DirectoryUtils.Exists(BaseMcFolder & "versions\" & DownloadInfo.Inherit) Then
                        DirectoryUtils.Delete(BaseMcFolder & "versions\" & DownloadInfo.Inherit)
                    End If
                    DirectoryUtils.Create(BaseMcFolder & "versions\" & DownloadInfo.Inherit & "\")
                    McFolderLauncherProfilesJsonCreate(BaseMcFolder)
                    FileUtils.Copy(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".json",
                              BaseMcFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".json")
                    FileUtils.Copy(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar",
                              BaseMcFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar")
                    Task.Progress = 0.06
                    '进行安装
                    Dim UseJavaWrapper As Boolean = True
Retry:
                    Try
                        McDownloadOptiFineInstall(BaseMcFolderHome, Target, Task, UseJavaWrapper)
                    Catch ex As Exception
                        If UseJavaWrapper Then
                            Logger.Warn(ex, "使用 JavaWrapper 安装 OptiFine 失败，将不使用 JavaWrapper 并重试")
                            UseJavaWrapper = False
                            GoTo Retry
                        Else
                            Throw New Exception(GetLang("LangModDownloadLibExceptionOptiFineInstallerRunFail"), ex)
                        End If
                    End Try
                    Task.Progress = 0.96
                    '复制文件
                    FileUtils.Delete(BaseMcFolder & "launcher_profiles.json")
                    DirectoryUtils.Copy(BaseMcFolder, McFolder)
                    Task.Progress = 0.98
                    '清理文件
                    FileUtils.Delete(Target)
                    DirectoryUtils.Delete(BaseMcFolderHome)
                Catch ex As Exception
                    Throw New Exception(GetLang("LangModDownloadLibExceptionInstallOptiFineMethodAFail"), ex)
                End Try
            End Sub) With {.ProgressWeight = 8})
        Else
            Logger.Info($"检测为旧版 OptiFine：{DownloadInfo.Inherit}")
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)(GetLang("LangModDownloadLibTaskInstallOptiFineMethodB"),
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
                Try
                    '复制 Jar 文件
                    FileUtils.Copy(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar", VersionFolder & Id & ".jar")
                    Task.Progress = 0.7
                    '建立 Json 文件
                    Dim InheritInstance As New McInstance(McFolder & "versions\" & DownloadInfo.Inherit)
                    Dim Json As String = "{
    ""id"": """ & Id & """,
    ""inheritsFrom"": """ & DownloadInfo.Inherit & """,
    ""time"": """ & If(DownloadInfo.ReleaseTime = "", InheritInstance.ReleaseTime.ToString("yyyy'-'MM'-'dd"), DownloadInfo.ReleaseTime.Replace("/", "-")) & "T23:33:33+08:00"",
    ""releaseTime"": """ & If(DownloadInfo.ReleaseTime = "", InheritInstance.ReleaseTime.ToString("yyyy'-'MM'-'dd"), DownloadInfo.ReleaseTime.Replace("/", "-")) & "T23:33:33+08:00"",
    ""type"": ""release"",
    ""libraries"": [
        {""name"": ""optifine:OptiFine:" & DownloadInfo.FileName.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "") & """},
        {""name"": ""net.minecraft:launchwrapper:1.12""}
    ],
    ""mainClass"": ""net.minecraft.launchwrapper.Launch"","
                    Task.Progress = 0.8
                    If InheritInstance.IsOldJson Then
                        '输出旧版 Json 格式
                        Json += "
    ""minimumLauncherVersion"": 18,
    ""minecraftArguments"": """ & InheritInstance.JsonObject("minecraftArguments").ToString & "  --tweakClass optifine.OptiFineTweaker""
}"
                    Else
                        '输出新版 Json 格式
                        Json += "
    ""minimumLauncherVersion"": ""21"",
    ""arguments"": {
        ""game"": [
            ""--tweakClass"",
            ""optifine.OptiFineTweaker""
        ]
    }
}"
                    End If
                    FileUtils.Write(VersionFolder & Id & ".json", Json)
                Catch ex As Exception
                    Throw New Exception(GetLang("LangModDownloadLibExceptionInstallOptiFineMethodBFail"), ex)
                End Try
            End Sub) With {.ProgressWeight = 1})
        End If

        Return Loaders
    End Function
    ''' <summary>
    ''' 获取保存某个 OptiFine 版本的加载器列表。
    ''' </summary>
    Private Function McDownloadOptiFineSaveLoader(DownloadInfo As DlOptiFineListEntry, TargetFolder As String) As List(Of LoaderBase)
        Dim Loaders As New List(Of LoaderBase)
        '获取下载地址
        Loaders.Add(New LoaderTask(Of DlOptiFineListEntry, List(Of NetFile))(GetLang("LangModDownloadLibTaskGetOptiFineDownloadUrl"),
        Sub(Task As LoaderTask(Of DlOptiFineListEntry, List(Of NetFile)))
            Dim Sources As New List(Of String)
            'BMCLAPI 源
            Dim BmclapiInherit As String = DownloadInfo.Inherit
            If BmclapiInherit = "1.8" OrElse BmclapiInherit = "1.9" Then BmclapiInherit &= ".0" '#4281
            If DownloadInfo.IsPreview Then
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U_" & DownloadInfo.DisplayName.Replace(DownloadInfo.Inherit & " ", "").Replace(" ", "/"))
            Else
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U/" & DownloadInfo.DisplayName.Replace(DownloadInfo.Inherit & " ", ""))
            End If
            '官方源
            Dim PageData As String
            Try
                PageData = NetRequestByClientRetry("https://optifine.net/adloadx?f=" & DownloadInfo.FileName,
                    Encoding:=New UTF8Encoding(False), Accept:="text/html", SimulateBrowserHeaders:=True)
                Task.Progress = 0.8
                Sources.Add("https://optifine.net/" & PageData.RegexSearch("downloadx\?f=[^""']+").First)
                Logger.Info($"OptiFine {DownloadInfo.DisplayName} 官方下载地址：{Sources.Last}")
            Catch ex As Exception
                Logger.Warn(ex, $"获取 OptiFine {DownloadInfo.DisplayName} 官方下载地址失败")
            End Try
            Task.Progress = 0.9
            '构造文件请求
            Task.Output = New List(Of NetFile) From {New NetFile(Sources.ToArray, TargetFolder, New FileChecker With {.MinSize = 64 * 1024})}
        End Sub) With {.ProgressWeight = 6})
        '下载
        Loaders.Add(New LoaderDownload(GetLang("LangPageSpeedRightDownloadOptiFineMainFile"), New List(Of NetFile)) With {.ProgressWeight = 10, .Block = True})
        Return Loaders
    End Function

#End Region

#Region "OptiFine 下载菜单"

    Public Function OptiFineDownloadListItem(Entry As DlOptiFineListEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            '建立控件
            Dim NewItem As New MyListItem With {
                .Title = Entry.DisplayName, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
                .Info = If(Entry.IsPreview, GetLang("LangDownloadPreviewOptiFine"), GetLang("LangDownloadStable")) &
                        If(Entry.ReleaseTime = "", "", GetLang("LangComma") & GetLang("LangDownloadReleaseOn", Entry.ReleaseTime)) &
                        If(Entry.RequiredForgeVersion Is Nothing, GetLang("LangComma") & GetLang("LangDownloadForgeIncompatible"), If(Entry.RequiredForgeVersion = "", "", GetLang("LangComma") & GetLang("LangDownloadRecommendForge", Entry.RequiredForgeVersion))),
                .Logo = PathImage & "Blocks/GrassPath.png"
            }
            AddHandler NewItem.Click, OnClick
            '建立菜单
            If IsSaveOnly Then
                NewItem.ContentHandler = AddressOf OptiFineSaveContMenuBuild
            Else
                NewItem.ContentHandler = AddressOf OptiFineContMenuBuild
            End If
            Return NewItem
        End Function) With {.Height = 42}
    End Function
    Private Sub OptiFineSaveContMenuBuild(sender As Object, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf OptiFineLog_Click
        sender.Buttons = {BtnInfo}
    End Sub
    Private Sub OptiFineContMenuBuild(sender As Object, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = GetLang("LangDownloadSaveAs")}
        AddHandler BtnSave.Click, AddressOf OptiFineSave_Click
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf OptiFineLog_Click
        sender.Buttons = {BtnSave, BtnInfo}
    End Sub
    Private Sub OptiFineLog_Click(sender As Object, e As RoutedEventArgs)
        Dim Version As DlOptiFineListEntry
        If sender.Tag IsNot Nothing Then
            Version = sender.Tag
        ElseIf sender.Parent.Tag IsNot Nothing Then
            Version = sender.Parent.Tag
        Else
            Version = sender.Parent.Parent.Tag
        End If
        OpenWebsite("https://optifine.net/changelog?f=" & Version.FileName)
    End Sub
    Public Sub OptiFineSave_Click(sender As Object, e As RoutedEventArgs)
        Dim Version As DlOptiFineListEntry
        If sender.Tag IsNot Nothing Then
            Version = sender.Tag
        ElseIf sender.Parent.Tag IsNot Nothing Then
            Version = sender.Parent.Tag
        Else
            Version = sender.Parent.Parent.Tag
        End If
        McDownloadOptiFineSave(Version)
    End Sub

#End Region

#Region "LiteLoader 下载"

    Private Sub McDownloadLiteLoaderSave(DownloadInfo As DlLiteLoaderListEntry)
        Try
            Dim Id As String = DownloadInfo.Inherit
            Dim Target As String = Dialogs.SaveFile(GetLang("LangSaveAs"), DownloadInfo.FileName.Replace("-SNAPSHOT", ""), filter:={("jar", "LiteLoader 安装器")})
            If Target Is Nothing Then Return

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> GetLang("LangModDownloadLibTaskLiteLoaderDownload", Id) Then Continue For
                Hint(GetLang("LangModDownloadLibHintInstanceDownloading"), HintType.Red)
                Return
            Next

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            '下载
            Dim Address As New List(Of String)
            If DownloadInfo.IsLegacy Then
                '老版本
                Select Case DownloadInfo.Inherit
                    Case "1.7.10"
                        Address.Add("https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar")
                    Case "1.7.2"
                        Address.Add("https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar")
                    Case "1.6.4"
                        Address.Add("https://dl.liteloader.com/redist/1.6.4/liteloader-installer-1.6.4-01.jar")
                    Case "1.6.2"
                        Address.Add("https://dl.liteloader.com/redist/1.6.2/liteloader-installer-1.6.2-04.jar")
                    Case "1.5.2"
                        Address.Add("https://dl.liteloader.com/redist/1.5.2/liteloader-installer-1.5.2-01.jar")
                    Case Else
                        Throw New NotSupportedException(GetLang("LangModDownloadLibExceptionNotSupportVersion", DownloadInfo.Inherit))
                End Select
            Else
                '官方源
                Address.Add("http://jenkins.liteloader.com/job/LiteLoaderInstaller%20" & DownloadInfo.Inherit & "/lastSuccessfulBuild/artifact/" & If(DownloadInfo.Inherit = "1.8", "ant/dist/", "build/libs/") & DownloadInfo.FileName)
            End If
            Loaders.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskDownloadMainFile"), New List(Of NetFile) From {New NetFile(Address.ToArray, Target, New FileChecker With {.MinSize = 1024 * 1024})}) With {.ProgressWeight = 15})
            '启动
            Dim Loader As New LoaderCombo(Of DlLiteLoaderListEntry)(GetLang("LangModDownloadLibTaskLiteLoaderInstallerDownload", Id), Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(DownloadInfo)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Logger.Error(ex, "开始 LiteLoader 安装器下载失败")
        End Try
    End Sub

    ''' <summary>
    ''' 获取下载某个 LiteLoader 版本的加载器列表。
    ''' 这不会补全所需的支持库文件，需要后续手动补全支持库。
    ''' </summary>
    Private Function McDownloadLiteLoaderLoader(DownloadInfo As DlLiteLoaderListEntry, McFolder As String) As List(Of LoaderBase)

        '参数初始化
        Dim IsCustomFolder As Boolean = McFolder <> McFolderSelected
        Dim Id As String = DownloadInfo.Inherit
        Dim Target As String = PathTemp & "Download\" & Id & "-Liteloader.jar"
        Dim VersionName As String = DownloadInfo.Inherit & "-LiteLoader"
        Dim VersionFolder As String = McFolder & "versions\" & VersionName & "\"
        Dim Loaders As New List(Of LoaderBase)

        '安装
        Loaders.Add(New LoaderTask(Of String, String)(GetLang("LangModDownloadLibTaskInstallLiteLoader"),
        Sub(Task As LoaderTask(Of String, String))
            Try
                '构造版本 Json
                Dim InstanceJson As New JObject
                InstanceJson.Add("id", VersionName)
                InstanceJson.Add("time", Date.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", Globalization.CultureInfo.InvariantCulture))
                InstanceJson.Add("releaseTime", Date.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", Globalization.CultureInfo.InvariantCulture))
                InstanceJson.Add("type", "release")
                InstanceJson.Add("arguments", ("{""game"":[""--tweakClass"",""" & DownloadInfo.JsonToken("tweakClass").ToString & """]}").DeserializeJson())
                InstanceJson.Add("libraries", DownloadInfo.JsonToken("libraries"))
                CType(InstanceJson("libraries"), JContainer).Add(("{""name"": ""com.mumfrey:liteloader:" & DownloadInfo.JsonToken("version").ToString & """,""url"": ""https://dl.liteloader.com/versions/""}").DeserializeJson())
                InstanceJson.Add("mainClass", "net.minecraft.launchwrapper.Launch")
                InstanceJson.Add("minimumLauncherVersion", 18)
                InstanceJson.Add("inheritsFrom", DownloadInfo.Inherit)
                InstanceJson.Add("jar", DownloadInfo.Inherit)
                '输出 Json 文件，同时会新建该文件夹
                FileUtils.Write(VersionFolder & VersionName & ".json", InstanceJson.ToString)
            Catch ex As Exception
                Throw New Exception(GetLang("LangModDownloadLibExceptionInstallLiteLoaderFail"), ex)
            End Try
        End Sub) With {.ProgressWeight = 1})

        Return Loaders
    End Function

#End Region

#Region "LiteLoader 下载菜单"

    Public Function LiteLoaderDownloadListItem(Entry As DlLiteLoaderListEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            '建立控件
            Dim NewItem As New MyListItem With {
                .Title = Entry.Inherit, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
                .Info = If(Entry.IsPreview, GetLang("LangDownloadPreviewLiteLoader"), GetLang("LangDownloadStable")) & If(Entry.ReleaseTime = "", "", GetLang("LangComma") & GetLang("LangDownloadReleaseOn", Entry.ReleaseTime)),
                .Logo = PathImage & "Blocks/Egg.png"
            }
            AddHandler NewItem.Click, OnClick
            '建立菜单
            If IsSaveOnly Then
                NewItem.ContentHandler = AddressOf LiteLoaderSaveContMenuBuild
            Else
                NewItem.ContentHandler = AddressOf LiteLoaderContMenuBuild
            End If
            '结束
            Return NewItem
        End Function) With {.Height = 42}
    End Function
    Private Sub LiteLoaderSaveContMenuBuild(sender As MyListItem, e As EventArgs)
        If sender.Tag.IsLegacy Then
            sender.Buttons = {}
        Else
            Dim BtnList As New MyIconButton With {.Logo = Logo.IconButtonList, .ToolTip = GetLang("LangDownloadShowAll"), .Tag = sender}
            AddHandler BtnList.Click, AddressOf LiteLoaderAll_Click
            sender.Buttons = {BtnList}
        End If
    End Sub
    Private Sub LiteLoaderContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = GetLang("LangDownloadSaveInstaller"), .Tag = sender}
        AddHandler BtnSave.Click, AddressOf LiteLoaderSave_Click
        If sender.Tag.IsLegacy Then
            sender.Buttons = {BtnSave}
        Else
            Dim BtnList As New MyIconButton With {.Logo = Logo.IconButtonList, .ToolTip = GetLang("LangDownloadShowAll"), .Tag = sender}
            AddHandler BtnList.Click, AddressOf LiteLoaderAll_Click
            sender.Buttons = {BtnSave, BtnList}
        End If
    End Sub
    Private Sub LiteLoaderAll_Click(sender As Object, e As RoutedEventArgs)
        Dim Version As DlLiteLoaderListEntry
        If TypeOf sender.Tag Is DlLiteLoaderListEntry Then
            Version = sender.Tag
        Else
            Version = sender.Tag.Tag
        End If
        OpenWebsite("https://jenkins.liteloader.com/view/" & Version.Inherit)
    End Sub
    Public Sub LiteLoaderSave_Click(sender As Object, e As RoutedEventArgs)
        'ListItem 与小按钮都会调用这个方法
        Dim Version As DlLiteLoaderListEntry
        If TypeOf sender.Tag Is DlLiteLoaderListEntry Then
            Version = sender.Tag
        Else
            Version = sender.Tag.Tag
        End If
        McDownloadLiteLoaderSave(Version)
    End Sub

#End Region

#Region "Forgelike 下载"

    Public Sub McDownloadForgelikeSave(Info As DlForgelikeEntry)
        Try
            Dim Target As String = Dialogs.SaveFile(GetLang("LangSaveAs"), $"{Info.LoaderName}-{Info.Inherit}-{Info.VersionName}.{Info.FileExtension}",
                                            filter:={(Info.FileExtension, $"{Info.LoaderName} 安装器")})
            If Target Is Nothing Then Return
            Dim DisplayName As String = $"{Info.LoaderName} {Info.Inherit} - {Info.VersionName}"

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> GetLang("LangModDownloadLibTaskForgelikeDownload", DisplayName) Then Continue For
                Hint(GetLang("LangModDownloadLibHintInstanceDownloading"), HintType.Red)
                Return
            Next

            '获取下载地址
            Dim Files As New List(Of NetFile)
            If Info.IsNeoForge Then
                'NeoForge
                Dim NeoForge As DlNeoForgeListEntry = Info
                Dim Url As String = NeoForge.UrlBase & "-installer.jar"
                Files.Add(New NetFile({
                    Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url
                }, Target, New FileChecker With {.MinSize = 64 * 1024}))
            Else
                'Forge
                Dim Forge As DlForgeVersionEntry = Info
                Files.Add(New NetFile({
                    $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}",
                    $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}"
                }, Target, New FileChecker With {.MinSize = 64 * 1024, .Hash = Forge.Hash}))
            End If

            '构造加载器
            Dim Loaders As New List(Of LoaderBase)
            Loaders.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskDownloadMainFile"), Files) With {.ProgressWeight = 6})

            '启动
            Dim Loader = New LoaderCombo(Of DlForgelikeEntry)(GetLang("LangModDownloadLibTaskForgelikeDownload", DisplayName), Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(Info)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Logger.Error(ex, $"开始 {Info.LoaderName} 安装器下载失败")
        End Try
    End Sub

    Private Sub ForgelikeInjector(Target As String, Task As LoaderTask(Of Boolean, Boolean), McFolder As String, UseJavaWrapper As Boolean, IsNeoForge As Boolean)
        '选择 Java
        Dim Java As Java = SelectOrDownloadJava(ValueRange(Of Version).AtLeast(New Version(8, 0, 60)), True, Task.CreateCancellationToken, Task.CreateSyncProgressProvider(0, 0.2))
        If Task.IsCanceled Then Return
        If Java Is Nothing Then Throw New OperationCanceledException
        '添加 Java Wrapper 作为主 Jar
        Dim Arguments As String
        If UseJavaWrapper AndAlso Not Settings.Get(Of Boolean)("LaunchAdvanceDisableJLW") Then
            Arguments = $"-Doolloo.jlw.tmpdir=""{PathPure.TrimEnd("\")}"" -cp ""{PathTemp}Cache\forge_installer.jar;{Target}"" -jar ""{ExtractPatch("JavaWrapper")}"" com.bangbang93.ForgeInstaller ""{McFolder}"
        Else
            Arguments = $"-cp ""{PathTemp}Cache\forge_installer.jar;{Target}"" com.bangbang93.ForgeInstaller ""{McFolder}"
        End If
        If Java.Version.Major >= 9 Then Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " & Arguments
        '开始启动
        SyncLock InstallSyncLock
            Dim Info = New ProcessStartInfo With {
                .FileName = Java.JavaExePath,
                .Arguments = Arguments,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardError = True,
                .RedirectStandardOutput = True
            }
            Dim LoaderName As String = If(IsNeoForge, "NeoForge", "Forge")
            Logger.Info($"开始安装 {LoaderName}：{Arguments}")
            Dim process As New Process With {.StartInfo = Info}
            Dim LastResults As New Queue(Of String)
            Using outputWaitHandle As New AutoResetEvent(False)
                Using errorWaitHandle As New AutoResetEvent(False)
                    AddHandler process.OutputDataReceived,
                    Function(sender, e)
                        Try
                            If e.Data Is Nothing Then
                                outputWaitHandle.[Set]()
                            Else
                                LastResults.Enqueue(e.Data)
                                If LastResults.Count > 100 Then LastResults.Dequeue()
                                ForgelikeInjectorLine(e.Data, Task)
                            End If
                        Catch ex As ObjectDisposedException
                        Catch ex As Exception
                            Logger.Warn(ex, $"读取 {LoaderName} 安装器信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Canceled AndAlso Not process.HasExited Then
                                Logger.Info($"由于任务取消，已中止 {LoaderName} 安装")
                                process.Kill()
                            End If
                        Catch
                        End Try
                        Return Nothing
                    End Function
                    AddHandler process.ErrorDataReceived,
                    Function(sender, e)
                        Try
                            If e.Data Is Nothing Then
                                errorWaitHandle.[Set]()
                            Else
                                LastResults.Enqueue(e.Data)
                                If LastResults.Count > 100 Then LastResults.Dequeue()
                                ForgelikeInjectorLine(e.Data, Task)
                            End If
                        Catch ex As ObjectDisposedException
                        Catch ex As Exception
                            Logger.Warn(ex, $"读取 {LoaderName} 安装器错误信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Canceled AndAlso Not process.HasExited Then
                                Logger.Info($"由于任务取消，已中止 {LoaderName} 安装")
                                process.Kill()
                            End If
                        Catch
                        End Try
                        Return Nothing
                    End Function
                    process.Start()
                    process.BeginOutputReadLine()
                    process.BeginErrorReadLine()
                    '等待
                    Do Until process.HasExited
                        Thread.Sleep(10)
                    Loop
                    '输出
                    outputWaitHandle.WaitOne(10000)
                    errorWaitHandle.WaitOne(10000)
                    process.Dispose()
                    '检查是否安装成功：最后 5 行中是否有 true（true 可能在倒数数行，见 #832）
                    If LastResults.Reverse().Take(5).Any(Function(l) l = "true") Then Return
                    Logger.Warn(LastResults.Join(vbCrLf))
                    Dim LastLines As String = ""
                    For i As Integer = Math.Max(0, LastResults.Count - 5) To LastResults.Count - 1 '最后 5 行
                        LastLines &= vbCrLf & LastResults(i)
                    Next
                    Throw New Exception($"{LoaderName} 安装器出错，日志结束部分为：" & LastLines)
                End Using
            End Using
        End SyncLock
    End Sub
    Private Sub ForgelikeInjectorLine(Content As String, Task As LoaderTask(Of Boolean, Boolean))
        Select Case Content
            Case "Extracting json"
                Task.Progress = 0.27
            Case "Downloading libraries"
                Task.Progress = 0.28
            Case "  File exists: Checksum validated."
                Task.Progress += 0.003
            Case "Building Processors"
                Task.Progress = 0.38
            Case "Task: DOWNLOAD_MOJMAPS" 'B
                Task.Progress = 0.4
            Case "Task: MERGE_MAPPING" 'B
                Task.Progress = 0.5
            Case "Splitting: "
                Task.Progress = 0.55
            Case "Parameter Annotations" 'B
                Task.Progress = 0.6
            Case "Processing Complete" 'B
                Task.Progress = 0.67
            Case "log: null" 'new
                Task.Progress = 0.67
            Case "Sorting" 'new
                Task.Progress = 0.8
            Case "Remapping final jar" 'A
                Task.Progress = 0.85
            Case "Remapping jar... 50%" 'A
                Task.Progress = 0.9
            Case "Remapping jar... 100%" 'A
                Task.Progress = 0.95
            Case "Injecting profile"
                Task.Progress = 0.98
            Case Else
                Logger.Trace(Content)
                Return
        End Select
        Logger.Info(Content)
    End Sub

    ''' <summary>
    ''' 获取下载某个 Forgelike 版本的加载器列表。
    ''' </summary>
    Private Function McDownloadForgelikeLoader(IsNeoForge As Boolean, LoaderVersion As String, NewInstanceName As String, Inherit As String, Info As DlForgelikeEntry, McFolder As String, ClientDownloadLoader As LoaderCombo(Of String), ClientFolder As String) As List(Of LoaderBase)

        '参数初始化
        McFolder = If(McFolder, McFolderSelected)
        If IsNeoForge AndAlso Info Is Nothing Then
            '需要传入 API Name，但整合包版本可能不以 1.20.1- 开头，所以需要进行特别处理
            If Inherit = "1.20.1" AndAlso Not LoaderVersion.StartsWithF("1.20.1-") Then
                Info = New DlNeoForgeListEntry("1.20.1-" & LoaderVersion)
            Else
                Info = New DlNeoForgeListEntry(LoaderVersion)
            End If
        End If
        If Not IsNeoForge AndAlso LoaderVersion.StartsWithF("1.") AndAlso LoaderVersion.Contains("-") Then
            '类似 1.19.3-41.2.8 格式，优先使用 Version 中要求的版本而非 Inherit（例如 1.19.3 却使用了 1.19 的 Forge）
            Inherit = LoaderVersion.BeforeFirst("-")
            LoaderVersion = LoaderVersion.AfterLast("-")
        End If
        Dim LoaderName As String = If(IsNeoForge, "NeoForge", "Forge")
        Dim IsCustomFolder As Boolean = McFolder <> McFolderSelected
        Dim InstallerAddress As String = RequestTaskTempFolder() & "forge_installer.jar"
        Dim VersionFolder As String = $"{McFolder}versions\{NewInstanceName}\"
        Dim DisplayName As String = $"{LoaderName} {Inherit} - {LoaderVersion}"
        Dim Loaders As New List(Of LoaderBase)
        Dim LibVersionFolder As String = $"{McFolderSelected}versions\{NewInstanceName}\" '作为 Lib 文件目标的版本文件夹

        '获取 Forge 下载信息
        If Info Is Nothing Then
            Loaders.Add(New LoaderTask(Of String, String)(GetLang("LangModDownloadLibTaskForgelikeGetDetailInfo", LoaderName),
            Sub(Task As LoaderTask(Of String, String))
                '获取 Forge 对应 MC 版本列表
                Dim ForgeLoader = New LoaderTask(Of String, List(Of DlForgeVersionEntry))("McDownloadForgeLoader " & Inherit, AddressOf DlForgeVersionMain)
                ForgeLoader.WaitForExit(Inherit)
                Task.Progress = 0.8
                '查找对应版本
                For Each ForgeVersion In ForgeLoader.Output
                    If CompareVersion(ForgeVersion.Version.ToString, LoaderVersion) = 0 Then
                        Info = ForgeVersion
                        Return
                    End If
                Next
                Throw New Exception(GetLang("LangModDownloadLibExceptionForgelikeGetDetailInfoFail", LoaderName & Inherit & "-" & LoaderVersion))
            End Sub) With {.ProgressWeight = 3})
        End If
        '下载 Forgelike 主文件
        Loaders.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModDownloadLibTaskForgelikeReadyDownload", LoaderName),
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            '添加主文件下载
            Dim Files As New List(Of NetFile)
            If Info.IsNeoForge Then
                'NeoForge
                Dim Neo As DlNeoForgeListEntry = Info
                Dim Url As String = Neo.UrlBase & "-installer.jar"
                Files.Add(New NetFile({
                    Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url
                }, InstallerAddress, New FileChecker With {.MinSize = 64 * 1024}))
            Else
                'Forge
                Dim Forge As DlForgeVersionEntry = Info
                Dim FileName As String =
                    $"{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}/forge-{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}"
                Files.Add(New NetFile({
                    $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{FileName}",
                    $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{FileName}"
                }, InstallerAddress, New FileChecker With {.MinSize = 64 * 1024, .Hash = Forge.Hash}))
            End If
            Task.Output = Files
        End Sub) With {.ProgressWeight = 0.5, .Show = False})
        Loaders.Add(New LoaderDownload(GetLang("LangPageSpeedRightDownloadModLoaderMainFile", LoaderName), New List(Of NetFile)) With {.ProgressWeight = 9})

        '安装（仅在新版安装时需要原版 Jar）
        If IsNeoForge OrElse LoaderVersion.BeforeFirst(".") >= 20 Then
            Logger.Info($"检测为{If(IsNeoForge, " Neo", "新版 ")}Forge：{LoaderVersion}")
            Dim Libs As List(Of McLibEntry) = Nothing
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangPageSpeedRightAnalyzeModLoaderSupportLibrary", LoaderName),
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                Task.Output = New List(Of NetFile)
                Dim Installer As ZipArchive = Nothing
                Try
                    '解压并获取、合并两个 Json 的信息
                    Installer = FileUtils.OpenZip(InstallerAddress)
                    Task.Progress = 0.2
                    Dim Json As JObject = Installer.GetEntry("install_profile.json").Open().ReadString().DeserializeJson()
                    Dim Json2 As JObject = Installer.GetEntry("version.json").Open().ReadString().DeserializeJson()
                    Json.Merge(Json2)
                    '获取 Lib 下载信息
                    Libs = McLibListGetWithJson(Json, True)
                    '添加 Mappings 下载信息
                    If Json("data") IsNot Nothing AndAlso Json("data")("MOJMAPS") IsNot Nothing Then
                        '下载原版 Json 文件
                        Task.Progress = 0.4
                        Dim RawJson As JObject = NetRequestByLoader(DlSourceLauncherOrMetaGet(DlClientListGet(Inherit)), IsJson:=True).DeserializeJson()
                        '[net.minecraft:client:1.17.1-20210706.113038:mappings@txt] 或 @tsrg]
                        Dim OriginalName As String = Json("data")("MOJMAPS")("client").ToString.Trim("[]".ToCharArray()).BeforeFirst("@")
                        Dim Address = McLibGet(OriginalName).Replace(".jar", "." & Json("data")("MOJMAPS")("client").ToString.Trim("[]".ToCharArray()).Split("@")(1))
                        Dim ClientMappings As JToken = RawJson("downloads")("client_mappings")
                        Libs.Add(New McLibEntry With {
                                 .IsNatives = False, .LocalPath = Address, .OriginalName = OriginalName,
                                 .Url = ClientMappings("url"), .Size = ClientMappings("size"), .SHA1 = ClientMappings("sha1")})
                        Logger.Info($"需要下载 Mappings：{ClientMappings("url")} (SHA1: {ClientMappings("sha1")})")
                    End If
                    Task.Progress = 0.8
                    '去除其中的原始 Forgelike 项
                    For i = 0 To Libs.Count - 1
                        If Libs(i).LocalPath.EndsWithF($"{LoaderName.Lower}-{Inherit}-{LoaderVersion}.jar") OrElse
                           Libs(i).LocalPath.EndsWithF($"{LoaderName.Lower}-{Inherit}-{LoaderVersion}-client.jar") Then
                            Logger.Warn($"已从待下载 {LoaderName} 支持库中移除：{Libs(i).LocalPath}")
                            Libs.RemoveAt(i)
                            Exit For
                        End If
                    Next
                    Task.Output = McLibNetFilesFromTokens(Libs, McFolderSelected)
                Catch ex As Exception
                    Throw New Exception($"获取{If(IsNeoForge, " Neo", "新版 ")}Forge 支持库列表失败", ex)
                Finally
                    If Installer IsNot Nothing Then Installer.Dispose()
                End Try
            End Sub) With {.ProgressWeight = 1})
            Loaders.Add(New LoaderDownload(GetLang("LangPageSpeedRightDownloadModLoaderSupportLibrary", LoaderName), New List(Of NetFile)) With {.ProgressWeight = 12})
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)(GetLang("LangModDownloadLibTaskForgelikeGetLibFile", LoaderName),
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
#Region "Forgelike 文件"
                If IsCustomFolder Then
                    For Each LibFile As McLibEntry In Libs
                        Dim RealPath As String = LibFile.LocalPath.Replace(McFolderSelected, McFolder)
                        If Not FileUtils.Exists(RealPath) Then FileUtils.Copy(LibFile.LocalPath, RealPath)
                        Logger.Trace($"复制的 {LoaderName} 支持库文件：{LibFile.LocalPath}")
                    Next
                End If
#End Region
#Region "原版文件"
                '等待原版文件下载完成
                Dim TargetLoaders As List(Of LoaderBase) =
                    ClientDownloadLoader.GetLoaderList.Where(Function(l) l.Name = McDownloadClientLibName OrElse l.Name = McDownloadClientJsonName).
                    Where(Function(l) l.State <> LoadState.Finished).ToList()
                If TargetLoaders.Any Then Logger.Info($"{LoaderName} 安装正在等待原版文件下载完成")
                Do While TargetLoaders.Any AndAlso Not Task.IsCanceled
                    TargetLoaders = TargetLoaders.Where(Function(l) l.State <> LoadState.Finished).ToList
                    Thread.Sleep(50)
                Loop
                If Task.IsCanceled Then Return
                '复制原版文件
                If Not IsCustomFolder Then Return
                SyncLock VanillaSyncLock
                    Dim ClientName As String = PathUtils.GetLastPart(ClientFolder)
                    DirectoryUtils.Create(McFolder & "versions\" & Inherit)
                    If Not FileUtils.Exists(McFolder & "versions\" & Inherit & "\" & Inherit & ".json") Then
                        FileUtils.Copy(ClientFolder & ClientName & ".json", McFolder & "versions\" & Inherit & "\" & Inherit & ".json")
                    End If
                    If Not FileUtils.Exists(McFolder & "versions\" & Inherit & "\" & Inherit & ".jar") Then
                        FileUtils.Copy(ClientFolder & ClientName & ".jar", McFolder & "versions\" & Inherit & "\" & Inherit & ".jar")
                    End If
                End SyncLock
#End Region
            End Sub) With {.ProgressWeight = 0.1, .Show = False})
            Loaders.Add(New LoaderTask(Of Boolean, Boolean)(If(IsNeoForge, GetLang("LangPageSpeedRightInstallNeoForge"), GetLang("LangPageSpeedRightInstallForgeMethodA")),
            Sub(Task As LoaderTask(Of Boolean, Boolean))
                Dim Installer As ZipArchive = Nothing
                Try
                    Logger.Info($"开始进行 Forgelike 安装：{InstallerAddress}")
                    '记录当前文件夹列表（在新建目标文件夹之前）
                    Dim OldList = DirectoryUtils.EnumerateDirectories(McFolder & "versions\").ToList()
                    '解压并获取信息
                    Installer = FileUtils.OpenZip(InstallerAddress)
                    Dim Json As JObject = Installer.GetEntry("install_profile.json").Open().ReadString().DeserializeJson()
                    '新建目标版本文件夹
                    DirectoryUtils.Create(VersionFolder)
                    Task.Progress = 0.04
                    '释放 launcher_installer.json
                    McFolderLauncherProfilesJsonCreate(McFolder)
                    Task.Progress = 0.05
                    '运行 Forge 安装器
                    Dim UseJavaWrapper As Boolean = True
Retry:
                    Try
                        '释放 Forge 注入器
                        ExtractResources(PathTemp & "Cache\forge_installer.jar", "ForgeInstaller")
                        Task.Progress = 0.06
                        '运行注入器
                        ForgelikeInjector(InstallerAddress, Task, McFolder, UseJavaWrapper, IsNeoForge)
                        Task.Progress = 0.97
                    Catch ex As Exception
                        If UseJavaWrapper Then
                            Logger.Warn(ex, $"使用 JavaWrapper 安装 {LoaderName} 失败，将不使用 JavaWrapper 并重试")
                            UseJavaWrapper = False
                            GoTo Retry
                        Else
                            Throw New Exception(GetLang("LangModDownloadLibExceptionForgelikeRunInstallerFail", LoaderName), ex)
                        End If
                    End Try
                    '复制新增的版本 Json
                    Dim DeltaList = DirectoryUtils.EnumerateDirectories(McFolder & "versions\").
                        SkipWhile(Function(i) OldList.Contains(i)).Select(Function(f) DirectoryUtils.GetInfo(f)).ToList()
                    If DeltaList.Count > 1 Then
                        '它可能和 OptiFine 安装同时运行，导致增加的文件不止一个（这导致了 #151）
                        '也可能是因为 Forge 安装器的 Bug，生成了一个名字错误的文件夹，所以需要检查文件夹是否为空
                        DeltaList = DeltaList.Where(Function(l) l.Name.ContainsIgnoreCase("forge") AndAlso l.EnumerateFiles.Any).ToList
                    End If
                    If DeltaList.Count = 1 Then
                        '如果没有新增文件夹，那么预测的文件夹名就是正确的
                        '如果只新增 1 个文件夹，那么复制 json 文件
                        Dim JsonFile As FileInfo = DeltaList(0).EnumerateFiles.First()
                        FileUtils.Copy(JsonFile.FullName, VersionFolder & NewInstanceName & ".json")
                        Logger.Info($"已复制新增的版本 JSON 文件：{JsonFile.FullName} -> {VersionFolder}{NewInstanceName}.json")
                    ElseIf DeltaList.Count > 1 Then
                        '新增了多个文件夹
                        Logger.Info($"有多个疑似的新增版本，无法确定：{DeltaList.Select(Function(d) d.Name).Join(";")}")
                    Else
                        '没有新增文件夹
                        Logger.Info("未找到新增的版本文件夹")
                    End If
                Catch ex As Exception
                    Throw New Exception(GetLang("LangModDownloadLibExceptionForgelikeInstallInstanceFail", LoaderName), ex)
                Finally
                    '清理文件
                    Try
                        Installer?.Dispose()
                        FileUtils.Delete(InstallerAddress)
                    Catch ex As Exception
                        Logger.Warn(ex, $"安装 {LoaderName} 清理文件时出错")
                    End Try
                End Try
            End Sub) With {.ProgressWeight = 10})
        Else
            Logger.Info($"检测为非新版 Forge：{LoaderVersion}")
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)(GetLang("LangModDownloadLibTaskForgelikeInstallMethodB", LoaderName),
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
                Dim Installer As ZipArchive = Nothing
                Try
                    '解压并获取信息
                    Installer = FileUtils.OpenZip(InstallerAddress)
                    Task.Progress = 0.2
                    Dim Json As JObject = Installer.GetEntry("install_profile.json").Open().ReadString().DeserializeJson()
                    Task.Progress = 0.4
                    '新建版本文件夹
                    DirectoryUtils.Create(VersionFolder)
                    Task.Progress = 0.5
                    If Json("install") Is Nothing Then
                        '中版：Legacy 方式 1
                        Logger.Info($"开始进行 Forge 安装，Legacy 方式 1：{InstallerAddress}")
                        '建立 Json 文件
                        Dim JsonVersion As JObject = Installer.GetEntry(Json("json").ToString.TrimStart("/")).Open().ReadString().DeserializeJson()
                        JsonVersion("id") = NewInstanceName
                        FileUtils.Write(VersionFolder & NewInstanceName & ".json", JsonVersion.ToString)
                        Task.Progress = 0.6
                        '解压支持库文件
                        Installer.Dispose()
                        FileUtils.ExtractToDirectory(InstallerAddress, InstallerAddress & "_unrar\")
                        If DirectoryUtils.Exists(InstallerAddress & "_unrar\maven\") Then DirectoryUtils.Copy(InstallerAddress & "_unrar\maven\", McFolder & "libraries\")
                        DirectoryUtils.Delete(InstallerAddress & "_unrar\")
                    Else
                        '旧版：Legacy 方式 2
                        Logger.Info($"开始进行 Forge 安装，Legacy 方式 2：{InstallerAddress}")
                        '解压 Jar 文件
                        Dim JarAddress As String = McLibGet(Json("install")("path"), CustomMcFolder:=McFolder)
                        FileUtils.Write(JarAddress, Installer.GetEntry(Json("install")("filePath")).Open)
                        Task.Progress = 0.9
                        '建立 Json 文件
                        Json("versionInfo")("id") = NewInstanceName
                        If Json("versionInfo")("inheritsFrom") Is Nothing Then CType(Json("versionInfo"), JObject).Add("inheritsFrom", Inherit)
                        FileUtils.Write(VersionFolder & NewInstanceName & ".json", Json("versionInfo").ToString)
                    End If
                Catch ex As Exception
                    Throw New Exception(GetLang("LangModDownloadLibExceptionForgelikeInstallFail"), ex)
                Finally
                    Try
                        '清理文件
                        If Installer IsNot Nothing Then Installer.Dispose()
                        FileUtils.Delete(InstallerAddress)
                        DirectoryUtils.Delete(InstallerAddress & "_unrar\")
                    Catch ex As Exception
                        Logger.Warn(ex, "非新版方式安装 Forge 清理文件时出错")
                    End Try
                End Try
            End Sub) With {.ProgressWeight = 1})
        End If

        Return Loaders
    End Function

#End Region

#Region "Forge 下载菜单"

    Public Sub ForgeDownloadListItemPreload(Stack As StackPanel, Entries As List(Of DlForgeVersionEntry), OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean)
        '如果只有一个版本，则不特别列出
        If Entries.IsSingle Then Return
        '获取推荐版本与最新版本
        Dim FreshVersion As DlForgeVersionEntry = Nothing
        If Entries.Any Then
            FreshVersion = Entries(0)
        Else
            Logger.Info("未找到可用的 Forge 版本")
        End If
        Dim RecommendedVersion As DlForgeVersionEntry = Nothing
        For Each Entry In Entries
            If Entry.IsRecommended Then RecommendedVersion = Entry
        Next
        '若推荐版本与最新版本为同一版本，则仅显示推荐版本
        If FreshVersion IsNot Nothing AndAlso FreshVersion Is RecommendedVersion Then FreshVersion = Nothing
        '显示各个版本
        If RecommendedVersion IsNot Nothing Then
            Dim Recommended = ForgeDownloadListItem(RecommendedVersion, OnClick, IsSaveOnly, False).Init()
            Recommended.Info = GetLang("LangDownloadRecommend") & If(Recommended.Info = "", "", GetLang("LangComma") & Recommended.Info)
            Stack.Children.Add(Recommended)
        End If
        If FreshVersion IsNot Nothing Then
            Dim Fresh = ForgeDownloadListItem(FreshVersion, OnClick, IsSaveOnly, False).Init()
            Fresh.Info = GetLang("LangDownloadLatest") & If(Fresh.Info = "", "", GetLang("LangComma") & Fresh.Info)
            Stack.Children.Add(Fresh)
        End If
        '添加间隔
        Stack.Children.Add(New TextBlock With {.Text = GetLang("LangDownloadAll") & " (" & Entries.Count & ")", .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(6, 13, 0, 4)})
    End Sub
    Public Function ForgeDownloadListItem(Entry As DlForgeVersionEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean, Optional IsUpperCase As Boolean = True) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            '建立控件
            Dim NewItem As New MyListItem With {
                .Title = Entry.VersionName, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
                .Info = {
                If(Entry.ReleaseTime = "", "", GetLang(If(IsUpperCase = False, "LangDownloadReleaseOn", "LangDownloadReleaseOnU"), Entry.ReleaseTime)),
                If(ModeDebug, GetLang("LangDownloadCategory") & Entry.Category, "")
                }.Where(Function(d) d <> "").Join(GetLang("LangComma")),
                .Logo = PathImage & "Blocks/Anvil.png"
            }
            AddHandler NewItem.Click, OnClick
            '建立菜单
            If IsSaveOnly Then
                NewItem.ContentHandler = AddressOf ForgeSaveContMenuBuild
            Else
                NewItem.ContentHandler = AddressOf ForgeContMenuBuild
            End If
            '结束
            Return NewItem
        End Function) With {.Height = 42}
    End Function
    Private Sub ForgeContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = GetLang("LangDownloadSaveAs")}
        AddHandler BtnSave.Click, AddressOf ForgeSave_Click
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf ForgeLog_Click
        sender.Buttons = {BtnSave, BtnInfo}
    End Sub
    Private Sub ForgeSaveContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf ForgeLog_Click
        sender.Buttons = {BtnInfo}
    End Sub
    Private Sub ForgeLog_Click(sender As Object, e As RoutedEventArgs)
        Dim Version As DlForgeVersionEntry
        If sender.Tag IsNot Nothing Then
            Version = sender.Tag
        ElseIf sender.Parent.Tag IsNot Nothing Then
            Version = sender.Parent.Tag
        Else
            Version = sender.Parent.Parent.Tag
        End If
        OpenWebsite($"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{Version.Inherit}-{Version.VersionName}/forge-{Version.Inherit}-{Version.VersionName}-changelog.txt")
    End Sub
    Public Sub ForgeSave_Click(sender As Object, e As RoutedEventArgs)
        Dim Version As DlForgeVersionEntry
        If sender.Tag IsNot Nothing Then
            Version = sender.Tag
        ElseIf sender.Parent.Tag IsNot Nothing Then
            Version = sender.Parent.Tag
        Else
            Version = sender.Parent.Parent.Tag
        End If
        McDownloadForgelikeSave(Version)
    End Sub

#End Region

#Region "Forge 推荐版本获取"

    ''' <summary>
    ''' 尝试刷新 Forge 推荐版本缓存。
    ''' </summary>
    Public Sub McDownloadForgeRecommendedRefresh()
        If IsForgeRecommendedRefreshed Then Return
        IsForgeRecommendedRefreshed = True
        RunInNewThread(
        Sub()
            Try
                Logger.Info("刷新 Forge 推荐版本缓存开始")
                Dim Result As String = NetRequestByClientRetry("https://bmclapi2.bangbang93.com/forge/promos", RequireJson:=True)
                If Result.Length < 1000 Then Throw New Exception("获取的结果过短（" & Result & "）")
                Dim ResultJson As JContainer = Result.DeserializeJson()
                '获取所有推荐版本列表
                Dim RecommendedList As New List(Of String)
                For Each Version As JObject In ResultJson
                    If Version("name") Is Nothing OrElse Version("build") Is Nothing Then Continue For
                    Dim Name As String = Version("name")
                    If Not Name.EndsWithF("-recommended") Then Continue For
                    '内容为："1.15.2":"31.2.0"
                    RecommendedList.Add("""" & Name.Replace("-recommended", """:""" & Version("build")("version").ToString & """"))
                Next
                If RecommendedList.Count < 5 Then Throw New Exception("获取的推荐版本数过少（" & Result & "）")
                '保存
                Dim CacheJson As String = "{" & RecommendedList.Join(",") & "}"
                FileUtils.Write(PathTemp & "Cache\ForgeRecommendedList.json", CacheJson)
                Logger.Info("刷新 Forge 推荐版本缓存成功")
            Catch ex As Exception
                Logger.Warn(ex, "刷新 Forge 推荐版本缓存失败")
            End Try
        End Sub, "ForgeRecommendedRefresh")
    End Sub
    Private IsForgeRecommendedRefreshed As Boolean = False

    ''' <summary>
    ''' 尝试获取某个 MC 版本对应的 Forge 推荐版本。如果不可用会返回 Nothing。
    ''' </summary>
    Public Function McDownloadForgeRecommendedGet(McVersion As String) As String
        Try
            If McVersion Is Nothing Then Return Nothing
            Dim List As String = FileUtils.TryReadAsString(PathTemp & "Cache\ForgeRecommendedList.json")
            If String.IsNullOrEmpty(List) Then
                Logger.Info("没有 Forge 推荐版本缓存文件")
                Return Nothing
            End If
            Dim Json As JObject = List.DeserializeJson()
            If Json Is Nothing OrElse (Not If(McVersion, "").Contains(".")) OrElse Not Json.ContainsKey(McVersion) Then Return Nothing
            Return If(Json(McVersion), "").ToString
        Catch ex As Exception
            Logger.Error(ex, $"获取 Forge 推荐版本失败（{If(McVersion, "null")}）")
            Return Nothing
        End Try
    End Function

#End Region

#Region "NeoForge 下载菜单"

    Public Sub NeoForgeDownloadListItemPreload(Stack As StackPanel, Entries As List(Of DlNeoForgeListEntry), OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean)
        '如果只有一个版本，则不特别列出
        If Entries.IsSingle Then Return
        '获取最新稳定版和测试版
        Dim FreshStableVersion As DlNeoForgeListEntry = Nothing
        Dim FreshBetaVersion As DlNeoForgeListEntry = Nothing
        If Entries.Any() Then
            For Each Entry In Entries.ToList()
                If Entry.IsBeta Then
                    If FreshBetaVersion Is Nothing Then FreshBetaVersion = Entry
                Else
                    FreshStableVersion = Entry
                    Exit For
                End If
            Next
        Else
            Logger.Warn("未找到可用的 NeoForge 版本")
        End If
        '显示各个版本
        If FreshStableVersion IsNot Nothing Then
            Dim Fresh = NeoForgeDownloadListItem(FreshStableVersion, OnClick, IsSaveOnly).Init()
            Fresh.Info = If(Fresh.Info = "", GetLang("LangDownloadNewStable"), GetLang("LangDownloadNewStable"))
            Stack.Children.Add(Fresh)
        End If
        If FreshBetaVersion IsNot Nothing Then
            Dim Fresh = NeoForgeDownloadListItem(FreshBetaVersion, OnClick, IsSaveOnly).Init()
            Fresh.Info = If(Fresh.Info = "", GetLang("LangDownloadNewTest"), GetLang("LangDownloadNewTest"))
            Stack.Children.Add(Fresh)
        End If
        '添加间隔
        Stack.Children.Add(New TextBlock With {.Text = GetLang("LangDownloadAll") & " (" & Entries.Count & ")", .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(6, 13, 0, 4)})
    End Sub
    Public Function NeoForgeDownloadListItem(Info As DlNeoForgeListEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            '建立控件
            Dim NewItem As New MyListItem With {
                .Title = Info.VersionName, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Info,
                .Info = If(Info.IsBeta, GetLang("LangDownloadTest"), GetLang("LangDownloadStable")),
                .Logo = PathImage & "Blocks/NeoForge.png"
            }
            AddHandler NewItem.Click, OnClick
            '建立菜单
            If IsSaveOnly Then
                NewItem.ContentHandler = AddressOf NeoForgeSaveContMenuBuild
            Else
                NewItem.ContentHandler = AddressOf NeoForgeContMenuBuild
            End If
            Return NewItem
        End Function
        ) With {.Height = 42}
    End Function
    Private Sub NeoForgeContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = GetLang("LangDownloadSaveAs")}
        AddHandler BtnSave.Click, AddressOf NeoForgeSave_Click
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf NeoForgeLog_Click
        sender.Buttons = {BtnSave, BtnInfo}
    End Sub
    Private Sub NeoForgeSaveContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = GetLang("LangDownloadChangelog")}
        AddHandler BtnInfo.Click, AddressOf NeoForgeLog_Click
        sender.Buttons = {BtnInfo}
    End Sub
    Private Sub NeoForgeLog_Click(sender As Object, e As RoutedEventArgs)
        Dim Info As DlNeoForgeListEntry
        If sender.Tag IsNot Nothing Then
            Info = sender.Tag
        ElseIf sender.Parent.Tag IsNot Nothing Then
            Info = sender.Parent.Tag
        Else
            Info = sender.Parent.Parent.Tag
        End If
        OpenWebsite(Info.UrlBase & "-changelog.txt")
    End Sub
    Public Sub NeoForgeSave_Click(sender As Object, e As RoutedEventArgs)
        Dim Info As DlNeoForgeListEntry
        If sender.Tag IsNot Nothing Then
            Info = sender.Tag
        ElseIf sender.Parent.Tag IsNot Nothing Then
            Info = sender.Parent.Tag
        Else
            Info = sender.Parent.Parent.Tag
        End If
        McDownloadForgelikeSave(Info)
    End Sub

#End Region

#Region "Fabric 下载"

    Public Sub McDownloadFabricLoaderSave(DownloadInfo As JObject)
        Try
            Dim Url As String = DownloadInfo("url").ToString
            Dim FileName As String = PathUtils.GetLastPart(Url)
            Dim Version As String = PathUtils.GetLastPart(DownloadInfo("version").ToString)
            Dim Target As String = Dialogs.SaveFile(GetLang("LangSaveAs"), FileName, filter:={("jar", "Fabric 安装器")})
            If Target Is Nothing Then Return

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> GetLang("LangModDownloadLibTaskFabricInstallerDownload", Version) Then Continue For
                Hint(GetLang("LangModDownloadLibHintInstanceDownloading"), HintType.Red)
                Return
            Next

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            '下载
            'BMCLAPI 不支持 Fabric Installer 下载
            Dim Address As New List(Of String)
            Address.Add(Url)
            Loaders.Add(New LoaderDownload(GetLang("LangModDownloadLibTaskDownloadMainFile"), New List(Of NetFile) From {New NetFile(Address.ToArray, Target, New FileChecker With {.MinSize = 1024 * 64})}) With {.ProgressWeight = 15})
            '启动
            Dim Loader As New LoaderCombo(Of JObject)(GetLang("LangModDownloadLibTaskFabricInstallerDownload", Version), Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(DownloadInfo)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Logger.Error(ex, "开始 Fabric 安装器下载失败")
        End Try
    End Sub

    ''' <summary>
    ''' 获取下载某个 Fabric 版本的加载器列表。
    ''' 这不会补全所需的支持库文件，需要后续手动补全支持库。
    ''' </summary>
    Private Function McDownloadFabricLoader(FabricVersion As String, MinecraftName As String, McFolder As String) As List(Of LoaderBase)

        '参数初始化
        Dim IsCustomFolder As Boolean = McFolder <> McFolderSelected
        Dim Id As String = "fabric-loader-" & FabricVersion & "-" & MinecraftName
        Dim VersionFolder As String = McFolder & "versions\" & Id & "\"
        Dim Loaders As New List(Of LoaderBase)

        '下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite") '放在 ID 后面避免影响版本文件夹名称
        Loaders.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangPageSpeedRightGetFabricMainFileDownloadAddress"),
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            Task.Output = New List(Of NetFile) From {New NetFile({
                "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/" & MinecraftName & "/" & FabricVersion & "/profile/json",
                "https://meta.fabricmc.net/v2/versions/loader/" & MinecraftName & "/" & FabricVersion & "/profile/json"
            }, VersionFolder & Id & ".json", New FileChecker With {.IsJson = True})} '构造文件请求
        End Sub) With {.ProgressWeight = 0.5})
        Loaders.Add(New LoaderDownload(GetLang("LangPageSpeedRightDownloadFabricMainFile"), New List(Of NetFile)) With {.ProgressWeight = 2.5})

        Return Loaders
    End Function

#End Region

#Region "Fabric 下载菜单"

    Public Function FabricDownloadListItem(Entry As JObject, OnClick As MyListItem.ClickEventHandler) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            Dim NewItem As New MyListItem With {
                .Title = Entry("version").ToString.Replace("+build", ""), .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
                .Info = If(Entry("stable").ToObject(Of Boolean), GetLang("LangDownloadStable"), GetLang("LangDownloadTest")),
                .Logo = PathImage & "Blocks/Fabric.png"
            }
            AddHandler NewItem.Click, OnClick
            Return NewItem
        End Function) With {.Height = 42}
    End Function
    Public Function FabricApiDownloadListItem(Entry As ResourceVersion, OnClick As MyListItem.ClickEventHandler) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            Dim NewItem As New MyListItem With {
                .Title = Entry.Display.Split("]")(1).Replace("Fabric API ", "").Replace(" build ", ".").BeforeFirst("+").Trim, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
                .Info = Entry.ReleaseTypeDisplay & GetLang("LangComma") & GetLang("LangDownloadReleaseOn", GetLocalTimeFormat(Entry.ReleaseDate)),
                .Logo = PathImage & "Blocks/Fabric.png"
            }
            AddHandler NewItem.Click, OnClick
            Return NewItem
        End Function) With {.Height = 42}
    End Function
    Public Function OptiFabricDownloadListItem(Entry As ResourceVersion, OnClick As MyListItem.ClickEventHandler) As MyVirtualizingElement(Of MyListItem)
        Return New MyVirtualizingElement(Of MyListItem)(
        Function()
            Dim NewItem As New MyListItem With {
               .Title = Entry.Display.Lower.Replace("optifabric-", "").Replace(".jar", "").Trim.TrimStart("v"), .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
               .Info = Entry.ReleaseTypeDisplay & GetLang("LangComma") & GetLang("LangDownloadReleaseOn", GetLocalTimeFormat(Entry.ReleaseDate)),
               .Logo = PathImage & "Blocks/OptiFabric.png"
            }
            AddHandler NewItem.Click, OnClick
            Return NewItem
        End Function) With {.Height = 42}
    End Function

#End Region

#Region "合并安装"

    ''' <summary>
    ''' 安装请求。
    ''' </summary>
    Public Class McInstallRequest

        ''' <summary>
        ''' 必填。安装目标版本名称。
        ''' </summary>
        Public NewInstanceName As String
        ''' <summary>
        ''' 必填。安装目标文件夹。
        ''' </summary>
        Public VersionFolder As String

        ''' <summary>
        ''' 必填。欲下载的 Minecraft 的版本名。
        ''' </summary>
        Public MinecraftName As String = Nothing
        ''' <summary>
        ''' 可选。欲下载的 Minecraft Json 地址。
        ''' </summary>
        Public MinecraftJson As String = Nothing

        '若要下载 OptiFine，则需要在下面两项中完成至少一项
        ''' <summary>
        ''' 欲下载的 OptiFine 版本名。例如 HD_U_F6_pre1。
        ''' </summary>
        Public OptiFineVersion As String = Nothing
        ''' <summary>
        ''' 欲下载的 OptiFine 详细信息。
        ''' </summary>
        Public OptiFineEntry As DlOptiFineListEntry = Nothing

        '若要下载 Forge，则需要在下面两项中完成至少一项
        ''' <summary>
        ''' 欲下载的 Forge 版本名。接受例如 36.1.4 / 14.23.5.2859 / 1.19-41.1.0 的输入。
        ''' </summary>
        Public ForgeVersion As String = Nothing
        ''' <summary>
        ''' 欲下载的 Forge。
        ''' </summary>
        Public ForgeEntry As DlForgeVersionEntry = Nothing

        '若要下载 NeoForge，则需要在下面两项中完成至少一项
        ''' <summary>
        ''' 欲下载的 NeoForge 版本名。
        ''' </summary>
        Public NeoForgeVersion As String = Nothing
        ''' <summary>
        ''' 欲下载的 NeoForge。
        ''' </summary>
        Public NeoForgeEntry As DlNeoForgeListEntry = Nothing

        ''' <summary>
        ''' 欲下载的 Fabric Loader 版本名。
        ''' </summary>
        Public FabricVersion As String = Nothing

        ''' <summary>
        ''' 欲下载的 Fabric API 信息。
        ''' </summary>
        Public FabricApi As ResourceVersion = Nothing

        ''' <summary>
        ''' 欲下载的 OptiFabric 信息。
        ''' </summary>
        Public OptiFabric As ResourceVersion = Nothing

        ''' <summary>
        ''' 欲下载的 LiteLoader 详细信息。
        ''' </summary>
        Public LiteLoaderEntry As DlLiteLoaderListEntry = Nothing

    End Class

    ''' <summary>
    ''' 在加载器状态改变后显示一条提示。
    ''' 不会进行任何其他操作。
    ''' </summary>
    Public Sub LoaderStateChangedHintOnly(Loader As LoaderBase)
        Select Case Loader.State
            Case LoadState.Finished
                Hint(GetLang("LangModDownloadLibSuccess", Loader.Name), HintType.Green)
            Case LoadState.Failed
                Hint(GetLang("LangModDownloadLibFail", Loader.Name, Loader.Error.GetDisplay(False)), HintType.Red)
            Case LoadState.Canceled
                Hint(GetLang("LangModDownloadLibCancel", Loader.Name), HintType.Blue)
        End Select
    End Sub
    ''' <summary>
    ''' 安装加载器状态改变后进行提示和重载文件夹列表的方法。
    ''' </summary>
    Public Sub McInstallState(Loader As LoaderBase)
        Select Case Loader.State
            Case LoadState.Finished
                WriteIni(McFolderSelected & "PCL.ini", "InstanceCache", "") '清空缓存（合并安装会先生成文件夹，这会在刷新时误判为可以使用缓存）
                Hint(GetLang("LangModDownloadLibSuccess", Loader.Name), HintType.Green)
                'TODO: 自动选择安装成功的版本
            Case LoadState.Failed
                MyMsgBox(Loader.Error.GetDisplay(True), GetLang("LangModDownloadLibFail", Loader.Name), IsWarn:=True)
            Case LoadState.Canceled
                Hint(GetLang("LangModDownloadLibCancel", Loader.Name), HintType.Blue)
            Case LoadState.Loading
                Return '不重新加载版本列表
        End Select
        McInstallFailedClearFolder(Loader)
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub
    Public Sub McInstallFailedClearFolder(Loader)
        Try
            Thread.Sleep(1000) '防止存在尚未完全释放的文件，导致清理失败（例如整合包安装）
            If Loader.State = LoadState.Failed OrElse Loader.State = LoadState.Canceled Then
                '删除版本文件夹
                If DirectoryUtils.Exists(Loader.Input & "saves\") OrElse DirectoryUtils.Exists(Loader.Input & "versions\") Then
                    Logger.Warn($"由于版本已被独立启动，不清理版本文件夹：{Loader.Input}")
                Else
                    Logger.Warn($"由于下载失败或取消，清理版本文件夹：{Loader.Input}")
                    DirectoryUtils.Delete(Loader.Input)
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "下载失败或取消后清理版本文件夹失败")
        End Try
    End Sub

    ''' <summary>
    ''' 进行合并安装。返回是否已经开始安装（例如如果没有安装 Java 则会进行提示并返回 False）
    ''' </summary>
    Public Function McInstall(Request As McInstallRequest) As Boolean
        Try
            Dim SubLoaders = McInstallLoader(Request)
            If SubLoaders Is Nothing Then Return False
            Dim Loader As New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskMcInstall", Request.NewInstanceName), SubLoaders) With {.OnStateChanged = AddressOf McInstallState}

            '启动
            Loader.Start(Request.VersionFolder)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            Return True

        Catch ex As Exception
            Logger.Error(ex, GetLang("LangModDownloadLibExceptionInstallInstallFail"))
            Return False
        End Try
    End Function
    ''' <summary>
    ''' 获取合并安装加载器列表，并进行前期的缓存清理与 Java 检查工作。
    ''' </summary>
    Public Function McInstallLoader(Request As McInstallRequest) As List(Of LoaderBase)
        '获取缓存目录（安装 Mod 加载器的文件夹不能包含空格）
        Dim TempMcFolder As String = RequestTaskTempFolder(Request.OptiFineEntry IsNot Nothing OrElse Request.ForgeEntry IsNot Nothing OrElse Request.NeoForgeEntry IsNot Nothing)

        '获取参数
        Dim VersionFolder As String = McFolderSelected & "versions\" & Request.NewInstanceName & "\"
        If DirectoryUtils.Exists(TempMcFolder) Then DirectoryUtils.Delete(TempMcFolder)
        Dim OptiFineFolder As String = Nothing
        If Request.OptiFineVersion IsNot Nothing Then
            If Request.OptiFineVersion.Contains("_HD_U_") Then Request.OptiFineVersion = "HD_U_" & Request.OptiFineVersion.AfterLast("_HD_U_") '#735
            Request.OptiFineEntry = New DlOptiFineListEntry With {
                .DisplayName = Request.MinecraftName & " " & Request.OptiFineVersion.Replace("HD_U_", "").Replace("_", "").Replace("pre", " pre"),
                .Inherit = Request.MinecraftName,
                .IsPreview = Request.OptiFineVersion.ContainsIgnoreCase("pre"),
                .InstanceName = Request.MinecraftName & "-OptiFine_" & Request.OptiFineVersion,
                .FileName = If(Request.OptiFineVersion.ContainsIgnoreCase("pre"), "preview_", "") &
                    "OptiFine_" & Request.MinecraftName & "_" & Request.OptiFineVersion & ".jar"
            }
        End If
        If Request.OptiFineEntry IsNot Nothing Then OptiFineFolder = TempMcFolder & "versions\" & Request.OptiFineEntry.InstanceName
        Dim ForgeFolder As String = Nothing
        If Request.ForgeEntry IsNot Nothing Then Request.ForgeVersion = If(Request.ForgeVersion, Request.ForgeEntry.VersionName)
        If Request.ForgeVersion IsNot Nothing Then ForgeFolder = TempMcFolder & "versions\forge-" & Request.ForgeVersion
        Dim NeoForgeFolder As String = Nothing
        If Request.NeoForgeEntry IsNot Nothing Then Request.NeoForgeVersion = If(Request.NeoForgeVersion, Request.NeoForgeEntry.VersionName)
        If Request.NeoForgeVersion IsNot Nothing Then NeoForgeFolder = TempMcFolder & "versions\neoforge-" & Request.NeoForgeVersion
        Dim FabricFolder As String = Nothing
        If Request.FabricVersion IsNot Nothing Then FabricFolder = TempMcFolder & "versions\fabric-loader-" & Request.FabricVersion & "-" & Request.MinecraftName
        Dim LiteLoaderFolder As String = Nothing
        If Request.LiteLoaderEntry IsNot Nothing Then LiteLoaderFolder = TempMcFolder & "versions\" & Request.MinecraftName & "-LiteLoader"

        '判断 OptiFine 是否作为 Mod 进行下载
        Dim Modable As Boolean = Request.FabricVersion IsNot Nothing OrElse Request.ForgeEntry IsNot Nothing OrElse Request.NeoForgeEntry IsNot Nothing OrElse Request.LiteLoaderEntry IsNot Nothing
        Dim ModsTempFolder As String = TempMcFolder & "mods\"
        Dim OptiFineAsMod As Boolean = Request.OptiFineEntry IsNot Nothing AndAlso Modable '选择了 OptiFine 与任意 Mod 加载器
        If OptiFineAsMod Then
            Logger.Info("OptiFine 将作为 Mod 进行下载")
            If Request.LiteLoaderEntry IsNot Nothing Then
                OptiFineFolder = ModsTempFolder & Request.MinecraftName & "\" '#8147
            Else
                OptiFineFolder = ModsTempFolder
            End If
        End If

        '记录日志
        If OptiFineFolder IsNot Nothing Then Logger.Info($"OptiFine 缓存：{OptiFineFolder}")
        If ForgeFolder IsNot Nothing Then Logger.Info($"Forge 缓存：{ForgeFolder}")
        If NeoForgeFolder IsNot Nothing Then Logger.Info($"NeoForge 缓存：{NeoForgeFolder}")
        If FabricFolder IsNot Nothing Then Logger.Info($"Fabric 缓存：{FabricFolder}")
        If LiteLoaderFolder IsNot Nothing Then Logger.Info($"LiteLoader 缓存：{LiteLoaderFolder}")
        Logger.Info($"对应的原版版本：{Request.MinecraftName}")

        '重复版本检查
        If FileUtils.Exists($"{VersionFolder}{Request.NewInstanceName}.json") Then
            Hint(GetLang("LangModDownloadLibHintMcExist", Request.NewInstanceName), HintType.Red)
            Throw New OperationCanceledException
        End If

        Dim LoaderList As New List(Of LoaderBase)
        '添加忽略标识
        LoaderList.Add(New LoaderTask(Of Integer, Integer)(GetLang("LangModDownloadLibTaskAddIgnore"), Sub() FileUtils.Write(VersionFolder & ".pclignore", $"用于临时地在 PCL 的版本列表中屏蔽此版本。{vbCr}This file is used to temporarily hide this instance in the PCL instance list.")) With {.Show = False, .Block = False})
        'Fabric API
        If Request.FabricApi IsNot Nothing Then
            LoaderList.Add(New LoaderDownload(GetLang("LangPageSpeedRightDownloadFabricAPI"), New List(Of NetFile) From {Request.FabricApi.ToNetFile(ModsTempFolder, ResourceVersion.DownloadReason.Dependency, Request.MinecraftName, ModLoaders.Fabric)}) With {.ProgressWeight = 3, .Block = False})
        End If
        'OptiFabric
        If Request.OptiFabric IsNot Nothing Then
            LoaderList.Add(New LoaderDownload(GetLang("LangPageSpeedRightDownloadOptiFabric"), New List(Of NetFile) From {Request.OptiFabric.ToNetFile(ModsTempFolder, ResourceVersion.DownloadReason.Dependency, Request.MinecraftName, ModLoaders.Fabric)}) With {.ProgressWeight = 3, .Block = False})
        End If
        '原版
        Dim ClientLoader = New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadVanilla", Request.MinecraftName), McDownloadClientLoader(Request.MinecraftName, Request.MinecraftJson, Request.NewInstanceName)) With {.Show = False, .ProgressWeight = 39,
            .Block = Request.ForgeVersion Is Nothing AndAlso Request.NeoForgeVersion Is Nothing AndAlso Request.OptiFineEntry Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing}
        LoaderList.Add(ClientLoader)
        'OptiFine
        If Request.OptiFineEntry IsNot Nothing Then
            If OptiFineAsMod Then
                LoaderList.Add(New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadOptiFine", Request.OptiFineEntry.DisplayName), McDownloadOptiFineSaveLoader(Request.OptiFineEntry, OptiFineFolder & Request.OptiFineEntry.FileName)) With {.Show = False, .ProgressWeight = 16,
                    .Block = Request.ForgeVersion Is Nothing AndAlso Request.NeoForgeVersion Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing})
            Else
                LoaderList.Add(New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadOptiFine", Request.OptiFineEntry.DisplayName), McDownloadOptiFineLoader(Request.OptiFineEntry, TempMcFolder, ClientLoader, Request.VersionFolder)) With {.Show = False, .ProgressWeight = 24,
                    .Block = Request.ForgeVersion Is Nothing AndAlso Request.NeoForgeVersion Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing})
            End If
        End If
        'Forge
        If Request.ForgeVersion IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadForge", Request.ForgeVersion), McDownloadForgelikeLoader(False, Request.ForgeVersion, "forge-" & Request.ForgeVersion, Request.MinecraftName, Request.ForgeEntry, TempMcFolder, ClientLoader, Request.VersionFolder)) With {.Show = False, .ProgressWeight = 25,
                .Block = Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing AndAlso Request.NeoForgeEntry Is Nothing})
        End If
        'NeoForge
        If Request.NeoForgeVersion IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadNeoForge", Request.NeoForgeVersion), McDownloadForgelikeLoader(True, Request.NeoForgeVersion, "neoforge-" & Request.NeoForgeVersion, Request.MinecraftName, Request.NeoForgeEntry, TempMcFolder, ClientLoader, Request.VersionFolder)) With {.Show = False, .ProgressWeight = 25,
                .Block = Request.ForgeEntry Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing})
        End If
        'LiteLoader
        If Request.LiteLoaderEntry IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadLiteLoader", Request.MinecraftName), McDownloadLiteLoaderLoader(Request.LiteLoaderEntry, TempMcFolder)) With {.Show = False, .ProgressWeight = 1,
                .Block = Request.FabricVersion Is Nothing})
        End If
        'Fabric
        If Request.FabricVersion IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)(GetLang("LangModDownloadLibTaskDownloadFabric", Request.FabricVersion), McDownloadFabricLoader(Request.FabricVersion, Request.MinecraftName, TempMcFolder)) With {.Show = False, .ProgressWeight = 2,
                .Block = True})
        End If
        '合并安装
        LoaderList.Add(New LoaderTask(Of String, String)(GetLang("LangPageSpeedRightInstallGame"),
        Sub(Task As LoaderTask(Of String, String))
            '合并 JSON
            MergeJson(VersionFolder, VersionFolder, OptiFineFolder, OptiFineAsMod, ForgeFolder, Request.ForgeVersion, NeoForgeFolder, Request.NeoForgeVersion, FabricFolder, LiteLoaderFolder)
            Task.Progress = 0.2
            '迁移文件
            DirectoryUtils.Copy(TempMcFolder & "libraries", McFolderSelected & "libraries")
            Task.Progress = 0.8
            '创建 Mod 和资源包文件夹
            Dim ModsFolder = New McInstance(VersionFolder).PathIndie & "mods\" '版本隔离信息在此时被决定
            If DirectoryUtils.Exists(ModsTempFolder) Then
                DirectoryUtils.Copy(ModsTempFolder, ModsFolder)
            ElseIf Modable Then
                DirectoryUtils.Create(ModsFolder)
                Logger.Info($"自动创建 Mod 文件夹：{ModsFolder}")
            End If
            Dim ResourcepacksFolder = New McInstance(VersionFolder).PathIndie & "resourcepacks\"
            DirectoryUtils.Create(ResourcepacksFolder)
            Logger.Info($"自动创建资源包文件夹：{ResourcepacksFolder}")
        End Sub) With {.ProgressWeight = 2, .Block = True})
        '补全文件
        If Request.OptiFineEntry IsNot Nothing OrElse (Request.ForgeVersion IsNot Nothing AndAlso Request.ForgeVersion.BeforeFirst(".") >= 20) OrElse Request.NeoForgeVersion IsNot Nothing OrElse Request.FabricVersion IsNot Nothing OrElse Request.LiteLoaderEntry IsNot Nothing Then
            Dim LoadersLib As New List(Of LoaderBase)
            LoadersLib.Add(New LoaderTask(Of String, List(Of NetFile))(GetLang("LangModModpackTaskAnalysisLibSideLoader"), Sub(Task) Task.Output = McLibNetFilesFromInstance(New McInstance(VersionFolder))) With {.ProgressWeight = 1, .Show = False})
            LoadersLib.Add(New LoaderDownload(GetLang("LangModModpackTaskDownloadLibSideLoader"), New List(Of NetFile)) With {.ProgressWeight = 7, .Show = False})
            LoaderList.Add(New LoaderCombo(Of String)(GetLang("LangPageSpeedRightDownloadGameSupportLibrary"), LoadersLib) With {.ProgressWeight = 8})
        End If
        '删除忽略标识
        LoaderList.Add(New LoaderTask(Of Integer, Integer)(GetLang("LangModDownloadLibTaskDelIgnore"), Sub() FileUtils.Delete(VersionFolder & ".pclignore")) With {.Show = False})
        '总加载器
        Return LoaderList
    End Function

    ''' <summary>
    ''' 将多个版本 JSON 进行合并，如果目标已存在则直接覆盖。失败会抛出异常。
    ''' </summary>
    Private Sub MergeJson(OutputFolder As String, MinecraftFolder As String, Optional OptiFineFolder As String = Nothing, Optional OptiFineAsMod As Boolean = False, Optional ForgeFolder As String = Nothing, Optional ForgeVersion As String = Nothing, Optional NeoForgeFolder As String = Nothing, Optional NeoForgeVersion As String = Nothing, Optional FabricFolder As String = Nothing, Optional LiteLoaderFolder As String = Nothing)
        Logger.Info($"开始进行版本合并，输出：{OutputFolder}，Minecraft：{MinecraftFolder}" &
            If(OptiFineFolder IsNot Nothing, "，OptiFine：" & OptiFineFolder, "") &
            If(ForgeFolder IsNot Nothing, "，Forge：" & ForgeFolder, "") &
            If(NeoForgeFolder IsNot Nothing, "，NeoForge：" & NeoForgeFolder, "") &
            If(LiteLoaderFolder IsNot Nothing, "，LiteLoader：" & LiteLoaderFolder, "") &
            If(FabricFolder IsNot Nothing, "，Fabric：" & FabricFolder, ""))
        DirectoryUtils.Create(OutputFolder)

        Dim HasOptiFine As Boolean = OptiFineFolder IsNot Nothing AndAlso Not OptiFineAsMod, HasForge As Boolean = ForgeFolder IsNot Nothing, HasNeoForge As Boolean = NeoForgeFolder IsNot Nothing, HasLiteLoader As Boolean = LiteLoaderFolder IsNot Nothing, HasFabric As Boolean = FabricFolder IsNot Nothing
        Dim OutputName As String, MinecraftName As String, OptiFineName As String, ForgeName As String, NeoForgeName As String, LiteLoaderName As String, FabricName As String
        Dim OutputJsonPath As String, MinecraftJsonPath As String, OptiFineJsonPath As String = Nothing, ForgeJsonPath As String = Nothing, NeoForgeJsonPath As String = Nothing, LiteLoaderJsonPath As String = Nothing, FabricJsonPath As String = Nothing
        Dim OutputJar As String, MinecraftJar As String
#Region "初始化路径信息"
        If Not OutputFolder.EndsWithF("\") Then OutputFolder += "\"
        OutputName = PathUtils.GetLastPart(OutputFolder)
        OutputJsonPath = OutputFolder & OutputName & ".json"
        OutputJar = OutputFolder & OutputName & ".jar"

        If Not MinecraftFolder.EndsWithF("\") Then MinecraftFolder += "\"
        MinecraftName = PathUtils.GetLastPart(MinecraftFolder)
        MinecraftJsonPath = MinecraftFolder & MinecraftName & ".json"
        MinecraftJar = MinecraftFolder & MinecraftName & ".jar"

        If HasOptiFine Then
            If Not OptiFineFolder.EndsWithF("\") Then OptiFineFolder += "\"
            OptiFineName = PathUtils.GetLastPart(OptiFineFolder)
            OptiFineJsonPath = OptiFineFolder & OptiFineName & ".json"
        End If

        If HasForge Then
            If Not ForgeFolder.EndsWithF("\") Then ForgeFolder += "\"
            ForgeName = PathUtils.GetLastPart(ForgeFolder)
            ForgeJsonPath = ForgeFolder & ForgeName & ".json"
        End If

        If HasNeoForge Then
            If Not NeoForgeFolder.EndsWithF("\") Then NeoForgeFolder += "\"
            NeoForgeName = PathUtils.GetLastPart(NeoForgeFolder)
            NeoForgeJsonPath = NeoForgeFolder & NeoForgeName & ".json"
        End If

        If HasLiteLoader Then
            If Not LiteLoaderFolder.EndsWithF("\") Then LiteLoaderFolder += "\"
            LiteLoaderName = PathUtils.GetLastPart(LiteLoaderFolder)
            LiteLoaderJsonPath = LiteLoaderFolder & LiteLoaderName & ".json"
        End If

        If HasFabric Then
            If Not FabricFolder.EndsWithF("\") Then FabricFolder += "\"
            FabricName = PathUtils.GetLastPart(FabricFolder)
            FabricJsonPath = FabricFolder & FabricName & ".json"
        End If
#End Region

        Dim OutputJson As JObject, MinecraftJson As JObject, OptiFineJson As JObject = Nothing, ForgeJson As JObject = Nothing, NeoForgeJson As JObject = Nothing, LiteLoaderJson As JObject = Nothing, FabricJson As JObject = Nothing
#Region "读取文件并检查文件是否合规"
        Dim MinecraftJsonText As String = If(FileUtils.TryReadAsString(MinecraftJsonPath), "")
        If Not MinecraftJsonText.StartsWithF("{") Then Throw New Exception("Minecraft json 有误，地址：" & MinecraftJsonPath & "，前段内容：" & MinecraftJsonText.Substring(0, Math.Min(MinecraftJsonText.Length, 1000)))
        MinecraftJson = MinecraftJsonText.DeserializeJson()

        If HasOptiFine Then
            Dim OptiFineJsonText As String = If(FileUtils.TryReadAsString(OptiFineJsonPath), "")
            If Not OptiFineJsonText.StartsWithF("{") Then Throw New Exception("OptiFine json 有误，地址：" & OptiFineJsonPath & "，前段内容：" & OptiFineJsonText.Substring(0, Math.Min(OptiFineJsonText.Length, 1000)))
            OptiFineJson = OptiFineJsonText.DeserializeJson()
        End If

        If HasForge Then
            Dim ForgeJsonText As String = If(FileUtils.TryReadAsString(ForgeJsonPath), "")
            If Not ForgeJsonText.StartsWithF("{") Then Throw New Exception("Forge json 有误，地址：" & ForgeJsonPath & "，前段内容：" & ForgeJsonText.Substring(0, Math.Min(ForgeJsonText.Length, 1000)))
            ForgeJson = ForgeJsonText.DeserializeJson()
        End If

        If HasNeoForge Then
            Dim NeoForgeJsonText As String = If(FileUtils.TryReadAsString(NeoForgeJsonPath), "")
            If Not NeoForgeJsonText.StartsWithF("{") Then Throw New Exception("NeoForge json 有误，地址：" & NeoForgeJsonPath & "，前段内容：" & NeoForgeJsonText.Substring(0, Math.Min(NeoForgeJsonText.Length, 1000)))
            NeoForgeJson = NeoForgeJsonText.DeserializeJson()
        End If

        If HasLiteLoader Then
            Dim LiteLoaderJsonText As String = If(FileUtils.TryReadAsString(LiteLoaderJsonPath), "")
            If Not LiteLoaderJsonText.StartsWithF("{") Then Throw New Exception("LiteLoader json 有误，地址：" & LiteLoaderJsonPath & "，前段内容：" & LiteLoaderJsonText.Substring(0, Math.Min(LiteLoaderJsonText.Length, 1000)))
            LiteLoaderJson = LiteLoaderJsonText.DeserializeJson()
        End If

        If HasFabric Then
            Dim FabricJsonText As String = If(FileUtils.TryReadAsString(FabricJsonPath), "")
            If Not FabricJsonText.StartsWithF("{") Then Throw New Exception("Fabric json 有误，地址：" & FabricJsonPath & "，前段内容：" & FabricJsonText.Substring(0, Math.Min(FabricJsonText.Length, 1000)))
            FabricJson = FabricJsonText.DeserializeJson()
        End If
#End Region

#Region "处理 JSON 文件"
        '获取 minecraftArguments
        Dim AllArguments As String =
            If(MinecraftJson("minecraftArguments"), " ").ToString & " " &
            If(OptiFineJson IsNot Nothing, If(OptiFineJson("minecraftArguments"), " ").ToString, " ") & " " &
            If(ForgeJson IsNot Nothing, If(ForgeJson("minecraftArguments"), " ").ToString, " ") & " " &
            If(NeoForgeJson IsNot Nothing, If(NeoForgeJson("minecraftArguments"), " ").ToString, " ") & " " &
            If(LiteLoaderJson IsNot Nothing, If(LiteLoaderJson("minecraftArguments"), " ").ToString, " ")
        '分割参数字符串
        Dim RawArguments As List(Of String) = AllArguments.Split(" ", True).Select(Function(l) l.Trim).ToList
        Dim SplitArguments As New List(Of String)
        For i = 0 To RawArguments.Count - 1
            If RawArguments(i).StartsWithF("-") Then
                SplitArguments.Add(RawArguments(i))
            ElseIf SplitArguments.Any AndAlso SplitArguments.Last.StartsWithF("-") AndAlso Not SplitArguments.Last.Contains(" ") Then
                SplitArguments(SplitArguments.Count - 1) = SplitArguments.Last & " " & RawArguments(i)
            Else
                SplitArguments.Add(RawArguments(i))
            End If
        Next
        Dim RealArguments As String = SplitArguments.Distinct.Join(" ")
        '合并
        '相关讨论见 #2801
        OutputJson = MinecraftJson
        If HasOptiFine Then
            '合并 OptiFine
            OptiFineJson.Remove("releaseTime")
            OptiFineJson.Remove("time")
            OutputJson.Merge(OptiFineJson)
        End If
        If HasForge Then
            '合并 Forge
            ForgeJson.Remove("releaseTime")
            ForgeJson.Remove("time")
            OutputJson.Merge(ForgeJson)
        End If
        If HasNeoForge Then
            '合并 NeoForge
            NeoForgeJson.Remove("releaseTime")
            NeoForgeJson.Remove("time")
            OutputJson.Merge(NeoForgeJson)
        End If
        If HasLiteLoader Then
            '合并 LiteLoader
            LiteLoaderJson.Remove("releaseTime")
            LiteLoaderJson.Remove("time")
            OutputJson.Merge(LiteLoaderJson)
        End If
        If HasFabric Then
            '合并 Fabric
            FabricJson.Remove("releaseTime")
            FabricJson.Remove("time")
            OutputJson.Merge(FabricJson)
        End If
        '修改
        If RealArguments IsNot Nothing AndAlso RealArguments.Replace(" ", "") <> "" Then OutputJson("minecraftArguments") = RealArguments
        OutputJson.Remove("_comment_")
        OutputJson.Remove("inheritsFrom")
        OutputJson.Remove("jar")
        OutputJson("id") = OutputName
#End Region

#Region "保存"
        FileUtils.Write(OutputJsonPath, OutputJson.ToString)
        FileUtils.Copy(MinecraftJar, OutputJar)
        Logger.Info($"版本合并 {OutputName} 完成")
#End Region

    End Sub

#End Region

    ''' <summary>
    ''' 如果 OptiFine 与 Forge 同时开始安装，就会导致 Forge 安装失败。
    ''' </summary>
    Private InstallSyncLock As New Object
    ''' <summary>
    ''' 如果 OptiFine 与 Forge 同时复制原版 JAR，就会导致复制文件时冲突。
    ''' </summary>
    Private VanillaSyncLock As New Object

    ''' <summary>
    ''' 释放补丁文件并返回完整文件路径。
    ''' </summary>
    ''' <param name="Patch">"LUA" 或 "JavaWrapper"。</param>
    Public Function ExtractPatch(Patch As String) As String
        Dim PatchPath As String = $"{PathPure}{Patch}.jar"
        Logger.Info($"选定的 {Patch} 路径：{PatchPath}")
        Static Lock As New Object
        SyncLock Lock '避免 OptiFine 和 Forge 安装时同时释放导致冲突
            Try
                ExtractResources(PatchPath, Patch)
            Catch ex As Exception
                If FileUtils.Exists(PatchPath) Then
                    '因为未知原因可能变为只读文件（#4243）
                    Logger.Warn(ex, $"{Patch} 文件释放失败，但文件已存在，将在删除后尝试重新生成")
                    Try
                        FileUtils.Delete(PatchPath)
                        ExtractResources(PatchPath, Patch)
                    Catch ex2 As Exception
                        Logger.Warn(ex2, $"{Patch} 文件重新释放失败，将尝试更换文件名重新生成")
                        PatchPath = $"{PathPure}{Patch}2.jar"
                        Try
                            ExtractResources(PatchPath, Patch)
                        Catch ex3 As Exception
                            Throw New FileNotFoundException($"释放 {Patch} 最终尝试失败", ex3)
                        End Try
                    End Try
                Else
                    Throw New FileNotFoundException($"释放 {Patch} 失败", ex)
                End If
            End Try
        End SyncLock
        Return PatchPath
    End Function

End Module
