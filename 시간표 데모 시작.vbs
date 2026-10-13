Option Explicit
Dim shell, fso, base, appPath
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
base = fso.GetParentFolderName(WScript.ScriptFullName)
appPath = fso.BuildPath(base, fso.GetBaseName(WScript.ScriptFullName) & ".bat")
shell.Run "cmd.exe /d /c " & Chr(34) & Chr(34) & appPath & Chr(34) & Chr(34), 1, False
