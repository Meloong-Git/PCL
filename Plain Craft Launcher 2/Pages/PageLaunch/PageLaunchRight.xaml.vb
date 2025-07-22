Public Class PageLaunchRight
    Implements IRefreshable

    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        PanScroll = PanBack '不知道为啥不能在 XAML 设置
        PanLog.Visibility = If(ModeDebug, Visibility.Visible, Visibility.Collapsed)
        '快照版提示
#If BETA Then
        PanHint.Visibility = Visibility.Collapsed
#Else
        PanHint.Visibility = If(ThemeCheckGold(), Visibility.Collapsed, Visibility.Visible)
        LabHint1.Text = "快照版包含尚未正式发布的测试功能，仅用于赞助者本人尝鲜。请不要发给其他人或者用来制作整合包哦！"
        LabHint2.Text = $"若已累积赞助￥23.33，在爱发电私信发送 {vbLQ}解锁码{vbRQ} 即可永久隐藏此提示。"
#End If
    End Sub

    '暂时关闭快照版提示
#If Not BETA Then
    Private Sub BtnHintClose_Click(sender As Object, e As EventArgs) Handles BtnHintClose.Click
        AniDispose(PanHint, True)
    End Sub
#End If

#Region "主页"

    ''' <summary>
    ''' 刷新主页。
    ''' </summary>
    Private Sub Refresh() Handles Me.Loaded
        RunInNewThread(
        Sub()
            Try
                SyncLock RefreshLock
                    RefreshReal()
                End SyncLock
            Catch ex As Exception
                Log(ex, "加载 PCL 主页自定义信息失败", If(ModeDebug, LogLevel.Msgbox, LogLevel.Hint))
            End Try
        End Sub, $"刷新主页 #{GetUuid()}")
    End Sub
    Private Sub RefreshReal()
        Dim Content As String = ""
        Dim Url As String
        Select Case Setup.Get("UiCustomType")
            Case 1
                '加载本地文件
                Log("[Page] 主页自定义数据来源：本地文件")
                Content = ReadFile(Path & "PCL\Custom.xaml") 'ReadFile 会进行存在检测
            Case 2
                Url = Setup.Get("UiCustomNet")
Download:
                '加载联网文件
                If String.IsNullOrWhiteSpace(Url) Then Exit Select
                If Url = Setup.Get("CacheSavedPageUrl") AndAlso File.Exists(PathTemp & "Cache\Custom.xaml") Then
                    '缓存可用
                    Log("[Page] 主页自定义数据来源：联网缓存文件")
                    Content = ReadFile(PathTemp & "Cache\Custom.xaml")
                    '后台更新缓存
                    OnlineLoader.Start(Url)
                Else
                    '缓存不可用
                    Log("[Page] 主页自定义数据来源：联网全新下载")
                    Hint("正在加载主页……")
                    RunInUiWait(Sub() LoadContent("")) '在加载结束前清空页面
                    Setup.Set("CacheSavedPageVersion", "")
                    OnlineLoader.Start(Url) '下载完成后将会再次触发更新
                    Return
                End If
            Case 3
                Select Case Setup.Get("UiCustomPreset")
                    Case 0
                        Log("[Page] 主页预设：你知道吗")
                        Content = "
                            <local:MyCard Title=""你知道吗？"" Margin=""0,0,0,15"">
                                <TextBlock Margin=""25,38,23,15"" FontSize=""13.5"" IsHitTestVisible=""False"" Text=""{hint}"" TextWrapping=""Wrap"" Foreground=""{DynamicResource ColorBrush1}"" />
                                <local:MyIconButton Height=""22"" Width=""22"" Margin=""9"" VerticalAlignment=""Top"" HorizontalAlignment=""Right"" 
                                    EventType=""刷新主页"" EventData=""/""
                                    Logo=""M875.52 148.48C783.36 56.32 655.36 0 512 0 291.84 0 107.52 138.24 30.72 332.8l122.88 46.08C204.8 230.4 348.16 128 512 128c107.52 0 199.68 40.96 271.36 112.64L640 384h384V0L875.52 148.48zM512 896c-107.52 0-199.68-40.96-271.36-112.64L384 640H0v384l148.48-148.48C240.64 967.68 368.64 1024 512 1024c220.16 0 404.48-138.24 481.28-332.8L870.4 645.12C819.2 793.6 675.84 896 512 896z"" />
                            </local:MyCard>"
                    Case 1
                        Log("[Page] 主页预设：回声洞")
                        Content = "
                            <local:MyCard Title=""回声洞"" Margin=""0,0,0,15"">
                                <TextBlock Margin=""25,38,23,15"" FontSize=""13.5"" IsHitTestVisible=""False"" Text=""{cave}"" TextWrapping=""Wrap"" Foreground=""{DynamicResource ColorBrush1}"" />
                                <local:MyIconButton Height=""22"" Width=""22"" Margin=""9"" VerticalAlignment=""Top"" HorizontalAlignment=""Right"" 
                                    EventType=""刷新主页"" EventData=""/""
                                    Logo=""M875.52 148.48C783.36 56.32 655.36 0 512 0 291.84 0 107.52 138.24 30.72 332.8l122.88 46.08C204.8 230.4 348.16 128 512 128c107.52 0 199.68 40.96 271.36 112.64L640 384h384V0L875.52 148.48zM512 896c-107.52 0-199.68-40.96-271.36-112.64L384 640H0v384l148.48-148.48C240.64 967.68 368.64 1024 512 1024c220.16 0 404.48-138.24 481.28-332.8L870.4 645.12C819.2 793.6 675.84 896 512 896z"" />
                            </local:MyCard>"
                    Case 2
                        Log("[Page] 主页预设：Minecraft 新闻")
                        Url = "http://pcl.mcnews.thestack.top"
                        GoTo Download
                    Case 3
                        Log("[Page] 主页预设：简单主页")
                        Url = "https://raw.gitcode.com/mfn233/PCL-Mainpage/raw/main/Custom.xaml"
                        GoTo Download
                    Case 4
                        Log("[Page] 主页预设：每日整合包推荐")
                        Url = "https://pclsub.sodamc.com/"
                        GoTo Download
                    Case 5
                        Log("[Page] 主页预设：Minecraft 皮肤推荐")
                        Url = "https://forgepixel.com/pcl_sub_file"
                        GoTo Download
                    Case 6
                        Log("[Page] 主页预设：OpenBMCLAPI 仪表盘 Lite")
                        Url = "https://pcl-bmcl.milu.ink/"
                        GoTo Download
                    Case 7
                        Log("[Page] 主页预设：主页市场")
                        Url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml"
                        GoTo Download
                    Case 8
                        Log("[Page] 主页预设：更新日志")
                        Url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Joker2184/UpdateHomepage.xaml"
                        GoTo Download
                    Case 9
                        Log("[Page] 主页预设：PCL 新功能说明书")
                        Url = "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml"
                        GoTo Download
                    Case 10
                        Log("[Page] 主页预设：OpenMCIM Dashboard")
                        Url = "https://files.mcimirror.top/PCL"
                        GoTo Download
                    Case 11
                        Log("[Page] 主页预设：杂志主页")
                        Url = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml"
                        GoTo Download
                    Case 12
                        Log("[Page] 主页预设：PCL GitHub 仪表盘")
                        Url = "https://raw.gitcode.com/Deep-Dark-Forest/PCL2-GitHub-Dashboard-Homepage/raw/main/custom.xaml"
                        GoTo Download
                End Select
        End Select
        RunInUi(Sub() LoadContent(Content))
    End Sub
    Private RefreshLock As New Object

    '联网获取主页文件
    Private OnlineLoader As New LoaderTask(Of String, Integer)("下载主页", AddressOf OnlineLoaderSub) With {.ReloadTimeout = 10 * 60 * 1000}
    Private Sub OnlineLoaderSub(Task As LoaderTask(Of String, Integer))
        Dim Address As String = Task.Input '#3721 中连续触发两次导致内容变化
        Try
            '获取版本校验地址
            Dim VersionAddress As String
            If Address.Contains(".xaml") Then
                VersionAddress = Address.Replace(".xaml", ".xaml.ini")
            Else
                VersionAddress = Address.BeforeFirst("?")
                If Not VersionAddress.EndsWith("/") Then VersionAddress += "/"
                VersionAddress += "version"
                If Address.Contains("?") Then VersionAddress += "?" & Address.AfterFirst("?")
            End If
            '校验版本
            Dim Version As String = ""
            Dim NeedDownload As Boolean = True
            Try
                Version = NetGetCodeByRequestOnce(VersionAddress, Timeout:=10000)
                If Version.Length > 1000 Then Throw New Exception($"获取的主页版本过长（{Version.Length} 字符）")
                Dim CurrentVersion As String = Setup.Get("CacheSavedPageVersion")
                If Version <> "" AndAlso CurrentVersion <> "" AndAlso Version = CurrentVersion Then
                    Log($"[Page] 当前缓存的主页已为最新，当前版本：{Version}，检查源：{VersionAddress}")
                    NeedDownload = False
                Else
                    Log($"[Page] 需要下载联网主页，当前版本：{Version}，检查源：{VersionAddress}")
                End If
            Catch exx As Exception
                Log(exx, $"联网获取主页版本失败", LogLevel.Developer)
                Log($"[Page] 无法检查联网主页版本，将直接下载，检查源：{VersionAddress}")
            End Try
            '实际下载
            If NeedDownload Then
                Dim FileContent As String = NetGetCodeByRequestRetry(Address)
                Log($"[Page] 已联网下载主页，内容长度：{FileContent.Length}，来源：{Address}")
                Setup.Set("CacheSavedPageUrl", Address)
                Setup.Set("CacheSavedPageVersion", Version)
                WriteFile(PathTemp & "Cache\Custom.xaml", FileContent)
            End If
            '要求刷新
            RunInUi(AddressOf Refresh) '不直接调用 Refresh，以防止死循环（#6245）
        Catch ex As Exception
            Log(ex, $"下载主页失败（{Address}）", If(ModeDebug, LogLevel.Msgbox, LogLevel.Hint))
        End Try
    End Sub

    ''' <summary>
    ''' 立即强制刷新主页。
    ''' 必须在 UI 线程调用。
    ''' </summary>
    Public Sub ForceRefresh() Implements IRefreshable.Refresh
        Log("[Page] 要求强制刷新主页")
        ClearCache()
        '实际的刷新
        If FrmMain.PageCurrent.Page = FormMain.PageType.Launch Then
            PanBack.ScrollToHome()
            Refresh()
        Else
            FrmMain.PageChange(FormMain.PageType.Launch)
        End If
    End Sub

    ''' <summary>
    ''' 清空主页缓存信息。
    ''' </summary>
    Private Sub ClearCache()
        LoadedContentHash = -1
        OnlineLoader.Input = ""
        Setup.Set("CacheSavedPageUrl", "")
        Setup.Set("CacheSavedPageVersion", "")
        Log("[Page] 已清空主页缓存")
    End Sub

    ''' <summary>
    ''' 从文本内容中加载主页。
    ''' 必须在 UI 线程调用。
    ''' </summary>
    Private Sub LoadContent(Content As String)
        SyncLock LoadContentLock
            '如果加载目标内容一致则不加载
            Dim Hash = Content.GetHashCode()
            If Hash = LoadedContentHash Then Return
            LoadedContentHash = Hash
            '实际加载内容
            PanCustom.Children.Clear()
            If String.IsNullOrWhiteSpace(Content) Then
                Log($"[Page] 实例化：清空主页 UI，来源为空")
                Return
            End If
            Dim LoadStartTime As Date = Date.Now
            Try
                '修改时应同时修改 PageOtherHelpDetail.Init
                Content = HelpArgumentReplace(Content)
                If Content.Contains("xmlns") Then Content = Content.RegexReplace("xmlns[^""']*(""|')[^""']*(""|')", "").Replace("xmlns", "") '禁止声明命名空间
                Content = "<StackPanel xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:sys=""clr-namespace:System;assembly=mscorlib"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2"">" & Content & "</StackPanel>"
                Log($"[Page] 实例化：加载主页 UI 开始，最终内容长度：{Content.Count}")
                PanCustom.Children.Add(GetObjectFromXML(Content))
            Catch ex As Exception
                If ModeDebug Then
                    Log(ex, "加载失败的主页内容：" & vbCrLf & Content)
                    If MyMsgBox(If(TypeOf ex Is UnauthorizedAccessException, ex.Message, $"主页内容编写有误，请根据下列错误信息进行检查：{vbCrLf}{GetExceptionSummary(ex)}"),
                                "加载主页界面失败", "重试", "取消") = 1 Then
                        GoTo Refresh '防止 SyncLock 死锁
                    End If
                Else
                    Log(ex, "加载主页界面失败", LogLevel.Hint)
                End If
                Return
            End Try
            Dim LoadCostTime = (Date.Now - LoadStartTime).Milliseconds
            Log($"[Page] 实例化：加载主页 UI 完成，耗时 {LoadCostTime}ms")
            If LoadCostTime > 3000 Then Hint($"主页加载过于缓慢（花费了 {Math.Round(LoadCostTime / 1000, 1)} 秒），请向主页作者反馈此问题，或暂时停止使用该主页")
        End SyncLock
        Return
Refresh:
        ForceRefresh()
    End Sub
    Private LoadedContentHash As Integer = -1
    Private LoadContentLock As New Object

#End Region

End Class
