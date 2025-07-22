Imports System.IO.Compression
Imports System.Net.Http

Public Module ModDownloadLib

    ''' <summary>
    ''' 如果 OptiFine 与 Forge 同时开始安装，就会导致 Forge 安装失败。
    ''' </summary>
    Private InstallSyncLock As New Object
    ''' <summary>
    ''' 如果 OptiFine 与 Forge 同时复制原版 Jar，就会导致复制文件时冲突。
    ''' </summary>
    Private VanillaSyncLock As New Object
    ''' <summary>
    ''' 最高的 Minecraft 大版本号，-1 代表尚未获取。
    ''' </summary>
    Public McVersionHighest As Integer = -1

#Region "Minecraft 下载"

    ''' <summary>
    ''' 下载某个 Minecraft 版本，这会创造一个单独的下载任务，失败会跳过执行并要求反馈。
    ''' 返回正在下载的任务，若跳过或失败，则返回 Nothing。
    ''' </summary>
    ''' <param name="Id">所下载的 Minecraft 的版本名。</param>
    ''' <param name="JsonUrl">Json 文件的 Mojang 官方地址。</param>
    Public Function McDownloadClient(Behaviour As NetPreDownloadBehaviour, Id As String, Optional JsonUrl As String = Nothing) As LoaderCombo(Of String)
        Try
            Dim VersionFolder As String = PathMcFolder & "versions\" & Id & "\"

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"Minecraft {Id} 下载" Then Continue For
                If Behaviour = NetPreDownloadBehaviour.ExitWhileExistsOrDownloading Then Return OngoingLoader
                Hint("该版本正在下载中！", HintType.Critical)
                Return OngoingLoader
            Next

            '已有版本检查
            If Behaviour <> NetPreDownloadBehaviour.IgnoreCheck AndAlso File.Exists(VersionFolder & Id & ".json") AndAlso File.Exists(VersionFolder & Id & ".jar") Then
                If Behaviour = NetPreDownloadBehaviour.ExitWhileExistsOrDownloading Then Return Nothing
                If MyMsgBox("版本 " & Id & " 已存在，是否重新下载？" & vbCrLf & "这会覆盖版本的 json 与 jar 文件，但不会影响版本隔离的文件。", "版本已存在", "继续", "取消") = 1 Then
                    File.Delete(VersionFolder & Id & ".jar")
                    File.Delete(VersionFolder & Id & ".json")
                Else
                    Return Nothing
                End If
            End If

            '启动
            Dim Loader As New LoaderCombo(Of String)("Minecraft " & Id & " 下载", McDownloadClientLoader(Id, JsonUrl)) With {.OnStateChanged = AddressOf McInstallState}
            Loader.Start(VersionFolder)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            Return Loader

        Catch ex As Exception
            Log(ex, "开始 Minecraft 下载失败", LogLevel.Feedback)
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 获取下载某个 Minecraft 版本的加载器列表。
    ''' 它必须安装到 PathMcFolder，但是可以自定义版本名（不过自定义的版本名不会修改 Json 中的 id 项）。
    ''' </summary>
    Private Function McDownloadClientLoader(Id As String, Optional JsonUrl As String = Nothing, Optional VersionName As String = Nothing) As List(Of LoaderBase)
        VersionName = If(VersionName, Id)
        Dim VersionFolder As String = PathMcFolder & "versions\" & VersionName & "\"

        Dim Loaders As New List(Of LoaderBase)

        '下载版本 Json 文件
        If JsonUrl Is Nothing Then
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("获取原版 json 文件下载地址",
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                Dim JsonAddress As String = DlClientListGet(Id)
                Task.Output = New List(Of NetFile) From {New NetFile(DlSourceLauncherOrMetaGet(JsonAddress), VersionFolder & VersionName & ".json")}
            End Sub) With {.ProgressWeight = 2, .Show = False})
        End If
        Loaders.Add(New LoaderDownload(McDownloadClientJsonName, New List(Of NetFile) From {
            New NetFile(DlSourceLauncherOrMetaGet(If(JsonUrl, "")), VersionFolder & VersionName & ".json", New FileChecker(CanUseExistsFile:=False, IsJson:=True))
        }) With {.ProgressWeight = 3})

        '下载支持库文件
        Dim LoadersLib As New List(Of LoaderBase)
        LoadersLib.Add(New LoaderTask(Of String, List(Of NetFile))("分析原版支持库文件（副加载器）",
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            Thread.Sleep(50) '等待 JSON 文件实际写入硬盘（#3710）
            Log("[Download] 开始分析原版支持库文件：" & VersionFolder)
            Task.Output = McLibFix(New McVersion(VersionFolder))
        End Sub) With {.ProgressWeight = 1, .Show = False})
        LoadersLib.Add(New LoaderDownload("下载原版支持库文件（副加载器）", New List(Of NetFile)) With {.ProgressWeight = 13, .Show = False})
        Loaders.Add(New LoaderCombo(Of String)(McDownloadClientLibName, LoadersLib) With {.Block = False, .ProgressWeight = 14})

        '下载资源文件
        Dim LoadersAssets As New List(Of LoaderBase)
        LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))("分析资源文件索引地址（副加载器）",
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            Try
                Dim Version As New McVersion(VersionFolder)
                Task.Output = New List(Of NetFile) From {DlClientAssetIndexGet(Version)}
            Catch ex As Exception
                Throw New Exception("分析资源文件索引地址失败", ex)
            End Try
            '顺手添加 Json 项目
            Try
                Dim VersionJson As JObject = GetJson(ReadFile(VersionFolder & VersionName & ".json"))
                VersionJson.Add("clientVersion", Id)
                WriteFile(VersionFolder & VersionName & ".json", VersionJson.ToString)
            Catch ex As Exception
                Throw New Exception("添加客户端版本失败", ex)
            End Try
        End Sub) With {.ProgressWeight = 1, .Show = False})
        LoadersAssets.Add(New LoaderDownload("下载资源文件索引（副加载器）", New List(Of NetFile)) With {.ProgressWeight = 3, .Show = False})
        LoadersAssets.Add(New LoaderTask(Of String, List(Of NetFile))("分析所需资源文件（副加载器）",
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            Task.Output = McAssetsFixList(New McVersion(VersionFolder), True, Task)
        End Sub) With {.ProgressWeight = 3, .Show = False})
        LoadersAssets.Add(New LoaderDownload("下载资源文件（副加载器）", New List(Of NetFile)) With {.ProgressWeight = 14, .Show = False})
        Loaders.Add(New LoaderCombo(Of String)("下载原版资源文件", LoadersAssets) With {.Block = False, .ProgressWeight = 21})

        Return Loaders

    End Function
    Private Const McDownloadClientLibName As String = "下载原版支持库文件"
    Private Const McDownloadClientJsonName As String = "下载原版 json 文件"

#End Region

#Region "Minecraft 下载菜单"

    Public Function McDownloadListItem(Entry As JObject, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyListItem
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
            NewItem.Info = Entry("releaseTime").Value(Of Date).ToString("yyyy'/'MM'/'dd HH':'mm")
        Else
            NewItem.Info = Entry("lore").ToString
        End If
        If Entry("url").ToString.Contains("pcl") Then NewItem.Info = "[PCL 特供下载] " & NewItem.Info
        AddHandler NewItem.Click, OnClick
        '建立菜单
        If IsSaveOnly Then
            NewItem.ContentHandler = AddressOf McDownloadSaveMenuBuild
        Else
            NewItem.ContentHandler = AddressOf McDownloadMenuBuild
        End If
        '结束
        Return NewItem
    End Function
    Private Sub McDownloadSaveMenuBuild(sender As Object, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
        AddHandler BtnInfo.Click, AddressOf McDownloadMenuLog
        Dim BtnServer As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonServer, .ToolTip = "下载服务端"}
        ToolTipService.SetPlacement(BtnServer, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnServer, 30)
        ToolTipService.SetHorizontalOffset(BtnServer, 2)
        AddHandler BtnServer.Click, AddressOf McDownloadMenuSaveServer
        sender.Buttons = {BtnServer, BtnInfo}
    End Sub
    Private Sub McDownloadMenuBuild(sender As Object, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = "另存为"}
        ToolTipService.SetPlacement(BtnSave, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnSave, 30)
        ToolTipService.SetHorizontalOffset(BtnSave, 2)
        AddHandler BtnSave.Click, AddressOf McDownloadMenuSave
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
        AddHandler BtnInfo.Click, AddressOf McDownloadMenuLog
        Dim BtnServer As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonServer, .ToolTip = "下载服务端"}
        ToolTipService.SetPlacement(BtnServer, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnServer, 30)
        ToolTipService.SetHorizontalOffset(BtnServer, 2)
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
            Dim VersionFolder As String = SelectFolder()
            If Not VersionFolder.Contains("\") Then Return
            VersionFolder = VersionFolder & Id & "\"

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"Minecraft {Id} 服务端下载" Then Continue For
                Hint("该服务端正在下载中！", HintType.Critical)
                Return
            Next

            Dim Loaders As New List(Of LoaderBase)
            '下载版本 JSON 文件
            Loaders.Add(New LoaderDownload("下载版本 JSON 文件", New List(Of NetFile) From {
                New NetFile(DlSourceLauncherOrMetaGet(JsonUrl), VersionFolder & Id & ".json", New FileChecker(CanUseExistsFile:=False, IsJson:=True))
            }) With {.ProgressWeight = 2})
            '构建服务端
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("构建服务端",
                Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                    '分析服务端 JAR 文件下载地址
                    Dim McVersion As New McVersion(VersionFolder)
                    If McVersion.JsonObject("downloads") Is Nothing OrElse McVersion.JsonObject("downloads")("server") Is Nothing OrElse McVersion.JsonObject("downloads")("server")("url") Is Nothing Then
                        File.Delete(VersionFolder & Id & ".json")
                        If Not New DirectoryInfo(VersionFolder).GetFileSystemInfos.Any() Then Directory.Delete(VersionFolder)
                        Task.Output = New List(Of NetFile)
                        Hint($"Mojang 没有给 Minecraft {Id} 提供官方服务端下载，没法下，撤退！", HintType.Critical)
                        Thread.Sleep(2000) '等玩家把上一个提示看完
                        Task.Abort()
                        Return
                    End If
                    Dim JarUrl As String = McVersion.JsonObject("downloads")("server")("url")
                    Dim Checker As New FileChecker(MinSize:=1024, ActualSize:=If(McVersion.JsonObject("downloads")("server")("size"), -1), Hash:=McVersion.JsonObject("downloads")("server")("sha1"))
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
""java"" -server -XX:+UseG1GC -Xmx4096M -Xms1024M -XX:+UseCompressedOops -jar {Id}-server.jar nogui
echo ----------------------
echo 服务端已停止。
pause"
                    WriteFile(VersionFolder & "Launch Server.bat", Bat,
                        Encoding:=If(Encoding.Default.Equals(Encoding.UTF8), Encoding.UTF8, Encoding.GetEncoding("GB18030")))
                    '删除版本 JSON
                    File.Delete(VersionFolder & Id & ".json")
                End Sub
            ) With {.ProgressWeight = 0.5, .Show = False})
            '下载服务端文件
            Loaders.Add(New LoaderDownload("下载服务端文件", New List(Of NetFile)) With {.ProgressWeight = 5})

            '启动
            Dim Loader As New LoaderCombo(Of String)("Minecraft " & Id & " 服务端下载", Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(Id)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Log(ex, "开始 Minecraft 服务端下载失败", LogLevel.Feedback)
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
            Dim VersionFolder As String = SelectFolder()
            If Not VersionFolder.Contains("\") Then Return
            VersionFolder = VersionFolder & Id & "\"

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"Minecraft {Id} 下载" Then Continue For
                Hint("该版本正在下载中！", HintType.Critical)
                Return
            Next

            Dim Loaders As New List(Of LoaderBase)
            '下载版本 JSON 文件
            Loaders.Add(New LoaderDownload("下载版本 JSON 文件", New List(Of NetFile) From {
                New NetFile(DlSourceLauncherOrMetaGet(JsonUrl), VersionFolder & Id & ".json", New FileChecker(CanUseExistsFile:=False, IsJson:=True))
            }) With {.ProgressWeight = 2})
            '获取支持库文件地址
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("分析核心 JAR 文件下载地址",
                Sub(Task) Task.Output = New List(Of NetFile) From {DlClientJarGet(New McVersion(VersionFolder), False)}
            ) With {.ProgressWeight = 0.5, .Show = False})
            '下载支持库文件
            Loaders.Add(New LoaderDownload("下载核心 JAR 文件", New List(Of NetFile)) With {.ProgressWeight = 5})

            '启动
            Dim Loader As New LoaderCombo(Of String)("Minecraft " & Id & " 下载", Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(Id)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Log(ex, "开始 Minecraft 下载失败", LogLevel.Feedback)
        End Try
    End Sub
    ''' <summary>
    ''' 显示某 Minecraft 版本的更新日志。
    ''' </summary>
    ''' <param name="VersionJson">在 version_manifest.json 中的对应项。</param>
    Public Sub McUpdateLogShow(VersionJson As JToken)
        Dim WikiName As String
        Dim Id As String = VersionJson("id").ToString.ToLower
        If Id = "3d shareware v1.34" Then
            WikiName = "3D_Shareware_v1.34"
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
            WikiName = "Java版RC2"
        ElseIf Id.StartsWithF("1.19_deep_dark_experimental_snapshot-") OrElse Id.StartsWithF("1_19_deep_dark_experimental_snapshot-") Then
            WikiName = Id.Replace("1_19", "1.19").Replace("1.19_deep_dark_experimental_snapshot-", "Java版Deep_Dark_Experimental_Snapshot_")
        ElseIf Id = "b1.9-pre6" Then
            WikiName = "Java版Beta_1.9_Prerelease_6"
        ElseIf Id.Contains("b1.9") Then
            WikiName = "Java版Beta_1.9_Prerelease"
        ElseIf VersionJson("type") = "release" OrElse VersionJson("type") = "snapshot" OrElse VersionJson("type") = "special" Then
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
            Log("[Error] 未知的版本格式：" & Id & "。", LogLevel.Feedback)
            Return
        End If
        OpenWebsite("https://zh.minecraft.wiki/w/" & WikiName.Replace("_experimental-snapshot-", "-exp"))
    End Sub

#End Region

#Region "OptiFine 下载"

    Private Sub McDownloadOptiFineSave(DownloadInfo As DlOptiFineListEntry)
        Try
            Dim Id As String = DownloadInfo.NameVersion
            Dim Target As String = SelectSaveFile("选择保存位置", DownloadInfo.NameFile, "OptiFine Jar (*.jar)|*.jar")
            If Not Target.Contains("\") Then Return

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"OptiFine {DownloadInfo.NameDisplay} 下载" Then Continue For
                Hint("该版本正在下载中！", HintType.Critical)
                Return
            Next

            Dim Loader As New LoaderCombo(Of DlOptiFineListEntry)("OptiFine " & DownloadInfo.NameDisplay & " 下载", McDownloadOptiFineSaveLoader(DownloadInfo, Target)) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(DownloadInfo)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "开始 OptiFine 下载失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub McDownloadOptiFineInstall(BaseMcFolderHome As String, Target As String, Task As LoaderTask(Of List(Of NetFile), Boolean), UseJavaWrapper As Boolean)
        '选择 Java
        Dim Java As JavaEntry
        SyncLock JavaLock
            Java = JavaSelect("已取消安装。", New Version(1, 8, 0, 0))
            If Java Is Nothing Then
                If Not JavaDownloadConfirm("Java 8 或更高版本") Then Throw New Exception("由于未找到 Java，已取消安装。")
                '开始自动下载
                Dim JavaLoader = JavaFixLoaders(17)
                Try
                    JavaLoader.Start(17, IsForceRestart:=True)
                    Do While JavaLoader.State = LoadState.Loading AndAlso Not Task.IsAborted
                        Thread.Sleep(10)
                    Loop
                Finally
                    JavaLoader.Abort() '确保取消时中止 Java 下载
                End Try
                '检查下载结果
                Java = JavaSelect("已取消安装。", New Version(1, 8, 0, 0))
                If Task.IsAborted Then Return
                If Java Is Nothing Then Throw New Exception("由于未找到 Java，已取消安装。")
            End If
        End SyncLock
        '添加 Java Wrapper 作为主 Jar
        Dim Arguments As String
        If UseJavaWrapper AndAlso Not Setup.Get("LaunchAdvanceDisableJLW") Then
            Arguments = $"-Doolloo.jlw.tmpdir=""{PathPure.TrimEnd("\")}"" -Duser.home=""{BaseMcFolderHome.TrimEnd("\")}"" -cp ""{Target}"" -jar ""{ExtractJavaWrapper()}"" optifine.Installer"
        Else
            Arguments = $"-Duser.home=""{BaseMcFolderHome.TrimEnd("\")}"" -cp ""{Target}"" optifine.Installer"
        End If
        If Java.VersionCode >= 9 Then Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " & Arguments
        '开始启动
        SyncLock InstallSyncLock
            Dim Info = New ProcessStartInfo With {
                .FileName = Java.PathJavaw,
                .Arguments = Arguments,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardError = True,
                .RedirectStandardOutput = True,
                .WorkingDirectory = ShortenPath(BaseMcFolderHome)
            }
            If Info.EnvironmentVariables.ContainsKey("appdata") Then
                Info.EnvironmentVariables("appdata") = BaseMcFolderHome
            Else
                Info.EnvironmentVariables.Add("appdata", BaseMcFolderHome)
            End If
            Log("[Download] 开始安装 OptiFine：" & Target)
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
                                If ModeDebug Then Log("[Installer] " & LastResult)
                                TotalLength += 1
                                Task.Progress += 0.9 / 7000
                            End If
                        Catch ex As ObjectDisposedException
                        Catch ex As Exception
                            Log(ex, "读取 OptiFine 安装器信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Aborted AndAlso Not process.HasExited Then
                                Log("[Installer] 由于任务取消，已中止 OptiFine 安装")
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
                                If ModeDebug Then Log("[Installer] " & LastResult)
                                TotalLength += 1
                                Task.Progress += 0.9 / 7000
                            End If
                        Catch ex As ObjectDisposedException
                        Catch ex As Exception
                            Log(ex, "读取 OptiFine 安装器错误信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Aborted AndAlso Not process.HasExited Then
                                Log("[Installer] 由于任务取消，已中止 OptiFine 安装")
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
                    If TotalLength < 1000 OrElse LastResult.Contains("at ") Then Throw New Exception("安装器运行出错，末行为 " & LastResult)
                End Using
            End Using
        End SyncLock
    End Sub

    ''' <summary>
    ''' 获取下载某个 OptiFine 版本的加载器列表。
    ''' </summary>
    Private Function McDownloadOptiFineLoader(DownloadInfo As DlOptiFineListEntry, Optional McFolder As String = Nothing, Optional ClientDownloadLoader As LoaderCombo(Of String) = Nothing, Optional ClientFolder As String = Nothing, Optional FixLibrary As Boolean = True) As List(Of LoaderBase)

        '参数初始化
        McFolder = If(McFolder, PathMcFolder)
        Dim IsCustomFolder As Boolean = McFolder <> PathMcFolder
        Dim Id As String = DownloadInfo.NameVersion
        Dim VersionFolder As String = McFolder & "versions\" & Id & "\"
        Dim IsNewVersion As Boolean = DownloadInfo.Inherit.Contains("w") OrElse Val(DownloadInfo.Inherit.Split(".")(1)) >= 14
        Dim Target As String = If(IsNewVersion,
            $"{RequestTaskTempFolder()}OptiFine.jar",
            $"{McFolder}libraries\optifine\OptiFine\{DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "")}\{DownloadInfo.NameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", "")}")
        Dim Loaders As New List(Of LoaderBase)

        '获取下载地址
        Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("获取 OptiFine 主文件下载地址",
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            '启动依赖版本的下载
            If ClientDownloadLoader Is Nothing Then
                If IsCustomFolder Then Throw New Exception("如果没有指定原版下载器，则不能指定 MC 安装文件夹")
                ClientDownloadLoader = McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, DownloadInfo.Inherit)
            End If
            Task.Progress = 0.1
            Dim Sources As New List(Of String)
            'BMCLAPI 源
            Dim BmclapiInherit As String = DownloadInfo.Inherit
            If BmclapiInherit = "1.8" OrElse BmclapiInherit = "1.9" Then BmclapiInherit &= ".0" '#4281
            If DownloadInfo.IsPreview Then
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U_" & DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit & " ", "").Replace(" ", "/"))
            Else
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U/" & DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit & " ", ""))
            End If
            '官方源
            Dim PageData As String
            Try
                PageData = NetGetCodeByClient("https://optifine.net/adloadx?f=" & DownloadInfo.NameFile, New UTF8Encoding(False), 15000, "text/html", True)
                Task.Progress = 0.8
                Sources.Add("https://optifine.net/" & RegexSearch(PageData, "downloadx\?f=[^""']+")(0))
                Log("[Download] OptiFine " & DownloadInfo.NameDisplay & " 官方下载地址：" & Sources.Last)
            Catch ex As Exception
                Log(ex, "获取 OptiFine " & DownloadInfo.NameDisplay & " 官方下载地址失败")
            End Try
            '构造文件请求
            Task.Output = New List(Of NetFile) From {New NetFile(Sources.ToArray, Target, New FileChecker(MinSize:=300 * 1024))}
        End Sub) With {.ProgressWeight = 8})
        Loaders.Add(New LoaderDownload("下载 OptiFine 主文件", New List(Of NetFile)) With {.ProgressWeight = 8})
        Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)("等待原版下载",
        Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
            '等待原版文件下载完成
            If ClientDownloadLoader Is Nothing Then Return
            Dim TargetLoaders As List(Of LoaderBase) =
               ClientDownloadLoader.GetLoaderList.Where(Function(l) l.Name = McDownloadClientLibName OrElse l.Name = McDownloadClientJsonName).
               Where(Function(l) l.State <> LoadState.Finished).ToList
            If TargetLoaders.Any Then Log("[Download] OptiFine 安装正在等待原版文件下载完成")
            Do While TargetLoaders.Any AndAlso Not Task.IsAborted
                TargetLoaders = TargetLoaders.Where(Function(l) l.State <> LoadState.Finished).ToList
                Thread.Sleep(50)
            Loop
            If Task.IsAborted Then Return
            '拷贝原版文件
            If Not IsCustomFolder Then Return
            SyncLock VanillaSyncLock
                Dim ClientName As String = GetFolderNameFromPath(ClientFolder)
                Directory.CreateDirectory(McFolder & "versions\" & DownloadInfo.Inherit)
                If Not File.Exists(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".json") Then
                    CopyFile($"{ClientFolder}{ClientName}.json", $"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.json")
                End If
                If Not File.Exists(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar") Then
                    CopyFile($"{ClientFolder}{ClientName}.jar", $"{McFolder}versions\{DownloadInfo.Inherit}\{DownloadInfo.Inherit}.jar")
                End If
            End SyncLock
        End Sub) With {.ProgressWeight = 0.1, .Show = False})

        '安装（新旧方式均需要原版 Jar 和 Json）
        If IsNewVersion Then
            Log("[Download] 检测为新版 OptiFine：" & DownloadInfo.Inherit)
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)("安装 OptiFine（方式 A）",
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
                Dim BaseMcFolderHome As String = RequestTaskTempFolder()
                Dim BaseMcFolder As String = BaseMcFolderHome & ".minecraft\"
                Try
                    '准备安装环境
                    If Directory.Exists(BaseMcFolder & "versions\" & DownloadInfo.Inherit) Then
                        DeleteDirectory(BaseMcFolder & "versions\" & DownloadInfo.Inherit)
                    End If
                    Directory.CreateDirectory(BaseMcFolder & "versions\" & DownloadInfo.Inherit & "\")
                    McFolderLauncherProfilesJsonCreate(BaseMcFolder)
                    CopyFile(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".json",
                              BaseMcFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".json")
                    CopyFile(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar",
                              BaseMcFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar")
                    Task.Progress = 0.06
                    '进行安装
                    Dim UseJavaWrapper As Boolean = True
Retry:
                    Try
                        McDownloadOptiFineInstall(BaseMcFolderHome, Target, Task, UseJavaWrapper)
                    Catch ex As Exception
                        If UseJavaWrapper Then
                            Log(ex, "使用 JavaWrapper 安装 OptiFine 失败，将不使用 JavaWrapper 并重试")
                            UseJavaWrapper = False
                            GoTo Retry
                        Else
                            Throw New Exception("运行 OptiFine 安装器失败", ex)
                        End If
                    End Try
                    Task.Progress = 0.96
                    '复制文件
                    File.Delete(BaseMcFolder & "launcher_profiles.json")
                    CopyDirectory(BaseMcFolder, McFolder)
                    Task.Progress = 0.98
                    '清理文件
                    File.Delete(Target)
                    DeleteDirectory(BaseMcFolderHome)
                Catch ex As Exception
                    Throw New Exception("安装 OptiFine（方式 A）失败", ex)
                End Try
            End Sub) With {.ProgressWeight = 8})
        Else
            Log("[Download] 检测为旧版 OptiFine：" & DownloadInfo.Inherit)
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)("安装 OptiFine（方式 B）",
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
                Try
                    '新建版本文件夹
                    Directory.CreateDirectory(VersionFolder)
                    Task.Progress = 0.1
                    '复制 Jar 文件
                    If File.Exists(VersionFolder & Id & ".jar") Then File.Delete(VersionFolder & Id & ".jar")
                    CopyFile(McFolder & "versions\" & DownloadInfo.Inherit & "\" & DownloadInfo.Inherit & ".jar", VersionFolder & Id & ".jar")
                    Task.Progress = 0.7
                    '建立 Json 文件
                    Dim InheritVersion As New McVersion(McFolder & "versions\" & DownloadInfo.Inherit)
                    Dim Json As String = "{
    ""id"": """ & Id & """,
    ""inheritsFrom"": """ & DownloadInfo.Inherit & """,
    ""time"": """ & If(DownloadInfo.ReleaseTime = "", InheritVersion.ReleaseTime.ToString("yyyy'-'MM'-'dd"), DownloadInfo.ReleaseTime.Replace("/", "-")) & "T23:33:33+08:00"",
    ""releaseTime"": """ & If(DownloadInfo.ReleaseTime = "", InheritVersion.ReleaseTime.ToString("yyyy'-'MM'-'dd"), DownloadInfo.ReleaseTime.Replace("/", "-")) & "T23:33:33+08:00"",
    ""type"": ""release"",
    ""libraries"": [
        {""name"": ""optifine:OptiFine:" & DownloadInfo.NameFile.Replace("OptiFine_", "").Replace(".jar", "").Replace("preview_", "") & """},
        {""name"": ""net.minecraft:launchwrapper:1.12""}
    ],
    ""mainClass"": ""net.minecraft.launchwrapper.Launch"","
                    Task.Progress = 0.8
                    If InheritVersion.IsOldJson Then
                        '输出旧版 Json 格式
                        Json += "
    ""minimumLauncherVersion"": 18,
    ""minecraftArguments"": """ & InheritVersion.JsonObject("minecraftArguments").ToString & "  --tweakClass optifine.OptiFineTweaker""
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
                    WriteFile(VersionFolder & Id & ".json", Json)
                Catch ex As Exception
                    Throw New Exception("安装 OptiFine（方式 B）失败", ex)
                End Try
            End Sub) With {.ProgressWeight = 1})
        End If

        '下载支持库
        If FixLibrary Then
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("分析 OptiFine 支持库文件",
                Sub(Task) Task.Output = McLibFix(New McVersion(VersionFolder))) With {.ProgressWeight = 1, .Show = False})
            Loaders.Add(New LoaderDownload("下载 OptiFine 支持库文件", New List(Of NetFile)) With {.ProgressWeight = 4})
        End If

        Return Loaders
    End Function
    ''' <summary>
    ''' 获取保存某个 OptiFine 版本的加载器列表。
    ''' </summary>
    Private Function McDownloadOptiFineSaveLoader(DownloadInfo As DlOptiFineListEntry, TargetFolder As String) As List(Of LoaderBase)
        Dim Loaders As New List(Of LoaderBase)
        '获取下载地址
        Loaders.Add(New LoaderTask(Of DlOptiFineListEntry, List(Of NetFile))("获取 OptiFine 下载地址",
        Sub(Task As LoaderTask(Of DlOptiFineListEntry, List(Of NetFile)))
            Dim Sources As New List(Of String)
            'BMCLAPI 源
            Dim BmclapiInherit As String = DownloadInfo.Inherit
            If BmclapiInherit = "1.8" OrElse BmclapiInherit = "1.9" Then BmclapiInherit &= ".0" '#4281
            If DownloadInfo.IsPreview Then
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U_" & DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit & " ", "").Replace(" ", "/"))
            Else
                Sources.Add("https://bmclapi2.bangbang93.com/optifine/" & BmclapiInherit & "/HD_U/" & DownloadInfo.NameDisplay.Replace(DownloadInfo.Inherit & " ", ""))
            End If
            '官方源
            Dim PageData As String
            Try
                PageData = NetGetCodeByClient("https://optifine.net/adloadx?f=" & DownloadInfo.NameFile, New UTF8Encoding(False), 15000, "text/html", True)
                Task.Progress = 0.8
                Sources.Add("https://optifine.net/" & RegexSearch(PageData, "downloadx\?f=[^""']+")(0))
                Log("[Download] OptiFine " & DownloadInfo.NameDisplay & " 官方下载地址：" & Sources.Last)
            Catch ex As Exception
                Log(ex, "获取 OptiFine " & DownloadInfo.NameDisplay & " 官方下载地址失败")
            End Try
            Task.Progress = 0.9
            '构造文件请求
            Task.Output = New List(Of NetFile) From {New NetFile(Sources.ToArray, TargetFolder, New FileChecker(MinSize:=64 * 1024))}
        End Sub) With {.ProgressWeight = 6})
        '下载
        Loaders.Add(New LoaderDownload("下载 OptiFine 主文件", New List(Of NetFile)) With {.ProgressWeight = 10, .Block = True})
        Return Loaders
    End Function

#End Region

#Region "OptiFine 下载菜单"

    Public Function OptiFineDownloadListItem(Entry As DlOptiFineListEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyListItem
        '建立控件
        Dim NewItem As New MyListItem With {
            .Title = Entry.NameDisplay, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
            .Info = If(Entry.IsPreview, "测试版", "正式版") &
                    If(Entry.ReleaseTime = "", "", "，发布于 " & Entry.ReleaseTime) &
                    If(Entry.RequiredForgeVersion Is Nothing, "，不兼容 Forge", If(Entry.RequiredForgeVersion = "", "", "，兼容 Forge " & Entry.RequiredForgeVersion)),
            .Logo = PathImage & "Blocks/GrassPath.png"
        }
        AddHandler NewItem.Click, OnClick
        '建立菜单
        If IsSaveOnly Then
            NewItem.ContentHandler = AddressOf OptiFineSaveContMenuBuild
        Else
            NewItem.ContentHandler = AddressOf OptiFineContMenuBuild
        End If
        '结束
        Return NewItem
    End Function
    Private Sub OptiFineSaveContMenuBuild(sender As Object, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
        AddHandler BtnInfo.Click, AddressOf OptiFineLog_Click
        sender.Buttons = {BtnInfo}
    End Sub
    Private Sub OptiFineContMenuBuild(sender As Object, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = "另存为"}
        ToolTipService.SetPlacement(BtnSave, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnSave, 30)
        ToolTipService.SetHorizontalOffset(BtnSave, 2)
        AddHandler BtnSave.Click, AddressOf OptiFineSave_Click
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
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
        OpenWebsite("https://optifine.net/changelog?f=" & Version.NameFile)
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

    Public Sub McDownloadLiteLoader(DownloadInfo As DlLiteLoaderListEntry)
        Try
            Dim Id As String = DownloadInfo.Inherit
            Dim Target As String = PathTemp & "Download\" & Id & "-Liteloader.jar"
            Dim VersionName As String = DownloadInfo.Inherit & "-LiteLoader"
            Dim VersionFolder As String = PathMcFolder & "versions\" & VersionName & "\"

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"LiteLoader {Id} 下载" Then Continue For
                Hint("该版本正在下载中！", HintType.Critical)
                Return
            Next

            '已有版本检查
            If File.Exists(VersionFolder & VersionName & ".json") Then
                If MyMsgBox("版本 " & VersionName & " 已存在，是否重新下载？" & vbCrLf & "这会覆盖版本的 json 和 jar 文件，但不会影响版本隔离的文件。", "版本已存在", "继续", "取消") = 1 Then
                    File.Delete(VersionFolder & VersionName & ".jar")
                    File.Delete(VersionFolder & VersionName & ".json")
                Else
                    Return
                End If
            End If

            '启动
            Dim Loader As New LoaderCombo(Of String)("LiteLoader " & Id & " 下载", McDownloadLiteLoaderLoader(DownloadInfo)) With {.OnStateChanged = AddressOf McInstallState}
            Loader.Start(VersionFolder)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "开始 LiteLoader 下载失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub McDownloadLiteLoaderSave(DownloadInfo As DlLiteLoaderListEntry)
        Try
            Dim Id As String = DownloadInfo.Inherit
            Dim Target As String = SelectSaveFile("选择保存位置", DownloadInfo.FileName.Replace("-SNAPSHOT", ""), "LiteLoader 安装器 (*.jar)|*.jar")
            If Not Target.Contains("\") Then Return

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"LiteLoader {Id} 下载" Then Continue For
                Hint("该版本正在下载中！", HintType.Critical)
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
                        Throw New NotSupportedException("未知的 Minecraft 版本（" & DownloadInfo.Inherit & "）")
                End Select
            Else
                '官方源
                Address.Add("http://jenkins.liteloader.com/job/LiteLoaderInstaller%20" & DownloadInfo.Inherit & "/lastSuccessfulBuild/artifact/" & If(DownloadInfo.Inherit = "1.8", "ant/dist/", "build/libs/") & DownloadInfo.FileName)
            End If
            Loaders.Add(New LoaderDownload("下载主文件", New List(Of NetFile) From {New NetFile(Address.ToArray, Target, New FileChecker(MinSize:=1024 * 1024))}) With {.ProgressWeight = 15})
            '启动
            Dim Loader As New LoaderCombo(Of DlLiteLoaderListEntry)("LiteLoader " & Id & " 安装器下载", Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(DownloadInfo)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "开始 LiteLoader 安装器下载失败", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 获取下载某个 LiteLoader 版本的加载器列表。
    ''' </summary>
    Private Function McDownloadLiteLoaderLoader(DownloadInfo As DlLiteLoaderListEntry, Optional McFolder As String = Nothing, Optional ClientDownloadLoader As LoaderCombo(Of String) = Nothing, Optional FixLibrary As Boolean = True) As List(Of LoaderBase)

        '参数初始化
        McFolder = If(McFolder, PathMcFolder)
        Dim IsCustomFolder As Boolean = McFolder <> PathMcFolder
        Dim Id As String = DownloadInfo.Inherit
        Dim Target As String = PathTemp & "Download\" & Id & "-Liteloader.jar"
        Dim VersionName As String = DownloadInfo.Inherit & "-LiteLoader"
        Dim VersionFolder As String = McFolder & "versions\" & VersionName & "\"
        Dim Loaders As New List(Of LoaderBase)

        '启动依赖版本的下载
        If ClientDownloadLoader Is Nothing Then
            Loaders.Add(New LoaderTask(Of String, String)("启动 LiteLoader 依赖版本下载",
            Sub()
                If IsCustomFolder Then Throw New Exception("如果没有指定原版下载器，则不能指定 MC 安装文件夹")
                ClientDownloadLoader = McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, DownloadInfo.Inherit)
            End Sub) With {.ProgressWeight = 0.2, .Show = False, .Block = False})
        End If
        '安装
        Loaders.Add(New LoaderTask(Of String, String)("安装 LiteLoader",
        Sub(Task As LoaderTask(Of String, String))
            Try
                '新建版本文件夹
                Directory.CreateDirectory(VersionFolder)
                '构造版本 Json
                Dim VersionJson As New JObject
                VersionJson.Add("id", VersionName)
                VersionJson.Add("time", Date.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", Globalization.CultureInfo.CurrentCulture))
                VersionJson.Add("releaseTime", Date.ParseExact(DownloadInfo.ReleaseTime, "yyyy/MM/dd HH:mm", Globalization.CultureInfo.CurrentCulture))
                VersionJson.Add("type", "release")
                VersionJson.Add("arguments", GetJson("{""game"":[""--tweakClass"",""" & DownloadInfo.JsonToken("tweakClass").ToString & """]}"))
                VersionJson.Add("libraries", DownloadInfo.JsonToken("libraries"))
                CType(VersionJson("libraries"), JContainer).Add(GetJson("{""name"": ""com.mumfrey:liteloader:" & DownloadInfo.JsonToken("version").ToString & """,""url"": ""https://dl.liteloader.com/versions/""}"))
                VersionJson.Add("mainClass", "net.minecraft.launchwrapper.Launch")
                VersionJson.Add("minimumLauncherVersion", 18)
                VersionJson.Add("inheritsFrom", DownloadInfo.Inherit)
                VersionJson.Add("jar", DownloadInfo.Inherit)
                '输出 Json 文件
                WriteFile(VersionFolder & VersionName & ".json", VersionJson.ToString)
            Catch ex As Exception
                Throw New Exception("安装新 LiteLoader 版本失败", ex)
            End Try
        End Sub) With {.ProgressWeight = 1})
        '下载支持库
        If FixLibrary Then
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("分析 LiteLoader 支持库文件",
                Sub(Task) Task.Output = McLibFix(New McVersion(VersionFolder))) With {.ProgressWeight = 1, .Show = False})
            Loaders.Add(New LoaderDownload("下载 LiteLoader 支持库文件", New List(Of NetFile)) With {.ProgressWeight = 6})
        End If

        Return Loaders
    End Function

#End Region

#Region "LiteLoader 下载菜单"

    Public Function LiteLoaderDownloadListItem(Entry As DlLiteLoaderListEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyListItem
        '建立控件
        Dim NewItem As New MyListItem With {
            .Title = Entry.Inherit, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
            .Info = If(Entry.IsPreview, "测试版", "稳定版") & If(Entry.ReleaseTime = "", "", "，发布于 " & Entry.ReleaseTime),
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
    End Function
    Private Sub LiteLoaderSaveContMenuBuild(sender As MyListItem, e As EventArgs)
        If sender.Tag.IsLegacy Then
            sender.Buttons = {}
        Else
            Dim BtnList As New MyIconButton With {.Logo = Logo.IconButtonList, .ToolTip = "查看全部版本", .Tag = sender}
            ToolTipService.SetPlacement(BtnList, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnList, 30)
            ToolTipService.SetHorizontalOffset(BtnList, 2)
            AddHandler BtnList.Click, AddressOf LiteLoaderAll_Click
            sender.Buttons = {BtnList}
        End If
    End Sub
    Private Sub LiteLoaderContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = "保存安装器", .Tag = sender}
        ToolTipService.SetPlacement(BtnSave, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnSave, 30)
        ToolTipService.SetHorizontalOffset(BtnSave, 2)
        AddHandler BtnSave.Click, AddressOf LiteLoaderSave_Click
        If sender.Tag.IsLegacy Then
            sender.Buttons = {BtnSave}
        Else
            Dim BtnList As New MyIconButton With {.Logo = Logo.IconButtonList, .ToolTip = "查看全部版本", .Tag = sender}
            ToolTipService.SetPlacement(BtnList, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnList, 30)
            ToolTipService.SetHorizontalOffset(BtnList, 2)
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
            Dim Target As String = SelectSaveFile("选择保存位置", $"{Info.LoaderName}-{Info.Inherit}-{Info.VersionName}.{Info.FileExtension}",
                                            $"{Info.LoaderName} 安装器 (*.{Info.FileExtension})|*.{Info.FileExtension}")
            Dim DisplayName As String = $"{Info.LoaderName} {Info.Inherit} - {Info.VersionName}"
            If Not Target.Contains("\") Then Return

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"{DisplayName} 下载" Then Continue For
                Hint("该版本正在下载中！", HintType.Critical)
                Return
            Next

            '获取下载地址
            Dim Files As New List(Of NetFile)
            If Info.IsNeoForge Then
                'NeoForge
                Dim Neo As DlNeoForgeListEntry = Info
                Dim Url As String = Neo.UrlBase & "-installer.jar"
                Files.Add(New NetFile({
                    Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url
                }, Target, New FileChecker(MinSize:=64 * 1024)))
            Else
                'Forge
                Dim Forge As DlForgeVersionEntry = Info
                Files.Add(New NetFile({
                    $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}",
                    $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{Forge.Inherit}-{Forge.FileVersion}/forge-{Forge.Inherit}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}"
                }, Target, New FileChecker(MinSize:=64 * 1024, Hash:=Forge.Hash)))
            End If

            '构造加载器
            Dim Loaders As New List(Of LoaderBase)
            Loaders.Add(New LoaderDownload("下载主文件", Files) With {.ProgressWeight = 6})

            '启动
            Dim Loader = New LoaderCombo(Of DlForgelikeEntry)(DisplayName & " 下载", Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(Info)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, $"开始 {Info.LoaderName} 安装器下载失败", LogLevel.Feedback)
        End Try
    End Sub

    Private Sub ForgelikeInjector(Target As String, Task As LoaderTask(Of Boolean, Boolean), McFolder As String, UseJavaWrapper As Boolean, IsNeoForge As Boolean)
        '选择 Java
        Dim Java As JavaEntry
        SyncLock JavaLock
            Java = JavaSelect("已取消安装。", New Version(1, 8, 0, 60))
            If Java Is Nothing Then
                If Not JavaDownloadConfirm("Java 8 或更高版本") Then Throw New Exception("由于未找到 Java，已取消安装。")
                '开始自动下载
                Dim JavaLoader = JavaFixLoaders(17)
                Try
                    JavaLoader.Start(17, IsForceRestart:=True)
                    Do While JavaLoader.State = LoadState.Loading AndAlso Not Task.IsAborted
                        Thread.Sleep(10)
                    Loop
                Finally
                    JavaLoader.Abort() '确保取消时中止 Java 下载
                End Try
                '检查下载结果
                Java = JavaSelect("已取消安装。", New Version(1, 8, 0, 60))
                If Task.IsAborted Then Return
                If Java Is Nothing Then Throw New Exception("由于未找到 Java，已取消安装。")
            End If
        End SyncLock
        '添加 Java Wrapper 作为主 Jar
        Dim Arguments As String
        If UseJavaWrapper AndAlso Not Setup.Get("LaunchAdvanceDisableJLW") Then
            Arguments = $"-Doolloo.jlw.tmpdir=""{PathPure.TrimEnd("\")}"" -cp ""{PathTemp}Cache\forge_installer.jar;{Target}"" -jar ""{ExtractJavaWrapper()}"" com.bangbang93.ForgeInstaller ""{McFolder}"
        Else
            Arguments = $"-cp ""{PathTemp}Cache\forge_installer.jar;{Target}"" com.bangbang93.ForgeInstaller ""{McFolder}"
        End If
        If Java.VersionCode >= 9 Then Arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " & Arguments
        '开始启动
        SyncLock InstallSyncLock
            Dim Info = New ProcessStartInfo With {
                .FileName = Java.PathJavaw,
                .Arguments = Arguments,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardError = True,
                .RedirectStandardOutput = True
            }
            Dim LoaderName As String = If(IsNeoForge, "NeoForge", "Forge")
            Log($"[Download] 开始安装 {LoaderName}：" & Arguments)
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
                            Log(ex, $"读取 {LoaderName} 安装器信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Aborted AndAlso Not process.HasExited Then
                                Log($"[Installer] 由于任务取消，已中止 {LoaderName} 安装")
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
                            Log(ex, $"读取 {LoaderName} 安装器错误信息失败")
                        End Try
                        Try
                            If Task.State = LoadState.Aborted AndAlso Not process.HasExited Then
                                Log($"[Installer] 由于任务取消，已中止 {LoaderName} 安装")
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
                    Log(Join(LastResults, vbCrLf))
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
                Log("[Installer] " & Content)
                Task.Progress = 0.07
            Case "Downloading libraries"
                Log("[Installer] " & Content)
                Task.Progress = 0.08
            Case "  File exists: Checksum validated."
                If ModeDebug Then Log("[Installer] " & Content)
                Task.Progress += 0.003
            Case "Building Processors"
                Task.Progress = 0.18
            Case "Task: DOWNLOAD_MOJMAPS" 'B
                Task.Progress = 0.2
            Case "Task: MERGE_MAPPING" 'B
                Task.Progress = 0.3
            Case "Splitting: "
                Task.Progress = 0.35
            Case "Parameter Annotations" 'B
                Task.Progress = 0.4
            Case "Processing Complete" 'B
                Task.Progress = 0.5
            Case "log: null" 'new
                Task.Progress = 0.5
            Case "Sorting" 'new
                Task.Progress = 0.65
            Case "Remapping final jar" 'A
                Task.Progress = 0.72
            Case "Remapping jar... 50%" 'A
                Task.Progress = 0.76
            Case "Remapping jar... 100%" 'A
                Task.Progress = 0.81
            Case "Injecting profile"
                Task.Progress = 0.91
            Case Else
                If ModeDebug Then Log("[Installer] " & Content)
                Return
        End Select
        Log("[Installer] " & Content)
    End Sub

    ''' <summary>
    ''' 获取下载某个 Forgelike 版本的加载器列表。
    ''' </summary>
    Private Function McDownloadForgelikeLoader(IsNeoForge As Boolean, LoaderVersion As String, TargetVersion As String, Inherit As String, Optional Info As DlForgelikeEntry = Nothing, Optional McFolder As String = Nothing, Optional ClientDownloadLoader As LoaderCombo(Of String) = Nothing, Optional ClientFolder As String = Nothing) As List(Of LoaderBase)

        '参数初始化
        McFolder = If(McFolder, PathMcFolder)
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
        Dim IsCustomFolder As Boolean = McFolder <> PathMcFolder
        Dim InstallerAddress As String = RequestTaskTempFolder() & "forge_installer.jar"
        Dim VersionFolder As String = $"{McFolder}versions\{TargetVersion}\"
        Dim DisplayName As String = $"{LoaderName} {Inherit} - {LoaderVersion}"
        Dim Loaders As New List(Of LoaderBase)
        Dim LibVersionFolder As String = $"{PathMcFolder}versions\{TargetVersion}\" '作为 Lib 文件目标的版本文件夹

        '获取 Forge 下载信息
        If Info Is Nothing Then
            Loaders.Add(New LoaderTask(Of String, String)($"获取 {LoaderName} 详细信息",
            Sub(Task As LoaderTask(Of String, String))
                '获取 Forge 对应 MC 版本列表
                Dim ForgeLoader = New LoaderTask(Of String, List(Of DlForgeVersionEntry))("McDownloadForgeLoader " & Inherit, AddressOf DlForgeVersionMain)
                ForgeLoader.WaitForExit(Inherit)
                Task.Progress = 0.8
                '查找对应版本
                For Each ForgeVersion In ForgeLoader.Output
                    If VersionSortInteger(ForgeVersion.Version.ToString, LoaderVersion) = 0 Then
                        Info = ForgeVersion
                        Return
                    End If
                Next
                Throw New Exception($"未能找到 {LoaderName} " & Inherit & "-" & LoaderVersion & " 的详细信息！")
            End Sub) With {.ProgressWeight = 3})
        End If
        '下载 Forgelike 主文件
        Loaders.Add(New LoaderTask(Of String, List(Of NetFile))($"准备下载 {LoaderName}",
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            '启动依赖版本的下载
            If ClientDownloadLoader Is Nothing Then
                If IsCustomFolder Then Throw New Exception("如果没有指定原版下载器，则不能指定 MC 安装文件夹")
                ClientDownloadLoader = McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, Inherit)
            End If
            '添加主文件下载
            Dim Files As New List(Of NetFile)
            If Info.IsNeoForge Then
                'NeoForge
                Dim Neo As DlNeoForgeListEntry = Info
                Dim Url As String = Neo.UrlBase & "-installer.jar"
                Files.Add(New NetFile({
                    Url.Replace("maven.neoforged.net/releases", "bmclapi2.bangbang93.com/maven"), Url
                }, InstallerAddress, New FileChecker(MinSize:=64 * 1024)))
            Else
                'Forge
                Dim Forge As DlForgeVersionEntry = Info
                Dim FileName As String =
                    $"{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}/forge-{Forge.Inherit.Replace("-", "_")}-{Forge.FileVersion}-{Forge.Category}.{Forge.FileExtension}"
                Files.Add(New NetFile({
                    $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{FileName}",
                    $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{FileName}"
                }, InstallerAddress, New FileChecker(MinSize:=64 * 1024, Hash:=Forge.Hash)))
            End If
            Task.Output = Files
        End Sub) With {.ProgressWeight = 0.5, .Show = False})
        Loaders.Add(New LoaderDownload($"下载 {LoaderName} 主文件", New List(Of NetFile)) With {.ProgressWeight = 9})

        '安装（仅在新版安装时需要原版 Jar）
        If IsNeoForge OrElse LoaderVersion.BeforeFirst(".") >= 20 Then
            Log($"[Download] 检测为{If(IsNeoForge, " Neo", "新版 ")}Forge：" & LoaderVersion)
            Dim Libs As List(Of McLibToken) = Nothing
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))($"分析 {LoaderName} 支持库文件",
            Sub(Task As LoaderTask(Of String, List(Of NetFile)))
                Task.Output = New List(Of NetFile)
                Dim Installer As ZipArchive = Nothing
                Try
                    '解压并获取、合并两个 Json 的信息
                    Installer = New ZipArchive(New FileStream(InstallerAddress, FileMode.Open))
                    Task.Progress = 0.2
                    Dim Json As JObject = GetJson(ReadFile(Installer.GetEntry("install_profile.json").Open))
                    Dim Json2 As JObject = GetJson(ReadFile(Installer.GetEntry("version.json").Open))
                    Json.Merge(Json2)
                    '获取 Lib 下载信息
                    Libs = McLibListGetWithJson(Json, True)
                    '添加 Mappings 下载信息
                    If Json("data") IsNot Nothing AndAlso Json("data")("MOJMAPS") IsNot Nothing Then
                        '下载原版 Json 文件
                        Task.Progress = 0.4
                        Dim RawJson As JObject = GetJson(NetGetCodeByLoader(DlSourceLauncherOrMetaGet(DlClientListGet(Inherit)), IsJson:=True))
                        '[net.minecraft:client:1.17.1-20210706.113038:mappings@txt] 或 @tsrg]
                        Dim OriginalName As String = Json("data")("MOJMAPS")("client").ToString.Trim("[]".ToCharArray()).BeforeFirst("@")
                        Dim Address = McLibGet(OriginalName).Replace(".jar", "-mappings." & Json("data")("MOJMAPS")("client").ToString.Trim("[]".ToCharArray()).Split("@")(1))
                        Dim ClientMappings As JToken = RawJson("downloads")("client_mappings")
                        Libs.Add(New McLibToken With {
                                 .IsNatives = False, .LocalPath = Address, .OriginalName = OriginalName,
                                 .Url = ClientMappings("url"), .Size = ClientMappings("size"), .SHA1 = ClientMappings("sha1")})
                        Log($"[Download] 需要下载 Mappings：{ClientMappings("url")} (SHA1: {ClientMappings("sha1")})")
                    End If
                    Task.Progress = 0.8
                    '去除其中的原始 Forgelike 项
                    For i = 0 To Libs.Count - 1
                        If Libs(i).LocalPath.EndsWithF($"{LoaderName.ToLower}-{Inherit}-{LoaderVersion}.jar") OrElse
                           Libs(i).LocalPath.EndsWithF($"{LoaderName.ToLower}-{Inherit}-{LoaderVersion}-client.jar") Then
                            Log($"[Download] 已从待下载 {LoaderName} 支持库中移除：" & Libs(i).LocalPath, LogLevel.Debug)
                            Libs.RemoveAt(i)
                            Exit For
                        End If
                    Next
                    Task.Output = McLibFixFromLibToken(Libs, PathMcFolder)
                Catch ex As Exception
                    Throw New Exception($"获取{If(IsNeoForge, " Neo", "新版 ")}Forge 支持库列表失败", ex)
                Finally
                    '释放文件
                    If Installer IsNot Nothing Then Installer.Dispose()
                End Try
            End Sub) With {.ProgressWeight = 2})
            Loaders.Add(New LoaderDownload($"下载 {LoaderName} 支持库文件", New List(Of NetFile)) With {.ProgressWeight = 12})
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)($"获取 {LoaderName} 支持库文件",
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
#Region "Forgelike 文件"
                If IsCustomFolder Then
                    For Each LibFile As McLibToken In Libs
                        Dim RealPath As String = LibFile.LocalPath.Replace(PathMcFolder, McFolder)
                        If Not File.Exists(RealPath) Then
                            Directory.CreateDirectory(IO.Path.GetDirectoryName(RealPath))
                            CopyFile(LibFile.LocalPath, RealPath)
                        End If
                        If ModeDebug Then Log($"[Download] 复制的 {LoaderName} 支持库文件：" & LibFile.LocalPath)
                    Next
                End If
#End Region
#Region "原版文件"
                '等待原版文件下载完成
                If ClientDownloadLoader Is Nothing Then Return
                Dim TargetLoaders As List(Of LoaderBase) =
                    ClientDownloadLoader.GetLoaderList.Where(Function(l) l.Name = McDownloadClientLibName OrElse l.Name = McDownloadClientJsonName).
                    Where(Function(l) l.State <> LoadState.Finished).ToList()
                If TargetLoaders.Any Then Log($"[Download] {LoaderName} 安装正在等待原版文件下载完成")
                Do While TargetLoaders.Any AndAlso Not Task.IsAborted
                    TargetLoaders = TargetLoaders.Where(Function(l) l.State <> LoadState.Finished).ToList
                    Thread.Sleep(50)
                Loop
                If Task.IsAborted Then Return
                '拷贝原版文件
                If Not IsCustomFolder Then Return
                SyncLock VanillaSyncLock
                    Dim ClientName As String = GetFolderNameFromPath(ClientFolder)
                    Directory.CreateDirectory(McFolder & "versions\" & Inherit)
                    If Not File.Exists(McFolder & "versions\" & Inherit & "\" & Inherit & ".json") Then
                        CopyFile(ClientFolder & ClientName & ".json", McFolder & "versions\" & Inherit & "\" & Inherit & ".json")
                    End If
                    If Not File.Exists(McFolder & "versions\" & Inherit & "\" & Inherit & ".jar") Then
                        CopyFile(ClientFolder & ClientName & ".jar", McFolder & "versions\" & Inherit & "\" & Inherit & ".jar")
                    End If
                End SyncLock
#End Region
            End Sub) With {.ProgressWeight = 0.1, .Show = False})
            Loaders.Add(New LoaderTask(Of Boolean, Boolean)(If(IsNeoForge, "安装 NeoForge", "安装 Forge（方式 A）"),
            Sub(Task As LoaderTask(Of Boolean, Boolean))
                Dim Installer As ZipArchive = Nothing
                Try
                    Log($"[Download] 开始进行 Forgelike 安装：" & InstallerAddress)
                    '记录当前文件夹列表（在新建目标文件夹之前）
                    Dim OldList = New DirectoryInfo(McFolder & "versions\").EnumerateDirectories.Select(Function(i) i.FullName).ToList()
                    '解压并获取信息
                    Installer = New ZipArchive(New FileStream(InstallerAddress, FileMode.Open))
                    Dim Json As JObject = GetJson(ReadFile(Installer.GetEntry("install_profile.json").Open))
                    '新建目标版本文件夹
                    Directory.CreateDirectory(VersionFolder)
                    Task.Progress = 0.04
                    '释放 launcher_installer.json
                    McFolderLauncherProfilesJsonCreate(McFolder)
                    Task.Progress = 0.05
                    '运行 Forge 安装器
                    Dim UseJavaWrapper As Boolean = True
Retry:
                    Try
                        '释放 Forge 注入器
                        WriteFile(PathTemp & "Cache\forge_installer.jar", GetResources("ForgeInstaller"))
                        Task.Progress = 0.06
                        '运行注入器
                        ForgelikeInjector(InstallerAddress, Task, McFolder, UseJavaWrapper, IsNeoForge)
                        Task.Progress = 0.97
                    Catch ex As Exception
                        If UseJavaWrapper Then
                            Log(ex, $"使用 JavaWrapper 安装 {LoaderName} 失败，将不使用 JavaWrapper 并重试")
                            UseJavaWrapper = False
                            GoTo Retry
                        Else
                            Throw New Exception($"运行 {LoaderName} 安装器失败", ex)
                        End If
                    End Try
                    '拷贝新增的版本 Json
                    Dim DeltaList = New DirectoryInfo(McFolder & "versions\").EnumerateDirectories.
                        SkipWhile(Function(i) OldList.Contains(i.FullName)).ToList()
                    If DeltaList.Count > 1 Then
                        '它可能和 OptiFine 安装同时运行，导致增加的文件不止一个（这导致了 #151）
                        '也可能是因为 Forge 安装器的 Bug，生成了一个名字错误的文件夹，所以需要检查文件夹是否为空
                        DeltaList = DeltaList.Where(Function(l) l.Name.ContainsF("forge", True) AndAlso l.EnumerateFiles.Any).ToList
                    End If
                    If DeltaList.Count = 1 Then
                        '如果没有新增文件夹，那么预测的文件夹名就是正确的
                        '如果只新增 1 个文件夹，那么拷贝 json 文件
                        Dim JsonFile As FileInfo = DeltaList(0).EnumerateFiles.First()
                        WriteFile(VersionFolder & TargetVersion & ".json", ReadFile(JsonFile.FullName))
                        Log($"[Download] 已拷贝新增的版本 JSON 文件：{JsonFile.FullName} -> {VersionFolder}{TargetVersion}.json")
                    ElseIf DeltaList.Count > 1 Then
                        '新增了多个文件夹
                        Log($"[Download] 有多个疑似的新增版本，无法确定：{DeltaList.Select(Function(d) d.Name).Join(";")}")
                    Else
                        '没有新增文件夹
                        Log("[Download] 未找到新增的版本文件夹")
                    End If
                Catch ex As Exception
                    Throw New Exception($"安装新 {LoaderName} 版本失败", ex)
                Finally
                    '清理文件
                    Try
                        If Installer IsNot Nothing Then Installer.Dispose()
                        If File.Exists(InstallerAddress) Then File.Delete(InstallerAddress)
                    Catch ex As Exception
                        Log(ex, $"安装 {LoaderName} 清理文件时出错")
                    End Try
                End Try
            End Sub) With {.ProgressWeight = 10})
        Else
            Log("[Download] 检测为非新版 Forge：" & LoaderVersion)
            Loaders.Add(New LoaderTask(Of List(Of NetFile), Boolean)($"安装 {LoaderName}（方式 B）",
            Sub(Task As LoaderTask(Of List(Of NetFile), Boolean))
                Dim Installer As ZipArchive = Nothing
                Try
                    '解压并获取信息
                    Installer = New ZipArchive(New FileStream(InstallerAddress, FileMode.Open))
                    Task.Progress = 0.2
                    Dim Json As JObject = GetJson(ReadFile(Installer.GetEntry("install_profile.json").Open))
                    Task.Progress = 0.4
                    '新建版本文件夹
                    Directory.CreateDirectory(VersionFolder)
                    Task.Progress = 0.5
                    If Json("install") Is Nothing Then
                        '中版：Legacy 方式 1
                        Log("[Download] 开始进行 Forge 安装，Legacy 方式 1：" & InstallerAddress)
                        '建立 Json 文件
                        Dim JsonVersion As JObject = GetJson(ReadFile(Installer.GetEntry(Json("json").ToString.TrimStart("/")).Open))
                        JsonVersion("id") = TargetVersion
                        WriteFile(VersionFolder & TargetVersion & ".json", JsonVersion.ToString)
                        Task.Progress = 0.6
                        '解压支持库文件
                        Installer.Dispose()
                        ExtractFile(InstallerAddress, InstallerAddress & "_unrar\")
                        CopyDirectory(InstallerAddress & "_unrar\maven\", McFolder & "libraries\")
                        DeleteDirectory(InstallerAddress & "_unrar\")
                    Else
                        '旧版：Legacy 方式 2
                        Log("[Download] 开始进行 Forge 安装，Legacy 方式 2：" & InstallerAddress)
                        '解压 Jar 文件
                        Dim JarAddress As String = McLibGet(Json("install")("path"), CustomMcFolder:=McFolder)
                        If File.Exists(JarAddress) Then File.Delete(JarAddress)
                        WriteFile(JarAddress, Installer.GetEntry(Json("install")("filePath")).Open)
                        Task.Progress = 0.9
                        '建立 Json 文件
                        Json("versionInfo")("id") = TargetVersion
                        If Json("versionInfo")("inheritsFrom") Is Nothing Then CType(Json("versionInfo"), JObject).Add("inheritsFrom", Inherit)
                        WriteFile(VersionFolder & TargetVersion & ".json", Json("versionInfo").ToString)
                    End If
                Catch ex As Exception
                    Throw New Exception("非新版方式安装 Forge 失败", ex)
                Finally
                    Try
                        '清理文件
                        If Installer IsNot Nothing Then Installer.Dispose()
                        If File.Exists(InstallerAddress) Then File.Delete(InstallerAddress)
                        If Directory.Exists(InstallerAddress & "_unrar\") Then DeleteDirectory(InstallerAddress & "_unrar\")
                    Catch ex As Exception
                        Log(ex, "非新版方式安装 Forge 清理文件时出错")
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
        If Entries.Count = 1 Then Return
        '获取推荐版本与最新版本
        Dim FreshVersion As DlForgeVersionEntry = Nothing
        If Entries.Any Then
            FreshVersion = Entries(0)
        Else
            Log("[System] 未找到可用的 Forge 版本", LogLevel.Debug)
        End If
        Dim RecommendedVersion As DlForgeVersionEntry = Nothing
        For Each Entry In Entries
            If Entry.IsRecommended Then RecommendedVersion = Entry
        Next
        '若推荐版本与最新版本为同一版本，则仅显示推荐版本
        If FreshVersion IsNot Nothing AndAlso FreshVersion Is RecommendedVersion Then FreshVersion = Nothing
        '显示各个版本
        If RecommendedVersion IsNot Nothing Then
            Dim Recommended = ForgeDownloadListItem(RecommendedVersion, OnClick, IsSaveOnly)
            Recommended.Info = "推荐版" & If(Recommended.Info = "", "", "，" & Recommended.Info)
            Stack.Children.Add(Recommended)
        End If
        If FreshVersion IsNot Nothing Then
            Dim Fresh = ForgeDownloadListItem(FreshVersion, OnClick, IsSaveOnly)
            Fresh.Info = "最新版" & If(Fresh.Info = "", "", "，" & Fresh.Info)
            Stack.Children.Add(Fresh)
        End If
        '添加间隔
        Stack.Children.Add(New TextBlock With {.Text = "全部版本 (" & Entries.Count & ")", .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(6, 13, 0, 4)})
    End Sub
    Public Function ForgeDownloadListItem(Entry As DlForgeVersionEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyListItem
        '建立控件
        Dim NewItem As New MyListItem With {
            .Title = Entry.VersionName, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
            .Info = {If(Entry.ReleaseTime = "", "", "发布于 " & Entry.ReleaseTime), If(ModeDebug, "种类：" & Entry.Category, "")}.
                Where(Function(d) d <> "").Join("，"),
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
    End Function
    Private Sub ForgeContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = "另存为"}
        ToolTipService.SetPlacement(BtnSave, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnSave, 30)
        ToolTipService.SetHorizontalOffset(BtnSave, 2)
        AddHandler BtnSave.Click, AddressOf ForgeSave_Click
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
        AddHandler BtnInfo.Click, AddressOf ForgeLog_Click
        sender.Buttons = {BtnSave, BtnInfo}
    End Sub
    Private Sub ForgeSaveContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
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
        RunInNewThread(Sub()
                           Try
                               Log("[Download] 刷新 Forge 推荐版本缓存开始")
                               Dim Result As String = NetGetCodeByLoader("https://bmclapi2.bangbang93.com/forge/promos")
                               If Result.Length < 1000 Then Throw New Exception("获取的结果过短（" & Result & "）")
                               Dim ResultJson As JContainer = GetJson(Result)
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
                               Dim CacheJson As String = "{" & Join(RecommendedList, ",") & "}"
                               WriteFile(PathTemp & "Cache\ForgeRecommendedList.json", CacheJson)
                               Log("[Download] 刷新 Forge 推荐版本缓存成功")
                           Catch ex As Exception
                               Log(ex, "刷新 Forge 推荐版本缓存失败")
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
            Dim List As String = ReadFile(PathTemp & "Cache\ForgeRecommendedList.json")
            If List Is Nothing OrElse List = "" Then
                Log("[Download] 没有 Forge 推荐版本缓存文件")
                Return Nothing
            End If
            Dim Json As JObject = GetJson(List)
            If Json Is Nothing OrElse (Not If(McVersion, "null").Contains(".")) OrElse Not Json.ContainsKey(McVersion) Then Return Nothing
            Return If(Json(McVersion), "").ToString
        Catch ex As Exception
            Log(ex, "获取 Forge 推荐版本失败（" & If(McVersion, "null") & "）", LogLevel.Feedback)
            Return Nothing
        End Try
    End Function

#End Region

#Region "NeoForge 下载菜单"

    Public Sub NeoForgeDownloadListItemPreload(Stack As StackPanel, Entries As List(Of DlNeoForgeListEntry), OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean)
        '如果只有一个版本，则不特别列出
        If Entries.Count = 1 Then Return
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
            Log("[System] 未找到可用的 NeoForge 版本", LogLevel.Debug)
        End If
        '显示各个版本
        If FreshStableVersion IsNot Nothing Then
            Dim Fresh = NeoForgeDownloadListItem(FreshStableVersion, OnClick, IsSaveOnly)
            Fresh.Info = If(Fresh.Info = "", "最新稳定版", "最新" & Fresh.Info)
            Stack.Children.Add(Fresh)
        End If
        If FreshBetaVersion IsNot Nothing Then
            Dim Fresh = NeoForgeDownloadListItem(FreshBetaVersion, OnClick, IsSaveOnly)
            Fresh.Info = If(Fresh.Info = "", "最新测试版", "最新" & Fresh.Info)
            Stack.Children.Add(Fresh)
        End If
        '添加间隔
        Stack.Children.Add(New TextBlock With {.Text = "全部版本 (" & Entries.Count & ")", .HorizontalAlignment = HorizontalAlignment.Left, .Margin = New Thickness(6, 13, 0, 4)})
    End Sub
    Public Function NeoForgeDownloadListItem(Info As DlNeoForgeListEntry, OnClick As MyListItem.ClickEventHandler, IsSaveOnly As Boolean) As MyListItem
        '建立控件
        Dim NewItem As New MyListItem With {
            .Title = Info.VersionName, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Info,
            .Info = If(Info.IsBeta, "测试版", "稳定版"),
            .Logo = PathImage & "Blocks/NeoForge.png"
        }
        AddHandler NewItem.Click, OnClick
        '建立菜单
        If IsSaveOnly Then
            NewItem.ContentHandler = AddressOf NeoForgeSaveContMenuBuild
        Else
            NewItem.ContentHandler = AddressOf NeoForgeContMenuBuild
        End If
        '结束
        Return NewItem
    End Function
    Private Sub NeoForgeContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnSave As New MyIconButton With {.Logo = Logo.IconButtonSave, .ToolTip = "另存为"}
        ToolTipService.SetPlacement(BtnSave, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnSave, 30)
        ToolTipService.SetHorizontalOffset(BtnSave, 2)
        AddHandler BtnSave.Click, AddressOf NeoForgeSave_Click
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
        AddHandler BtnInfo.Click, AddressOf NeoForgeLog_Click
        sender.Buttons = {BtnSave, BtnInfo}
    End Sub
    Private Sub NeoForgeSaveContMenuBuild(sender As MyListItem, e As EventArgs)
        Dim BtnInfo As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonInfo, .ToolTip = "更新日志"}
        ToolTipService.SetPlacement(BtnInfo, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnInfo, 30)
        ToolTipService.SetHorizontalOffset(BtnInfo, 2)
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
            Dim FileName As String = GetFileNameFromPath(Url)
            Dim Version As String = GetFileNameFromPath(DownloadInfo("version").ToString)
            Dim Target As String = SelectSaveFile("选择保存位置", FileName, "Fabric 安装器 (*.jar)|*.jar")
            If Not Target.Contains("\") Then Return

            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> $"Fabric {Version} 安装器下载" Then Continue For
                Hint("该版本正在下载中！", HintType.Critical)
                Return
            Next

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            '下载
            'BMCLAPI 不支持 Fabric Installer 下载
            Dim Address As New List(Of String)
            Address.Add(Url)
            Loaders.Add(New LoaderDownload("下载主文件", New List(Of NetFile) From {New NetFile(Address.ToArray, Target, New FileChecker(MinSize:=1024 * 64))}) With {.ProgressWeight = 15})
            '启动
            Dim Loader As New LoaderCombo(Of JObject)("Fabric " & Version & " 安装器下载", Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start(DownloadInfo)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "开始 Fabric 安装器下载失败", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 获取下载某个 Fabric 版本的加载器列表。
    ''' </summary>
    Private Function McDownloadFabricLoader(FabricVersion As String, MinecraftName As String, Optional McFolder As String = Nothing, Optional FixLibrary As Boolean = True) As List(Of LoaderBase)

        '参数初始化
        McFolder = If(McFolder, PathMcFolder)
        Dim IsCustomFolder As Boolean = McFolder <> PathMcFolder
        Dim Id As String = "fabric-loader-" & FabricVersion & "-" & MinecraftName
        Dim VersionFolder As String = McFolder & "versions\" & Id & "\"
        Dim Loaders As New List(Of LoaderBase)

        '下载 Json
        MinecraftName = MinecraftName.Replace("∞", "infinite") '放在 ID 后面避免影响版本文件夹名称
        Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("获取 Fabric 主文件下载地址",
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            '启动依赖版本的下载
            If FixLibrary Then
                McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, MinecraftName)
            End If
            Task.Progress = 0.5
            '构造文件请求
            Task.Output = New List(Of NetFile) From {New NetFile({
                "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/" & MinecraftName & "/" & FabricVersion & "/profile/json",
                "https://meta.fabricmc.net/v2/versions/loader/" & MinecraftName & "/" & FabricVersion & "/profile/json"
            }, VersionFolder & Id & ".json", New FileChecker(IsJson:=True))}
        End Sub) With {.ProgressWeight = 0.5})
        Loaders.Add(New LoaderDownload("下载 Fabric 主文件", New List(Of NetFile)) With {.ProgressWeight = 2.5})

        '下载支持库
        If FixLibrary Then
            Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("分析 Fabric 支持库文件",
                Sub(Task) Task.Output = McLibFix(New McVersion(VersionFolder))) With {.ProgressWeight = 1, .Show = False})
            Loaders.Add(New LoaderDownload("下载 Fabric 支持库文件", New List(Of NetFile)) With {.ProgressWeight = 8})
        End If

        Return Loaders
    End Function

