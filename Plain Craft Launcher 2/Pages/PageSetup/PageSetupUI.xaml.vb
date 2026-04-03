Public Class PageSetupUI

    Private Sub PageSetupUI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
        ThemeCheckAll(True)
        If ThemeDontClick <> 0 Then
            Dim NewText As String = Nothing
            Select Case ThemeDontClick
                Case 1
                    NewText = "眼瞎白"
                Case 2
                    NewText = "真·滑稽彩"
            End Select
            For Each Control In PanLauncherTheme.Children
                If (TypeOf Control Is MyRadioBox) AndAlso CType(Control, MyRadioBox).IsEnabled Then CType(Control, MyRadioBox).Text = NewText
            Next
        End If

        AniControlEnabled += 1
        Refresh() '#4826
        AniControlEnabled -= 1

        '非重复加载部分
        Static Reloaded As Boolean = False
        If Reloaded Then Return
        Reloaded = True

        SliderLoad()

#If BETA Then
        PanLauncherHide.Visibility = Visibility.Visible
#End If

        '设置解锁
        If Not RadioLauncherTheme8.IsEnabled Then LabLauncherTheme8Copy.ToolTip = "累积赞助达到 ¥23.33 后，在爱发电私信发送【解锁码】以解锁。" & vbCrLf & "右键打开赞助页面，如果觉得 PCL 做得还不错就支持一下吧 =w=！"
        RadioLauncherTheme8.ToolTip = "累积赞助达到 ¥23.33 后，在爱发电私信发送【解锁码】以解锁"
        If Not RadioLauncherTheme9.IsEnabled Then LabLauncherTheme9Copy.ToolTip = "· 反馈一个 Bug，在标记为 [完成] 后回复识别码要求解锁（右键打开反馈页面）" & vbCrLf & "· 提交一个 Pull Request 或主页预设，在标记为 [完成] 后回复识别码要求解锁"
        RadioLauncherTheme9.ToolTip = "· 反馈一个 Bug，在标记为 [完成] 后回复识别码要求解锁" & vbCrLf & "· 提交一个 Pull Request 或主页预设，在标记为 [完成] 后回复识别码要求解锁"
        '极客蓝的处理在 ThemeCheck 中

    End Sub
    Public Sub Refresh()
        Try
            SettingService.RefreshSettings(Me)
            BackgroundRefresh(False, False)

            '标题栏
            CType(FindName("RadioLogoType" & Settings.Get("UiLogoType")), MyRadioBox).Checked = True
            CheckLogoLeft.Visibility = If(RadioLogoType0.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoText.Visibility = If(RadioLogoType2.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoChange.Visibility = If(RadioLogoType3.Checked, Visibility.Visible, Visibility.Collapsed)
            TextLogoText.Text = Settings.Get("UiLogoText")
            CheckLogoLeft.Checked = Settings.Get("UiLogoLeft")

            '背景音乐
            CheckMusicRandom.Checked = Settings.Get("UiMusicRandom")
            CheckMusicAuto.Checked = Settings.Get("UiMusicAuto")
            CheckMusicStop.Checked = Settings.Get("UiMusicStop")
            CheckMusicStart.Checked = Settings.Get("UiMusicStart")
            SliderMusicVolume.Value = Settings.Get("UiMusicVolume")
            MusicRefreshUI()

            '主页
            CType(FindName("RadioCustomType" & Settings.Get("UiCustomType")), MyRadioBox).Checked = True
            OnUiCustomTypeChanged(Settings.Get("UiCustomType"))
            TextCustomNet.Text = Settings.Get("UiCustomNet")

            '功能隐藏
            CheckLauncherEmail.Checked = Settings.Get("UiLauncherEmail")
            CheckHiddenPageDownload.Checked = Settings.Get("UiHiddenPageDownload")
            CheckHiddenPageLink.Checked = Settings.Get("UiHiddenPageLink")
            CheckHiddenPageSetup.Checked = Settings.Get("UiHiddenPageSetup")
            CheckHiddenPageOther.Checked = Settings.Get("UiHiddenPageOther")
            CheckHiddenFunctionSelect.Checked = Settings.Get("UiHiddenFunctionSelect")
            CheckHiddenFunctionModUpdate.Checked = Settings.Get("UiHiddenFunctionModUpdate")
            CheckHiddenFunctionHidden.Checked = Settings.Get("UiHiddenFunctionHidden")
            CheckHiddenSetupLaunch.Checked = Settings.Get("UiHiddenSetupLaunch")
            CheckHiddenSetupUI.Checked = Settings.Get("UiHiddenSetupUi")
            CheckHiddenSetupLink.Checked = Settings.Get("UiHiddenSetupLink")
            CheckHiddenSetupSystem.Checked = Settings.Get("UiHiddenSetupSystem")
            CheckHiddenOtherAbout.Checked = Settings.Get("UiHiddenOtherAbout")
            CheckHiddenOtherFeedback.Checked = Settings.Get("UiHiddenOtherFeedback")
            CheckHiddenOtherVote.Checked = Settings.Get("UiHiddenOtherVote")
            CheckHiddenOtherHelp.Checked = Settings.Get("UiHiddenOtherHelp")
            CheckHiddenOtherTest.Checked = Settings.Get("UiHiddenOtherTest")

        Catch ex As NullReferenceException
            Log(ex, "个性化设置项存在异常，已被自动重置", LogLevel.Msgbox)
            Reset()
        Catch ex As Exception
            Log(ex, "重载个性化设置时出错", LogLevel.Feedback)
        End Try
    End Sub

    '初始化
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Settings.Reset("UiLauncherLogo")
            Settings.Reset("UiLauncherEmail")
            Settings.Reset("UiLogoType")
            Settings.Reset("UiLogoText")
            Settings.Reset("UiLogoLeft")
            Settings.Reset("UiMusicVolume")
            Settings.Reset("UiMusicStop")
            Settings.Reset("UiMusicStart")
            Settings.Reset("UiMusicRandom")
            Settings.Reset("UiMusicAuto")
            Settings.Reset("UiCustomType")
            Settings.Reset("UiCustomNet")
            Settings.Reset("UiHiddenPageDownload")
            Settings.Reset("UiHiddenPageLink")
            Settings.Reset("UiHiddenPageSetup")
            Settings.Reset("UiHiddenPageOther")
            Settings.Reset("UiHiddenFunctionSelect")
            Settings.Reset("UiHiddenFunctionModUpdate")
            Settings.Reset("UiHiddenFunctionHidden")
            Settings.Reset("UiHiddenSetupLaunch")
            Settings.Reset("UiHiddenSetupUi")
            Settings.Reset("UiHiddenSetupLink")
            Settings.Reset("UiHiddenSetupSystem")
            Settings.Reset("UiHiddenOtherAbout")
            Settings.Reset("UiHiddenOtherFeedback")
            Settings.Reset("UiHiddenOtherVote")
            Settings.Reset("UiHiddenOtherHelp")
            Settings.Reset("UiHiddenOtherTest")

            Log("[Setup] 已初始化个性化设置！")
            Hint("已初始化个性化设置", HintType.Green, False)
        Catch ex As Exception
            Log(ex, "初始化个性化设置失败", LogLevel.Msgbox)
        End Try

        Refresh()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles SliderMusicVolume.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Value)
    End Sub
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckMusicStop.Change, CheckMusicRandom.Change, CheckMusicAuto.Change, CheckLogoLeft.Change, CheckHiddenFunctionHidden.Change, CheckHiddenFunctionSelect.Change, CheckHiddenFunctionModUpdate.Change, CheckHiddenPageDownload.Change, CheckHiddenPageLink.Change, CheckHiddenPageOther.Change, CheckHiddenPageSetup.Change, CheckHiddenSetupLaunch.Change, CheckHiddenSetupSystem.Change, CheckHiddenSetupLink.Change, CheckHiddenSetupUI.Change, CheckHiddenOtherAbout.Change, CheckHiddenOtherFeedback.Change, CheckHiddenOtherVote.Change, CheckHiddenOtherHelp.Change, CheckHiddenOtherTest.Change, CheckMusicStart.Change, CheckLauncherEmail.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Checked)
    End Sub
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextLogoText.ValidatedTextChanged, TextCustomNet.ValidatedTextChanged
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Text)
    End Sub
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioLogoType0.Check, RadioLogoType1.Check, RadioLogoType2.Check, RadioLogoType3.Check, RadioCustomType0.Check, RadioCustomType1.Check, RadioCustomType2.Check, RadioCustomType3.Check
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag.ToString.Split("/")(0), Val(sender.Tag.ToString.Split("/")(1)))
    End Sub

    '背景图片
    Private Sub BtnUIBgOpen_Click(sender As Object, e As EventArgs) Handles BtnBackgroundOpen.Click
        OpenExplorer(Path & "PCL\Pictures\")
    End Sub
    Private Sub BtnBackgroundRefresh_Click(sender As Object, e As EventArgs) Handles BtnBackgroundRefresh.Click
        BackgroundRefresh(True, True)
    End Sub
    Public Sub BackgroundRefreshUI(Show As Boolean, Count As Integer)
        If IsNothing(PanBackgroundOpacity) Then Return
        If Show Then
            PanBackgroundOpacity.Visibility = Visibility.Visible
            PanBackgroundBlur.Visibility = Visibility.Visible
            PanBackgroundSuit.Visibility = Visibility.Visible
            BtnBackgroundClear.Visibility = Visibility.Visible
            CardBackground.Title = "背景图片（" & Count & " 张）"
        Else
            PanBackgroundOpacity.Visibility = Visibility.Collapsed
            PanBackgroundBlur.Visibility = Visibility.Collapsed
            PanBackgroundSuit.Visibility = Visibility.Collapsed
            BtnBackgroundClear.Visibility = Visibility.Collapsed
            CardBackground.Title = "背景图片"
        End If
        CardBackground.TriggerForceResize()
    End Sub
    Private Sub BtnBackgroundClear_Click(sender As Object, e As EventArgs) Handles BtnBackgroundClear.Click
        If MyMsgBox("即将删除背景图片文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            DeleteDirectory(Path & "PCL\Pictures")
            BackgroundRefresh(False, True)
            Hint("背景图片已清空！", HintType.Green)
        End If
    End Sub
    ''' <summary>
    ''' 刷新背景图片及设置页 UI。
    ''' </summary>
    ''' <param name="IsHint">是否显示刷新提示。</param>
    ''' <param name="Refresh">是否刷新图片显示。</param>
    Public Shared Sub BackgroundRefresh(IsHint As Boolean, Refresh As Boolean)
        Try

            '获取可用的图片文件
            Directory.CreateDirectory(Path & "PCL\Pictures\")
            Dim Pic As New List(Of String)
            For Each File In EnumerateFiles(Path & "PCL\Pictures\")
                If File.Extension.Lower <> ".ini" AndAlso File.Extension.Lower <> ".db" Then '文件夹可能会被加入 .ini 和 thumbs.db
                    Pic.Add(File.FullName)
                End If
            Next
            '加载
            If Not Pic.Any() Then
                If Refresh Then
                    If FrmMain.ImgBack.Visibility = Visibility.Collapsed Then
                        If IsHint Then Hint("未检测到可用背景图片！", HintType.Red)
                    Else
                        FrmMain.ImgBack.Visibility = Visibility.Collapsed
                        If IsHint Then Hint("背景图片已清除！", HintType.Green)
                    End If
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(False, 0)
            Else
                If Refresh Then
                    Dim Address As String = RandomOne(Pic)
                    Try
                        Log("[UI] 加载背景图片：" & Address)
                        FrmMain.ImgBack.Background = New MyBitmap(Address)
                        FrmMain.ImgBack.Visibility = Visibility.Visible
                        FrmMain.UpdateBackgroundAndTitleBar()
                        If IsHint Then Hint("背景图片已刷新：" & GetFileNameFromPath(Address), HintType.Green, False)
                    Catch ex As Exception
                        If ex.Message.Contains("参数无效") Then
                            Log("刷新背景图片失败，该图片文件可能并非标准格式。" & vbCrLf &
                                "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。" & vbCrLf &
                                "文件：" & Address, LogLevel.Msgbox)
                        Else
                            Log(ex, "刷新背景图片失败（" & Address & "）", LogLevel.Msgbox)
                        End If
                    End Try
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(True, Pic.Count)
            End If

        Catch ex As Exception
            Log(ex, "刷新背景图片时出现未知错误", LogLevel.Feedback)
        End Try
    End Sub

    '顶部栏
    Private Sub BtnLogoChange_Click(sender As Object, e As EventArgs) Handles BtnLogoChange.Click
        Dim FileName As String = SelectFile("常用图片文件(*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp", "选择图片")
        If FileName = "" Then Return
        Try
            '拷贝文件
            File.Delete(Path & "PCL\Logo.png")
            CopyFile(FileName, Path & "PCL\Logo.png")
            '设置当前显示
            FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
            FrmMain.ImageTitleLogo.Source = Path & "PCL\Logo.png"
        Catch ex As Exception
            If ex.Message.Contains("参数无效") Then
                Log("改变标题栏图片失败，该图片文件可能并非标准格式。" & vbCrLf &
                    "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。", LogLevel.Msgbox)
            Else
                Log(ex, "设置标题栏图片失败", LogLevel.Msgbox)
            End If
            FrmMain.ImageTitleLogo.Source = Nothing
        End Try
    End Sub
    Private Sub RadioLogoType3_Check(sender As Object, e As RouteEventArgs) Handles RadioLogoType3.PreviewCheck
        If Not (AniControlEnabled = 0 AndAlso e.RaiseByMouse) Then Return
Refresh:
        '已有图片则不再选择
        If File.Exists(Path & "PCL\Logo.png") Then
            Try
                FrmMain.ImageTitleLogo.Source = Nothing '防止因为 Source 属性前后的值相同而不更新 (#5628)
                FrmMain.ImageTitleLogo.Source = Path & "PCL\Logo.png"
            Catch ex As Exception
                If ex.Message.Contains("参数无效") Then
                    Log("调整标题栏图片失败，该图片文件可能并非标准格式。" & vbCrLf &
                    "你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。", LogLevel.Msgbox)
                Else
                    Log(ex, "调整标题栏图片失败", LogLevel.Msgbox)
                End If
                FrmMain.ImageTitleLogo.Source = Nothing
                e.Handled = True
                Try
                    File.Delete(Path & "PCL\Logo.png")
                Catch exx As Exception
                    Log(exx, "清理错误的标题栏图片失败", LogLevel.Msgbox)
                End Try
            End Try
            Return
        End If
        '没有图片则要求选择
        Dim FileName As String = SelectFile("常用图片文件(*.png;*.jpeg;*.jpg;*.gif;*.webp)|*.png;*.jpeg;*.jpg;*.gif;*.webp", "选择图片")
        If FileName = "" Then
            FrmMain.ImageTitleLogo.Source = Nothing
            e.Handled = True
        Else
            Try
                '拷贝文件
                File.Delete(Path & "PCL\Logo.png")
                CopyFile(FileName, Path & "PCL\Logo.png")
                GoTo Refresh
            Catch ex As Exception
                Log(ex, "复制标题栏图片失败", LogLevel.Msgbox)
            End Try
        End If
    End Sub
    Private Sub BtnLogoDelete_Click(sender As Object, e As EventArgs) Handles BtnLogoDelete.Click
        Try
            File.Delete(Path & "PCL\Logo.png")
            RadioLogoType1.SetChecked(True, True)
            Hint("标题栏图片已清空！", HintType.Green)
        Catch ex As Exception
            Log(ex, "清空标题栏图片失败", LogLevel.Msgbox)
        End Try
    End Sub

    '背景音乐
    Private Sub BtnMusicOpen_Click(sender As Object, e As EventArgs) Handles BtnMusicOpen.Click
        OpenExplorer(Path & "PCL\Musics\")
    End Sub
    Private Sub BtnMusicRefresh_Click(sender As Object, e As EventArgs) Handles BtnMusicRefresh.Click
        MusicRefreshPlay(True)
    End Sub
    Public Sub MusicRefreshUI()
        If PanBackgroundOpacity Is Nothing Then Return
        If MusicAllList.Any Then
            PanMusicVolume.Visibility = Visibility.Visible
            PanMusicDetail.Visibility = Visibility.Visible
            BtnMusicClear.Visibility = Visibility.Visible
            CardMusic.Title = "背景音乐（" &
                EnumerateFiles(Path & "PCL\Musics\").Count(Function(f) Not {".ini", ".jpg", ".txt", ".cfg", ".lrc", ".db", ".png"}.Contains(f.Extension.Lower)) &
                " 首）"
        Else
            PanMusicVolume.Visibility = Visibility.Collapsed
            PanMusicDetail.Visibility = Visibility.Collapsed
            BtnMusicClear.Visibility = Visibility.Collapsed
            CardMusic.Title = "背景音乐"
        End If
        CardMusic.TriggerForceResize()
    End Sub
    Private Sub BtnMusicClear_Click(sender As Object, e As EventArgs) Handles BtnMusicClear.Click
        If MyMsgBox("即将删除背景音乐文件夹中的所有文件。" & vbCrLf & "此操作不可撤销，是否确定？", "警告",, "取消", IsWarn:=True) = 1 Then
            RunInThread(
            Sub()
                Hint("正在删除背景音乐……")
                '停止播放音乐
                MusicNAudio = Nothing
                MusicWaitingList = New List(Of String)
                MusicAllList = New List(Of String)
                Thread.Sleep(200)
                '删除文件
                Try
                    DeleteDirectory(Path & "PCL\Musics")
                    Hint("背景音乐已删除！", HintType.Green)
                Catch ex As Exception
                    Log(ex, "删除背景音乐失败", LogLevel.Msgbox)
                End Try
                Try
                    Directory.CreateDirectory(Path & "PCL\Musics")
                    RunInUi(Sub() MusicRefreshPlay(False))
                Catch ex As Exception
                    Log(ex, "重建背景音乐文件夹失败", LogLevel.Msgbox)
                End Try
            End Sub)
        End If
    End Sub
    Private Sub CheckMusicStart_Change() Handles CheckMusicStart.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStart.Checked Then CheckMusicStop.Checked = False
    End Sub
    Private Sub CheckMusicStop_Change() Handles CheckMusicStop.Change
        If AniControlEnabled <> 0 Then Return
        If CheckMusicStop.Checked Then CheckMusicStart.Checked = False
    End Sub

    '主页
    Private Sub BtnCustomFile_Click(sender As Object, e As EventArgs) Handles BtnCustomFile.Click
        Try
            If File.Exists(Path & "PCL\Custom.xaml") Then
                If MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn:=True) = 2 Then Return
            End If
            WriteFile(Path & "PCL\Custom.xaml", GetResources("Custom"))
            Hint("教学文件已生成！", HintType.Green)
            OpenExplorer(Path & "PCL\Custom.xaml")
        Catch ex As Exception
            Log(ex, "生成教学文件失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub BtnCustomRefresh_Click() Handles BtnCustomRefresh.Click
        FrmLaunchRight.ForceRefresh()
        Hint("已刷新主页！", HintType.Green)
    End Sub
    Private Sub BtnCustomTutorial_Click(sender As Object, e As EventArgs) Handles BtnCustomTutorial.Click
        MyMsgBox("1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。" & vbCrLf &
                 "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" & vbCrLf &
                 "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" & vbCrLf &
                 vbCrLf &
                 "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" & vbCrLf &
                 "直接将主页文件拖进 PCL 窗口也可以快捷加载。", "主页自定义教程")
    End Sub
    Public Shared Sub OnUiCustomTypeChanged(Value As Integer)
        If FrmSetupUI Is Nothing Then Return
        Select Case Value
            Case 0 '无
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
            Case 1 '本地
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Settings.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。"
                CustomEventService.SetEventType(FrmSetupUI.HintCustom, CustomEvent.EventType.None)
            Case 2 '联网
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Settings.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！"
                CustomEventService.SetEventType(FrmSetupUI.HintCustom, CustomEvent.EventType.打开网页)
                CustomEventService.SetEventData(FrmSetupUI.HintCustom, "https://github.com/Meloong-Git/PCL/discussions/2528")
            Case 3 '预设
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
        End Select
        FrmSetupUI.CardCustom.TriggerForceResize()
    End Sub

    '主题
    Private Sub LabLauncherTheme5Unlock_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles LabLauncherTheme5Unlock.MouseLeftButtonUp
        RadioLauncherTheme5Gray.Opacity -= 0.7
        RadioLauncherTheme5.Opacity += 0.7
        AniStart({
            AaOpacity(RadioLauncherTheme5Gray, 1, 1000 * AniSpeed, 500 * AniSpeed, New AniEaseInFluent),
            AaOpacity(RadioLauncherTheme5, -1, 1000 * AniSpeed, 500 * AniSpeed, New AniEaseInFluent)
        }, "ThemeUnlock")
        If RadioLauncherTheme5Gray.Opacity < 0.02 Then
            ThemeUnlock(5, UnlockHint:="隐藏主题 玄素黑 已解锁！")
            AniStop("ThemeUnlock")
            RadioLauncherTheme5.Checked = True
        End If
    End Sub
    Private Sub LabLauncherTheme11Click_MouseLeftButtonUp() Handles LabLauncherTheme11Click.MouseLeftButtonUp, RadioLauncherTheme11.MouseRightButtonUp
        If LabLauncherTheme11Click.Visibility = Visibility.Collapsed OrElse If(LabLauncherTheme11Click.ToolTip, "").ToString.Contains("点击") Then
            If MyMsgBox(
                "1. 不爬取或攻击相关服务或网站，不盗取相关账号，没有谜题可以或需要以此来解决。" & vbCrLf &
                "2. 不得篡改或损毁相关公开信息，请尽量让它们保持原状。" & vbCrLf &
                "3. 在你感到迷茫的时候，看看回声洞可能会给你带来惊喜。" & vbCrLf & vbCrLf &
                "若违规，可能会被从任意相关群中踢出！",
                "解密游戏的基本规则", "我知道了", "恕我拒绝") = 1 Then
                MyMsgBox("你需要用自己的智慧来找到下一步的线索……" & vbCrLf &
                         "初始线索：gnp.dorC61\60\20\0202\moc.x1xa.2s\\:sp" & "T".Lower & "th", "解密游戏") '防止触发病毒检测规则
            End If
        End If
    End Sub
    Private Sub LabLauncherTheme8Copy_MouseRightButtonUp() Handles LabLauncherTheme8Copy.MouseRightButtonUp, RadioLauncherTheme8.MouseRightButtonUp
        OpenWebsite("https://meloong.com/afd/a/LTCat")
    End Sub
    Private Sub LabLauncherTheme9Copy_MouseRightButtonUp() Handles LabLauncherTheme9Copy.MouseRightButtonUp, RadioLauncherTheme9.MouseRightButtonUp
        PageOtherLeft.TryFeedback()
    End Sub

    '主题自定义
    Private Sub RadioLauncherTheme14_Change(sender As Object, e As RouteEventArgs) Handles RadioLauncherTheme14.Changed
        If RadioLauncherTheme14.Checked Then
            If LabLauncherHue.Visibility = Visibility.Visible Then Return
            LabLauncherHue.Visibility = Visibility.Visible
            SliderLauncherHue.Visibility = Visibility.Visible
            LabLauncherSat.Visibility = Visibility.Visible
            SliderLauncherSat.Visibility = Visibility.Visible
            LabLauncherDelta.Visibility = Visibility.Visible
            SliderLauncherDelta.Visibility = Visibility.Visible
            LabLauncherLight.Visibility = Visibility.Visible
            SliderLauncherLight.Visibility = Visibility.Visible
        Else
            If LabLauncherHue.Visibility = Visibility.Collapsed Then Return
            LabLauncherHue.Visibility = Visibility.Collapsed
            SliderLauncherHue.Visibility = Visibility.Collapsed
            LabLauncherSat.Visibility = Visibility.Collapsed
            SliderLauncherSat.Visibility = Visibility.Collapsed
            LabLauncherDelta.Visibility = Visibility.Collapsed
            SliderLauncherDelta.Visibility = Visibility.Collapsed
            LabLauncherLight.Visibility = Visibility.Collapsed
            SliderLauncherLight.Visibility = Visibility.Collapsed
        End If
        CardLauncher.TriggerForceResize()
    End Sub
    Private Sub HSL_Change() Handles SliderLauncherHue.Change, SliderLauncherLight.Change, SliderLauncherSat.Change, SliderLauncherDelta.Change
        If AniControlEnabled <> 0 OrElse SliderLauncherSat Is Nothing OrElse Not SliderLauncherSat.IsLoaded Then Return
        ThemeRefresh()
    End Sub

#Region "功能隐藏"

    Private Shared _HiddenForceShow As Boolean = False
    ''' <summary>
    ''' 是否强制显示被禁用的功能。
    ''' </summary>
    Public Shared Property HiddenForceShow As Boolean
        Get
            Return _HiddenForceShow
        End Get
        Set(value As Boolean)
            _HiddenForceShow = value
            HiddenRefresh()
        End Set
    End Property

    ''' <summary>
    ''' 更新功能隐藏带来的显示变化。
    ''' </summary>
    Public Shared Sub HiddenRefresh() Handles Me.Loaded
        If FrmMain.PanTitleSelect Is Nothing OrElse Not FrmMain.PanTitleSelect.IsLoaded Then Return
        Try
            '顶部栏
            If Not HiddenForceShow AndAlso Settings.Get("UiHiddenPageDownload") AndAlso Settings.Get("UiHiddenPageLink") AndAlso Settings.Get("UiHiddenPageSetup") AndAlso Settings.Get("UiHiddenPageOther") Then
                '顶部栏已被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed
            Else
                '顶部栏未被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Visible
                FrmMain.BtnTitleSelect1.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenPageDownload"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect2.Visibility = Visibility.Collapsed 'If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenPageLink"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect3.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenPageSetup"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect4.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenPageOther"), Visibility.Collapsed, Visibility.Visible)
            End If
            '功能
            FrmLaunchLeft.RefreshButtonsUI()
            If FrmSetupUI IsNot Nothing Then
                FrmSetupUI.CardSwitch.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenFunctionHidden"), Visibility.Collapsed, Visibility.Visible)
            End If
            '设置子页面
            If FrmSetupLeft IsNot Nothing Then
                FrmSetupLeft.ItemLaunch.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenSetupLaunch"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemUI.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenSetupUi"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemLink.Visibility = Visibility.Collapsed 'If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenSetupLink"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemSystem.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenSetupSystem"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                Dim AvaliableCount As Integer = 0
                If Not Settings.Get("UiHiddenSetupLaunch") Then AvaliableCount += 1
                If Not Settings.Get("UiHiddenSetupUi") Then AvaliableCount += 1
                'If Not Settings.Get("UiHiddenSetupLink") Then AvaliableCount += 1
                If Not Settings.Get("UiHiddenSetupSystem") Then AvaliableCount += 1
                FrmSetupLeft.PanItem.Visibility = If(AvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            '更多子页面
            Dim OtherAvaliableCount As Integer = 0
            If Not Settings.Get("UiHiddenOtherHelp") Then OtherAvaliableCount += 1
            If Not Settings.Get("UiHiddenOtherAbout") Then OtherAvaliableCount += 1
            If Not Settings.Get("UiHiddenOtherTest") Then OtherAvaliableCount += 1
            If Not Settings.Get("UiHiddenOtherFeedback") Then OtherAvaliableCount += 1
            If Not Settings.Get("UiHiddenOtherVote") Then OtherAvaliableCount += 1
            If FrmOtherLeft IsNot Nothing Then
                FrmOtherLeft.ItemHelp.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenOtherHelp"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemFeedback.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenOtherFeedback"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemVote.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenOtherVote"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemAbout.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenOtherAbout"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemTest.Visibility = If(Not HiddenForceShow AndAlso Settings.Get("UiHiddenOtherTest"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                FrmOtherLeft.PanItem.Visibility = If(OtherAvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            If OtherAvaliableCount = 1 AndAlso Not HiddenForceShow Then
                If Not Settings.Get("UiHiddenOtherHelp") Then
                    FrmMain.BtnTitleSelect4.Text = "帮助"
                ElseIf Not Settings.Get("UiHiddenOtherAbout") Then
                    FrmMain.BtnTitleSelect4.Text = "关于"
                Else
                    FrmMain.BtnTitleSelect4.Text = "百宝箱"
                End If
            Else
                FrmMain.BtnTitleSelect4.Text = "更多"
            End If
            '各个页面的入口
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSelect Then FrmSelectRight.BtnEmptyDownload_Loaded()
            If FrmMain.PageCurrent = FormMain.PageType.Launch Then FrmLaunchLeft.RefreshButtonsUI()
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmInstanceModDisabled IsNot Nothing Then FrmInstanceModDisabled.BtnDownload_Loaded()
            '备注
            If FrmSetupUI IsNot Nothing Then FrmSetupUI.CardSwitch.Title = If(HiddenForceShow, "功能隐藏（已暂时关闭，按 F12 以重新启用）", "功能隐藏")
        Catch ex As Exception
            Log(ex, "刷新功能隐藏项目失败", LogLevel.Feedback)
        End Try
    End Sub

    'UI 协同改变
    Private Sub HiddenSetupMain() Handles CheckHiddenPageSetup.Change
        '设置主页面
        If CheckHiddenPageSetup.Checked Then
            '开启
            CheckHiddenSetupLaunch.Checked = True
            CheckHiddenSetupSystem.Checked = True
            CheckHiddenSetupLink.Checked = True
            CheckHiddenSetupUI.Checked = True
        Else
            '关闭
            If Settings.Get("UiHiddenSetupLaunch") AndAlso Settings.Get("UiHiddenSetupUi") AndAlso Settings.Get("UiHiddenSetupSystem") AndAlso Settings.Get("UiHiddenSetupLink") Then
                CheckHiddenSetupLaunch.Checked = False
                CheckHiddenSetupSystem.Checked = False
                CheckHiddenSetupLink.Checked = False
                CheckHiddenSetupUI.Checked = False
            End If
        End If
    End Sub
    Private Sub HiddenSetupSub() Handles CheckHiddenSetupLaunch.Change, CheckHiddenSetupSystem.Change, CheckHiddenSetupLink.Change, CheckHiddenSetupUI.Change
        '设置子页面
        If Settings.Get("UiHiddenSetupLaunch") AndAlso Settings.Get("UiHiddenSetupUi") AndAlso Settings.Get("UiHiddenSetupSystem") AndAlso Settings.Get("UiHiddenSetupLink") Then
            '已被全部隐藏
            CheckHiddenPageSetup.Checked = True
        Else
            '未被全部隐藏
            CheckHiddenPageSetup.Checked = False
        End If
    End Sub
    Private Sub HiddenOtherMain() Handles CheckHiddenPageOther.Change
        '更多主页面
        If CheckHiddenPageOther.Checked Then
            '开启
            CheckHiddenOtherAbout.Checked = True
            CheckHiddenOtherTest.Checked = True
            CheckHiddenOtherFeedback.Checked = True
            CheckHiddenOtherVote.Checked = True
            CheckHiddenOtherHelp.Checked = True
        Else
            '关闭
            If Settings.Get("UiHiddenOtherHelp") AndAlso Settings.Get("UiHiddenOtherAbout") AndAlso Settings.Get("UiHiddenOtherTest") AndAlso
                Settings.Get("UiHiddenOtherVote") AndAlso Settings.Get("UiHiddenOtherFeedback") Then
                CheckHiddenOtherAbout.Checked = False
                CheckHiddenOtherTest.Checked = False
                CheckHiddenOtherFeedback.Checked = False
                CheckHiddenOtherVote.Checked = False
                CheckHiddenOtherHelp.Checked = False
            End If
        End If
    End Sub
    Private Sub HiddenOtherSub(sender As Object, user As Boolean) Handles CheckHiddenOtherHelp.Change, CheckHiddenOtherAbout.Change, CheckHiddenOtherTest.Change
        '更多子页面（有具体内容的）
        If Settings.Get("UiHiddenOtherHelp") AndAlso Settings.Get("UiHiddenOtherAbout") AndAlso Settings.Get("UiHiddenOtherTest") Then
            '已被全部隐藏
            CheckHiddenPageOther.Checked = True
        Else
            '未被全部隐藏
            CheckHiddenPageOther.Checked = False
        End If
        '修改无具体内容的项
        If Not user Then Return
        If Settings.Get("UiHiddenOtherHelp") AndAlso Settings.Get("UiHiddenOtherAbout") AndAlso Settings.Get("UiHiddenOtherTest") Then
            CheckHiddenOtherFeedback.Checked = True
            CheckHiddenOtherVote.Checked = True
        End If
    End Sub
    Private Sub HiddenOtherNet(sender As Object, user As Boolean) Handles CheckHiddenOtherFeedback.Change, CheckHiddenOtherVote.Change
        '更多子页面（无具体内容的）
        If Not user Then Return
        If Settings.Get("UiHiddenOtherHelp") AndAlso Settings.Get("UiHiddenOtherAbout") AndAlso Settings.Get("UiHiddenOtherTest") AndAlso
            (Not Settings.Get("UiHiddenOtherFeedback") OrElse Not Settings.Get("UiHiddenOtherVote")) Then
            CheckHiddenOtherAbout.Checked = False
            CheckHiddenOtherTest.Checked = False
            CheckHiddenOtherHelp.Checked = False
        End If
    End Sub

    '警告提示
    Private Sub HiddenHint(sender As Object, user As Boolean) Handles CheckHiddenFunctionHidden.Change, CheckHiddenPageSetup.Change, CheckHiddenSetupUI.Change
        If AniControlEnabled = 0 AndAlso sender.Checked Then Hint("按 F12 即可暂时关闭功能隐藏设置。千万别忘了，要不然设置就改不回来了……")
    End Sub

#End Region

    '滑动条
    Private Sub SliderLoad()
        SliderMusicVolume.GetHintText = Function(v) Math.Ceiling(v * 0.1) & "%"
        SliderLauncherTransparent.GetHintText = Function(v) Math.Round(40 + v * 0.1) & "%"
        SliderLauncherHue.GetHintText = Function(v) v & "°"
        SliderLauncherSat.GetHintText = Function(v) v & "%"
        SliderLauncherDelta.GetHintText =
        Function(Value As Integer)
            If Value > 90 Then
                Return "+" & (Value - 90)
            ElseIf Value = 90 Then
                Return 0
            Else
                Return Value - 90
            End If
        End Function
        SliderLauncherLight.GetHintText =
        Function(Value As Integer)
            If Value > 20 Then
                Return "+" & (Value - 20)
            ElseIf Value = 20 Then
                Return 0
            Else
                Return Value - 20
            End If
        End Function
        SliderBackgroundOpacity.GetHintText = Function(v) Math.Round(v * 0.1) & "%"
        SliderBackgroundBlur.GetHintText = Function(v) v & " 像素"
    End Sub

End Class
