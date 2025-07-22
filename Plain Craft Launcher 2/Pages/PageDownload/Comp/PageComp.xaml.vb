Imports System.Windows.Markup

<ContentProperty("SearchTags")>
Public Class PageComp

#Region "属性"

    ''' <summary>
    ''' 用于 XAML 快速设置的 Tag 下拉框列表。
    ''' </summary>
    Public ReadOnly Property SearchTags As ItemCollection
        Get
            Return ComboSearchTag.Items
        End Get
    End Property

    ''' <summary>
    ''' 英文前后不含空格的可读资源类型名，例如 "Mod"、"整合包"。
    ''' </summary>
    Public Property TypeName As String
        Get
            Return _TypeName
        End Get
        Set(Value As String)
            If _TypeName = Value Then Return
            _TypeName = Value
            Loader.Name = $"社区资源获取：{Value}"
        End Set
    End Property
    Private _TypeName As String = ""

    ''' <summary>
    ''' 英文前后含一个空格的可读资源类型名，例如 " Mod "、"整合包"。
    ''' </summary>
    Public Property TypeNameSpaced As String
        Get
            Return _TypeNameSpaced
        End Get
        Set(Value As String)
            If _TypeNameSpaced = Value Then Return
            _TypeNameSpaced = Value
            PanAlways.Title = $"搜索{Value}"
            Load.Text = $"正在获取{Value}列表"
        End Set
    End Property
    Private _TypeNameSpaced As String = ""

    ''' <summary>
    ''' 该页面对应的资源类型。
    ''' </summary>
    Public Property PageType As CompType
        Get
            Return _Type
        End Get
        Set(Value As CompType)
            If _Type = Value Then Return
            _Type = Value
            BtnSearchInstallModPack.Visibility = If(Value = CompType.ModPack, Visibility.Visible, Visibility.Collapsed)
        End Set
    End Property
    Private _Type As CompType = -1

#End Region

#Region "加载"

    ''' <summary>
    ''' 在切换到页面时，应自动将筛选项设置为与该目标 MC 版本和加载器相同。
    ''' </summary>
    Public Shared TargetVersion As McVersion = Nothing
    ''' <summary>
    ''' 在切换到该页面时自动设置的搜索框内的内容。
    ''' </summary>
    Public Shared TargetName As String = Nothing

    '在点击 MyCompItem 时会获取 Loader 的输入，以使资源详情页面可以应用相同的筛选项
    Public Loader As New LoaderTask(Of CompProjectRequest, Integer)("社区资源获取：XXX", AddressOf CompProjectsGet, AddressOf LoaderInput) With {.ReloadTimeout = 60 * 1000}

    Private IsLoaderInited As Boolean = False
    Private Sub PageCompControls_Inited(sender As Object, e As EventArgs) Handles Me.Loaded
        '不知道从 Initialized 改成 Loaded 会不会有问题，但用 Initialized 会导致初始的筛选器修改被覆盖回默认值
        If TargetVersion IsNot Nothing Then
            '设置目标
            ResetFilter() '重置筛选器
            TextSearchVersion.Text = TargetVersion.Version.McName
            Dim GetTargetItemByName =
            Function(Name As String) As MyComboBoxItem
                For Each Item As MyComboBoxItem In ComboSearchLoader.Items
                    If Item.Content = Name Then Return Item
                Next
                Return ComboSearchLoader.Items(0)
            End Function
            If TargetVersion.Version.HasForge Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Forge")
            ElseIf TargetVersion.Version.HasFabric Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Fabric")
            ElseIf TargetVersion.Version.HasNeoForge Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("NeoForge")
            End If
            TargetVersion = Nothing
            If TargetName IsNot Nothing Then
                '设置搜索框内容
                TextSearchName.Text = TargetName
                TargetName = Nothing
            End If
            '如果已经完成请求，则重新开始
            If IsLoaderInited Then StartNewSearch()
            ScrollToHome()
        End If
        '加载器初始化
        If IsLoaderInited Then Return
        IsLoaderInited = True
        CType(Parent, MyPageRight).PageLoaderInit(Load, PanLoad, PanContent, PanAlways, Loader, AddressOf Load_OnFinish, AddressOf LoaderInput)
        If McVersionHighest = -1 Then McVersionHighest = Math.Max(McVersionHighest, Integer.Parse(CType(TextSearchVersion.Items(1), MyComboBoxItem).Content.ToString.Split(".")(1)))
    End Sub
    Private Function LoaderInput() As CompProjectRequest
        Dim Request As New CompProjectRequest(PageType, Storage, (Page + 1) * PageSize)
        Dim GameVersion As String = If(TextSearchVersion.Text = "全部 (也可自行输入)", Nothing,
                If(TextSearchVersion.Text.Contains(".") OrElse TextSearchVersion.Text.Contains("w"), TextSearchVersion.Text, Nothing))
        With Request
            .SearchText = TextSearchName.Text
            .GameVersion = GameVersion
            .Tag = ComboSearchTag.SelectedItem.Tag
            .ModLoader = If(PageType = CompType.Mod, Val(ComboSearchLoader.SelectedItem.Tag), CompModLoaderType.Any)
            .Source = CType(Val(ComboSearchSource.SelectedItem.Tag), CompSourceType)
        End With
        Return Request
    End Function

