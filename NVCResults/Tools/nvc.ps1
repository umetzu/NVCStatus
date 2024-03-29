﻿#!/usr/bin/env pwsh

$tempFile = "/var/nvc.tmp.log"
$outputFile = "/var/nvc.log"

if (Test-Path $tempFile) {
  Remove-Item -Path $tempFile -Force
}

if (Test-Path $outputFile) {
  Remove-Item -Path $outputFile -Force
}

#$k = 0

for($i=600; $i -lt 670; $i++) #501 680
#for($i=668; $i -lt 669; $i++)
{
    $j = 1 # 1
    #$j = 37 # 1
    $lastError = ""
    
    $ProgressPreference = 'SilentlyContinue'

    while(!$lastError -and ($j -lt 100))  #100
    {
        $caseIndex = $j.ToString().PadLeft(3, '0')
        $caseUrl = 'https://us-central1-admob-app-id-4102154173.cloudfunctions.net/getNVCcaseInfo?caseNumber=LMA2020' + $i + $caseIndex + '&applicationType=IV&location='

        Write-Output $caseUrl

        $errorInvoke = $false
        try {
            Invoke-RestMethod $caseUrl -OutFile $tempFile
        } catch {
            $errorInvoke = $true
        }

        if (!$errorInvoke) {
            $lastError = Get-Content $tempFile | Where-Object {$_ -like ‘*Your search did not*’}
            $wrongCode = Get-Content $tempFile | Where-Object {$_ -like ‘*The code entered does not*’}
            $wrongCodeX = Get-Content $tempFile | Where-Object {$_ -like ‘*Enter the code as shown*’}
            $timeoutExp = Get-Content $tempFile | Where-Object {$_ -like ‘*Navigation timeout of*’}
            $wrongDom = Get-Content $tempFile | Where-Object {$_ -like ‘*DOM.describeNode*’}
            $timeoutSelector = Get-Content $tempFile | Where-Object {$_ -like ‘*waiting for selector*’}

            if ($wrongCode -or $wrongDom -or $wrongCodeX -or $timeoutExp -or $timeoutSelector) 
            {
                $j--
            } else {
                $byteArray = Get-Content $tempFile -AsByteStream -Raw

                Add-Content $outputFile -Value $byteArray -AsByteStream
                Add-Content $outputFile -Value "`r`n"
            }

            $j++
        }
    }
}

$currentDate = Get-Date -Format "yyyy-MM-dd"
$destinationLog = '/var/www/nvc-umetzu/wwwroot/logs/' + $currentDate + '.log'

Copy-Item $outputFile -Destination $destinationLog

if (Test-Path $tempFile) {
  Remove-Item -Path $tempFile -Force
}

if (Test-Path $outputFile) {
  Remove-Item -Path $outputFile -Force
}

#{"error":true,"errorMessage":"The code entered does not match the code displayed on the page."} retry
#{"error":true,"errorMessage":"Enter the code as shown is required."} retry
#{"error":true,"errorMessage":"ProtocolError: Protocol error (DOM.describeNode): Cannot find context with specified id"} skip/retry?
#{"error":true,"errorMessage":"Error: Error: failed to find element matching selector \"#ctl00_ContentPlaceHolder1_ucApplicationStatusView_lblStatus\""} skip
#{"error":true,"errorMessage":"TimeoutError: waiting for selector `#c_status_ctl00_contentplaceholder1_defaultcaptcha_CaptchaImageDiv` failed: timeout 30000ms exceeded"}
#{"error":true,"errorMessage":"waiting for selector `#ctl00_ContentPlaceHolder1_ucApplicationStatusView_lblStatus` failed: timeout 30000ms exceeded"}
#{"error":true,"errorMessage":"Your search did not return any data."}  next $i