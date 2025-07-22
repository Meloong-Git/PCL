Public Class CrashAnalyzer

    '构造函数
    Private TempFolder As String
    Public Sub New(UUID As Integer)
        '构建文件结构
        TempFolder = RequestTaskTempFolder()
        Directory.CreateDirectory(TempFolder & "Temp\")
        Directory.CreateDirectory(TempFolder & "Report\")
        Log("[Crash] 崩溃分析暂存文件夹：" & TempFolder)
    End Sub

    '1：准备用于分析的 Log 文件
    Private AnalyzeRawFiles As New List(Of KeyValuePair(Of String, String())) '暂存的日志文件：文件完整路径 -> 文件内容
    ''' <summary>
    ''' 将可用于分析的日志存储到 AnalyzeRawFiles。
    ''' </summary>
    ''' <param name="LatestLog">从 PCL 捕获到的最后 200 行程序输出。</param>
    Public Sub Collect(VersionPathIndie As String, Optional LatestLog As IList(Of String) = Nothing)
        Log("[Crash] 步骤 1：收集日志文件")

        '简单收集可能的日志文件路径
        Dim PossibleLogs As New List(Of String)
        Try
            Dim DirInfo As New DirectoryInfo(VersionPathIndie & "crash-reports\")
            If DirInfo.Exists Then
                For Each File In DirInfo.EnumerateFiles
                    PossibleLogs.Add(File.FullName)
                Next
            End If
        Catch ex As Exception
            Log(ex, "收集 Minecraft 崩溃日志文件夹下的日志失败")
        End Try
        Try
            For Each File In New DirectoryInfo(VersionPathIndie).Parent.Parent.EnumerateFiles
                If If(File.Extension, "") <> ".log" Then Continue For
                PossibleLogs.Add(File.FullName)
            Next
        Catch ex As Exception
            Log(ex, "收集 Minecraft 主文件夹下的日志失败")
        End Try
        Try
            For Each File In New DirectoryInfo(VersionPathIndie).EnumerateFiles
                If If(File.Extension, "") <> ".log" Then Continue For
                PossibleLogs.Add(File.FullName)
            Next
        Catch ex As Exception
            Log(ex, "收集 Minecraft 隔离文件夹下的日志失败")
        End Try
        PossibleLogs.Add(VersionPathIndie & "logs\latest.log") 'Minecraft 日志
        PossibleLogs.Add(VersionPathIndie & "logs\debug.log") 'Minecraft Debug 日志
        PossibleLogs = PossibleLogs.Distinct.ToList

        '确定最新的日志文件
        Dim RightLogs As New List(Of String)
        For Each LogFile In PossibleLogs
            Try
                Dim Info As New FileInfo(LogFile)
                If Not Info.Exists Then Continue For
                Dim Time = Math.Abs((Info.LastWriteTime - Date.Now).TotalMinutes)
                If Time < 3 AndAlso Info.Length > 0 Then
                    RightLogs.Add(LogFile)
                    Log("[Crash] 可能可用的日志文件：" & LogFile & "（" & Math.Round(Time, 1) & " 分钟）")
                End If
            Catch ex As Exception
                Log(ex, "确认崩溃日志时间失败（" & LogFile & "）")
            End Try
        Next
        If Not RightLogs.Any() Then Log("[Crash] 未发现可能可用的日志文件")

        '将可能可用的日志文件导出
        For Each FilePath In RightLogs
            Try
                AnalyzeRawFiles.Add(New KeyValuePair(Of String, String())(FilePath, ReadFile(FilePath).Split(vbCrLf.ToCharArray)))
            Catch ex As Exception
                Log(ex, "读取可能的崩溃日志文件失败（" & FilePath & "）")
            End Try
        Next
        If LatestLog IsNot Nothing AndAlso LatestLog.Any Then
            Dim RawOutput As String = Join(LatestLog, vbCrLf)
            Log("[Crash] 以下为游戏输出的最后一段内容：" & vbCrLf & RawOutput)
            WriteFile(TempFolder & "RawOutput.log", RawOutput)
            AnalyzeRawFiles.Add(New KeyValuePair(Of String, String())(TempFolder & "RawOutput.log", LatestLog.ToArray))
            LatestLog.Clear()
        End If

        Log("[Crash] 步骤 1：收集日志文件完成，收集到 " & AnalyzeRawFiles.Count & " 个文件")
    End Sub
    ''' <summary>
    ''' 从文件路径直接导入日志文件或崩溃报告压缩包。
    ''' </summary>
    Public Sub Import(FilePath As String)
        Log("[Crash] 步骤 1：自主导入日志文件")

        '尝试视作压缩包解压
        Try
            Dim Info As New FileInfo(FilePath)
            If Info.Exists AndAlso Info.Length > 0 AndAlso Not FilePath.EndsWithF(".jar", True) Then
                ExtractFile(FilePath, TempFolder & "Temp\")
                Log("[Crash] 已解压导入的日志文件：" & FilePath)
                GoTo Extracted
            End If
        Catch
        End Try
        '并非压缩包
        CopyFile(FilePath, TempFolder & "Temp\" & GetFileNameFromPath(FilePath))
        Log("[Crash] 已复制导入的日志文件：" & FilePath)
Extracted:

        '导入其中的日志文件
        For Each TargetFile As FileInfo In New DirectoryInfo(TempFolder & "Temp\").EnumerateFiles.ToList()
            Try
                If Not TargetFile.Exists OrElse TargetFile.Length = 0 Then Continue For
                Dim Ext As String = TargetFile.Extension.ToLower
                If Ext = ".log" OrElse Ext = ".txt" Then
                    AnalyzeRawFiles.Add(New KeyValuePair(Of String, String())(TargetFile.FullName, ReadFile(TargetFile.FullName).Split(vbCrLf.ToCharArray)))
                Else
                    File.Delete(TargetFile.FullName)
                End If
            Catch ex As Exception
                Log(ex, "导入单个日志文件失败")
            End Try
        Next

        Log("[Crash] 步骤 1：自主导入日志文件，收集到 " & AnalyzeRawFiles.Count & " 个文件")
    End Sub

    '2：确认实际用于分析的 Log 文本
    Private Enum AnalyzeFileType
        HsErr
        MinecraftLog
        ExtraLogFile
        ExtraReportFile
        CrashReport
    End Enum
    Private DirectFile As KeyValuePair(Of String, String())? = Nothing '在弹窗中选择直接打开的文件
    ''' <summary>
    ''' 从 AnalyzeRawFiles 中提取实际有用的文本片段存储到 AnalyzeFiles，并整理可用于生成报告的文件。
    ''' 返回是否有足够信息可用于分析。
    ''' </summary>
    Public Function Prepare() As Boolean
        Log("[Crash] 步骤 2：准备日志文本")

        '对日志文件进行分类
        DirectFile = Nothing
        Dim AllFiles As New List(Of KeyValuePair(Of AnalyzeFileType, KeyValuePair(Of String, String())))
        For Each LogFile In AnalyzeRawFiles
            Dim MatchName As String = GetFileNameFromPath(LogFile.Key).ToLower
            Dim TargetType As AnalyzeFileType
            If MatchName.StartsWithF("hs_err") Then
                TargetType = AnalyzeFileType.HsErr
                DirectFile = LogFile
            ElseIf MatchName.StartsWithF("crash-") Then
                TargetType = AnalyzeFileType.CrashReport
                DirectFile = LogFile
            ElseIf MatchName = "latest.log" OrElse MatchName = "latest log.txt" OrElse
                   MatchName = "debug.log" OrElse MatchName = "debug log.txt" OrElse
                   MatchName = "游戏崩溃前的输出.txt" OrElse MatchName = "rawoutput.log" Then
                TargetType = AnalyzeFileType.MinecraftLog
                If DirectFile Is Nothing Then DirectFile = LogFile
            ElseIf MatchName = "启动器日志.txt" OrElse MatchName = "PCL2 启动器日志.txt" OrElse MatchName = "PCL 启动器日志.txt" OrElse MatchName = "log1.txt" Then
                If LogFile.Value.Any(Function(s) s.Contains("以下为游戏输出的最后一段内容")) Then
                    TargetType = AnalyzeFileType.MinecraftLog
                    If DirectFile Is Nothing Then DirectFile = LogFile
                Else
                    TargetType = AnalyzeFileType.ExtraLogFile
                End If
            ElseIf MatchName.EndsWithF(".log", True) Then
                TargetType = AnalyzeFileType.ExtraLogFile
            ElseIf MatchName.EndsWithF(".txt", True) Then
                TargetType = AnalyzeFileType.ExtraReportFile
            Else
                Log("[Crash] " & MatchName & " 分类为 Ignore")
                Continue For
            End If
            If LogFile.Value.Any Then
                AllFiles.Add(New KeyValuePair(Of AnalyzeFileType, KeyValuePair(Of String, String()))(TargetType, LogFile))
                Log("[Crash] " & MatchName & " 分类为 " & GetStringFromEnum(TargetType))
            Else
                Log("[Crash] " & MatchName & " 由于内容为空跳过")
            End If
        Next

        '若只有额外日志，则将它们视作 Minecraft 日志
        If AllFiles.Any() AndAlso AllFiles.All(Function(p) p.Key = AnalyzeFileType.ExtraLogFile) Then
            Log("[Crash] 由于仅发现了额外日志，将它们视作 Minecraft 日志进行分析")
            AllFiles = AllFiles.Select(Function(p) New KeyValuePair(Of AnalyzeFileType, KeyValuePair(Of String, String()))(AnalyzeFileType.MinecraftLog, p.Value)).ToList
        End If

        '将分类后的文件分别写入
        For Each SelectType In {AnalyzeFileType.MinecraftLog, AnalyzeFileType.HsErr, AnalyzeFileType.ExtraLogFile, AnalyzeFileType.CrashReport}
            '获取该种类的所有文件 {文件路径 -> 文件内容行}
            Dim SelectedFiles As New List(Of KeyValuePair(Of String, String()))
            For Each File In AllFiles
                If SelectType = File.Key Then SelectedFiles.Add(File.Value)
            Next
            If Not SelectedFiles.Any() Then Continue For
            Try
                '根据文件类别判断
                Select Case SelectType
                    Case AnalyzeFileType.HsErr, AnalyzeFileType.CrashReport
                        '获取文件的修改日期
                        Dim DatedFiles As New SortedList(Of Date, KeyValuePair(Of String, String()))()
                        For Each File In SelectedFiles
                            Try
                                DatedFiles.Add(New FileInfo(File.Key).LastWriteTime, File)
                            Catch ex As Exception
                                Log(ex, "获取日志文件修改时间失败")
                                DatedFiles.Add(New Date(1900, 1, 1), File)
                            End Try
                        Next
                        '输出最新的文件
                        Dim NewestFile As KeyValuePair(Of String, String()) = DatedFiles.Last.Value
                        OutputFiles.Add(NewestFile.Key)
                        If SelectType = AnalyzeFileType.HsErr Then
                            LogHs = GetHeadTailLines(NewestFile.Value, 200, 100)
                            Log("[Crash] 输出报告：" & NewestFile.Key & "，作为虚拟机错误信息")
                            Log("[Crash] 导入分析：" & NewestFile.Key & "，作为虚拟机错误信息")
                        Else
                            LogCrash = GetHeadTailLines(NewestFile.Value, 300, 700)
                            Log("[Crash] 输出报告：" & NewestFile.Key & "，作为 Minecraft 崩溃报告")
                            Log("[Crash] 导入分析：" & NewestFile.Key & "，作为 Minecraft 崩溃报告")
                        End If
                    Case AnalyzeFileType.MinecraftLog
                        LogMc = ""
                        LogMcDebug = ""
                        '创建文件名词典
                        Dim FileNameDict As New Dictionary(Of String, KeyValuePair(Of String, String()))
                        For Each SelectedFile In SelectedFiles
                            FileNameDict(GetFileNameFromPath(SelectedFile.Key).ToLower) = SelectedFile
                            OutputFiles.Add(SelectedFile.Key)
                            Log("[Crash] 输出报告：" & SelectedFile.Key & "，作为 Minecraft 或启动器日志")
                        Next
                        '选择一份最佳的来自启动器的游戏日志
                        For Each FileName As String In {"rawoutput.log", "启动器日志.txt", "log1.txt", "游戏崩溃前的输出.txt", "PCL2 启动器日志.txt", "PCL 启动器日志.txt"}
                            If Not FileNameDict.ContainsKey(FileName) Then Continue For
                            Dim CurrentLog = FileNameDict(FileName)
                            '截取 “以下为游戏输出的最后一段内容” 后的内容
                            Dim HasLauncherMark As Boolean = False
                            For Each Line In CurrentLog.Value
                                If HasLauncherMark Then
                                    LogMc += Line & vbLf
                                ElseIf Line.Contains("以下为游戏输出的最后一段内容") Then
                                    HasLauncherMark = True
                                    Log("[Crash] 找到 PCL 输出的游戏实时日志头")
                                End If
                            Next
                            '导入后 500 行
                            If Not HasLauncherMark Then LogMc += GetHeadTailLines(CurrentLog.Value, 0, 500)
                            LogMc = LogMc.TrimEnd(vbCrLf.ToCharArray)
                            Log("[Crash] 导入分析：" & CurrentLog.Key & "，作为启动器日志")
                            Exit For
                        Next
                        '选择一份最佳的 Minecraft Log
                        For Each FileName As String In {"latest.log", "latest log.txt", "debug.log", "debug log.txt"}
                            If Not FileNameDict.ContainsKey(FileName) Then Continue For
                            Dim CurrentLog = FileNameDict(FileName)
                            LogMc += GetHeadTailLines(CurrentLog.Value, 1500, 500)
                            Log("[Crash] 导入分析：" & CurrentLog.Key & "，作为 Minecraft 日志")
                            Exit For
                        Next
                        '查找 Debug Log
                        For Each FileName As String In {"debug.log", "debug log.txt"}
                            If Not FileNameDict.ContainsKey(FileName) Then Continue For
                            Dim CurrentLog = FileNameDict(FileName)
                            LogMcDebug += GetHeadTailLines(CurrentLog.Value, 1000, 0)
                            Log("[Crash] 导入分析：" & CurrentLog.Key & "，作为 Minecraft Debug 日志")
                            Exit For
                        Next
                        '兜底
                        If LogMc = "" Then
                            If LogMcDebug <> "" Then '如果没有找到 Minecraft 日志，则使用 Debug 日志作为兜底
                                LogMc = LogMcDebug
                            ElseIf FileNameDict.Any() Then '如果都没有找到，则使用第一个文件
                                Dim CurrentLog = FileNameDict.First.Value
                                LogMc += GetHeadTailLines(CurrentLog.Value, 1500, 500)
                                Log("[Crash] 导入分析：" & CurrentLog.Key & "，作为兜底日志")
                            Else
                                LogMc = Nothing
                                Throw New Exception("无法找到匹配的 Minecraft Log")
                            End If
                        End If
                        If LogMcDebug = "" Then LogMcDebug = Nothing
                    Case AnalyzeFileType.ExtraLogFile, AnalyzeFileType.ExtraReportFile
                        '全部丢过去
                        For Each SelectedFile In SelectedFiles
                            OutputFiles.Add(SelectedFile.Key)
                            Log("[Crash] 输出报告：" & SelectedFile.Key & "，不用作分析")
                        Next
                End Select
            Catch ex As Exception
                Log(ex, "分类处理日志文件时出错")
            End Try
        Next

        '结束
        Prepare = LogMc IsNot Nothing OrElse LogHs IsNot Nothing OrElse LogCrash IsNot Nothing
        If Prepare Then
            Log(("[Crash] 步骤 2：准备日志文本完成，找到" & If(LogMc Is Nothing, "", "游戏日志、") & If(LogMcDebug Is Nothing, "", "游戏 Debug 日志、") & If(LogHs Is Nothing, "", "虚拟机日志、") & If(LogCrash Is Nothing, "", "崩溃日志、")).TrimEnd("、") & "用作分析")
        Else
            Log("[Crash] 步骤 2：准备日志文本完成，没有任何可供分析的日志")
        End If
    End Function
    ''' <summary>
    ''' 输出字符串的前后某些行，并统一行尾为 vbLf (正则 \n)、删除空行和重复行。
    ''' </summary>
    Private Function GetHeadTailLines(Raw As String(), HeadLines As Integer, TailLines As Integer) As String
        If Raw.Length <= HeadLines + TailLines Then Return Join(Raw.Distinct, vbLf)
        Dim Lines As New List(Of String)
        Dim RealHeadLines As Integer = 0, ViewedLines As Integer
        For ViewedLines = 0 To Raw.Length - 1
            If Lines.Contains(Raw(ViewedLines)) Then Continue For
            RealHeadLines += 1
            Lines.Add(Raw(ViewedLines))
            If RealHeadLines >= HeadLines Then Exit For
        Next
        Dim RealTailLines = 0
        For i = Raw.Length - 1 To ViewedLines Step -1
            If Lines.Contains(Raw(i)) Then Continue For
            RealTailLines += 1
            Lines.Insert(RealHeadLines, Raw(i))
            If RealTailLines >= TailLines Then Exit For
        Next
        Dim Result As New StringBuilder
        For Each Line In Lines
            If Line = "" Then Continue For
            Result.Append(Line)
            Result.Append(vbLf)
        Next
        Return Result.ToString
    End Function

    '3：根据文本分析崩溃原因
    Private LogMc As String = Nothing, LogMcDebug As String = Nothing, LogHs As String = Nothing, LogCrash As String = Nothing
    Private LogAll As String
    '可能导致崩溃的原因与附加信息
    Private CrashReasons As New Dictionary(Of CrashReason, List(Of String))
    ''' <summary>
    ''' 导致崩溃的原因枚举。
    ''' </summary>
    Private Enum CrashReason
        Mod文件被解压
        MixinBootstrap缺失
        内存不足
        使用JDK
        显卡不支持OpenGL
        使用OpenJ9
        Java版本过高
        Java版本不兼容
        Mod名称包含特殊字符
        显卡驱动不支持导致无法设置像素格式
        极短的程序输出
        Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION 'https://bugs.mojang.com/browse/MC-32606
        AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION 'https://bugs.mojang.com/browse/MC-31618
        Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION
        玩家手动触发调试崩溃
        光影或资源包导致OpenGL1282错误
        文件或内容校验失败
        确定Mod导致游戏崩溃
        怀疑Mod导致游戏崩溃
        Mod配置文件导致游戏崩溃
        ModMixin失败
        Mod加载器报错
        Mod初始化失败
        堆栈分析发现关键字
        堆栈分析发现Mod名称
        OptiFine导致无法加载世界 'https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
        特定方块导致崩溃
        特定实体导致崩溃
        材质过大或显卡配置不足
        没有可用的分析文件
        使用32位Java导致JVM无法分配足够多的内存
        Mod重复安装
        Mod互不兼容
        OptiFine与Forge不兼容
        Fabric报错
        Fabric报错并给出解决方案
        Forge报错
        低版本Forge与高版本Java不兼容
        版本Json中存在多个Forge
        Mod过多导致超出ID限制
        NightConfig的Bug
        ShadersMod与OptiFine同时安装
        Forge安装不完整
        Mod需要Java11
        Mod缺少前置或MC版本错误
    End Enum
    ''' <summary>
    ''' 根据 AnalyzeLogs 与可能的版本信息分析崩溃原因。
    ''' </summary>
    Public Sub Analyze(Optional Version As McVersion = Nothing)
        Log("[Crash] 步骤 3：分析崩溃原因")
        LogAll = If(LogMc, If(LogMcDebug, "")) & If(LogHs, "") & If(LogCrash, "")

        '1. 精准日志匹配，中/高优先级
        AnalyzeCrit1()
        If CrashReasons.Any Then GoTo Done
        AnalyzeCrit2()
        If CrashReasons.Any Then GoTo Done

        '2. 堆栈分析
        If LogAll.Contains("orge") OrElse LogAll.Contains("abric") OrElse LogAll.Contains("uilt") OrElse LogAll.Contains("iteloader") Then
            Dim Keywords As New List(Of String)
            '崩溃日志
            If LogCrash IsNot Nothing Then
                Log("[Crash] 开始进行崩溃日志堆栈分析")
                Keywords.AddRange(AnalyzeStackKeyword(LogCrash.BeforeFirst("System Details")))
            End If
            'Minecraft 日志
            If LogMc IsNot Nothing Then
                Dim Fatals As List(Of String) = RegexSearch(LogMc, "/FATAL] .+?(?=[\n]+\[)")
                If LogMc.Contains("Unreported exception thrown!") Then Fatals.Add(LogMc.Between("Unreported exception thrown!", "at oolloo.jlw.Wrapper"))
                Log("[Crash] 开始进行 Minecraft 日志堆栈分析，发现 " & Fatals.Count & " 个报错项")
                For Each Fatal In Fatals
                    Keywords.AddRange(AnalyzeStackKeyword(Fatal))
                Next
            End If
            '虚拟机日志
            If LogHs IsNot Nothing Then
                Log("[Crash] 开始进行虚拟机堆栈分析")
                Dim StackLogs As String = LogHs.Between("T H R E A D", "Registers:")
                Keywords.AddRange(AnalyzeStackKeyword(StackLogs))
            End If
            'Mod 名称分析
            If Keywords.Any Then
                Dim Names = AnalyzeModName(Keywords)
                If Names Is Nothing Then
                    AppendReason(CrashReason.堆栈分析发现关键字, Keywords)
                Else
                    AppendReason(CrashReason.堆栈分析发现Mod名称, Names)
                End If
                GoTo Done
            End If
        Else
            Log("[Crash] 可能并未安装 Mod，不进行堆栈分析")
        End If

        '3. 精准日志匹配，低优先级
        AnalyzeCrit3()

        '输出到日志
Done:
        If Not CrashReasons.Any() Then
            Log("[Crash] 步骤 3：分析崩溃原因完成，未找到可能的原因")
        Else
            Log("[Crash] 步骤 3：分析崩溃原因完成，找到 " & CrashReasons.Count & " 条可能的原因")
            For Each Reason In CrashReasons
                Log("[Crash]  - " & GetStringFromEnum(Reason.Key) & If(Reason.Value.Any, "（" & Join(Reason.Value, "；") & "）", ""))
            Next
        End If
    End Sub
    ''' <summary>
    ''' 增加一个可能的崩溃原因。
    ''' </summary>
    Private Sub AppendReason(Reason As CrashReason, Optional Additional As ICollection(Of String) = Nothing)
        If CrashReasons.ContainsKey(Reason) Then
            If Additional IsNot Nothing Then
                CrashReasons(Reason).AddRange(Additional)
                CrashReasons(Reason) = CrashReasons(Reason).Distinct.ToList
            End If
        Else
            CrashReasons.Add(Reason, New List(Of String)(If(Additional, {})))
        End If
        Log("[Crash] 可能的崩溃原因：" & GetStringFromEnum(Reason) & If(Additional IsNot Nothing AndAlso Additional.Any, "（" & Join(Additional, "；") & "）", ""))
    End Sub
    Private Sub AppendReason(Reason As CrashReason, Additional As String)
        AppendReason(Reason, If(String.IsNullOrEmpty(Additional), Nothing, New List(Of String) From {Additional}))
    End Sub

    '具体的分析代码
    ''' <summary>
    ''' 进行精准日志匹配。匹配优先级高于堆栈分析的崩溃。
    ''' </summary>
    Private Sub AnalyzeCrit1()

        '空白分析
        If LogMc Is Nothing AndAlso LogHs Is Nothing AndAlso LogCrash Is Nothing Then
            AppendReason(CrashReason.没有可用的分析文件)
            Return
        End If

        '崩溃报告分析，高优先级
        If LogCrash IsNot Nothing Then
            If LogCrash.Contains("Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass") Then AppendReason(CrashReason.Java版本过高)
        End If

        '游戏日志分析
        If LogMc IsNot Nothing Then
            If LogMc.Contains("Found multiple arguments for option fml.forgeVersion, but you asked for only one") Then AppendReason(CrashReason.版本Json中存在多个Forge)
            If LogMc.Contains("The driver does not appear to support OpenGL") Then AppendReason(CrashReason.显卡不支持OpenGL)
            If LogMc.Contains("java.lang.ClassCastException: java.base/jdk") Then AppendReason(CrashReason.使用JDK)
            If LogMc.Contains("java.lang.ClassCastException: class jdk.") Then AppendReason(CrashReason.使用JDK)
            If LogMc.Contains("TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            If LogMc.Contains("Open J9 is not supported") OrElse LogMc.Contains("OpenJ9 is incompatible") OrElse LogMc.Contains(".J9VMInternals.") Then AppendReason(CrashReason.使用OpenJ9)
            If LogMc.Contains("java.lang.NoSuchFieldException: ucp") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("because module java.base does not export") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory") Then AppendReason(CrashReason.Java版本过高)
            If LogMc.Contains("The directories below appear to be extracted jar files. Fix this before you continue.") Then AppendReason(CrashReason.Mod文件被解压)
            If LogMc.Contains("Extracted mod jars found, loading will NOT continue") Then AppendReason(CrashReason.Mod文件被解压)
            If LogMc.Contains("java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker") Then AppendReason(CrashReason.MixinBootstrap缺失)
            If LogMc.Contains("Couldn't set pixel format") Then AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式)
            If LogMc.Contains("java.lang.OutOfMemoryError") OrElse LogMc.Contains("an out of memory error") Then AppendReason(CrashReason.内存不足)
            If LogMc.Contains("java.lang.RuntimeException: Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders.") Then AppendReason(CrashReason.ShadersMod与OptiFine同时安装)
            If LogMc.Contains("java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier") Then AppendReason(CrashReason.低版本Forge与高版本Java不兼容)
            If LogMc.Contains("1282: Invalid operation") Then AppendReason(CrashReason.光影或资源包导致OpenGL1282错误)
            If LogMc.Contains("signer information does not match signer information of other classes in the same package") Then AppendReason(CrashReason.文件或内容校验失败, If(RegexSeek(LogMc, "(?<=class "")[^']+(?=""'s signer information)"), "").TrimEnd(vbCrLf))
            If LogMc.Contains("Maybe try a lower resolution resourcepack?") Then AppendReason(CrashReason.材质过大或显卡配置不足)
            If LogMc.Contains("java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z") AndAlso LogMc.Contains("OptiFine") Then AppendReason(CrashReason.OptiFine导致无法加载世界)
            If LogMc.Contains("Unsupported class file major version") Then AppendReason(CrashReason.Java版本不兼容)
            If LogMc.Contains("com.electronwill.nightconfig.core.io.ParsingException: Not enough data available") Then AppendReason(CrashReason.NightConfig的Bug)
            If LogMc.Contains("Cannot find launch target fmlclient, unable to launch") Then AppendReason(CrashReason.Forge安装不完整)
            If LogMc.Contains("Invalid paths argument, contained no existing paths") AndAlso LogMc.Contains("libraries\net\minecraftforge\fmlcore") Then AppendReason(CrashReason.Forge安装不完整)
            If LogMc.Contains("Invalid module name: '' is not a Java identifier") Then AppendReason(CrashReason.Mod名称包含特殊字符)
            If LogMc.Contains("has been compiled by a more recent version of the Java Runtime (class file version 55.0), this version of the Java Runtime only recognizes class file versions up to") Then AppendReason(CrashReason.Mod需要Java11)
            If LogMc.Contains("java.lang.RuntimeException: java.lang.NoSuchMethodException: no such method: sun.misc.Unsafe.defineAnonymousClass(Class,byte[],Object[])Class/invokeVirtual") Then AppendReason(CrashReason.Mod需要Java11)
            If LogMc.Contains("java.lang.IllegalArgumentException: The requested compatibility level JAVA_11 could not be set. Level is not supported by the active JRE or ASM version") Then AppendReason(CrashReason.Mod需要Java11)
            If LogMc.Contains("Unsupported major.minor version") Then AppendReason(CrashReason.Java版本不兼容)
            If LogMc.Contains("Invalid maximum heap size") Then AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存)
            If LogMc.Contains("Could not reserve enough space") Then
                If LogMc.Contains("for 1048576KB object heap") Then
                    AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存)
                Else
                    AppendReason(CrashReason.内存不足)
                End If
            End If
            '确定的 Mod 导致崩溃
            If LogMc.Contains("Caught exception from ") Then AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(RegexSeek(LogMc, "(?<=Caught exception from )[^\n]+?")?.TrimEnd((vbCrLf & " ").ToCharArray)))
            'Mod 重复 / 前置问题
            If LogMc.Contains("DuplicateModsFoundException") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(LogMc, "(?<=\n\t[\w]+ : [A-Z]{1}:[^\n]+(/|\\))[^/\\\n]+?.jar", RegularExpressions.RegexOptions.IgnoreCase))
            If LogMc.Contains("Found a duplicate mod") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(If(RegexSeek(LogMc, "Found a duplicate mod[^\n]+"), ""), "[^\\/]+.jar", RegularExpressions.RegexOptions.IgnoreCase))
            If LogMc.Contains("Found duplicate mods") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(LogMc, "(?<=Mod ID: ')\w+?(?=' from mod files:)").Distinct.ToList)
            If LogMc.Contains("ModResolutionException: Duplicate") Then AppendReason(CrashReason.Mod重复安装, RegexSearch(If(RegexSeek(LogMc, "ModResolutionException: Duplicate[^\n]+"), ""), "[^\\/]+.jar", RegularExpressions.RegexOptions.IgnoreCase))
            If LogMc.Contains("Incompatible mods found!") Then '#5006
                AppendReason(CrashReason.Mod互不兼容, If(RegexSeek(LogMc, "(?<=Incompatible mods found![\s\S]+: )[\s\S]+?(?=\tat )"), ""))
            End If
            If LogMc.Contains("Missing or unsupported mandatory dependencies:") Then
                AppendReason(CrashReason.Mod缺少前置或MC版本错误,
                    RegexSearch(LogMc, "(?<=Missing or unsupported mandatory dependencies:)([\n\r]+\t(.*))+", RegularExpressions.RegexOptions.IgnoreCase).
                    Select(Function(s) s.Trim((vbCrLf & vbTab & " ").ToCharArray)).Distinct().ToList())
            End If
        End If

        '虚拟机日志分析
        If LogHs IsNot Nothing Then
            If LogHs.Contains("The system is out of physical RAM or swap space") Then AppendReason(CrashReason.内存不足)
            If LogHs.Contains("Out of Memory Error") Then AppendReason(CrashReason.内存不足)
            If LogHs.Contains("EXCEPTION_ACCESS_VIOLATION") Then
                If LogHs.Contains("# C  [ig") Then AppendReason(CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION)
                If LogHs.Contains("# C  [atio") Then AppendReason(CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION)
                If LogHs.Contains("# C  [nvoglv") Then AppendReason(CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION)
            End If
        End If

        '崩溃报告分析
        If LogCrash IsNot Nothing Then
            If LogCrash.Contains("maximum id range exceeded") Then AppendReason(CrashReason.Mod过多导致超出ID限制)
            If LogCrash.Contains("java.lang.OutOfMemoryError") Then AppendReason(CrashReason.内存不足)
            If LogCrash.Contains("Pixel format not accelerated") Then AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式)
            If LogCrash.Contains("Manually triggered debug crash") Then AppendReason(CrashReason.玩家手动触发调试崩溃)
            If LogCrash.Contains("has mods that were not found") AndAlso RegexCheck(LogCrash, "The Mod File [^\n]+optifine\\OptiFine[^\n]+ has mods that were not found") Then AppendReason(CrashReason.OptiFine与Forge不兼容)
            'Mod 导致的崩溃
            If LogCrash.Contains("-- MOD ") Then
                Dim LogCrashMod As String = LogCrash.Between("-- MOD ", "Failure message:")
                If LogCrashMod.ContainsF(".jar", True) Then
                    AppendReason(CrashReason.确定Mod导致游戏崩溃, If(RegexSeek(LogCrashMod, "(?<=Mod File: ).+"), "").TrimEnd((vbCrLf & " ").ToCharArray))
                Else
                    AppendReason(CrashReason.Mod加载器报错, If(RegexSeek(LogCrash, "(?<=Failure message: )[\w\W]+?(?=\tMod)"), "").Replace(vbTab, " ").TrimEnd((vbCrLf & " ").ToCharArray))
                End If
            End If
            If LogCrash.Contains("Multiple entries with same key: ") Then AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(If(RegexSeek(LogCrash, "(?<=Multiple entries with same key: )[^=]+"), "").TrimEnd((vbCrLf & " ").ToCharArray)))
            If LogCrash.Contains("LoaderExceptionModCrash: Caught exception from ") Then AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(If(RegexSeek(LogCrash, "(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+"), "").TrimEnd((vbCrLf & " ").ToCharArray)))
            If LogCrash.Contains("Failed loading config file ") Then AppendReason(CrashReason.Mod配置文件导致游戏崩溃, {TryAnalyzeModName(If(RegexSeek(LogCrash, "(?<=Failed loading config file .+ for modid )[^\n]+"), "").TrimEnd(vbCrLf)).First, If(RegexSeek(LogCrash, "(?<=Failed loading config file ).+(?= of type)"), "").TrimEnd(vbCrLf)})
        End If

    End Sub
    ''' <summary>
    ''' 进行精准日志匹配。匹配优先级高于堆栈分析的崩溃，但低于上面的。
    ''' 如果第一步已经找到了原因则不执行该检测。
    ''' </summary>
    Private Sub AnalyzeCrit2()

        'Mixin 分析
        Dim MixinAnalyze =
        Function(LogText As String) As Boolean
            Dim IsMixin As Boolean =
                LogText.Contains("Mixin prepare failed ") OrElse LogText.Contains("Mixin apply failed ") OrElse
                LogText.Contains("MixinApplyError") OrElse LogText.Contains("MixinTransformerError") OrElse
                LogText.Contains("mixin.injection.throwables.") OrElse LogText.Contains(".json] FAILED during )")
            If Not IsMixin Then Return False
            'Mod 名称匹配
            Dim ModName As String = RegexSeek(LogText, "(?<=from mod )[^.\/ ]+(?=\] from)")
            If ModName Is Nothing Then ModName = RegexSeek(LogText, "(?<=for mod )[^.\/ ]+(?= failed)")
            If ModName IsNot Nothing Then
                AppendReason(CrashReason.ModMixin失败, TryAnalyzeModName(ModName.TrimEnd((vbCrLf & " ").ToCharArray)))
                Return True
            End If
            'JSON 名称匹配
            For Each JsonName In RegexSearch(LogText, "(?<=^[^\t]+[ \[{(]{1})[^ \[{(]+\.[^ ]+(?=\.json)", RegularExpressions.RegexOptions.Multiline)
                AppendReason(CrashReason.ModMixin失败,
                             TryAnalyzeModName(JsonName.Replace("mixins", "mixin").Replace(".mixin", "").Replace("mixin.", "")))
                Return True
            Next
            '没有明确匹配
            AppendReason(CrashReason.ModMixin失败)
            Return True
        End Function

        '游戏日志分析
        If LogMc IsNot Nothing Then
            'Mixin 崩溃
            Dim IsMixin As Boolean = MixinAnalyze(LogMc)
            '常规信息
            If LogMc.Contains("An exception was thrown, the game will display an error screen and halt.") Then AppendReason(CrashReason.Forge报错, RegexSeek(LogMc, "(?<=the game will display an error screen and halt.[\n\r]+[^\n]+?Exception: )[\s\S]+?(?=\n\tat)")?.Trim(vbCrLf))
            If LogMc.Contains("A potential solution has been determined:") Then AppendReason(CrashReason.Fabric报错并给出解决方案, Join(RegexSearch(If(RegexSeek(LogMc, "(?<=A potential solution has been determined:\n)(\s+ - [^\n]+\n)+"), ""), "(?<=\s+)[^\n]+"), vbLf))
            If LogMc.Contains("A potential solution has been determined, this may resolve your problem:") Then AppendReason(CrashReason.Fabric报错并给出解决方案, Join(RegexSearch(If(RegexSeek(LogMc, "(?<=A potential solution has been determined, this may resolve your problem:\n)(\s+ - [^\n]+\n)+"), ""), "(?<=\s+)[^\n]+"), vbLf))
            If LogMc.Contains("确定了一种可能的解决方法，这样做可能会解决你的问题：") Then AppendReason(CrashReason.Fabric报错并给出解决方案, Join(RegexSearch(If(RegexSeek(LogMc, "(?<=确定了一种可能的解决方法，这样做可能会解决你的问题：\n)(\s+ - [^\n]+\n)+"), ""), "(?<=\s+)[^\n]+"), vbLf))
            If Not IsMixin AndAlso LogMc.Contains("due to errors, provided by ") Then '在 #3104 的情况下，这一句导致 OptiFabric 的 Mixin 失败错判为 Fabric Loader 加载失败
                AppendReason(CrashReason.确定Mod导致游戏崩溃, TryAnalyzeModName(If(RegexSeek(LogMc, "(?<=due to errors, provided by ')[^']+"), "").TrimEnd((vbCrLf & " ").ToCharArray)))
            End If
        End If

        '崩溃报告分析
        If LogCrash IsNot Nothing Then
            'Mixin 崩溃
            MixinAnalyze(LogCrash)
            '常规信息
            If LogCrash.Contains("Suspected Mod") Then
                Dim SuspectsRaw As String = LogCrash.Between("Suspected Mod", "Stacktrace")
                If Not SuspectsRaw.StartsWithF("s: None") Then 'Suspected Mods: None
                    Dim Suspects = RegexSearch(SuspectsRaw, "(?<=\n\t[^(\t]+\()[^)\n]+")
                    If Suspects.Any Then AppendReason(CrashReason.怀疑Mod导致游戏崩溃, TryAnalyzeModName(Suspects))
                End If
            End If
        End If

    End Sub
    ''' <summary>
    ''' 进行精准日志匹配。匹配优先级低于堆栈分析的崩溃。
    ''' </summary>
    Private Sub AnalyzeCrit3()

        '游戏日志分析
        If LogMc IsNot Nothing Then
            '极短的程序输出
            If Not (LogMc.Contains("at net.") OrElse LogMc.Contains("INFO]")) AndAlso LogHs Is Nothing AndAlso LogCrash Is Nothing AndAlso LogMc.Length < 100 Then
                AppendReason(CrashReason.极短的程序输出, LogMc)
            End If
            'Mod 解析错误（常见于 Fabric 前置校验失败）
            If LogMc.Contains("Mod resolution failed") Then AppendReason(CrashReason.Mod加载器报错)
            'Mixin 失败可以导致大量 Mod 实例创建失败
            If LogMc.Contains("Failed to create mod instance.") Then AppendReason(CrashReason.Mod初始化失败, TryAnalyzeModName(If(RegexSeek(LogMc, "(?<=Failed to create mod instance. ModID: )[^,]+"), If(RegexSeek(LogMc, "(?<=Failed to create mod instance. ModId )[^\n]+(?= for )"), "")).TrimEnd(vbCrLf)))
            '注意：Fabric 的 Warnings were found! 不一定是崩溃原因，它可能是单纯的警报
        End If

        '崩溃报告分析
        If LogCrash IsNot Nothing Then
            If LogCrash.Contains(vbTab & "Block location: World: ") Then AppendReason(CrashReason.特定方块导致崩溃, If(RegexSeek(LogCrash, "(?<=\tBlock: Block\{)[^\}]+"), "") & " " & If(RegexSeek(LogCrash, "(?<=\tBlock location: World: )\([^\)]+\)"), ""))
            If LogCrash.Contains(vbTab & "Entity's Exact location: ") Then AppendReason(CrashReason.特定实体导致崩溃, If(RegexSeek(LogCrash, "(?<=\tEntity Type: )[^\n]+(?= \()"), "") & " (" & If(RegexSeek(LogCrash, "(?<=\tEntity's Exact location: )[^\n]+"), "").TrimEnd(vbCrLf.ToCharArray) & ")")
        End If

    End Sub
    ''' <summary>
    ''' 从堆栈中提取 Mod ID 关键字。若失败则返回空列表。
    ''' </summary>
    Private Function AnalyzeStackKeyword(ErrorStack As String) As List(Of String)
        ErrorStack = vbLf & If(ErrorStack, "") & vbLf

        '进行正则匹配
        Dim StackSearchResults As New List(Of String)
        StackSearchResults.AddRange(RegexSearch(ErrorStack, "(?<=\n[^{]+)[a-zA-Z_]+\w+\.[a-zA-Z_]+[\w\.]+(?=\.[\w\.$]+\.)"))
        StackSearchResults.AddRange(RegexSearch(ErrorStack, "(?<=at [^(]+?\.\w+\$\w+\$)[\w\$]+?(?=\$\w+\()").Select(Function(s) s.Replace("$", "."))) 'Mixin 堆栈：xxx.xxx.xxxx$xxxx$xxx
        StackSearchResults = StackSearchResults.Distinct.ToList

        '检查堆栈开头
        Dim PossibleStacks As New List(Of String)
        For Each Stack As String In StackSearchResults
            'If Not Stack.Contains(".") Then Continue For
            For Each IgnoreStack In {
                "java", "sun", "javax", "jdk", "oolloo",
                "org.lwjgl", "com.sun", "net.minecraftforge", "paulscode.sound", "com.mojang", "net.minecraft", "cpw.mods", "com.google", "org.apache", "org.spongepowered", "net.fabricmc", "com.mumfrey",
                "com.electronwill.nightconfig", "it.unimi.dsi",
                "MojangTricksIntelDriversForPerformance_javaw"}
                If Stack.StartsWithF(IgnoreStack) Then GoTo NextStack
            Next
            PossibleStacks.Add(Stack.Trim)
