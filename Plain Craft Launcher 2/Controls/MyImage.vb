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
    ''' 若输入以 http 开头的字符串，则会尝试下载图片然后显示；若 EnableCache 设为 True，图片还会保存为本地缓存。
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
    ''' 若 Source 是一个网络图片，该地址将作为第二图片源。
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
                Dim Bitmap As ImageSource = If(value Is Nothing, Nothing, New MyBitmap(value)) '在这里先触发可能的文件读取，尽量避免在 UI 线程中读取文件
                RunInUi(Sub() MyBase.Source = Bitmap)
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
        Dim EnableCache As Boolean = Me.EnableCache
        Dim TempPath As String = $"{PathTemp}MyImage\{GetHash(Source)}.png{If(EnableCache, "", GetUuid())}" '不启用缓存时在末尾加上随机字符串，避免冲突
        Dim TempFile As New FileInfo(TempPath)
        If EnableCache AndAlso TempFile.Exists Then
            ActualSource = TempPath '显示缓存的图片
            If (Date.Now - TempFile.LastWriteTime) < FileCacheExpiredTime Then Return '无需刷新缓存
        Else
            ActualSource = LoadingSource '显示加载中图片
        End If
        RunInNewThread(
        Sub()
            Dim IsLocalFallback As Boolean = Not String.IsNullOrEmpty(FallbackSource) AndAlso Not FallbackSource.StartsWithF("http")
            Try
                '下载
                NetDownloadByLoader(
                    If(FallbackSource IsNot Nothing AndAlso FallbackSource.StartsWithF("http") AndAlso Source <> FallbackSource,
                        {Source, FallbackSource}, {Source}),
                    TempPath, SimulateBrowserHeaders:=True)
                ActualSource = TempPath
                If Not EnableCache Then File.Delete(TempPath)
            Catch ex As Exception
                Try
                    If TempPath IsNot Nothing Then File.Delete(TempPath)
                Catch
                End Try
                '加载本地备用图片
                If IsLocalFallback Then
                    Try
                        ActualSource = FallbackSource
                        Log(ex, $"下载图片失败，使用本地备用图片（{Source}）")
                        Return
                    Catch exx As Exception
                        Log(ex, $"下载图片失败（{Source}）", LogLevel.Hint)
                        Log(exx, $"加载备用图片失败（{FallbackSource}）", LogLevel.Hint)
                    End Try
                Else
                    Log(ex, $"下载图片失败（{Source}，备用：{FallbackSource}）", LogLevel.Hint)
                End If
            End Try
        End Sub, "MyImage PicLoader " & GetUuid() & "#", ThreadPriority.BelowNormal)
    End Sub

End Class