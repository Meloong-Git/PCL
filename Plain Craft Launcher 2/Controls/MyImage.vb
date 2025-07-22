Public Class MyImage
    Inherits Image

#Region "公开属性"

    ''' <summary>
    ''' 网络图片的缓存有效期。
    ''' 在这个时间后，才会重新尝试下载图片。
    ''' </summary>
    Public FileCacheExpiredTime As New TimeSpan(7, 0, 0, 0) '7 天

    ''' <summary>
    ''' 是否允许将网络图片存储到本地用作缓存。
    ''' </summary>
    Public Property EnableCache As Boolean
        Get
            Return GetValue(EnableCacheProperty)
        End Get
        Set(value As Boolean)
            SetValue(EnableCacheProperty, value)
        End Set
    End Property
    Public Shared Shadows ReadOnly EnableCacheProperty As DependencyProperty = DependencyProperty.Register(
        "EnableCache", GetType(Boolean), GetType(MyImage), New PropertyMetadata(True))

    ''' <summary>
    ''' 与 Image 的 Source 类似。
    ''' 若输入以 http 开头的字符串，则会尝试下载图片然后显示，图片会保存为本地缓存。
    ''' 支持 WebP 格式的图片。
    ''' </summary>
    Public Shadows Property Source As String '覆写 Image 的 Source 属性
        Get
            Return _Source
        End Get
        Set(value As String)
            If value = "" Then value = Nothing
            If _Source = value Then Return
            _Source = value
            If Not IsInitialized Then Return '属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
            Load()
        End Set
    End Property
    Private _Source As String = ""
    Public Shared Shadows ReadOnly SourceProperty As DependencyProperty = DependencyProperty.Register(
        "Source", GetType(String), GetType(MyImage), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender, e) If sender IsNot Nothing Then CType(sender, MyImage).Source = e.NewValue.ToString())))

    ''' <summary>
    ''' 当 Source 首次下载失败时，会从该备用地址加载图片。
    ''' </summary>
    Public Property FallbackSource As String
        Get
            Return _FallbackSource
        End Get
        Set(value As String)
            _FallbackSource = value
        End Set
    End Property
    Private _FallbackSource As String = Nothing

    ''' <summary>
    ''' 正在下载网络图片时显示的本地图片。
    ''' </summary>
    Public Property LoadingSource As String
        Get
            Return _LoadingSource
        End Get
        Set(value As String)
            _LoadingSource = value
        End Set
    End Property
    Private _LoadingSource As String = "pack://application:,,,/images/Icons/NoIcon.png"

#End Region

    ''' <summary>
    ''' 实际被呈现的图片地址。
    ''' </summary>
    Public Property ActualSource As String
        Get
            Return _ActualSource
        End Get
        Set(value As String)
            If value = "" Then value = Nothing
            If _ActualSource = value Then Return
            _ActualSource = value
            Try
                Dim Bitmap As MyBitmap = If(value Is Nothing, Nothing, New MyBitmap(value)) '在这里先触发可能的文件读取，尽量避免在 UI 线程中读取文件
                RunInUiWait(Sub() MyBase.Source = Bitmap)
            Catch ex As Exception
                Log(ex, $"加载图片失败（{value}）")
                Try
                    If value.StartsWithF(PathTemp) AndAlso File.Exists(value) Then File.Delete(value)
                Catch
                End Try
            End Try
        End Set
    End Property
    Private _ActualSource As String = Nothing

    Private Sub Load() _
        Handles Me.Initialized '属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
        '空
        If Source Is Nothing Then
            ActualSource = Nothing
            Return
        End If
        '本地图片
        If Not Source.StartsWithF("http") Then
            ActualSource = Source
            Return
        End If
        '从缓存加载网络图片
        Dim Url As String = Source
        Dim Retried As Boolean = False
        Dim TempPath As String = GetTempPath(Url)
        Dim TempFile As New FileInfo(TempPath)
        Dim EnableCache As Boolean = Me.EnableCache
        If EnableCache AndAlso TempFile.Exists Then
            ActualSource = TempPath
            If (Date.Now - TempFile.LastWriteTime) < FileCacheExpiredTime Then Return '无需刷新缓存
        End If
        RunInNewThread(
        Sub()
            Dim TempDownloadingPath As String = Nothing
            Try
RetryStart:
                '下载
                ActualSource = LoadingSource '显示加载中图片
                TempDownloadingPath = TempPath & RandomInteger(0, 10000000)
                Directory.CreateDirectory(GetPathFromFullPath(TempPath)) '重新实现下载，以避免携带 Header（#5072）
                Using Client As New Net.WebClient
                    Client.DownloadFile(Url, TempDownloadingPath)
                End Using
                If Url <> Source AndAlso Url <> FallbackSource Then
                    '已经更换了地址
                    File.Delete(TempDownloadingPath)
                ElseIf EnableCache Then
                    '保存缓存并显示
                    If File.Exists(TempPath) Then File.Delete(TempPath)
                    FileSystem.Rename(TempDownloadingPath, TempPath)
                    RunInUi(Sub() ActualSource = TempPath)
                Else
                    '直接显示
                    RunInUiWait(Sub() ActualSource = TempDownloadingPath)
                    File.Delete(TempDownloadingPath)
                End If
            Catch ex As Exception
                Try
                    If TempPath IsNot Nothing Then File.Delete(TempPath)
                    If TempDownloadingPath IsNot Nothing Then File.Delete(TempDownloadingPath)
                Catch
                End Try
                If Not Retried Then
                    '更换备用地址
                    Log(ex, $"下载图片可重试地失败（{Url}）", LogLevel.Developer)
                    Retried = True
                    Url = If(FallbackSource, Source)
                    '空
                    If Url Is Nothing Then
                        ActualSource = Nothing
                        Return
                    End If
                    '本地图片
                    If Not Url.StartsWithF("http") Then
                        ActualSource = Url
                        Return
                    End If
                    '从缓存加载网络图片
                    TempPath = GetTempPath(Url)
                    TempFile = New FileInfo(TempPath)
                    If EnableCache AndAlso TempFile.Exists() Then
                        ActualSource = TempPath
                        If (Date.Now - TempFile.CreationTime) < FileCacheExpiredTime Then Return '无需刷新缓存
                    End If
                    '下载
                    If Source = Url Then Thread.Sleep(1000) '延迟 1s 重试
                    GoTo RetryStart
                Else
                    Log(ex, $"下载图片失败（{Url}）", LogLevel.Hint)
                End If
            End Try
        End Sub, "MyImage PicLoader " & GetUuid() & "#", ThreadPriority.BelowNormal)
    End Sub
    Public Shared Function GetTempPath(Url As String) As String
        Return $"{PathTemp}MyImage\{GetHash(Url)}.png"
    End Function

End Class