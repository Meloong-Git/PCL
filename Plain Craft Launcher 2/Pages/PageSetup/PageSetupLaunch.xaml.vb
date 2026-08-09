Public Class PageSetupLaunch

    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
        RefreshRam(False)
        BtnSwitch.Visibility = If(McInstanceSelected Is Nothing, Visibility.Collapsed, Visibility.Visible)

        'Java 可能在版本设置被修改，所以总是重新加载（反正其他的 Refresh 也不太吃性能）
        AniControlEnabled += 1
        Refresh()
        AniControlEnabled -= 1

        '非重复加载部分
        Static Reloaded As Boolean = False
        If Reloaded Then Return
        Reloaded = True

        '内存自动刷新
        Dim timer As New Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 1)}
        AddHandler timer.Tick, AddressOf RefreshRam
        timer.Start()
    End Sub
    Public Sub Refresh()
        Try
            SettingService.RefreshSettings(Me)
            UpdateSkinType()
            UpdateRamType()
            UpdateJavaList()
        Catch ex As NullReferenceException
            Logger.Error(ex, "启动设置项存在异常，已被自动重置", LogBehavior.Alert)
            Reset()
        Catch ex As Exception
            Logger.Error(ex, "重载启动设置时出错")
        End Try
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Settings.Set("LaunchArgumentIndieV2", Settings.GetDefault("LaunchArgumentIndieV2"))
            Configs.JavaList.Reset()
            Configs.JavaRemovedList.Reset()
            JavaListRefreshWorker.Start()
            Hint("已初始化启动设置！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动设置失败", LogBehavior.Alert)
        End Try
        Refresh()
    End Sub

