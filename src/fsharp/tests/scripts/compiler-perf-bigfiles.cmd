setlocal 

artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\libtest\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\printf\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\members\basics-hw\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\members\basics-hw-mutrec\test.fs  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\tools\eval\test.fsx  2>> log.err 1>> log.out

artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\libtest\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\printf\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\members\basics-hw\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\members\basics-hw-mutrec\test.fs  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\tools\eval\test.fsx  2>> log.err 1>> log.out

artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\libtest\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\printf\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\members\basics-hw\test.fsx  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\core\members\basics-hw-mutrec\test.fs  2>> log.err 1>> log.out
artifacts\bin\fsc\Release\net472\fsc.exe /out:tmp.dll %* tests\fsharp\tools\eval\test.fsx  2>> log.err 1>> log.out

REM compiler-perf-bigfiles.log

endlocal
