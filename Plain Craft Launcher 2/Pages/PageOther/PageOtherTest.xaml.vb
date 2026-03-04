Public Class PageOtherTest

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint(GetLang("LangPageOtherTestNoUtility"))
    End Sub
    Public Shared Sub Jrrp()
        Hint(GetLang("LangPageOtherTestNoUtility"))
    End Sub
    Public Shared Sub RubbishClear()
        Hint(GetLang("LangPageOtherTestNoUtility"))
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        If ShowHint Then Hint(GetLang("LangPageOtherTestNoUtility"))
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
    End Sub
    Public Shared Function GetRandomCave() As String
        Return GetLang("LangPageOtherTestNoUtility")
    End Function
    Public Shared Function GetRandomHint() As String
        Return GetLang("LangPageOtherTestNoUtility")
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return GetLang("LangPageOtherTestNoUtility")
    End Function

End Class
