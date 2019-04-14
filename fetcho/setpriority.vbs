'
' Script to set the priority of all postgres executables to below normal to avoid 
' maxing out my CPU
'
' Thanks https://devblogs.microsoft.com/scripting/hey-scripting-guy-how-can-i-change-the-priority-of-each-instance-of-an-application/
'

Const BELOW_NORMAL = 16384

strComputer = "."

Set objWMIService = GetObject("winmgmts:\\" & strComputer & "\root\cimv2")

Set colProcesses = objWMIService.ExecNotificationQuery _
    ("Select * From __InstanceCreationEvent Within 5 Where TargetInstance ISA 'Win32_Process'")

Do While True
    Set objLatestProcess = colProcesses.NextEvent
    If objLatestProcess.TargetInstance.Name = "postgres.exe" Then
        objLatestProcess.TargetInstance.SetPriority(BELOW_NORMAL)
    End If
Loop