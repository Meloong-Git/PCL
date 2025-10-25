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
            ToolTip = If(_Address = "", GetLang("LangMySkinLoading"), GetLang("LangMySkinClickToChange"))
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
            Hint(GetLang("LangMySkinHintLoading"), HintType.Red)
            If Not Loader.State = LoadState.Loading Then Loader.Start()
            Return
        End If
        Try
            Dim FileAddress As String = SelectSaveFile(GetLang("LangMySkinDialogChoseSavePath"), GetFileNameFromPath(Address), "皮肤图片文件(*.png)|*.png")
            If FileAddress.Contains("\") Then
                File.Delete(FileAddress)
                If Address.StartsWith(PathImage) Then
                    Dim Image As New MyBitmap(Address)
                    Image.Save(FileAddress)
                Else
                    CopyFile(Address, FileAddress)
                End If
                Hint(GetLang("LangMySkinHintSaveSuccess"), HintType.Green)
            End If
        Catch ex As Exception
            Log(ex, GetLang("LangMySkinHintSaveFail"), LogLevel.Hint)
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
                Log(ex, GetLang("LangMySkinHintSkinFileCorruption") & Address, LogLevel.Hint)
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
            Hint(GetLang("LangMySkinHintExistSkinFileGetTask"), HintType.Blue)
        Else
            RunInThread(
            Sub()
                Try
                    Hint(GetLang("LangMySkinHintRefreshing"))
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
                    Hint(GetLang("LangMySkinHintRefreshed"), HintType.Green)
                Catch ex As Exception
                    Log(ex, GetLang("LangMySkinHintRefreshFail"), LogLevel.Msgbox)
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
                Hint(GetLang("LangMySkinHintChangeSuccess"), HintType.Green)
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
            Hint(GetLang("LangMySkinHintChanging"))
            Return
        End If
        If McLoginMsLoader.State = LoadState.Failed Then
            Hint(GetLang("LangMySkinHintChangFailByLoginFail"), HintType.Red)
            Return
        End If
        Hint(GetLang("LangMySkinHintGettingCape"))
        IsChanging = True
        '开始实际获取
        RunInNewThread(
        Sub()
            Try
Retry:
                '获取登录信息
                If McLoginMsLoader.State <> LoadState.Finished Then McLoginMsLoader.WaitForExit(PageLoginMsSkin.GetLoginData())
                If McLoginMsLoader.State <> LoadState.Finished Then
                    Hint(GetLang("LangMySkinHintChangFailByLoginFail"), HintType.Red)
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
                            {"Migrator", GetLang("LangMySkinCapeNameMigrator")}, {"MapMaker", GetLang("LangMySkinCapeNameMapMaker")}, {"Moderator", GetLang("LangMySkinCapeNameModerator")},
                            {"Translator-Chinese", GetLang("LangMySkinCapeNameTranslator-Chinese")}, {"Translator", GetLang("LangMySkinCapeNameTranslator")}, {"Cobalt", GetLang("LangMySkinCapeNameCobalt")},
                            {"Vanilla", GetLang("LangMySkinCapeNameVanilla")}, {"Minecon2011", GetLang("LangMySkinCapeNameMinecon2011")}, {"Minecon2012", GetLang("LangMySkinCapeNameMinecon2012")},
                            {"Minecon2013", GetLang("LangMySkinCapeNameMinecon2013")}, {"Minecon2015", GetLang("LangMySkinCapeNameMinecon2015")}, {"Minecon2016", GetLang("LangMySkinCapeNameMinecon2016")},
                            {"Cherry Blossom", GetLang("LangMySkinCapeNameCherryBlossom")}, {"15th Anniversary", GetLang("LangMySkinCapeName15th-Anniversary")}, {"Purple Heart", GetLang("LangMySkinCapeNamePurpleHeart")},
                            {"Follower's", GetLang("LangMySkinCapeNameFollower's")}, {"MCC 15th Year", GetLang("LangMySkinCapeNameMCC15thYear")}, {"Minecraft Experience", GetLang("LangMySkinCapeNameMinecraftExperience")},
                            {"Mojang Office", GetLang("LangMySkinCapeNameMojangOffice")}, {"Waiting", GetLang("LangMySkinCapeNameHome")}, {"Menace", GetLang("LangMySkinCapeNameMenace")}, {"Yearn", GetLang("LangMySkinCapeNameYearn")},
                            {"Common", GetLang("LangMySkinCapeNameCommon")}, {"Pan", GetLang("LangMySkinCapeNamePan")}, {"Founder's", GetLang("LangMySkinCapeNameFounders")}, {"Copper", GetLang("LangMySkinCapeNameCopper")}
                        }
                        Dim SelectionControl As New List(Of IMyRadio) From {New MyRadioBox With {.Text = GetLang("LangMySkinCapeNameNone")}}
                        For Each Cape In SkinData("capes")
                            Dim CapeName As String = Cape("alias").ToString
                            If CapeNames.ContainsKey(CapeName) Then CapeName = CapeNames(CapeName)
                            SelectionControl.Add(New MyRadioBox With {.Text = CapeName})
                        Next
                        SelId = MyMsgBoxSelect(SelectionControl, GetLang("LangMySkinDialogChooseCape"), GetLang("LangDialogBtnOK"), GetLang("LangDialogBtnCancel"))
                    Catch ex As Exception
                        Log(ex, "获取玩家皮肤列表失败", LogLevel.Feedback)
                    End Try
                End Sub)
                If SelId Is Nothing Then Return
                '发送请求
                Dim Result As String = NetRequestByClientRetry("https://api.minecraftservices.com/minecraft/profile/capes/active",
                    If(SelId = 0, HttpMethod.Delete, HttpMethod.Put),
                    Content:=If(SelId = 0, "", New JObject(New JProperty("capeId", SkinData("capes")(SelId - 1)("id"))).ToString(0)),
                    ContentType:="application/json",
                    Headers:={{"Authorization", "Bearer " & AccessToken}})
                If Result.Contains("""errorMessage""") Then
                    Hint(GetLang("LangMySkinHintChangeCapeFail") & ":" & GetJson(Result)("errorMessage").ToString, HintType.Red)
                    Return
                Else
                    Hint(GetLang("LangMySkinHintChangeCapeSuccess"), HintType.Green)
                End If
            Catch ex As Exception
                Log(ex, GetLang("LangMySkinHintChangeCapeFail"), LogLevel.Hint)
            Finally
                IsChanging = False
            End Try
        End Sub, "Cape Change")
    End Sub

End Class
