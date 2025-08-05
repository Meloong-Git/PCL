Imports System.Threading.Tasks

Public Class PageDownloadClient

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanBack, Nothing, DlClientListLoader, AddressOf Load_OnFinish)
    End Sub
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub

    Private Sub Load_OnFinish()
        '结果数据化
        Try
            '归类
            Dim Dict As New Dictionary(Of String, List(Of JObject)) From {
                {"正式版", New List(Of JObject)}, {"预览版", New List(Of JObject)}, {"远古版", New List(Of JObject)}, {"愚人节版", New List(Of JObject)}
            }

            Dim Versions As JArray = DlClientListLoader.Output.Value("versions")

            '批量处理，减少字符串操作
            Dim versionList = Versions.Cast(Of JObject)().ToList()
            Parallel.ForEach(versionList, Sub(version)
                ProcessVersionItem(version, Dict)
            End Sub)

            '排序 - 使用并行排序提升性能
            Parallel.ForEach(Dict.Keys, Sub(key)
                Dict(key) = Dict(key).OrderByDescending(Function(v) v("releaseTime").Value(Of Date)).ToList()
            End Sub)

            '在UI线程中更新界面
            RunInUi(Sub() UpdateUI(Dict))

        Catch ex As Exception
            Log(ex, "可视化 MC 版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    Private Sub ProcessVersionItem(version As JObject, dict As Dictionary(Of String, List(Of JObject)))
        '确定分类（线程安全的版本）
        Dim versionType As String = version("type")?.ToString()
        Dim versionId As String = version("id")?.ToString()
        Dim typeCategory As String

        Select Case versionType
            Case "release"
                typeCategory = "正式版"
            Case "snapshot"
                typeCategory = "预览版"
                '优化的Mojang误分类检查
                If versionId?.StartsWith("1.") AndAlso
                   Not ContainsAny(versionId.ToLower(), {"combat", "rc", "experimental", "pre"}) Then
                    typeCategory = "正式版"
                    version("type") = "release"
                End If

                '愚人节版本检查
                If IsAprilFoolsVersion(versionId, version) Then
                    typeCategory = "愚人节版"
                End If
            Case "special"
                typeCategory = "愚人节版"
            Case Else
                typeCategory = "远古版"
        End Select

        '线程安全地添加到字典
        SyncLock dict(typeCategory)
            dict(typeCategory).Add(version)
        End SyncLock
    End Sub

    Private Function ContainsAny(text As String, keywords As String()) As Boolean
        Return keywords.Any(Function(keyword) text.Contains(keyword))
    End Function

    Private Function IsAprilFoolsVersion(versionId As String, version As JObject) As Boolean
        Dim aprilFoolsVersions As String() = {
            "20w14infinite", "20w14∞", "3d shareware v1.34", "1.rv-pre1",
            "15w14a", "2.0", "22w13oneblockatatime", "23w13a_or_b",
            "24w14potato", "25w14craftmine"
        }

        If aprilFoolsVersions.Contains(versionId?.ToLower()) Then
            version("type") = "special"
            If versionId = "20w14infinite" Then version("id") = "20w14∞"
            version.Add("lore", GetMcFoolName(version("id")))
            Return True
        End If

        '4/1 自动检测
        Try
            Dim releaseDate = version("releaseTime")?.Value(Of Date).ToUniversalTime().AddHours(2)
            If releaseDate.HasValue AndAlso releaseDate.Value.Month = 4 AndAlso releaseDate.Value.Day = 1 Then
                version("type") = "special"
                Return True
            End If
        Catch
            '忽略日期解析错误
        End Try

        Return False
    End Function

    Private Sub UpdateUI(dict As Dictionary(Of String, List(Of JObject)))
        '排序
        For i = 0 To dict.Keys.Count - 1
            dict(dict.Keys(i)) = dict.Values(i).OrderByDescending(Function(v) v("releaseTime").Value(Of Date)).ToList
        Next
        '清空当前
        PanMain.Children.Clear()
        '添加最新版本
        Dim CardInfo As New MyCard With {.Title = "最新版本", .Margin = New Thickness(0, 0, 0, 15), .SwapType = 2}
        Dim TopestVersions As New List(Of JObject)
        Dim Release As JObject = dict("正式版")(0).DeepClone()
        Release("lore") = "最新正式版，发布于 " & Release("releaseTime").Value(Of Date).ToString("yyyy'/'MM'/'dd HH':'mm")
        TopestVersions.Add(Release)
        If dict("正式版")(0)("releaseTime").Value(Of Date) < dict("预览版")(0)("releaseTime").Value(Of Date) Then
            Dim Snapshot As JObject = dict("预览版")(0).DeepClone()
            Snapshot("lore") = "最新预览版，发布于 " & Snapshot("releaseTime").Value(Of Date).ToString("yyyy'/'MM'/'dd HH':'mm")
            TopestVersions.Add(Snapshot)
        End If
        Dim PanInfo As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = TopestVersions}
        MyCard.StackInstall(PanInfo, 2)
        CardInfo.Children.Add(PanInfo)
        PanMain.Children.Add(CardInfo)
        '添加其他版本
        For Each Pair As KeyValuePair(Of String, List(Of JObject)) In dict
            If Not Pair.Value.Any() Then Continue For
            '增加卡片
            Dim NewCard As New MyCard With {.Title = Pair.Key & " (" & Pair.Value.Count & ")", .Margin = New Thickness(0, 0, 0, 15), .SwapType = 2}
            Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = Pair.Value}
            NewCard.Children.Add(NewStack)
            NewCard.SwapControl = NewStack
            NewCard.IsSwaped = True
            PanMain.Children.Add(NewCard)
        Next
    End Sub

    Public Sub DownloadStart(sender As MyListItem, e As Object)
        McDownloadClient(NetPreDownloadBehaviour.HintWhileExists, sender.Title, sender.Tag("url").ToString)
    End Sub

    ''介绍栏
    'Private Sub BtnWeb_Click(sender As Object, e As EventArgs) Handles BtnWeb.Click
    '    OpenWebsite("https://www.minecraft.net/zh-hans")
    'End Sub
    'Private Sub BtnInstall_Click(sender As Object, e As EventArgs) Handles BtnInstall.Click
    '    FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    'End Sub

End Class
