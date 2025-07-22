Public Class MySkin

    '事件
    Public Event Click(sender As Object, e As MouseButtonEventArgs)

    '皮肤储存
    Private _Address As String
    Public Property Address As String
        Get
            Return _Address
        End Get
        Set(value As String)
            _Address = value
            ToolTip = If(_Address = "", "加载中", "点击更换皮肤（右键查看更多选项）")
        End Set
    End Property
    Public Loader As LoaderTask(Of EqualableList(Of String), String)

    '控件动画
    Private Sub PanSkin_MouseEnter(sender As Object, e As MouseEventArgs) Handles Me.MouseEnter
        AniStart(AaOpacity(ShadowSkin, 0.8 - ShadowSkin.Opacity, 200, 100), "Skin Shadow")
    End Sub
    Private Sub PanSkin_MouseLeave(sender As Object, e As MouseEventArgs) Handles Me.MouseLeave
        AniStart(AaOpacity(ShadowSkin, 0.2 - ShadowSkin.Opacity, 200), "Skin Shadow")
        IsSkinMouseDown = False
        AniStart(AaScaleTransform(Me, 1 - CType(Me.RenderTransform, ScaleTransform).ScaleX, 60,, New AniEaseOutFluent), "Skin Scale")
    End Sub

    '点击
    Private IsSkinMouseDown As Boolean = False
    Private Sub PanSkin_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonDown
        IsSkinMouseDown = True
        AniStart(AaScaleTransform(Me, 0.9 - CType(Me.RenderTransform, ScaleTransform).ScaleX, 60,, New AniEaseOutFluent), "Skin Scale")
    End Sub
    Private Sub PanSkin_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles Me.MouseLeftButtonUp
        AniStart(AaScaleTransform(Me, 1 - CType(Me.RenderTransform, ScaleTransform).ScaleX, 60,, New AniEaseOutFluent), "Skin Scale")
        If IsSkinMouseDown Then
            IsSkinMouseDown = False
            RaiseEvent Click(sender, e)
        End If
    End Sub

    '保存皮肤
    Public Sub BtnSkinSave_Click() Handles BtnSkinSave.Click
        Save(Loader)
    End Sub
    Public Shared Sub Save(Loader As LoaderTask(Of EqualableList(Of String), String))
        Dim Address = Loader.Output
        If Not Loader.State = LoadState.Finished Then
            Hint("皮肤正在获取中，请稍候！", HintType.Critical)
            If Not Loader.State = LoadState.Loading Then Loader.Start()
            Return
        End If
        Try
            Dim FileAddress As String = SelectSaveFile("选取保存皮肤的位置", GetFileNameFromPath(Address), "皮肤图片文件(*.png)|*.png")
            If FileAddress.Contains("\") Then
                File.Delete(FileAddress)
                If Address.StartsWith(PathImage) Then
                    Dim Image As New MyBitmap(Address)
                    Image.Save(FileAddress)
                Else
                    CopyFile(Address, FileAddress)
                End If
                Hint("皮肤保存成功！", HintType.Finish)
            End If
        Catch ex As Exception
            Log(ex, "保存皮肤失败", LogLevel.Hint)
        End Try
    End Sub
    Private Sub BtnSkinSave_Checked(sender As MyMenuItem, e As RoutedEventArgs) Handles BtnSkinSave.Checked
        sender.IsEnabled = Address = ""
    End Sub

    ''' <summary>
    ''' 载入皮肤。
    ''' </summary>
    Public Sub Load()
        Try
            '检查文件存在
            Address = Loader.Output
            If String.IsNullOrEmpty(Address) Then Throw New Exception("皮肤加载器 " & Loader.Name & " 没有输出")
            If Not Address.StartsWith(PathImage) AndAlso Not File.Exists(Address) Then Throw New FileNotFoundException("皮肤文件未找到", Address)
            '加载
            Dim Image As MyBitmap
            Try
                Image = New MyBitmap(Address)
            Catch ex As Exception '#2272
                Log(ex, $"皮肤文件已损坏：{Address}", LogLevel.Hint)
                File.Delete(Address)
                Return
            End Try
            ImgBack.Tag = Address
            '大小检查
            Dim Scale As Integer = Image.Pic.Width / 64
            If Image.Pic.Width < 32 OrElse Image.Pic.Height < 32 Then
                ImgFore.Source = Nothing : ImgBack.Source = Nothing
                Throw New Exception("图片大小不足，长为 " & Image.Pic.Height & "，宽为 " & Image.Pic.Width)
            End If
            '头发层（附加层）
            If Image.Pic.Width >= 64 AndAlso Image.Pic.Height >= 32 Then
                If (Image.Pic.GetPixel(1, 1).A = 0 OrElse '如果图片中有任何透明像素（避免纯色白底）
                    Image.Pic.GetPixel(Image.Pic.Width - 1, Image.Pic.Height - 1).A = 0 OrElse
                    Image.Pic.GetPixel(Image.Pic.Width - 2, Image.Pic.Height / 2 - 2).A = 0) OrElse
                   (Image.Pic.GetPixel(1, 1) <> Image.Pic.GetPixel(Scale * 41, Scale * 9) AndAlso '或是头部颜色和透明区均不一样
                    Image.Pic.GetPixel(Image.Pic.Width - 1, Image.Pic.Height - 1) <> Image.Pic.GetPixel(Scale * 41, Scale * 9) AndAlso
                    Image.Pic.GetPixel(Image.Pic.Width - 2, Image.Pic.Height / 2 - 2) <> Image.Pic.GetPixel(Scale * 41, Scale * 9)) Then
                    ImgFore.Source = Image.Clip(Scale * 40, Scale * 8, Scale * 8, Scale * 8)
                Else
                    ImgFore.Source = Nothing
                End If
            Else
                ImgFore.Source = Nothing
            End If
            '脸层
            ImgBack.Source = Image.Clip(Scale * 8, Scale * 8, Scale * 8, Scale * 8)
            Log("[Skin] 载入头像成功：" & Loader.Name)
        Catch ex As Exception
            Log(ex, "载入头像失败（" & If(Address, "null") & "," & Loader.Name & "）", LogLevel.Hint)
        End Try
    End Sub
    ''' <summary>
    ''' 清空皮肤。
    ''' </summary>
    Public Sub Clear()
        Address = ""
        ImgFore.Source = Nothing
        ImgBack.Source = Nothing
    End Sub

    '刷新缓存
    Public Sub RefreshClick() Handles BtnSkinRefresh.Click
        RefreshCache(Loader)
    End Sub
    ''' <summary>
    ''' 刷新皮肤缓存。
    ''' </summary>
    Public Shared Sub RefreshCache(Optional sender As LoaderTask(Of EqualableList(Of String), String) = Nothing)
        Dim HasLoaderRunning As Boolean = False
        For Each SkinLoader In PageLaunchLeft.SkinLoaders
            If SkinLoader.State = LoadState.Loading Then
                HasLoaderRunning = True : Exit For
            End If
        Next
        If FrmLaunchLeft IsNot Nothing AndAlso HasLoaderRunning Then
            '由于 Abort 不是实时的，暂时不会释放文件，会导致删除报错，故只能取消执行
            Hint("有正在获取中的皮肤，请稍后再试！", HintType.Info)
        Else
            RunInThread(
            Sub()
                Try
                    Hint("正在刷新头像……")
                    '清空缓存
                    Log("[Skin] 正在清空皮肤缓存")
                    If Directory.Exists(PathTemp & "Cache\Skin") Then DeleteDirectory(PathTemp & "Cache\Skin")
                    If Directory.Exists(PathTemp & "Cache\Uuid") Then DeleteDirectory(PathTemp & "Cache\Uuid")
                    IniClearCache(PathTemp & "Cache\Skin\IndexMs.ini")
                    IniClearCache(PathTemp & "Cache\Skin\IndexNide.ini")
                    IniClearCache(PathTemp & "Cache\Skin\IndexAuth.ini")
                    IniClearCache(PathTemp & "Cache\Uuid\Mojang.ini")
                    '刷新控件
                    For Each SkinLoader In If(sender IsNot Nothing, {sender}, {PageLaunchLeft.SkinLegacy, PageLaunchLeft.SkinMs})
                        SkinLoader.WaitForExit(IsForceRestart:=True)
                    Next
                    Hint("已刷新头像！", HintType.Finish)
                Catch ex As Exception
                    Log(ex, "刷新皮肤缓存失败", LogLevel.Msgbox)
                End Try
            End Sub)
        End If
    End Sub
    ''' <summary>
    ''' 在更换正版皮肤后，刷新正版皮肤。
    ''' </summary>
    ''' <param name="SkinAddress">新的正版皮肤完整地址。</param>
    Public Shared Sub ReloadCache(SkinAddress As String)
        RunInThread(
        Sub()
            Try
                '更新缓存
                WriteIni(PathTemp & "Cache\Skin\IndexMs.ini", Setup.Get("CacheMsV2Uuid"), SkinAddress)
                Log(String.Format("[Skin] 已写入皮肤地址缓存 {0} -> {1}", Setup.Get("CacheMsV2Uuid"), SkinAddress))
                '刷新控件
                For Each SkinLoader In {PageLaunchLeft.SkinMs, PageLaunchLeft.SkinLegacy}
                    SkinLoader.WaitForExit(IsForceRestart:=True)
                Next
                '完成提示
                Hint("更改皮肤成功！", HintType.Finish)
            Catch ex As Exception
                Log(ex, "更改正版皮肤后刷新皮肤失败", LogLevel.Feedback)
            End Try
        End Sub)
    End Sub

    '披风
    Public Property HasCape As Boolean
        Get
            Return BtnSkinCape.Visibility = Visibility.Collapsed
        End Get
        Set(value As Boolean)
            If value Then
                BtnSkinCape.Visibility = Visibility.Visible
            Else
                BtnSkinCape.Visibility = Visibility.Collapsed
            End If
        End Set
    End Property
    Private IsChanging As Boolean = False
    Public Sub BtnSkinCape_Click() Handles BtnSkinCape.Click
        '检查条件，获取新披风
        If IsChanging Then
            Hint("正在更改披风中，请稍候！")
            Return
        End If
        If McLoginMsLoader.State = LoadState.Failed Then
            Hint("登录失败，无法更改披风！", HintType.Critical)
            Return
        End If
        Hint("正在获取披风列表，请稍候……")
        IsChanging = True
        '开始实际获取
        RunInNewThread(
        Sub()
            Try
Retry:
                '获取登录信息
                If McLoginMsLoader.State <> LoadState.Finished Then McLoginMsLoader.WaitForExit(PageLoginMsSkin.GetLoginData())
                If McLoginMsLoader.State <> LoadState.Finished Then
                    Hint("登录失败，无法更改披风！", HintType.Critical)
                    Return
                End If
                Dim AccessToken As String = McLoginMsLoader.Output.AccessToken
                Dim Uuid As String = McLoginMsLoader.Output.Uuid
                Dim SkinData As JObject = GetJson(McLoginMsLoader.Output.ProfileJson)
                '获取玩家的所有披风
                Dim SelId As Integer? = Nothing
                RunInUiWait(
                Sub()
                    Try
                        Dim CapeNames As New Dictionary(Of String, String) From {
                            {"Migrator", "迁移者披风"}, {"MapMaker", "Realms 地图制作者披风"}, {"Moderator", "Mojira 管理员披风"},
                            {"Translator-Chinese", "Crowdin 中文翻译者披风"}, {"Translator", "Crowdin 翻译者披风"}, {"Cobalt", "Cobalt 披风"},
                            {"Vanilla", "原版披风"}, {"Minecon2011", "Minecon 2011 参与者披风"}, {"Minecon2012", "Minecon 2012 参与者披风"},
                            {"Minecon2013", "Minecon 2013 参与者披风"}, {"Minecon2015", "Minecon 2015 参与者披风"}, {"Minecon2016", "Minecon 2016 参与者披风"},
                            {"Cherry Blossom", "樱花披风"}, {"15th Anniversary", "15 周年纪念披风"}, {"Purple Heart", "紫色心形披风"},
                            {"Follower's", "追随者披风"}, {"MCC 15th Year", "MCC 15 周年披风"}, {"Minecraft Experience", "村民救援披风"},
                            {"Mojang Office", "Mojang 办公室披风"}, {"Home", "家园披风"}, {"Menace", "入侵披风"}, {"Yearn", "渴望披风"},
                            {"Common", "普通披风"}, {"Pan", "薄煎饼披风"}, {"Founder's", "创始人披风"}
                        }
                        Dim SelectionControl As New List(Of IMyRadio) From {New MyRadioBox With {.Text = "无披风"}}
                        For Each Cape In SkinData("capes")
                            Dim CapeName As String = Cape("alias").ToString
                            If CapeNames.ContainsKey(CapeName) Then CapeName = CapeNames(CapeName)
                            SelectionControl.Add(New MyRadioBox With {.Text = CapeName})
                        Next
                        SelId = MyMsgBoxSelect(SelectionControl, "选择披风", "确定", "取消")
                    Catch ex As Exception
                        Log(ex, "获取玩家皮肤列表失败", LogLevel.Feedback)
                    End Try
                End Sub)
                If SelId Is Nothing Then Return
                '发送请求
                Dim Result As String = NetRequestRetry("https://api.minecraftservices.com/minecraft/profile/capes/active",
                    If(SelId = 0, "DELETE", "PUT"),
                    If(SelId = 0, "", New JObject(New JProperty("capeId", SkinData("capes")(SelId - 1)("id"))).ToString(0)),
                    "application/json", Headers:=New Dictionary(Of String, String) From {{"Authorization", "Bearer " & AccessToken}})
                If Result.Contains("""errorMessage""") Then
                    Hint("更改披风失败：" & GetJson(Result)("errorMessage"), HintType.Critical)
                    Return
                Else
                    Hint("更改披风成功！", HintType.Finish)
                End If
            Catch ex As Exception
                Log(ex, "更改披风失败", LogLevel.Hint)
            Finally
                IsChanging = False
            End Try
        End Sub, "Cape Change")
    End Sub

End Class
