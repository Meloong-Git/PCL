Imports System.Net.Http.Headers
Imports System.Net.Sockets
Imports System.Runtime.CompilerServices
Imports System.Threading.Tasks

Public Module ModNet

#Region "网络请求"

    ''' <summary>
    ''' 发送一次网络请求并获取返回内容。
    ''' </summary>
    Public Function NetRequestByClient(Url As String, Optional Method As HttpMethod = Nothing,
            Optional Content As Object = Nothing, Optional ContentType As String = Nothing, Optional Accept As String = "*/*",
            Optional Timeout As Integer = 25000, Optional Headers As String(,) = Nothing, Optional RequireJson As Boolean = False,
            Optional Encoding As Encoding = Nothing, Optional SimulateBrowserHeaders As Boolean = False, Optional MakeLog As Boolean = True) As String
        If Method Is Nothing Then Method = HttpMethod.Get
        If MakeLog Then Log("[Net] 发起网络请求（" & Method.Method & "，" & Url & "），最大超时 " & Timeout)
        Try
            Dim HeaderDictionary = If(Headers, {}).ToDictionary
            If ContentType IsNot Nothing Then HeaderDictionary("Content-Type") = ContentType
            If Accept IsNot Nothing Then HeaderDictionary("Accept") = Accept
            Dim Result = If(Encoding, Encoding.UTF8).GetString(SendRequest(Url, Method, Content, HeaderDictionary, Timeout:=Timeout, Encoding:=Encoding, SimulateBrowserHeaders:=SimulateBrowserHeaders))
            '检查结果是否为完整 JSON（在 #6683 中可能返回空，在被 GFW 截断时可能不完整）
            '在此处检查并报错，可以让 NetRequestByClientRetry 等方法进行重试
            If RequireJson Then
                Result = Result.Trim((vbCrLf & " ").ToCharArray) 'Authlib-Injector 的结果会在末尾附带一个空行
                If Not (Result.StartsWithF("{") AndAlso Result.EndsWithF("}")) AndAlso
                   Not (Result.StartsWithF("[") AndAlso Result.EndsWithF("]")) Then
                    Throw New FormatException("返回结果并非 JSON 格式：" &
                    If(Result.Length > 2000, Result.Substring(0, 1000) & "..." & Result.Substring(Result.Length - 1000), Result))
                End If
            End If
            Return Result
        Catch ex As FormatException
            Throw
        Catch ex As ThreadInterruptedException
            Throw
        Catch ex As Exception
            If MakeLog Then Log(ex, "网络请求失败", LogLevel.Developer)
            Throw
        End Try
    End Function
    ''' <summary>
    ''' 发送网络请求并获取返回内容。
    ''' 会进行至多 45 秒 3 次的尝试，允许最长 30s 的超时。
    ''' </summary>
    ''' <param name="BackupUrl">如果第一次尝试失败，换用的备用 URL。</param>
    Public Function NetRequestByClientRetry(Url As String, Optional Method As HttpMethod = Nothing, Optional BackupUrl As String = Nothing,
            Optional Content As Object = Nothing, Optional ContentType As String = Nothing, Optional Accept As String = "*/*",
            Optional Headers As String(,) = Nothing, Optional RequireJson As Boolean = False,
            Optional Encoding As Encoding = Nothing, Optional SimulateBrowserHeaders As Boolean = False) As String
        Dim RetryCount As Integer = 0
        Dim RetryException As Exception = Nothing
        Dim StartTime As Long = GetTimeMs()
        Try
Retry:
            Select Case RetryCount
                Case 0 '正常尝试
                    Return NetRequestByClient(Url, Method, Content, ContentType, Accept, 10000, Headers, RequireJson, Encoding, SimulateBrowserHeaders)
                Case 1 '慢速重试
                    Thread.Sleep(500)
                    Return NetRequestByClient(Url, Method, Content, ContentType, Accept, 30000, Headers, RequireJson, Encoding, SimulateBrowserHeaders)
                Case Else '快速重试
                    If GetTimeMs() - StartTime > 5500 Then
                        '若前两次加载耗费 5 秒以上，才进行重试
                        Thread.Sleep(500)
                        Return NetRequestByClient(Url, Method, Content, ContentType, Accept, 4000, Headers, RequireJson, Encoding, SimulateBrowserHeaders)
                    Else
                        Throw RetryException
                    End If
            End Select
        Catch ex As ThreadInterruptedException
            Throw
        Catch ex As Exception
            If TypeOf ex Is ResponsedWebException Then
                If CType(ex, ResponsedWebException).StatusCode = HttpStatusCode.Forbidden Then Throw
                If CType(ex, ResponsedWebException).StatusCode = 429 Then Thread.Sleep(10000) 'Too Many Requests
            End If
            '重试
            Select Case RetryCount
                Case 0
                    RetryException = ex
                    RetryCount += 1
                    GoTo Retry
                Case 1
                    RetryCount += 1
                    GoTo Retry
                Case Else
                    Throw
            End Select
        End Try
    End Function
    ''' <summary>
    ''' 生成数个线程，同时发送网络请求并获取返回内容。
    ''' 每个线程开始之前会略有延迟。
    ''' </summary>
    Public Function NetRequestByClientMultiple(Url As String, Optional Method As HttpMethod = Nothing, Optional ThreadCount As Integer = 3,
            Optional Content As Object = Nothing, Optional ContentType As String = Nothing, Optional Accept As String = "*/*",
            Optional Headers As String(,) = Nothing, Optional RequireJson As Boolean = False, Optional Timeout As Integer = 25000,
            Optional Encoding As Encoding = Nothing, Optional SimulateBrowserHeaders As Boolean = False) As String
        Dim Threads As New List(Of Thread)
        Dim RequestResult = Nothing
        Dim RequestEx As Exception = Nothing
        Dim FailCount As Integer = 0
        For i = 1 To ThreadCount
            Dim th As New Thread(
            Sub()
                Try
                    RequestResult = NetRequestByClient(Url, Method, Content, ContentType, Accept, Timeout, Headers, RequireJson, Encoding, SimulateBrowserHeaders)
                Catch ex As Exception
                    FailCount += 1
                    RequestEx = ex
                End Try
            End Sub)
            th.Start()
            Threads.Add(th)
            Thread.Sleep(i * 250)
            If RequestResult IsNot Nothing Then GoTo RequestFinished
        Next
        Do While True
            If RequestResult IsNot Nothing Then
RequestFinished:
                Try
                    For Each th In Threads
                        If th.IsAlive Then th.Interrupt()
                    Next
                Catch
                End Try
                Return RequestResult
            ElseIf FailCount = ThreadCount Then
                Try
                    For Each th In Threads
                        If th.IsAlive Then th.Interrupt()
                    Next
                Catch
                End Try
                Throw RequestEx
            End If
            Thread.Sleep(20)
        Loop
        Return Nothing
    End Function

    ''' <summary>
    ''' 以多线程下载网页文件的方式获取内容。
    ''' 不支持缓存协商；若需缓存协商，需要换用 NetRequestByClient。
    ''' </summary>
    Public Function NetRequestByLoader(Url As String, Optional Timeout As Integer = 45000, Optional IsJson As Boolean = False, Optional SimulateBrowserHeaders As Boolean = False) As String
        Return NetRequestByLoader({Url}, Timeout, IsJson, SimulateBrowserHeaders)
    End Function
    ''' <summary>
    ''' 以多线程下载网页文件的方式获取内容。
    ''' 不支持缓存协商；若需缓存协商，需要换用 NetRequestByClient。
    ''' </summary>
    Public Function NetRequestByLoader(Urls As IEnumerable(Of String), Optional Timeout As Integer = 45000, Optional IsJson As Boolean = False, Optional SimulateBrowserHeaders As Boolean = False) As String
        Dim Temp As String = RequestTaskTempFolder() & "download.txt"
        Dim NewTask As New LoaderDownload("源码获取 " & GetUuid() & "#", New List(Of NetFile) From {New NetFile(Urls, Temp, New FileChecker With {.IsJson = IsJson}, SimulateBrowserHeaders)})
        Try
            NewTask.WaitForExitTime(Timeout, TimeoutMessage:="连接服务器超时（第一下载源：" & Urls.First & "）")
            NetRequestByLoader = ReadFile(Temp)
            File.Delete(Temp)
        Finally
            NewTask.Abort()
        End Try
    End Function

#End Region

#Region "文件下载"

    ''' <summary>
    ''' 通过网络请求直接下载小文件，若文件已存在将被覆盖。
    ''' 不建议用于下载大文件。
    ''' </summary>
    Public Sub NetDownloadByClient(Url As String, LocalPath As String, Optional SimulateBrowserHeaders As Boolean = False)
        Log($"[Net] 通过网络请求直接下载小文件：{Url} → {LocalPath}")
        '初始化文件路径
        Try
            Directory.CreateDirectory(GetPathFromFullPath(LocalPath))
            File.Delete(LocalPath)
        Catch ex As Exception
            Throw New Exception($"预处理下载文件路径失败（{LocalPath}）", ex)
        End Try
        '下载
        Try
            File.WriteAllBytes(LocalPath, SendRequest(Url, HttpMethod.Get, SimulateBrowserHeaders:=SimulateBrowserHeaders))
        Catch ex As Exception
            File.Delete(LocalPath)
            Throw New WebException($"通过网络请求直接下载小文件失败（{LocalPath}）", ex)
        End Try
    End Sub

    ''' <summary>
    ''' 通过多线程下载引擎下载文件。
    ''' </summary>
    Public Sub NetDownloadByLoader(Url As String, LocalPath As String, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional Check As FileChecker = Nothing, Optional SimulateBrowserHeaders As Boolean = False)
        NetDownloadByLoader({Url}, LocalPath, LoaderToSyncProgress, Check, SimulateBrowserHeaders)
    End Sub
    ''' <summary>
    ''' 通过多线程下载引擎下载文件。
    ''' </summary>
    ''' <param name="Urls">文件的 Url 列表。</param>
    ''' <param name="LocalPath">下载的本地地址。</param>
    Public Sub NetDownloadByLoader(Urls As IEnumerable(Of String), LocalPath As String, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional Check As FileChecker = Nothing, Optional SimulateBrowserHeaders As Boolean = False)
        Dim NewTask As New LoaderDownload($"文件下载 {GetUuid()}#", New List(Of NetFile) From {New NetFile(Urls, LocalPath, Check, SimulateBrowserHeaders)})
        Try
            NewTask.WaitForExit(LoaderToSyncProgress:=LoaderToSyncProgress)
        Catch ex As Exception
            Throw New WebException($"多线程直接下载文件失败（第一下载源：" & Urls.First() & "）", ex)
        Finally
            NewTask.Abort()
        End Try
    End Sub

#End Region

