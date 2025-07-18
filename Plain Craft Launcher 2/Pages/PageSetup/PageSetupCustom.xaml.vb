Public Class PageSetupCustom

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageSetupCustom_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()


        AniControlEnabled += 1
        Reload() '#4826，在每次进入页面时都刷新一下
        AniControlEnabled -= 1

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True


    End Sub
    Private Sub Reload()
        Try

            '主页
            Try
                ComboCustomPreset.SelectedIndex = Setup.Get("UiCustomPreset")
            Catch
                Setup.Reset("UiCustomPreset")
            End Try
            CType(FindName("RadioCustomType" & Setup.Load("UiCustomType", ForceReload:=True)), MyRadioBox).Checked = True
            TextCustomNet.Text = Setup.Get("UiCustomNet")

        Catch ex As NullReferenceException
            Log(ex, "主页设置项存在异常，已被自动重置", LogLevel.Msgbox)
            Reset()
        Catch ex As Exception
            Log(ex, "重载主页设置时出错", LogLevel.Feedback)
        End Try
    End Sub

    '初始化
    Public Sub Reset()
        Try
            Setup.Reset("UiCustomType")
            Setup.Reset("UiCustomPreset")
            Setup.Reset("UiCustomNet")

            Log("[Setup] 已初始化主页设置！")
            Hint("已初始化主页设置", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化主页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    'Slider 和 Checkbox 暂时没用到，先留着吧，谁知道呢……
    'Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles Nothing
    '   If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value)
    'End Sub
    Private Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboCustomPreset.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    End Sub
    'Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles Nothing
    '   If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    'End Sub
    Private Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextCustomNet.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub
    Private Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioCustomType0.Check, RadioCustomType1.Check, RadioCustomType2.Check, RadioCustomType3.Check
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag.ToString.Split("/")(0), Val(sender.Tag.ToString.Split("/")(1)))
        UiCustomType(Val(sender.Tag.ToString.Split("/")(1)))
    End Sub
    Private Sub PresetSelectedFromCard(sender As MyTextButton, e As Object) Handles CustomPreset1.Click
        RadioCustomType3.SetChecked(True, True)
        If AniControlEnabled = 0 Then Setup.Set("UiCustomPreset", Val(sender.Tag.ToString.Split("/")(0)))
        ComboCustomPreset.SelectedItem = ComboCustomPreset.Items(Val(sender.Tag.ToString.Split("/")(0)))
    End Sub


    '主页
    Private Sub BtnCustomFile_Click(sender As Object, e As EventArgs) Handles BtnCustomFile.Click
        Try
            If File.Exists(Path & "PCL\Custom.xaml") Then
                If MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn:=True) = 2 Then Return
            End If
            WriteFile(Path & "PCL\Custom.xaml", GetResources("Custom"))
            Hint("教学文件已生成！", HintType.Finish)
            OpenExplorer(Path & "PCL\Custom.xaml")
        Catch ex As Exception
            Log(ex, "生成教学文件失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub BtnCustomRefresh_Click() Handles BtnCustomRefresh.Click
        FrmLaunchRight.ForceRefresh()
        Hint("已刷新主页！", HintType.Finish)
    End Sub
    Private Sub BtnCustomTutorial_Click(sender As Object, e As EventArgs) Handles BtnCustomTutorial.Click
        MyMsgBox("1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。" & vbCrLf &
                 "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" & vbCrLf &
                 "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" & vbCrLf &
                 vbCrLf &
                 "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" & vbCrLf &
                 "直接将主页文件拖进 PCL 窗口也可以快捷加载。", "主页自定义教程")
    End Sub

    '主页
    Private Shared Sub UiCustomType(Value As Integer)
        Select Case Value
            Case 0 '无
                FrmSetupCustom.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustomWarn.Visibility = Visibility.Collapsed
            Case 1 '本地
                FrmSetupCustom.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomLocal.Visibility = Visibility.Visible
                FrmSetupCustom.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustom.Visibility = Visibility.Visible
                FrmSetupCustom.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupCustom.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。"
                FrmSetupCustom.HintCustom.EventType = ""
                FrmSetupCustom.HintCustom.EventData = ""
            Case 2 '联网
                FrmSetupCustom.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomNet.Visibility = Visibility.Visible
                FrmSetupCustom.HintCustom.Visibility = Visibility.Visible
                FrmSetupCustom.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupCustom.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！"
                FrmSetupCustom.HintCustom.EventType = "打开网页"
                FrmSetupCustom.HintCustom.EventData = "https://github.com/Hex-Dragon/PCL2/discussions/2528"
            Case 3 '预设
                FrmSetupCustom.PanCustomPreset.Visibility = Visibility.Visible
                FrmSetupCustom.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustomWarn.Visibility = Visibility.Collapsed
        End Select
        FrmSetupCustom.CardCustom.TriggerForceResize()
    End Sub

End Class
