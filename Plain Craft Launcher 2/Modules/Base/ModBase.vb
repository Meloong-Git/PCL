Imports System.Runtime.CompilerServices
Imports System.Xaml

Public Module ModBase

#Region "声明"

    '下列版本信息由更新器自动修改
    Public Const VersionBaseName As String = "2.13.0.1" '显示用版本名
    Public Const CommitHash As String = "" 'Commit Hash，由 GitHub Workflow 自动替换
#If RELEASE Then
    Public Const VersionCode As Integer = 406 '正式版
#Else
    Public Const VersionCode As Integer = 407 '快照版
#End If

    '版本信息
    Public Const VersionDisplay As String = BuildTypeDisplay & " " & VersionBaseName
#If DEBUG Then
    Public Const BuildTypeDisplay As String = "开发版"
    Public Const BuildType As BuildTypes = BuildTypes.Debug
#ElseIf RELEASE Then
    Public Const BuildTypeDisplay As String = "正式版"
    Public Const BuildType As BuildTypes = BuildTypes.Release
#Else
    Public Const BuildTypeDisplay As String = "快照版"
    Public Const BuildType As BuildTypes = BuildTypes.Snapshot
#End If
    Public Enum BuildTypes
        Debug = 100
        Release = 50
        Snapshot = 0
    End Enum

    ''' <summary>
    ''' 主窗口句柄。
    ''' </summary>
    Public Handle As IntPtr
    ''' <summary>
    ''' 包含程序名的完整路径。
    ''' </summary>
    Public PathExe As String = Paths.Base & AppDomain.CurrentDomain.SetupInformation.ApplicationName
    ''' <summary>
    ''' 程序内嵌图片文件夹路径，以 / 结尾。
    ''' </summary>
    Public PathImage As String = "pack://application:,,,/Plain Craft Launcher 2;component/Images/"
    ''' <summary>
    ''' 程序的缓存文件夹路径，以 \ 结尾。
    ''' </summary>
    Public PathTemp As String = If(Settings.Get(Of String)("SystemSystemCache") = "", Path.GetTempPath() & "PCL\", Settings.Get(Of String)("SystemSystemCache")).ToString.Replace("/", "\").TrimEnd("\") & "\"
    ''' <summary>
    ''' 当前程序的语言。
    ''' </summary>
    Public Lang As String = "zh_CN"
    ''' <summary>
    ''' 程序的打开计时。
    ''' </summary>
    Public ApplicationStartTick As Long = GetTimeMs()
    ''' <summary>
    ''' 程序打开时的时间。
    ''' </summary>
    Public ApplicationOpenTime As Date = Date.Now
    ''' <summary>
    ''' 识别码。
    ''' </summary>
    Public Identify As String = GetIdentify()
    ''' <summary>
    ''' 程序是否正在结束。
    ''' </summary>
    Public IsProgramEnding As Boolean = False
    ''' <summary>
    ''' 是否使用 GBK 编码。
    ''' </summary>
    Public IsGBKEncoding As Boolean = Encoding.Default.CodePage = 936
    ''' <summary>
    ''' 系统盘盘符，以 \ 结尾。例如 “C:\”。
    ''' </summary>
    Public OsDrive As String = Environment.GetLogicalDrives().Where(Function(p) Directory.Exists(p)).First.Upper.First & ":\" '#3799

    Public ReadOnly Property ModeDebug As Boolean
        Get
            Return Settings.Get(Of Boolean)("SystemDebugMode")
        End Get
    End Property

#End Region

#Region "矢量图标"

    Public Class Logo
        ''' <summary>
        ''' 图标按钮，心（空心），1.1x
        ''' </summary>
        Public Const IconButtonLikeLine As String = "M512 896a42.666667 42.666667 0 0 1-30.293333-12.373333l-331.52-331.946667a224.426667 224.426667 0 0 1 0-315.733333 223.573333 223.573333 0 0 1 315.733333 0L512 282.026667l46.08-46.08a223.573333 223.573333 0 0 1 315.733333 0 224.426667 224.426667 0 0 1 0 315.733333l-331.52 331.946667A42.666667 42.666667 0 0 1 512 896zM308.053333 256a136.533333 136.533333 0 0 0-97.28 40.106667 138.24 138.24 0 0 0 0 194.986666L512 792.746667l301.226667-301.653334a138.24 138.24 0 0 0 0-194.986666 141.653333 141.653333 0 0 0-194.56 0l-76.373334 76.8a42.666667 42.666667 0 0 1-60.586666 0L405.333333 296.106667A136.533333 136.533333 0 0 0 308.053333 256z"
        ''' <summary>
        ''' 图标按钮，心（实心），1.1x
        ''' </summary>
        Public Const IconButtonLikeFill As String = "M700.856 155.543c-74.769 0-144.295 72.696-190.046 127.26-45.737-54.576-115.247-127.26-190.056-127.26-134.79 0-244.443 105.78-244.443 235.799 0 77.57 39.278 131.988 70.845 175.713C238.908 694.053 469.62 852.094 479.39 858.757c9.41 6.414 20.424 9.629 31.401 9.629 11.006 0 21.998-3.215 31.398-9.63 9.782-6.662 240.514-164.703 332.238-291.701 31.587-43.724 70.874-98.143 70.874-175.713-0.001-130.02-109.656-235.8-244.445-235.8z m0 0"
        ''' <summary>
        ''' 图标按钮，垃圾桶，1.1x
        ''' </summary>
        Public Const IconButtonDelete As String = "M520.192 0C408.43 0 317.44 82.87 313.563 186.734H52.736c-29.038 0-52.663 21.943-52.663 49.079s23.625 49.152 52.663 49.152h58.075v550.473c0 103.35 75.118 187.757 167.717 187.757h472.43c92.599 0 167.716-83.894 167.716-187.757V285.477h52.59c29.038 0 52.59-21.943 52.663-49.08-0.073-27.135-23.625-49.151-52.663-49.151H726.235C723.237 83.017 631.955 0 520.192 0zM404.846 177.957c3.803-50.03 50.176-89.015 107.447-89.015 57.197 0 103.57 38.985 106.788 89.015H404.92zM284.379 933.669c-33.353 0-69.997-39.351-69.997-95.525v-549.01H833.39v549.522c0 56.247-36.645 95.525-69.998 95.525H284.379v-0.512z M357.23 800.695a48.274 48.274 0 0 0 47.616-49.006V471.7a48.274 48.274 0 0 0-47.543-49.08 48.274 48.274 0 0 0-47.69 49.006V751.69c0 27.282 20.846 49.006 47.617 49.006z m166.62 0a48.274 48.274 0 0 0 47.688-49.006V471.7a48.274 48.274 0 0 0-47.689-49.08 48.274 48.274 0 0 0-47.543 49.006V751.69c0 27.282 21.431 49.006 47.543 49.006z m142.92 0a48.274 48.274 0 0 0 47.543-49.006V471.7a48.274 48.274 0 0 0-47.543-49.08 48.274 48.274 0 0 0-47.616 49.006V751.69c0 27.282 20.773 49.006 47.543 49.006z"
        ''' <summary>
        ''' 图标按钮，禁止，1x
        ''' </summary>
        Public Const IconButtonStop As String = "M508 990.4c-261.6 0-474.4-212-474.4-474.4S246.4 41.6 508 41.6s474.4 212 474.4 474.4S769.6 990.4 508 990.4zM508 136.8c-209.6 0-379.2 169.6-379.2 379.2 0 209.6 169.6 379.2 379.2 379.2s379.2-169.6 379.2-379.2C887.2 306.4 717.6 136.8 508 136.8zM697.6 563.2 318.4 563.2c-26.4 0-47.2-21.6-47.2-47.2 0-26.4 21.6-47.2 47.2-47.2l379.2 0c26.4 0 47.2 21.6 47.2 47.2C744.8 542.4 724 563.2 697.6 563.2z"
        ''' <summary>
        ''' 图标按钮，勾选，1x
        ''' </summary>
        Public Const IconButtonCheck As String = "M512 0a512 512 0 1 0 512 512A512 512 0 0 0 512 0z m0 921.6a409.6 409.6 0 1 1 409.6-409.6 409.6 409.6 0 0 1-409.6 409.6z M716.8 339.968l-256 253.44L328.192 460.8A51.2 51.2 0 0 0 256 532.992l168.448 168.96a51.2 51.2 0 0 0 72.704 0l289.28-289.792A51.2 51.2 0 0 0 716.8 339.968z"
        ''' <summary>
        ''' 图标按钮，笔，1x
        ''' </summary>
        Public Const IconButtonEdit As String = "M732.64 64.32C688.576 21.216 613.696 21.216 569.6 64.32L120.128 499.52c-17.6 12.896-26.432 30.144-30.848 51.68L32 870.048c0 25.856 8.8 56 26.432 73.248 17.632 17.216 17.632 48.704 88.64 48.704h13.248l326.08-56c22.016-4.32 39.68-12.928 52.864-30.176l449.472-435.2c22.048-21.536 35.264-47.36 35.264-77.536 0-30.176-13.216-56-35.264-77.568l-256.096-251.2zM139.712 903.776l56-326.912 311.04-295.136 267.104 269.44-310.976 295.168-323.168 57.44zM844.576 467.84l-273.984-260.672 61.856-59.84c8.832-8.512 26.528-8.512 39.776 0l234.24 226.496c4.384 4.288 8.832 12.8 8.832 17.088s-4.416 8.544-8.864 12.8l-61.856 64.128z"
        ''' <summary>
        ''' 图标按钮，齿轮，1.1x
        ''' </summary>
        Public Const IconButtonSetup As String = "M651.946667 1001.813333c-22.186667 0-42.666667-10.24-61.44-27.306666-23.893333-23.893333-49.493333-35.84-75.093334-35.84-29.013333 0-56.32 11.946667-73.386666 30.72v3.413333c-17.066667 17.066667-42.666667 27.306667-66.56 27.306667h-6.826667c-6.826667 0-11.946667-1.706667-15.36-1.706667l-6.826667-1.706667c-64.853333-20.48-121.173333-54.613333-168.96-98.986666-29.013333-23.893333-37.546667-63.146667-25.6-95.573334 8.533333-23.893333 5.12-51.2-10.24-75.093333-15.36-27.306667-34.133333-40.96-59.733333-47.786667h-1.706667l-5.12-1.706666c-35.84-8.533333-61.44-34.133333-66.56-69.973334C1.706667 575.146667 0 537.6 0 512c0-32.426667 3.413333-63.146667 8.533333-93.866667v-6.826666l3.413334-8.533334c10.24-23.893333 23.893333-40.96 44.373333-51.2 5.12-3.413333 11.946667-6.826667 20.48-8.533333 27.306667-8.533333 51.2-25.6 63.146667-44.373333 13.653333-23.893333 17.066667-52.906667 10.24-81.92-11.946667-34.133333 0-71.68 30.72-93.866667 44.373333-37.546667 97.28-68.266667 158.72-93.866667l3.413333-1.706666c44.373333-13.653333 75.093333 3.413333 92.16 20.48 23.893333 23.893333 49.493333 35.84 75.093333 35.84 30.72 0 56.32-10.24 71.68-30.72l3.413334-3.413334c27.306667-27.306667 63.146667-35.84 93.866666-22.186666 63.146667 22.186667 117.76 54.613333 165.546667 97.28 29.013333 23.893333 37.546667 63.146667 25.6 95.573333-8.533333 23.893333-5.12 51.2 10.24 75.093333 15.36 27.306667 34.133333 40.96 59.733333 47.786667h1.706667l5.12 1.706667c35.84 8.533333 61.44 34.133333 66.56 71.68 6.826667 30.72 10.24 63.146667 11.946667 93.866666v3.413334c0 32.426667-3.413333 63.146667-8.533334 93.866666v6.826667l-3.413333 8.533333c-10.24 23.893333-23.893333 40.96-44.373333 51.2-5.12 3.413333-11.946667 6.826667-20.48 8.533334-27.306667 8.533333-51.2 25.6-63.146667 46.08-13.653333 23.893333-17.066667 52.906667-10.24 81.92 11.946667 35.84-1.706667 75.093333-30.72 95.573333-44.373333 35.84-95.573333 66.56-157.013333 92.16-15.36 3.413333-27.306667 3.413333-35.84 3.413333z m3.413333-83.626666z m1.706667 0zM517.12 853.333333c47.786667 0 93.866667 20.48 134.826667 59.733334 1.706667 1.706667 3.413333 1.706667 3.413333 3.413333 52.906667-22.186667 97.28-49.493333 136.533333-80.213333l1.706667-1.706667v-3.413333c-13.653333-52.906667-8.533333-104.106667 17.066667-148.48 23.893333-39.253333 64.853333-69.973333 114.346666-85.333334 1.706667 0 3.413333-1.706667 6.826667-6.826666 5.12-25.6 8.533333-51.2 8.533333-78.506667-1.706667-29.013333-3.413333-56.32-10.24-81.92v-5.12h-1.706666c-51.2-11.946667-90.453333-39.253333-119.466667-87.04-27.306667-44.373333-34.133333-100.693333-17.066667-148.48l-1.706666-1.706667h-3.413334c-39.253333-35.84-85.333333-63.146667-136.533333-80.213333H648.533333s-1.706667 1.706667-3.413333 1.706667c-32.426667 39.253333-80.213333 59.733333-136.533333 59.733333-47.786667 0-93.866667-20.48-134.826667-59.733333l-1.706667-1.706667h-1.706666c-54.613333 22.186667-98.986667 49.493333-136.533334 80.213333l-1.706666 1.706667v3.413333c13.653333 52.906667 8.533333 104.106667-17.066667 148.48-23.893333 39.253333-64.853333 69.973333-114.346667 85.333334-1.706667 0-3.413333 1.706667-6.826666 6.826666-6.826667 25.6-8.533333 51.2-8.533334 78.506667 0 30.72 3.413333 58.026667 6.826667 76.8l1.706667 5.12h1.706666c51.2 11.946667 90.453333 39.253333 119.466667 87.04 27.306667 44.373333 34.133333 100.693333 17.066667 148.48l1.706666 1.706667 1.706667 1.706666c37.546667 35.84 83.626667 63.146667 134.826667 80.213334 1.706667 0 3.413333 0 3.413333 1.706666h1.706667s1.706667 0 5.12-1.706666c34.133333-37.546667 81.92-59.733333 136.533333-59.733334z m-6.826667-146.773333c-110.933333 0-199.68-85.333333-199.68-196.266667 0-109.226667 87.04-196.266667 199.68-196.266666s199.68 85.333333 199.68 196.266666c-1.706667 109.226667-88.746667 196.266667-199.68 196.266667z m0-307.2c-63.146667 0-114.346667 49.493333-114.346666 110.933333 0 63.146667 49.493333 110.933333 114.346666 110.933334 30.72 0 59.733333-11.946667 80.213334-32.426667 20.48-20.48 32.426667-49.493333 32.426666-78.506667 0-63.146667-49.493333-110.933333-112.64-110.933333z"
        ''' <summary>
        ''' 图标按钮，重置，0.9x
        ''' </summary>
        Public Const IconButtonReset As String = "M667.6817627 313.65283203l-45.28564454 55.76660156L858.06933594 391.27124023 787.61950684 165.93066406l-56.01379395 69.01611328A354.47387695 354.47387695 0 0 0 520.89892578 165.93066406C324.87536621 165.93066406 165.93066406 324.43041992 165.93066406 519.91015625c0 195.52917481 158.94470215 353.97949219 354.96826172 353.97949219a355.06713867 355.06713867 0 0 0 331.73217774-227.66418458 50.52612305 50.52612305 0 0 0-29.21813966-65.25878905 50.77331543 50.77331543 0 0 0-65.50598144 29.16870117A253.61938477 253.61938477 0 0 1 520.94836426 772.78796387c-140.05920411 0-253.61938477-113.21411133-253.61938477-252.87780762 0-139.61425781 113.56018067-252.82836914 253.61938477-252.82836914 53.59130859 0 104.46350098 16.61132813 146.73339843 46.57104492"
        ''' <summary>
        ''' 图标按钮，刷新，0.85x
        ''' </summary>
        Public Const IconButtonRefresh As String = "M512.0 838.3c-80.2 0-153.4-29.3-210.2-77.4l75.5-75.5c11.5-11.5 25.8-22.0 25.8-37.0a27.2 27.2 0 0 0-27.1-27.1H104.0c-27.1 0-27.1 23.9-27.1 27.1v271.9a27.1 27.1 0 0 0 27.1 27.1c15.0 0 27.8-16.6 42.5-31.2l77.9-77.9c76.6 67.7 177.1 108.9 287.4 108.9 221.7 0 404.5-166.0 431.2-380.6h-109.8c-25.9 154.2-159.7 271.9-321.3 271.9zM919.9 76.6c-15.0 0-27.8 16.6-42.5 31.3L799.5 185.8c-76.5-67.7-177.1-108.9-287.4-108.9-221.8 0-404.5 166.1-431.3 380.6H190.6c25.9-154.2 159.7-271.9 321.4-271.9 80.2 0 153.4 29.3 210.1 77.4l-75.5 75.5c-11.6 11.5-25.8 22.0-25.8 37.1a27.2 27.2 0 0 0 27.1 27.1h271.9c27.1 0 27.1-23.9 27.1-27.1V103.8a27.1 27.1 0 0 0-27.1-27.1z"
        ''' <summary>
        ''' 图标按钮，软盘，1x
        ''' </summary>
        Public Const IconButtonSave As String = "M819.392 0L1024 202.752v652.16a168.96 168.96 0 0 1-168.832 168.768h-104.192a47.296 47.296 0 0 1-10.752 0H283.776a47.232 47.232 0 0 1-10.752 0H168.832A168.96 168.96 0 0 1 0 854.912V168.768A168.96 168.96 0 0 1 168.832 0h650.56z m110.208 854.912V242.112l-149.12-147.776H168.896c-41.088 0-74.432 33.408-74.432 74.432v686.144c0 41.024 33.344 74.432 74.432 74.432h62.4v-190.528c0-33.408 27.136-60.544 60.544-60.544h440.448c33.408 0 60.544 27.136 60.544 60.544v190.528h62.4c41.088 0 74.432-33.408 74.432-74.432z m-604.032 74.432h372.864v-156.736H325.568v156.736z m403.52-596.48a47.168 47.168 0 1 1 0 94.336H287.872a47.168 47.168 0 1 1 0-94.336h441.216z m0-153.728a47.168 47.168 0 1 1 0 94.4H287.872a47.168 47.168 0 1 1 0-94.4h441.216z"
        ''' <summary>
        ''' 图标按钮，信息，1.05x
        ''' </summary>
        Public Const IconButtonInfo As String = "M512 917.333333c223.861333 0 405.333333-181.472 405.333333-405.333333S735.861333 106.666667 512 106.666667 106.666667 288.138667 106.666667 512s181.472 405.333333 405.333333 405.333333z m0 106.666667C229.226667 1024 0 794.773333 0 512S229.226667 0 512 0s512 229.226667 512 512-229.226667 512-512 512z m-32-597.333333h64a21.333333 21.333333 0 0 1 21.333333 21.333333v320a21.333333 21.333333 0 0 1-21.333333 21.333333h-64a21.333333 21.333333 0 0 1-21.333333-21.333333V448a21.333333 21.333333 0 0 1 21.333333-21.333333z m0-192h64a21.333333 21.333333 0 0 1 21.333333 21.333333v64a21.333333 21.333333 0 0 1-21.333333 21.333333h-64a21.333333 21.333333 0 0 1-21.333333-21.333333v-64a21.333333 21.333333 0 0 1 21.333333-21.333333z"
        ''' <summary>
        ''' 图标按钮，列表，1x
        ''' </summary>
        Public Const IconButtonList As String = "M384 128h640v128H384zM160 192m-96 0a96 96 0 1 0 192 0 96 96 0 1 0-192 0ZM384 448h640v128H384zM160 512m-96 0a96 96 0 1 0 192 0 96 96 0 1 0-192 0ZM384 768h640v128H384zM160 832m-96 0a96 96 0 1 0 192 0 96 96 0 1 0-192 0Z"
        ''' <summary>
        ''' 图标按钮，文件夹，1.1x
        ''' </summary>
        Public Const IconButtonOpen As String = "M889.018182 418.909091H884.363636V316.509091a93.090909 93.090909 0 0 0-99.607272-89.832727h-302.545455l-93.090909-76.334546A46.545455 46.545455 0 0 0 358.865455 139.636364H146.152727A93.090909 93.090909 0 0 0 46.545455 229.469091V837.818182a46.545455 46.545455 0 0 0 46.545454 46.545454 46.545455 46.545455 0 0 0 16.756364-3.258181 109.381818 109.381818 0 0 0 25.134545 3.258181h586.472727a85.178182 85.178182 0 0 0 87.04-63.301818l163.374546-302.545454a46.545455 46.545455 0 0 0 5.585454-21.876364A82.385455 82.385455 0 0 0 889.018182 418.909091z m-744.727273-186.181818h198.283636l93.09091 76.334545a46.545455 46.545455 0 0 0 29.323636 10.705455h319.301818a12.101818 12.101818 0 0 1 6.516364 0V418.909091H302.545455a85.178182 85.178182 0 0 0-87.04 63.301818L139.636364 622.778182V232.727273a19.549091 19.549091 0 0 1 6.516363 0z m578.094546 552.029091a27.461818 27.461818 0 0 0-2.792728 6.516363H154.530909l147.083636-272.290909a27.461818 27.461818 0 0 0 2.792728-6.981818h565.061818z"
        ''' <summary>
        ''' 图标按钮，上箭头，0.95x
        ''' </summary>
        Public Const IconButtonArrowUp As String = "M554 333V853h-85V333l-228 228-60-60L512 170l331 331-60 60L554 333z"
        ''' <summary>
        ''' 图标按钮，下箭头，0.95x
        ''' </summary>
        Public Const IconButtonArrowDown As String = "M554 691V171h-85V691L241 463l-60 60L512 854l331-331-60-60L554 691z"
        ''' <summary>
        ''' 图标按钮，名片，1.1x
        ''' </summary>
        Public Const IconButtonCard As String = "M834.5 684.1c-31.2-70.4-98.9-120.9-179.1-127.3 63.5-8.5 112.6-63 112.6-128.8 0-71.8-58.2-130-130-130s-130 58.2-130 130c0 65.9 49 120.3 112.6 128.8-80.2 6.4-148 57-179.1 127.3-8.7 19.7 6 42 27.6 42 12.1 0 22.7-7.5 27.7-18.5 24.3-53.9 78.5-91.5 141.3-91.5s117 37.6 141.3 91.5c5 11.1 15.6 18.5 27.7 18.5 21.4 0 36.1-22.3 27.4-42zM567.9 427.9c0-38.6 31.4-70 70-70s70 31.4 70 70-31.4 70-70 70-70-31.4-70-70zM460.3 347.9H216.9c-16.6 0-30 13.4-30 30s13.4 30 30 30h243.3c16.6 0 30-13.4 30-30 0.1-16.5-13.4-30-29.9-30zM367.4 459.6H216.9c-16.6 0-30 13.4-30 30s13.4 30 30 30h150.4c16.6 0 30-13.4 30-30 0.1-16.6-13.4-30-29.9-30zM297.4 571.2H217c-16.6 0-30 13.4-30 30s13.4 30 30 30h80.4c16.6 0 30-13.4 30-30 0-16.5-13.5-30-30-30zM900 236v552H124V236h776m0-60H124c-33.1 0-60 26.9-60 60v552c0 33.1 26.9 60 60 60h776c33.1 0 60-26.9 60-60V236c0-33.1-26.9-60-60-60z"
        ''' <summary>
        ''' 图标按钮，×，0.85x
        ''' </summary>
        Public Const IconButtonCross As String = "F1 M 26.9166,22.1667L 37.9999,33.25L 49.0832,22.1668L 53.8332,26.9168L 42.7499,38L 53.8332,49.0834L 49.0833,53.8334L 37.9999,42.75L 26.9166,53.8334L 22.1666,49.0833L 33.25,38L 22.1667,26.9167L 26.9166,22.1667 Z"
        ''' <summary>
        ''' 图标按钮，Mojang，1.1x
        ''' </summary>
        Public Const IconButtonMojang As String = "M9.183,18.967c-0.109-2.239,1.336-5.119,3.92-4.96c3.657-0.027,7.319-0.044,10.977,0.005	c3.712,0.214,6.596,2.759,9.652,4.533c0.181-1.697-0.197-3.717,1.237-4.982c1.899,2.091,3.143,4.894,5.677,6.334	c1.577,0.805,2.973-0.668,4.221-1.44c1.55,2.305,2.108,5.075,2.622,7.752c0.657,3.438,0.947,6.925,1.111,10.413	c-1.734-0.733-3.355-1.708-4.971-2.671c-1.396-4.933-5.349-8.656-9.723-11.059c-3.46-1.817-7.752-2.185-11.338-0.52	c-3.761,1.593-6.285,5.666-5.891,9.75c0.121,4.577,3.17,8.765,7.391,10.456c7.746,3.285,16.407,2.234,24.521,1.243	c0.454,2.315-1.511,4.527-3.695,4.889c-10.577,0.038-21.148-0.011-31.725,0.022c-2.217,0.279-4.079-1.879-3.964-4.008	C9.167,36.141,9.216,27.556,9.183,18.967z M40.114,8.09c0.294-0.801,0.872-1.417,1.542-1.924c1.379,2.589,2.742,5.482,2.311,8.491	c-0.332,2.54-4.42,2.507-5.052,0.163C38.349,12.527,39.209,10.178,40.114,8.09z M0,53 l0.1,0 z"
        ''' <summary>
        ''' 图标按钮，用户，0.95x
        ''' </summary>
        Public Const IconButtonUser As String = "M660.338 528.065c63.61-46.825 105.131-121.964 105.131-206.83 0-141.7-115.29-256.987-256.997-256.987-141.706 0-256.998 115.288-256.998 256.987 0 85.901 42.52 161.887 107.456 208.562-152.1 59.92-260.185 207.961-260.185 381.077 0 21.276 17.253 38.53 38.53 38.53 21.278 0 38.53-17.254 38.53-38.53 0-183.426 149.232-332.671 332.667-332.671 1.589 0 3.113-0.207 4.694-0.244 0.8 0.056 1.553 0.244 2.362 0.244 183.434 0 332.664 149.245 332.664 332.671 0 21.276 17.255 38.53 38.533 38.53 21.277 0 38.53-17.254 38.53-38.53 0-174.885-110.354-324.13-264.917-382.809z m-331.803-206.83c0-99.22 80.72-179.927 179.935-179.927s179.937 80.708 179.937 179.927c0 99.203-80.721 179.91-179.937 179.91s-179.935-80.708-179.935-179.91z"
        ''' <summary>
        ''' 图标按钮，盾牌，1x
        ''' </summary>
        Public Const IconButtonShield As String = "M511.488256 95.184408c35.310345 22.516742 95.184408 55.78011 167.34033 84.437781 75.738131 29.681159 148.405797 40.93953 191.392304 45.033483v353.615193c0 73.691154-50.662669 164.781609-136.123938 244.101949C649.65917 901.181409 558.568716 942.12094 512 942.12094c-46.568716 0-137.65917-40.93953-222.096952-119.748126C204.441779 742.54073 153.77911 651.450275 153.77911 577.247376v-353.103448c42.474763-4.093953 116.165917-15.352324 191.904048-45.545227 75.226387-30.192904 133.565217-63.456272 165.805098-83.414293M512 0c-4.093953 0-8.187906 1.535232-11.258371 3.582209l-14.84058 10.234882c-1.023488 0.511744-67.550225 47.592204-170.410794 88.531735-100.813593 39.916042-198.556722 41.963018-199.58021 41.963018l-25.075462 0.511744c-10.746627 0.511744-18.934533 8.187906-18.934533 18.422789v414.000999c0 216.97951 286.064968 446.24088 440.09995 446.24088s440.09995-229.261369 440.09995-445.729136V163.758121c0-10.234883-8.69965-18.422789-18.934533-18.422789l-24.563718-0.511744c-1.023488 0-98.766617-2.046977-199.58021-41.963018-103.372314-40.93953-170.410795-88.01999-170.922538-88.531734L523.258371 3.582209c-3.070465-2.558721-7.164418-3.582209-11.258371-3.582209z M743.308346 410.930535l-260.477761 260.477761c-15.864068 15.864068-41.963018 15.864068-57.827087 0l-144.823588-144.823588c-15.864068-15.864068-15.864068-41.963018 0-57.827087 8.187906-8.187906 18.422789-11.770115 29.169415-11.770115 10.234883 0 20.981509 4.093953 29.169416 11.770115l115.654173 115.654173L685.993003 352.591704c15.864068-15.864068 41.963018-15.864068 57.827087 0 15.352324 16.375812 15.352324 42.474763-0.511744 58.338831z"
        ''' <summary>
        ''' 图标按钮，离线，0.85x
        ''' </summary>
        Public Const IconButtonOffline As String = "M533.293176 788.841412a60.235294 60.235294 0 1 1 85.202824 85.202823l-42.616471 42.586353c-129.355294 129.385412-339.124706 129.385412-468.510117 0-129.385412-129.385412-129.385412-339.124706 0-468.510117l42.586353-42.616471a60.235294 60.235294 0 1 1 85.202823 85.202824l-42.61647 42.586352a210.823529 210.823529 0 1 0 298.164706 298.164706l42.586352-42.61647z m255.548236-255.548236l42.61647-42.586352a210.823529 210.823529 0 1 0-298.164706-298.164706l-42.586352 42.61647a60.235294 60.235294 0 1 1-85.202824-85.202823l42.616471-42.586353c129.355294-129.385412 339.124706-129.385412 468.510117 0 129.385412 129.385412 129.385412 339.124706 0 468.510117l-42.586353 42.616471a60.235294 60.235294 0 1 1-85.202823-85.202824zM192.542118 192.542118a60.235294 60.235294 0 0 1 85.202823 0l553.712941 553.712941a60.235294 60.235294 0 0 1-85.202823 85.202823L192.542118 277.744941a60.235294 60.235294 0 0 1 0-85.202823z"
        ''' <summary>
        ''' 图标，服务端，1x
        ''' </summary>
        Public Const IconButtonServer As String = "M224 160a64 64 0 0 0-64 64v576a64 64 0 0 0 64 64h576a64 64 0 0 0 64-64V224a64 64 0 0 0-64-64H224z m0 384h576v256H224v-256z m192 96v64h320v-64H416z m-128 0v64h64v-64H288zM224 224h576v256H224V224z m192 96v64h320v-64H416z m-128 0v64h64v-64H288z"
        ''' <summary>
        ''' 图标，音符，1x
        ''' </summary>
        Public Const IconMusic As String = "M348.293565 716.53287V254.797913c0-41.672348 28.004174-78.358261 68.919652-90.37913L815.994435 40.826435c62.775652-18.610087 125.907478 26.579478 125.907478 89.933913v539.158261c8.013913 42.25113-8.94887 89.177043-47.014956 127.109565a232.848696 232.848696 0 0 1-170.785392 65.758609c-61.885217-2.938435-111.081739-33.435826-129.113043-80.050087-18.031304-46.614261-2.137043-102.177391 41.672348-145.853218a232.848696 232.848696 0 0 1 170.785391-65.80313c21.014261 1.024 40.514783 5.164522 57.878261 12.065391V233.338435c0-12.109913-10.551652-20.034783-20.569044-20.034783a24.620522 24.620522 0 0 0-5.787826 0.934957L439.785739 338.18713a19.545043 19.545043 0 0 0-14.825739 19.144348v438.984348H423.846957c11.53113 43.987478-5.164522 94.208-45.412174 134.322087a232.848696 232.848696 0 0 1-170.785392 65.758609c-61.885217-2.938435-111.081739-33.435826-129.113043-80.050087-18.031304-46.614261-2.137043-102.177391 41.672348-145.853218a232.848696 232.848696 0 0 1 170.785391-65.80313c20.791652 1.024 40.069565 5.075478 57.299478 11.842783z"
        ''' <summary>
        ''' 图标，播放，0.8x
        ''' </summary>
        Public Const IconPlay As String = "M803.904 463.936a55.168 55.168 0 0 1 0 96.128l-463.616 264.448C302.848 845.888 256 819.136 256 776.448V247.616c0-42.752 46.848-69.44 84.288-48.064l463.616 264.384z"
    End Class

#End Region

#Region "自定义类"

    ''' <summary>
    ''' 支持小数与常见类型隐式转换的颜色。
    ''' </summary>
    Public Class MyColor

        Public A As Double = 255
        Public R As Double = 0
        Public G As Double = 0
        Public B As Double = 0

        '类型转换
        Public Shared Widening Operator CType(str As String) As MyColor
            Return New MyColor(str)
        End Operator
        Public Shared Widening Operator CType(col As Color) As MyColor
            Return New MyColor(col)
        End Operator
        Public Shared Widening Operator CType(conv As MyColor) As Color
            Return Color.FromArgb(ClampToByte(conv.A), ClampToByte(conv.R), ClampToByte(conv.G), ClampToByte(conv.B))
        End Operator
        Public Shared Widening Operator CType(conv As MyColor) As System.Drawing.Color
            Return System.Drawing.Color.FromArgb(ClampToByte(conv.A), ClampToByte(conv.R), ClampToByte(conv.G), ClampToByte(conv.B))
        End Operator
        Public Shared Widening Operator CType(bru As SolidColorBrush) As MyColor
            Return New MyColor(bru.Color)
        End Operator
        Public Shared Widening Operator CType(conv As MyColor) As SolidColorBrush
            Return New SolidColorBrush(Color.FromArgb(ClampToByte(conv.A), ClampToByte(conv.R), ClampToByte(conv.G), ClampToByte(conv.B)))
        End Operator
        Public Shared Widening Operator CType(bru As Brush) As MyColor
            Return New MyColor(bru)
        End Operator
        Public Shared Widening Operator CType(conv As MyColor) As Brush
            Return New SolidColorBrush(Color.FromArgb(ClampToByte(conv.A), ClampToByte(conv.R), ClampToByte(conv.G), ClampToByte(conv.B)))
        End Operator

        '颜色运算
        Public Shared Operator +(a As MyColor, b As MyColor) As MyColor
            Return New MyColor With {.A = a.A + b.A, .B = a.B + b.B, .G = a.G + b.G, .R = a.R + b.R}
        End Operator
        Public Shared Operator -(a As MyColor, b As MyColor) As MyColor
            Return New MyColor With {.A = a.A - b.A, .B = a.B - b.B, .G = a.G - b.G, .R = a.R - b.R}
        End Operator
        Public Shared Operator *(a As MyColor, b As Double) As MyColor
            Return New MyColor With {.A = a.A * b, .B = a.B * b, .G = a.G * b, .R = a.R * b}
        End Operator
        Public Shared Operator /(a As MyColor, b As Double) As MyColor
            Return New MyColor With {.A = a.A / b, .B = a.B / b, .G = a.G / b, .R = a.R / b}
        End Operator
        Public Shared Operator =(a As MyColor, b As MyColor) As Boolean
            If IsNothing(a) AndAlso IsNothing(b) Then Return True
            If IsNothing(a) OrElse IsNothing(b) Then Return False
            Return a.A = b.A AndAlso a.R = b.R AndAlso a.G = b.G AndAlso a.B = b.B
        End Operator
        Public Shared Operator <>(a As MyColor, b As MyColor) As Boolean
            If IsNothing(a) AndAlso IsNothing(b) Then Return False
            If IsNothing(a) OrElse IsNothing(b) Then Return True
            Return Not (a.A = b.A AndAlso a.R = b.R AndAlso a.G = b.G AndAlso a.B = b.B)
        End Operator
        ''' <summary>
        ''' 获取两颜色间的百分比，基于 RGB 计算。
        ''' </summary>
        Public Shared Function Lerp(ValueA As MyColor, ValueB As MyColor, Percent As Double) As MyColor
            Return Round(ValueA * (1 - Percent) + ValueB * Percent, 6)
        End Function
        Public Shared Function Round(col As MyColor, Optional w As Integer = 0) As MyColor
            Return New MyColor With {.A = Math.Round(col.A, w), .R = Math.Round(col.R, w), .G = Math.Round(col.G, w), .B = Math.Round(col.B, w)}
        End Function
        ''' <summary>
        ''' 将一个数字限制为 0~255 的 Byte 值。
        ''' </summary>
        Public Shared Function ClampToByte(d As Double) As Byte
            If d < 0 Then d = 0
            If d > 255 Then d = 255
            Return Math.Round(d)
        End Function


        '构造函数
        Public Sub New()
        End Sub
        Public Sub New(col As Color)
            Me.A = col.A
            Me.R = col.R
            Me.G = col.G
            Me.B = col.B
        End Sub
        Public Sub New(HexString As String)
            Dim StringColor As Media.Color = ColorConverter.ConvertFromString(HexString)
            A = StringColor.A
            R = StringColor.R
            G = StringColor.G
            B = StringColor.B
        End Sub
        Public Sub New(newA As Double, col As MyColor)
            Me.A = newA
            Me.R = col.R
            Me.G = col.G
            Me.B = col.B
        End Sub
        Public Sub New(newR As Double, newG As Double, newB As Double)
            Me.A = 255
            Me.R = newR
            Me.G = newG
            Me.B = newB
        End Sub
        Public Sub New(newA As Double, newR As Double, newG As Double, newB As Double)
            Me.A = newA
            Me.R = newR
            Me.G = newG
            Me.B = newB
        End Sub
        Public Sub New(brush As Brush)
            Dim Color As Color = CType(brush, SolidColorBrush).Color
            A = Color.A
            R = Color.R
            G = Color.G
            B = Color.B
        End Sub
        Public Sub New(brush As SolidColorBrush)
            Dim Color As Color = brush.Color
            A = Color.A
            R = Color.R
            G = Color.G
            B = Color.B
        End Sub
        Public Sub New(obj As Object)
            If obj Is Nothing Then
                A = 255 : R = 255 : G = 255 : B = 255
            Else
                If TypeOf obj Is SolidColorBrush Then
                    '避免反复获取 Color 对象造成性能下降
                    Dim Color As Color = CType(obj, SolidColorBrush).Color
                    A = Color.A
                    R = Color.R
                    G = Color.G
                    B = Color.B
                Else
                    A = obj.A
                    R = obj.R
                    G = obj.G
                    B = obj.B
                End If
            End If
        End Sub

        'HSL
        Public Function Hue(v1 As Double, v2 As Double, vH As Double) As Double
            If vH < 0 Then vH += 1
            If vH > 1 Then vH -= 1
            If vH < 0.16667 Then Return v1 + (v2 - v1) * 6 * vH
            If vH < 0.5 Then Return v2
            If vH < 0.66667 Then Return v1 + (v2 - v1) * (4 - vH * 6)
            Return v1
        End Function
        Public Function FromHSL(sH As Double, sS As Double, sL As Double) As MyColor
            A = 255
            If sS = 0 Then
                R = sL * 2.55 : G = R : B = R
                Return Me
            End If

            Dim H = sH / 360
            Dim S = sS / 100
            Dim L = sL / 100
            S = If(L < 0.5, S * L + L, S * (1.0 - L) + L)
            L = 2 * L - S
            R = 255 * Hue(L, S, H + 1 / 3)
            G = 255 * Hue(L, S, H)
            B = 255 * Hue(L, S, H - 1 / 3)
            Return Me
        End Function
        Public Function FromHSL2(sH As Double, sS As Double, sL As Double) As MyColor
            A = 255
            If sS = 0 Then
                R = sL * 2.55 : G = R : B = R
                Return Me
            End If

            '初始化
            sH = (sH + 3600000) Mod 360
            Dim cent As Double() = {
                +0.1, -0.06, -0.3, '0, 30, 60
                -0.19, -0.15, -0.24, '90, 120, 150
                -0.32, -0.09, +0.18, '180, 210, 240
                +0.05, -0.12, -0.02, '270, 300, 330
                +0.1, -0.06} '最后两位与前两位一致，加是变亮，减是变暗
            '计算色调对应的亮度片区
            Dim center As Double = sH / 30.0
            Dim intCenter As Integer = Math.Floor(center) '亮度片区编号
            center = 50 - (
                 (1 - center + intCenter) * cent(intCenter) + (center - intCenter) * cent(intCenter + 1)
                ) * sS
            sL = If(sL < center, sL / center, 1 + (sL - center) / (100 - center)) * 50
            FromHSL(sH, sS, sL)
            Return Me
        End Function

        Public Overrides Function ToString() As String
            Return "(" & A & "," & R & "," & G & "," & B & ")"
        End Function
        Public Overrides Function Equals(obj As Object) As Boolean
            Return Me = obj
        End Function

    End Class

    ''' <summary>
    ''' 支持负数与浮点数的矩形。
    ''' </summary>
    Public Class MyRect

        '属性
        Public Property Width As Double = 0
        Public Property Height As Double = 0
        Public Property Left As Double = 0
        Public Property Top As Double = 0

        '构造函数
        Public Sub New()
        End Sub
        Public Sub New(left As Double, top As Double, width As Double, height As Double)
            Me.Left = left
            Me.Top = top
            Me.Width = width
            Me.Height = height
        End Sub

    End Class

    ''' <summary>
    ''' 模块加载状态枚举。
    ''' </summary>
    Public Enum LoadState
        Waiting
        Loading
        Finished
        Failed
        Canceled
    End Enum

    ''' <summary>
    ''' 执行返回值。
    ''' </summary>
    Public Enum ProcessReturnValues
        ''' <summary>
        ''' 执行成功。
        ''' </summary>
        Success = 0
        ''' <summary>
        ''' 执行失败。
        ''' </summary>
        Fail = 1
        ''' <summary>
        ''' 执行时出现未经处理的异常。
        ''' </summary>
        Exception = 2
        ''' <summary>
        ''' 执行超时。
        ''' </summary>
        Timeout = 3
        ''' <summary>
        ''' 取消执行。可能是由于不满足执行的前置条件。
        ''' </summary>
        Cancel = 4
        ''' <summary>
        ''' 任务成功完成。
        ''' </summary>
        TaskDone = 5
    End Enum

#End Region

#Region "文件"

    '=============================
    '  注册表
    '=============================

    ''' <summary>
    ''' 读取注册表键。如果失败则返回默认值。
    ''' </summary>
    Public Function ReadReg(Key As String, Optional DefaultValue As String = "") As String
        Try
            Return If(My.Computer.Registry.CurrentUser.OpenSubKey("Software\" & RegFolder, False)?.GetValue(Key), DefaultValue)
        Catch ex As Exception
            Logger.Error(ex, $"读取注册表出错：{Key}", LogBehavior.Toast)
            Return DefaultValue
        End Try
    End Function
    ''' <summary>
    ''' 写入注册表键。
    ''' </summary>
    Public Sub WriteReg(Key As String, Value As String, Optional ThrowException As Boolean = False)
        Try
            Dim SubKey As Microsoft.Win32.RegistryKey = My.Computer.Registry.CurrentUser.OpenSubKey("Software\" & RegFolder, True)
            If SubKey Is Nothing Then SubKey = My.Computer.Registry.CurrentUser.CreateSubKey("Software\" & RegFolder) '如果不存在就创建  
            SubKey.SetValue(Key, Value)
        Catch ex As Exception
            Logger.Warn(ex, $"写入注册表出错：{Key}", If(ThrowException, LogBehavior.Toast, LogBehavior.ToastIfDebug))
            If ThrowException Then Throw
        End Try
    End Sub
    ''' <summary>
    ''' 是否存在某个注册表键。
    ''' </summary>
    Public Function HasReg(Key As String) As Boolean
        Return ReadReg(Key, Nothing) IsNot Nothing
    End Function
    ''' <summary>
    ''' 删除注册表键。
    ''' </summary>
    Public Sub DeleteReg(Key As String, Optional ThrowException As Boolean = False)
        Try
            Dim SubKey As Microsoft.Win32.RegistryKey = My.Computer.Registry.CurrentUser.OpenSubKey("Software\" & RegFolder, True)
            If SubKey?.GetValue(Key) Is Nothing Then Return
            SubKey.DeleteValue(Key)
        Catch ex As Exception
            Logger.Warn(ex, $"删除注册表出错：{Key}", If(ThrowException, LogBehavior.Toast, LogBehavior.ToastIfDebug))
            If ThrowException Then Throw
        End Try
    End Sub

    '=============================
    '  ini
    '=============================

    Private ReadOnly IniCache As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, String))
    ''' <summary>
    ''' 清除某 ini 文件的运行时缓存。
    ''' </summary>
    ''' <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    Public Sub IniClearCache(FileName As String)
        If Not FileName.Contains(":\") Then FileName = $"{Paths.Base}PCL\{FileName}.ini"
        IniCache.Remove(FileName)
    End Sub
    ''' <summary>
    ''' 获取 ini 文件缓存。如果没有，则新读取 ini 文件内容。
    ''' 在文件不存在或读取失败时返回 Nothing。
    ''' </summary>
    ''' <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    Private Function IniGetContent(FileName As String) As ConcurrentDictionary(Of String, String)
        Try
            '还原文件路径
            If Not FileName.Contains(":\") Then FileName = $"{Paths.Base}PCL\{FileName}.ini"
            '检索缓存
            Dim Cache As ConcurrentDictionary(Of String, String) = Nothing
            If IniCache.TryGetValue(FileName, Cache) Then Return Cache
            '读取文件
            If Not FileUtils.Exists(FileName) Then Return Nothing
            Dim Ini As New ConcurrentDictionary(Of String, String)
            For Each Line In FileUtils.ReadAsLines(FileName, True)
                Dim Index As Integer = Line.IndexOfF(":")
                If Index > 0 Then Ini(Line.Substring(0, Index)) = Line.Substring(Index + 1) '可能会有重复键，见 #3616
            Next
            IniCache(FileName) = Ini
            Return Ini
        Catch ex As Exception
            Logger.Error(ex, $"生成 ini 文件缓存失败（{FileName}）", LogBehavior.Toast)
            Return Nothing
        End Try
    End Function
    ''' <summary>
    ''' 读取 ini 文件。这可能会使用到缓存。
    ''' </summary>
    ''' <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    ''' <param name="Key">键。</param>
    ''' <param name="DefaultValue">没有找到键时返回的默认值。</param>
    Public Function ReadIni(FileName As String, Key As String, Optional DefaultValue As String = "") As String
        Dim Content = IniGetContent(FileName)
        If Content Is Nothing Then Return DefaultValue
        Return Content.GetOrDefault(Key, DefaultValue)
    End Function
    ''' <summary>
    ''' 判断 ini 文件中是否包含某个键。这可能会使用到缓存。
    ''' </summary>
    Public Function HasIniKey(FileName As String, Key As String) As Boolean
        Dim Content = IniGetContent(FileName)
        Return Content IsNot Nothing AndAlso Content.ContainsKey(Key)
    End Function
    ''' <summary>
    ''' 从 ini 文件中移除某个键。这会更新缓存。
    ''' </summary>
    Public Sub DeleteIniKey(FileName As String, Key As String)
        WriteIni(FileName, Key, Nothing)
    End Sub
    ''' <summary>
    ''' 写入 ini 文件，这会更新缓存。
    ''' 若 Value 为 Nothing，则删除该键。
    ''' </summary>
    ''' <param name="FileName">文件完整路径或简写文件名。简写将会使用“ApplicationName\文件名.ini”作为路径。</param>
    ''' <param name="Key">键。</param>
    ''' <param name="Value">值。</param>
    Public Sub WriteIni(FileName As String, Key As String, Value As String)
        Try
            '预处理
            If Key.Contains(":") Then Throw New Exception($"尝试写入 ini 文件 {FileName} 的键名中包含了冒号：{Key}")
            Key = Key.ReplaceLineEndings("")
            Value = Value?.ReplaceLineEndings("")
            '获取目前文件
            Dim Content = IniGetContent(FileName)
            If Content Is Nothing Then Content = New ConcurrentDictionary(Of String, String)
            '更新值
            If Value Is Nothing Then
                If Not Content.Remove(Key) Then Return '无需处理
            Else
                Dim ExistingValue As String = Nothing
                If Content.TryGetValue(Key, ExistingValue) AndAlso ExistingValue = Value Then Return '无需处理
                Content(Key) = Value
            End If
            '写入文件
            Dim FileContent As New StringBuilder
            For Each Pair In Content
                FileContent.Append(Pair.Key)
                FileContent.Append(":")
                FileContent.Append(Pair.Value)
                FileContent.Append(vbCrLf)
            Next
            If Not FileName.Contains(":\") Then FileName = $"{Paths.Base}PCL\{FileName}.ini"
            '处理相对路径
            FileName = If(FileName.Contains(":\"), FileName, Paths.Base & FileName)
            FileUtils.Write(FileName, FileContent.ToString)
        Catch ex As Exception
            Logger.Error(ex, $"写入文件失败（{FileName} → {Key}:{Value}）", LogBehavior.Toast)
        End Try
    End Sub

    '文件校验
    ''' <summary>
    ''' 检查是否拥有某一文件夹的 I/O 权限。如果文件夹不存在，会返回 False。
    ''' </summary>
    Public Function CheckPermission(Folder As String) As Boolean
        Try
            If String.IsNullOrEmpty(Folder) Then Return False
            Folder = PathUtils.AddSlashSuffix(Folder)
            If Folder.EndsWithF(":\System Volume Information\") OrElse Folder.EndsWithF(":\$RECYCLE.BIN\") Then Return False
            If Not DirectoryUtils.Exists(Folder) Then Return False
            Dim TestFilePath As String = $"{Folder}CheckPermission{GetUuid()}.txt"
            FileUtils.Write(TestFilePath, "临时文件，用于检测该文件夹的权限是否正常。")
            FileUtils.Delete(TestFilePath)
            Return True
        Catch ex As Exception
            Logger.Warn(ex, $"没有对文件夹 {Folder} 的权限，请尝试以管理员权限运行 PCL")
            Return False
        End Try
    End Function
    ''' <summary>
    ''' 检查是否拥有某一文件夹的 I/O 权限。如果出错，则抛出异常。
    ''' </summary>
    Public Sub CheckPermissionWithException(Folder As String)
        If String.IsNullOrWhiteSpace(Folder) Then Throw New ArgumentNullException("文件夹名不能为空！")
        Folder = PathUtils.AddSlashSuffix(Folder)
        If Folder.EndsWithF(":\System Volume Information\") OrElse Folder.EndsWithF(":\$RECYCLE.BIN\") Then Throw New UnauthorizedAccessException("没有对系统文件夹的权限！")
        If Not DirectoryUtils.Exists(Folder) Then Throw New DirectoryNotFoundException("文件夹不存在！")
        Dim TestFilePath As String = $"{Folder}CheckPermission{GetUuid()}.txt"
        FileUtils.Write(TestFilePath, "临时文件，用于检测该文件夹的权限是否正常。")
        FileUtils.Delete(TestFilePath)
    End Sub

#End Region

#Region "文本"
    Public vbLQ As Char = Convert.ToChar(8220)
    Public vbRQ As Char = Convert.ToChar(8221)

    ''' <summary>
    ''' 不会报错的 Val。
    ''' 如果输入有误，返回 0。
    ''' </summary>
    Public Function Val(Str As Object) As Double
        If Str Is Nothing Then Return 0
        Try
            Return If(TypeOf Str Is String AndAlso Str = "&", 0, Conversion.Val(Str))
        Catch
            Return 0
        End Try
    End Function

#End Region

#Region "搜索"

    ''' <summary>
    ''' 获取搜索文本的相似度。
    ''' </summary>
    ''' <param name="Source">被搜索的长内容。</param>
    ''' <param name="Query">用户输入的搜索文本。</param>
    Private Function SearchSimilarity(Source As String, Query As String) As Double
        If String.IsNullOrEmpty(Source) OrElse String.IsNullOrEmpty(Query) Then Return 0
        Dim qp As Integer = 0, lenSum As Double = 0
        Dim str As New StringBuilder(Source.Length)
        str.Append(Source.Lower().Replace(" ", ""))
        Query = Query.Lower().Replace(" ", "")
        Dim sourceLength As Integer = str.Length, queryLength As Integer = Query.Length '用于计算最后因数的长度缓存
        If queryLength = 0 Then Return 0
        Do While qp < queryLength
            '对 qp 作为开始位置计算
            Dim sp As Integer = 0, lenMax As Integer = 0, spMax As Integer = 0
            Dim currentSourceLength As Integer = str.Length
            Do While sp < currentSourceLength
                Dim len As Integer = 0
                While (qp + len) < queryLength AndAlso (sp + len) < currentSourceLength AndAlso str(sp + len) = Query(qp + len)
                    len += 1
                End While
                '存储 len
                If len > lenMax Then
                    lenMax = len
                    spMax = sp
                End If
                '根据结果增加 sp
                sp += If(len > 0, len, 1)
            Loop
            If lenMax > 0 Then
                str.Remove(spMax, lenMax) '将源中的对应字段移除
                '存储 lenSum
                Dim IncWeight = (Math.Pow(1.4, 3 + lenMax) - 3.6) '根据长度加成
                IncWeight *= 1 + 0.3 * Math.Max(0, 3 - Math.Abs(qp - spMax)) '根据位置加成
                lenSum += IncWeight
            End If
            qp += If(lenMax > 0, lenMax, 1)
        Loop
        '计算结果：重复字段量 × 源长度影响比例
        Return (lenSum / queryLength) * (3 / Math.Sqrt(sourceLength + 15)) * If(queryLength <= 2, 3 - queryLength, 1)
    End Function
    ''' <summary>
    ''' 获取多段文本加权后的相似度。
    ''' </summary>
    Private Function SearchSimilarityWeighted(Source As List(Of SearchSource), Query As String) As Double
        Dim TotalWeight As Double = 0
        Dim Sum As Double = 0
        For Each Pair In Source
            If Pair.Aliases.Any Then
                Sum += Pair.Aliases.Max(Function(a) SearchSimilarity(a, Query)) * Pair.Weight
            End If
            TotalWeight += Pair.Weight
        Next
        Return Sum / TotalWeight
    End Function
    ''' <summary>
    ''' 用于搜索的项目。
    ''' </summary>
    Public Class SearchEntry(Of T)
        ''' <summary>
        ''' 该项目对应的源数据。
        ''' </summary>
        Public Item As T
        ''' <summary>
        ''' 该项目用于搜索的文本源。
        ''' 在搜索时，会对每个文本源单独加权，但单个文本源内的多个别名只取最高的一个的相似度。
        ''' </summary>
        Public SearchSource As List(Of SearchSource)
        ''' <summary>
        ''' 相似度。
        ''' </summary>
        Public Similarity As Double
        ''' <summary>
        ''' 是否完全匹配。
        ''' </summary>
        Public AbsoluteRight As Boolean
        Public Overrides Function ToString() As String
            Return Math.Round(Similarity, 3) & " - " & Item.ToString()
        End Function
    End Class
    ''' <summary>
    ''' 单个用于搜索的文本源。
    ''' </summary>
    Public Class SearchSource
        Public Aliases As String()
        Public Weight As Double
        Public Sub New(Aliases As String(), Optional Weight As Double = 1)
            Me.Aliases = Aliases
            Me.Weight = Weight
        End Sub
        Public Sub New(Text As String, Optional Weight As Double = 1)
            Me.Aliases = {Text}
            Me.Weight = Weight
        End Sub
    End Class
    ''' <summary>
    ''' 进行多段文本加权搜索，获取相似度较高的数项结果。
    ''' 在搜索时，会对每个文本源单独加权，但单个文本源内的多个别名只取最高的一个的相似度。
    ''' 这会修改 Entries 中每项的 Similarity 与 AbsoluteRight 字段。
    ''' </summary>
    ''' <param name="MaxBlurCount">返回的最大模糊结果数。</param>
    ''' <param name="MinBlurSimilarity">返回结果要求的最低相似度。</param>
    Public Function Search(Of T)(Entries As List(Of SearchEntry(Of T)), Query As String, Optional MaxBlurCount As Integer = 5, Optional MinBlurSimilarity As Double = 0.1) As List(Of SearchEntry(Of T))
        '初始化
        Dim ResultList As New List(Of SearchEntry(Of T))
        If Not Entries.Any() Then Return ResultList
        '进行搜索，获取相似信息
        Dim QueryParts = Query.Split(" ", True)
        Dim Candidates As New List(Of SearchEntry(Of T))
        For Each Entry In Entries
            Entry.Similarity = SearchSimilarityWeighted(Entry.SearchSource, Query)
            Entry.AbsoluteRight =
                QueryParts.All( '对于按空格分割的每一段
                Function(QueryPart) Entry.SearchSource.Any( '若与任意一个搜索源完全匹配，则标记为完全匹配项
                Function(Source) Source.Aliases.Any(
                Function([Alias]) [Alias].Replace(" ", "").ContainsIgnoreCase(QueryPart))))
            If Entry.AbsoluteRight OrElse Entry.Similarity >= MinBlurSimilarity Then Candidates.Add(Entry)
        Next
        '按照相似度进行排序
        Candidates = Candidates.SortByComparison(Function(Left, Right) If(Left.AbsoluteRight <> Right.AbsoluteRight, Left.AbsoluteRight, Left.Similarity > Right.Similarity))
        '返回结果
        Dim BlurCount As Integer = 0
        For Each Entry In Candidates
            If Entry.AbsoluteRight Then
                ResultList.Add(Entry) '完全匹配，直接加入
            Else
                If BlurCount = MaxBlurCount Then Exit For
                ResultList.Add(Entry)
                BlurCount += 1 '模糊结果计数
            End If
        Next
        Return ResultList
    End Function

#End Region

#Region "系统"

    ''' <summary>
    ''' 为 Task 设置超时，在超时时抛出 TimeoutException。
    ''' </summary>
    <Extension> Public Function GetResultWithTimeout(Of T)(TargetTask As Task(Of T), TokenSource As CancellationTokenSource, TimeoutMs As Integer) As T
        Dim DelayTask = Task.Delay(TimeoutMs)
        If Task.WhenAny(TargetTask, DelayTask).ConfigureAwait(False).GetAwaiter().GetResult() Is DelayTask Then
            TokenSource.Cancel()
            Throw New TimeoutException($"任务超时（{TimeoutMs} ms）")
        End If
        Return TargetTask.Run()
    End Function
    ''' <summary>
    ''' 为 Task 设置超时，在超时时抛出 TimeoutException。
    ''' </summary>
    <Extension> Public Sub GetResultWithTimeout(TargetTask As Task, TokenSource As CancellationTokenSource, TimeoutMs As Integer)
        Dim DelayTask = Task.Delay(TimeoutMs)
        If Task.WhenAny(TargetTask, DelayTask).ConfigureAwait(False).GetAwaiter().GetResult() Is DelayTask Then
            TokenSource.Cancel()
            Throw New TimeoutException($"任务超时（{TimeoutMs} ms）")
        End If
        TargetTask.Run()
    End Sub

    ''' <summary>
    ''' 可用于临时存放文件的，不含任何特殊字符的文件夹路径，以“\”结尾。
    ''' </summary>
    Public PathPure As String = GetPureASCIIDir()
    Private Function GetPureASCIIDir() As String
        If Paths.Base.IsAsciiOnly() Then
            Return Paths.Base & "PCL\"
        ElseIf Paths.AppDataThenName.IsAsciiOnly() Then
            Return Paths.AppDataThenName
        ElseIf PathTemp.IsAsciiOnly() Then
            Return PathTemp
        Else
            Return OsDrive & "ProgramData\PCL\"
        End If
    End Function

    ''' <summary>
    ''' 指示接取到这个异常的函数进行重试。
    ''' </summary>
    Public Class RestartException
        Inherits Exception
    End Class

    ''' <summary>
    ''' 以管理员权限运行当前程序，并等待程序运行结束。
    ''' 返回程序的返回代码，如果运行失败将抛出异常。
    ''' </summary>
    Public Function RunAsAdmin(Argument As String) As Integer
        Dim NewProcess = StartProcess(New ProcessStartInfo(PathExe) With {.Verb = "runas", .Arguments = Argument})
        NewProcess.WaitForExit()
        Return NewProcess.ExitCode
    End Function

    Private Uuid As Integer = 1
    Private UuidLock As Object
    ''' <summary>
    ''' 获取一个全程序内不会重复的数字（伪 Uuid）。
    ''' </summary>
    Public Function GetUuid() As Integer
        If UuidLock Is Nothing Then UuidLock = New Object
        SyncLock UuidLock
            Uuid += 1
            Return Uuid
        End SyncLock
    End Function

    '时间相关
    ''' <summary>
    ''' 获取一个单调递增时间值（毫秒）。
    ''' </summary>
    Public Function GetTimeMs() As Long
        Return Stopwatch.GetTimestamp() \ (Stopwatch.Frequency \ 1000L)
    End Function
    ''' <summary>
    ''' 获取十进制 Unix 时间戳。
    ''' </summary>
    Public Function GetUnixTimestampUtc() As Long
        Return (Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000
    End Function
    ''' <summary>
    ''' 时间戳转化为日期。
    ''' </summary>
    Public Function GetDate(timeStamp As Integer) As Date
        Return DateTimeOffset.FromUnixTimeSeconds(timeStamp).LocalDateTime
    End Function
    ''' <summary>
    ''' 将 UTC 时间转化为当前时区的时间。
    ''' </summary>
    Public Function GetLocalTime(UtcDate As Date) As Date
        Return DateTime.SpecifyKind(UtcDate, DateTimeKind.Utc).ToLocalTime
    End Function

    ''' <summary>
    ''' 用于储存 RaiseByMouse 的 EventArgs。
    ''' </summary>
    Public NotInheritable Class RouteEventArgs
        Inherits EventArgs
        Public RaiseByMouse As Boolean
        Public Handled As Boolean = False
        Public Sub New(Optional RaiseByMouse As Boolean = False)
            Me.RaiseByMouse = RaiseByMouse
        End Sub
    End Class

    ''' <summary>
    ''' 启动进程并返回进程实例，若失败则抛出异常。
    ''' 会自动进行长路径处理。
    ''' </summary>
    Public Function StartProcess(FileName As String, Optional Arguments As String = "") As Process
        Return StartProcess(New ProcessStartInfo With {.FileName = FileName, .Arguments = Arguments})
    End Function
    ''' <summary>
    ''' 启动进程并返回进程实例，若失败则抛出异常。
    ''' 会自动进行长路径处理。
    ''' </summary>
    Public Function StartProcess(StartInfo As ProcessStartInfo) As Process
        StartInfo.FileName = PathUtils.ToShortPath(StartInfo.FileName)
        If Not String.IsNullOrEmpty(StartInfo.WorkingDirectory) Then StartInfo.WorkingDirectory = PathUtils.ToShortPath(StartInfo.WorkingDirectory)
        Logger.Info($"启动进程：{StartInfo.FileName} {StartInfo.Arguments}")
        Return Process.Start(StartInfo)
    End Function

    ''' <summary>
    ''' 在新的工作线程中执行代码。
    ''' </summary>
    Public Function RunInNewThread(Action As Action, Optional Name As String = Nothing, Optional Priority As ThreadPriority = ThreadPriority.Normal) As Thread
        Dim th As New Thread(
        Sub()
            Try
                Action()
            Catch ex As Exception
                Logger.Error(ex, $"{Name}：线程执行失败")
            End Try
        End Sub) With {.Name = If(Name, "Runtime New Invoke " & GetUuid() & "#"), .Priority = Priority}
        th.Start()
        Return th
    End Function
    ''' <summary>
    ''' 确保在 UI 线程中执行代码。
    ''' 如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ''' 为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    ''' </summary>
    Public Function RunInUiWait(Of Output)(Action As Func(Of Output)) As Output
        If RunInUi() Then
            Return Action()
        Else
            Return Application.Current.Dispatcher.Invoke(Action)
        End If
    End Function
    ''' <summary>
    ''' 确保在 UI 线程中执行代码。
    ''' 如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    ''' 为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    ''' </summary>
    Public Sub RunInUiWait(Action As Action)
        If RunInUi() Then
            Action()
        Else
            Application.Current.Dispatcher.Invoke(Action)
        End If
    End Sub
    ''' <summary>
    ''' 确保在 UI 线程中执行代码，代码按触发顺序执行。
    ''' 如果当前并非 UI 线程，也不阻断当前线程的执行。
    ''' </summary>
    Public Sub RunInUi(Action As Action, Optional ForceWaitUntilLoaded As Boolean = False)
        If ForceWaitUntilLoaded Then
            Application.Current.Dispatcher.InvokeAsync(Action, Threading.DispatcherPriority.Loaded)
        ElseIf RunInUi() Then
            Action()
        Else
            Application.Current.Dispatcher.InvokeAsync(Action)
        End If
    End Sub
    ''' <summary>
    ''' 确保在工作线程中执行代码。
    ''' </summary>
    Public Sub RunInThread(Action As Action)
        If RunInUi() Then
            RunInNewThread(Action, "Invoke " & GetUuid())
        Else
            Action()
        End If
    End Sub

    ''' <summary>
    ''' 按照既定的函数进行选择排序。
    ''' 返回第一个对象是否应该排在前面（a > b）。
    ''' </summary>
    <Extension> Public Function SortByComparison(Of T)(List As IList(Of T), SortRule As Func(Of T, T, Boolean)) As List(Of T)
        Dim NewList As New List(Of T)
        While List.Any
            Dim Highest = List(0)
            For i = 1 To List.Count - 1
                If SortRule(List(i), Highest) Then Highest = List(i)
            Next
            List.Remove(Highest)
            NewList.Add(Highest)
        End While
        Return NewList
    End Function

    ''' <summary>
    ''' 获取程序启动参数。
    ''' </summary>
    ''' <param name="Name">参数名。</param>
    ''' <param name="DefaultValue">默认值。</param>
    Public Function GetProgramArgument(Name As String, Optional DefaultValue As Object = "")
        Dim AllArguments() As String = Command.Split(" ")
        For i = 0 To AllArguments.Length - 1
            If AllArguments(i) = "-" & Name Then
                If AllArguments.Length = i + 1 OrElse AllArguments(i + 1).StartsWithF("-") Then Return True
                Return AllArguments(i + 1)
            End If
        Next
        Return DefaultValue
    End Function

    ''' <summary>
    ''' 打开网页。
    ''' </summary>
    Public Sub OpenWebsite(Url As String)
        Try
            If Not Url.StartsWithF("http", True) AndAlso Not Url.StartsWithF("minecraft://", True) AndAlso Not Url.StartsWithF("minecraft-preview://", True) Then
                Throw New Exception(Url & " 不是一个有效的网址，它必须以 http 开头！")
            End If
            Logger.Info($"正在打开网页：{Url}")
            StartProcess(Url)
        Catch ex As Exception
            Logger.Warn(ex, $"无法打开网页（{Url}）")
            ClipboardSet(Url, False)
            MyMsgBox("可能由于浏览器未正确配置，PCL 无法为你打开网页。" & vbCrLf & "网址已经复制到剪贴板，若有需要可以手动粘贴访问。" & vbCrLf &
                     $"网址：{Url}", "无法打开网页")
        End Try
    End Sub
    ''' <summary>
    ''' 打开 explorer。
    ''' 若不以 \ 结尾，则将视作文件路径，打开并选中此文件。
    ''' </summary>
    Public Sub OpenExplorer(Location As String)
        Try
            Location = Location.Replace("/", "\").Trim(" "c, """"c)
            Logger.Info($"正在打开资源管理器：{Location}")
            If Location.EndsWithF("\") Then
                StartProcess(Location)
            Else
                StartProcess("explorer", $"/select,""{Location}""")
            End If
        Catch ex As Exception
            Logger.Error(ex, "打开资源管理器失败，请尝试关闭安全软件（如 360 安全卫士）", LogBehavior.Alert)
        End Try
    End Sub

    ''' <summary>
    ''' 设置剪贴板。将在另一线程运行，且不会抛出异常。
    ''' </summary>
    Public Sub ClipboardSet(Text As String, Optional ShowSuccessHint As Boolean = True)
        RunInThread(
        Sub()
            Try
                Retrier.Attempt(delay:=Function(Attempt) TimeSpan.FromMilliseconds(200), maxAttempts:=4, isRetryAllowed:=Function(ex) True, action:=
                Sub()
                    RunInUi(
                    Sub()
                        My.Computer.Clipboard.Clear()
                        If Not String.IsNullOrEmpty(Text) Then My.Computer.Clipboard.SetText(Text)
                    End Sub)
                End Sub)
                If ShowSuccessHint Then Hint("已成功复制！", HintType.Green)
            Catch ex As Exception
                Logger.Error(ex, "可能由于剪贴板被其他程序占用，文本复制失败", LogBehavior.Toast)
            End Try
        End Sub)
    End Sub
    ''' <summary>
    ''' 获取剪贴板文本。将在 UI 线程运行，且不会抛出异常。
    ''' </summary>
    Public Function ClipboardGetText() As String
        Dim Result As String = Nothing
        RunInUiWait(
        Sub()
            Try
                If My.Computer.Clipboard.ContainsText() Then Result = My.Computer.Clipboard.GetText()
            Catch ex As Exception
                Logger.Warn(ex, "获取剪贴板文本失败")
            End Try
        End Sub)
        Return Result
    End Function

    ''' <summary>
    ''' 将程序中的资源释放到文件。
    ''' </summary>
    Public Sub ExtractResources(FilePath As String, ResourceName As String)
        Dim Resource = My.Resources.ResourceManager.GetObject(ResourceName)
        If TypeOf Resource Is Byte() Then
            Dim Bytes = DirectCast(Resource, Byte())
            If FileUtils.Exists(FilePath) AndAlso CryptographyUtils.ComputeFileHash(FilePath) = CryptographyUtils.ComputeHash(Bytes) Then Return
            Logger.Info($"将资源写入到文件：{ResourceName} → {FilePath}")
            FileUtils.Write(FilePath, Bytes)
        ElseIf TypeOf Resource Is String Then
            Dim Content = DirectCast(Resource, String)
            If FileUtils.Exists(FilePath) AndAlso FileUtils.ReadAsString(FilePath) = Content Then Return
            Logger.Info($"将资源写入到文件：{ResourceName} → {FilePath}")
            FileUtils.Write(FilePath, Content)
        Else
            Throw New Exception($"资源 {ResourceName} 的类型不支持：{Resource.GetType.Name}")
        End If
    End Sub

#End Region

#Region "UI"

    'DPI 转换
    Public ReadOnly DPI As Integer = System.Drawing.Graphics.FromHwnd(IntPtr.Zero).DpiX
    ''' <summary>
    ''' 将经过 DPI 缩放的 WPF 尺寸转化为实际的像素尺寸。
    ''' </summary>
    Public Function GetPixelSize(WPFSize As Double) As Double
        Return WPFSize / 96 * DPI
    End Function
    ''' <summary>
    ''' 将实际的像素尺寸转化为经过 DPI 缩放的 WPF 尺寸。
    ''' </summary>
    Public Function GetWPFSize(PixelSize As Double) As Double
        Return PixelSize * 96 / DPI
    End Function

    ''' <summary>
    ''' 将 XML 转换为对应 UI 对象。
    ''' 注意：性能较差，不应大量使用。
    ''' </summary>
    Public Function GetObjectFromXML(Str As XElement)
        Return GetObjectFromXML(Str.ToString)
    End Function
    ''' <summary>
    ''' 将 XML 转换为对应 UI 对象。
    ''' 注意：性能较差，不应大量使用。
    ''' </summary>
    Public Function GetObjectFromXML(Str As String) As Object
        Str = Str. '兼容旧版自定义事件写法
            Replace("EventType=""", "local:CustomEventService.EventType=""").
            Replace("EventData=""", "local:CustomEventService.EventData=""").
            Replace("EventType='", "local:CustomEventService.EventType='").
            Replace("EventData='", "local:CustomEventService.EventData='").
            Replace("Property=""EventType""", "Property=""local:CustomEventService.EventType""").
            Replace("Property=""EventData""", "Property=""local:CustomEventService.EventData""").
            Replace("Property='EventType'", "Property='local:CustomEventService.EventType'").
            Replace("Property='EventData'", "Property='local:CustomEventService.EventData'")
        Using Stream As New MemoryStream(Encoding.UTF8.GetBytes(Str))
            '类型检查
            Using Reader As New XamlXmlReader(Stream)
                While Reader.Read()
                    For Each BlackListType In {GetType(WebBrowser), GetType(Frame), GetType(MediaElement), GetType(ObjectDataProvider), GetType(XamlReader), GetType(Window), GetType(XmlDataProvider), GetType(SettingService)}
                        If Reader.Type IsNot Nothing AndAlso BlackListType.IsAssignableFrom(Reader.Type.UnderlyingType) Then Throw New UnauthorizedAccessException($"基于安全考虑，不允许使用 {BlackListType.Name} 类型。")
                        If Reader.Value IsNot Nothing AndAlso Reader.Value = BlackListType.Name Then Throw New UnauthorizedAccessException($"基于安全考虑，不允许使用 {BlackListType.Name} 值。")
                    Next
                    For Each BlackListMember In {"Code", "FactoryMethod", "Static"}
                        If Reader.Member IsNot Nothing AndAlso Reader.Member.Name = BlackListMember Then Throw New UnauthorizedAccessException($"基于安全考虑，不允许使用 {BlackListMember} 成员。")
                    Next
                End While
            End Using
            '实际的加载
            Stream.Position = 0
            Using Writer As New StreamWriter(Stream)
                Writer.Write(Str)
                Writer.Flush()
                Stream.Position = 0
                Return Markup.XamlReader.Load(Stream)
            End Using
        End Using
    End Function

    Private ReadOnly UiThreadId As Integer = Thread.CurrentThread.ManagedThreadId
    ''' <summary>
    ''' 当前线程是否为主线程。
    ''' </summary>
    Public Function RunInUi() As Boolean
        Return Thread.CurrentThread.ManagedThreadId = UiThreadId
    End Function

    ''' <summary>
    ''' 检查某个控件是否位于主窗口可视区域内，且控件本身可见。
    ''' </summary>
    <Extension> Public Function IsVisibleInForm(element As FrameworkElement) As Boolean
        If Not element.IsVisible Then Return False
        Dim bounds As Rect = element.TransformToAncestor(FrmMain).TransformBounds(New Rect(0, 0, element.ActualWidth, element.ActualHeight))
        Dim rect As New Rect(0, 0, FrmMain.ActualWidth, FrmMain.ActualHeight)
        Return rect.Contains(bounds.TopLeft) OrElse rect.Contains(bounds.BottomRight)
    End Function

    ''' <summary>
    ''' 控件是否受到 TextTrimming 属性影响，导致内容被截取。
    ''' </summary>
    <Extension> Public Function IsTextTrimmed(TextBlock As TextBlock) As Boolean
        Dim typeface As New Typeface(TextBlock.FontFamily, TextBlock.FontStyle, TextBlock.FontWeight, TextBlock.FontStretch)
        Dim formattedText As New FormattedText(TextBlock.Text, Thread.CurrentThread.CurrentCulture, TextBlock.FlowDirection, typeface, TextBlock.FontSize, TextBlock.Foreground, DPI)
        Return formattedText.Width > TextBlock.ActualWidth
    End Function

    ''' <summary>
    ''' 将布尔值转换为 Visibility。True 转换为 Visible，False 转换为 Collapsed。
    ''' </summary>
    <Extension> Public Function ToVisibility(IsVisible As Boolean) As Visibility
        Return If(IsVisible, Visibility.Visible, Visibility.Collapsed)
    End Function

#End Region

#Region "随机"

    Private ReadOnly Random As New Random

    ''' <summary>
    ''' 随机选择其一。
    ''' </summary>
    Public Function RandomOne(Of T)(objects As ICollection(Of T)) As T
        Return objects(RandomInteger(0, objects.Count - 1))
    End Function

    ''' <summary>
    ''' 取随机整数（包含）。
    ''' </summary>
    Public Function RandomInteger(min As Integer, max As Integer) As Integer
        Return Math.Floor((max - min + 1) * Random.NextDouble) + min
    End Function

    ''' <summary>
    ''' 将数组随机打乱。
    ''' </summary>
    <Extension> Public Iterator Function Shuffle(Of T)(Raw As IEnumerable(Of T)) As IEnumerable(Of T)
        Dim RawCopy As New List(Of T)(Raw)
        Do While RawCopy.Any
            Dim i As Integer = RandomInteger(0, RawCopy.Count - 1)
            Yield RawCopy(i)
            RawCopy.RemoveAt(i)
        Loop
    End Function

#End Region

End Module
