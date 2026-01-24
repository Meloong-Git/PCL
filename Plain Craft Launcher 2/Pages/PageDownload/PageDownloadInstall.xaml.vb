Public Class PageDownloadInstall

    Private Sub LoaderInit() Handles Me.Initialized
        DisabledPageAnimControls.Add(BtnStart)
        PageLoaderInit(LoadMinecraft, PanLoad, PanAllBack, Nothing, DlClientListLoader, AddressOf LoadMinecraft_OnFinish)
    End Sub

    Private IsLoad As Boolean = False
    Private Sub Init() Handles Me.Loaded
        PanBack.ScrollToHome()
        DlOptiFineListLoader.Start()
        DlLiteLoaderListLoader.Start()
        DlFabricListLoader.Start()
        DlNeoForgeListLoader.Start()

        '重载预览
        TextSelectName.ValidateRules = New ObjectModel.Collection(Of Validate) From {New ValidateFolderName(McFolderSelected & "versions")}
        TextSelectName.Validate()
        ReloadSelected()

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True

        McDownloadForgeRecommendedRefresh()

        LoadOptiFine.State = DlOptiFineListLoader
        LoadLiteLoader.State = DlLiteLoaderListLoader
        LoadFabric.State = DlFabricListLoader
        LoadFabricApi.State = DlFabricApiLoader
        LoadNeoForge.State = DlNeoForgeListLoader
        LoadOptiFabric.State = DlOptiFabricLoader
    End Sub

#Region "页面切换"

    '页面切换动画
    Public IsInSelectPage As Boolean = False
    Private IsFirstLoaded As Boolean = False
    Private Sub EnterSelectPage()
        If IsInSelectPage Then Return
        IsInSelectPage = True

        PanInner.Margin = New Thickness(25, 10, 25, 40)

        AutoSelectedFabricApi = False
        AutoSelectedOptiFabric = False
        IsSelectNameEdited = False
        PanSelect.Visibility = Visibility.Visible
        PanSelect.IsHitTestVisible = True
        PanMinecraft.IsHitTestVisible = False
        PanBack.IsHitTestVisible = False
        PanBack.ScrollToHome()

        DisabledPageAnimControls.Remove(BtnStart)
        BtnStart.Show = True
        CardOptiFine.IsSwapped = True
        CardLiteLoader.IsSwapped = True
        CardForge.IsSwapped = True
        CardNeoForge.IsSwapped = True
        CardFabric.IsSwapped = True
        CardFabricApi.IsSwapped = True
        CardOptiFabric.IsSwapped = True

        '如果在选择页面按了刷新键，选择页的东西可能会由于动画被隐藏，但不会由于加载结束而再次显示，因此这里需要手动恢复
        For Each Control In GetAllAnimControls(PanSelect)
            Control.Opacity = 1
            If Control.RenderTransform Is Nothing OrElse TypeOf Control.RenderTransform Is TranslateTransform Then
                Control.RenderTransform = New TranslateTransform
            End If
        Next

        '启动 Forge 加载
        If McVersion.IsFormatFit(VanillaName) Then
            Dim ForgeLoader = New LoaderTask(Of String, List(Of DlForgeVersionEntry))("DlForgeVersion " & VanillaName, AddressOf DlForgeVersionMain)
            LoadForge.State = ForgeLoader
            ForgeLoader.Start(VanillaName)
        End If

        '启动 Fabric API、OptiFabric 加载
        DlFabricApiLoader.Start()
        DlOptiFabricLoader.Start()

        AniStart({
            AaOpacity(PanMinecraft, -PanMinecraft.Opacity, 70, 10),
            AaTranslateX(PanMinecraft, -50 - CType(PanMinecraft.RenderTransform, TranslateTransform).X, 90, 10),
            AaCode(
            Sub()
                PanBack.ScrollToHome()
                TextSelectName.Validate()
                OptiFine_Loaded()
                LiteLoader_Loaded()
                Forge_Loaded()
                NeoForge_Loaded()
                Fabric_Loaded()
                FabricApi_Loaded()
                OptiFabric_Loaded()
                ReloadSelected()
                PanMinecraft.Visibility = Visibility.Collapsed
            End Sub, After:=True),
            AaOpacity(PanSelect, 1 - PanSelect.Opacity, 70, 100),
            AaTranslateX(PanSelect, -CType(PanSelect.RenderTransform, TranslateTransform).X, 160, 100, Ease:=New AniEaseOutFluent(AniEasePower.ExtraStrong)),
            AaCode(
            Sub()
                PanBack.IsHitTestVisible = True
                '初始化 Binding
                If IsFirstLoaded Then Return
                IsFirstLoaded = True
                BtnOptiFineClearInner.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = CardOptiFine.MainTextBlock, .Mode = BindingMode.OneWay})
                BtnLiteLoaderClearInner.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = CardLiteLoader.MainTextBlock, .Mode = BindingMode.OneWay})
                BtnForgeClearInner.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = CardForge.MainTextBlock, .Mode = BindingMode.OneWay})
                BtnNeoForgeClearInner.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = CardNeoForge.MainTextBlock, .Mode = BindingMode.OneWay})
                BtnFabricClearInner.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = CardFabric.MainTextBlock, .Mode = BindingMode.OneWay})
                BtnFabricApiClearInner.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = CardFabricApi.MainTextBlock, .Mode = BindingMode.OneWay})
                BtnOptiFabricClearInner.SetBinding(Shapes.Path.FillProperty, New Binding("Foreground") With {.Source = CardOptiFabric.MainTextBlock, .Mode = BindingMode.OneWay})
            End Sub,, True)
        }, "FrmDownloadInstall SelectPageSwitch", True)
    End Sub
    Public Sub ExitSelectPage() Handles BtnBack.Click
        If Not IsInSelectPage Then Return
        IsInSelectPage = False

        PanInner.Margin = New Thickness(25, 10, 25, 25)

        DisabledPageAnimControls.Add(BtnStart)
        BtnStart.Show = False
        ClearSelected() '清除已选择项
        PanMinecraft.Visibility = Visibility.Visible
        PanSelect.IsHitTestVisible = False
        PanMinecraft.IsHitTestVisible = True
        PanBack.IsHitTestVisible = False
        PanBack.ScrollToHome()

        AniStart({
            AaOpacity(PanSelect, -PanSelect.Opacity, 70, 10),
            AaTranslateX(PanSelect, 50 - CType(PanSelect.RenderTransform, TranslateTransform).X, 90, 10),
            AaCode(Sub() PanBack.ScrollToHome(), After:=True),
            AaOpacity(PanMinecraft, 1 - PanMinecraft.Opacity, 70, 100),
            AaTranslateX(PanMinecraft, -CType(PanMinecraft.RenderTransform, TranslateTransform).X, 160, 100, Ease:=New AniEaseOutFluent(AniEasePower.ExtraStrong)),
            AaCode(
            Sub()
                PanSelect.Visibility = Visibility.Collapsed
                PanBack.IsHitTestVisible = True
            End Sub,, True)
        }, "FrmDownloadInstall SelectPageSwitch")
    End Sub
    Public Sub MinecraftSelected(sender As MyListItem, e As MouseButtonEventArgs)
        VanillaName = sender.Title
        VanillaData = sender.Tag
        VanillaIcon = sender.Logo
        EnterSelectPage()
    End Sub