#Region "离线皮肤"

    Private Sub BtnSkinChange_Click(sender As Object, e As EventArgs) Handles BtnSkinChange.Click
        Dim SkinInfo As McSkinInfo = McSkinSelect()
        If Not SkinInfo.IsVaild Then Return
        ChangeSkin(SkinInfo)
    End Sub
    Private Sub RadioSkinType3_Check(sender As Object, e As RouteEventArgs) Handles RadioSkinType4.PreviewCheck
        If Not (AniControlEnabled = 0 AndAlso e.RaiseByMouse) Then Return
        '已有图片则不再选择
        If FileUtils.Exists(Paths.AppDataThenName & "CustomSkin.png") Then Return
        '没有图片则要求选择
        Dim SkinInfo As McSkinInfo = McSkinSelect()
        If Not SkinInfo.IsVaild Then
            e.Handled = True
            Return
        End If
        '正式改变
        If Not ChangeSkin(SkinInfo) Then e.Handled = True
    End Sub
    '返回是否成功改变
    Private Function ChangeSkin(SkinInfo As McSkinInfo) As Boolean
        Try
            '复制文件
            FileUtils.Copy(SkinInfo.LocalFile, Paths.AppDataThenName & "CustomSkin.png")
            '将单层皮肤扩展到双层
            Dim Bitmap As New MyBitmap(Paths.AppDataThenName & "CustomSkin.png")
            If Bitmap.Pic.Width = 64 AndAlso Bitmap.Pic.Height = 32 Then
                Dim Img As System.Drawing.Image = Bitmap
                Dim NewBitmap As New System.Drawing.Bitmap(64, 64)
                Using g As System.Drawing.Graphics = System.Drawing.Graphics.FromImage(NewBitmap)
                    g.DrawImageUnscaled(Img, New System.Drawing.Point(0, 0))
                End Using
                FileUtils.Delete(Paths.AppDataThenName & "CustomSkin.png")
                NewBitmap.Save(Paths.AppDataThenName & "CustomSkin.png")
            End If
            '更新设置
            Settings.Set("LaunchSkinSlim", SkinInfo.IsSlim)
            Return True
        Catch ex As Exception
            Logger.Error(ex, "改变离线皮肤失败", LogBehavior.Alert)
            Return False
        Finally
            '设置当前显示
            PageLaunchLeft.SkinLegacy.Start(IsForceRestart:=True)
        End Try
    End Function
    Private Sub BtnSkinDelete_Click(sender As Object, e As EventArgs) Handles BtnSkinDelete.Click
        Try
            FileUtils.Delete(Paths.AppDataThenName & "CustomSkin.png")
            RadioSkinType0.SetChecked(True, True)
            Hint("离线皮肤已清空！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "清空离线皮肤失败", LogBehavior.Alert)
        End Try
    End Sub
    Private Sub BtnSkinSave_Click(sender As Object, e As EventArgs) Handles BtnSkinSave.Click
        MySkin.Save(PageLaunchLeft.SkinLegacy)
    End Sub
    Private Sub BtnSkinCache_Click(sender As Object, e As EventArgs) Handles BtnSkinCache.Click
        MySkin.RefreshCache(Nothing)
    End Sub

    Public Shared Sub UpdateSkinType()
        PageLaunchLeft.SkinLegacy.Start()
        '设置 UI 改变
        If FrmSetupLaunch Is Nothing Then Return
        Select Case Settings.Get(Of Integer)("LaunchSkinType")
            Case 0, 1, 2 '默认
                FrmSetupLaunch.PanSkinID.Visibility = Visibility.Collapsed
                FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Collapsed
            Case 3 '正版
                FrmSetupLaunch.PanSkinID.Visibility = Visibility.Visible
                FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Collapsed
            Case 4 '自定义
                FrmSetupLaunch.PanSkinID.Visibility = Visibility.Collapsed
                FrmSetupLaunch.PanSkinChange.Visibility = Visibility.Visible
        End Select
        FrmSetupLaunch.CardSkin.TriggerForceResize()
    End Sub

#End Region

#Region "游戏内存"

    Public Shared Sub UpdateRamType()
        If FrmSetupLaunch?.SliderRamCustom Is Nothing Then Return
        FrmSetupLaunch.SliderRamCustom.IsEnabled = Settings.Get(Of Integer)("LaunchRamType") = 1
    End Sub

    ''' <summary>
    ''' 刷新 UI 上的 RAM 显示。
    ''' </summary>
    Public Sub RefreshRam(ShowAnim As Boolean)
        If LabRamGame Is Nothing OrElse LabRamUsed Is Nothing OrElse FrmMain.PageCurrent <> FormMain.PageType.Setup OrElse FrmSetupLeft.PageID <> FormMain.PageSubType.SetupLaunch Then Return
        '获取内存情况
        Dim RamGame As Double = Math.Round(GetRam(McInstanceSelected, False), 5)
        Dim RamTotal As Double = Math.Round(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024 / 1024, 1)
        Dim RamAvailable As Double = Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024, 1)
        Dim RamGameActual As Double = Math.Round(Math.Min(RamGame, RamAvailable), 5)
        Dim RamUsed As Double = Math.Round(RamTotal - RamAvailable, 5)
        Dim RamEmpty As Double = Math.Round((RamTotal - RamUsed - RamGame).Clamp(0, 1000), 1)
        '设置最大可用内存
        If RamTotal <= 1.5 Then
            SliderRamCustom.MaxValue = Math.Max(Math.Floor((RamTotal - 0.3) / 0.1), 1)
        ElseIf RamTotal <= 8 Then
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 1.5) / 0.5) + 12
        ElseIf RamTotal <= 16 Then
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 8) / 1) + 25
        Else
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 16) / 2) + 33
        End If
        '设置文本
        LabRamGame.Text = If(RamGame = Math.Floor(RamGame), RamGame & ".0", RamGame) & " GB" &
                          If(RamGame <> RamGameActual, " (可用 " & If(RamGameActual = Math.Floor(RamGameActual), RamGameActual & ".0", RamGameActual) & " GB)", "")
        LabRamUsed.Text = If(RamUsed = Math.Floor(RamUsed), RamUsed & ".0", RamUsed) & " GB"
        LabRamTotal.Text = " / " & If(RamTotal = Math.Floor(RamTotal), RamTotal & ".0", RamTotal) & " GB"
        If ShowAnim Then
            '宽度动画
            AniStart({
                AaGridLengthWidth(ColumnRamUsed, RamUsed - ColumnRamUsed.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamGame, RamGameActual - ColumnRamGame.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamEmpty, RamEmpty - ColumnRamEmpty.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong))
            }, "SetupLaunch Ram Grid")
        Else
            '宽度设置
            ColumnRamUsed.Width = New GridLength(RamUsed, GridUnitType.Star)
            ColumnRamGame.Width = New GridLength(RamGameActual, GridUnitType.Star)
            ColumnRamEmpty.Width = New GridLength(RamEmpty, GridUnitType.Star)
        End If
    End Sub
    Private Sub RefreshRam() Handles SliderRamCustom.Change, RadioRamType0.Check, RadioRamType1.Check
        RefreshRam(True)
    End Sub

    Private RamTextLeft As Integer = 2, RamTextRight As Integer = 1
    ''' <summary>
    ''' 刷新 UI 上的文本位置。
    ''' </summary>
    Private Sub RefreshRamText() Handles RectRamGame.SizeChanged, RectRamEmpty.SizeChanged, LabRamGame.SizeChanged
        '获取宽度信息
        Dim RectUsedWidth = RectRamUsed.ActualWidth
        Dim TotalWidth = PanRamDisplay.ActualWidth
        Dim LabGameWidth = LabRamGame.ActualWidth, LabUsedWidth = LabRamUsed.ActualWidth, LabTotalWidth = LabRamTotal.ActualWidth
        Dim LabGameTitleWidth = LabRamGameTitle.ActualWidth, LabUsedTitleWidth = LabRamUsedTitle.ActualWidth
        '左侧
        Dim Left As Integer
        If RectUsedWidth - 30 < LabUsedWidth OrElse RectUsedWidth - 30 < LabUsedTitleWidth Then
            '全写不下了
            Left = 0
        ElseIf RectUsedWidth - 25 < (LabUsedWidth + LabTotalWidth) Then
            '显示不下完整数据
            Left = 1
        Else
            '正常
            Left = 2
        End If
        If RamTextLeft <> Left Then
            RamTextLeft = Left
            Select Case Left
                Case 0
                    AniStart({
                            AaOpacity(LabRamUsed, -LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, -LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft")
                Case 1
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft")
                Case 2
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, 1 - LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "SetupLaunch Ram TextLeft")
            End Select
        End If
        '右侧
        Dim Right As Integer
        If TotalWidth < LabGameWidth + 2 + RectUsedWidth OrElse TotalWidth < LabGameTitleWidth + 2 + RectUsedWidth Then
            '挤到最右边
            Right = 0
        Else
            '正常情况
            Right = 1
        End If
        If Right = 0 Then
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("SetupLaunch Ram TextRight")) Then
                '需要动画
                AniStart({
                        AaX(LabRamGame, TotalWidth - LabGameWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, TotalWidth - LabGameTitleWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "SetupLaunch Ram TextRight")
            Else
                '不需要动画
                AniStop("SetupLaunch Ram TextRight")
                LabRamGame.Margin = New Thickness(TotalWidth - LabGameWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(TotalWidth - LabGameTitleWidth, 0, 0, 5)
            End If
        Else
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("SetupLaunch Ram TextRight")) Then
                '需要动画
                AniStart({
                        AaX(LabRamGame, 2 + RectUsedWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, 2 + RectUsedWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "SetupLaunch Ram TextRight")
            Else
                '不需要动画
                AniStop("SetupLaunch Ram TextRight")
                LabRamGame.Margin = New Thickness(2 + RectUsedWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(2 + RectUsedWidth, 0, 0, 5)
            End If
        End If
        RamTextRight = Right
    End Sub

    ''' <summary>
    ''' 获取当前设置的 RAM 值。单位为 GB。
    ''' </summary>
    Public Shared Function GetRam(Instance As McInstance, UseVersionJavaSetup As Boolean) As Double

        '------------------------------------------
        ' 修改下方代码时需要一并修改 PageInstanceSetup
        '------------------------------------------

        Dim RamGive As Double
        If Settings.Get(Of Integer)("LaunchRamType") = 0 Then
            '自动配置
            Dim RamAvailable As Double = Math.Round(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024 / 1024 * 10) / 10
            '确定需求的内存值
            Dim RamMininum As Double '无论如何也需要保证的最低限度内存
            Dim RamTarget1 As Double '估计能勉强带动了的内存
            Dim RamTarget2 As Double '估计没啥问题了的内存
            Dim RamTarget3 As Double '放一百万个材质和 Mod 和光影需要的内存
            If Instance IsNot Nothing AndAlso Not Instance.IsLoaded Then Instance.Load()
            If Instance IsNot Nothing AndAlso Instance.Modable Then
                '可安装 Mod 的版本
                Dim ModDir = DirectoryUtils.GetInfo(Instance.PathIndie & "mods\")
                Dim ModCount As Integer = If(ModDir.Exists, ModDir.GetFiles.Count(Function(f) {".jar", ".zip", ".litemod"}.Contains(f.Extension.Lower)), 0)
                RamMininum = 0.5 + ModCount / 150
                RamTarget1 = 1.5 + ModCount / 90
                RamTarget2 = 2.7 + ModCount / 50
                RamTarget3 = 4.5 + ModCount / 25
            ElseIf Instance IsNot Nothing AndAlso Instance.Version.HasOptiFine Then
                'OptiFine 版本
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 3
                RamTarget3 = 5
            Else
                '普通版本
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 2.5
                RamTarget3 = 4
            End If
            Dim RamDelta As Double
            '预分配内存，阶段一，0 ~ T1，100%
            RamDelta = RamTarget1
            RamGive += Math.Min(RamAvailable, RamDelta)
            RamAvailable -= RamDelta
            If RamAvailable < 0.1 Then GoTo PreFin
            '预分配内存，阶段二，T1 ~ T2，70%
            RamDelta = RamTarget2 - RamTarget1
            RamGive += Math.Min(RamAvailable * 0.7, RamDelta)
            RamAvailable -= RamDelta / 0.7
            If RamAvailable < 0.1 Then GoTo PreFin
            '预分配内存，阶段三，T2 ~ T3，40%
            RamDelta = RamTarget3 - RamTarget2
            RamGive += Math.Min(RamAvailable * 0.4, RamDelta)
            RamAvailable -= RamDelta / 0.4
            If RamAvailable < 0.1 Then GoTo PreFin
            '预分配内存，阶段四，T3 ~ T3 * 2，15%
            RamDelta = RamTarget3
            RamGive += Math.Min(RamAvailable * 0.15, RamDelta)
            RamAvailable -= RamDelta / 0.15
            If RamAvailable < 0.1 Then GoTo PreFin
PreFin:
            '不低于最低值
            RamGive = Math.Round(Math.Max(RamGive, RamMininum), 1)
        Else
            '手动配置
            Dim Value = Settings.Get(Of Integer)("LaunchRamCustom")
            If Value <= 12 Then
                RamGive = Value * 0.1 + 0.3
            ElseIf Value <= 25 Then
                RamGive = (Value - 12) * 0.5 + 1.5
            ElseIf Value <= 33 Then
                RamGive = (Value - 25) * 1 + 8
            Else
                RamGive = (Value - 33) * 2 + 16
            End If
        End If
        Return RamGive
    End Function

#End Region

#Region "Java 选择"
    '注意：修改此处代码时需要同时修改 PageInstanceSetup.xaml.vb

    '刷新 Java 下拉框显示
    Public Sub UpdateJavaList()
        If ComboAdvanceJava Is Nothing OrElse LabAdvanceJava Is Nothing OrElse Not ComboAdvanceJava.IsLoaded Then Return
        ComboAdvanceJava.Items.Clear()
        '还需要等待搜索结束
        If JavaListRefreshWorker.Running Then
            BtnAdvanceJavaSearch.IsEnabled = False
            ComboAdvanceJava.IsEnabled = False
            LabAdvanceJava.Text = "搜索中 …"
            Return
        End If
        '========================================== 显示结果 ==========================================
        BtnAdvanceJavaSearch.IsEnabled = True
        ComboAdvanceJava.IsEnabled = True
        '更新下拉框文本
        Dim Count = Configs.JavaList.Get().Count
        LabAdvanceJava.Text = If(Count > 0, $"共有 {Count} 个 Java …", "未找到 Java，点击以导入已有的 Java")
        '更新列表
        Try
            Dim JavaEntries = Configs.JavaList.Get()
            For i = 0 To JavaEntries.Count - 1
                Dim JavaEntry = JavaEntries(i)
                Dim JavaItem As New MyListItem With {
                    .FontSize = 13, .Height = 24, .IsScaleAnimationEnabled = False, .Type = MyListItem.CheckType.Clickable,
                    .Tag = JavaEntry, .Title = JavaEntry.ToString}
                AddHandler JavaItem.MouseLeftButtonUp, Sub(sender As Object, e As MouseButtonEventArgs) e.Handled = True
                AddHandler JavaItem.Click, Sub() Hint("点击选项右侧的箭头可以进行排序，以控制 PCL 优先选择哪个 Java！")
                ComboAdvanceJava.Items.Add(JavaItem)
                Dim Buttons As New List(Of MyIconButton)
                '向上移动按钮
                Dim UpButton As New MyIconButton With {.Logo = Logo.IconButtonArrowUp, .LogoScale = 0.95, .Height = 24, .Width = 24}
                UpButton.IsEnabled = i > 0
                UpButton.ToolTip = "提高优先级"
                ToolTipService.SetShowOnDisabled(UpButton, True)
                AddHandler UpButton.Click, Sub() MoveJavaInList(JavaEntry, -1)
                Buttons.Add(UpButton)
                '向下移动按钮
                Dim DownButton As New MyIconButton With {.Logo = Logo.IconButtonArrowDown, .LogoScale = 0.95, .Height = 24, .Width = 24}
                DownButton.IsEnabled = i < JavaEntries.Count - 1
                DownButton.ToolTip = "降低优先级"
                ToolTipService.SetShowOnDisabled(DownButton, True)
                AddHandler DownButton.Click, Sub() MoveJavaInList(JavaEntry, 1)
                Buttons.Add(DownButton)
                '从列表中移除按钮
                Dim IsOfficial As Boolean = JavaEntry.Folder.StartsWithF($"{Paths.AppData}.minecraft\runtime\")
                Dim DeleteButton As New MyIconButton With {.Logo = Logo.IconButtonStop, .Height = 24, .Width = 24}
                DeleteButton.IsEnabled = Not IsOfficial
                DeleteButton.ToolTip = If(DeleteButton.IsEnabled, "从列表中移除", "无法移除官方 Java")
                AddHandler DeleteButton.Click, Sub() ManuallyRemoveJava(JavaEntry)
                ToolTipService.SetShowOnDisabled(DeleteButton, True)
                Buttons.Add(DeleteButton)
                '打开文件夹按钮
                Dim OpenButton As New MyIconButton With {.Logo = Logo.IconButtonOpen, .LogoScale = 1.1, .Height = 24, .Width = 24}
                OpenButton.ToolTip = "打开文件夹"
                AddHandler OpenButton.Click, Sub() OpenExplorer(JavaEntry.JavaExePath)
                Buttons.Add(OpenButton)
                JavaItem.Buttons = Buttons
            Next
            Dim ImportItem As New MyListItem With {
                .FontSize = 13, .Height = 24, .IsScaleAnimationEnabled = False, .Title = "导入电脑中已有的 Java…", .Type = MyListItem.CheckType.Clickable}
            AddHandler ImportItem.MouseLeftButtonUp,
            Sub(sender As Object, e As MouseButtonEventArgs)
                e.Handled = True
                ManuallyImportJava()
            End Sub
            ComboAdvanceJava.Items.Add(ImportItem)
        Catch ex As Exception
            Logger.Error(ex, "更新 Java 列表下拉框失败")
        End Try
    End Sub
    Private Sub MoveJavaInList(JavaEntry As Java, Offset As Integer)
        Configs.JavaList.Edit(
        Sub(ByRef List)
            Dim Index = List.IndexOf(JavaEntry)
            Dim NewIndex = Index + Offset
            If Index < 0 OrElse NewIndex < 0 OrElse NewIndex >= List.Count Then Return
            List.RemoveAt(Index)
            List.Insert(NewIndex, JavaEntry)
        End Sub)
        UpdateJavaLists()
    End Sub
    Private Sub BtnAdvanceJavaSearch_Click() Handles BtnAdvanceJavaSearch.Click
        ManuallySearchJava()
    End Sub

#End Region

#Region "其他选项"

    Private Sub WindowTypeUIRefresh() Handles ComboArgumentWindowType.SelectionChanged
        Dim IsVisibie = (ComboArgumentWindowType.SelectedIndex = 3).ToVisibility
        TextArgumentWindowHeight.Visibility = IsVisibie
        LabArgumentWindowMiddle.Visibility = IsVisibie
        TextArgumentWindowWidth.Visibility = IsVisibie
    End Sub

    '可见性选择直接关闭的警告
    Private Sub ComboArgumentVisibie_SizeChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboArgumentVisibie.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboArgumentVisibie.SelectedIndex = 0 Then
            If MyMsgBox("若在游戏启动后立即关闭启动器，崩溃检测、更改游戏标题等功能将失效。" & vbCrLf &
                        "如果想保留这些功能，可以选择让启动器在游戏启动后隐藏，游戏退出后自动关闭。", "提醒", "继续", "取消") = 2 Then
                If e.RemovedItems.Count > 0 Then ComboArgumentVisibie.SelectedItem = e.RemovedItems(0)
            End If
        End If
    End Sub

    '开启自动内存优化的警告
    Private Sub CheckArgumentRam_Change() Handles CheckArgumentRam.Change
        If AniControlEnabled <> 0 Then Return
        If Not CheckArgumentRam.Checked Then Return
        If MyMsgBox("内存优化会显著延长启动耗时，建议仅在内存不足时开启。" & vbCrLf &
                    "如果你在使用机械硬盘，这还可能导致一小段时间的严重卡顿。" &
                    If(WindowsUtils.HasAdminRole(), "", $"{vbCrLf}{vbCrLf}每次启动游戏，PCL 都需要申请管理员权限以进行内存优化。{vbCrLf}若想自动授予权限，可以右键 PCL，打开 属性 → 兼容性 → 以管理员身份运行此程序。"),
                    "提醒", "确定", "取消") = 2 Then
            CheckArgumentRam.Checked = False
        End If
    End Sub

    '版本隔离提示
    Private Sub ComboArgumentIndie_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboArgumentIndieV2.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        MyMsgBox("本设置仅会对之后新安装的版本生效。" & vbCrLf & "如果要修改已安装的版本的隔离方式，请在它的版本独立设置中调整。")
    End Sub

#End Region

#Region "高级设置"

    Private Sub TextAdvanceRun_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextAdvanceRun.TextChanged
        CheckAdvanceRunWait.Visibility = If(TextAdvanceRun.Text = "", Visibility.Collapsed, Visibility.Visible)
    End Sub

    'JVM 参数重设
    Private Sub TextAdvanceJvm_TextChanged() Handles TextAdvanceJvm.ValidatedTextChanged
        BtnAdvanceJvmReset.Visibility = If(TextAdvanceJvm.Text = Settings.GetDefault("LaunchAdvanceJvm"), Visibility.Hidden, Visibility.Visible)
    End Sub
    Private Sub BtnAdvanceJvmReset_Click(sender As Object, e As EventArgs) Handles BtnAdvanceJvmReset.Click
        Settings.Reset("LaunchAdvanceJvm")
        Refresh()
    End Sub

    '去除参数中的回车
    Private Sub ReplaceEnter(sender As MyTextBox, e As TextChangedEventArgs) Handles TextAdvanceJvm.TextChanged, TextAdvanceGame.TextChanged
        Dim NewText = sender.Text.ReplaceLineEndings(" ", mergeMultiple:=True)
        If NewText = sender.Text Then Return
        Dim CaretIndex = sender.CaretIndex
        sender.Text = NewText
        sender.CaretIndex = Math.Max(0, CaretIndex - 1)
    End Sub

#End Region

    '切换到版本独立设置
    Private Sub BtnSwitch_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSwitch.Click
        McInstanceSelected.Load()
        PageInstanceLeft.Instance = McInstanceSelected
        FrmMain.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.InstanceSetup)
    End Sub

End Class
