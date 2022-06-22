#Run code coverage tests to generate report
..\packages\OpenCover.4.6.519\tools\OpenCover.Console.exe -register:user "-target:C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" "-targetargs:..\bin\Release\Test\CoreSDK.Test\Net40\Microsoft.ApplicationInsights.Core.Net40.Tests.dll ..\bin\Release\Test\CoreSDK.Test\Net45\Microsoft.ApplicationInsights.Core.Net45.Tests.dll ..\bin\Release\Test\CoreSDK.Test\Net46\Microsoft.ApplicationInsights.Core.Net46.Tests.dll ..\bin\Release\Test\ServerTelemetryChannel.Test\TelemetryChannel.Nuget.Tests\TelemetryChannel.Nuget.Tests.dll ..\bin\Release\Test\ServerTelemetryChannel.Test\Net40.Tests\Microsoft.ApplicationInsights.TelemetryChannel.Net40.Tests.dll ..\bin\Release\Test\ServerTelemetryChannel.Test\Net45.Tests\Microsoft.ApplicationInsights.TelemetryChannel.Net45.Tests.dll /logger:trx" "-filter:+[Microsoft.ApplicationInsights*]* +[Microsoft.AI*]* -[*Tests]* -[*TestFramework*]*" -hideskipped:All -output:.\coverage.xml

#Download report uploader
(New-Object System.Net.WebClient).DownloadFile("https://codecov.io/bash", ".\CodecovUploader.sh")

#On the Agent box repo is in a detached state. So get branchName by commit hash
$lastCommit = $(git rev-parse HEAD)
Write-Host "Last commit:" $lastCommit

$branchNames = $(git branch --all --contains $lastCommit) 
Write-Host "All branches that have this commit:" $branchNames

$i=0
Foreach ($branchName in $branchNames)
{
    $i++
    # First element in the array is trash because repo is detached
    if ($i -gt 1)
    {
        $branchName = $branchName.Trim()
        # Check for what branches current commit (for which we have coverage) is the last commit
        $lastCommitOnBranch = $(git rev-parse $branchName)
        if ($lastCommitOnBranch -eq  $lastCommit)
        {
			#Cut the prefix that CodeCov does not handle well
            if ($branchName.StartsWith("remotes/origin/"))
            {
                $branchName = $branchName.Substring("remotes/origin/".Length)
            }
            Write-Host "We will upload report to:"  $branchName
			
			#Upload report
			.\CodecovUploader.sh -f coverage.xml -t $env:CODECOVACCESSKEY -X gcov -B $branchName
        }
    }
}