#End Region

    Public Storage As New CompProjectStorage
    ''' <summary>
    ''' 每页展示的结果数量。
    ''' </summary>
    Public Const PageSize = 40
    Public Page As Integer = 0

    '结果 UI 化
    Private Sub Load_OnFinish()
        Try
            Log($"[Comp] 开始可视化{TypeNameSpaced}列表，已储藏 {Storage.Results.Count} 个结果，当前在第 {Page + 1} 页")
            '列表项
            PanProjects.Children.Clear()
            For i = Math.Min(Page * PageSize, Storage.Results.Count - 1) To Math.Min((Page + 1) * PageSize - 1, Storage.Results.Count - 1)
                PanProjects.Children.Add(Storage.Results(i).ToCompItem(
                    ShowMcVersionDesc:=Loader.Input.GameVersion Is Nothing,
                    ShowLoaderDesc:=Loader.Input.ModLoader = CompModLoaderType.Any AndAlso (PageType = CompType.Mod OrElse PageType = CompType.ModPack)))
            Next
            '页码
            CardPages.Visibility = If(Storage.Results.Count > 40 OrElse
                                      Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal,
                                      Visibility.Visible, Visibility.Collapsed)
            LabPage.Text = Page + 1
            BtnPageFirst.IsEnabled = Page > 1
            BtnPageFirst.Opacity = If(Page > 1, 1, 0.2)
            BtnPageLeft.IsEnabled = Page > 0
            BtnPageLeft.Opacity = If(Page > 0, 1, 0.2)
            Dim IsRightEnabled As Boolean = '由于 WPF 的未知 bug，读取到的 IsEnabled 可能是错误的值（#3319）
                Storage.Results.Count > PageSize * (Page + 1) OrElse
                Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal
            BtnPageRight.IsEnabled = IsRightEnabled
            BtnPageRight.Opacity = If(IsRightEnabled, 1, 0.2)
            '错误信息
            If Storage.ErrorMessage Is Nothing Then
                HintError.Visibility = Visibility.Collapsed
            Else
                HintError.Visibility = Visibility.Visible
                HintError.Text = Storage.ErrorMessage
            End If
            '强制返回顶部
            ScrollToTop()
        Catch ex As Exception
            Log(ex, $"可视化{TypeNameSpaced}列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '自动重试
    Private Sub Load_State(sender As Object, state As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState) Handles Load.StateChanged
        Select Case Loader.State
            Case LoadState.Failed
                Dim ErrorMessage As String = ""
                If Loader.Error IsNot Nothing Then ErrorMessage = Loader.Error.Message
                If ErrorMessage.Contains("不是有效的 json 文件") Then
                    Log($"[Download] 下载的{TypeNameSpaced}列表 json 文件损坏，已自动重试", LogLevel.Debug)
                    CType(Parent, MyPageRight).PageLoaderRestart()
                End If
        End Select
    End Sub

    '切换页码
    Private Sub BtnPageFirst_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageFirst.Click
        ChangePage(0)
    End Sub
    Private Sub BtnPageLeft_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageLeft.Click
        ChangePage(Page - 1)
    End Sub
    Private Sub BtnPageRight_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageRight.Click
        ChangePage(Page + 1)
    End Sub
    Private Sub ChangePage(NewPage As Integer)
        CardPages.IsEnabled = False
        Page = NewPage
        FrmMain.BackToTop()
        Log($"[Download] {TypeName}：切换到第 {Page + 1} 页")
        RunInThread(
        Sub()
            Thread.Sleep(100) '等待向上滚的动画结束
            RunInUi(Sub() CardPages.IsEnabled = True)
            Loader.Start()
        End Sub)
    End Sub

#Region "搜索"

    '搜索按钮
    Private Sub StartNewSearch() Handles BtnSearchRun.Click
        Page = 0
        If Loader.ShouldStart(LoaderInput()) Then Storage = New CompProjectStorage '避免连续搜索两次使得 CompProjectStorage 引用丢失（#1311）
        Loader.Start()
    End Sub
    Private Sub EnterTrigger(sender As Object, e As KeyEventArgs) Handles TextSearchName.KeyDown, TextSearchVersion.KeyDown
        If e.Key = Key.Enter Then StartNewSearch()
    End Sub

    '重置按钮
    Private Sub ResetFilter() Handles BtnSearchReset.Click
        TextSearchName.Text = ""
        TextSearchVersion.Text = "全部 (也可自行输入)"
        TextSearchVersion.SelectedIndex = 0
        ComboSearchSource.SelectedIndex = 0
        ComboSearchTag.SelectedIndex = 0
        ComboSearchLoader.SelectedIndex = 0
        Loader.LastFinishedTime = 0 '要求强制重新开始
    End Sub

    '版本选择
    '#3067：当下拉菜单展开时，程序会被 WPF 挂起，因而无法更新 Grid 布局，所以必须延迟到下拉菜单收起后才能更新
    Private Sub TextSearchVersion_TextChanged() Handles TextSearchVersion.TextChanged
        If Not TextSearchVersion.IsDropDownOpen Then UpdateSearchLoaderVisibility()
    End Sub
    Private Sub UpdateSearchLoaderVisibility() Handles TextSearchVersion.DropDownClosed
        If PageType = CompType.Mod AndAlso (TextSearchVersion.Text.Contains(".") OrElse TextSearchVersion.Text.Contains("w")) Then
            ComboSearchLoader.Visibility = Visibility.Visible
            Grid.SetColumnSpan(TextSearchVersion, 1)
        Else
            ComboSearchLoader.Visibility = Visibility.Collapsed
            Grid.SetColumnSpan(TextSearchVersion, 2)
            ComboSearchLoader.SelectedIndex = 0
        End If
    End Sub

#End Region

    '安装已有整合包按钮
    Private Sub BtnSearchInstallModPack_Click(sender As Object, e As EventArgs) Handles BtnSearchInstallModPack.Click
        ModpackInstall()
    End Sub

End Class
