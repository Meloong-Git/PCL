Public Class PageSetupSystem

#Region "语言"
    Private Sub SelectCurrentLanguage()
        For i As Integer = 0 To ComboLanguage.Items.Count - 1
            Dim item As MyComboBoxItem = CType(ComboLanguage.Items(i), MyComboBoxItem)
            If item.Tag.Equals(Lang) Then
                ComboLanguage.SelectedIndex = i
                Exit For
            End If
        Next
    End Sub

    Private Sub RefreshLang() Handles ComboLanguage.SelectionChanged
        If Not IsLoaded Or Not ComboLanguage.IsLoaded Then Exit Sub
        Dim TargetLang As String = CType(ComboLanguage.SelectedItem, MyComboBoxItem).Tag
        If TargetLang.Equals(Lang) Then Exit Sub
        If HasRunningMinecraft OrElse McLaunchLoader.State = LoadState.Loading Then
            Hint(GetLang("LangPageSetupSystemHintCloseGameBeforeChangeLanguage"))
            SelectCurrentLanguage()
            Exit Sub
        End If
        If HasDownloadingTask() Then
            Hint(GetLang("LangPageSetupSystemHintFinishDownloadTaskBeforeChangeLanguage"))
            SelectCurrentLanguage()
            Exit Sub
        End If
        Lang = TargetLang
        Settings.Set("SystemLang", Lang)
        Application.Current.Resources.MergedDictionaries(1) = New ResourceDictionary With {.Source = New Uri("pack://application:,,,/Resources/Language/" & Lang & ".xaml", UriKind.RelativeOrAbsolute)}
        If Lang.Equals("zh-MEME") Then MyMsgBox(GetLang("LangPageSetupSystemDialogContentEntertainment"), IsWarn:=True)
        MyMsgBox(GetLang("LangPageSetupSystemDialogContentLanguageRestart"), ForceWait:=True)
        Process.Start(New ProcessStartInfo(PathExe))
        FormMain.EndProgramForce()
    End Sub

    Private Sub HelpTranslate(sender As Object, e As EventArgs) Handles BtnHelpTranslate.Click
        OpenWebsite("https://weblate.tangge233.top/engage/PCL/")
    End Sub
#End Region

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        If BuildType = BuildTypes.Release Then
            PanDonate.Visibility = Visibility.Collapsed
        Else
            PanDonate.Visibility = Visibility.Visible
            ItemSystemUpdateDownload.Content = GetLang("LangPageSetupSystemSystemLaunchUpdateE")
        End If

        '语言
        SelectCurrentLanguage()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info(GetLang("LangPageSetupSystemLaunchResetSuccess"))
            Hint(GetLang("LangPageSetupSystemLaunchResetSuccess"), HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, GetLang("LangPageSetupSystemLaunchResetFail"), LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return GetLang("LangPageSetupSystemDownloadSpeedUnlimited")
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, GetLang("LangPageSetupSystemDebugAnimSpeedDisable"), (v / 10 + 0.1) & "x")
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox(GetLang("LangPageSetupSystemDownloadSpeedDialogThreadTooMuchContent"), GetLang("LangDialogTitleWarning"), GetLang("LangDialogBtnIC"), IsWarn:=True)
        End If
    End Sub

    '识别码/土豆码替代入口
    Private Sub BtnSystemIdentify_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemIdentify.Click
        PageOtherAbout.CopyIdentify()
    End Sub
    Private Sub BtnSystemUnlock_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemUnlock.Click
        InputPotatoCode(False)
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint(GetLang("LangPageSetupSystemDebugNeedRestart"),, False)
    End Sub

    '自动更新
    Private Sub ComboSystemActivity_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemActivity.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemActivity.SelectedIndex <> 2 Then Return
        If MyMsgBox(GetLang("LangPageSetupSystemLaunchDialogAnnouncementSilentContent"), GetLang("LangDialogTitleWarning"), GetLang("LangPageSetupSystemLaunchDialogAnnouncementBtnConfirm"), GetLang("LangDialogBtnCancel"), IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemActivity.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub ComboSystemUpdate_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemUpdate.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemUpdate.SelectedIndex <> 3 Then Return
        If MyMsgBox(GetLang("LangPageSetupSystemLaunchDialogAnnouncementDisableContent"), GetLang("LangDialogTitleWarning"), GetLang("LangPageSetupSystemLaunchDialogAnnouncementBtnConfirm"), GetLang("LangDialogBtnCancel"), IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemUpdate.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub BtnSystemUpdate_Click(sender As Object, e As EventArgs) Handles BtnSystemUpdate.Click
        UpdateCheckByButton()
    End Sub
    ''' <summary>
    ''' 启动器是否已经是最新版？
    ''' 若返回 Nothing，则代表无更新缓存文件或出错。
    ''' </summary>
    Public Shared Function IsLauncherNewest() As Boolean?
        Try
            '确认服务器公告是否正常
            Dim ServerContent As String = If(FileUtils.TryReadAsString(PathTemp & "Cache\Notice.cfg"), "")
            If ServerContent.Split("|").Count < 3 Then Return Nothing
            '确认是否为最新
            Return ServerContent.Split("|")(If(BuildType = BuildTypes.Release, 2, 1)) <= VersionCode
        Catch ex As Exception
            Logger.Error(ex, GetLang("LangPageSetupSystemSystemLaunchUpdateFail"))
            Return Nothing
        End Try
    End Function

#Region "导出 / 导入设置"

    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Hint(GetLang("LangPageSetupSystemInDev"))
    End Sub
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Hint(GetLang("LangPageSetupSystemInDev"))
    End Sub

#End Region

End Class
