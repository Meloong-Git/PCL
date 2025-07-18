Public Module ModJava
    Public JavaListCacheVersion As Integer = 7

    ''' <summary>
    ''' 目前所有可用的 Java。
    ''' </summary>
    Public JavaList As New List(Of JavaEntry)

    Public Class JavaEntry

        '路径
        ''' <summary>
        ''' Java.exe 文件的完整路径。
        ''' </summary>
        Public ReadOnly Property PathJava As String
            Get
                Return PathFolder & "java.exe"
            End Get
        End Property
        ''' <summary>
        ''' Javaw.exe 文件的完整路径。
        ''' </summary>
        Public ReadOnly Property PathJavaw As String
            Get
                Return PathFolder & "javaw.exe"
            End Get
        End Property
        ''' <summary>
        ''' Javaw.exe 文件所在文件夹的路径，以 \ 结尾。
        ''' </summary>
        Public PathFolder As String
        ''' <summary>
        ''' 是否为用户手动导入的 Java。
        ''' </summary>
        Public IsUserImport As Boolean

        '版本信息
        ''' <summary>
        ''' Java 的详细版本。若不足 4 位会在前方补 1，例如 1.16.0.1。
        ''' 其大版本号为 Minor。
        ''' </summary>
        Public Version As Version
        ''' <summary>
        ''' Java 的大版本号。
        ''' </summary>
        Public ReadOnly Property VersionCode As Integer
            Get
                Return Version.Minor
            End Get
        End Property
        ''' <summary>
        ''' 是否为 Java Runtime Environment。
        ''' </summary>
        Public IsJre As Boolean
        ''' <summary>
        ''' 是否为 64 位 Java。
        ''' </summary>
        Public Is64Bit As Boolean
        ''' <summary>
        ''' 是否已设置环境变量。
        ''' </summary>
        Public ReadOnly Property HasEnvironment As Boolean
            Get
                If PathFolder Is Nothing OrElse PathEnv Is Nothing Then Return False
                Return PathEnv.Replace("\", "").Replace("/", "").ContainsF(PathFolder.Replace("\", ""), True)
            End Get
        End Property

        '序列化
        Public Function ToJson() As JObject
            Return New JObject({New JProperty("Path", PathFolder), New JProperty("VersionString", Version.ToString), New JProperty("IsJre", IsJre), New JProperty("Is64Bit", Is64Bit), New JProperty("IsUserImport", IsUserImport)})
        End Function
        Public Shared Function FromJson(Data As JObject) As JavaEntry
            Return New JavaEntry(Data("Path"), Data("IsUserImport")) With {.Version = New Version(Data("VersionString")), .IsJre = Data("IsJre"), .Is64Bit = Data("Is64Bit")}
        End Function
        ''' <summary>
        ''' 转化为用户友好的字符串输出。
        ''' </summary>
        Public Overrides Function ToString() As String
            Dim VersionString = Version.ToString
            If VersionString.StartsWithF("1.") Then VersionString = Mid(VersionString, 3)
            Return If(IsJre, "JRE ", "JDK ") & VersionCode & " (" & VersionString & ")" & If(Is64Bit, "", "，32 位") & If(IsUserImport, "，手动导入", "") & "：" & PathFolder
        End Function

        '构造
        ''' <summary>
        ''' 输入 javaw.exe 文件所在文件夹的路径，不限制结尾。
        ''' </summary>
        Public Sub New(Folder As String, IsUserImport As Boolean)
            If Not Folder.EndsWithF("\") Then Folder += "\"
            PathFolder = Folder.Replace("/", "\")
            Me.IsUserImport = IsUserImport
        End Sub

        '方法
        Private IsChecked As Boolean = False
        ''' <summary>
        ''' 检查并获取 Java 详细信息。在 Java 存在异常时抛出错误。
        ''' </summary>
        Public Sub Check()
            If IsChecked Then Return
            Dim Output As String = Nothing
            Try
                '确定文件存在
                If Not File.Exists(PathJavaw) Then
                    Throw New FileNotFoundException("未找到 javaw.exe 文件", PathJavaw)
                End If
                If Not File.Exists(PathFolder & "java.exe") Then
                    Throw New FileNotFoundException("未找到 java.exe 文件", PathFolder & "java.exe")
                End If
                If File.Exists(PathFolder & "pdf-bookmark") Then
                    Throw New Exception("不兼容 PDF Bookmark 的 Java") '#5326
                End If
                IsJre = Not File.Exists(PathFolder & "javac.exe")
                '运行 -version
                Output = ShellAndGetOutput(PathFolder & "java.exe", "-version", 15000).ToLower
                If Output = "" Then Throw New ApplicationException("尝试运行该 Java 失败")
                If ModeDebug Then Log("[Java] Java 检查输出：" & PathFolder & "java.exe" & vbCrLf & Output)
                If Output.Contains("/lib/ext exists") Then Throw New ApplicationException("无法运行该 Java，请在删除 Java 文件夹中的 /lib/ext 文件夹后再试")
                If Output.Contains("a fatal error") Then Throw New ApplicationException("由于操作系统或 JVM 存在问题，无法运行该 Java。")
                '获取详细信息
                Dim VersionString = If(RegexSeek(Output, "(?<=version "")[^""]+"), If(RegexSeek(Output, "(?<=openjdk )[0-9]+"), "")).Replace("_", ".").Split("-").First
                If VersionString.Split(".").Count > 4 Then VersionString = VersionString.Replace(".0.", ".") '#3493，VersionString = "21.0.2.0.2"
                Do While VersionString.Split(".").Count < 4
                    If VersionString.StartsWithF("1.") Then
                        VersionString = VersionString & ".0"
                    Else
                        VersionString = "1." & VersionString
                    End If
                Loop
                If VersionString = "" Then Throw New ApplicationException($"未找到该 Java 的版本号{If(Output.Length < 500, $"{vbCrLf}输出为：{vbCrLf}{Output}", "")}")
                Version = New Version(VersionString)
                If Version.Minor = 0 Then
                    Log("[Java] 疑似 X.0.X.X 格式版本号：" & Version.ToString)
                    Version = New Version(1, Version.Major, Version.Build, Version.Revision)
                End If
                Is64Bit = Output.Contains("64-bit")
                If Version.Minor <= 4 OrElse Version.Minor >= 100 Then Throw New ApplicationException("分析详细信息失败，获取的版本为 " & Version.ToString)
                '基于 #3649，在 64 位系统上禁用 32 位 Java
                If Not Is64Bit AndAlso Not Is32BitSystem Then Throw New Exception("该 Java 为 32 位版本，请安装 64 位的 Java")
                '基于 #2249 发现 JRE 17 似乎也导致了 Forge 安装失败，干脆禁用更多版本的 JRE
                If IsJre AndAlso VersionCode >= 16 Then Throw New Exception("由于高版本 JRE 对游戏的兼容性很差，因此不再允许使用。你可以使用对应版本的 JDK，而非 JRE！")
            Catch ex As ApplicationException
                Throw ex
            Catch ex As ThreadInterruptedException
                Throw ex
            Catch ex As Exception
                Log("[Java] 检查失败的 Java 输出：" & PathFolder & "java.exe" & vbCrLf & If(Output, "无程序输出"))
                Throw New Exception("检查 Java 失败（" & If(PathJavaw, "Nothing") & "）", ex)
            End Try
            IsChecked = True
        End Sub

    End Class

    ''' <summary>
    ''' Path 环境变量。
    ''' </summary>
    Private ReadOnly Property PathEnv As String
        Get
            If _PathEnv Is Nothing Then _PathEnv = If(Environment.GetEnvironmentVariable("Path"), "")
            Return _PathEnv
        End Get
    End Property
    Private _PathEnv As String = Nothing

    ''' <summary>
    ''' JAVA_HOME 环境变量。
    ''' </summary>
    Private ReadOnly Property PathJavaHome As String
        Get
            If _PathJavaHome Is Nothing Then _PathJavaHome = If(Environment.GetEnvironmentVariable("JAVA_HOME"), "")
            Return _PathJavaHome
        End Get
    End Property
    Private _PathJavaHome As String = Nothing

    ''' <summary>
    ''' 初始化 Java 列表，但除非没有 Java，否则不进行检查。
    ''' </summary>
    Public Sub JavaListInit()
        JavaList = New List(Of JavaEntry)
        Try
            If Setup.Get("CacheJavaListVersion") < JavaListCacheVersion Then
                '不使用缓存
                Log("[Java] 要求 Java 列表缓存更新")
                Setup.Set("CacheJavaListVersion", JavaListCacheVersion)
            Else
                '使用缓存
                For Each JsonEntry In GetJson(Setup.Get("LaunchArgumentJavaAll"))
                    JavaList.Add(JavaEntry.FromJson(JsonEntry))
                Next
            End If
            If Not JavaList.Any() Then
                Log("[Java] 初始化未找到可用的 Java，将自动触发搜索", LogLevel.Developer)
                JavaSearchLoader.Start(0)
            Else
                Log("[Java] 缓存中有 " & JavaList.Count & " 个可用的 Java：")
                JavaList.ForEach(Sub(j) Log($"[Java]  - {j}"))
            End If
        Catch ex As Exception
            Log(ex, "初始化 Java 列表失败", LogLevel.Feedback)
            Setup.Set("LaunchArgumentJavaAll", "[]")
        End Try
    End Sub

    ''' <summary>
    ''' 防止多个需要 Java 的部分同时要求下载 Java（#3797）。
    ''' </summary>
    Public JavaLock As New Object
    ''' <summary>
    ''' 根据要求返回最适合的 Java，若找不到则返回 Nothing。
    ''' 最小与最大版本在与输入相同时也会通过。
    ''' 必须在工作线程调用，且必须包括 SyncLock JavaLock。
    ''' </summary>
    Public Function JavaSelect(CancelException As String, Optional MinVersion As Version = Nothing, Optional MaxVersion As Version = Nothing,
                               Optional RelatedVersion As McVersion = Nothing) As JavaEntry
        Try
            Dim AllowedJavaList As New List(Of JavaEntry)

            '添加特定的 Java
            Dim JavaPreList As New Dictionary(Of String, Boolean)
            If PathMcFolder.Split("\").Count > 3 AndAlso Not PathMcFolder.Contains("AppData\Roaming") Then
                JavaSearchFolder(GetPathFromFullPath(PathMcFolder), JavaPreList, False, True) 'Minecraft 文件夹的父文件夹（如果不是根目录或 %APPDATA% 的话）
            End If
            JavaSearchFolder(PathMcFolder, JavaPreList, False, True) 'Minecraft 文件夹
            JavaPreList = JavaPreList.Where(Function(j) Not j.Key.Contains(".minecraft\runtime")).
                ToDictionary(Function(j) j.Key, Function(j) j.Value) '排除官启自带 Java（#4286）
            If RelatedVersion IsNot Nothing Then JavaSearchFolder(RelatedVersion.Path, JavaPreList, False, True) '所选版本文件夹
            Dim TargetJavaList As New List(Of JavaEntry)
            For Each Entry In JavaPreList
                TargetJavaList.Add(New JavaEntry(Entry.Key, Entry.Value))
            Next

            '检查特定的 Java
            If TargetJavaList.Any Then
                TargetJavaList = JavaCheckList(TargetJavaList)
                Log("[Java] 检查后找到 " & TargetJavaList.Count & " 个特定路径下的 Java：")
                For Each Java In TargetJavaList
                    Log($"[Java]  - {Java}")
                Next
            End If

#Region "添加用户指定的 Java，储存到 UserJava 中"

            Dim UserJava As JavaEntry = Nothing

            '获取版本独立设置中指定的 Java
            Dim VersionSelect As String = ""
            If RelatedVersion IsNot Nothing Then
                VersionSelect = Setup.Get("VersionArgumentJavaSelect", Version:=RelatedVersion)
                If VersionSelect.StartsWithF("{") Then
                    Try
                        UserJava = JavaEntry.FromJson(GetJson(VersionSelect))
                        UserJava.Check()
                    Catch ex As ThreadInterruptedException
                        Throw
                    Catch ex As Exception
                        UserJava = Nothing
                        Setup.Reset("VersionArgumentJavaSelect", Version:=RelatedVersion)
                        Log(ex, "版本独立设置中指定的 Java 已无法使用，此设置已重置", LogLevel.Hint)
                    End Try
                End If
            End If

            '获取全局设置中指定的 Java
            If UserJava Is Nothing AndAlso VersionSelect <> "" AndAlso Setup.Get("LaunchArgumentJavaSelect") <> "" Then
                Try
                    UserJava = JavaEntry.FromJson(GetJson(Setup.Get("LaunchArgumentJavaSelect")))
                    UserJava.Check()
                Catch ex As ThreadInterruptedException
                    Throw
                Catch ex As Exception
                    UserJava = Nothing
                    Setup.Reset("LaunchArgumentJavaSelect")
                    Log(ex, "全局设置中指定的 Java 已无法使用，此设置已重置", LogLevel.Hint)
                End Try
            End If

            '添加到特定 Java 列表
            If UserJava IsNot Nothing Then
                Log($"[Java] 用户指定的 Java：{UserJava}")
                TargetJavaList.Add(UserJava)
            End If

#End Region

RetryGet:
            '等待进行中的搜索结束
            If JavaSearchLoader.State <> LoadState.Finished AndAlso JavaSearchLoader.State <> LoadState.Waiting Then JavaSearchLoader.WaitForExit()
            Select Case JavaSearchLoader.State
                Case LoadState.Failed
                    Throw JavaSearchLoader.Error
                Case LoadState.Aborted
                    Throw New ThreadInterruptedException("Java 搜索加载器已中断")
            End Select

            '生成完整的 Java 列表
            Dim AllJavaList As New List(Of JavaEntry)
            AllJavaList.AddRange(TargetJavaList)
            AllJavaList.AddRange(JavaList)

            '根据选定条件进行过滤
            For Each Java In AllJavaList
                If MinVersion IsNot Nothing AndAlso Java.Version < MinVersion Then Continue For
                If MaxVersion IsNot Nothing AndAlso Java.Version > MaxVersion Then Continue For
                If Java.Is64Bit AndAlso Is32BitSystem Then Continue For
                AllowedJavaList.Add(Java)
            Next

            '若未找到适合的 Java，尝试触发搜索
            If Not AllowedJavaList.Any() AndAlso JavaSearchLoader.State = LoadState.Waiting Then
                Log("[Java] 未找到满足条件的 Java，尝试进行搜索")
                JavaSearchLoader.Start(IsForceRestart:=True)
                GoTo RetryGet
            End If

#Region "检查用户指定的 Java 是否可用"

            '确保指定的 Java 可用
            If UserJava Is Nothing Then GoTo ExitUserJavaCheck
            If AllowedJavaList.Any(Function(j) j.PathFolder = UserJava.PathFolder) Then
                Log("[Java] 使用用户指定的 Java：" & UserJava.PathFolder)
                AllowedJavaList = New List(Of JavaEntry) From {UserJava}
                GoTo UserPass
            End If

            '指定的 Java 不可用，弹窗要求选择
            Log("[Java] 发现用户指定的不兼容 Java：" & UserJava.ToString)
            Log($"[Java] 目前实际可用的 Java 列表：")
            For Each Java In AllowedJavaList
                Log($"[Java]  - {Java}")
            Next
            Dim Requirement As String = ""
            Dim ShowRevision As Boolean = False
            If (MinVersion Is Nothing OrElse MinVersion.Minor = 0) AndAlso (MaxVersion IsNot Nothing AndAlso MaxVersion.Minor < 999) Then
                ShowRevision = MaxVersion.MinorRevision < 999
                Requirement = "最高兼容到 Java " & MaxVersion.Minor & If(ShowRevision, "." & MaxVersion.MajorRevision & "." & MaxVersion.MinorRevision, "")
            ElseIf (MinVersion IsNot Nothing AndAlso MinVersion.Minor > 0) AndAlso (MaxVersion Is Nothing OrElse MaxVersion.Minor >= 999) Then
                ShowRevision = MinVersion.MinorRevision > 0 OrElse MinVersion.MajorRevision > 0
                Requirement = "至少需要 Java " & MinVersion.Minor & If(ShowRevision, "." & MinVersion.MajorRevision & "." & MinVersion.MinorRevision, "")
            ElseIf (MinVersion IsNot Nothing AndAlso MinVersion.Minor > 0) AndAlso (MaxVersion IsNot Nothing AndAlso MaxVersion.Minor < 999) Then
                ShowRevision = MinVersion.MinorRevision > 0 OrElse MinVersion.MajorRevision > 0 OrElse MaxVersion.MinorRevision < 999
                Dim Left As String = MinVersion.Minor & If(ShowRevision, "." & MinVersion.MajorRevision & "." & MinVersion.MinorRevision, "")
                Dim Right As String = MaxVersion.Minor & If(ShowRevision, "." & MaxVersion.MajorRevision & "." & MaxVersion.MinorRevision, "")
                Requirement = "需要 Java " & If(Left = Right, Left, Left & " ~ " & Right)
            End If
            Dim JavaCurrent As String = UserJava.VersionCode & If(ShowRevision, "." & UserJava.Version.MajorRevision & "." & UserJava.Version.MinorRevision, "")
            If RelatedVersion IsNot Nothing AndAlso Setup.Get("VersionAdvanceJava", RelatedVersion) Then
                '直接跳过弹窗
                Log("[Java] 设置中指定了使用 Java " & JavaCurrent & "，但当前版本" & Requirement & "，这可能会导致游戏崩溃！", LogLevel.Debug)
                AllowedJavaList = New List(Of JavaEntry) From {UserJava}
            Else
                Select Case MyMsgBox("你在设置中手动指定了使用 Java " & JavaCurrent & "，但当前" & Requirement & "。" & vbCrLf &
                            "如果强制使用该 Java，可能导致游戏崩溃。" & vbCrLf &
                            "你也可以将 游戏 Java 设置修改为 自动选择合适的 Java。" & vbCrLf &
                            vbCrLf &
                            " - 指定的 Java：" & UserJava.ToString,
                            "Java 兼容性警告", "让 PCL 自动选择", "强制使用该 Java", "取消")
                    Case 1 '让 PCL 自动选择
                    Case 2 '强制使用指定的 Java
                        Log("[Java] 已强制使用用户指定的不兼容 Java")
                        AllowedJavaList = New List(Of JavaEntry) From {UserJava}
                    Case 3 '取消启动
                        Throw New Exception(CancelException)
                End Select
            End If

ExitUserJavaCheck:
#End Region

            '若依然未找到适合的 Java，直接返回
            If Not AllowedJavaList.Any() Then Return Nothing

            '优先使用特定目录下的 Java
            For Each Java In AllowedJavaList
                '如果在官启文件夹启动，会将官启自带 Java 错误视作 MC 文件夹指定 Java，导致了 #2054 的第二例
                If Java.PathFolder.Contains(".minecraft\cache\java") Then Continue For
                If Java.PathFolder.Contains("PCL\MyDownload\") Then Continue For '#5780
                If TargetJavaList.Contains(Java) Then
                    '直接使用指定的 Java
                    AllowedJavaList = New List(Of JavaEntry) From {Java}
                    Log("[Java] 优先使用特定路径下的 Java：" & Java.ToString)
                    GoTo UserPass
                End If
            Next
UserPass:

            '对适合的 Java 进行排序
            AllowedJavaList = AllowedJavaList.Sort(AddressOf JavaSorter)
            Log($"[Java] 排序后的 Java 优先顺序：")
            For Each Java In AllowedJavaList
                Log($"[Java]  - {Java}")
            Next

            '检查选定的 Java，若测试失败则尝试进行搜索
            Dim SelectedJava = AllowedJavaList.First
            Try
                SelectedJava.Check()
            Catch ex As ThreadInterruptedException
                Throw
            Catch ex As Exception
                If ex.InnerException IsNot Nothing AndAlso TypeOf (ex.InnerException) Is ThreadInterruptedException Then Throw ex.InnerException
                Log(ex, "最终选定的 Java 已无法使用，尝试进行搜索")
                AllowedJavaList = New List(Of JavaEntry)
                JavaSearchLoader.Start(IsForceRestart:=True)
                GoTo RetryGet
            End Try

            '返回
            Log("[Java] 最终选定的 Java：" & AllowedJavaList.First.ToString)
            Return SelectedJava

        Catch ex As ThreadInterruptedException
            Log(ex, "查找符合条件的 Java 时出现加载器中断")
            Return Nothing
        Catch ex As Exception
            If ex.Message = "$$" Then Throw ex
            Log(ex, "查找符合条件的 Java 失败", LogLevel.Feedback)
            Return Nothing
        End Try
    End Function
    ''' <summary>
    ''' 是否强制指定了 64 位 Java。如果没有强制指定，返回是否安装了 64 位 Java。
    ''' </summary>
    Public Function JavaIs64Bit(Optional RelatedVersion As McVersion = Nothing) As Boolean
        Try
            '检查强制指定
            Dim UserSetup As String = Setup.Get("LaunchArgumentJavaSelect")
            If RelatedVersion IsNot Nothing Then
                Dim UserSetupVersion As String = Setup.Get("VersionArgumentJavaSelect", Version:=RelatedVersion)
                If UserSetupVersion <> "使用全局设置" Then UserSetup = UserSetupVersion
            End If
            If UserSetup <> "" Then
                Dim UserJava As JavaEntry = Nothing
                Try
                    UserJava = JavaEntry.FromJson(GetJson(UserSetup))
                Catch ex As Exception
                    Log(ex, "版本指定的 Java 信息已损坏，已重置版本设置中指定的 Java")
                    Setup.Set("VersionArgumentJavaSelect", "使用全局设置", Version:=RelatedVersion)
                    GoTo NoUserJava
                End Try
                For Each Java In JavaList
                    If Java.PathFolder = UserJava.PathFolder Then Return UserJava.Is64Bit
                Next
            End If
