Public Class PageSetupUI

    Public Shadows IsLoaded As Boolean = False

    Private Sub PageSetupUI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        ThemeCheckAll(True)

        If ThemeDontClick <> 0 Then
            Dim NewText As String
            Select Case ThemeDontClick
                Case 1
                    NewText = GetLang("LangThemeRealWhite")
                Case 2
                    NewText = GetLang("LangThemeRealFunny")
                Case Else
                    NewText = "？？？"
            End Select
            For Each Control In PanLauncherTheme.Children
                If (TypeOf Control Is MyRadioBox) AndAlso CType(Control, MyRadioBox).IsEnabled Then
                    CType(Control, MyRadioBox).Text = NewText
                End If
            Next
        End If

        AniControlEnabled += 1
        Reload() '#4826，在每次进入页面时都刷新一下
        AniControlEnabled -= 1

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        SliderLoad()

#If BETA Then
        PanLauncherHide.Visibility = Visibility.Visible
#End If

        '设置解锁
        If Not RadioLauncherTheme8.IsEnabled Then LabLauncherTheme8Copy.ToolTip = GetLang("LangThemeUnlockCopyTip8")
        RadioLauncherTheme8.ToolTip = GetLang("LangThemeUnlockTip8")
        If Not RadioLauncherTheme9.IsEnabled Then LabLauncherTheme9Copy.ToolTip = GetLang("LangThemeUnlockCopyTip9")
        RadioLauncherTheme9.ToolTip = GetLang("LangThemeUnlockTip9")
        '极客蓝的处理在 ThemeCheck 中

    End Sub
    Public Sub Reload()
        Try

            '启动器
            SliderLauncherOpacity.Value = Setup.Get("UiLauncherTransparent")
            SliderLauncherHue.Value = Setup.Get("UiLauncherHue")
            SliderLauncherSat.Value = Setup.Get("UiLauncherSat")
            SliderLauncherDelta.Value = Setup.Get("UiLauncherDelta")
            SliderLauncherLight.Value = Setup.Get("UiLauncherLight")
            If Setup.Get("UiLauncherTheme") <= 14 Then CType(FindName("RadioLauncherTheme" & Setup.Get("UiLauncherTheme")), MyRadioBox).Checked = True
            CheckLauncherLogo.Checked = Setup.Get("UiLauncherLogo")
            CheckLauncherEmail.Checked = Setup.Get("UiLauncherEmail")

            '背景图片
            SliderBackgroundOpacity.Value = Setup.Get("UiBackgroundOpacity")
            SliderBackgroundBlur.Value = Setup.Get("UiBackgroundBlur")
            ComboBackgroundSuit.SelectedIndex = Setup.Get("UiBackgroundSuit")
            CheckBackgroundColorful.Checked = Setup.Get("UiBackgroundColorful")
            BackgroundRefresh(False, False)

            '标题栏
            CType(FindName("RadioLogoType" & Setup.Get("UiLogoType")), MyRadioBox).Checked = True
            CheckLogoLeft.Visibility = If(RadioLogoType0.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoText.Visibility = If(RadioLogoType2.Checked, Visibility.Visible, Visibility.Collapsed)
            PanLogoChange.Visibility = If(RadioLogoType3.Checked, Visibility.Visible, Visibility.Collapsed)
            TextLogoText.Text = Setup.Get("UiLogoText")
            CheckLogoLeft.Checked = Setup.Get("UiLogoLeft")

            '背景音乐
            CheckMusicRandom.Checked = Setup.Get("UiMusicRandom")
            CheckMusicAuto.Checked = Setup.Get("UiMusicAuto")
            CheckMusicStop.Checked = Setup.Get("UiMusicStop")
            CheckMusicStart.Checked = Setup.Get("UiMusicStart")
            SliderMusicVolume.Value = Setup.Get("UiMusicVolume")
            MusicRefreshUI()

            '主页
            Try
                ComboCustomPreset.SelectedIndex = Setup.Get("UiCustomPreset")
            Catch
                Setup.Reset("UiCustomPreset")
            End Try
            CType(FindName("RadioCustomType" & Setup.Load("UiCustomType", ForceReload:=True)), MyRadioBox).Checked = True
            TextCustomNet.Text = Setup.Get("UiCustomNet")

            '功能隐藏
            CheckHiddenPageDownload.Checked = Setup.Get("UiHiddenPageDownload")
            CheckHiddenPageLink.Checked = Setup.Get("UiHiddenPageLink")
            CheckHiddenPageSetup.Checked = Setup.Get("UiHiddenPageSetup")
            CheckHiddenPageOther.Checked = Setup.Get("UiHiddenPageOther")
            CheckHiddenFunctionSelect.Checked = Setup.Get("UiHiddenFunctionSelect")
            CheckHiddenFunctionModUpdate.Checked = Setup.Get("UiHiddenFunctionModUpdate")
            CheckHiddenFunctionHidden.Checked = Setup.Get("UiHiddenFunctionHidden")
            CheckHiddenSetupLaunch.Checked = Setup.Get("UiHiddenSetupLaunch")
            CheckHiddenSetupUI.Checked = Setup.Get("UiHiddenSetupUi")
            CheckHiddenSetupLink.Checked = Setup.Get("UiHiddenSetupLink")
            CheckHiddenSetupSystem.Checked = Setup.Get("UiHiddenSetupSystem")
            CheckHiddenOtherAbout.Checked = Setup.Get("UiHiddenOtherAbout")
            CheckHiddenOtherFeedback.Checked = Setup.Get("UiHiddenOtherFeedback")
            CheckHiddenOtherVote.Checked = Setup.Get("UiHiddenOtherVote")
            CheckHiddenOtherHelp.Checked = Setup.Get("UiHiddenOtherHelp")
            CheckHiddenOtherTest.Checked = Setup.Get("UiHiddenOtherTest")

        Catch ex As NullReferenceException
            Log(ex, GetLang("LangHintThemeSetIncorrect"), LogLevel.Msgbox)
            Reset()
        Catch ex As Exception
            Log(ex, GetLang("LangHintThemeSetLoadFail"), LogLevel.Feedback)
        End Try
    End Sub

    '初始化
    Public Sub Reset()
        Try
            Setup.Reset("UiLauncherTransparent")
            Setup.Reset("UiLauncherTheme")
            Setup.Reset("UiLauncherLogo")
            Setup.Reset("UiLauncherHue")
            Setup.Reset("UiLauncherSat")
            Setup.Reset("UiLauncherDelta")
            Setup.Reset("UiLauncherLight")
            Setup.Reset("UiLauncherEmail")
            Setup.Reset("UiBackgroundColorful")
            Setup.Reset("UiBackgroundOpacity")
            Setup.Reset("UiBackgroundBlur")
            Setup.Reset("UiBackgroundSuit")
            Setup.Reset("UiLogoType")
            Setup.Reset("UiLogoText")
            Setup.Reset("UiLogoLeft")
            Setup.Reset("UiMusicVolume")
            Setup.Reset("UiMusicStop")
            Setup.Reset("UiMusicStart")
            Setup.Reset("UiMusicRandom")
            Setup.Reset("UiMusicAuto")
            Setup.Reset("UiCustomType")
            Setup.Reset("UiCustomPreset")
            Setup.Reset("UiCustomNet")
            Setup.Reset("UiHiddenPageDownload")
            Setup.Reset("UiHiddenPageLink")
            Setup.Reset("UiHiddenPageSetup")
            Setup.Reset("UiHiddenPageOther")
            Setup.Reset("UiHiddenFunctionSelect")
            Setup.Reset("UiHiddenFunctionModUpdate")
            Setup.Reset("UiHiddenFunctionHidden")
            Setup.Reset("UiHiddenSetupLaunch")
            Setup.Reset("UiHiddenSetupUi")
            Setup.Reset("UiHiddenSetupLink")
            Setup.Reset("UiHiddenSetupSystem")
            Setup.Reset("UiHiddenOtherAbout")
            Setup.Reset("UiHiddenOtherFeedback")
            Setup.Reset("UiHiddenOtherVote")
            Setup.Reset("UiHiddenOtherHelp")
            Setup.Reset("UiHiddenOtherTest")

            Log("[Setup] 已初始化个性化设置！")
            Hint(GetLang("LangHintThemeSetInit"), HintType.Finish, False)
        Catch ex As Exception
            Log(ex, GetLang("LangHintThemeSetInitFail"), LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles SliderBackgroundOpacity.Change, SliderBackgroundBlur.Change, SliderLauncherOpacity.Change, SliderMusicVolume.Change, SliderLauncherHue.Change, SliderLauncherLight.Change, SliderLauncherSat.Change, SliderLauncherDelta.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value)
    End Sub
    Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboBackgroundSuit.SelectionChanged, ComboCustomPreset.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    End Sub
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckMusicStop.Change, CheckMusicRandom.Change, CheckMusicAuto.Change, CheckBackgroundColorful.Change, CheckLogoLeft.Change, CheckLauncherLogo.Change, CheckHiddenFunctionHidden.Change, CheckHiddenFunctionSelect.Change, CheckHiddenFunctionModUpdate.Change, CheckHiddenPageDownload.Change, CheckHiddenPageLink.Change, CheckHiddenPageOther.Change, CheckHiddenPageSetup.Change, CheckHiddenSetupLaunch.Change, CheckHiddenSetupSystem.Change, CheckHiddenSetupLink.Change, CheckHiddenSetupUI.Change, CheckHiddenOtherAbout.Change, CheckHiddenOtherFeedback.Change, CheckHiddenOtherVote.Change, CheckHiddenOtherHelp.Change, CheckHiddenOtherTest.Change, CheckMusicStart.Change, CheckLauncherEmail.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    End Sub
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextLogoText.ValidatedTextChanged, TextCustomNet.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioLogoType0.Check, RadioLogoType1.Check, RadioLogoType2.Check, RadioLogoType3.Check, RadioLauncherTheme0.Check, RadioLauncherTheme1.Check, RadioLauncherTheme2.Check, RadioLauncherTheme3.Check, RadioLauncherTheme4.Check, RadioLauncherTheme5.Check, RadioLauncherTheme6.Check, RadioLauncherTheme7.Check, RadioLauncherTheme8.Check, RadioLauncherTheme9.Check, RadioLauncherTheme10.Check, RadioLauncherTheme11.Check, RadioLauncherTheme12.Check, RadioLauncherTheme13.Check, RadioLauncherTheme14.Check, RadioCustomType0.Check, RadioCustomType1.Check, RadioCustomType2.Check, RadioCustomType3.Check
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag.ToString.Split("/")(0), Val(sender.Tag.ToString.Split("/")(1)))
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
            CardBackground.Title = GetLang("LangBackgroundPicCount", Count)
        Else
            PanBackgroundOpacity.Visibility = Visibility.Collapsed
            PanBackgroundBlur.Visibility = Visibility.Collapsed
            PanBackgroundSuit.Visibility = Visibility.Collapsed
            BtnBackgroundClear.Visibility = Visibility.Collapsed
            CardBackground.Title = GetLang("LangBackgroundPic")
        End If
        CardBackground.TriggerForceResize()
    End Sub
    Private Sub BtnBackgroundClear_Click(sender As Object, e As EventArgs) Handles BtnBackgroundClear.Click
        If MyMsgBox(GetLang("LangDialogDeleteAllBackgroundPicContent"), GetLang("LangDialogTitleWarning"),, GetLang("LangDialogBtnCancel"), IsWarn:=True) = 1 Then
            DeleteDirectory(Path & "PCL\Pictures")
            BackgroundRefresh(False, True)
            Hint(GetLang("LangHintBackgroundPicDeleted"), HintType.Finish)
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
                If File.Extension.ToLower <> ".ini" AndAlso File.Extension.ToLower <> ".db" Then '文件夹可能会被加入 .ini 和 thumbs.db
                    Pic.Add(File.FullName)
                End If
            Next
            '加载
            If Not Pic.Any() Then
                If Refresh Then
                    If FrmMain.ImgBack.Visibility = Visibility.Collapsed Then
                        If IsHint Then Hint(GetLang("LangHintBackgroundPicNotFound"), HintType.Critical)
                    Else
                        FrmMain.ImgBack.Visibility = Visibility.Collapsed
                        If IsHint Then Hint(GetLang("LangHintBackgroundPicDeleted"), HintType.Finish)
                    End If
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(False, 0)
            Else
                If Refresh Then
                    Dim Address As String = RandomOne(Pic)
                    Try
                        Log("[UI] 加载背景图片：" & Address)
                        FrmMain.ImgBack.Background = New MyBitmap(Address)
                        Setup.Load("UiBackgroundSuit", True)
                        FrmMain.ImgBack.Visibility = Visibility.Visible
                        If IsHint Then Hint(GetLang("LangHintBackgroundPicRefreshed") & GetFileNameFromPath(Address), HintType.Finish, False)
                    Catch ex As Exception
                        If ex.Message.Contains("参数无效") Then
                            Log(GetLang("LangDialogBackgroundPicRefreshFailA") & Address, LogLevel.Msgbox)
                        Else
                            Log(ex, GetLang("LangDialogBackgroundPicRefreshFailB", Address), LogLevel.Msgbox)
                        End If
                    End Try
                End If
                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BackgroundRefreshUI(True, Pic.Count)
            End If

        Catch ex As Exception
            Log(ex, GetLang("LangDialogBackgroundPicRefreshFailUnknown"), LogLevel.Feedback)
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
                Log(GetLang("LangDialogTitlePicChangeFailA"), LogLevel.Msgbox)
            Else
                Log(ex, GetLang("LangDialogTitlePicChangeFailB"), LogLevel.Msgbox)
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
                    Log(GetLang("LangDialogTitlePicChangeFailC"), LogLevel.Msgbox)
                Else
                    Log(ex, GetLang("LangDialogTitlePicChangeFailD"), LogLevel.Msgbox)
                End If
                FrmMain.ImageTitleLogo.Source = Nothing
                e.Handled = True
                Try
                    File.Delete(Path & "PCL\Logo.png")
                Catch exx As Exception
                    Log(exx, GetLang("LangDialogTitlePicChangeFailE"), LogLevel.Msgbox)
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
                Log(ex, GetLang("LangDialogTitlePicChangeFailF"), LogLevel.Msgbox)
            End Try
        End If
    End Sub
    Private Sub BtnLogoDelete_Click(sender As Object, e As EventArgs) Handles BtnLogoDelete.Click
        Try
            File.Delete(Path & "PCL\Logo.png")
            RadioLogoType1.SetChecked(True, True)
            Hint(GetLang("LangHintTitlePicEmptied"), HintType.Finish)
        Catch ex As Exception
            Log(ex, GetLang("LangDialogTitlePicEmptyFail"), LogLevel.Msgbox)
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
            CardMusic.Title = GetLang("LangBackgroundMusicCount", EnumerateFiles(Path & "PCL\Musics\").Count(Function(f) {".ini", ".jpg", ".txt", ".cfg", ".lrc", ".db", ".png"}.Contains(f.Extension.ToLower)))
        Else
            PanMusicVolume.Visibility = Visibility.Collapsed
            PanMusicDetail.Visibility = Visibility.Collapsed
            BtnMusicClear.Visibility = Visibility.Collapsed
            CardMusic.Title = GetLang("LangBackgroundMusic")
        End If
        CardMusic.TriggerForceResize()
    End Sub
    Private Sub BtnMusicClear_Click(sender As Object, e As EventArgs) Handles BtnMusicClear.Click
        If MyMsgBox(GetLang("LangDialogBackgroundMusicDeleteContent"), GetLang("LangDialogTitleWarning"),, GetLang("LangDialogBtnCancel"), IsWarn:=True) = 1 Then
            RunInThread(
            Sub()
                Hint(GetLang("LangHintBackgroundMusicDeleting"))
                '停止播放音乐
                MusicNAudio = Nothing
                MusicWaitingList = New List(Of String)
                MusicAllList = New List(Of String)
                Thread.Sleep(200)
                '删除文件
                Try
                    DeleteDirectory(Path & "PCL\Musics")
                    Hint(GetLang("LangHintBackgroundMusicDeleted"), HintType.Finish)
                Catch ex As Exception
                    Log(ex, GetLang("LangHintBackgroundMusicDeleteFail"), LogLevel.Msgbox)
                End Try
                Try
                    Directory.CreateDirectory(Path & "PCL\Musics")
                    RunInUi(Sub() MusicRefreshPlay(False))
                Catch ex As Exception
                    Log(ex, GetLang("LangHintBackgroundMusicCreateFolderFail"), LogLevel.Msgbox)
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
                If MyMsgBox(GetLang("LangDialogCustomPageOverwriteConfirmationContent"), GetLang("LangDialogCustomHomePageReplaceTitle"), GetLang("LangDialogBtnContinue"), GetLang("LangDialogBtnCancel"), IsWarn:=True) = 2 Then Return
            End If
            WriteFile(Path & "PCL\Custom.xaml", GetResources("Custom"))
            Hint(GetLang("LangHintCustomPageOverwriteSuccess"), HintType.Finish)
            OpenExplorer(Path & "PCL\Custom.xaml")
        Catch ex As Exception
            Log(ex, GetLang("LangDialogCustomPageOverwriteFail"), LogLevel.Feedback)
        End Try
    End Sub
    Private Sub BtnCustomRefresh_Click() Handles BtnCustomRefresh.Click
        FrmLaunchRight.ForceRefresh()
        Hint(GetLang("LangHintCustomPageRefreshed"), HintType.Finish)
    End Sub
    Private Sub BtnCustomTutorial_Click(sender As Object, e As EventArgs) Handles BtnCustomTutorial.Click
        MyMsgBox(GetLang("LangDialogCustomPageTeachContent"), GetLang("LangDialogCustomPageTeachTitle"))
    End Sub

    '主题
    Private Sub LabLauncherTheme5Unlock_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles LabLauncherTheme5Unlock.MouseLeftButtonUp
        RadioLauncherTheme5Gray.Opacity -= 0.23
        RadioLauncherTheme5.Opacity += 0.23
        AniStart({
            AaOpacity(RadioLauncherTheme5Gray, 1, 1000 * AniSpeed),
            AaOpacity(RadioLauncherTheme5, -1, 1000 * AniSpeed)
        }, "ThemeUnlock")
        If RadioLauncherTheme5Gray.Opacity < 0.08 Then
            ThemeUnlock(5, UnlockHint:=GetLang("LangThemeBackUnlock"))
            AniStop("ThemeUnlock")
            RadioLauncherTheme5.Checked = True
        End If
    End Sub
    Private Sub LabLauncherTheme11Click_MouseLeftButtonUp() Handles LabLauncherTheme11Click.MouseLeftButtonUp, RadioLauncherTheme11.MouseRightButtonUp
        If LabLauncherTheme11Click.Visibility = Visibility.Collapsed OrElse If(LabLauncherTheme11Click.ToolTip, "").ToString.Contains("点击") Then
            If MyMsgBox(GetLang("LangDialogThemeUnlockGameContent"),
                GetLang("LangDialogThemeUnlockGameTitle"), GetLang("LangDialogThemeUnlockGameAccept"), GetLang("LangDialogThemeUnlockGameDeny")) = 1 Then
                MyMsgBox(GetLang("LangDialogThemeUnlockGameFirstClueContent") &
                         "gnp.dorC61\60\20\0202\moc.x1xa.2s\\:sp" & "T".ToLower & "th", GetLang("LangDialogThemeUnlockGameFirstClueTitle")) '防止触发病毒检测规则
            End If
        End If
    End Sub
    Private Sub LabLauncherTheme8Copy_MouseRightButtonUp() Handles LabLauncherTheme8Copy.MouseRightButtonUp, RadioLauncherTheme8.MouseRightButtonUp
        OpenWebsite("https://afdian.com/a/LTCat")
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
            If Not HiddenForceShow AndAlso Setup.Get("UiHiddenPageDownload") AndAlso Setup.Get("UiHiddenPageLink") AndAlso Setup.Get("UiHiddenPageSetup") AndAlso Setup.Get("UiHiddenPageOther") Then
                '顶部栏已被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Collapsed
            Else
                '顶部栏未被全部隐藏
                FrmMain.PanTitleSelect.Visibility = Visibility.Visible
                FrmMain.BtnTitleSelect1.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenPageDownload"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect2.Visibility = Visibility.Collapsed 'If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenPageLink"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect3.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenPageSetup"), Visibility.Collapsed, Visibility.Visible)
                FrmMain.BtnTitleSelect4.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenPageOther"), Visibility.Collapsed, Visibility.Visible)
            End If
            '功能
            FrmLaunchLeft.RefreshButtonsUI()
            If FrmSetupUI IsNot Nothing Then
                FrmSetupUI.CardSwitch.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenFunctionHidden"), Visibility.Collapsed, Visibility.Visible)
            End If
            '设置子页面
            If FrmSetupLeft IsNot Nothing Then
                FrmSetupLeft.ItemLaunch.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenSetupLaunch"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemUI.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenSetupUi"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemLink.Visibility = Visibility.Collapsed 'If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenSetupLink"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupLeft.ItemSystem.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenSetupSystem"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                Dim AvaliableCount As Integer = 0
                If Not Setup.Get("UiHiddenSetupLaunch") Then AvaliableCount += 1
                If Not Setup.Get("UiHiddenSetupUi") Then AvaliableCount += 1
                If Not Setup.Get("UiHiddenSetupLink") Then AvaliableCount += 1
                If Not Setup.Get("UiHiddenSetupSystem") Then AvaliableCount += 1
                FrmSetupLeft.PanItem.Visibility = If(AvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            '更多子页面
            Dim OtherAvaliableCount As Integer = 0
            If Not Setup.Get("UiHiddenOtherHelp") Then OtherAvaliableCount += 1
            If Not Setup.Get("UiHiddenOtherAbout") Then OtherAvaliableCount += 1
            If Not Setup.Get("UiHiddenOtherTest") Then OtherAvaliableCount += 1
            If Not Setup.Get("UiHiddenOtherFeedback") Then OtherAvaliableCount += 1
            If Not Setup.Get("UiHiddenOtherVote") Then OtherAvaliableCount += 1
            If FrmOtherLeft IsNot Nothing Then
                FrmOtherLeft.ItemHelp.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenOtherHelp"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemFeedback.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenOtherFeedback"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemVote.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenOtherVote"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemAbout.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenOtherAbout"), Visibility.Collapsed, Visibility.Visible)
                FrmOtherLeft.ItemTest.Visibility = If(Not HiddenForceShow AndAlso Setup.Get("UiHiddenOtherTest"), Visibility.Collapsed, Visibility.Visible)
                '隐藏左边选择卡
                FrmOtherLeft.PanItem.Visibility = If(OtherAvaliableCount < 2 AndAlso Not HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
            End If
            If OtherAvaliableCount = 1 AndAlso Not HiddenForceShow Then
                If Not Setup.Get("UiHiddenOtherHelp") Then
                    FrmMain.BtnTitleSelect4.Text = GetLang("LangOtherHelp")
                ElseIf Not Setup.Get("UiHiddenOtherAbout") Then
                    FrmMain.BtnTitleSelect4.Text = GetLang("LangOtherAbout")
                Else
                    FrmMain.BtnTitleSelect4.Text = GetLang("LangOtherTool")
                End If
            Else
                FrmMain.BtnTitleSelect4.Text = GetLang("LangOtherMore")
            End If
            '各个页面的入口
            If FrmMain.PageCurrent = FormMain.PageType.VersionSelect Then FrmSelectRight.BtnEmptyDownload_Loaded()
            If FrmMain.PageCurrent = FormMain.PageType.Launch Then FrmLaunchLeft.RefreshButtonsUI()
            If FrmMain.PageCurrent = FormMain.PageType.VersionSetup AndAlso FrmVersionModDisabled IsNot Nothing Then FrmVersionModDisabled.BtnDownload_Loaded()
            '备注
            If FrmSetupUI IsNot Nothing Then FrmSetupUI.CardSwitch.Title = If(HiddenForceShow, GetLang("LangHiddenModeTitleA"), GetLang("LangHiddenModeTitleB"))
        Catch ex As Exception
            Log(ex, GetLang("LangHiddenModeFail"), LogLevel.Feedback)
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
            If Setup.Get("UiHiddenSetupLaunch") AndAlso Setup.Get("UiHiddenSetupUi") AndAlso Setup.Get("UiHiddenSetupSystem") AndAlso Setup.Get("UiHiddenSetupLink") Then
                CheckHiddenSetupLaunch.Checked = False
                CheckHiddenSetupSystem.Checked = False
                CheckHiddenSetupLink.Checked = False
                CheckHiddenSetupUI.Checked = False
            End If
        End If
    End Sub
    Private Sub HiddenSetupSub() Handles CheckHiddenSetupLaunch.Change, CheckHiddenSetupSystem.Change, CheckHiddenSetupLink.Change, CheckHiddenSetupUI.Change
        '设置子页面
        If Setup.Get("UiHiddenSetupLaunch") AndAlso Setup.Get("UiHiddenSetupUi") AndAlso Setup.Get("UiHiddenSetupSystem") AndAlso Setup.Get("UiHiddenSetupLink") Then
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
            If Setup.Get("UiHiddenOtherHelp") AndAlso Setup.Get("UiHiddenOtherAbout") AndAlso Setup.Get("UiHiddenOtherTest") AndAlso
                Setup.Get("UiHiddenOtherVote") AndAlso Setup.Get("UiHiddenOtherFeedback") Then
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
        If Setup.Get("UiHiddenOtherHelp") AndAlso Setup.Get("UiHiddenOtherAbout") AndAlso Setup.Get("UiHiddenOtherTest") Then
            '已被全部隐藏
            CheckHiddenPageOther.Checked = True
        Else
            '未被全部隐藏
            CheckHiddenPageOther.Checked = False
        End If
        '修改无具体内容的项
        If Not user Then Return
        If Setup.Get("UiHiddenOtherHelp") AndAlso Setup.Get("UiHiddenOtherAbout") AndAlso Setup.Get("UiHiddenOtherTest") Then
            CheckHiddenOtherFeedback.Checked = True
            CheckHiddenOtherVote.Checked = True
        End If
    End Sub
    Private Sub HiddenOtherNet(sender As Object, user As Boolean) Handles CheckHiddenOtherFeedback.Change, CheckHiddenOtherVote.Change
        '更多子页面（无具体内容的）
        If Not user Then Return
        If Setup.Get("UiHiddenOtherHelp") AndAlso Setup.Get("UiHiddenOtherAbout") AndAlso Setup.Get("UiHiddenOtherTest") AndAlso
            (Not Setup.Get("UiHiddenOtherFeedback") OrElse Not Setup.Get("UiHiddenOtherVote")) Then
            CheckHiddenOtherAbout.Checked = False
            CheckHiddenOtherTest.Checked = False
            CheckHiddenOtherHelp.Checked = False
        End If
    End Sub

    '警告提示
    Private Sub HiddenHint(sender As Object, user As Boolean) Handles CheckHiddenFunctionHidden.Change, CheckHiddenPageSetup.Change, CheckHiddenSetupUI.Change
        If AniControlEnabled = 0 AndAlso sender.Checked Then Hint(GetLang("LangHintHiddenModeTip"))
    End Sub

#End Region

    '赞助
    Private Sub BtnLauncherDonate_Click(sender As Object, e As EventArgs) Handles BtnLauncherDonate.Click
        OpenWebsite("https://afdian.com/a/LTCat")
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderMusicVolume.GetHintText = Function(v) Math.Ceiling(v * 0.1) & "%"
        SliderLauncherOpacity.GetHintText = Function(v) Math.Round(40 + v * 0.1) & "%"
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
        SliderBackgroundBlur.GetHintText = Function(v) v & " " & GetLang("LangSetupUIBackgroundPicPixel")
    End Sub

End Class