#Region "基础请求引擎"

    '基础请求函数
    Private Function SendRequest(Url As String, Method As HttpMethod,
            Optional Content As Object = Nothing, Optional Headers As Dictionary(Of String, String) = Nothing,
            Optional SimulateBrowserHeaders As Boolean = False, Optional Timeout As Integer = 25000, Optional Encoding As Encoding = Nothing) As Byte()
        If RunInUi() AndAlso Not Url.Contains("//127.") Then Throw New Exception("在 UI 线程执行了网络请求")
        Dim Request As HttpRequestMessage = Nothing, CancelToken As CancellationTokenSource = Nothing,
            Response As HttpResponseMessage = Nothing, HostIp As String = Nothing
        Try
            Url = SecretCdnSign(Url)
            Request = New HttpRequestMessage(Method, Url)
            '写入 Content
            If Content IsNot Nothing AndAlso Request.Method <> HttpMethod.Get AndAlso Request.Method <> HttpMethod.Head Then
                If TypeOf Content Is HttpContent Then
                    Request.Content = DirectCast(Content, HttpContent)
                ElseIf TypeOf Content Is Byte() Then
                    Request.Content = New ByteArrayContent(DirectCast(Content, Byte()))
                ElseIf Content IsNot Nothing Then
                    Request.Content = New ByteArrayContent(If(Encoding, Encoding.UTF8).GetBytes(Content.ToString()))
                End If
            End If
            '写入 Headers
            For Each Header In If(Headers, New Dictionary(Of String, String))
                If Header.Key.ToLower = "content-type" Then
                    Request.Content?.Headers.TryAddWithoutValidation(Header.Key, Header.Value)
                Else
                    Request.Headers.TryAddWithoutValidation(Header.Key, Header.Value)
                End If
            Next
            SecretHeadersSign(Url, Request, SimulateBrowserHeaders)
            'DNS 解析
            CancelToken = New CancellationTokenSource(Timeout)
            HostIp = DNSLookup(Request, CancelToken)
            If HostIp IsNot Nothing AndAlso Not IPReliability.ContainsKey(HostIp) Then
                IPReliability(HostIp) = -0.01 '预先降低一点，这样快速的重复请求会使用不同的 IP 以提高成功率
            End If
            '发送请求
            SyncLock RequestClientLock
                '延迟初始化，以避免在程序启动前加载 CacheCow 导致 DLL 加载失败
                If RequestClient Is Nothing Then
                    RequestClient = CacheCow.Client.ClientExtensions.CreateClient(New CacheCow.Client.FileCacheStore.FileStore(PathTemp & "Cache/Http/"), New HttpClientHandler With {
                        .AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip,
                        .UseCookies = False '不设为 False 就不能从 Header 手动传入 Cookies
                    })
                End If
            End SyncLock
            Response = RequestClient.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
            Dim ResponseStream = Response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
            Dim ResponseBytes As Byte()
            Using Stream As New MemoryStream
                ResponseStream.CopyToAsync(Stream, 81920, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
                ResponseBytes = Stream.ToArray()
            End Using
            '输出
            If Response.IsSuccessStatusCode Then
                RecordIPReliability(HostIp, 0.5)
                Return ResponseBytes
            Else
                RecordIPReliability(HostIp, -0.7)
                Dim ResponseMessage = If(Encoding, Encoding.UTF8).GetString(ResponseBytes)
                Throw New ResponsedWebException(
                    $"错误码 {Response.StatusCode} ({CInt(Response.StatusCode)})，{Method}，{Url}，{HostIp}" &
                    If(String.IsNullOrEmpty(ResponseMessage), "", vbCrLf & ResponseMessage), Response.StatusCode, ResponseMessage)
            End If
        Catch ex As ThreadInterruptedException
            Throw
        Catch ex As ResponsedWebException
            Throw
        Catch ex As Exception
            RecordIPReliability(HostIp, -1)
            If TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is TimeoutException Then 'CancellationToken 超时
                Throw New WebException($"连接服务器超时，请稍后再试，或使用 VPN 改善网络环境（{Method}, {Url}，IP：{HostIp}）", WebExceptionStatus.Timeout)
            ElseIf ex.IsNetworkRelated Then
                Throw New WebException($"网络请求失败，请稍后再试，或使用 VPN 改善网络环境（{Method}, {Url}，IP：{HostIp}）", WebExceptionStatus.Timeout)
            Else
                Throw New Exception($"网络请求出现意外异常（{Method}, {Url}，{HostIp}）", ex)
            End If
        Finally
            Request?.Dispose()
            CancelToken?.Dispose()
            Response?.Dispose()
        End Try
    End Function
    Private RequestClient As HttpClient = Nothing
    Private RequestClientLock As New Object

    ''' <summary>
    ''' 进行 DNS 解析。它仅在选择的 IP 与系统默认的不一致时才对 URL 中的 Host 进行替换。
    ''' 返回请求时应使用的 IP；若为 IPv6，则加上了方括号。
    ''' 若解析失败，则返回 Nothing。
    ''' </summary>
    Private Function DNSLookup(Request As HttpRequestMessage, CancelToken As CancellationTokenSource) As String
        '若一分钟内已失败过，则不再重复尝试解析，以减少断网时的 Exception 数量
        Dim Host = Request.RequestUri.Host
        SyncLock DNSFailureRecord.SyncRoot
            If DNSFailureRecord.ContainsKey(Host) Then
                If Date.Now - DNSFailureRecord(Host) < New TimeSpan(0, 1, 0) Then
                    Return Nothing
                Else
                    DNSFailureRecord.Remove(Host)
                End If
            End If
        End SyncLock
        '初步 DNS 解析
        Dim DnsTask As Task(Of IPAddress()) = Nothing
        Try
            DnsTask = Dns.GetHostAddressesAsync(Host)
            DnsTask.Wait(CancelToken.Token)
        Catch ex As Exception
            Log(ex, $"DNS 解析失败（{Host}）")
            DNSFailureRecord(Host) = Date.Now
            Return Nothing
        End Try
        Dim Candidates As IPAddress() = DnsTask.Result.ToArray
        If Not Candidates.Any Then
            Log($"[Net] DNS 解析无结果（{Host}）")
            Return Nothing
        End If
        '若同时存在 IPv4 和 IPv6 地址，仅选择其中一类（因为 GFW 可能只屏蔽了 IPv4 或 IPv6）
        Dim GetIPReliability = Function(Key) IPReliability.GetOrDefault(Key.ToString, 0)
        Dim IPv4Targets = Candidates.Where(Function(i) i.AddressFamily = AddressFamily.InterNetwork).ToArray
        Dim IPv6Targets = Candidates.Where(Function(i) i.AddressFamily = AddressFamily.InterNetworkV6).ToArray
        If IPv4Targets.Any AndAlso IPv6Targets.Any Then
            Dim IPv4Reliability = IPv4Targets.Average(Function(i) GetIPReliability(i))
            Dim IPv6Reliability = IPv6Targets.Average(Function(i) GetIPReliability(i))
            If Host = "api.modrinth.com" Then IPv6Reliability -= 0.1 '让 Modrinth 优先使用 IPv4 地址（#6887）
            Candidates = If(IPv4Reliability >= IPv6Reliability, IPv4Targets, IPv6Targets)
        End If
        '选择可靠度最高的 IP
        Dim MaxReliability As Double = Candidates.Max(Function(i) GetIPReliability(i))
        Dim Target As IPAddress = Candidates.First(Function(i) GetIPReliability(i) = MaxReliability)
        Dim TargetIp As String = If(Target.AddressFamily = AddressFamily.InterNetworkV6, $"[{Target}]", Target.ToString)
        If Target IsNot Candidates.First Then
            '优选结果与系统默认 IP 不一致，替换域名并设置 Host 头
            If ModeDebug Then Log($"[Net] DNS 解析结果：{Host} → {Target}，所有可能的 IP 与可靠度：{DnsTask.Result.Select(Function(i) $"{i} → {GetIPReliability(i):0.000}").Join("；")}")
            Request.Headers.Host = Host
            Dim Builder As New UriBuilder(Request.RequestUri)
            Builder.Host = TargetIp
            Request.RequestUri = Builder.Uri
        End If
        Return TargetIp
    End Function
    Private DNSFailureRecord As New SafeDictionary(Of String, Date)
    ''' <summary>
    ''' 记录每个 IP 地址的请求可靠度。
    ''' 通常取值 -1 ~ +0.5，越高越好。未尝试过的 IP 应视为 0。
    ''' </summary>
    Private IPReliability As New SafeDictionary(Of String, Double)
    ''' <summary>
    ''' 根据请求结果，记录 IP 地址的可靠度。
    ''' </summary>
    Private Sub RecordIPReliability(IP As String, Result As Double)
        If IP Is Nothing Then Return
        If Not IPReliability.ContainsKey(IP) Then IPReliability(IP) = 0
        IPReliability(IP) = (IPReliability(IP) + Result) / 2
    End Sub

    ''' <summary>
    ''' 当 HTTP 状态码不指示成功时引发的异常。
    ''' 附带额外属性，可用于获取远程服务器给予的回复以及 HTTP 状态码。
    ''' </summary>
    Public Class ResponsedWebException
        Inherits WebException
        ''' <summary>
        ''' HTTP 状态码。
        ''' </summary>
        Public StatusCode As HttpStatusCode
        ''' <summary>
        ''' 远程服务器给予的回复。
        ''' </summary>
        Public Overloads Property Response As String
        Public Sub New(Message As String, StatusCode As HttpStatusCode, Response As String)
            MyBase.New(Message)
            Me.StatusCode = StatusCode
            Me.Response = Response
        End Sub
    End Class

#End Region

#Region "多线程下载引擎"

    Private ThreadClient As New HttpClient(New HttpClientHandler With {
        .AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
    })

    ''' <summary>
    ''' 最大线程数。
    ''' </summary>
    Public NetTaskThreadLimit As Integer
    ''' <summary>
    ''' 速度下限。
    ''' </summary>
    Public NetTaskSpeedLimitLow As Long = 256 * 1024L '256K/s
    ''' <summary>
    ''' 速度上限。若无限制则为 -1。
    ''' </summary>
    Public NetTaskSpeedLimitHigh As Long = -1
    ''' <summary>
    ''' 基于限速，当前可以下载的剩余量。
    ''' </summary>
    Public NetTaskSpeedLimitLeft As Long = -1
    Private ReadOnly NetTaskSpeedLimitLeftLock As New Object
    ''' <summary>
    ''' 正在运行中的线程数。
    ''' </summary>
    Public NetTaskThreadCount As Integer = 0
    Private ReadOnly NetTaskThreadCountLock As New Object

    ''' <summary>
    ''' 下载源。
    ''' </summary>
    Public Class NetSource
        Public Id As Integer
        Public Url As String
        Public FailCount As Integer
        Public Ex As Exception
        ''' <summary>
        ''' 若该下载源正在进行强制单线程下载，标记这个唯一的线程。
        ''' </summary>
        Public SingleThread As NetThread
        Public IsFailed As Boolean
        Public Overrides Function ToString() As String
            Return Url
        End Function
    End Class
    ''' <summary>
    ''' 下载进度标示。
    ''' </summary>
    Public Enum NetState
        ''' <summary>
        ''' 尚未进行已存在检查。
        ''' </summary>
        WaitingToCheck = -1
        ''' <summary>
        ''' 尚未开始。
        ''' </summary>
        WaitingToDownload = 0
        ''' <summary>
        ''' 正在连接，尚未获取文件大小。
        ''' </summary>
        Connecting = 1
        ''' <summary>
        ''' 已连接，正在尝试接收首个下载数据包。
        ''' </summary>
        Reading = 2
        ''' <summary>
        ''' 正在下载。
        ''' </summary>
        Downloading = 3
        ''' <summary>
        ''' 正在合并文件。
        ''' </summary>
        Merging = 4
        ''' <summary>
        ''' 已完成。
        ''' </summary>
        Finished = 5
        ''' <summary>
        ''' 已失败或中断。
        ''' </summary>
        Interrupted = 6
    End Enum
    ''' <summary>
    ''' 预下载检查行为。
    ''' </summary>
    Public Enum NetPreDownloadBehaviour
        ''' <summary>
        ''' 当文件已存在时，显示提示以提醒用户是否继续下载。
        ''' </summary>
        HintWhileExists
        ''' <summary>
        ''' 当文件已存在或正在下载时，直接退出下载函数执行，不对用户进行提示。
        ''' </summary>
        ExitWhileExistsOrDownloading
        ''' <summary>
        ''' 不进行已存在检查。
        ''' </summary>
        IgnoreCheck
    End Enum

    ''' <summary>
    ''' 下载线程。
    ''' </summary>
    Public Class NetThread
        Implements IEnumerable(Of NetThread), IEquatable(Of NetThread)

        ''' <summary>
        ''' 对应的下载任务。
        ''' </summary>
        Public Task As NetFile
        ''' <summary>
        ''' 对应的线程。
        ''' </summary>
        Public Thread As Thread
        ''' <summary>
        ''' 链表中的下一个线程。
        ''' </summary>
        Public NextThread As NetThread
        Private ReadOnly Iterator Property [Next]() As IEnumerable(Of NetThread)
            Get
                Dim CurrentChain As NetThread = Me
                While CurrentChain IsNot Nothing
                    Yield CurrentChain
                    CurrentChain = CurrentChain.NextThread
                End While
            End Get
        End Property
        Public Function GetEnumerator() As IEnumerator(Of NetThread) Implements IEnumerable(Of NetThread).GetEnumerator
            Return [Next].GetEnumerator()
        End Function
        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return [Next].GetEnumerator()
        End Function

        ''' <summary>
        ''' 分配给任务中每个线程（无论其是否失败）的编号。
        ''' </summary>
        Public Uuid As Integer
        ''' <summary>
        ''' 是否为第一个线程。
        ''' </summary>
        Public ReadOnly Property IsFirstThread As Boolean
            Get
                Return DownloadStart = 0 AndAlso Task.FileSize = -2
            End Get
        End Property
        ''' <summary>
        ''' 该线程的缓存文件。
        ''' </summary>
        Public Temp As String

        ''' <summary>
        ''' 线程下载起始位置。
        ''' </summary>
        Public DownloadStart As Long
        ''' <summary>
        ''' 线程下载结束位置。
        ''' </summary>
        Public ReadOnly Property DownloadEnd As Long
            Get
                SyncLock Task.LockChain
                    If NextThread Is Nothing Then
                        If Task.IsUnknownSize Then
                            Return 1000 * 1000 * 1000 * 1000L '约 1T
                        Else
                            Return Task.FileSize - 1
                        End If
                    Else
                        Return NextThread.DownloadStart - 1
                    End If
                End SyncLock
            End Get
        End Property
        ''' <summary>
        ''' 线程未下载的文件大小。
        ''' </summary>
        Public ReadOnly Property DownloadUndone As Long
            Get
                Return DownloadEnd - (DownloadStart + DownloadDone) + 1
            End Get
        End Property
        ''' <summary>
        ''' 线程已下载的文件大小。
        ''' </summary>
        Public DownloadDone As Long = 0

        ''' <summary>
        ''' 上次记速时的时间。
        ''' </summary>
        Private SpeedLastTime As Long = GetTimeMs()
        ''' <summary>
        ''' 上次记速时的已下载大小。
        ''' </summary>
        Private SpeedLastDone As Long = 0
        ''' <summary>
        ''' 当前的下载速度，单位为 Byte / 秒。
        ''' </summary>
        Public ReadOnly Property Speed As Long
            Get
                If GetTimeMs() - SpeedLastTime > 200 Then
                    Dim DeltaTime As Long = GetTimeMs() - SpeedLastTime
                    _Speed = (DownloadDone - SpeedLastDone) / (DeltaTime / 1000)
                    SpeedLastDone = DownloadDone
                    SpeedLastTime += DeltaTime
                End If
                Return _Speed
            End Get
        End Property
        Private _Speed As Long = 0

        ''' <summary>
        ''' 线程初始化时的时间。
        ''' </summary>
        Public InitTime As Long = GetTimeMs()
        ''' <summary>
        ''' 上次接受到有效数据的时间，-1 表示尚未有有效数据。
        ''' </summary>
        Public LastReceiveTime As Long = -1

        ''' <summary>
        ''' 当前线程的状态。
        ''' </summary>
        Public State As NetState = NetState.WaitingToDownload
        ''' <summary>
        ''' 是否已经结束。
        ''' </summary>
        Public ReadOnly Property IsEnded As Boolean
            Get
                Return State = NetState.Finished OrElse State = NetState.Interrupted
            End Get
        End Property

        ''' <summary>
        ''' 当前选取的是哪一个 Url。
        ''' </summary>
        Public Source As NetSource

        '允许进行 UUID 比较
        Public Overloads Function Equals(other As NetThread) As Boolean Implements IEquatable(Of NetThread).Equals
            Return other IsNot Nothing AndAlso Uuid = other.Uuid
        End Function
        Public Overrides Function Equals(obj As Object) As Boolean
            Return Equals(TryCast(obj, NetThread))
        End Function
        Public Shared Operator =(left As NetThread, right As NetThread) As Boolean
            Return EqualityComparer(Of NetThread).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As NetThread, right As NetThread) As Boolean
            Return Not left = right
        End Operator
    End Class
    ''' <summary>
    ''' 下载单个文件。
    ''' </summary>
    Public Class NetFile

#Region "属性"

        ''' <summary>
        ''' 所属的文件列表任务。
        ''' </summary>
        Public Tasks As New SafeList(Of LoaderDownload)
        ''' <summary>
        ''' 所有下载源。
        ''' </summary>
        Public Sources As SafeList(Of NetSource)
        ''' <summary>
        ''' 用于在第一个线程出错时切换下载源。
        ''' </summary>
        Private FirstThreadSource As Integer = 0
        ''' <summary>
        ''' 所有已经被标记为失败的，但未完整尝试过的，不允许断点续传的下载源。
        ''' </summary>
        Public SourcesOnce As New SafeList(Of NetSource)
        ''' <summary>
        ''' 仅当合并失败或首次下载失败时，会将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载。
        ''' 这一策略可以兼容多个下载源中的一部分返回错误的文件的情况，以及部分在多线程下载时会抽风的源。
        ''' </summary>
        Private Retried As Boolean = False
        ''' <summary>
        ''' 获取从某个源开始，第一个可用的源。
        ''' </summary>
        Private Function GetSource(Optional Id As Integer = 0) As NetSource
            If Id >= Sources.Count OrElse Id < 0 Then Id = 0
            SyncLock LockSource
                If HasAvailableSource(False) Then
                    '存在多线程可用源
                    Dim CurrentSource As NetSource = Sources(Id)
                    While CurrentSource.IsFailed
                        Id += 1
                        If Id >= Sources.Count Then Id = 0
                        CurrentSource = Sources(Id)
                    End While
                    Return CurrentSource
                ElseIf SourcesOnce.Any Then
                    '仅存在单线程可用源
                    Return SourcesOnce.First
                Else
                    '没有可用源
                    Return Nothing
                End If
            End SyncLock
        End Function
        ''' <summary>
        ''' 是否存在可用源。
        ''' </summary>
        Public Function HasAvailableSource(Optional AllowOnceSource As Boolean = True) As Boolean
            SyncLock LockSource
                If Sources.Any(Function(s) Not s.IsFailed) Then Return True '存在多线程可用源
                If AllowOnceSource AndAlso SourcesOnce.Any Then Return True '存在单线程可用源
            End SyncLock
            Return False
        End Function

        ''' <summary>
        ''' 存储在本地的带文件名的地址。
        ''' </summary>
        Public LocalPath As String = Nothing
        ''' <summary>
        ''' 存储在本地的文件名。
        ''' </summary>
        Public LocalName As String = Nothing

        ''' <summary>
        ''' 当前的下载状态。
        ''' </summary>
        Public State As NetState = NetState.WaitingToCheck
        ''' <summary>
        ''' 导致下载失败的原因。
        ''' </summary>
        Public Ex As New List(Of Exception)

        ''' <summary>
        ''' 作为文件组成部分的线程链表。
        ''' 如果没有线程，可以为 Nothing。
        ''' </summary>
        Public Threads As NetThread

        ''' <summary>
        ''' 文件的总大小。若为 -2 则为未获取，若为 -1 则为无法获取准确大小。
        ''' </summary>
        Public FileSize As Long = -2
        ''' <summary>
        ''' 该文件是否无法获取准确大小。
        ''' </summary>
        Public IsUnknownSize As Boolean = False
        ''' <summary>
        ''' 该文件是否不需要分割。
        ''' </summary>
        Public ReadOnly Property IsNoSplit As Boolean
            Get
                Return IsUnknownSize OrElse FileSize < 1024 * 1024 '小于 1MB 的文件不分割
            End Get
        End Property
        ''' <summary>
        ''' 为不需要分割的小文件在内存中提供临时存储。
        ''' </summary>
        Private Cache As MemoryStream

        ''' <summary>
        ''' 文件的已下载大小。
        ''' </summary>
        Public DownloadDone As Long = 0
        Private ReadOnly LockDone As New Object
        ''' <summary>
        ''' 文件的校验规则。
        ''' </summary>
        Public Check As FileChecker
        ''' <summary>
        ''' 是否模拟浏览器的 UserAgent 和 Referer。
        ''' </summary>
        Public SimulateBrowserHeaders As Boolean

        ''' <summary>
        ''' 上次记速时的时间。
        ''' </summary>
        Private SpeedLastTime As Long = GetTimeMs()
        ''' <summary>
        ''' 上次记速时的已下载大小。
        ''' </summary>
        Private SpeedLastDone As Long = 0
        ''' <summary>
        ''' 当前的下载速度，单位为 Byte / 秒。
        ''' </summary>
        Public ReadOnly Property Speed As Long
            Get
                If GetTimeMs() - SpeedLastTime > 200 Then
                    Dim DeltaTime As Long = GetTimeMs() - SpeedLastTime
                    _Speed = (DownloadDone - SpeedLastDone) / (DeltaTime / 1000)
                    SpeedLastDone = DownloadDone
                    SpeedLastTime += DeltaTime
                End If
                Return _Speed
            End Get
        End Property
        Private _Speed As Long = 0

        ''' <summary>
        ''' 该文件是否由本地文件直接拷贝完成。
        ''' </summary>
        Public IsCopy As Boolean = False
        ''' <summary>
        ''' 本文件的显示进度。
        ''' </summary>
        Public ReadOnly Property Progress As Double
            Get
                Select Case State
                    Case NetState.WaitingToCheck
                        Return 0
                    Case NetState.WaitingToDownload
                        Return 0.01
                    Case NetState.Connecting
                        Return 0.02
                    Case NetState.Reading
                        Return 0.04
                    Case NetState.Downloading
                        '正在下载中，对应 5% ~ 98%
                        Dim OriginalProgress As Double = If(IsUnknownSize, 0.5, DownloadDone / Math.Max(FileSize, 1))
                        OriginalProgress = 1 - (1 - OriginalProgress) ^ 0.9
                        Return OriginalProgress * 0.93 + 0.05
                    Case NetState.Merging
                        Return 0.99
                    Case NetState.Finished, NetState.Interrupted
                        Return 1
                    Case Else
                        Throw New ArgumentOutOfRangeException("文件状态未知：" & State)
                End Select
            End Get
        End Property

        ''' <summary>
        ''' 各个线程建立连接成功的总次数。
        ''' </summary>
        Private ConnectCount As Integer = 0
        ''' <summary>
        ''' 各个线程建立连接成功的总时间。
        ''' </summary>
        Private ConnectTime As Long = 0
        ''' <summary>
        ''' 各个线程建立连接成功的平均时间，单位为毫秒，-1 代表尚未有成功连接。
        ''' </summary>
        Private ReadOnly Property ConnectAverage As Integer
            Get
                SyncLock LockCount
                    Return If(ConnectCount = 0, -1, ConnectTime / ConnectCount)
                End SyncLock
            End Get
        End Property

        Private Const FilePieceLimit As Long = 256 * 1024
        Public ReadOnly LockCount As New Object
        Public ReadOnly LockState As New Object
        Public ReadOnly LockChain As New Object
        Public ReadOnly LockSource As New Object

        Public ReadOnly Uuid As Integer = GetUuid()
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim file = TryCast(obj, NetFile)
            Return file IsNot Nothing AndAlso Uuid = file.Uuid
        End Function

#End Region

        ''' <summary>
        ''' 新建一个需要下载的文件。
        ''' </summary>
        ''' <param name="LocalPath">包含文件名的本地地址。</param>
        Public Sub New(Urls As IEnumerable(Of String), LocalPath As String, Optional Checker As FileChecker = Nothing, Optional SimulateBrowserHeaders As Boolean = False)
            Dim Sources As New List(Of NetSource)
            Dim Count As Integer = 0
            Urls = Urls.Distinct.ToArray
            For Each Source As String In Urls
                Sources.Add(New NetSource With {.FailCount = 0, .Url = SecretCdnSign(Source.Replace(vbCr, "").Replace(vbLf, "").Trim), .Id = Count, .IsFailed = False, .Ex = Nothing})
                Count += 1
            Next
            Me.Sources = Sources
            Me.LocalPath = LocalPath
            Me.Check = Checker
            Me.SimulateBrowserHeaders = SimulateBrowserHeaders
            Me.LocalName = GetFileNameFromPath(LocalPath)
        End Sub

        ''' <summary>
        ''' 尝试开始一个新的下载线程。
        ''' 如果失败，返回 Nothing。
        ''' </summary>
        Public Function TryBeginThread() As NetThread
            Try

                Dim StartPosition As Long, StartSource As NetSource = Nothing
                Dim Th As Thread, ThreadInfo As NetThread
                '条件检测
                SyncLock LockSource
                    If NetTaskThreadCount >= NetTaskThreadLimit OrElse Not HasAvailableSource() OrElse
                        (IsNoSplit AndAlso Threads IsNot Nothing AndAlso Threads.State <> NetState.Interrupted) Then Return Nothing
                    If State >= NetState.Merging OrElse State = NetState.WaitingToCheck Then Return Nothing
                    SyncLock LockState
                        If State < NetState.Connecting Then State = NetState.Connecting
                    End SyncLock
                    '初始化参数
                    '获取线程起点与下载源
                    '不分割
                    If IsNoSplit Then GoTo StartSingleThreadDownload
                    '只剩下单线程可用点
                    If Not HasAvailableSource(False) Then
                        If SourcesOnce.First.SingleThread IsNot Nothing AndAlso SourcesOnce.First.SingleThread.State <> NetState.Interrupted Then Return Nothing
StartSingleThreadDownload:
                        Reset()
                        State = NetState.Reading
                    End If
                End SyncLock
                SyncLock LockChain
                    '首个开始点
                    If Threads Is Nothing Then
                        StartPosition = 0
                        StartSource = GetSource(FirstThreadSource)
                        FirstThreadSource = StartSource.Id + 1
                        GoTo StartThread
                    End If
                    '寻找失败点
                    For Each Thread As NetThread In Threads
                        If Thread.State = NetState.Interrupted AndAlso Thread.DownloadUndone > 0 Then
                            StartPosition = Thread.DownloadStart + Thread.DownloadDone
                            StartSource = GetSource(Thread.Source.Id + 1)
                            GoTo StartThread
                        End If
                    Next
                    '是否禁用多线程，以及规定碎片大小
                    Dim Source As NetSource = GetSource()
                    If Source Is Nothing Then Return Nothing
                    Dim TargetUrl As String = Source.Url
                    If TargetUrl.Contains("pcl2-server") OrElse TargetUrl.Contains("bmclapi") OrElse TargetUrl.Contains("github.com") OrElse
                       TargetUrl.Contains("optifine.net") OrElse TargetUrl.Contains("modrinth") OrElse TargetUrl.Contains("momot.rs") Then Return Nothing
                    '寻找最大碎片
                    'FUTURE: 下载引擎重做，计算下载源平均链接时间和线程下载速度，按最高时间节省来开启多线程
                    Dim FilePieceMax As NetThread = Threads
                    For Each Thread As NetThread In Threads
                        If Thread.DownloadUndone > FilePieceMax.DownloadUndone Then FilePieceMax = Thread
                    Next
                    If FilePieceMax Is Nothing OrElse FilePieceMax.DownloadUndone < FilePieceLimit Then Return Nothing
                    StartPosition = FilePieceMax.DownloadEnd - FilePieceMax.DownloadUndone * 0.4
                    StartSource = GetSource()

                    '开始线程
StartThread:
                    If (StartPosition > FileSize AndAlso FileSize >= 0 AndAlso Not IsUnknownSize) OrElse StartPosition < 0 OrElse IsNothing(StartSource) Then Return Nothing
                    '构建线程
                    Dim ThreadUuid As Integer = GetUuid()
                    If Not Tasks.Any() Then Return Nothing '由于中断，已没有可用任务
                    Th = New Thread(AddressOf Thread) With {.Name = $"下载 {Tasks(0).Uuid}/{Uuid}/{ThreadUuid}#", .Priority = ThreadPriority.BelowNormal}
                    ThreadInfo = New NetThread With {.Uuid = ThreadUuid, .DownloadStart = StartPosition, .Thread = Th, .Source = StartSource, .Task = Me, .State = NetState.WaitingToDownload}
                    '链表处理
                    If ThreadInfo.IsFirstThread OrElse Threads Is Nothing Then
                        Threads = ThreadInfo
                    Else
                        Dim CurrentChain As NetThread = Threads
                        While CurrentChain.DownloadEnd <= StartPosition
                            CurrentChain = CurrentChain.NextThread
                        End While
                        ThreadInfo.NextThread = CurrentChain.NextThread
                        CurrentChain.NextThread = ThreadInfo
                    End If

                End SyncLock
                '开始线程
                SyncLock NetTaskThreadCountLock
                    NetTaskThreadCount += 1
                End SyncLock
                SyncLock LockSource
                    If Not HasAvailableSource(False) Then SourcesOnce.First.SingleThread = ThreadInfo
                End SyncLock
                Th.Start(ThreadInfo)
                Return ThreadInfo

            Catch ex As Exception
                Fail(New Exception($"尝试开始下载线程失败（{LocalName}）", ex))
                Return Nothing
            End Try
        End Function
        ''' <summary>
        ''' 每个下载线程执行的代码。
        ''' </summary>
        Private Sub Thread(Th As NetThread)
            If ModeDebug OrElse Th.DownloadStart = 0 Then Log($"[Download] {LocalName}：开始，起始点 {Th.DownloadStart}，{Th.Source.Url}")
            Dim ResultStream As Stream = Nothing, HttpRequest As HttpRequestMessage = Nothing,
                Response As HttpResponseMessage = Nothing, ResponseStream As Stream = Nothing,
                CancelToken As CancellationTokenSource = Nothing, HostIp As String = Nothing
            '部分下载源真的特别慢，并且只需要一个请求，例如 Ping 为 20s，如果增长太慢，就会造成类似 2.5s 5s 7.5s 10s 12.5s... 的极大延迟
            '延迟过长会导致某些特别慢的链接迟迟不被掐死
            Dim Timeout As Integer = Math.Min(Math.Max(ConnectAverage, 10000) * (1 + Th.Source.FailCount), 30000)
            Th.State = NetState.Connecting
            Try
                Dim HttpDataCount As Integer = 0
                If SourcesOnce.Contains(Th.Source) AndAlso Th <> Th.Source.SingleThread Then GoTo SourceBreak
                HttpRequest = New HttpRequestMessage(HttpMethod.Get, Th.Source.Url)
                SecretHeadersSign(Th.Source.Url, HttpRequest, SimulateBrowserHeaders)
                CancelToken = New CancellationTokenSource(Timeout)
                HostIp = DNSLookup(HttpRequest, CancelToken) 'DNS 预解析
                If Not Th.IsFirstThread Then HttpRequest.Headers.Range = New RangeHeaderValue(Th.DownloadStart, Nothing)
                Dim ContentLength As Long = 0
                Response = ThreadClient.SendAsync(HttpRequest, HttpCompletionOption.ResponseHeadersRead, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
                If Not Response.IsSuccessStatusCode Then Throw New Exception($"错误码 {Response.StatusCode} ({CInt(Response.StatusCode)})，{Th.Source.Url}") '状态码检查
                If State = NetState.Interrupted Then GoTo SourceBreak '快速中断
                If ModeDebug AndAlso Response.RequestMessage.RequestUri.ToString <> Th.Source.Url Then Log($"[Download] {LocalName}：重定向至 {Response.RequestMessage.RequestUri}")
                '文件大小校验
                ContentLength = Response.Content.Headers.ContentLength.GetValueOrDefault(-1)
                If ContentLength < 0 Then
                    If FileSize > 1 Then
                        If Th.DownloadStart = 0 Then
                            Log($"[Download] {LocalName}：文件大小未知，但已从其他下载源获取，不作处理")
                        Else
                            Log($"[Download] {LocalName}：ContentLength 返回了 {ContentLength}，无法确定是否支持分段下载，视作不支持")
                            GoTo NotSupportRange
                        End If
                    Else
                        FileSize = -1 : IsUnknownSize = True
                        Log($"[Download] {LocalName}：文件大小未知")
                    End If
                ElseIf Th.IsFirstThread Then
                    If Check IsNot Nothing Then
                        If ContentLength < Check.MinSize AndAlso Check.MinSize > 0 Then
                            Throw New Exception($"文件大小不足，获取结果为 {ContentLength}，要求至少为 {Check.MinSize}。")
                        End If
                        If ContentLength <> Check.ActualSize AndAlso Check.ActualSize > 0 Then
                            Throw New Exception($"文件大小不一致，获取结果为 {ContentLength}，要求必须为 {Check.ActualSize}。")
                        End If
                    End If
                    FileSize = ContentLength : IsUnknownSize = False
                    Log($"[Download] {LocalName}：文件大小 {ContentLength}（{GetString(ContentLength)}）")
                    '若文件大小大于 50 M，进行剩余磁盘空间校验
                    If ContentLength > 50 * 1024 * 1024 Then
                        For Each Drive As DriveInfo In DriveInfo.GetDrives
                            Dim DriveName As String = Drive.Name.First.ToString
                            Dim RequiredSpace = If(PathTemp.StartsWithF(DriveName), ContentLength * 1.1, 0) +
                                                If(LocalPath.StartsWithF(DriveName), ContentLength + 5 * 1024 * 1024, 0)
                            If Drive.TotalFreeSpace < RequiredSpace Then
                                Throw New Exception(DriveName & " 盘空间不足，无法进行下载。" & vbCrLf & "需要至少 " & GetString(RequiredSpace) & " 空间，但当前仅剩余 " & GetString(Drive.TotalFreeSpace) & "。" &
                                                    If(PathTemp.StartsWithF(DriveName), vbCrLf & vbCrLf & "下载时需要与文件同等大小的空间存放缓存，你可以在设置中调整缓存文件夹的位置。", ""))
                            End If
                        Next
                    End If
                ElseIf FileSize < 0 Then
                    Throw New Exception("非首线程运行时，尚未获取文件大小")
                ElseIf Th.DownloadStart > 0 AndAlso ContentLength = FileSize Then
NotSupportRange:
                    SyncLock LockSource
                        If SourcesOnce.Contains(Th.Source) Then GoTo SourceBreak
                    End SyncLock
                    Throw New RangeNotSupportedException($"该下载源不支持分段下载：Range 起始于 {Th.DownloadStart}，预期 ContentLength 为 {FileSize - Th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}")
                ElseIf Not FileSize - Th.DownloadStart = ContentLength Then
                    Throw New RangeNotSupportedException($"获取到的分段大小不一致：Range 起始于 {Th.DownloadStart}，预期 ContentLength 为 {FileSize - Th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}")
                End If
                'Log($"[Download] {LocalName} {Info.Uuid}#：通过大小检查，文件大小 {FileSize}，起始点 {Info.DownloadStart}，ContentLength {ContentLength}")
                Th.State = NetState.Reading
                SyncLock LockState
                    If State < NetState.Reading Then State = NetState.Reading
                End SyncLock
                '创建缓存文件
                If IsNoSplit Then
                    Th.Temp = Nothing
                    Cache = New MemoryStream
                    ResultStream = Cache
                Else
                    Directory.CreateDirectory(PathTemp & "Download")
                    Th.Temp = $"{PathTemp}Download\{Uuid}_{Th.Uuid}_{RandomInteger(0, 999999)}.tmp"
                    ResultStream = New FileStream(Th.Temp, FileMode.Create, FileAccess.Write, FileShare.Read)
                End If
                '开始下载
                ResponseStream = Response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                If Setup.Get("SystemDebugDelay") Then Threading.Thread.Sleep(RandomInteger(50, 3000))
                Dim ResponseBytes As Byte() = New Byte(16384) {}
                HttpDataCount = ResponseStream.ReadAsync(ResponseBytes, 0, 16384, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
                While (IsUnknownSize OrElse Th.DownloadUndone > 0) AndAlso '判断是否下载完成
                            HttpDataCount > 0 AndAlso Not IsProgramEnded AndAlso State < NetState.Merging AndAlso (Not Th.Source.IsFailed OrElse Th.Source.SingleThread = Th)
                    '限速
                    While NetTaskSpeedLimitHigh > 0 AndAlso NetTaskSpeedLimitLeft <= 0
                        Threading.Thread.Sleep(16)
                    End While
                    Dim RealDataCount As Integer = If(IsUnknownSize, HttpDataCount, Math.Min(HttpDataCount, Th.DownloadUndone))
                    SyncLock NetTaskSpeedLimitLeftLock
                        If NetTaskSpeedLimitHigh > 0 Then NetTaskSpeedLimitLeft -= RealDataCount
                    End SyncLock
                    Dim DeltaTime = GetTimeMs() - Th.LastReceiveTime
                    If DeltaTime > 1000000 Then DeltaTime = 1 '避免时间刻反转导致出现极大值
                    If RealDataCount > 0 Then
                        '有数据
                        If Th.DownloadDone = 0 Then
                            '第一次接受到数据
                            Th.State = NetState.Downloading
                            SyncLock LockState
                                If State < NetState.Downloading Then State = NetState.Downloading
                            End SyncLock
                            SyncLock LockCount
                                ConnectCount += 1
                                ConnectTime += GetTimeMs() - Th.InitTime
                            End SyncLock
                        End If
                        SyncLock LockCount
                            Th.Source.FailCount = 0
                            For Each Task In Tasks
                                Task.FailCount = 0
                            Next
                        End SyncLock
                        NetManager.DownloadDone += RealDataCount
                        SyncLock LockDone
                            DownloadDone += RealDataCount
                        End SyncLock
                        Th.DownloadDone += RealDataCount
                        ResultStream.Write(ResponseBytes, 0, RealDataCount)
                        '已完成
                        If Th.DownloadUndone = 0 AndAlso Not IsUnknownSize Then Exit While
                        '检查速度是否过慢
                        If DeltaTime > 5000 AndAlso DeltaTime > RealDataCount AndAlso '数据包间隔大于 5s，且速度小于 1K/s
                                    Th.Source.SingleThread Is Nothing Then '且并非单线程下载
                            Throw New TimeoutException("由于速度过慢断开链接，下载 " & RealDataCount & " B，消耗 " & DeltaTime & " ms。")
                        End If
                        Th.LastReceiveTime = GetTimeMs()
                    ElseIf Th.LastReceiveTime > 0 AndAlso DeltaTime > Timeout Then
                        '无数据，且已超时
                        Throw New TimeoutException("操作超时，无数据。")
                    End If
                    HttpDataCount = ResponseStream.ReadAsync(ResponseBytes, 0, 16384, CancelToken.Token).GetResultWithTimeout(CancelToken, Timeout)
                End While
SourceBreak:
                If State = NetState.Interrupted OrElse (Th.Source.IsFailed AndAlso Th.Source.SingleThread <> Th) OrElse (Th.DownloadUndone > 0 AndAlso Not IsUnknownSize) Then
                    '被外部中断
                    Th.State = NetState.Interrupted
                    Log($"[Download] {LocalName}：中断")
                ElseIf HttpDataCount = 0 AndAlso Th.DownloadUndone > 0 AndAlso Not IsUnknownSize Then
                    '服务器无返回数据
                    Throw New Exception($"返回的 ContentLength 过多：ContentLength 为 {ContentLength}，但获取到的总数据量仅为 {Th.DownloadDone}（全文件总数据量 {DownloadDone}）")
                Else
                    '本线程完成
                    Th.State = NetState.Finished
                    If ModeDebug Then Log($"[Download] {LocalName}：完成，已下载 {Th.DownloadDone}（{Th.DownloadStart}~{Th.DownloadEnd}）")
                    RecordIPReliability(HostIp, 0.5)
                End If
            Catch ex As Exception
                Log($"[Download] {LocalName}：出错，{If(TypeOf ex Is OperationCanceledException OrElse TypeOf ex Is TimeoutException,
                    $"已超时（{Timeout}ms）", ex.GetDetail())}，IP：{HostIp}")
                RecordIPReliability(HostIp, -0.7)
                SourceFail(Th, ex, False)
            Finally
                '释放资源
                HttpRequest?.Dispose()
                Response?.Dispose()
                ResponseStream?.Dispose()
                CancelToken?.Dispose()
                If Not IsNoSplit Then ResultStream?.Dispose()
                '改变计数
                SyncLock NetTaskThreadCountLock
                    NetTaskThreadCount -= 1
                End SyncLock
                '合并
                If ((FileSize >= 0 AndAlso DownloadDone >= FileSize) OrElse (FileSize = -1 AndAlso DownloadDone > 0)) AndAlso
                    State < NetState.Merging AndAlso Th.State <> NetState.Interrupted Then Merge(Th)
            End Try
        End Sub
        Private Sub SourceFail(Th As NetThread, ex As Exception, IsMergeFailure As Boolean)
            '状态变更
            SyncLock LockCount
                Th.Source.FailCount += 1
                For Each Task In Tasks
                    Task.FailCount += 1
                Next
            End SyncLock
            Th.State = NetState.Interrupted
            Th.Source.Ex = ex
            '根据情况判断，是否在多线程下禁用下载源（连续错误过多，或不支持断点续传）
            Dim IsRangeNotSupported As Boolean = TypeOf ex Is RangeNotSupportedException OrElse ex.Message.Contains("(416)")
            If IsMergeFailure OrElse IsRangeNotSupported OrElse
               ex.Message.Contains("(502)") OrElse ex.Message.Contains("(404)") OrElse
               ex.Message.Contains("未能解析") OrElse ex.Message.Contains("无返回数据") OrElse ex.Message.Contains("空间不足") OrElse
               ((ex.Message.Contains("(403)") OrElse ex.Message.Contains("(429)")) AndAlso Not Th.Source.Url.ContainsF("bmclapi")) OrElse 'BMCLAPI 的部分源在高频率请求下会返回 403/429，所以不应因此禁用下载源
               (Th.Source.FailCount >= MathClamp(NetTaskThreadLimit, 5, 30) AndAlso DownloadDone < 1) OrElse Th.Source.FailCount > NetTaskThreadLimit + 2 Then
                '当一个下载源有多个线程在下载时，只选择其中一个线程进行后续处理
                Dim IsThisFail As Boolean = False
                SyncLock LockSource
                    If Not Th.Source.IsFailed OrElse Th.Source.SingleThread = Th Then
                        IsThisFail = True
                        Th.Source.IsFailed = True
                    End If
                End SyncLock
                '……后续处理
                If IsThisFail Then
                    Log($"[Download] {LocalName}：下载源被禁用（{Th.Source.Id}，Range 问题：{IsRangeNotSupported}）：{Th.Source.Url}")
                    Log(ex, $"{If(SourcesOnce.FirstOrDefault?.SingleThread Is Nothing, "", "单线程")}下载源 {Th.Source.Id} 已被禁用",
                        If(IsRangeNotSupported OrElse ex.Message.Contains("(404)"), LogLevel.Developer, LogLevel.Debug))
                    SyncLock LockSource
                        SourcesOnce.Remove(Th.Source)
                    End SyncLock
                    If ex.Message.Contains("空间不足") Then
                        '硬盘空间不足：强制失败
                        Fail(ex)
                    ElseIf HasAvailableSource() AndAlso Not IsMergeFailure Then
                        '当前源失败，但还有下载源：正常地继续执行
                    ElseIf Not Retried Then
                        '合并失败或首次下载失败，未重试：将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载
                        '若所有源均不支持 Range，也会走到这里重试
                        If Not IsRangeNotSupported Then Log($"[Download] {LocalName}：文件下载失败，正在自动重试……", LogLevel.Debug)
                        Retried = True
                        SyncLock LockSource
                            SourcesOnce.Clear()
                            For Each Source In Sources
                                SourcesOnce.Add(Source)
                                Source.IsFailed = True
                            Next
                        End SyncLock
                        Reset()
                        SyncLock LockState
                            State = NetState.WaitingToDownload
                        End SyncLock
                    ElseIf HasAvailableSource() AndAlso IsMergeFailure Then
                        '合并失败且单个源失败：继续下一个源
                        Reset()
                        SyncLock LockState
                            State = NetState.WaitingToDownload
                        End SyncLock
                    Else
                        '失败
                        Log($"[Download] {LocalName}：已无可用下载源，下载失败")
                        Dim ExampleEx As Exception = Nothing
                        SyncLock LockSource
                            For Each Source As NetSource In Sources
                                Log("[Download] 已禁用的下载源：" & Source.Url)
                                If Source.Ex IsNot Nothing Then
                                    ExampleEx = Source.Ex
                                    Log(Source.Ex, "下载源禁用原因", LogLevel.Developer)
                                End If
                            Next
                        End SyncLock
                        Fail(ExampleEx)
                    End If
                End If
            End If
            '清理当前已下载的内容
            If FileSize = -2 Then Reset()
        End Sub
        Private Sub Reset()
            FileSize = -2
            Cache?.Dispose() : Cache = Nothing
            SyncLock LockChain
                Threads = Nothing
            End SyncLock
            NetManager.DownloadDone -= DownloadDone
            SyncLock LockDone
                DownloadDone = 0
            End SyncLock
            SpeedLastDone = 0
        End Sub

        '最终收束事件

        ''' <summary>
        ''' 下载完成。合并文件。
        ''' </summary>
        Private Sub Merge(Th As NetThread)
            '状态判断
            SyncLock LockState
                If State < NetState.Merging Then
                    State = NetState.Merging
                Else
                    Return
                End If
            End SyncLock
            Dim RetryCount As Integer = 0
            Try
Retry:
                '创建文件夹
                If File.Exists(LocalPath) Then File.Delete(LocalPath)
                Directory.CreateDirectory(GetPathFromFullPath(LocalPath))
                SyncLock LockChain
                    '合并文件
                    If IsNoSplit Then
                        '仅有一个线程，从缓存中输出
                        If ModeDebug Then Log($"[Download] {LocalName}：下载结束，从内存输出文件")
                        Cache.Position = 0
                        WriteFile(LocalPath, Cache)
                    ElseIf Threads.DownloadDone = DownloadDone AndAlso Threads.Temp IsNot Nothing Then
                        '仅有一个文件，直接复制
                        If ModeDebug Then Log($"[Download] {LocalName}：下载结束，仅有一个文件，无需合并")
                        CopyFile(Threads.Temp, LocalPath)
                    Else
                        '有多个线程，合并
                        If ModeDebug Then Log($"[Download] {LocalName}：下载结束，开始合并文件")
                        Using MergeFile As New FileStream(LocalPath, FileMode.Create)
                            Using AddWriter As New BinaryWriter(MergeFile)
                                For Each Thread As NetThread In Threads
                                    If Thread.DownloadDone = 0 OrElse Thread.Temp Is Nothing Then Continue For
                                    Using fs As New FileStream(Thread.Temp, FileMode.Open, FileAccess.Read, FileShare.Read)
                                        Using TempReader As New BinaryReader(fs)
                                            AddWriter.Write(TempReader.ReadBytes(Thread.DownloadDone))
                                        End Using
                                    End Using
                                Next
                            End Using
                        End Using
                    End If
                    '检查文件
                    Dim CheckResult As String = Check?.Check(LocalPath)
                    If CheckResult Is Nothing AndAlso Not IsUnknownSize AndAlso Check IsNot Nothing Then
                        If Check.ActualSize = -1 Then
                            CheckResult = (New FileChecker With {.ActualSize = FileSize}).Check(LocalPath) '不修改原始的 Checker，以免污染原始实例
                        ElseIf Check.ActualSize <> FileSize Then
                            CheckResult = $"文件大小不一致：任务校验要求为 {Check.ActualSize}，请求结果为 {FileSize}"
                        End If
                    End If
                    If CheckResult IsNot Nothing Then
                        Log($"[Download] {LocalName} 文件校验失败，下载线程细节：")
                        For Each T As NetThread In Threads
                            Log($"[Download] - {T.Uuid}#，状态 {GetStringFromEnum(T.State)}，范围 {T.DownloadStart}~{T.DownloadEnd}，已下载 {T.DownloadDone}，未下载 {T.DownloadUndone}")
                        Next
                        Throw New Exception(CheckResult)
                    End If
                    '后处理
                    If Not IsNoSplit Then
                        For Each Thread As NetThread In Threads
                            If Thread.Temp IsNot Nothing Then File.Delete(Thread.Temp)
                        Next
                    End If
                    Finish()
                End SyncLock
            Catch ex As Exception
                RetryCount += 1
                Log(ex, $"合并文件出错，第 {RetryCount} 次尝试（{LocalName}）")
                '重新尝试合并
                If RetryCount < 3 Then
                    Threading.Thread.Sleep(500 * RetryCount)
                    GoTo Retry
                End If
                '失败，禁用当前下载源并重启下载
                Try
                    If File.Exists(LocalPath) Then File.Delete(LocalPath)
                Catch exx As Exception
                    Log(ex, $"删除合并失败的文件出错（{LocalPath}）")
                End Try
                SourceFail(Th, ex, True)
            Finally
                Cache?.Dispose()
            End Try
        End Sub
        ''' <summary>
        ''' 下载失败。
        ''' </summary>
        Private Sub Fail(Optional RaiseEx As Exception = Nothing)
            SyncLock LockState
                If State >= NetState.Finished Then Return
                If RaiseEx IsNot Nothing Then Ex.Add(RaiseEx)
                '凉凉
                Log($"[Download] {LocalName}：已失败，当前状态 {State}")
                State = NetState.Interrupted
            End SyncLock
            AbortInternal()
            For Each Task In Tasks
                Task.OnFileFail(Me)
            Next
        End Sub
        ''' <summary>
        ''' 下载中断。
        ''' </summary>
        Public Sub Abort(AbortedTask As LoaderDownload)
            '从特定任务中移除，如果它还属于其他任务，则继续下载
            Tasks.Remove(AbortedTask)
            If Tasks.Any Then Return
            '确认中断
            SyncLock LockState
                If State >= NetState.Finished Then Return
                Log($"[Download] {LocalName}：已中断，当前状态 {State}")
                State = NetState.Interrupted
            End SyncLock
            AbortInternal()
        End Sub
        Private Sub AbortInternal()
            On Error Resume Next
            Reset()
            SyncLock NetManager.LockRemain
                NetManager.FileRemain -= 1
            End SyncLock
        End Sub

        '状态改变接口
        ''' <summary>
        ''' 将该文件设置为已下载完成。
        ''' </summary>
        Public Sub Finish(Optional PrintLog As Boolean = True)
            SyncLock LockState
                If State >= NetState.Finished Then Return
                State = NetState.Finished
            End SyncLock
            SyncLock NetManager.LockRemain
                NetManager.FileRemain -= 1
            End SyncLock
            If PrintLog Then Log($"[Download] {LocalName}：已完成")
            For Each Task In Tasks
                Task.OnFileFinish(Me)
            Next
        End Sub

    End Class
    Private Class RangeNotSupportedException
        Inherits WebException
        Public Sub New(Message As String)
            MyBase.New(Message)
        End Sub
    End Class
    ''' <summary>
    ''' 下载一系列文件的加载器。
    ''' </summary>
    Public Class LoaderDownload
        Inherits LoaderBase

#Region "属性"

        ''' <summary>
        ''' 需要下载的文件。
        ''' </summary>
        Public Files As SafeList(Of NetFile)
        ''' <summary>
        ''' 剩余未完成的文件数。（用于减轻 FilesLock 的占用）
        ''' </summary>
        Private FileRemain As Integer
        Private ReadOnly FileRemainLock As New Object

        ''' <summary>
        ''' 用于显示的百分比进度。
        ''' </summary>
        Public Overrides Property Progress As Double
            Get
                If State >= LoadState.Finished Then Return 1
                If Not Files.Any() Then Return 0 '必须返回 0，否则在获取列表的时候会错觉已经下载完了
                Return _Progress
            End Get
            Set(value As Double)
                Throw New Exception("文件下载不允许指定进度")
            End Set
        End Property
        Private _Progress As Double = 0

        ''' <summary>
        ''' 任务中的文件的连续失败计数。
        ''' </summary>
        Public Property FailCount As Integer
            Get
                Return _FailCount
            End Get
            Set(value As Integer)
                _FailCount = value
                If State = LoadState.Loading AndAlso value >= Math.Min(10000, Math.Max(FileRemain * 5.5, NetTaskThreadLimit * 5.5 + 3)) Then
                    Log("[Download] 由于同加载器中失败次数过多引发强制失败：连续失败了 " & value & " 次", LogLevel.Debug)
                    On Error Resume Next
                    Dim ExList As New List(Of Exception)
                    For Each File In Files
                        For Each Source In File.Sources
                            If Source.Ex IsNot Nothing Then
                                ExList.Add(Source.Ex)
                                If ExList.Count > 10 Then GoTo FinishExCatch
                            End If
                        Next
                    Next
FinishExCatch:
                    OnFail(ExList)
                End If
            End Set
        End Property
        Private _FailCount As Integer = 0

#End Region

        ''' <summary>
        ''' 刷新公开属性。由 NetManager 每 0.1 秒调用一次。
        ''' </summary>
        Public Sub RefreshStat()
            '计算进度
            Dim NewProgress As Double = 0
            Dim TotalProgress As Double = 0
            For Each File In Files
                If File.IsCopy Then
                    NewProgress += File.Progress * 0.2
                    TotalProgress += 0.2
                Else
                    NewProgress += File.Progress
                    TotalProgress += 1
                End If
            Next
            If TotalProgress > 0 AndAlso Not Double.IsNaN(TotalProgress) Then NewProgress /= TotalProgress
            '刷新进度
            _Progress = NewProgress
        End Sub

        Public Sub New(Name As String, FileTasks As List(Of NetFile))
            Me.Name = Name
            Files = New SafeList(Of NetFile)(FileTasks)
        End Sub
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            If Input IsNot Nothing Then Files = New SafeList(Of NetFile)(Input)
            '去重
            Files = New SafeList(Of NetFile)(Files.Distinct(Function(a, b) a.LocalPath = b.LocalPath))
            '设置剩余文件数
            SyncLock FileRemainLock
                FileRemain += Files.Where(Function(f) f.State <> NetState.Finished).Count
            End SyncLock
            State = LoadState.Loading
            '开始执行
            RunInNewThread(
            Sub()
                Try
                    '输入检测
                    If Not Files.Any() Then
                        OnFinish()
                        Return
                    End If
                    For Each File As NetFile In Files
                        If File Is Nothing Then Throw New ArgumentException("存在空文件请求！")
                        For Each Source As NetSource In File.Sources
                            If Not (Source.Url.StartsWithF("https://", True) OrElse Source.Url.StartsWithF("http://", True)) Then
                                Source.Ex = New ArgumentException("输入的下载链接不正确：" & Source.Url)
                                Source.IsFailed = True
                            End If
                        Next
                        If Not File.HasAvailableSource() Then Throw New ArgumentException("输入的下载链接不正确！")
                        File.LocalPath = File.LocalPath.Replace("/", "\")
                        If Not File.LocalPath.ToLower.Contains(":\") Then Throw New ArgumentException("输入的本地文件地址不正确：" & File.LocalPath)
                        If File.LocalPath.EndsWithF("\") Then Throw New ArgumentException("请输入含文件名的完整文件路径：" & File.LocalPath)
                        Directory.CreateDirectory(GetPathFromFullPath(File.LocalPath)) '创建目标文件夹
                    Next
                    '接入下载管理器
                    NetManager.Start(Me)

                    '====================================
                    ' 已存在文件查找
                    '====================================

                    '整理允许进行查找的文件
                    Dim FilesToCheck As New List(Of NetFile)
                    Dim DisabledCopy As Boolean = Setup.Get("SystemDebugSkipCopy") '在设置中禁用了复制
                    For Each File In Files
                        If Not DisabledCopy AndAlso File.Check?.CanUseExistsFile Then
                            FilesToCheck.Add(File)
                        Else '不允许，直接开始下载
                            SyncLock LockState
                                File.State = NetState.WaitingToDownload
                                File.IsCopy = False
                            End SyncLock
                        End If
                    Next
                    If Not FilesToCheck.Any Then Return
                    '获取 MC 文件夹列表
                    Dim Folders As New List(Of String)
                    Folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\") '总是添加官启文件夹，因为 HMCL 会把所有文件存在这里
                    Folders.AddRange(McFolderList.Select(Function(f) f.Location))
                    Folders = Folders.Distinct.Where(Function(f) Directory.Exists(f)).ToList
                    '平均分配到多个检查线程
                    Dim ThreadCount As Integer = MathClamp(FilesToCheck.Count \ 40, 1, 8) '每个线程至少 40 个文件，最多 8 线程
                    If ThreadCount = 1 Then '只有一个线程，直接执行
                        CheckExistingFiles(FilesToCheck, Folders)
                    Else
                        Dim BaseSize = FilesToCheck.Count \ ThreadCount
                        Dim Remainder = FilesToCheck.Count Mod ThreadCount
                        Dim Index = 0
                        For i = 0 To ThreadCount - 1
                            Dim Size = BaseSize + If(i < Remainder, 1, 0)
                            Dim ThreadFiles = FilesToCheck.GetRange(Index, Size)
                            Index += Size
                            RunInNewThread(Sub() CheckExistingFiles(ThreadFiles, Folders), $"下载 文件复制 {Uuid}/{GetUuid()}")
                        Next
                    End If
                Catch ex As Exception
                    OnFail(New List(Of Exception) From {New Exception("下载初始化失败", ex)})
                End Try
            End Sub, "L/下载 " & Uuid)
        End Sub
        Private Sub CheckExistingFiles(Files As List(Of NetFile), FolderList As List(Of String))
            Try
                If ModeDebug Then Log($"[Download] 文件检查线程已启动，分配的文件数：{Files.Count}")
                '列出 MC 文件夹中的各个版本文件夹
                Dim VersionFolders As New List(Of String)
                For Each McFolder In FolderList
                    Dim VersionsFolder As New DirectoryInfo(McFolder & "versions\")
                    If VersionsFolder.Exists() Then
                        For Each VersionFolder In VersionsFolder.GetDirectories
                            VersionFolders.Add(VersionFolder.FullName & "\")
                        Next
                    End If
                Next
                '处理每个文件
                For Each File As NetFile In Files
                    Dim Target As String = CheckExistingFile(FolderList, VersionFolders, File)
                    If File.State >= NetState.WaitingToDownload Then Return '中断
                    '已找到相同文件
                    If Target IsNot Nothing Then
                        File.IsCopy = True
                        Dim RetryCount As Integer = 0
Retry:
                        Try
                            If Target <> File.LocalPath Then
                                Log($"[Download] 复制已存在的文件：{Target} → {File.LocalPath}")
                                CopyFile(Target, File.LocalPath)
                            End If
                            File.Finish(False)
                            Continue For
                        Catch ex As Exception
                            RetryCount += 1
                            Log(ex, $"复制已存在的文件失败，第 {RetryCount} 次重试（{Target} → {File.LocalPath}）")
                            If RetryCount < 3 Then
                                Thread.Sleep(2000)
                                GoTo Retry
                            End If
                        End Try
                    End If
                    '回退到下载
                    SyncLock LockState
                        File.State = NetState.WaitingToDownload
                        File.IsCopy = False
                    End SyncLock
                Next
            Catch ex As Exception
                OnFail(New List(Of Exception) From {New Exception("下载已存在文件查找失败", ex)})
            End Try
        End Sub
        Private Function CheckExistingFile(FolderList As List(Of String), VersionFolders As List(Of String), File As NetFile) As String
            '目标文件已存在
            If File.Check.Check(File.LocalPath) Is Nothing Then Return File.LocalPath
            '没有可用的检查规则，只能开始下载
            If File.Check.Hash Is Nothing AndAlso File.Check.ActualSize < 0 Then Return Nothing
            '大致判断文件类别
            Dim TypeIndexes =
                {"\assets\", "\libraries\", "\versions\", "\mods\", "\coremods\", "\lib\", "\resourcepacks\", "\texturepacks\", "\shaderpacks\"}.
                Select(Function(FolderName) (FolderName, File.LocalPath.IndexOfF(FolderName, True))).
                Where(Function(kv) kv.Item2 >= 0).ToList
            If Not TypeIndexes.Any Then
                If File.LocalName.EndsWithF(".jar") Then
                    TypeIndexes.Add(("\versions\", 1)) '总是对 jar 进行版本文件检查，以包括另存为 jar 的情况
                Else
                    Return Nothing
                End If
            End If
            Dim Type = TypeIndexes.MaxOf(Function(kv) kv.Item2).FolderName.TrimStart("\"c)
            '根据类别进行查找
            Static Sizes As New SafeDictionary(Of String, Long)
            Select Case Type
                Case "assets\", "libraries\"
                    'assets/libraries：查找 MC 文件夹下的相同路径
                    For Each Folder In FolderList
                        Dim Candidate = Folder & Type & File.LocalPath.AfterFirst(Type)
                        If File.Check.Check(Candidate) Is Nothing Then Return Candidate
                    Next
                Case "versions\"
                    '版本 jar 或 json：查找 MC 文件夹下的各个版本文件夹
                    For Each VersionFolder In VersionFolders
                        For Each Candidate In Directory.GetFiles(VersionFolder,
                            "*." & GetFileNameFromPath(File.LocalPath).AfterLast(".").ToLower, SearchOption.TopDirectoryOnly)
                            '快速进行大小校验
                            If Not Sizes.ContainsKey(Candidate) Then Sizes(Candidate) = New FileInfo(Candidate).Length
                            If File.Check.ActualSize <> Sizes(Candidate) Then Continue For
                            'Hash 校验
                            If File.Check.Check(Candidate) Is Nothing Then Return Candidate
                        Next
                    Next
                Case Else
                    '社区资源
                    If File.Check.ActualSize < 0 OrElse File.Check.Hash Is Nothing Then Return Nothing '必须要求指定了文件大小和 Hash
                    For Each Folder In FolderList.Concat(VersionFolders)
                        Dim TargetFolder = Folder & Type
                        If Not Directory.Exists(TargetFolder) Then Continue For
                        For Each Candidate In Directory.GetFiles(TargetFolder)
                            '快速进行大小校验
                            If Not Sizes.ContainsKey(Candidate) Then Sizes(Candidate) = New FileInfo(Candidate).Length
                            If File.Check.ActualSize <> Sizes(Candidate) Then Continue For
                            'Hash 校验
                            If File.Check.Check(Candidate) Is Nothing Then Return Candidate
                        Next
                    Next
            End Select
            Return Nothing
        End Function

        Public Sub OnFileFinish(File As NetFile)
            '要求全部文件完成
            SyncLock FileRemainLock
                FileRemain -= 1
                If FileRemain > 0 Then Return
            End SyncLock
            OnFinish()
        End Sub
        Public Sub OnFinish()
            RaisePreviewFinish()
            SyncLock LockState
                If State > LoadState.Loading Then Return
                State = LoadState.Finished
            End SyncLock
        End Sub
        Public Sub OnFileFail(File As NetFile)
            '将下载源的错误加入主错误列表
            For Each Source In File.Sources
                If Not IsNothing(Source.Ex) Then File.Ex.Add(Source.Ex)
            Next
            OnFail(File.Ex)
        End Sub
        Public Overrides Sub Failed(Ex As Exception)
            OnFail(New List(Of Exception) From {Ex})
        End Sub
        Public Sub OnFail(ExList As List(Of Exception))
            SyncLock LockState
                If State >= LoadState.Finished Then Return
                If ExList Is Nothing OrElse Not ExList.Any() Then ExList = New List(Of Exception) From {New Exception("未知错误！")}
                '寻找有效的错误信息
                Dim UsefulExs = ExList.Where(Function(e) TypeOf e IsNot OperationCanceledException AndAlso TypeOf e IsNot TimeoutException AndAlso TypeOf e IsNot ThreadInterruptedException).ToList
                [Error] = If(UsefulExs.FirstOrDefault, ExList.FirstOrDefault)
                '获取实际失败的文件
                For Each File In Files
                    If File.State <> NetState.Interrupted Then Continue For
                    If File.Sources.All(Function(s) TypeOf s.Ex Is OperationCanceledException OrElse TypeOf s.Ex Is TimeoutException OrElse TypeOf s.Ex Is ThreadInterruptedException) Then Continue For
                    Dim Detail As String = Join(File.Sources.Select(Function(s) $"{If(s.Ex Is Nothing, "无错误信息。", s.Ex.GetBrief())}（{s.Url}）"), vbCrLf)
                    [Error] = New Exception("文件下载失败：" & File.LocalPath & vbCrLf &
                                            "各下载源的错误如下：" & vbCrLf & Detail, [Error])
                    '上报
                    Telemetry("文件下载失败",
                              "FileName", File.LocalName,
                              "Exception", Detail)
                    Exit For
                Next
                '在设置 Error 对象后再更改为失败，避免 WaitForExit 无法捕获错误
                State = LoadState.Failed
            End SyncLock
            '中断所有文件
            For Each TaskFile In Files
                TaskFile.Abort(Me)
            Next
            '在退出同步锁后再进行日志输出
            Dim ErrOutput As New List(Of String)
            For Each Ex As Exception In ExList
                ErrOutput.Add(Ex.GetDetail())
            Next
            Log("[Download] " & Join(ErrOutput.Distinct.ToArray, vbCrLf))
        End Sub
        Public Overrides Sub Abort()
            SyncLock LockState
                If State >= LoadState.Finished Then Return
                State = LoadState.Aborted
            End SyncLock
            Log("[Download] " & Name & " 已取消！")
            '中断所有文件
            For Each TaskFile In Files
                TaskFile.Abort(Me)
            Next
        End Sub

    End Class

    Public NetManager As New NetManagerClass
    ''' <summary>
    ''' 下载文件管理。
    ''' </summary>
    Public Class NetManagerClass

#Region "属性"

        ''' <summary>
        ''' 需要下载的文件。为“本地地址 - 文件对象”键值对。
        ''' </summary>
        Public Files As New Dictionary(Of String, NetFile)
        Public ReadOnly LockFiles As New Object

        ''' <summary>
        ''' 当前的所有下载任务。
        ''' </summary>
        Public Tasks As New SafeList(Of LoaderDownload)

        ''' <summary>
        ''' 已下载完成的大小。
        ''' </summary>
        Public Property DownloadDone As Long
            Get
                Return _DownloadDone
            End Get
            Set(value As Long)
                SyncLock LockDone
                    _DownloadDone = value
                End SyncLock
            End Set
        End Property
        Private _DownloadDone As Long = 0
        Private ReadOnly LockDone As New Object


        ''' <summary>
        ''' 尚未完成下载的文件数。
        ''' </summary>
        Public FileRemain As Integer = 0
        Public ReadOnly LockRemain As New Object

        '这些属性由 RefreshStat 刷新
        ''' <summary>
        ''' 当前的全局下载速度，单位为 Byte / 秒。
        ''' </summary>
        Public Speed As Long = 0

        Public ReadOnly Uuid As Integer = GetUuid()

#End Region

        ''' <summary>
        ''' 进度与下载速度由下载管理线程每隔约 0.1 秒刷新一次。
        ''' </summary>
        Private Sub RefreshStat()
            Try
                Dim DeltaTime As Long = GetTimeMs() - RefreshStatLast
                If DeltaTime = 0 Then Return
                RefreshStatLast += DeltaTime
#Region "刷新整体速度"
                '计算瞬时速度
                Static SpeedLast As New List(Of Long) '记录至多最近 30 次下载速度的记录，较新的在前面
                Static SpeedLastDone As Long = 0 '上次记速时的已下载大小
                Dim ActualSpeed As Double = Math.Max(0, (DownloadDone - SpeedLastDone) / (DeltaTime / 1000))
                SpeedLast.Insert(0, ActualSpeed)
                If SpeedLast.Count >= 31 Then SpeedLast.RemoveAt(30)
                SpeedLastDone = DownloadDone
                '计算用于显示的速度
                Dim SpeedSum As Long = 0, SpeedDiv As Long = 0, Weight = SpeedLast.Count
                For Each SpeedRecord In SpeedLast
                    SpeedSum += SpeedRecord * Weight
                    SpeedDiv += Weight
                    Weight -= 1
                Next
                Speed = If(SpeedDiv > 0, SpeedSum / SpeedDiv, 0)
                '计算新的速度下限
                Dim Limit As Long = 0
                If SpeedLast.Count >= 10 Then Limit = SpeedLast.Take(10).Average * 0.85 '取近 1 秒的平均速度的 85%
                If Limit > NetTaskSpeedLimitLow Then
                    NetTaskSpeedLimitLow = Limit
                    Log("[Download] " & "速度下限已提升到 " & GetString(Limit))
                End If
#End Region
#Region "刷新下载任务属性"
                For Each Task In Tasks
                    Task.RefreshStat()
                Next
#End Region
            Catch ex As Exception
                Log(ex, "刷新下载公开属性失败")
            End Try
        End Sub
        Private RefreshStatLast As Long

        ''' <summary>
        ''' 启动监控线程，用于新增下载线程。
        ''' </summary>
        Private Sub StartManager()
            Static IsStarted As Boolean = False
            If IsStarted Then Return
            IsStarted = True
            Dim ThreadStarter =
            Sub(Id As Integer) '0 或 1
                Try
                    While True
                        Thread.Sleep(20)
                        '获取文件列表
                        Dim AllFiles As List(Of NetFile)
                        SyncLock LockFiles
                            If Id = 0 AndAlso FileRemain = 0 AndAlso Files.Any() Then Files.Clear() '若已完成，则清空
                            AllFiles = Files.Values.ToList()
                        End SyncLock
                        Dim WaitingFiles As New List(Of NetFile)
                        Dim OngoingFiles As New List(Of NetFile)
                        For Each File As NetFile In AllFiles
                            If File.Uuid Mod 2 = Id Then Continue For
                            If File.State = NetState.WaitingToDownload Then
                                WaitingFiles.Add(File)
                            ElseIf File.State < NetState.Merging Then
                                OngoingFiles.Add(File)
                            End If
                        Next
                        '为等待中的文件开始线程
                        For Each File As NetFile In WaitingFiles
                            If NetTaskThreadCount >= NetTaskThreadLimit Then Continue While '最大线程数检查
                            Dim NewThread = File.TryBeginThread()
                            If NewThread IsNot Nothing AndAlso NewThread.Source.Url.Contains("bmclapi") Then Thread.Sleep(100) '减少 BMCLAPI 请求频率
                        Next
                        '为进行中的文件追加线程
                        If Speed >= NetTaskSpeedLimitLow Then Continue While '下载速度足够，无需新增
                        For Each File As NetFile In OngoingFiles
                            If NetTaskThreadCount >= NetTaskThreadLimit Then Continue While '最大线程数检查
                            '线程种类计数
                            Dim PreparingCount = 0, DownloadingCount = 0
                            If File.Threads IsNot Nothing Then
                                For Each Thread As NetThread In File.Threads.ToList
                                    If Thread.State < NetState.Downloading Then
                                        PreparingCount += 1
                                    ElseIf Thread.State = NetState.Downloading Then
                                        DownloadingCount += 1
                                    End If
                                Next
                            End If
                            '新增线程
                            If PreparingCount > DownloadingCount Then Continue For '准备中的线程已多于下载中的线程，不再新增
                            Dim NewThread = File.TryBeginThread()
                            If NewThread IsNot Nothing AndAlso NewThread.Source.Url.Contains("bmclapi") Then Thread.Sleep(100) '减少 BMCLAPI 请求频率
                        Next
                    End While
                Catch ex As Exception
                    Log(ex, $"下载管理启动线程 {Id} 出错", LogLevel.Critical)
                End Try
            End Sub
            RunInNewThread(Sub() ThreadStarter(0), "NetManager ThreadStarter 0")
            RunInNewThread(Sub() ThreadStarter(1), "NetManager ThreadStarter 1")
            RunInNewThread(
            Sub()
                Try
                    Dim NextTick As Long = GetTimeMs()
                    While True
                        '增加限速余量
                        If NetTaskSpeedLimitHigh > 0 Then NetTaskSpeedLimitLeft = NetTaskSpeedLimitHigh / 10
                        '刷新公开属性
                        RefreshStat()
                        '等待 100 ms
                        NextTick += 100
                        Dim SleepTime = NextTick - GetTimeMs()
                        If SleepTime > 0 Then
                            Thread.Sleep(SleepTime)
                        Else
                            NextTick = GetTimeMs() '超时，直接追帧，不等待
                        End If
                    End While
                Catch ex As Exception
                    Log(ex, "下载管理刷新线程出错", LogLevel.Critical)
                End Try
            End Sub, "NetManager StatRefresher")
        End Sub

        Private DownloadCacheLock As New Object
        Private IsDownloadCacheCleared As Boolean = False
        ''' <summary>
        ''' 开始一个下载任务。
        ''' </summary>
        Public Sub Start(Task As LoaderDownload)
            StartManager()
            '清理缓存
            SyncLock DownloadCacheLock '防止同时开启多个下载任务时重复清理
                If Not IsDownloadCacheCleared Then
                    Try
                        Log("[Net] 开始清理下载缓存")
                        DeleteDirectory(PathTemp & "Download")
                        Log("[Net] 下载缓存已清理")
                    Catch ex As Exception
                        Log(ex, "清理下载缓存失败")
                    End Try
                End If
                IsDownloadCacheCleared = True
            End SyncLock
            '文件处理
            SyncLock LockFiles
                '添加每个文件
                For i = 0 To Task.Files.Count - 1
                    Dim File = Task.Files(i)
                    If Files.ContainsKey(File.LocalPath) Then
                        '已有该文件
                        If Files(File.LocalPath).State >= NetState.Finished Then
                            '该文件已经下载过一次，且下载完成
                            '将已下载的文件替换成当前文件，重新下载
                            File.Tasks.Add(Task)
                            Files(File.LocalPath) = File
                            SyncLock LockRemain
                                FileRemain += 1
                            End SyncLock
                            If ModeDebug Then Log($"[Download] {File.LocalName}：重新加入下载列表，目标 {File.LocalPath}")
                        Else
                            '该文件正在下载中
                            '将当前文件替换成下载中的文件，即两个任务指向同一个文件
                            File = Files(File.LocalPath)
                            File.Tasks.Add(Task)
                        End If
                    Else
                        '没有该文件
                        File.Tasks.Add(Task)
                        Files.Add(File.LocalPath, File)
                        SyncLock LockRemain
                            FileRemain += 1
                        End SyncLock
                        If ModeDebug Then Log($"[Download] {File.LocalName}：已加入下载列表，目标 {File.LocalPath}")
                    End If
                    Task.Files(i) = File '回设
                Next
            End SyncLock
            Tasks.Add(Task)
        End Sub

    End Class

    ''' <summary>
    ''' 是否有正在进行中、需要在下载管理页面显示的下载任务？
    ''' </summary>
    Public Function HasDownloadingTask(Optional IgnoreCustomDownload As Boolean = False) As Boolean
        For Each Task In LoaderTaskbar.ToList()
            If (Task.Show AndAlso Task.State = LoadState.Loading) AndAlso
               (Not IgnoreCustomDownload OrElse Not Task.Name.ToString.Contains("自定义下载")) Then
                Return True
            End If
        Next
        Return False
    End Function

#End Region

#Region "端口"

    ''' <summary>
    ''' 随机获取单个可用的端口。
    ''' </summary>
    Public Function FindFreePort() As Integer
        Dim Listener As New TcpListener(IPAddress.Loopback, 0)
        Listener.Start()
        Dim port As Integer = CType(Listener.LocalEndpoint, IPEndPoint).Port
        Listener.Stop()
        Return port
    End Function

    ''' <summary>
    ''' 获取当前已被占用的端口列表。
    ''' </summary>
    Public Function GetUsedPorts() As List(Of Integer)
        Dim IPProperties = NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
        Dim UsedPorts As New List(Of Integer)
        UsedPorts.AddRange(IPProperties.GetActiveTcpListeners().Select(Function(ep) ep.Port))
        UsedPorts.AddRange(IPProperties.GetActiveUdpListeners().Select(Function(ep) ep.Port))
        UsedPorts.AddRange(IPProperties.GetActiveTcpConnections().Select(Function(conn) conn.LocalEndPoint.Port))
        Return UsedPorts.Distinct().ToList()
    End Function

    ''' <summary>
    ''' 寻找数个连续编号的可用端口。
    ''' </summary>
    Public Function FindFreePorts(ConsecutiveCount As Integer, ParamArray ExtraBlackLists As Integer()) As List(Of Integer)
        Dim UsedPorts = GetUsedPorts().Concat(ExtraBlackLists)
        For port = 12000 + RandomInteger(0, 1000) To 65000 - ConsecutiveCount
            Dim Range = Enumerable.Range(port, ConsecutiveCount)
            If Not Range.Any(Function(p) UsedPorts.Contains(p)) Then Return Range.ToList
        Next
        Throw New Exception("未能找到可用的端口！")
    End Function

#End Region

    ''' <summary>
    ''' 测试 Ping。失败则返回 -1。
    ''' </summary>
    Public Function Ping(Ip As String, Optional Timeout As Integer = 10000, Optional MakeLog As Boolean = True) As Integer
        Dim PingResult As NetworkInformation.PingReply
        Try
            PingResult = (New NetworkInformation.Ping).Send(Ip)
        Catch ex As Exception
            If MakeLog Then Log("[Net] Ping " & Ip & " 失败：" & ex.Message)
            Return -1
        End Try
        If PingResult.Status = NetworkInformation.IPStatus.Success Then
            If MakeLog Then Log("[Net] Ping " & Ip & " 结束：" & PingResult.RoundtripTime & "ms")
            Return PingResult.RoundtripTime
        Else
            If MakeLog Then Log("[Net] Ping " & Ip & " 失败")
            Return -1
        End If
    End Function

    ''' <summary>
    ''' 判断某个 Exception 是否为网络问题所导致。
    ''' </summary>
    <Extension> Public Function IsNetworkRelated(Ex As Exception) As Boolean
        '提取 Message（不能用 GetDetail，因为堆栈的方法参数中就有 timeout 字样）
        Dim Detail As String = ""
        Dim InnerEx As Exception = Ex
        Detail += InnerEx.Message
        Do Until InnerEx.InnerException Is Nothing
            InnerEx = InnerEx.InnerException
            Detail += InnerEx.Message
        Loop
        '判断
        If Detail.Contains("(403)") Then Return False
        Return {
            "(408)", "超时", "timeout", "网络请求失败", "连接尝试失败", "远程主机强迫关闭了", "远程方已关闭传输流", "未能解析此远程名称",
            "由于目标计算机积极拒绝", "基础连接已经关闭"
        }.Any(Function(k) Detail.ContainsF(k, True))
    End Function

End Module