NextStack:
        Next
        PossibleStacks = PossibleStacks.Distinct.ToList
        Log("[Crash] 找到 " & PossibleStacks.Count & " 条可能的堆栈信息")
        If Not PossibleStacks.Any() Then Return New List(Of String)
        For Each Stack As String In PossibleStacks
            Log("[Crash]  - " & Stack)
        Next

        '检查堆栈关键词
        Dim PossibleWords As New List(Of String)
        For Each Stack As String In PossibleStacks
            Dim Splited = Stack.Split(".")
            For i = 0 To Math.Min(3, Splited.Count - 1) '最多取前 4 节
                Dim Word As String = Splited(i)
                If Word.Length <= 2 OrElse Word.StartsWithF("func_") Then Continue For
                If {"com", "org", "net", "asm", "fml", "mod", "jar", "sun", "lib", "map", "gui", "dev", "nio", "api", "dsi", "top", "mcp",
                    "core", "init", "mods", "main", "file", "game", "load", "read", "done", "util", "tile", "item", "base", "oshi", "impl", "data", "pool", "task",
                    "forge", "setup", "block", "model", "mixin", "event", "unimi", "netty", "world", "lwjgl",
                    "gitlab", "common", "server", "config", "mixins", "compat", "loader", "launch", "entity", "assist", "client", "plugin", "modapi", "mojang", "shader", "events", "github", "recipe", "render", "packet", "events",
                    "preinit", "preload", "machine", "reflect", "channel", "general", "handler", "content", "systems", "modules", "service",
                    "fastutil", "optifine", "internal", "platform", "override", "fabricmc", "neoforge",
                    "injection", "listeners", "scheduler", "minecraft", "universal", "multipart", "neoforged", "microsoft",
                    "transformer", "transformers", "minecraftforge", "blockentity", "spongepowered", "electronwill"
                   }.Contains(Word.ToLower) Then Continue For
                PossibleWords.Add(Word.Trim)
            Next
        Next
        PossibleWords = PossibleWords.Distinct.ToList
        Log("[Crash] 从堆栈信息中找到 " & PossibleWords.Count & " 个可能的 Mod ID 关键词")
        If PossibleWords.Any Then Log("[Crash]  - " & Join(PossibleWords, ", "))
        If PossibleWords.Count > 10 Then
            Log("[Crash] 关键词过多，考虑匹配出错，不纳入考虑")
            Return New List(Of String)
        Else
            Return PossibleWords
        End If

    End Function
    ''' <summary>
    ''' 根据 Mod 关键词尝试获取实际的 Mod 名称。
    ''' 若失败则返回 Nothing。
    ''' </summary>
    Private Function AnalyzeModName(Keywords As List(Of String)) As List(Of String)
        Dim ModFileNames As New List(Of String)

        '预处理关键词（分割括号）
        Dim RealKeywords As New List(Of String)
        For Each Keyword In Keywords
            For Each SubKeyword In Keyword.Split("(")
                RealKeywords.Add(SubKeyword.Trim(" )".ToCharArray))
            Next
        Next
        Keywords = RealKeywords

        '从崩溃报告获取 Mod 信息
        If LogCrash IsNot Nothing AndAlso LogCrash.Contains("A detailed walkthrough of the error") Then
            Dim Details As String = LogCrash.Replace("A detailed walkthrough of the error", "¨")
            Dim IsFabricDetail As Boolean = Details.Contains("Fabric Mods") '是否为 Fabric 信息格式
            If IsFabricDetail Then
                Details = Details.Replace("Fabric Mods", "¨")
                Log("[Crash] 崩溃报告中检测到 Fabric Mod 信息格式")
            End If
            Details = Details.AfterLast("¨")

            '[Forge] 获取所有包含 .jar 的行
            '[Fabric] 获取所有包含 Mod 信息的行
            Dim ModNameLines As New List(Of String)
            For Each Line In Details.Split(vbLf)
                If (Line.ContainsF(".jar", True) AndAlso Line.Length - Line.Replace(".jar", "").Length = 4) OrElse '只有一个 .jar
                   (IsFabricDetail AndAlso Line.StartsWithF(vbTab & vbTab) AndAlso Not RegexCheck(Line, "\t\tfabric[\w-]*: Fabric")) Then ModNameLines.Add(Line)
            Next
            Log("[Crash] 崩溃报告中找到 " & ModNameLines.Count & " 个可能的 Mod 项目行")

            '获取 Mod ID 与关键词的匹配行
            Dim HintLines As New List(Of String)
            For Each KeyWord As String In Keywords
                For Each ModString As String In ModNameLines
                    Dim RealModString As String = ModString.ToLower.Replace("_", "")
                    If Not RealModString.Contains(KeyWord.ToLower.Replace("_", "")) Then Continue For
                    If RealModString.Contains("minecraft.jar") OrElse RealModString.Contains(" forge-") OrElse RealModString.Contains(" mixin-") Then Continue For
                    HintLines.Add(ModString.Trim(vbCrLf.ToCharArray))
                    Exit For
                Next
            Next
            HintLines = HintLines.Distinct.ToList
            Log("[Crash] 崩溃报告中找到 " & HintLines.Count & " 个可能的崩溃 Mod 匹配行")
            For Each ModLine As String In HintLines
                Log("[Crash]  - " & ModLine)
            Next

            '从 Mod 匹配行中提取 .jar 文件的名称
            For Each Line As String In HintLines
                Dim Name As String
                If IsFabricDetail Then
                    Name = RegexSeek(Line, "(?<=: )[^\n]+(?= [^\n]+)")
                Else
                    Name = RegexSeek(Line, "(?<=\()[^\t]+.jar(?=\))|(?<=(\t\t)|(\| ))[^\t\|]+.jar", RegularExpressions.RegexOptions.IgnoreCase)
                End If
                If Name IsNot Nothing Then ModFileNames.Add(Name)
            Next

        End If

        '从 debug.log 获取 Mod 信息
        If LogMcDebug IsNot Nothing Then

            'Forge: Found valid mod file YungsBetterStrongholds-1.20-Forge-4.0.1.jar with {betterstrongholds} mods - versions {1.20-Forge-4.0.1}
            Dim ModNameLines As List(Of String) = RegexSearch(LogMcDebug, "(?<=valid mod file ).*", RegularExpressions.RegexOptions.Multiline)
            Log("[Crash] Debug 信息中找到 " & ModNameLines.Count & " 个可能的 Mod 项目行")

            '获取 Mod ID 与关键词的匹配行
            Dim HintLines As New List(Of String)
            For Each KeyWord As String In Keywords
                For Each ModString As String In ModNameLines
                    If ModString.Contains($"{{{KeyWord}}}") Then HintLines.Add(ModString)
                Next
            Next
            HintLines = HintLines.Distinct.ToList
            Log("[Crash] Debug 信息中找到 " & HintLines.Count & " 个可能的崩溃 Mod 匹配行")
            For Each ModLine As String In HintLines
                Log("[Crash]  - " & ModLine)
            Next

            '从 Mod 匹配行中提取 .jar 文件的名称
            For Each Line As String In HintLines
                Dim Name As String
                Name = RegexSeek(Line, ".*(?= with)")
                If Name IsNot Nothing Then ModFileNames.Add(Name)
            Next

        End If

        '输出
        ModFileNames = ModFileNames.Distinct.ToList
        If Not ModFileNames.Any() Then
            Return Nothing
        Else
            Log("[Crash] 找到 " & ModFileNames.Count & " 个可能的崩溃 Mod 文件名")
            For Each ModFileName As String In ModFileNames
                Log("[Crash]  - " & ModFileName)
            Next
            Return ModFileNames
        End If
    End Function
    ''' <summary>
    ''' 尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    ''' </summary>
    Private Function TryAnalyzeModName(Keyword As String) As List(Of String)
        Dim RawList As New List(Of String) From {If(Keyword, "")}
        If String.IsNullOrEmpty(Keyword) Then Return RawList
        Return If(AnalyzeModName(RawList), RawList)
    End Function
    ''' <summary>
    ''' 尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    ''' </summary>
    Private Function TryAnalyzeModName(Keywords As List(Of String)) As List(Of String)
        If Not Keywords.Any Then Return Keywords
        Return If(AnalyzeModName(Keywords), Keywords)
    End Function

    '4：根据原因输出信息
    Private OutputFiles As New List(Of String)
    ''' <summary>
    ''' 弹出崩溃弹窗，并指导导出崩溃报告。
    ''' </summary>
    Public Sub Output(IsHandAnalyze As Boolean, Optional ExtraFiles As List(Of String) = Nothing)
        '弹窗提示
        FrmMain.ShowWindowToTop()
        Select Case MyMsgBox(GetAnalyzeResult(IsHandAnalyze), If(IsHandAnalyze, "错误报告分析结果", "Minecraft 出现错误"),
            "确定", If(IsHandAnalyze OrElse DirectFile Is Nothing, "", "查看日志"), If(IsHandAnalyze, "", "导出错误报告"),
            Button2Action:=If(IsHandAnalyze OrElse DirectFile Is Nothing, Nothing,
            Sub()
                '弹窗选择：查看日志
                If File.Exists(DirectFile.Value.Key) Then
                    ShellOnly(DirectFile.Value.Key)
                Else
                    Dim FilePath As String = PathTemp & "Crash.txt"
                    WriteFile(FilePath, Join(DirectFile.Value.Value, vbCrLf))
                    ShellOnly(FilePath)
                End If
            End Sub))
            Case 3
                '弹窗选择：导出错误报告
                Dim FileAddress As String = Nothing
                Try
                    '获取文件路径
                    RunInUiWait(Sub() FileAddress = SelectSaveFile("选择保存位置", "错误报告-" & Date.Now.ToString("G").Replace("/", "-").Replace(":", ".").Replace(" ", "_") & ".zip", "Minecraft 错误报告(*.zip)|*.zip"))
                    If String.IsNullOrEmpty(FileAddress) Then Return
                    Directory.CreateDirectory(GetPathFromFullPath(FileAddress))
                    If File.Exists(FileAddress) Then File.Delete(FileAddress)
                    '输出诊断信息
                    FeedbackInfo()
                    LogFlush()
                    '复制文件
                    If ExtraFiles IsNot Nothing Then OutputFiles.AddRange(ExtraFiles)
                    For Each OutputFile In OutputFiles
                        Dim FileName As String = GetFileNameFromPath(OutputFile)
                        Dim FileEncoding As Encoding = Nothing
                        Select Case FileName
                            Case "LatestLaunch.bat"
                                FileName = "启动脚本.bat"
                            Case "Log1.txt"
                                FileName = "PCL 启动器日志.txt"
                                FileEncoding = Encoding.UTF8
                            Case "RawOutput.log"
                                FileName = "游戏崩溃前的输出.txt"
                                FileEncoding = Encoding.UTF8
                        End Select
                        If File.Exists(OutputFile) Then
                            If FileEncoding Is Nothing Then FileEncoding = GetEncoding(ReadFileBytes(OutputFile))
                            Dim FileContent As String = ReadFile(OutputFile, FileEncoding)
                            FileContent = FilterAccessToken(FileContent, If(FileName = "启动脚本.bat", "F", "*"))
                            FileContent = FilterUserName(FileContent, "*")
                            WriteFile(TempFolder & "Report\" & FileName, FileContent, Encoding:=FileEncoding)
                            Log($"[Crash] 导出文件：{FileName}，编码：{FileEncoding.HeaderName}")
                        End If
                    Next
                    '导出报告
                    Compression.ZipFile.CreateFromDirectory(TempFolder & "Report\", FileAddress)
                    DeleteDirectory(TempFolder & "Report\")
                    Hint("错误报告已导出！", HintType.Finish)
                Catch ex As Exception
                    Log(ex, "导出错误报告失败", LogLevel.Feedback)
                    Return
                End Try
                OpenExplorer(FileAddress)
        End Select
    End Sub
    ''' <summary>
    ''' 获取崩溃分析的结果描述。
    ''' </summary>
    Private Function GetAnalyzeResult(IsHandAnalyze As Boolean) As String

        '没有结果的处理
        If Not CrashReasons.Any() Then
            If IsHandAnalyze Then
                Return "很抱歉，PCL 无法确定错误原因。"
            Else
                Return $"很抱歉，你的游戏出现了一些问题……{vbCrLf}如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。"
            End If
        End If

        '根据不同原因判断
        Dim Results As New List(Of String)
        For Each Reason In CrashReasons
            Dim Additional As List(Of String) = Reason.Value
            Select Case Reason.Key
                Case CrashReason.Mod文件被解压
                    Results.Add("由于 Mod 文件被解压了，导致游戏无法继续运行。\n直接把整个 Mod 文件放进 Mod 文件夹中即可，若解压就会导致游戏出错。\n\n请删除 Mod 文件夹中已被解压的 Mod，然后再启动游戏。")
                Case CrashReason.内存不足
                    Results.Add("Minecraft 内存不足，导致其无法继续运行。\n这很可能是因为电脑内存不足、游戏分配的内存不足，或是配置要求过高。\n\n你可以尝试在 更多 → 百宝箱 中选择 内存优化，然后再启动游戏。\n如果还是不行，请在启动设置中增加为游戏分配的内存，并删除配置要求较高的材质、Mod、光影。\n如果依然不奏效，请在开始游戏前尽量关闭其他软件，或者……换台电脑？\h")
                Case CrashReason.使用OpenJ9
                    Results.Add("游戏因为使用 OpenJ9 而崩溃了。\n请在启动设置的 Java 选择一项中改用非 OpenJ9 的 Java，然后再启动游戏。")
                Case CrashReason.使用JDK
                    Results.Add("游戏似乎因为使用 JDK，或 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用 JRE 8（Java 8），然后再启动游戏。\n如果你没有安装 JRE 8，你可以从网络中下载、安装一个。")
                Case CrashReason.Java版本过高
                    Results.Add("游戏似乎因为你所使用的 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用较低版本的 Java，然后再启动游戏。\n如果没有，可以从网络中下载、安装一个。")
                Case CrashReason.Java版本不兼容
                    Results.Add("游戏不兼容你当前使用的 Java。\n如果没有合适的 Java，可以从网络中下载、安装一个。")
                Case CrashReason.Mod名称包含特殊字符
                    Results.Add("由于有 Mod 的名称包含特殊字符，导致游戏崩溃。\n请尝试修改 Mod 文件名，让它只包含英文字母、数字、减号（-）、下划线（_）和小数点，然后再启动游戏。")
                Case CrashReason.MixinBootstrap缺失
                    Results.Add("由于缺失 MixinBootstrap，导致游戏崩溃。\n请尝试安装 MixinBootstrap。若安装后依然崩溃，可以尝试在文件名前添加英文感叹号。")
                Case CrashReason.使用32位Java导致JVM无法分配足够多的内存
                    If Environment.Is64BitOperatingSystem Then
                        Results.Add("你似乎正在使用 32 位 Java，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n请在启动设置的 Java 选择一项中改用 64 位的 Java 再启动游戏，然后再启动游戏。\n如果你没有安装 64 位的 Java，你可以从网络中下载、安装一个。")
                    Else
                        Results.Add("你正在使用 32 位的操作系统，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n你或许只能重装 64 位的操作系统来解决此问题。\n如果你的电脑内存在 2GB 以内，那或许只能换台电脑了……\h")
                    End If
                Case CrashReason.Mod缺少前置或MC版本错误
                    If Additional.Any Then
                        Results.Add("由于未安装正确的前置 Mod，导致游戏退出。\n缺失的依赖项：\n - " & Join(Additional, "\n - ") & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("由于未安装正确的前置 Mod，导致游戏退出。\n请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.堆栈分析发现关键字
                    If Additional.Count = 1 Then
                        Results.Add("你的游戏遇到了一些问题，PCL 为此找到了一个可疑的关键词：" & Additional.First & "。\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h")
                    Else
                        Results.Add("你的游戏遇到了一些问题，PCL 为此找到了以下可疑的关键词：\n - " & Join(Additional, ", ") & "\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h")
                    End If
                Case CrashReason.堆栈分析发现Mod名称, CrashReason.怀疑Mod导致游戏崩溃
                    If Additional.Count = 1 Then
                        Results.Add("PCL 怀疑名为 " & Additional.First & " 的 Mod 导致了游戏出错，但不能完全确定。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("PCL 怀疑以下 Mod 导致了游戏出错，但不能完全确定：\n - " & Join(Additional, "\n - ") & "\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.确定Mod导致游戏崩溃
                    If Additional.Count = 1 Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 导致了游戏出错。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("以下 Mod 导致了游戏出错：\n - " & Join(Additional, "\n - ") & "\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.ModMixin失败
                    If Additional.Count = 0 Then
                        Results.Add("部分 Mod 注入失败，导致游戏出错。\n这一般代表着部分 Mod 与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试逐步禁用 Mod，然后观察游戏是否还会崩溃，以此定位导致崩溃的 Mod。\n\e\h")
                    ElseIf Additional.Count = 1 Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 注入失败，导致游戏出错。\n这一般代表着它与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("以下 Mod 导致了游戏出错：\n - " & Join(Additional, "\n - ") & "\n这一般代表着它们与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.Mod配置文件导致游戏崩溃
                    If Additional(1) Is Nothing Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 导致了游戏出错。\n\e\h")
                    Else
                        Results.Add("名为 " & Additional.First & " 的 Mod 导致了游戏出错：\n其配置文件 " & Additional(1) & " 存在异常，无法读取。")
                    End If
                Case CrashReason.Mod初始化失败
                    If Additional.Count = 1 Then
                        Results.Add("名为 " & Additional.First & " 的 Mod 初始化失败，导致游戏无法继续加载。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    Else
                        Results.Add("以下 Mod 初始化失败，导致游戏出错：\n - " & Join(Additional, "\n - ") & "\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h")
                    End If
                Case CrashReason.特定方块导致崩溃
                    If Additional.Count = 1 Then
                        Results.Add("游戏似乎因为方块 " & Additional.First & " 出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是该方块导致出错，你或许需要使用一些方式删除此方块。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    Else
                        Results.Add("游戏似乎因为世界中的某些方块出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是某些方块导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    End If
                Case CrashReason.Mod重复安装
                    If Additional.Count >= 2 Then
                        Results.Add("你重复安装了多个相同的 Mod：\n - " & Join(Additional, "\n - ") & "\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。")
                    Else
                        Results.Add("你可能重复安装了多个相同的 Mod，导致游戏出错。\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。\e\h")
                    End If
                Case CrashReason.特定实体导致崩溃
                    If Additional.Count = 1 Then
                        Results.Add("游戏似乎因为实体 " & Additional.First & " 出现了问题。\n\n你可以创建一个新世界，并生成一个该实体，然后观察游戏的运行情况：\n - 若正常运行，则是该实体导致出错，你或许需要使用一些方式删除此实体。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    Else
                        Results.Add("游戏似乎因为世界中的某些实体出现了问题。\n\n你可以创建一个新世界，并生成各种实体，观察游戏的运行情况：\n - 若正常运行，则是某些实体导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h")
                    End If
                Case CrashReason.OptiFine与Forge不兼容
                    Results.Add("由于 OptiFine 与当前版本的 Forge 不兼容，导致了游戏崩溃。\n\n请前往 OptiFine 官网（https://optifine.net/downloads）查看 OptiFine 所兼容的 Forge 版本，并严格按照对应版本重新安装游戏。")
                Case CrashReason.ShadersMod与OptiFine同时安装
                    Results.Add("无需同时安装 OptiFine 和 Shaders Mod，OptiFine 已经集成了 Shaders Mod 的功能。\n在删除 Shaders Mod 后，游戏即可正常运行。")
                Case CrashReason.低版本Forge与高版本Java不兼容
                    Results.Add("由于低版本 Forge 与当前 Java 不兼容，导致了游戏崩溃。\n\n请尝试以下解决方案：\n - 更新 Forge 到 36.2.26 或更高版本\n - 换用版本低于 1.8.0.320 的 Java")
                Case CrashReason.版本Json中存在多个Forge
                    Results.Add("可能由于其他启动器修改了 Forge 版本，当前版本的文件存在异常，导致了游戏崩溃。\n请尝试重新全新安装 Forge，而非使用其他启动器修改 Forge 版本。")
                Case CrashReason.玩家手动触发调试崩溃
                    Results.Add("* 事实上，你的游戏没有任何问题，这是你自己触发的崩溃。\n* 你难道没有更重要的事要做吗？")
                Case CrashReason.Mod需要Java11
                    Results.Add("你所安装的部分 Mod 似乎需要使用 Java 11 启动。\n请在启动设置的 Java 选择一项中改用 Java 11，然后再启动游戏。\n如果你没有安装 Java 11，你可以从网络中下载、安装一个。")
                Case CrashReason.极短的程序输出
                    Results.Add($"程序返回了以下信息：\n{Additional.First}\n\h")
                Case CrashReason.OptiFine导致无法加载世界 'https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
                    Results.Add("你所使用的 OptiFine 可能导致了你的游戏出现问题。\n\n该问题只在特定 OptiFine 版本中出现，你可以尝试更换 OptiFine 的版本。\h")
                Case CrashReason.显卡驱动不支持导致无法设置像素格式, CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, CrashReason.显卡不支持OpenGL
                    If LogAll.Contains("hd graphics ") Then
                        Results.Add("你的显卡驱动存在问题，或未使用独立显卡，导致游戏无法正常运行。\n\n如果你的电脑存在独立显卡，请使用独立显卡而非 Intel 核显启动 PCL 与 Minecraft。\n如果问题依然存在，请尝试升级你的显卡驱动到最新版本，或回退到出厂版本。\n如果还是不行，还可以尝试使用 8.0.51 或更低版本的 Java。\h")
                    Else
                        Results.Add("你的显卡驱动存在问题，导致游戏无法正常运行。\n\n请尝试升级你的显卡驱动到最新版本，或回退到出厂版本，然后再启动游戏。\n如果还是不行，可以尝试使用 8.0.51 或更低版本的 Java。\n如果问题依然存在，那么你可能需要换个更好的显卡……\h")
                    End If
                Case CrashReason.材质过大或显卡配置不足
                    Results.Add("你所使用的材质分辨率过高，或显卡配置不足，导致游戏无法继续运行。\n\n如果你正在使用高清材质，请将它移除。\n如果你没有使用材质，那么你可能需要更新显卡驱动，或者换个更好的显卡……\h")
                Case CrashReason.NightConfig的Bug
                    Results.Add("由于 Night Config 存在问题，导致了游戏崩溃。\n你可以尝试安装 Night Config Fixes 模组，这或许能解决此问题。\h")
                Case CrashReason.光影或资源包导致OpenGL1282错误
                    Results.Add("你所使用的光影或材质导致游戏出现了一些问题……\n\n请尝试删除你所添加的这些额外资源。\h")
                Case CrashReason.Mod过多导致超出ID限制
                    Results.Add("你所安装的 Mod 过多，超出了游戏的 ID 限制，导致了游戏崩溃。\n请尝试安装 JEID 等修复 Mod，或删除部分大型 Mod。")
                Case CrashReason.文件或内容校验失败
                    Results.Add("部分文件或内容校验失败，导致游戏出现了问题。\n\n请尝试删除游戏（包括 Mod）并重新下载，或尝试在重新下载时使用 VPN。\h")
                Case CrashReason.Forge安装不完整
                    Results.Add("由于安装的 Forge 文件丢失，导致游戏无法正常运行。\n请重新安装一次相同版本的 Forge，然后再启动游戏。\n在打包游戏时删除 libraries 文件夹可能导致此错误。\h")
                Case CrashReason.Fabric报错
                    If Additional.Count = 1 Then
                        Results.Add("Fabric 提供了以下错误信息：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Fabric 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Mod互不兼容
                    If Additional.Count = 1 Then
                        Results.Add("你所安装的 Mod 不兼容：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("你所安装的 Mod 不兼容，Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Mod加载器报错
                    If Additional.Count = 1 Then
                        Results.Add("Mod 加载器提供了以下错误信息：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Fabric报错并给出解决方案
                    If Additional.Count = 1 Then
                        Results.Add("Fabric 提供了以下解决方案：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Fabric 可能已经提供了解决方案，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.Forge报错
                    If Additional.Count = 1 Then
                        Results.Add("Forge 提供了以下错误信息：\n" & Additional.First & "\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。")
                    Else
                        Results.Add("Forge 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h")
                    End If
                Case CrashReason.没有可用的分析文件
                    Results.Add("你的游戏出现了一些问题，但 PCL 未能找到相关记录文件，因此无法进行分析。\h")
                Case Else
                    Results.Add("PCL 获取到了没有详细信息的错误原因（" & CrashReasons.First.Key & "），请向 PCL 作者提交反馈以获取详情。\h")
            End Select
        Next

        Return Join(Results, "\n\n此外，").
                    Replace("\n", vbCrLf).
                    Replace("\h", "").
                    Replace("\e", If(IsHandAnalyze, "", vbCrLf & "你可以查看错误报告了解错误具体是如何发生的。")).
                    Replace(vbCrLf, vbCr).Replace(vbLf, vbCr).Replace(vbCr, vbCrLf).
                    Trim(vbCrLf.ToCharArray) &
                If(Not Results.Any(Function(r) r.EndsWithF("\h")) OrElse IsHandAnalyze, "",
                    vbCrLf & "如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。" &
                    If(If(PageSetupSystem.IsLauncherNewest(), True), "",
                    vbCrLf & vbCrLf & "此外，你正在使用老版本 PCL，更新 PCL 或许也能解决这个问题。" & vbCrLf & "你可以点击 设置 → 启动器 → 检查更新 来更新 PCL。"))
    End Function

End Class