#End Region

#Region "选择"

    'Minecraft
    Private VanillaName As String
    Private VanillaData As JObject
    Private VanillaIcon As String
    Private ReadOnly Property VanillaDrop As Integer
        Get
            Return McVersion.VersionToDrop(VanillaName, True)
        End Get
    End Property

    '附加组件
    Private SelectedOptiFine As DlOptiFineListEntry = Nothing
    Private SelectedLiteLoader As DlLiteLoaderListEntry = Nothing
    Private SelectedForge As DlForgeVersionEntry = Nothing
    Private SelectedNeoForge As DlNeoForgeListEntry = Nothing
    Private SelectedFabric As String = Nothing
    Private SelectedFabricApi As CompFile = Nothing
    Private SelectedOptiFabric As CompFile = Nothing

    ''' <summary>
    ''' 重载已选择的项目的显示。
    ''' </summary>
    Private Sub ReloadSelected() Handles CardOptiFine.Swap, LoadOptiFine.StateChanged, CardForge.Swap, LoadForge.StateChanged, CardNeoForge.Swap, LoadNeoForge.StateChanged, CardFabric.Swap, LoadFabric.StateChanged, CardFabricApi.Swap, LoadFabricApi.StateChanged, CardOptiFabric.Swap, LoadOptiFabric.StateChanged, CardLiteLoader.Swap, LoadLiteLoader.StateChanged
        Static Ongoing As Boolean = False '#3742 中，LoadOptiFineGetError 会初始化 LoadOptiFine，触发事件 LoadOptiFine.StateChanged，导致再次调用 SelectReload
        If VanillaName Is Nothing OrElse Ongoing Then Return
        Ongoing = True
        '主预览
        SelectNameUpdate()
        ImgLogo.Source = GetSelectLogo()
        'OptiFine
        Dim OptiFineError As String = LoadOptiFineGetError()
        CardOptiFine.MainSwap.Visibility = If(OptiFineError Is Nothing, Visibility.Visible, Visibility.Collapsed)
        If OptiFineError IsNot Nothing Then CardOptiFine.IsSwapped = True '例如在同时展开卡片时选择了不兼容项则强制折叠
        SetPanelVisibility(PanOptiFineInfo, CardOptiFine.IsSwapped)
        If SelectedOptiFine Is Nothing Then
            BtnOptiFineClear.Visibility = Visibility.Collapsed
            ImgOptiFine.Visibility = Visibility.Collapsed
            LabOptiFine.Text = If(OptiFineError, "可以添加")
            LabOptiFine.Foreground = ColorGray4
        Else
            BtnOptiFineClear.Visibility = Visibility.Visible
            ImgOptiFine.Visibility = Visibility.Visible
            LabOptiFine.Text = SelectedOptiFine.DisplayName.Replace(VanillaName & " ", "")
            LabOptiFine.Foreground = ColorGray1
        End If
        'LiteLoader
        If VanillaDrop >= 130 Then
            CardLiteLoader.Visibility = Visibility.Collapsed
        Else
            CardLiteLoader.Visibility = Visibility.Visible
            Dim LiteLoaderError As String = LoadLiteLoaderGetError()
            CardLiteLoader.MainSwap.Visibility = If(LiteLoaderError Is Nothing, Visibility.Visible, Visibility.Collapsed)
            If LiteLoaderError IsNot Nothing Then CardLiteLoader.IsSwapped = True '例如在同时展开卡片时选择了不兼容项则强制折叠
            SetPanelVisibility(PanLiteLoaderInfo, CardLiteLoader.IsSwapped)
            If SelectedLiteLoader Is Nothing Then
                BtnLiteLoaderClear.Visibility = Visibility.Collapsed
                ImgLiteLoader.Visibility = Visibility.Collapsed
                LabLiteLoader.Text = If(LiteLoaderError, "可以添加")
                LabLiteLoader.Foreground = ColorGray4
            Else
                BtnLiteLoaderClear.Visibility = Visibility.Visible
                ImgLiteLoader.Visibility = Visibility.Visible
                LabLiteLoader.Text = SelectedLiteLoader.Inherit
                LabLiteLoader.Foreground = ColorGray1
            End If
        End If
        'Forge
        If Not McVersion.IsFormatFit(VanillaName) Then
            CardForge.Visibility = Visibility.Collapsed
        Else
            CardForge.Visibility = Visibility.Visible
            Dim ForgeError As String = LoadForgeGetError()
            CardForge.MainSwap.Visibility = If(ForgeError Is Nothing, Visibility.Visible, Visibility.Collapsed)
            If ForgeError IsNot Nothing Then CardForge.IsSwapped = True
            SetPanelVisibility(PanForgeInfo, CardForge.IsSwapped)
            If SelectedForge Is Nothing Then
                BtnForgeClear.Visibility = Visibility.Collapsed
                ImgForge.Visibility = Visibility.Collapsed
                LabForge.Text = If(ForgeError, "可以添加")
                LabForge.Foreground = ColorGray4
            Else
                BtnForgeClear.Visibility = Visibility.Visible
                ImgForge.Visibility = Visibility.Visible
                LabForge.Text = SelectedForge.VersionName
                LabForge.Foreground = ColorGray1
            End If
        End If
        'NeoForge
        If VanillaData("releaseTime").ToObject(Of Date) < New Date(2023, 6, 11) Then '匹配 1.20.1+ 与一些愚人节版本
            CardNeoForge.Visibility = Visibility.Collapsed
        Else
            CardNeoForge.Visibility = Visibility.Visible
            Dim NeoForgeError As String = LoadNeoForgeGetError()
            CardNeoForge.MainSwap.Visibility = If(NeoForgeError Is Nothing, Visibility.Visible, Visibility.Collapsed)
            If NeoForgeError IsNot Nothing Then CardNeoForge.IsSwapped = True
            SetPanelVisibility(PanNeoForgeInfo, CardNeoForge.IsSwapped)
            If SelectedNeoForge Is Nothing Then
                BtnNeoForgeClear.Visibility = Visibility.Collapsed
                ImgNeoForge.Visibility = Visibility.Collapsed
                LabNeoForge.Text = If(NeoForgeError, "可以添加")
                LabNeoForge.Foreground = ColorGray4
            Else
                BtnNeoForgeClear.Visibility = Visibility.Visible
                ImgNeoForge.Visibility = Visibility.Visible
                LabNeoForge.Text = SelectedNeoForge.VersionName
                LabNeoForge.Foreground = ColorGray1
            End If
        End If
        'Fabric
        If VanillaDrop <= 130 Then
            CardFabric.Visibility = Visibility.Collapsed
        Else
            CardFabric.Visibility = Visibility.Visible
            Dim FabricError As String = LoadFabricGetError()
            CardFabric.MainSwap.Visibility = If(FabricError Is Nothing, Visibility.Visible, Visibility.Collapsed)
            If FabricError IsNot Nothing Then CardFabric.IsSwapped = True
            SetPanelVisibility(PanFabricInfo, CardFabric.IsSwapped)
            If SelectedFabric Is Nothing Then
                BtnFabricClear.Visibility = Visibility.Collapsed
                ImgFabric.Visibility = Visibility.Collapsed
                LabFabric.Text = If(FabricError, "可以添加")
                LabFabric.Foreground = ColorGray4
            Else
                BtnFabricClear.Visibility = Visibility.Visible
                ImgFabric.Visibility = Visibility.Visible
                LabFabric.Text = SelectedFabric.Replace("+build", "")
                LabFabric.Foreground = ColorGray1
            End If
        End If
        'FabricApi
        If SelectedFabric Is Nothing Then
            CardFabricApi.Visibility = Visibility.Collapsed
        Else
            CardFabricApi.Visibility = Visibility.Visible
            Dim FabricApiError As String = LoadFabricApiGetError()
            CardFabricApi.MainSwap.Visibility = If(FabricApiError Is Nothing, Visibility.Visible, Visibility.Collapsed)
            If FabricApiError IsNot Nothing OrElse SelectedFabric Is Nothing Then CardFabricApi.IsSwapped = True
            SetPanelVisibility(PanFabricApiInfo, CardFabricApi.IsSwapped)
            If SelectedFabricApi Is Nothing Then
                BtnFabricApiClear.Visibility = Visibility.Collapsed
                ImgFabricApi.Visibility = Visibility.Collapsed
                LabFabricApi.Text = If(FabricApiError, "可以添加")
                LabFabricApi.Foreground = ColorGray4
            Else
                BtnFabricApiClear.Visibility = Visibility.Visible
                ImgFabricApi.Visibility = Visibility.Visible
                LabFabricApi.Text = SelectedFabricApi.DisplayName.Split("]")(1).Replace("Fabric API ", "").Replace(" build ", ".").Split("+").First.Trim
                LabFabricApi.Foreground = ColorGray1
            End If
        End If
        'OptiFabric
        If SelectedFabric Is Nothing OrElse SelectedOptiFine Is Nothing Then
            CardOptiFabric.Visibility = Visibility.Collapsed
        Else
            CardOptiFabric.Visibility = Visibility.Visible
            Dim OptiFabricError As String = LoadOptiFabricGetError()
            CardOptiFabric.MainSwap.Visibility = If(OptiFabricError Is Nothing, Visibility.Visible, Visibility.Collapsed)
            If OptiFabricError IsNot Nothing OrElse SelectedFabric Is Nothing Then CardOptiFabric.IsSwapped = True
            SetPanelVisibility(PanOptiFabricInfo, CardOptiFabric.IsSwapped)
            If SelectedOptiFabric Is Nothing Then
                BtnOptiFabricClear.Visibility = Visibility.Collapsed
                ImgOptiFabric.Visibility = Visibility.Collapsed
                LabOptiFabric.Text = If(OptiFabricError, "可以添加")
                LabOptiFabric.Foreground = ColorGray4
            Else
                BtnOptiFabricClear.Visibility = Visibility.Visible
                ImgOptiFabric.Visibility = Visibility.Visible
                LabOptiFabric.Text = SelectedOptiFabric.DisplayName.ToLower.Replace("optifabric-", "").Replace(".jar", "").Trim.TrimStart("v")
                LabOptiFabric.Foreground = ColorGray1
            End If
        End If
        '主警告
        If SelectedFabric IsNot Nothing AndAlso SelectedFabricApi Is Nothing Then
            HintFabricAPI.Visibility = Visibility.Visible
        Else
            HintFabricAPI.Visibility = Visibility.Collapsed
        End If
        If SelectedFabric IsNot Nothing AndAlso SelectedOptiFine IsNot Nothing AndAlso SelectedOptiFabric Is Nothing Then
            If VanillaDrop >= 140 AndAlso VanillaDrop <= 150 Then
                HintOptiFabric.Visibility = Visibility.Collapsed
                HintOptiFabricOld.Visibility = Visibility.Visible
            Else
                HintOptiFabric.Visibility = Visibility.Visible
                HintOptiFabricOld.Visibility = Visibility.Collapsed
            End If
        Else
            HintOptiFabric.Visibility = Visibility.Collapsed
            HintOptiFabricOld.Visibility = Visibility.Collapsed
        End If
        If VanillaDrop >= 160 AndAlso SelectedOptiFine IsNot Nothing AndAlso
           (SelectedForge IsNot Nothing OrElse SelectedFabric IsNot Nothing) Then
            HintModOptiFine.Visibility = Visibility.Visible
        Else
            HintModOptiFine.Visibility = Visibility.Collapsed
        End If
        '结束
        Ongoing = False
    End Sub
    ''' <summary>
    ''' 清空已选择的项目。
    ''' </summary>
    Private Sub ClearSelected()
        VanillaName = Nothing
        VanillaData = Nothing
        VanillaIcon = Nothing
        SelectedOptiFine = Nothing
        SelectedLiteLoader = Nothing
        SelectedForge = Nothing
        SelectedNeoForge = Nothing
        SelectedFabric = Nothing
        SelectedFabricApi = Nothing
        SelectedOptiFabric = Nothing
        IsSelectNameEdited = False
    End Sub
    '信息栏动画
    Private Sub SetPanelVisibility(Panel As Grid, IsVisible As Boolean)
        If Panel.Tag = IsVisible.ToString Then Return
        Panel.Tag = IsVisible.ToString
        If IsVisible Then
            AniStart({
                AaTranslateY(Panel, -CType(Panel.RenderTransform, TranslateTransform).Y, 150, Ease:=New AniEaseOutFluent),
                AaOpacity(Panel, 1 - Panel.Opacity, 60)
            }, "PageDownloadInstall Visibility " & Panel.Name)
        Else
            AniStart({
                AaTranslateY(Panel, 6 - CType(Panel.RenderTransform, TranslateTransform).Y, 60),
                AaOpacity(Panel, -Panel.Opacity, 60)
            }, "PageDownloadInstall Visibility " & Panel.Name)
        End If
    End Sub

    ''' <summary>
    ''' 获取版本图标。
    ''' </summary>
    Private Function GetSelectLogo() As String
        If SelectedFabric IsNot Nothing Then
            Return "pack://application:,,,/images/Blocks/Fabric.png"
        ElseIf SelectedForge IsNot Nothing Then
            Return "pack://application:,,,/images/Blocks/Anvil.png"
        ElseIf SelectedNeoForge IsNot Nothing Then
            Return "pack://application:,,,/images/Blocks/NeoForge.png"
        ElseIf SelectedLiteLoader IsNot Nothing Then
            Return "pack://application:,,,/images/Blocks/Egg.png"
        ElseIf SelectedOptiFine IsNot Nothing Then
            Return "pack://application:,,,/images/Blocks/GrassPath.png"
        Else
            Return VanillaIcon
        End If
    End Function

    '版本名处理
    ''' <summary>
    ''' 获取默认版本名。
    ''' </summary>
    Private Function GetSelectName() As String
        Dim Name As String = VanillaName
        If SelectedFabric IsNot Nothing Then Name += "-Fabric " & SelectedFabric.BeforeFirst("+")
        If SelectedForge IsNot Nothing Then Name += "-Forge_" & SelectedForge.VersionName
        If SelectedNeoForge IsNot Nothing Then Name += "-NeoForge_" & SelectedNeoForge.VersionName.BeforeFirst("+")
        If SelectedLiteLoader IsNot Nothing Then Name += "-LiteLoader"
        If SelectedOptiFine IsNot Nothing Then Name += "-OptiFine_" & SelectedOptiFine.DisplayName.Replace(VanillaName & " ", "").Replace(" ", "_")
        Return Name
    End Function
    Private IsSelectNameEdited As Boolean = False
    Private IsSelectNameChanging As Boolean = False
    Private Sub SelectNameUpdate()
        If IsSelectNameEdited OrElse IsSelectNameChanging Then Return
        IsSelectNameChanging = True
        TextSelectName.Text = GetSelectName()
        IsSelectNameChanging = False
    End Sub
    Private Sub TextSelectName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextSelectName.TextChanged
        If IsSelectNameChanging Then Return
        IsSelectNameEdited = True
        ReloadSelected()
    End Sub
    Private Sub TextSelectName_ValidateChanged(sender As Object, e As EventArgs) Handles TextSelectName.ValidateChanged
        BtnStart.IsEnabled = TextSelectName.IsValidated
    End Sub