NoUserJava:
            '检查列表
            For Each Java In JavaList
                If Java.Is64Bit Then Return True
            Next
            Return False
        Catch ex As Exception
            Log(ex, "检查 Java 类别时出错", LogLevel.Feedback)
            Setup.Set("LaunchArgumentJavaSelect", "")
            Return True
        End Try
    End Function
    ''' <summary>
    ''' 将 Java 按照适用性排序。
    ''' </summary>
    Public Function JavaSorter(Left As JavaEntry, Right As JavaEntry) As Boolean
        '1. 尽量在当前文件夹或当前 Minecraft 文件夹
        Dim ProgramPathParent As String, MinecraftPathParent As String = ""
        ProgramPathParent = If(New DirectoryInfo(Path).Parent, New DirectoryInfo(Path)).FullName
        If PathMcFolder <> "" Then MinecraftPathParent = If(New DirectoryInfo(PathMcFolder).Parent, New DirectoryInfo(PathMcFolder)).FullName
        If Left.PathFolder.StartsWithF(ProgramPathParent) AndAlso Not Right.PathFolder.StartsWithF(ProgramPathParent) Then Return True
        If Not Left.PathFolder.StartsWithF(ProgramPathParent) AndAlso Right.PathFolder.StartsWithF(ProgramPathParent) Then Return False
        If PathMcFolder <> "" Then
            If Left.PathFolder.StartsWithF(MinecraftPathParent) AndAlso Not Right.PathFolder.StartsWithF(MinecraftPathParent) Then Return True
            If Not Left.PathFolder.StartsWithF(MinecraftPathParent) AndAlso Right.PathFolder.StartsWithF(MinecraftPathParent) Then Return False
        End If
        '2. 尽量使用 64 位
        If Left.Is64Bit AndAlso Not Right.Is64Bit Then Return True
        If Not Left.Is64Bit AndAlso Right.Is64Bit Then Return False
        '3. 尽量不使用 JDK
        If Left.IsJre AndAlso Not Right.IsJre Then Return True
        If Not Left.IsJre AndAlso Right.IsJre Then Return False
        '4. Java 大版本
        If Left.VersionCode <> Right.VersionCode Then
            '                             Java  7   8   9  10  11  12 13 14 15  16  17  18  19  20  21  22  23...
            Dim Weight = {0, 1, 2, 3, 4, 5, 6, 14, 30, 10, 12, 15, 13, 9, 8, 7, 11, 31, 29, 16, 17, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18}
            Return Weight.ElementAtOrDefault(Left.VersionCode) >= Weight.ElementAtOrDefault(Right.VersionCode)
        End If
        '5. 最次级版本号更接近 51
        Return Math.Abs(Left.Version.Revision - 51) <= Math.Abs(Right.Version.Revision - 51)
    End Function

#Region "搜索"

    ''' <summary>
    ''' 模糊搜索并获取所有可用的 Java，并在结束后更新设置页面显示。输出将直接写入 JavaList。
    ''' </summary>
    Public JavaSearchLoader As New LoaderTask(Of Integer, Integer)("查找 Java", AddressOf JavaSearchLoaderSub) With {.ProgressWeight = 2}
    Private Sub JavaSearchLoaderSub(Loader As LoaderTask(Of Integer, Integer))
        If FrmSetupLaunch IsNot Nothing Then
            RunInUiWait(
            Sub()
                FrmSetupLaunch.ComboArgumentJava.Items.Clear()
                FrmSetupLaunch.ComboArgumentJava.Items.Add(New ComboBoxItem With {.Content = "加载中……", .IsSelected = True})
            End Sub)
        End If
        If FrmVersionSetup IsNot Nothing Then
            RunInUiWait(
            Sub()
                FrmVersionSetup.ComboArgumentJava.Items.Clear()
                FrmVersionSetup.ComboArgumentJava.Items.Add(New ComboBoxItem With {.Content = "加载中……", .IsSelected = True})
            End Sub)
        End If

        Try

            '可能包含 Java 的文件夹列表，以 “\” 结尾，且仅包含 “\”
            'Key：文件夹地址
            'Value: 是否为玩家手动导入
            Dim JavaPreList As New Dictionary(Of String, Boolean)