#End Region

#Region "Fabric 下载菜单"

    Public Function FabricDownloadListItem(Entry As JObject, OnClick As MyListItem.ClickEventHandler) As MyListItem
        '建立控件
        Dim NewItem As New MyListItem With {
            .Title = Entry("version").ToString.Replace("+build", ""), .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
            .Info = If(Entry("stable").ToObject(Of Boolean), "稳定版", "测试版"),
            .Logo = PathImage & "Blocks/Fabric.png"
        }
        AddHandler NewItem.Click, OnClick
        '结束
        Return NewItem
    End Function
    Public Function FabricApiDownloadListItem(Entry As CompFile, OnClick As MyListItem.ClickEventHandler) As MyListItem
        '建立控件
        Dim NewItem As New MyListItem With {
            .Title = Entry.DisplayName.Split("]")(1).Replace("Fabric API ", "").Replace(" build ", ".").BeforeFirst("+").Trim, .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
            .Info = Entry.StatusDescription & "，发布于 " & Entry.ReleaseDate.ToString("yyyy'/'MM'/'dd HH':'mm"),
            .Logo = PathImage & "Blocks/Fabric.png"
        }
        AddHandler NewItem.Click, OnClick
        '结束
        Return NewItem
    End Function
    Public Function OptiFabricDownloadListItem(Entry As CompFile, OnClick As MyListItem.ClickEventHandler) As MyListItem
        '建立控件
        Dim NewItem As New MyListItem With {
            .Title = Entry.DisplayName.ToLower.Replace("optifabric-", "").Replace(".jar", "").Trim.TrimStart("v"), .SnapsToDevicePixels = True, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry,
            .Info = Entry.StatusDescription & "，发布于 " & Entry.ReleaseDate.ToString("yyyy'/'MM'/'dd HH':'mm"),
            .Logo = PathImage & "Blocks/OptiFabric.png"
        }
        AddHandler NewItem.Click, OnClick
        '结束
        Return NewItem
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
        Public TargetVersionName As String
        ''' <summary>
        ''' 必填。安装目标文件夹。
        ''' </summary>
        Public TargetVersionFolder As String

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
        Public FabricApi As CompFile = Nothing

        ''' <summary>
        ''' 欲下载的 OptiFabric 信息。
        ''' </summary>
        Public OptiFabric As CompFile = Nothing

        ''' <summary>
        ''' 欲下载的 LiteLoader 详细信息。
        ''' </summary>
        Public LiteLoaderEntry As DlLiteLoaderListEntry = Nothing

    End Class

    ''' <summary>
    ''' 在加载器状态改变后显示一条提示。
    ''' 不会进行任何其他操作。
    ''' </summary>
    Public Sub LoaderStateChangedHintOnly(Loader)
        Select Case Loader.State
            Case LoadState.Finished
                Hint(Loader.Name & "成功！", HintType.Finish)
            Case LoadState.Failed
                Hint(Loader.Name & "失败：" & GetExceptionSummary(Loader.Error), HintType.Critical)
            Case LoadState.Aborted
                Hint(Loader.Name & "已取消！", HintType.Info)
        End Select
    End Sub
    ''' <summary>
    ''' 安装加载器状态改变后进行提示和重载文件夹列表的方法。
    ''' </summary>
    Public Sub McInstallState(Loader)
        Select Case Loader.State
            Case LoadState.Finished
                WriteIni(PathMcFolder & "PCL.ini", "VersionCache", "") '清空缓存（合并安装会先生成文件夹，这会在刷新时误判为可以使用缓存）
                Hint(Loader.Name & "成功！", HintType.Finish)
            Case LoadState.Failed
                Hint(Loader.Name & "失败：" & GetExceptionSummary(Loader.Error), HintType.Critical)
            Case LoadState.Aborted
                Hint(Loader.Name & "已取消！", HintType.Info)
            Case LoadState.Loading
                Return '不重新加载版本列表
        End Select
        McInstallFailedClearFolder(Loader)
        LoaderFolderRun(McVersionListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub
    Public Sub McInstallFailedClearFolder(Loader)
        Try
            Thread.Sleep(1000) '防止存在尚未完全释放的文件，导致清理失败（例如整合包安装）
            If Loader.State = LoadState.Failed OrElse Loader.State = LoadState.Aborted Then
                '删除版本文件夹
                If Directory.Exists(Loader.Input & "saves\") OrElse Directory.Exists(Loader.Input & "versions\") Then
                    Log("[Download] 由于版本已被独立启动，不清理版本文件夹：" & Loader.Input, LogLevel.Developer)
                Else
                    Log("[Download] 由于下载失败或取消，清理版本文件夹：" & Loader.Input, LogLevel.Developer)
                    DeleteDirectory(Loader.Input)
                End If
            End If
        Catch ex As Exception
            Log(ex, "下载失败或取消后清理版本文件夹失败")
        End Try
    End Sub

    ''' <summary>
    ''' 进行合并安装。返回是否已经开始安装（例如如果没有安装 Java 则会进行提示并返回 False）
    ''' </summary>
    Public Function McInstall(Request As McInstallRequest) As Boolean
        Try
            Dim SubLoaders = McInstallLoader(Request)
            If SubLoaders Is Nothing Then Return False
            Dim Loader As New LoaderCombo(Of String)(Request.TargetVersionName & " 安装", SubLoaders) With {.OnStateChanged = AddressOf McInstallState}

            '启动
            Loader.Start(Request.TargetVersionFolder)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            Return True

        Catch ex As CancelledException
            Return False
        Catch ex As Exception
            Log(ex, "开始合并安装失败", LogLevel.Feedback)
            Return False
        End Try
    End Function
    ''' <summary>
    ''' 获取合并安装加载器列表，并进行前期的缓存清理与 Java 检查工作。
    ''' </summary>
    ''' <exception cref="CancelledException" />
    Public Function McInstallLoader(Request As McInstallRequest) As List(Of LoaderBase)
        '获取缓存目录（安装 Mod 加载器的文件夹不能包含空格）
        Dim TempMcFolder As String = RequestTaskTempFolder(Request.OptiFineEntry IsNot Nothing OrElse Request.ForgeEntry IsNot Nothing OrElse Request.NeoForgeEntry IsNot Nothing)

        '获取参数
        Dim VersionFolder As String = PathMcFolder & "versions\" & Request.TargetVersionName & "\"
        If Directory.Exists(TempMcFolder) Then DeleteDirectory(TempMcFolder)
        Dim OptiFineFolder As String = Nothing
        If Request.OptiFineVersion IsNot Nothing Then
            If Request.OptiFineVersion.Contains("_HD_U_") Then Request.OptiFineVersion = "HD_U_" & Request.OptiFineVersion.AfterLast("_HD_U_") '#735
            Request.OptiFineEntry = New DlOptiFineListEntry With {
                .NameDisplay = Request.MinecraftName & " " & Request.OptiFineVersion.Replace("HD_U_", "").Replace("_", "").Replace("pre", " pre"),
                .Inherit = Request.MinecraftName,
                .IsPreview = Request.OptiFineVersion.ContainsF("pre", True),
                .NameVersion = Request.MinecraftName & "-OptiFine_" & Request.OptiFineVersion,
                .NameFile = If(Request.OptiFineVersion.ContainsF("pre", True), "preview_", "") &
                    "OptiFine_" & Request.MinecraftName & "_" & Request.OptiFineVersion & ".jar"
            }
        End If
        If Request.OptiFineEntry IsNot Nothing Then OptiFineFolder = TempMcFolder & "versions\" & Request.OptiFineEntry.NameVersion
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
            Log("[Download] OptiFine 将作为 Mod 进行下载")
            OptiFineFolder = ModsTempFolder
        End If

        '记录日志
        If OptiFineFolder IsNot Nothing Then Log("[Download] OptiFine 缓存：" & OptiFineFolder)
        If ForgeFolder IsNot Nothing Then Log("[Download] Forge 缓存：" & ForgeFolder)
        If NeoForgeFolder IsNot Nothing Then Log("[Download] NeoForge 缓存：" & NeoForgeFolder)
        If FabricFolder IsNot Nothing Then Log("[Download] Fabric 缓存：" & FabricFolder)
        If LiteLoaderFolder IsNot Nothing Then Log("[Download] LiteLoader 缓存：" & LiteLoaderFolder)
        Log("[Download] 对应的原版版本：" & Request.MinecraftName)

        '重复版本检查
        If File.Exists($"{VersionFolder}{Request.TargetVersionName}.json") Then
            Hint("版本 " & Request.TargetVersionName & " 已经存在！", HintType.Critical)
            Throw New CancelledException
        End If

        Dim LoaderList As New List(Of LoaderBase)
        '添加忽略标识
        LoaderList.Add(New LoaderTask(Of Integer, Integer)("添加忽略标识", Sub() WriteFile(VersionFolder & ".pclignore", "用于临时地在 PCL 的版本列表中屏蔽此版本。")) With {.Show = False, .Block = False})
        'Fabric API
        If Request.FabricApi IsNot Nothing Then
            LoaderList.Add(New LoaderDownload("下载 Fabric API", New List(Of NetFile) From {Request.FabricApi.ToNetFile(ModsTempFolder)}) With {.ProgressWeight = 3, .Block = False})
        End If
        'OptiFabric
        If Request.OptiFabric IsNot Nothing Then
            LoaderList.Add(New LoaderDownload("下载 OptiFabric", New List(Of NetFile) From {Request.OptiFabric.ToNetFile(ModsTempFolder)}) With {.ProgressWeight = 3, .Block = False})
        End If
        '原版
        Dim ClientLoader = New LoaderCombo(Of String)("下载原版 " & Request.MinecraftName, McDownloadClientLoader(Request.MinecraftName, Request.MinecraftJson, Request.TargetVersionName)) With {.Show = False, .ProgressWeight = 39,
            .Block = Request.ForgeVersion Is Nothing AndAlso Request.NeoForgeVersion Is Nothing AndAlso Request.OptiFineEntry Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing}
        LoaderList.Add(ClientLoader)
        'OptiFine
        If Request.OptiFineEntry IsNot Nothing Then
            If OptiFineAsMod Then
                LoaderList.Add(New LoaderCombo(Of String)("下载 OptiFine " & Request.OptiFineEntry.NameDisplay, McDownloadOptiFineSaveLoader(Request.OptiFineEntry, OptiFineFolder & Request.OptiFineEntry.NameFile)) With {.Show = False, .ProgressWeight = 16,
                    .Block = Request.ForgeVersion Is Nothing AndAlso Request.NeoForgeVersion Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing})
            Else
                LoaderList.Add(New LoaderCombo(Of String)("下载 OptiFine " & Request.OptiFineEntry.NameDisplay, McDownloadOptiFineLoader(Request.OptiFineEntry, TempMcFolder, ClientLoader, Request.TargetVersionFolder, False)) With {.Show = False, .ProgressWeight = 24,
                    .Block = Request.ForgeVersion Is Nothing AndAlso Request.NeoForgeVersion Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing})
            End If
        End If
        'Forge
        If Request.ForgeVersion IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)("下载 Forge " & Request.ForgeVersion, McDownloadForgelikeLoader(False, Request.ForgeVersion, "forge-" & Request.ForgeVersion, Request.MinecraftName, Request.ForgeEntry, TempMcFolder, ClientLoader, Request.TargetVersionFolder)) With {.Show = False, .ProgressWeight = 25,
                .Block = Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing AndAlso Request.NeoForgeEntry Is Nothing})
        End If
        'NeoForge
        If Request.NeoForgeVersion IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)("下载 NeoForge " & Request.NeoForgeVersion, McDownloadForgelikeLoader(True, Request.NeoForgeVersion, "neoforge-" & Request.NeoForgeVersion, Request.MinecraftName, Request.NeoForgeEntry, TempMcFolder, ClientLoader, Request.TargetVersionFolder)) With {.Show = False, .ProgressWeight = 25,
                .Block = Request.ForgeEntry Is Nothing AndAlso Request.FabricVersion Is Nothing AndAlso Request.LiteLoaderEntry Is Nothing})
        End If
        'LiteLoader
        If Request.LiteLoaderEntry IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)("下载 LiteLoader " & Request.MinecraftName, McDownloadLiteLoaderLoader(Request.LiteLoaderEntry, TempMcFolder, ClientLoader, False)) With {.Show = False, .ProgressWeight = 1,
                .Block = Request.FabricVersion Is Nothing})
        End If
        'Fabric
        If Request.FabricVersion IsNot Nothing Then
            LoaderList.Add(New LoaderCombo(Of String)("下载 Fabric " & Request.FabricVersion, McDownloadFabricLoader(Request.FabricVersion, Request.MinecraftName, TempMcFolder, False)) With {.Show = False, .ProgressWeight = 2,
                .Block = True})
        End If
        '合并安装
        LoaderList.Add(New LoaderTask(Of String, String)("安装游戏",
        Sub(Task As LoaderTask(Of String, String))
            '合并 JSON
            MergeJson(VersionFolder, VersionFolder, OptiFineFolder, OptiFineAsMod, ForgeFolder, Request.ForgeVersion, NeoForgeFolder, Request.NeoForgeVersion, FabricFolder, LiteLoaderFolder)
            Task.Progress = 0.2
            '迁移文件
            If Directory.Exists(TempMcFolder & "libraries") Then CopyDirectory(TempMcFolder & "libraries", PathMcFolder & "libraries")
            Task.Progress = 0.8
            '创建 Mod 和资源包文件夹
            Dim ModsFolder = New McVersion(VersionFolder).PathIndie & "mods\" '版本隔离信息在此时被决定
            If Directory.Exists(ModsTempFolder) Then
                CopyDirectory(ModsTempFolder, ModsFolder)
            ElseIf Modable Then
                Directory.CreateDirectory(ModsFolder)
                Log("[Download] 自动创建 Mod 文件夹：" & ModsFolder)
            End If
            Dim ResourcepacksFolder = New McVersion(VersionFolder).PathIndie & "resourcepacks\"
            Directory.CreateDirectory(ResourcepacksFolder)
            Log("[Download] 自动创建资源包文件夹：" & ResourcepacksFolder)
        End Sub) With {.ProgressWeight = 2, .Block = True})
        '补全文件
        If Request.OptiFineEntry IsNot Nothing OrElse (Request.ForgeVersion IsNot Nothing AndAlso Request.ForgeVersion.BeforeFirst(".") >= 20) OrElse Request.NeoForgeVersion IsNot Nothing OrElse Request.FabricVersion IsNot Nothing OrElse Request.LiteLoaderEntry IsNot Nothing Then
            Dim LoadersLib As New List(Of LoaderBase)
            LoadersLib.Add(New LoaderTask(Of String, List(Of NetFile))("分析游戏支持库文件（副加载器）", Sub(Task) Task.Output = McLibFix(New McVersion(VersionFolder))) With {.ProgressWeight = 1, .Show = False})
            LoadersLib.Add(New LoaderDownload("下载游戏支持库文件（副加载器）", New List(Of NetFile)) With {.ProgressWeight = 7, .Show = False})
            LoaderList.Add(New LoaderCombo(Of String)("下载游戏支持库文件", LoadersLib) With {.ProgressWeight = 8})
        End If
        '删除忽略标识
        LoaderList.Add(New LoaderTask(Of Integer, Integer)("删除忽略标识", Sub() File.Delete(VersionFolder & ".pclignore")) With {.Show = False})
        '总加载器
        Return LoaderList
    End Function

    ''' <summary>
    ''' 将多个版本 JSON 进行合并，如果目标已存在则直接覆盖。失败会抛出异常。
    ''' </summary>
    Private Sub MergeJson(OutputFolder As String, MinecraftFolder As String, Optional OptiFineFolder As String = Nothing, Optional OptiFineAsMod As Boolean = False, Optional ForgeFolder As String = Nothing, Optional ForgeVersion As String = Nothing, Optional NeoForgeFolder As String = Nothing, Optional NeoForgeVersion As String = Nothing, Optional FabricFolder As String = Nothing, Optional LiteLoaderFolder As String = Nothing)
        Log("[Download] 开始进行版本合并，输出：" & OutputFolder & "，Minecraft：" & MinecraftFolder &
            If(OptiFineFolder IsNot Nothing, "，OptiFine：" & OptiFineFolder, "") &
            If(ForgeFolder IsNot Nothing, "，Forge：" & ForgeFolder, "") &
            If(NeoForgeFolder IsNot Nothing, "，NeoForge：" & NeoForgeFolder, "") &
            If(LiteLoaderFolder IsNot Nothing, "，LiteLoader：" & LiteLoaderFolder, "") &
            If(FabricFolder IsNot Nothing, "，Fabric：" & FabricFolder, ""))
        Directory.CreateDirectory(OutputFolder)

        Dim HasOptiFine As Boolean = OptiFineFolder IsNot Nothing AndAlso Not OptiFineAsMod, HasForge As Boolean = ForgeFolder IsNot Nothing, HasNeoForge As Boolean = NeoForgeFolder IsNot Nothing, HasLiteLoader As Boolean = LiteLoaderFolder IsNot Nothing, HasFabric As Boolean = FabricFolder IsNot Nothing
        Dim OutputName As String, MinecraftName As String, OptiFineName As String, ForgeName As String, NeoForgeName As String, LiteLoaderName As String, FabricName As String
        Dim OutputJsonPath As String, MinecraftJsonPath As String, OptiFineJsonPath As String = Nothing, ForgeJsonPath As String = Nothing, NeoForgeJsonPath As String = Nothing, LiteLoaderJsonPath As String = Nothing, FabricJsonPath As String = Nothing
        Dim OutputJar As String, MinecraftJar As String
#Region "初始化路径信息"
        If Not OutputFolder.EndsWithF("\") Then OutputFolder += "\"
        OutputName = GetFolderNameFromPath(OutputFolder)
        OutputJsonPath = OutputFolder & OutputName & ".json"
        OutputJar = OutputFolder & OutputName & ".jar"

        If Not MinecraftFolder.EndsWithF("\") Then MinecraftFolder += "\"
        MinecraftName = GetFolderNameFromPath(MinecraftFolder)
        MinecraftJsonPath = MinecraftFolder & MinecraftName & ".json"
        MinecraftJar = MinecraftFolder & MinecraftName & ".jar"

        If HasOptiFine Then
            If Not OptiFineFolder.EndsWithF("\") Then OptiFineFolder += "\"
            OptiFineName = GetFolderNameFromPath(OptiFineFolder)
            OptiFineJsonPath = OptiFineFolder & OptiFineName & ".json"
        End If

        If HasForge Then
            If Not ForgeFolder.EndsWithF("\") Then ForgeFolder += "\"
            ForgeName = GetFolderNameFromPath(ForgeFolder)
            ForgeJsonPath = ForgeFolder & ForgeName & ".json"
        End If

        If HasNeoForge Then
            If Not NeoForgeFolder.EndsWithF("\") Then NeoForgeFolder += "\"
            NeoForgeName = GetFolderNameFromPath(NeoForgeFolder)
            NeoForgeJsonPath = NeoForgeFolder & NeoForgeName & ".json"
        End If

        If HasLiteLoader Then
            If Not LiteLoaderFolder.EndsWithF("\") Then LiteLoaderFolder += "\"
            LiteLoaderName = GetFolderNameFromPath(LiteLoaderFolder)
            LiteLoaderJsonPath = LiteLoaderFolder & LiteLoaderName & ".json"
        End If

        If HasFabric Then
            If Not FabricFolder.EndsWithF("\") Then FabricFolder += "\"
            FabricName = GetFolderNameFromPath(FabricFolder)
            FabricJsonPath = FabricFolder & FabricName & ".json"
        End If
#End Region

        Dim OutputJson As JObject, MinecraftJson As JObject, OptiFineJson As JObject = Nothing, ForgeJson As JObject = Nothing, NeoForgeJson As JObject = Nothing, LiteLoaderJson As JObject = Nothing, FabricJson As JObject = Nothing
#Region "读取文件并检查文件是否合规"
        Dim MinecraftJsonText As String = ReadFile(MinecraftJsonPath)
        If Not MinecraftJsonText.StartsWithF("{") Then Throw New Exception("Minecraft json 有误，地址：" & MinecraftJsonPath & "，前段内容：" & MinecraftJsonText.Substring(0, Math.Min(MinecraftJsonText.Length, 1000)))
        MinecraftJson = GetJson(MinecraftJsonText)

        If HasOptiFine Then
            Dim OptiFineJsonText As String = ReadFile(OptiFineJsonPath)
            If Not OptiFineJsonText.StartsWithF("{") Then Throw New Exception("OptiFine json 有误，地址：" & OptiFineJsonPath & "，前段内容：" & OptiFineJsonText.Substring(0, Math.Min(OptiFineJsonText.Length, 1000)))
            OptiFineJson = GetJson(OptiFineJsonText)
        End If

        If HasForge Then
            Dim ForgeJsonText As String = ReadFile(ForgeJsonPath)
            If Not ForgeJsonText.StartsWithF("{") Then Throw New Exception("Forge json 有误，地址：" & ForgeJsonPath & "，前段内容：" & ForgeJsonText.Substring(0, Math.Min(ForgeJsonText.Length, 1000)))
            ForgeJson = GetJson(ForgeJsonText)
        End If

        If HasNeoForge Then
            Dim NeoForgeJsonText As String = ReadFile(NeoForgeJsonPath)
            If Not NeoForgeJsonText.StartsWithF("{") Then Throw New Exception("NeoForge json 有误，地址：" & NeoForgeJsonPath & "，前段内容：" & NeoForgeJsonText.Substring(0, Math.Min(NeoForgeJsonText.Length, 1000)))
            NeoForgeJson = GetJson(NeoForgeJsonText)
        End If

        If HasLiteLoader Then
            Dim LiteLoaderJsonText As String = ReadFile(LiteLoaderJsonPath)
            If Not LiteLoaderJsonText.StartsWithF("{") Then Throw New Exception("LiteLoader json 有误，地址：" & LiteLoaderJsonPath & "，前段内容：" & LiteLoaderJsonText.Substring(0, Math.Min(LiteLoaderJsonText.Length, 1000)))
            LiteLoaderJson = GetJson(LiteLoaderJsonText)
        End If

        If HasFabric Then
            Dim FabricJsonText As String = ReadFile(FabricJsonPath)
            If Not FabricJsonText.StartsWithF("{") Then Throw New Exception("Fabric json 有误，地址：" & FabricJsonPath & "，前段内容：" & FabricJsonText.Substring(0, Math.Min(FabricJsonText.Length, 1000)))
            FabricJson = GetJson(FabricJsonText)
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
        Dim RawArguments As List(Of String) = AllArguments.Split(" ").Where(Function(l) l <> "").Select(Function(l) l.Trim).ToList
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
        Dim RealArguments As String = Join(SplitArguments.Distinct.ToList, " ")
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
        WriteFile(OutputJsonPath, OutputJson.ToString)
        If MinecraftJar <> OutputJar Then '可能是同一个文件
            If File.Exists(OutputJar) Then File.Delete(OutputJar)
            CopyFile(MinecraftJar, OutputJar)
        End If
        Log("[Download] 版本合并 " & OutputName & " 完成")
#End Region

    End Sub

#End Region

End Module
