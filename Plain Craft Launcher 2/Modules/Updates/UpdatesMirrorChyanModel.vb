Imports PCL.Core.Helper
Imports System.Text.Json
Public Class UpdatesMirrorChyanModel 'Mirror 酱的更新格式
    Implements IUpdateSource

    Private Const MirrorChyanBaseUrl As String = "https://mirrorchyan.com/api/resources/{cid}/latest?cdk={cdk}&os=win&arch={arch}&channel={channel}"
    Private Const MyCid As String = "PCL2-CE"
    Property SourceName As String = "MirrorChyan" Implements IUpdateSource.SourceName

    Public Function IsAvailable() As Boolean Implements IUpdateSource.IsAvailable
        Return Not String.IsNullOrWhiteSpace(Setup.Get("SystemMirrorChyanKey"))
    End Function
    Public Function GetLatestVersion(channel As UpdateChannel, arch As UpdateArch) As VersionDataModel Implements IUpdateSource.GetLatestVersion
        Dim ret As JObject = NetGetCodeByRequestRetry(GetUrl(channel, arch), IsJson:=True)
            If CType(ret("code"), Integer) <> 0 Then Throw New Exception("Mirror 酱获取数据不成功")
            Dim data = ret("data")
            Dim upd_url = data("url")?.ToString()
            If data IsNot Nothing AndAlso String.IsNullOrWhiteSpace(upd_url) Then Throw New Exception("无效 CDK")
            Return New VersionDataModel() With {
                .Source = SourceName,
                .VersionCode = data("version_number"),
                .VersionName = data("version_name"),
                .SHA256 = data("sha256"),
                .Changelog = data("release_note")}
            
    End Function

    Public Function RefreshCache() As Boolean Implements IUpdateSource.RefreshCache
        Return True
    End Function

    Public Function IsLatest(channel As UpdateChannel, arch As UpdateArch, currentVersion As SemVer, currentVersionCode As Integer) As Boolean Implements IUpdateSource.IsLatest
        Dim latest = GetLatestVersion(channel, arch)
        Return currentVersion >= SemVer.Parse(latest.VersionName)
    End Function

    Public Function GetAnnouncementList() As VersionAnnouncementDataModel Implements IUpdateSource.GetAnnouncementList
        Throw New Exception("Mirror 酱无公告系统")
    End Function

    Public Function GetDownloadLoader(channel As UpdateChannel, arch As UpdateArch, output As String) As List(Of LoaderBase) Implements IUpdateSource.GetDownloadLoader
        Dim loaders As New List(Of LoaderBase)
        loaders.Add(New LoaderTask(Of Integer, List(Of NetFile))("获取下载信息", Sub(load As LoaderTask(Of Integer, List(Of NetFile)))
                                                                               Dim ret As JObject = NetGetCodeByRequestRetry(GetUrl(channel, arch), IsJson:=True)
                                                                               Dim dlUrl = ret("data")("url")?.ToString()
                                                                               If dlUrl Is Nothing Then Throw New Exception("Mirror 酱下载源不可用")
                                                                               load.Output = New List(Of NetFile) From {
                                                                                   New NetFile({dlUrl}, output)
                                                                               }
                                                                           End Sub))
        loaders.Add(New LoaderDownload("下载更新文件", New List(Of NetFile)))
        Return loaders
    End Function

    Private Function GetUrl(channel As UpdateChannel, arch As UpdateArch) As String
        Dim ReqUrl As String = MirrorChyanBaseUrl
        Dim CDKey As String = Setup.Get("SystemMirrorChyanKey")
        ReqUrl = ReqUrl.Replace("{cid}", MyCid)
        ReqUrl = ReqUrl.Replace("{cdk}", CDKey)
        ReqUrl = ReqUrl.Replace("{arch}", arch.ToString())
        ReqUrl = ReqUrl.Replace("{channel}", channel.ToString())
        Return ReqUrl
    End Function
End Class