#Region "模糊查找可能可用的 Java"

            '查找环境变量中的 Java
            For Each PathInEnv As String In Split((PathEnv & ";" & PathJavaHome).Replace("\\", "\").Replace("/", "\"), ";")
                PathInEnv = PathInEnv.Trim(" """.ToCharArray())
                If PathInEnv = "" Then Continue For
                If Not PathInEnv.EndsWithF("\") Then PathInEnv += "\"
                '粗略检查有效性
                If File.Exists(PathInEnv & "javaw.exe") Then JavaPreList(PathInEnv) = False
            Next
            '查找磁盘中的 Java
            For Each Disk As DriveInfo In DriveInfo.GetDrives()
                If Disk.DriveType = DriveType.Network Then Continue For '跳过网络驱动器（#3705）
                JavaSearchFolder(Disk.Name, JavaPreList, False)
            Next
            '查找 APPDATA 文件夹中的 Java
            JavaSearchFolder(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\", JavaPreList, False)
            JavaSearchFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) & "\", JavaPreList, False)
            '查找启动器目录中的 Java
            JavaSearchFolder(Path, JavaPreList, False, IsFullSearch:=True)
            '查找所选 Minecraft 文件夹中的 Java
            If Not String.IsNullOrWhiteSpace(PathMcFolder) AndAlso Path <> PathMcFolder Then
                JavaSearchFolder(PathMcFolder, JavaPreList, False, IsFullSearch:=True)
            End If

            '若不全为符号链接，则清除符号链接的地址
            Dim JavaWithoutReparse As New Dictionary(Of String, Boolean)
            For Each Pair In JavaPreList
                Dim Folder As String = Pair.Key.Replace("\\", "\").Replace("/", "\")
                Dim Info As FileSystemInfo = New FileInfo(Folder & "javaw.exe")
                Do
                    If Info.Attributes.HasFlag(FileAttributes.ReparsePoint) Then
                        Log("[Java] 位于 " & Folder & " 的 Java 包含符号链接")
                        Continue For
                    End If
                    Info = If(TypeOf Info Is FileInfo, CType(Info, FileInfo).Directory, CType(Info, DirectoryInfo).Parent)
                Loop While Info IsNot Nothing
                Log("[Java] 位于 " & Folder & " 的 Java 不含符号链接")
                JavaWithoutReparse.Add(Pair.Key, Pair.Value)
            Next
            If JavaWithoutReparse.Any Then JavaPreList = JavaWithoutReparse

            '若不全为特殊引用，则清除特殊引用的地址
            Dim JavaWithoutInherit As New Dictionary(Of String, Boolean)
            For Each Pair In JavaPreList
                If Pair.Key.Contains("java8path_target_") OrElse Pair.Key.Contains("javapath_target_") OrElse Pair.Key.Contains("javatmp") OrElse Pair.Key.ContainsF("system32") Then
                    Log("[Java] 位于 " & Pair.Key & " 的 Java 位于特殊路径，不应优先使用")
                Else
                    JavaWithoutInherit.Add(Pair.Key, Pair.Value)
                End If
            Next
            If JavaWithoutInherit.Any Then JavaPreList = JavaWithoutInherit

#End Region

#Region "添加玩家手动导入的 Java"

            Dim ImportedJava As String = Setup.Get("LaunchArgumentJavaAll")
            Try
                For Each JavaJsonObject In GetJson(ImportedJava)
                    Dim Entry = JavaEntry.FromJson(JavaJsonObject)
                    If Entry.IsUserImport Then JavaPreList(Entry.PathFolder) = True
                Next
            Catch ex As Exception
                Log(ex, "Java 列表已损坏", LogLevel.Feedback)
                Setup.Set("LaunchArgumentJavaAll", "[]")
            End Try

#End Region

            '确保可用并获取详细信息，转入正式列表
            Dim NewJavaList As New List(Of JavaEntry)
            For Each Entry In JavaPreList.Distinct(Function(a, b) a.Key.ToLower = b.Key.ToLower) '#794
                NewJavaList.Add(New JavaEntry(Entry.Key, Entry.Value))
            Next
            NewJavaList = JavaCheckList(NewJavaList).Sort(AddressOf JavaSorter)

            '修改设置项
            Dim AllList As New JArray
            For Each Java In NewJavaList
                AllList.Add(Java.ToJson)
            Next
            Setup.Set("LaunchArgumentJavaAll", AllList.ToString(Newtonsoft.Json.Formatting.None))
            JavaList = NewJavaList

        Catch ex As Exception
            Log(ex, "搜索 Java 时出错", LogLevel.Feedback)
            JavaList = New List(Of JavaEntry)
        End Try

        Log("[Java] Java 搜索完成，发现 " & JavaList.Count & " 个 Java")
        If FrmSetupLaunch IsNot Nothing Then RunInUi(Sub() FrmSetupLaunch.RefreshJavaComboBox())
        If FrmVersionSetup IsNot Nothing Then RunInUi(Sub() FrmVersionSetup.RefreshJavaComboBox())
    End Sub

    ''' <summary>
    ''' 多线程检查列表中的所有 Java 项。
    ''' </summary>
    Private Function JavaCheckList(JavaEntries As List(Of JavaEntry)) As List(Of JavaEntry)
        Log("[Java] 开始确认列表 Java 状态，共 " & JavaEntries.Count & " 项")
        JavaCheckList = New List(Of JavaEntry)
        Dim ListLock As New Object

        '启动检查线程
        Dim CheckThreads As New List(Of Thread)
        For Each Entry In JavaEntries
            Dim CheckThread As New Thread(
            Sub()
                Try
                    Entry.Check()
                    If ModeDebug Then Log("[Java]  - " & Entry.ToString)
                    SyncLock ListLock
                        JavaCheckList.Add(Entry)
                    End SyncLock
                Catch ex As ThreadInterruptedException
                Catch ex As Exception
                    If Entry.IsUserImport Then
                        Log(ex, "位于 " & Entry.PathFolder & " 的 Java 存在异常，将被自动移除", LogLevel.Hint)
                    Else
                        Log(ex, "位于 " & Entry.PathFolder & " 的 Java 存在异常")
                    End If
                End Try
            End Sub)
            CheckThreads.Add(CheckThread)
            CheckThread.Start()
        Next

        '等待构造线程完成
Wait:
        Thread.Sleep(10)
        For Each CheckThread In CheckThreads
            If CheckThread.IsAlive Then GoTo Wait
        Next
    End Function
    ''' <summary>
    ''' 模糊搜索指定文件夹下的 Java，并只进行粗略的检查。这不会搜索全部路径。
    ''' </summary>
    ''' <param name="OriginalPath">开始搜索的起始路径，不限制结尾。</param>
    ''' <param name="IsFullSearch">搜索当前文件夹下的全部文件夹（此参数不会传递到子文件夹）。</param>
    Private Sub JavaSearchFolder(OriginalPath As String, ByRef Results As Dictionary(Of String, Boolean), Source As Boolean, Optional IsFullSearch As Boolean = False)
        Try
            Log("[Java] 开始" & If(IsFullSearch, "完全", "部分") & "遍历查找：" & OriginalPath)
            JavaSearchFolder(New DirectoryInfo(ShortenPath(OriginalPath)), Results, Source, IsFullSearch)
        Catch ex As UnauthorizedAccessException
            Log("[Java] 遍历查找 Java 时遭遇无权限的文件夹：" & OriginalPath)
        Catch ex As Exception
            Log(ex, "遍历查找 Java 时出错（" & OriginalPath & "）")
        End Try
    End Sub
    ''' <summary>
    ''' 模糊搜索指定文件夹下的 Java，并只进行粗略的检查。这不会搜索全部路径。
    ''' </summary>
    ''' <param name="OriginalPath">开始搜索的起始路径，不限制结尾。</param>
    ''' <param name="IsFullSearch">搜索当前文件夹下的全部文件夹（此参数不会传递到子文件夹）。</param>
    Private Sub JavaSearchFolder(OriginalPath As DirectoryInfo, ByRef Results As Dictionary(Of String, Boolean), Source As Boolean, Optional IsFullSearch As Boolean = False)
        Try
            '确认目录存在
            If Not OriginalPath.Exists Then Return
            Dim Path As String = OriginalPath.FullName.Replace("\\", "\")
            If Not Path.EndsWithF("\") Then Path += "\"
            '若该目录有 Java，则加入结果
            If File.Exists(Path & "javaw.exe") Then Results(Path) = Source
            '查找其下的所有文件夹
            '不应使用网易的 Java：https://github.com/Hex-Dragon/PCL2/issues/1279#issuecomment-2761489121
            Dim Keywords = {"java", "jdk", "env", "环境", "run", "软件", "jre", "mc", "dragon",
                            "soft", "cache", "temp", "corretto", "roaming", "users", "craft", "program", "世界", "net",
                            "游戏", "oracle", "game", "file", "data", "jvm", "服务", "server", "客户", "client", "整合",
                            "应用", "运行", "前置", "mojang", "官启", "新建文件夹", "eclipse", "microsoft", "hotspot",
                            "runtime", "x86", "x64", "forge", "原版", "optifine", "官方", "启动", "hmcl", "mod", "高清",
                            "download", "launch", "程序", "path", "version", "baka", "pcl", "zulu", "local", "packages",
                            "4297127d64ec6", "1.", "启动"}
            For Each FolderInfo As DirectoryInfo In OriginalPath.EnumerateDirectories
                If FolderInfo.Attributes.HasFlag(FileAttributes.ReparsePoint) Then Continue For '跳过符号链接
                Dim SearchEntry = GetFolderNameFromPath(FolderInfo.Name).ToLower '用于搜索的字符串
                If IsFullSearch OrElse
                   OriginalPath.Name.ToLower = "users" OrElse Val(SearchEntry) > 0 OrElse Keywords.Any(Function(w) SearchEntry.Contains(w)) OrElse SearchEntry = "bin" Then
                    JavaSearchFolder(FolderInfo, Results, Source)
                End If
            Next
        Catch ex As UnauthorizedAccessException
            Log("[Java] 遍历查找 Java 时遭遇无权限的文件夹：" & OriginalPath.FullName)
        Catch ex As Exception
            Log(ex, "遍历查找 Java 时出错（" & OriginalPath.FullName & "）")
        End Try
    End Sub

#End Region

#Region "下载"

    ''' <summary>
    ''' 提示 Java 缺失，并弹窗确认是否自动下载。返回玩家选择是否下载。
    ''' </summary>
    Public Function JavaDownloadConfirm(VersionDescription As String, Optional ForcedManualDownload As Boolean = False) As Boolean
        If ForcedManualDownload Then
            MyMsgBox($"PCL 未找到 {VersionDescription}。" & vbCrLf &
                     $"请自行搜索并安装 {VersionDescription}，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                     "未找到 Java")
            Return False
        Else
            Return MyMsgBox($"PCL 未找到 {VersionDescription}，是否需要 PCL 自动下载？" & vbCrLf &
                            $"如果你已经安装了 {VersionDescription}，可以在 设置 → 启动选项 → 游戏 Java 中手动导入。",
                            "自动下载 Java？", "自动下载", "取消") = 1
        End If
    End Function

    ''' <summary>
    ''' 获取下载 Java 8/14/17/21 的加载器。需要开启 IsForceRestart 以正常刷新 Java 列表。
    ''' </summary>
    Public Function JavaFixLoaders(Version As Integer) As LoaderCombo(Of Integer)
        Dim JavaDownloadLoader As New LoaderDownload("下载 Java 文件", New List(Of NetFile)) With {.ProgressWeight = 10}
        Dim Loader = New LoaderCombo(Of Integer)($"下载 Java {Version}", {
            New LoaderTask(Of Integer, List(Of NetFile))("获取 Java 下载信息", AddressOf JavaFileList) With {.ProgressWeight = 2},
            JavaDownloadLoader,
            JavaSearchLoader
        })
        AddHandler JavaDownloadLoader.OnStateChangedThread,
        Sub(Raw As LoaderBase, NewState As LoadState, OldState As LoadState)
            If (NewState = LoadState.Failed OrElse NewState = LoadState.Aborted) AndAlso LastJavaBaseDir IsNot Nothing Then
                Log($"[Java] 由于下载未完成，清理未下载完成的 Java 文件：{LastJavaBaseDir}", LogLevel.Debug)
                DeleteDirectory(LastJavaBaseDir)
            ElseIf NewState = LoadState.Finished Then
                LastJavaBaseDir = Nothing
            End If
        End Sub
        JavaDownloadLoader.HasOnStateChangedThread = True
        Return Loader
    End Function
    Private LastJavaBaseDir As String = Nothing '用于在下载中断或失败时删除未完成下载的 Java 文件夹，防止残留只下了一半但 -version 能跑的 Java
    Private Sub JavaFileList(Loader As LoaderTask(Of Integer, List(Of NetFile)))
        Log("[Java] 开始获取 Java 下载信息")
        Dim IndexFileStr As String = NetGetCodeByLoader(DlVersionListOrder(
            {"https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"},
            {"https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"}
        ), IsJson:=True)
        '获取下载地址
        Dim MainEntry As JObject = CType(GetJson(IndexFileStr), JObject)($"windows-x{If(Is32BitSystem, "86", "64")}")
        Dim Entries = MainEntry.Children.Reverse. '选择最靠后的一项（最新）
            SelectMany(Function(e As JProperty) CType(e.Value, JArray).Select(Function(v) New KeyValuePair(Of String, JObject)(e.Name, v)))
        Dim TargetEntry = Entries.First(Function(t) t.Value("version")("name").ToString.StartsWithF(Loader.Input))
        Dim Address As String = TargetEntry.Value("manifest")("url")
        Log($"[Java] 准备下载 Java {TargetEntry.Value("version")("name")}（{TargetEntry.Key}）：{Address}")
        '获取文件列表
        Dim ListFileStr As String = NetGetCodeByLoader(DlSourceOrder({Address}, {Address.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com")}), IsJson:=True)
        LastJavaBaseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\runtime\" & TargetEntry.Key & "\"
        Dim Results As New List(Of NetFile)
        For Each File As JProperty In CType(GetJson(ListFileStr), JObject)("files")
            If CType(File.Value, JObject)("downloads")?("raw") Is Nothing Then Continue For
            Dim Info As JObject = CType(File.Value, JObject)("downloads")("raw")
            Dim Checker As New FileChecker(ActualSize:=Info("size"), Hash:=Info("sha1"))
            If Checker.Hash = "12976a6c2b227cbac58969c1455444596c894656" OrElse Checker.Hash = "c80e4bab46e34d02826eab226a4441d0970f2aba" OrElse Checker.Hash = "84d2102ad171863db04e7ee22a259d1f6c5de4a5" Then
                '跳过 3 个无意义大量重复文件（#3827）
                Continue For
            End If
            If Checker.Check(LastJavaBaseDir & File.Name) Is Nothing Then Continue For '跳过已存在的文件
            Dim Url As String = Info("url")
            Results.Add(New NetFile(DlSourceOrder({Url}, {Url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com")}), LastJavaBaseDir & File.Name, Checker))
        Next
        Loader.Output = Results
        Log($"[Java] 需要下载 {Results.Count} 个文件，目标文件夹：{LastJavaBaseDir}")
    End Sub

#End Region

End Module
