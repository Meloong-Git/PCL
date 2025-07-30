Public Class PageOtherAbout

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", VersionDisplayName).Replace("%VERSIONCODE%", VersionCode).Replace("%BRANCH%", VersionBranchCode)
#If DEBUG Then
        BtnDonateDonate.Visibility = Visibility.Collapsed
        BtnDonateOutput.Visibility = Visibility.Visible
#End If

    End Sub

    Private Sub BtnAboutBmclapi_Click(sender As Object, e As EventArgs) Handles BtnAboutBmclapi.Click
        OpenWebsite("https://afdian.com/a/bangbang93")
    End Sub
    Private Sub BtnAboutWiki_Click(sender As Object, e As EventArgs) Handles BtnAboutWiki.Click
        OpenWebsite("https://www.mcmod.cn")
    End Sub
    Public Shared Sub CopyUniqueAddress() Handles BtnDonateCopy.Click
        ClipboardSet(UniqueAddress)
    End Sub
    Private Sub BtnDonateCodeInput_Click() Handles BtnDonateInput.Click
        DonateCodeInput()
    End Sub

#If DEBUG Then
    Private Sub BtnDonateOutput_Click(sender As Object, e As EventArgs) Handles BtnDonateOutput.Click
        DonateCodeGenerate()
    End Sub
#End If

End Class