#End Region

#Region "加载器"

    '结果数据化
    Private Sub LoadMinecraft_OnFinish()
        ExitSelectPage() '返回
        Try
            Dim Dict As New Dictionary(Of String, List(Of JObject)) From {
                {"正式版", New List(Of JObject)}, {"预览版", New List(Of JObject)}, {"远古版", New List(Of JObject)}, {"愚人节版", New List(Of JObject)}
            }
            Dim Versions As JArray = DlClientListLoader.Output.Value("versions")
            For Each Version As JObject In Versions
                '确定分类
                Dim Type As String = Version("type")
                Select Case Type
                    Case "release"
                        Type = "正式版"
                    Case "snapshot"
                        Type = "预览版"
                        'Mojang 误分类
                        If Version("id").ToString.StartsWith("1.") AndAlso
                            Not Version("id").ToString.ToLower.Contains("combat") AndAlso
                            Not Version("id").ToString.ToLower.Contains("rc") AndAlso
                            Not Version("id").ToString.ToLower.Contains("experimental") AndAlso
                            Not Version("id").ToString.ToLower.Contains("pre") Then
                            Type = "正式版"
                            Version("type") = "release"
                        End If
                        '愚人节版本
                        Select Case Version("id").ToString.ToLower
                            Case "20w14infinite", "20w14∞"
                                Type = "愚人节版"
                                Version("id") = "20w14∞"
                                Version("type") = "special"
                                Version.Add("lore", GetMcFoolName(Version("id")))
                            Case "3d shareware v1.34", "1.rv-pre1", "15w14a", "2.0", "22w13oneblockatatime", "23w13a_or_b", "24w14potato", "25w14craftmine"
                                Type = "愚人节版"
                                Version("type") = "special"
                                Version.Add("lore", GetMcFoolName(Version("id")))
                            Case Else '4/1 自动视作愚人节版
                                Dim ReleaseDate = Version("releaseTime").Value(Of Date).ToUniversalTime().AddHours(2)
                                If ReleaseDate.Month = 4 AndAlso ReleaseDate.Day = 1 Then
                                    Type = "愚人节版"
                                    Version("type") = "special"
                                End If
                        End Select
                    Case "special"
                        '已被处理的愚人节版
                        Type = "愚人节版"
                    Case Else
                        Type = "远古版"
                End Select
                '加入辞典
                Dict(Type).Add(Version)
            Next
            '排序
            For Each Pair In Dict.ToList
                Dict(Pair.Key) = Pair.Value.OrderByDescending(Function(j) j("releaseTime").Value(Of Date)).ToList
            Next
            '清空当前
            PanMinecraft.Children.Clear()
            '添加最新版本
            Dim CardInfo As New MyCard With {.Title = "最新版本", .Margin = New Thickness(0, 15, 0, 15), .SwapType = 2}
            Dim PinnedVersions As New List(Of JObject)
            Dim Release As JObject = Dict("正式版")(0).DeepClone()
            Release("lore") = "最新正式版，发布于 " & Release("releaseTime").Value(Of Date).ToString("yyyy'/'MM'/'dd HH':'mm")
            PinnedVersions.Add(Release)
            If Dict("正式版")(0)("releaseTime").Value(Of Date) < Dict("预览版")(0)("releaseTime").Value(Of Date) Then
                Dim Snapshot As JObject = Dict("预览版")(0).DeepClone()
                Snapshot("lore") = "最新预览版，发布于 " & Snapshot("releaseTime").Value(Of Date).ToString("yyyy'/'MM'/'dd HH':'mm")
                PinnedVersions.Add(Snapshot)
            End If
            Dim PanInfo As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = PinnedVersions}
            MyCard.StackInstall(PanInfo, 7)
            CardInfo.Children.Add(PanInfo)
            PanMinecraft.Children.Insert(0, CardInfo)
            '添加其他版本
            For Each Pair As KeyValuePair(Of String, List(Of JObject)) In Dict
                If Not Pair.Value.Any() Then Continue For
                '增加卡片
                Dim NewCard As New MyCard With {.Title = Pair.Key, .Margin = New Thickness(0, 0, 0, 15), .SwapType = 7}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = Pair.Value}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                NewCard.IsSwapped = True
                PanMinecraft.Children.Add(NewCard)
            Next
            '自动选择版本
            If VersionWaitingSelect Is Nothing Then Exit Try
            Log("[Download] 自动选择 MC 版本：" & VersionWaitingSelect)
            For Each Version As JObject In Versions
                If Version("id").ToString <> VersionWaitingSelect Then Continue For
                Dim Item = McDownloadListItem(Version, Sub()
                                                       End Sub, False).Init()
                MinecraftSelected(Item, Nothing)
            Next
        Catch ex As Exception
            Log(ex, "可视化安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub
    ''' <summary>
    ''' 当 MC 版本列表加载完时，立即自动选择的版本。用于外部调用。
    ''' </summary>
    Public Shared VersionWaitingSelect As String = Nothing

#End Region

#Region "OptiFine 列表"

    ''' <summary>
    ''' 获取 OptiFine 的加载异常信息。若正常则返回 Nothing。
    ''' </summary>
    Private Function LoadOptiFineGetError() As String
        If SelectedNeoForge IsNot Nothing Then Return "与 NeoForge 不兼容"
        '检查 Forge 1.13 - 1.14.3：全部不兼容
        If SelectedForge IsNot Nothing AndAlso
            CompareVersion(VanillaName, "1.13") >= 0 AndAlso CompareVersion("1.14.3", VanillaName) >= 0 Then
            Return "与 Forge 不兼容"
        End If
        '检查 Fabric 1.20.5+：全部不兼容
        If SelectedFabric IsNot Nothing AndAlso
            CompareVersion(VanillaName, "1.20.4") > 0 Then
            Return "与 Fabric 不兼容"
        End If
        '检查 Loader
        If GetLoaderError(LoadOptiFine) IsNot Nothing Then Return GetLoaderError(LoadOptiFine)
        '检查 Forge 版本
        Dim HasAny As Boolean = False
        Dim HasRequiredVersion As Boolean = False
        For Each OptiFineVersion As DlOptiFineListEntry In DlOptiFineListLoader.Output.Value
            If Not OptiFineVersion.DisplayName.StartsWith(VanillaName & " ") Then Continue For '不是同一个大版本
            HasAny = True
            If SelectedForge Is Nothing Then Return Nothing '未选择 Forge
            If IsOptiFineSuitForForge(OptiFineVersion, SelectedForge) Then Return Nothing '该版本可用
            If OptiFineVersion.RequiredForgeVersion IsNot Nothing Then HasRequiredVersion = True
        Next
        If Not HasAny Then
            Return "无"
        ElseIf HasRequiredVersion Then
            Return "仅兼容特定版本的 Forge"
        Else
            Return "与 Forge 不兼容"
        End If
    End Function

    '检查某个 OptiFine 是否与某个 Forge 兼容
    Private Function IsOptiFineSuitForForge(OptiFine As DlOptiFineListEntry, Forge As DlForgeVersionEntry)
        If Forge.Inherit <> OptiFine.Inherit Then Return False '不是同一个大版本
        If OptiFine.RequiredForgeVersion Is Nothing Then Return False '不兼容 Forge
        If String.IsNullOrWhiteSpace(OptiFine.RequiredForgeVersion) Then Return True '#4183
        If OptiFine.RequiredForgeVersion.Contains(".") Then 'XX.X.XXX
            Return CompareVersion(Forge.Version.ToString, OptiFine.RequiredForgeVersion) = 0
        Else 'XXXX
            Return Forge.Version.Revision = OptiFine.RequiredForgeVersion
        End If
    End Function

    '限制展开
    Private Sub CardOptiFine_PreviewSwap(sender As Object, e As RouteEventArgs) Handles CardOptiFine.PreviewSwap
        If LoadOptiFineGetError() IsNot Nothing Then e.Handled = True
    End Sub

    ''' <summary>
    ''' 尝试重新可视化 OptiFine 版本列表。
    ''' </summary>
    Private Sub OptiFine_Loaded() Handles LoadOptiFine.StateChanged
        Try
            If DlOptiFineListLoader.State <> LoadState.Finished Then Return

            '获取版本列表
            Dim Versions As New List(Of DlOptiFineListEntry)
            For Each Version As DlOptiFineListEntry In DlOptiFineListLoader.Output.Value
                If SelectedForge IsNot Nothing AndAlso Not IsOptiFineSuitForForge(Version, SelectedForge) Then Continue For
                If Version.DisplayName.StartsWith(VanillaName & " ") Then Versions.Add(Version)
            Next
            If Not Versions.Any() Then Return
            '排序
            Versions = Versions.SortByComparison(
            Function(Left As DlOptiFineListEntry, Right As DlOptiFineListEntry) As Boolean
                If Not Left.IsPreview AndAlso Right.IsPreview Then Return True
                If Left.IsPreview AndAlso Not Right.IsPreview Then Return False
                Return CompareVersionGE(Left.DisplayName, Right.DisplayName)
            End Function)
            '可视化
            PanOptiFine.Children.Clear()
            For Each Version In Versions
                PanOptiFine.Children.Add(OptiFineDownloadListItem(Version, AddressOf OptiFine_Selected, False))
            Next
        Catch ex As Exception
            Log(ex, "可视化 OptiFine 安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '选择与清除
    Private Sub OptiFine_Selected(sender As MyListItem, e As EventArgs)
        SelectedOptiFine = sender.Tag
        If SelectedForge IsNot Nothing AndAlso Not IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge) Then SelectedForge = Nothing
        OptiFabric_Loaded()
        Forge_Loaded()
        NeoForge_Loaded()
        CardOptiFine.IsSwapped = True
        ReloadSelected()
    End Sub
    Private Sub OptiFine_Clear(sender As Object, e As MouseButtonEventArgs) Handles BtnOptiFineClear.MouseLeftButtonUp
        SelectedOptiFine = Nothing
        SelectedOptiFabric = Nothing
        AutoSelectedOptiFabric = False
        CardOptiFine.IsSwapped = True
        e.Handled = True
        Forge_Loaded()
        NeoForge_Loaded()
        ReloadSelected()
    End Sub

#End Region

#Region "LiteLoader 列表"

    ''' <summary>
    ''' 获取 LiteLoader 的加载异常信息。若正常则返回 Nothing。
    ''' </summary>
    Private Function LoadLiteLoaderGetError() As String
        '检查 Loader
        If GetLoaderError(LoadLiteLoader) IsNot Nothing Then Return GetLoaderError(LoadLiteLoader)
        '检查版本
        Return If(DlLiteLoaderListLoader.Output.Value.Any(Function(v) v.Inherit = VanillaName), Nothing, "无")
    End Function

    '限制展开
    Private Sub CardLiteLoader_PreviewSwap(sender As Object, e As RouteEventArgs) Handles CardLiteLoader.PreviewSwap
        If LoadLiteLoaderGetError() IsNot Nothing Then e.Handled = True
    End Sub

    ''' <summary>
    ''' 尝试重新可视化 LiteLoader 版本列表。
    ''' </summary>
    Private Sub LiteLoader_Loaded() Handles LoadLiteLoader.StateChanged
        Try
            If DlLiteLoaderListLoader.State <> LoadState.Finished Then Return
            '获取版本列表
            Dim Versions As New List(Of DlLiteLoaderListEntry)
            For Each Version As DlLiteLoaderListEntry In DlLiteLoaderListLoader.Output.Value
                If Version.Inherit = VanillaName Then Versions.Add(Version)
            Next
            If Not Versions.Any() Then Return
            '可视化
            PanLiteLoader.Children.Clear()
            For Each Version In Versions
                PanLiteLoader.Children.Add(LiteLoaderDownloadListItem(Version, AddressOf LiteLoader_Selected, False))
            Next
        Catch ex As Exception
            Log(ex, "可视化 LiteLoader 安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '选择与清除
    Private Sub LiteLoader_Selected(sender As MyListItem, e As EventArgs)
        SelectedLiteLoader = sender.Tag
        CardLiteLoader.IsSwapped = True
        ReloadSelected()
    End Sub
    Private Sub LiteLoader_Clear(sender As Object, e As MouseButtonEventArgs) Handles BtnLiteLoaderClear.MouseLeftButtonUp
        SelectedLiteLoader = Nothing
        CardLiteLoader.IsSwapped = True
        e.Handled = True
        ReloadSelected()
    End Sub

#End Region

#Region "Forge 列表"

    ''' <summary>
    ''' 获取 Forge 的加载异常信息。若正常则返回 Nothing。
    ''' </summary>
    Private Function LoadForgeGetError() As String
        If CompareVersionGE("1.5.1", VanillaName) AndAlso CompareVersionGE(VanillaName, "1.1") Then Return "无"
        '检查 Loader
        If GetLoaderError(LoadForge) IsNot Nothing Then Return GetLoaderError(LoadForge)
        Dim Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)) = LoadForge.State
        If VanillaName <> Loader.Input Then Return "获取中……"
        '检查版本
        For Each Version In Loader.Output
            If Version.Category = "universal" OrElse Version.Category = "client" Then Continue For '跳过无法自动安装的版本
            If SelectedNeoForge IsNot Nothing Then Return "与 NeoForge 不兼容"
            If SelectedFabric IsNot Nothing Then Return "与 Fabric 不兼容"
            If SelectedOptiFine IsNot Nothing AndAlso
                CompareVersionGE(VanillaName, "1.13") AndAlso CompareVersionGE("1.14.3", VanillaName) Then
                Return "与 OptiFine 不兼容" '1.13 ~ 1.14.3 OptiFine 检查
            End If
            If SelectedOptiFine IsNot Nothing AndAlso Not IsOptiFineSuitForForge(SelectedOptiFine, Version) Then Continue For
            Return Nothing
        Next
        Return "与 OptiFine 不兼容"
    End Function

    '限制展开
    Private Sub CardForge_PreviewSwap(sender As Object, e As RouteEventArgs) Handles CardForge.PreviewSwap
        If LoadForgeGetError() IsNot Nothing Then e.Handled = True
    End Sub

    ''' <summary>
    ''' 尝试重新可视化 Forge 版本列表。
    ''' </summary>
    Private Sub Forge_Loaded() Handles LoadForge.StateChanged
        Try
            If Not LoadForge.State.IsLoader Then Return
            Dim Loader As LoaderTask(Of String, List(Of DlForgeVersionEntry)) = LoadForge.State
            If VanillaName <> Loader.Input Then Return
            If Loader.State <> LoadState.Finished Then Return
            '获取要显示的版本
            Dim Versions = Loader.Output.ToList '复制数组，以免 Output 在实例化后变空
            If Not Loader.Output.Any() Then Return
            PanForge.Children.Clear()
            Versions = Versions.Where(
            Function(v)
                If v.Category = "universal" OrElse v.Category = "client" Then Return False '跳过无法自动安装的版本
                If SelectedOptiFine IsNot Nothing AndAlso Not IsOptiFineSuitForForge(SelectedOptiFine, v) Then Return False
                Return True
            End Function).OrderByDescending(Function(v) v).ToList()
            ForgeDownloadListItemPreload(PanForge, Versions, AddressOf Forge_Selected, False)
            For Each Version In Versions
                PanForge.Children.Add(ForgeDownloadListItem(Version, AddressOf Forge_Selected, False))
            Next
        Catch ex As Exception
            Log(ex, "可视化 Forge 安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '选择与清除
    Private Sub Forge_Selected(sender As MyListItem, e As EventArgs)
        SelectedForge = sender.Tag
        CardForge.IsSwapped = True
        If SelectedOptiFine IsNot Nothing AndAlso Not IsOptiFineSuitForForge(SelectedOptiFine, SelectedForge) Then SelectedOptiFine = Nothing
        OptiFine_Loaded()
        ReloadSelected()
    End Sub
    Private Sub Forge_Clear(sender As Object, e As MouseButtonEventArgs) Handles BtnForgeClear.MouseLeftButtonUp
        SelectedForge = Nothing
        CardForge.IsSwapped = True
        e.Handled = True
        OptiFine_Loaded()
        ReloadSelected()
    End Sub

#End Region

#Region "NeoForge 列表"

    ''' <summary>
    ''' 获取 NeoForge 的加载异常信息。若正常则返回 Nothing。
    ''' </summary>
    Private Function LoadNeoForgeGetError() As String
        If SelectedOptiFine IsNot Nothing Then Return "与 OptiFine 不兼容"
        If SelectedForge IsNot Nothing Then Return "与 Forge 不兼容"
        If SelectedFabric IsNot Nothing Then Return "与 Fabric 不兼容"
        '检查 Loader
        If GetLoaderError(LoadNeoForge) IsNot Nothing Then Return GetLoaderError(LoadNeoForge)
        '检查版本
        Return If(DlNeoForgeListLoader.Output.Value.Any(Function(v) v.Inherit = VanillaName), Nothing, "无")
    End Function

    '限制展开
    Private Sub CardNeoForge_PreviewSwap(sender As Object, e As RouteEventArgs) Handles CardNeoForge.PreviewSwap
        If LoadNeoForgeGetError() IsNot Nothing Then e.Handled = True
    End Sub

    ''' <summary>
    ''' 尝试重新可视化 NeoForge 版本列表。
    ''' </summary>
    Private Sub NeoForge_Loaded() Handles LoadNeoForge.StateChanged
        Try
            '获取版本列表
            If DlNeoForgeListLoader.State <> LoadState.Finished Then Return
            Dim Versions = DlNeoForgeListLoader.Output.Value.Where(Function(v) v.Inherit = VanillaName).ToList
            If Not Versions.Any() Then Return
            '可视化
            PanNeoForge.Children.Clear()
            NeoForgeDownloadListItemPreload(PanNeoForge, Versions, AddressOf NeoForge_Selected, False)
            For Each Version In Versions
                PanNeoForge.Children.Add(NeoForgeDownloadListItem(Version, AddressOf NeoForge_Selected, False))
            Next
        Catch ex As Exception
            Log(ex, "可视化 NeoForge 安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '选择与清除
    Private Sub NeoForge_Selected(sender As MyListItem, e As EventArgs)
        SelectedNeoForge = sender.Tag
        CardNeoForge.IsSwapped = True
        OptiFine_Loaded()
        ReloadSelected()
    End Sub
    Private Sub NeoForge_Clear(sender As Object, e As MouseButtonEventArgs) Handles BtnNeoForgeClear.MouseLeftButtonUp
        SelectedNeoForge = Nothing
        CardNeoForge.IsSwapped = True
        e.Handled = True
        OptiFine_Loaded()
        ReloadSelected()
    End Sub

#End Region

#Region "Fabric 列表"

    ''' <summary>
    ''' 获取 Fabric 的加载异常信息。若正常则返回 Nothing。
    ''' </summary>
    Private Function LoadFabricGetError() As String
        '检查 OptiFine 1.20.5+：没有 OptiFabric 故全部不兼容
        If SelectedOptiFine IsNot Nothing AndAlso CompareVersionGE(VanillaName, "1.20.5") Then Return "与 OptiFine 不兼容"
        '检查 Loader
        If GetLoaderError(LoadFabric) IsNot Nothing Then Return GetLoaderError(LoadFabric)
        '检查版本
        For Each Version As JObject In DlFabricListLoader.Output.Value("game")
            If Version("version").ToString = VanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3") Then
                If SelectedForge IsNot Nothing Then Return "与 Forge 不兼容"
                If SelectedNeoForge IsNot Nothing Then Return "与 NeoForge 不兼容"
                Return Nothing
            End If
        Next
        Return "无"
    End Function

    '限制展开
    Private Sub CardFabric_PreviewSwap(sender As Object, e As RouteEventArgs) Handles CardFabric.PreviewSwap
        If LoadFabricGetError() IsNot Nothing Then e.Handled = True
    End Sub

    ''' <summary>
    ''' 尝试重新可视化 Fabric 版本列表。
    ''' </summary>
    Private Sub Fabric_Loaded() Handles LoadFabric.StateChanged
        Try
            If DlFabricListLoader.State <> LoadState.Finished Then Return
            '获取版本列表
            Dim Versions As JArray = DlFabricListLoader.Output.Value("loader")
            If Not Versions.Any() Then Return
            '可视化
            PanFabric.Children.Clear()
            PanFabric.Tag = Versions
            CardFabric.SwapControl = PanFabric
            CardFabric.SwapType = 12
        Catch ex As Exception
            Log(ex, "可视化 Fabric 安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '选择与清除
    Public Sub Fabric_Selected(sender As MyListItem, e As EventArgs)
        SelectedFabric = sender.Tag("version").ToString
        FabricApi_Loaded()
        OptiFabric_Loaded()
        CardFabric.IsSwapped = True
        ReloadSelected()
    End Sub
    Private Sub Fabric_Clear(sender As Object, e As MouseButtonEventArgs) Handles BtnFabricClear.MouseLeftButtonUp
        SelectedFabric = Nothing
        SelectedFabricApi = Nothing
        AutoSelectedFabricApi = False
        SelectedOptiFabric = Nothing
        AutoSelectedOptiFabric = False
        CardFabric.IsSwapped = True
        e.Handled = True
        ReloadSelected()
    End Sub

#End Region

#Region "Fabric API 列表"

    ''' <summary>
    ''' 判断某 Fabric API 是否适配当前选择的原版版本。
    ''' </summary>
    Public Function IsFabricApiCompatible(FabricApi As CompFile) As Boolean
        Dim FabricApiName = FabricApi.DisplayName
        Try
            If FabricApiName Is Nothing OrElse VanillaName Is Nothing Then Return False
            FabricApiName = FabricApiName.ToLower : VanillaName = VanillaName.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3").ToLower
            If FabricApiName.StartsWith("[" & VanillaName & "]") Then Return True
            If Not FabricApiName.Contains("/") OrElse Not FabricApiName.Contains("]") Then Return False
            '直接的判断（例如 1.18.1/22w03a）
            For Each Part As String In FabricApiName.BeforeFirst("]").TrimStart("[").Split("/")
                If Part = VanillaName Then Return True
            Next
            '将版本名分割语素（例如 1.16.4/5）
            Dim Lefts = RegexSearch(FabricApiName.BeforeFirst("]"), "[a-z/]+|[0-9/]+")
            Dim Rights = RegexSearch(VanillaName.BeforeFirst("]"), "[a-z/]+|[0-9/]+")
            '对每段进行判断
            Dim i As Integer = 0
            While True
                '两边均缺失，感觉是一个东西
                If Lefts.Count - 1 < i AndAlso Rights.Count - 1 < i Then Return True
                '确定两边是否一致
                Dim LeftValue As String = If(Lefts.Count - 1 < i, "-1", Lefts(i))
                Dim RightValue As String = If(Rights.Count - 1 < i, "-1", Rights(i))
                If Not LeftValue.Contains("/") Then
                    If LeftValue <> RightValue Then Return False
                Else
                    '左边存在斜杠
                    If Not LeftValue.Contains(RightValue) Then Return False
                End If
                i += 1
            End While
            Return True
        Catch ex As Exception
            Log(ex, "判断 Fabric API 版本适配性出错（" & FabricApiName & ", " & VanillaName & "）")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 获取 FabricApi 的加载异常信息。若正常则返回 Nothing。
    ''' </summary>
    Private Function LoadFabricApiGetError() As String
        '检查 Loader
        If GetLoaderError(LoadFabricApi) IsNot Nothing Then Return GetLoaderError(LoadFabricApi)
        If DlFabricApiLoader.Output Is Nothing Then Return If(SelectedFabric Is Nothing, "需要安装 Fabric", "获取中……")
        '检查版本
        If DlFabricApiLoader.Output.Any(Function(f) IsFabricApiCompatible(f)) Then
            Return If(SelectedFabric Is Nothing, "需要安装 Fabric", Nothing)
        Else
            Return "无"
        End If
    End Function

    '限制展开
    Private Sub CardFabricApi_PreviewSwap(sender As Object, e As RouteEventArgs) Handles CardFabricApi.PreviewSwap
        If LoadFabricApiGetError() IsNot Nothing Then e.Handled = True
    End Sub

    Private AutoSelectedFabricApi As Boolean = False
    ''' <summary>
    ''' 尝试重新可视化 FabricApi 版本列表。
    ''' </summary>
    Private Sub FabricApi_Loaded() Handles LoadFabricApi.StateChanged
        Try
            If DlFabricApiLoader.State <> LoadState.Finished Then Return
            If VanillaName Is Nothing OrElse SelectedFabric Is Nothing Then Return
            '获取版本列表
            Dim Versions As New List(Of CompFile)
            For Each Version In DlFabricApiLoader.Output
                If IsFabricApiCompatible(Version) Then
                    If Not Version.DisplayName.StartsWith("[") Then
                        Log("[Download] 已特判修改 Fabric API 显示名：" & Version.DisplayName, LogLevel.Debug)
                        Version.DisplayName = "[" & VanillaName & "] " & Version.DisplayName
                    End If
                    Versions.Add(Version)
                End If
            Next
            If Not Versions.Any() Then Return
            Versions = Versions.OrderByDescending(Function(v) v.ReleaseDate).ToList
            '可视化
            PanFabricApi.Children.Clear()
            For Each Version In Versions
                If Not IsFabricApiCompatible(Version) Then Continue For
                PanFabricApi.Children.Add(FabricApiDownloadListItem(Version, AddressOf FabricApi_Selected))
            Next
            '自动选择 Fabric API
            If Not AutoSelectedFabricApi Then
                AutoSelectedFabricApi = True
                Dim Item As MyListItem = MyVirtualizingElement.TryInit(PanFabricApi.Children(0))
                Log($"[Download] 已自动选择 Fabric API：{Item.Title}")
                FabricApi_Selected(Item, Nothing)
            End If
        Catch ex As Exception
            Log(ex, "可视化 Fabric API 安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '选择与清除
    Private Sub FabricApi_Selected(sender As MyListItem, e As EventArgs)
        SelectedFabricApi = sender.Tag
        CardFabricApi.IsSwapped = True
        ReloadSelected()
    End Sub
    Private Sub FabricApi_Clear(sender As Object, e As MouseButtonEventArgs) Handles BtnFabricApiClear.MouseLeftButtonUp
        SelectedFabricApi = Nothing
        CardFabricApi.IsSwapped = True
        e.Handled = True
        ReloadSelected()
    End Sub

#End Region

#Region "OptiFabric 列表"

    ''' <summary>
    ''' 判断某 OptiFabric 是否适配当前选择的原版版本。
    ''' </summary>
    Private Function IsOptiFabricCompatible(ModFile As CompFile) As Boolean
        Try
            If VanillaName Is Nothing Then Return False
            Return ModFile.GameVersions.Contains(VanillaName)
        Catch ex As Exception
            Log(ex, "判断 OptiFabric 版本适配性出错（" & VanillaName & "）")
            Return False
        End Try
    End Function

    Private AutoSelectedOptiFabric As Boolean = False
    ''' <summary>
    ''' 获取 OptiFabric 的加载异常信息。若正常则返回 Nothing。
    ''' </summary>
    Private Function LoadOptiFabricGetError() As String
        If VanillaDrop >= 140 AndAlso VanillaDrop <= 150 Then Return "不兼容老版本 Fabric，请手动下载 OptiFabric Origins"
        '检查 Loader
        If GetLoaderError(LoadOptiFabric) IsNot Nothing Then Return GetLoaderError(LoadOptiFabric)
        '检查版本
        If DlOptiFabricLoader.Output Is Nothing Then
            If SelectedFabric Is Nothing AndAlso SelectedOptiFine Is Nothing Then Return "需要安装 OptiFine 与 Fabric"
            If SelectedFabric Is Nothing Then Return "需要安装 Fabric"
            If SelectedOptiFine Is Nothing Then Return "需要安装 OptiFine"
            Return "获取中……"
        End If
        For Each Version In DlOptiFabricLoader.Output
            If Not IsOptiFabricCompatible(Version) Then Continue For '2135#
            If SelectedFabric Is Nothing AndAlso SelectedOptiFine Is Nothing Then Return "需要安装 OptiFine 与 Fabric"
            If SelectedFabric Is Nothing Then Return "需要安装 Fabric"
            If SelectedOptiFine Is Nothing Then Return "需要安装 OptiFine"
            Return Nothing '通过检查
        Next
        Return "无"
    End Function

    '限制展开
    Private Sub CardOptiFabric_PreviewSwap(sender As Object, e As RouteEventArgs) Handles CardOptiFabric.PreviewSwap
        If LoadOptiFabricGetError() IsNot Nothing Then e.Handled = True
    End Sub

    ''' <summary>
    ''' 尝试重新可视化 OptiFabric 版本列表。
    ''' </summary>
    Private Sub OptiFabric_Loaded() Handles LoadOptiFabric.StateChanged
        Try
            If DlOptiFabricLoader.State <> LoadState.Finished Then Return
            If VanillaName Is Nothing OrElse SelectedFabric Is Nothing OrElse SelectedOptiFine Is Nothing Then Return
            '获取版本列表
            Dim Versions As New List(Of CompFile)
            For Each Version In DlOptiFabricLoader.Output
                If IsOptiFabricCompatible(Version) Then Versions.Add(Version)
            Next
            If Not Versions.Any() Then Return
            '排序
            Versions = Versions.OrderByDescending(Function(v) v.ReleaseDate).ToList
            '可视化
            PanOptiFabric.Children.Clear()
            For Each Version In Versions
                If Not IsOptiFabricCompatible(Version) Then Continue For
                PanOptiFabric.Children.Add(OptiFabricDownloadListItem(Version, AddressOf OptiFabric_Selected))
            Next
            '自动选择 OptiFabric
            If AutoSelectedOptiFabric OrElse VanillaDrop >= 140 AndAlso VanillaDrop <= 150 Then Return '1.14~15 不自动选择
            AutoSelectedOptiFabric = True
            Dim Item As MyListItem = MyVirtualizingElement.TryInit(PanOptiFabric.Children(0))
            Log($"[Download] 已自动选择 OptiFabric：{Item.Title}")
            OptiFabric_Selected(Item, Nothing)
        Catch ex As Exception
            Log(ex, "可视化 OptiFabric 安装版本列表出错", LogLevel.Feedback)
        End Try
    End Sub

    '选择与清除
    Private Sub OptiFabric_Selected(sender As MyListItem, e As EventArgs)
        SelectedOptiFabric = sender.Tag
        CardOptiFabric.IsSwapped = True
        ReloadSelected()
    End Sub
    Private Sub OptiFabric_Clear(sender As Object, e As MouseButtonEventArgs) Handles BtnOptiFabricClear.MouseLeftButtonUp
        SelectedOptiFabric = Nothing
        CardOptiFabric.IsSwapped = True
        e.Handled = True
        ReloadSelected()
    End Sub

#End Region

#Region "安装"

    Private Sub TextSelectName_KeyDown(sender As Object, e As KeyEventArgs) Handles TextSelectName.KeyDown
        If e.Key = Key.Enter AndAlso BtnStart.IsEnabled Then BtnStart_Click()
    End Sub
    Private Sub BtnStart_Click() Handles BtnStart.Click
        '确认版本隔离
        If (SelectedForge IsNot Nothing OrElse SelectedNeoForge IsNot Nothing OrElse SelectedFabric IsNot Nothing) AndAlso
           (Setup.Get("LaunchArgumentIndieV2") = 0 OrElse Setup.Get("LaunchArgumentIndieV2") = 2) Then
            If MyMsgBox("你尚未开启版本隔离，多个 MC 版本会共用同一个 Mod 文件夹。" & vbCrLf &
                        "因此，游戏可能会因为读取到与当前版本不符的 Mod 而崩溃。" & vbCrLf &
                        "推荐先在 设置 → 启动选项 → 默认版本隔离 中开启版本隔离！", "版本隔离提示", "取消下载", "继续") = 1 Then
                Return
            End If
        End If
        '提交安装申请
        Dim InstanceName As String = TextSelectName.Text
        Dim Request As New McInstallRequest With {
            .NewInstanceName = InstanceName,
            .VersionFolder = $"{McFolderSelected}versions\{InstanceName}\",
            .MinecraftJson = VanillaData("url").ToString,
            .MinecraftName = VanillaName,
            .OptiFineEntry = SelectedOptiFine,
            .ForgeEntry = SelectedForge,
            .NeoForgeEntry = SelectedNeoForge,
            .FabricVersion = SelectedFabric,
            .FabricApi = SelectedFabricApi,
            .OptiFabric = SelectedOptiFabric,
            .LiteLoaderEntry = SelectedLiteLoader
        }
        If Not McInstall(Request) Then Return
        '返回，这样在再次进入安装页面时这个版本就会显示文件夹已重复
        ExitSelectPage()
    End Sub

#End Region

    Private Function GetLoaderError(Loader As MyLoading) As String
        If Loader Is Nothing Then Return "获取中……"
        If Not Loader.State.IsLoader Then Return "获取中……"
        Select Case Loader.State.LoadingState
            Case MyLoading.MyLoadingState.Run
                Return "获取中……"
            Case MyLoading.MyLoadingState.Error
                Dim Message As String = CType(Loader.State, LoaderBase).Error.Message
                Return If(Message = "无", "无", "获取失败：" & Message)
            Case MyLoading.MyLoadingState.Unloaded
                Return "未知错误，状态为 Unloaded"
            Case Else
                Return Nothing
        End Select
    End Function

End Class
