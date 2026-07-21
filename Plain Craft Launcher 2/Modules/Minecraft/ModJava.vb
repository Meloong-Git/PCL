Public Module ModJava

    ''' <summary>
    ''' 初始化 Java 列表。
    ''' 若没有任何可用的 Java，则触发搜索。
    ''' </summary>
    Public Sub JavaInit()
        Try
            '从老版本迁移
            If Configs.JavaConfigVersion.Get() = 0 Then
                Try
                    'Java 列表
                    Logger.Warn("正在从老版本迁移 Java 列表")
                    Dim OldJavaList = Settings.Get(Of String)("LaunchArgumentJavaAll").DeserializeJson(Of JArray).
                                      Select(Function(JavaObject) New Java(JavaObject("Path").ToString)).ToList()
                    OldJavaList = JavaUtils.SortAsync(OldJavaList).Run()
                    Logger.Info($"已从老版本设置中发现 {OldJavaList.Count} 个 Java")
                    '强制选择的 Java
                    Dim SelectedJavaSetting = Settings.Get(Of String)("LaunchArgumentJavaSelect")
                    Dim SelectedJava = If(SelectedJavaSetting.StartsWithF("{"), SelectedJavaSetting.DeserializeJson(Of JObject)()("Path").ToString, "")
                    Dim Match = OldJavaList.FirstOrDefault(Function(j) j.Folder.Equals(SelectedJava, StringComparison.OrdinalIgnoreCase))
                    If Match IsNot Nothing Then
                        OldJavaList.Remove(Match)
                        OldJavaList.Insert(0, Match)
                        Logger.Info($"已将老版本设置中强制选择的 Java 置顶：{Match}")
                    End If
                    Configs.JavaList.Set(New ConcurrentList(Of Java)(OldJavaList))
                    '完成迁移
                    Configs.JavaConfigVersion.Set(Versions.JavaConfigVersion)
                Catch ex As Exception
                    Logger.Error(ex, "从老版本 PCL 迁移 Java 列表失败")
                End Try
            End If
            '读取缓存
            Dim List = Configs.JavaList.Get()
            If List.Any Then
                Logger.Info($"共有 {List.Count} 个可用的 Java：{vbCrLf}- {List.Select(Function(j) j.ToString).Join(vbCrLf + "- ")}")
                Return
            End If
            '如果没有 Java，则触发搜索
            Logger.Warn("初始化未找到可用的 Java，将自动触发搜索")
            JavaListRefreshWorker.Start()
        Catch ex As Exception
            Logger.Error(ex, "初始化 Java 列表失败")
            Configs.JavaList.Reset()
        End Try
    End Sub

    ''' <summary>
    ''' 搜索并获取所有可用的 Java，检查所有 Java 的可用性，写入设置，并在结束后更新设置页面显示。
    ''' </summary>
    Public WithEvents JavaListRefreshWorker As New RedoableWorker(Sub(c, p) JavaUtils.RefreshListAsync(c, p).Run())

#Region "UI 事件"

    ''' <summary>
    ''' 更新设置页面的 Java 列表显示。
    ''' </summary>
    Public Sub UpdateJavaLists() Handles JavaListRefreshWorker.Started, JavaListRefreshWorker.Stopped
        RunInUi(
        Sub()
            FrmSetupLaunch?.UpdateJavaList()
            FrmInstanceSetup?.UpdateJavaList()
        End Sub)
    End Sub

    ''' <summary>
    ''' 手动触发导入 Java。
    ''' </summary>
    Public Sub ManuallyImportJava()
        '选择
        Dim NewJavaPath As String = Dialogs.SelectFile("选择 java.exe（可能在 bin 文件夹中）", False, filter:="java.exe|java.exe").FirstOrDefault()
        If String.IsNullOrEmpty(NewJavaPath) Then Return
        '验证 Java 可用
        Dim NewJava As New Java(PathUtils.RemoveLastPart(NewJavaPath))
        If Not NewJava.CheckAsync().Run() Then
            Hint("该 Java 存在异常，无法使用", HintType.Red)
            Return
        End If
        '不再移除它
        Configs.JavaRemovedList.Edit(Sub(ByRef List) List.RemoveIf(Function(f) f.Equals(NewJava.Folder, StringComparison.OrdinalIgnoreCase)))
        '加入列表顶部
        Configs.JavaList.Edit(
        Sub(ByRef List)
            List.Remove(NewJava)
            List.Insert(0, NewJava)
        End Sub)
        Hint($"已将 Java 加入列表：{NewJavaPath}", HintType.Green)
        UpdateJavaLists()
    End Sub

    ''' <summary>
    ''' 手动从 Java 列表中移除指定的 Java，并记录到已移除列表中。
    ''' </summary>
    Public Sub ManuallyRemoveJava(JavaEntry As Java)
        Configs.JavaList.Edit(Sub(ByRef List) List.Remove(JavaEntry)) '从 Java 列表中移除
        Configs.JavaRemovedList.Edit(Function(List) List.Append(JavaEntry.Folder).DistinctBy(Function(s) s.Lower()).ToList()) '记录已移除
        Hint($"已将 Java 移除出列表：{JavaEntry}", HintType.Green)
        UpdateJavaLists()
        PageInstanceLeft.ReloadCurrentJava() '如果强制指定了该 Java，这会把它自动重置掉
    End Sub

    ''' <summary>
    ''' 手动触发搜索 Java。
    ''' </summary>
    Public Sub ManuallySearchJava(Optional CancellationToken As CancellationToken = Nothing)
        RunInThread(
        Sub()
            JavaListRefreshWorker.Start(cancellationToken:=CancellationToken)
            JavaListRefreshWorker.WaitIfRunningAsync(cancellationToken:=CancellationToken).Run() 'worker 开始/结束时会自动重载 UI
            If Configs.JavaList.Get().Any Then
                Hint("已找到 " & Configs.JavaList.Get().Count & " 个 Java！", HintType.Green)
            Else
                Hint("未找到可用的 Java！", HintType.Red)
            End If
        End Sub)
    End Sub

#End Region

#Region "选取"

    ''' <summary>
    ''' 分析指定 Minecraft 版本的基础 Java 版本要求。不受设置影响。
    ''' 如果由于 Java 版本要求冲突而导致没有可用的 Java，则返回 null。
    ''' </summary>
    Public Function GetJavaRequirement(Instance As McInstance) As (Range As ValueRange(Of Version), RecommendedComponent As String)

        '版本设置已经强制指定了 Java 版本要求
        If Settings.Get(Of Integer)("VersionArgumentJavaV2", Instance) = 1 Then
            Try
                Dim Range = ValueRange(Of Version).FromString(Settings.Get(Of String)("VersionArgumentJavaRange", Instance), Function(s) StringUtils.ParseVersionWithDefaults(s))
                Return (Range, Nothing)
            Catch ex As Exception
                Logger.Warn($"版本设置中指定的 Java 版本范围有误（{Settings.Get(Of String)("VersionArgumentJavaRange", Instance)}），这可能是因为首次修改此设置，尚未初始化")
            End Try
        End If

        '----------------------------------- 从 Minecraft 版本信息获取需求 -----------------------------------

        Dim JavaRange = ValueRange(Of Version).All()
        Dim AddConstraint =
        Sub(Constraint As ValueRange(Of Version))
            JavaRange = JavaRange?.Intersect(Constraint)
            If JavaRange.IsEmpty Then Logger.Warn($"Java 版本要求冲突（当前追加的要求为 {Constraint}，版本为 {Instance.VersionDisplayName}）")
        End Sub

        '原版 26-：通过版本号判断
        If (Not Instance.Version.Vaild AndAlso Instance.ReleaseTime >= New Date(2024, 4, 2)) OrElse
           (Instance.Version.Vaild AndAlso Instance.Version.Vanilla >= New Version(20, 0, 5)) Then
            '1.20.5+（24w14a+）：至少 Java 21
            AddConstraint(ValueRange(Of Version).AtLeast(New Version(21, 0)))
        ElseIf (Not Instance.Version.Vaild AndAlso Instance.ReleaseTime >= New Date(2021, 11, 16)) OrElse
               (Instance.Version.Vaild AndAlso Instance.Version.Vanilla.Major >= 18) Then
            '1.18 pre2+：至少 Java 17
            AddConstraint(ValueRange(Of Version).AtLeast(New Version(17, 0)))
        ElseIf (Not Instance.Version.Vaild AndAlso Instance.ReleaseTime >= New Date(2021, 5, 11)) OrElse
               (Instance.Version.Vaild AndAlso Instance.Version.Vanilla.Major >= 17) Then
            '1.17+ (21w19a+)：至少 Java 16
            AddConstraint(ValueRange(Of Version).AtLeast(New Version(16, 0)))
        ElseIf Instance.ReleaseTime.Year >= 2017 Then 'Minecraft 1.12 与 1.11 的分界线正好是 2017 年，太棒了
            '1.12+：至少 Java 8
            AddConstraint(ValueRange(Of Version).AtLeast(New Version(8, 0)))
        ElseIf Instance.ReleaseTime <= New Date(2013, 5, 1) AndAlso Instance.ReleaseTime.Year >= 2001 Then '避免某些版本写个 1960 年
            '1.5.2-：最高 Java 8
            AddConstraint(ValueRange(Of Version).LessThan(New Version(9, 0)))
        End If

        '原版 26+：获取 Mojang 要求的 Java 版本
        Dim RecommendedComponent As String = Nothing
        Dim RecommendedCode As Integer =
            If(Instance.JsonObject?("javaVersion")?("majorVersion")?.ToObject(Of Integer),
            If(Instance.JsonVersion?("java_version")?.ToObject(Of Integer), 0))
        If RecommendedCode >= 22 Then
            Logger.Info("Mojang 要求至少使用 Java " & RecommendedCode)
            AddConstraint(ValueRange(Of Version).AtLeast(New Version(RecommendedCode, 0)))
            RecommendedComponent =
                If(Instance.JsonObject?("javaVersion")?("component")?.ToString,
                   Instance.JsonVersion?("java_component")?.ToString)
            If RecommendedComponent = "" Then RecommendedComponent = Nothing
        End If

        'OptiFine 检测
        If Instance.Version.HasOptiFine AndAlso Instance.Version.Vaild Then '不管非标准版本
            If Instance.Version.Vanilla.Major < 7 Then
                '<1.7：至多 Java 8
                AddConstraint(ValueRange(Of Version).LessThan(New Version(9, 0)))
            ElseIf Instance.Version.Vanilla.Major >= 8 AndAlso Instance.Version.Vanilla.Major < 12 Then
                '1.8 - 1.11：必须恰好 Java 8
                AddConstraint(ValueRange(Of Version).ClosedOpen(New Version(8, 0), New Version(9, 0)))
            ElseIf Instance.Version.Vanilla.Major = 12 Then
                '1.12：最高 Java 8
                AddConstraint(ValueRange(Of Version).LessThan(New Version(9, 0)))
            End If
        End If

        'Forge 检测（部分测试结果见 #8432）
        If Instance.Version.HasForge Then
            If Instance.Version.Vanilla >= New Version(6, 0, 1) AndAlso Instance.Version.Vanilla <= New Version(7, 0, 2) Then
                '1.6.1 - 1.7.2：必须 Java 7
                AddConstraint(ValueRange(Of Version).ClosedOpen(New Version(7, 0), New Version(8, 0)))
            ElseIf Instance.Version.Vanilla.Major <= 12 OrElse Not Instance.Version.Vaild Then '非标准版本
                '<=1.12：Java 8
                AddConstraint(ValueRange(Of Version).LessThan(New Version(9, 0)))
            ElseIf Instance.Version.Vanilla.Major <= 14 Then
                '1.13 - 1.14：Java 8 - 10
                AddConstraint(ValueRange(Of Version).ClosedOpen(New Version(8, 0), New Version(11, 0)))
            ElseIf Instance.Version.Vanilla.Major = 15 Then
                '1.15：Java 8 - 15
                AddConstraint(ValueRange(Of Version).ClosedOpen(New Version(8, 0), New Version(16, 0)))
            ElseIf CompareVersionGE(Instance.Version.Forge, "34.0.0") AndAlso CompareVersionGE("36.2.25", Instance.Version.Forge) Then
                '1.16.3~5，Forge 34.0.0 ~ 36.2.25：最高 Java 8u320
                AddConstraint(ValueRange(Of Version).AtMost(New Version(8, 0, 320)))
            ElseIf CompareVersionGE(Instance.Version.Forge, "36.2.26") AndAlso CompareVersionGE("36.999999.999999", Instance.Version.Forge) Then
                '1.16.5，Forge 36.2.26+，<37.0.0：最高 Java 23
                AddConstraint(ValueRange(Of Version).LessThan(New Version(24, 0)))
            ElseIf CompareVersionGE(Instance.Version.Forge, "37.0.0") AndAlso CompareVersionGE("37.0.79", Instance.Version.Forge) Then
                '1.17.1，Forge 37.0.0 ~ 37.0.79：最高 Java 16
                AddConstraint(ValueRange(Of Version).LessThan(New Version(17, 0)))
            ElseIf Instance.Version.Vanilla.Major = 18 AndAlso Instance.Version.HasOptiFine Then '#305
                '1.18：若安装了 OptiFine，最高 Java 18
                AddConstraint(ValueRange(Of Version).LessThan(New Version(19, 0)))
            ElseIf CompareVersionGE(Instance.Version.Forge, "45.0.21") AndAlso CompareVersionGE("45.0.65", Instance.Version.Forge) Then
                '1.19.4，Forge 45.0.21 ~ 45.0.65：最高 Java 19
                AddConstraint(ValueRange(Of Version).LessThan(New Version(20, 0)))
            ElseIf CompareVersionGE(Instance.Version.Forge, "45.0.66") AndAlso CompareVersionGE("47.4.8", Instance.Version.Forge) Then
                '1.19.4~1.20.1，Forge 45.0.66 ~ 47.4.8：最高 Java 21
                AddConstraint(ValueRange(Of Version).LessThan(New Version(22, 0)))
            End If
        End If

        'NeoForge 检测
        If Instance.Version.HasNeoForge Then
            If Instance.Version.Vanilla = New Version(20, 0, 1) OrElse
               (CompareVersionGE("20.2.62-beta", Instance.Version.NeoForge) AndAlso Not Instance.Version.NeoForge.Contains("25w14craftmine")) Then
                '1.20.1，以及 1.20.2 的 20.2.62-beta 之前：最高 Java 21
                AddConstraint(ValueRange(Of Version).LessThan(New Version(22, 0)))
            End If
        End If

        'Fabric 检测
        If Instance.Version.HasFabric AndAlso Instance.Version.Vaild Then '不管非标准版本
            If Instance.Version.Vanilla.Major >= 15 AndAlso Instance.Version.Vanilla.Major <= 16 Then
                '1.15 - 1.16：Java 8+
                AddConstraint(ValueRange(Of Version).AtLeast(New Version(8, 0)))
            ElseIf Instance.Version.Vanilla.Major >= 18 Then
                '1.18+：Java 17+
                AddConstraint(ValueRange(Of Version).AtLeast(New Version(17, 0)))
            End If
        End If

        'LiteLoader 检测
        If Instance.Version.HasLiteLoader AndAlso Instance.Version.Vaild Then '不管非标准版本
            '最高 Java 8
            AddConstraint(ValueRange(Of Version).LessThan(New Version(9, 0)))
        End If

        '统一通行证检测
        If Settings.Get(Of McLoginType)("LoginType") = McLoginType.Nide Then
            '至少 Java 8u101
            AddConstraint(ValueRange(Of Version).AtLeast(New Version(8, 0, 141)))
        End If

        Return (JavaRange, RecommendedComponent)
    End Function

    ''' <summary>
    ''' 根据版本或 Java 范围要求寻找适合的 Java。
    ''' 若指定了 <paramref name="TryFixOrHint"/>，则会在找不到时会尝试下载 Java 或提示用户下载，否则只会静默运行。
    ''' </summary>
    ''' <returns>最适合的 Java。若找不到或失败则返回 null，除非取消否则不会抛出异常。</returns>
    Public Function SelectOrDownloadJava(Target As OneOf(Of ValueRange(Of Version), McInstance), TryFixOrHint As Boolean, Optional c As CancellationToken = Nothing, Optional p As ProgressProvider = Nothing) As Java
        Try
            If Target.Is(Of McInstance) Then MigrateInstanceJavaSettings(Target.As(Of McInstance), c, p?.SplitTo(0.2)) '迁移
            Dim Instance As McInstance = If(Target.Is(Of McInstance), Target.As(Of McInstance), Nothing)
            Select Case If(Instance Is Nothing, 0, Settings.Get(Of Integer)("VersionArgumentJavaV2", Instance))

                Case 2 '============================= 使用版本文件夹中的 Java =============================

                    '查找版本文件夹下的 Java
                    Logger.Info($"版本设置要求使用版本文件夹中的 Java：{Instance.PathVersion}")
                    Dim JavaFound = JavaUtils.SearchFoldersAsync(True, {Instance.PathVersion}, c, p?.SplitTo(1)).Run().FirstOrDefault()
                    If JavaFound IsNot Nothing Then
                        Logger.Info($"已发现版本文件夹中的 Java：{JavaFound}")
                        Return JavaFound
                    End If
                    '未能找到
                    If TryFixOrHint Then
                        SwitchToInstanceSetup(Instance)
                        MyMsgBox($"该版本的版本设置选择了 {vbLQ}使用版本文件夹中的 Java{vbRQ}，但版本文件夹里没能找到 Java。{vbCrLf}请先修改启动选项中的 Java 设置，或在版本文件夹中放一个 Java，然后再试。",
                            "未找到 Java", IsWarn:=True, ForceWait:=True)
                    End If
                    Return Nothing

                Case 3 '=============================== 强制指定特定 Java ===============================

                    Dim ChosenJava = Configs.InstanceForcedJava.Get(Instance.Config)
                    Logger.Info($"版本设置中强制指定的 Java：{Instance.PathVersion} → {ChosenJava}")
                    If ChosenJava Is Nothing Then
                        If TryFixOrHint Then
                            SwitchToInstanceSetup(Instance)
                            MyMsgBox($"该版本的版本设置选择了 {vbLQ}使用指定的 Java{vbRQ}，但还没有指定任何 Java。{vbCrLf}请先选择你想使用的 Java，然后再试。",
                                "未选择 Java", IsWarn:=True, ForceWait:=True)
                        End If
                        p?.Skip() : Return Nothing '<==== 设置中未指定 Java
                    End If
                    If Not Configs.JavaList.Get().Contains(ChosenJava) Then
                        Configs.InstanceForcedJava.Reset(Instance.Config)
                        UpdateJavaLists()
                        If TryFixOrHint Then
                            SwitchToInstanceSetup(Instance)
                            Logger.Error("版本设置中指定的 Java 不在 Java 列表中，请重新选择一个 Java！", LogBehavior.Alert)
                        End If
                        p?.Skip() : Return Nothing '<==== 设置中指定的 Java 不在列表中
                    End If
                    If Not ChosenJava.CheckAsync(c).Run() Then
                        Configs.InstanceForcedJava.Reset(Instance.Config)
                        UpdateJavaLists()
                        If TryFixOrHint Then
                            SwitchToInstanceSetup(Instance)
                            Logger.Error("版本设置中指定的 Java 已无法使用，请重新选择一个 Java，然后再试。", LogBehavior.Alert)
                        Else
                            Logger.Error("版本设置中指定的 Java 已无法使用，请重新选择一个 Java！", LogBehavior.Toast)
                        End If
                        p?.Finish() : Return Nothing '<==== 指定的 Java 存在异常
                    End If
                    p?.Finish() : Return ChosenJava '<==== 成功找到并检查通过

                Case Else '======================== 根据版本范围自动选择 JavaList 中的 Java ========================

                    '获取范围
                    Dim Range As ValueRange(Of Version) =
                        If(Target.Is(Of McInstance), GetJavaRequirement(Instance).Range, Target.As(Of ValueRange(Of Version)))
                    Dim RecommendedComponent As String =
                        If(Target.Is(Of McInstance), GetJavaRequirement(Instance).RecommendedComponent, Nothing)
                    Logger.Info($"自动选择以下范围的 Java：{Range} {RecommendedComponent}")
                    If Range.IsEmpty Then Throw New Exception("Java 版本需求存在冲突，导致没有任何可能适配的 Java，请查看 PCL 的日志了解详细信息！")

                    '搜索 Java
                    Dim SelectJavaFromJavaList =
                    Function() As Java
                        JavaListRefreshWorker.WaitIfRunning() '等待进行中的搜索结束
                        For Each ChosenJava In Configs.JavaList.Get()
                            If Not ChosenJava.CheckAsync(c).Run() Then '检查
                                Logger.Warn($"Java 检查失败，将尝试列表中的下一个 Java：{ChosenJava}")
                                Continue For
                            End If
                            If ChosenJava.Version Is Nothing OrElse Not Range.Contains(ChosenJava.Version) Then Continue For '版本范围
                            Logger.Info($"已选定 Java：{ChosenJava}")
                            Return ChosenJava
                        Next
                        Return Nothing
                    End Function
                    p?.Set(0.25)
                    Dim Result = SelectJavaFromJavaList()
                    If Result IsNot Nothing Then p?.Skip() : Return Result

                    SyncLock JavaLock '#3797
                        '既有列表中没有合适的 Java，重新触发搜索
                        Logger.Warn($"既有列表中没有合适的 Java，重新触发搜索")
                        JavaListRefreshWorker.Start(c, p?.SplitTo(0.3))
                        Result = SelectJavaFromJavaList()
                        If Result IsNot Nothing Then p?.Skip() : Return Result
                        If Not TryFixOrHint Then Return Nothing

                        '电脑中没有合适的 Java，获取 Mojang 提供的 Java 下载列表
                        Logger.Warn($"电脑中没有合适的 Java，开始获取 Mojang 提供的 Java 下载列表")
                        Dim Components = DownloadJavaList()
                        c.ThrowIfCancellationRequested()
                        p?.Set(0.4)

                        '确定要下载的目标 Java
                        Dim JavaToDownload As JavaDownloadInfo = If(
                        Components.FirstOrDefault(Function(Comp) Comp.Java("version")("name").ToString = RecommendedComponent), '优先选择指定的 Component
                        Components.FirstOrDefault(Function(Comp) Range.Contains(Comp.JavaVersion)))
                        If JavaToDownload Is Nothing Then
                            Logger.Warn($"Mojang 提供的 Java 列表中没有合适的 Java，无法自动下载")
                            '提示需要手动下载 Java
                            Dim Description As String
                            Dim HasPureLower As Boolean = Range.HasLower AndAlso Range.IsLowerInclusive AndAlso
                            Range.Lower.Minor <= 0 AndAlso Range.Lower.Build <= 0 AndAlso Range.Lower.Revision <= 0
                            Dim HasPureUpper As Boolean = Range.HasUpper AndAlso Not Range.IsUpperInclusive AndAlso
                            Range.Upper.Minor <= 0 AndAlso Range.Upper.Build <= 0 AndAlso Range.Upper.Revision <= 0
                            If Range.HasLower AndAlso Range.HasUpper Then '有上下界
                                If HasPureLower AndAlso HasPureUpper Then
                                    If Range.Lower.Major = Range.Upper.Major - 1 Then
                                        Description = $" Java {Range.Lower.Major}"
                                    Else
                                        Description = $" Java {Range.Lower.Major} ~ {Range.Upper.Major - 1}"
                                    End If
                                Else
                                    Description = $" Java {Range}" '摆烂
                                End If
                            ElseIf Range.HasLower Then '只有下界
                                If HasPureLower Then
                                    Description = $" Java {Range.Lower.Major} 或更高版本的 Java"
                                ElseIf Range.IsLowerInclusive Then
                                    Description = $" Java {Range.Lower} 或更高版本的 Java"
                                Else
                                    Description = $"版本高于 {Range.Lower} 的 Java"
                                End If
                            Else '只有上界
                                If HasPureUpper Then
                                    Description = $" Java {Range.Upper.Major - 1} 或更低版本的 Java"
                                ElseIf Range.IsUpperInclusive Then
                                    Description = $" Java {Range.Upper} 或更低版本的 Java"
                                Else
                                    Description = $"版本低于 {Range.Upper} 的 Java"
                                End If
                            End If
                            MyMsgBox($"PCL 没能找到合适的 Java。{vbCrLf}请在网上搜索并手动安装{Description}， 然后在【设置 → 启动 → 高级选项 → Java 列表】中导入它。", "需要手动安装 Java")
                            Return Nothing
                        End If

                        '自动下载 Java
                        Dim JavaLoader = CreateJavaDownloadLoader(JavaToDownload)
                        Try
                            JavaLoader.WaitForExit(IsForceRestart:=True, c:=c, p:=p?.SplitTo(0.95))
                        Finally
                            JavaLoader.Cancel() '确保取消时中止 Java 下载
                        End Try
                        Result = SelectJavaFromJavaList()
                        If Result IsNot Nothing Then p?.Finish() : Return Result
                        Throw New Exception($"下载 Java {JavaToDownload.ComponentName} 后仍然无法找到合适的 Java")
                    End SyncLock
            End Select
        Catch ex As Exception
            ex.ThrowIfCanceled()
            Logger.Error(ex, "选择 Java 时出现异常")
            p?.Skip() : Return Nothing
        End Try
    End Function
    Private Sub SwitchToInstanceSetup(Instance As McInstance)
        RunInUi(
        Sub()
            PageInstanceLeft.Instance = Instance
            FrmMain.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.InstanceSetup)
        End Sub)
    End Sub
    Private ReadOnly JavaLock As New Object

    ''' <summary>
    ''' 从老版本 PCL 迁移版本独立的 Java 设置。
    ''' </summary>
    Private Sub MigrateInstanceJavaSettings(Instance As McInstance, Optional c As CancellationToken = Nothing, Optional p As ProgressProvider = Nothing)
        If Configs.InstanceMigratedJava.Get(Instance.Config) Then p?.Skip() : Return
        If FrmInstanceSetup IsNot Nothing Then RunInUiWait(Sub() FrmInstanceSetup.ComboArgumentJava.IsEnabled = False)
        Try
            Logger.Info($"正在尝试迁移版本独立的 Java 设置（{Instance.Name}）")
            '强制使用指定 Java
            Dim ForcedJavaSetting = Settings.Get(Of String)("VersionArgumentJavaSelect", Instance)
            If Not ForcedJavaSetting.StartsWithF("{") Then
                p?.Set(0.4, True) : GoTo NoForcedJava
            End If
            Dim ForcedJava = New Java(ForcedJavaSetting.DeserializeJson(Of JObject)()("Path").ToString)
            If Not ForcedJava.CheckAsync(c).Run() Then
                Hint("该版本的设置中强制指定的 Java 存在异常，该设置已重置", HintType.Red)
                p?.Set(0.4) : GoTo NoForcedJava
            End If
            Logger.Info($"将版本独立设置改为老版本中强制指定的 Java：{ForcedJava}")
            If Not Configs.JavaList.Get().Contains(ForcedJava) Then
                Configs.JavaList.Edit(Sub(ByRef List) List.Add(ForcedJava)) '加入列表底部
                Logger.Info($"已将强制指定的 Java 加入 Java 列表底部：{ForcedJava}")
            End If
            Configs.InstanceForcedJava.Set(ForcedJava, Instance.Config)
            Settings.Set("VersionArgumentJavaV2", 3, Instance)
            UpdateJavaLists()
            Configs.InstanceMigratedJava.Set(True, Instance.Config) '完成
            p?.Finish() : Return
NoForcedJava:
            Dim JavaFound = JavaUtils.SearchFoldersAsync(True, {Instance.PathVersion}, c, p?.SplitTo(1)).Run()
            If JavaFound.Any() Then
                '从版本文件夹中发现了 Java
                Logger.Info($"在版本文件夹中发现了 Java，将版本独立设置改为优先使用版本文件夹的 Java：{JavaFound}")
                Settings.Set("VersionArgumentJavaV2", 2, Instance)
            Else
                '自动选择
                Logger.Info("没有需要迁移的内容，自动选择 Java")
                Settings.Set("VersionArgumentJavaV2", 0, Instance)
            End If
            Configs.InstanceMigratedJava.Set(True, Instance.Config) '完成
        Finally
            If FrmInstanceSetup IsNot Nothing Then RunInUiWait(
            Sub()
                SettingService.RefreshSettings(FrmInstanceSetup.ComboArgumentJava)
                SettingService.RefreshSettings(FrmInstanceSetup.ComboArgumentJavaSelect)
                FrmInstanceSetup.ComboArgumentJava.IsEnabled = True
            End Sub)
        End Try
    End Sub

#End Region

#Region "下载（均为内部工具方法，无外部调用）"

    Private Class JavaDownloadInfo
        Public Property ComponentName As String
        Public Property Java As JObject
        Public Property JavaVersion As Version
    End Class

    ''' <summary>
    ''' 下载 Mojang 提供的 Java 列表。
    ''' <para/> 若本次打开程序已经下载过，则会直接返回缓存的结果。
    ''' </summary>
    Private Function DownloadJavaList() As List(Of JavaDownloadInfo)
        'TODO: 写一个更简单的 Lazy，允许重置缓存
        Static Components As New Lazy(Of List(Of JavaDownloadInfo))(
        Function()
            '下载 Mojang 提供的 Java 列表
            Dim IndexFileStr As String = NetRequestByLoader(DlVersionListOrder(
                {"https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"},
                {"https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"}
            ), IsJson:=True)
            '解析 Java 列表
            Dim Results = New List(Of JavaDownloadInfo)
            For Each Prop As JProperty In IndexFileStr.DeserializeJson(Of JObject)()("windows-x64")
                If Prop.Name = "minecraft-java-exe" Then Continue For '不是完整 Java
                Try
                    Dim Entry = Prop.Value.FirstOrDefault
                    If Entry Is Nothing Then Continue For
                    Dim JavaName = Entry("version")("name").ToString '可能的名称格式："12"、"8u51-cacert462b08"、"17.0.8.1"、"17.0.1.12.1"、"21.0.7"
                    If JavaName.Contains(".") Then
                        Dim Parts As String() = JavaName.Split(".")
                        Results.Add(New JavaDownloadInfo With {.ComponentName = Prop.Name, .Java = Entry, .JavaVersion = New Version(Val(Parts(0)), Val(Parts(1)), If(Parts.Length >= 3, Val(Parts(2)), 0), If(Parts.Length >= 4, Val(Parts(3)), 0))})
                    ElseIf JavaName.StartsWithF("8u51-") Then
                        Results.Add(New JavaDownloadInfo With {.ComponentName = Prop.Name, .Java = Entry, .JavaVersion = New Version(8, 0, 51, 0)})
                    Else
                        Results.Add(New JavaDownloadInfo With {.ComponentName = Prop.Name, .Java = Entry, .JavaVersion = New Version(Val(JavaName.BeforeFirstOfAny({"u", "."})), 0, 0, 0)})
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, $"解析 Mojang 提供的 Java 列表项失败，已跳过（{Prop.Name}）")
                End Try
            Next
            Return Results
        End Function)
        Return Components.Value
    End Function

    ''' <summary>
    ''' 构建下载指定 Java 的加载器。
    ''' </summary>
    Private Function CreateJavaDownloadLoader(Target As JavaDownloadInfo) As LoaderCombo(Of Integer)

        '分析需要下载的 Java 文件列表
        Dim Address As String = Target.Java("manifest")("url")
        Logger.Info($"准备下载 Java {Target.Java("version")("name")}（{Target.ComponentName}）：{Address}")
        Dim ListFileStr As String = NetRequestByLoader(DlSourceOrder({Address}, {Address.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com")}), IsJson:=True)
        Dim BaseDir As String = $"{Paths.AppData}.minecraft\runtime\{Target.ComponentName}\"
        Dim FilesToDownload As New List(Of NetFile)
        For Each File As JProperty In CType(ListFileStr.DeserializeJson(), JObject)("files")
            If CType(File.Value, JObject)("downloads")?("raw") Is Nothing Then Continue For
            Dim Info As JObject = CType(File.Value, JObject)("downloads")("raw")
            Dim Checker As New FileChecker With {.ActualSize = Info("size"), .Hash = Info("sha1")}
            If Checker.Hash = "12976a6c2b227cbac58969c1455444596c894656" OrElse Checker.Hash = "c80e4bab46e34d02826eab226a4441d0970f2aba" OrElse Checker.Hash = "84d2102ad171863db04e7ee22a259d1f6c5de4a5" Then
                Continue For '跳过 3 个无意义大量重复文件（#3827）
            End If
            Dim Url As String = Info("url")
            FilesToDownload.Add(New NetFile(
                DlSourceOrder({Url}, {Url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com")}),
                BaseDir & File.Name, Checker))
        Next
        Logger.Info($"该 Java 需要下载 {FilesToDownload.Count} 个文件，目标文件夹：{BaseDir}")

        '构建加载器
        Dim Loader = New LoaderCombo(Of Integer)("下载 Java", {
            New LoaderDownload("下载 Java 文件", FilesToDownload) With {.ProgressWeight = 10},
            New LoaderWorker("刷新 Java 列表", JavaListRefreshWorker)
        }) With {.OnStateChanged =
        Sub(RawLoader As LoaderCombo(Of Integer))
            If RawLoader.State = LoadState.Failed OrElse RawLoader.State = LoadState.Canceled Then
                Logger.Warn($"由于 Java 下载未完成，清理未下载完成的 Java 文件夹：{BaseDir}")
                DirectoryUtils.Delete(BaseDir)
            End If
        End Sub}
        Return Loader

    End Function

#End Region

End Module
