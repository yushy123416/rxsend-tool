param(
    [string]$GiteeManifestUrl='https://gitee.com/ysyyyy/rxsend-tool/raw/main/updates/update.json',
    [string]$GitHubManifestUrl='https://github.com/yushy123416/rxsend-tool/releases/latest/download/update.json',
    [ValidateRange(5,180)][int]$TimeoutSeconds=30
)
$ErrorActionPreference='Stop'
$previousProtocol=[Net.ServicePointManager]::SecurityProtocol
[Net.ServicePointManager]::SecurityProtocol=$previousProtocol -bor [Net.SecurityProtocolType]::Tls12
$testFolder=Join-Path ([IO.Path]::GetTempPath()) ('RXSendTool-MirrorTest-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testFolder | Out-Null
$headers=@{'User-Agent'='RXSendTool-MirrorTest/1.0'}
$failures=New-Object 'System.Collections.Generic.List[string]'
$results=@()

function Require-Https([string]$Address){
    $uri=$null
    if(-not [Uri]::TryCreate($Address,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo){
        throw "Invalid HTTPS URL: $Address"
    }
    return $uri
}
function Test-Manifest([string]$Name,[string]$Address){
    [void](Require-Https $Address)
    Write-Host ""
    Write-Host "[$Name] Fetching manifest..." -ForegroundColor Cyan
    Write-Host $Address
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $response=Invoke-WebRequest -Uri $Address -Headers $headers -UseBasicParsing -TimeoutSec $TimeoutSeconds
    $content=$response.Content
    if($content -is [byte[]]){$content=[Text.Encoding]::UTF8.GetString($content)}
    $manifest=([string]$content).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $parsedVersion=$null
    if(-not [Version]::TryParse([string]$manifest.version,[ref]$parsedVersion)){throw 'Invalid version in manifest'}
    if([string]$manifest.sha256 -notmatch '^[0-9a-fA-F]{64}$'){throw 'Invalid SHA-256 in manifest'}
    [void](Require-Https ([string]$manifest.url))
    foreach($mirror in @($manifest.mirrors)){[void](Require-Https ([string]$mirror))}
    $watch.Stop()
    Write-Host ("PASS manifest: v{0}, {1} ms" -f $manifest.version,$watch.ElapsedMilliseconds) -ForegroundColor Green
    return $manifest
}
function Test-Payload([string]$Name,[string]$Address,$Manifest){
    [void](Require-Https $Address)
    Write-Host ""
    Write-Host "[$Name] Downloading EXE (will NOT be run)..." -ForegroundColor Cyan
    Write-Host $Address
    $file=Join-Path $testFolder ($Name+'.exe')
    $watch=[Diagnostics.Stopwatch]::StartNew()
    Invoke-WebRequest -Uri $Address -Headers $headers -UseBasicParsing -TimeoutSec $TimeoutSeconds -OutFile $file
    $watch.Stop()
    $bytes=[IO.File]::ReadAllBytes($file)
    if($bytes.Length -lt 1024 -or $bytes[0] -ne 77 -or $bytes[1] -ne 90){throw 'Response is not a Windows EXE (possibly an HTML login or error page)'}
    $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if($hash -ine [string]$Manifest.sha256){throw "SHA-256 mismatch: actual=$hash expected=$($Manifest.sha256)"}
    $identity=[Reflection.AssemblyName]::GetAssemblyName($file)
    if($identity.Name -ne 'RxToolbox'){throw "Wrong application identity: $($identity.Name)"}
    if($identity.Version.ToString(3) -ne [string]$Manifest.version){throw "Assembly version mismatch: $($identity.Version) vs $($Manifest.version)"}
    Write-Host ("PASS EXE: {0:N0} bytes, {1} ms" -f $bytes.Length,$watch.ElapsedMilliseconds) -ForegroundColor Green
    Write-Host ("SHA-256: "+$hash)
    return [pscustomobject]@{Source=$Name;Version=$Manifest.version;Bytes=$bytes.Length;SHA256=$hash}
}

try{
    Write-Host 'RXSend Tool update mirror test' -ForegroundColor Cyan
    Write-Host 'Downloads temporary files only. No installation or execution.'
    $gitee=$null
    $github=$null
    try{$gitee=Test-Manifest 'Gitee' $GiteeManifestUrl}catch{$failures.Add("Gitee manifest: "+$_.Exception.Message);Write-Host ("FAIL "+$failures[$failures.Count-1]) -ForegroundColor Red}
    try{$github=Test-Manifest 'GitHub' $GitHubManifestUrl}catch{$failures.Add("GitHub manifest: "+$_.Exception.Message);Write-Host ("FAIL "+$failures[$failures.Count-1]) -ForegroundColor Red}
    if($gitee -and $github){
        if($gitee.version -ne $github.version -or $gitee.sha256 -ine $github.sha256){
            $failures.Add('Gitee and GitHub manifests differ; mirror sync may not be complete.')
            Write-Host ("FAIL "+$failures[$failures.Count-1]) -ForegroundColor Red
        }else{Write-Host 'PASS Gitee and GitHub manifest versions / hashes match.' -ForegroundColor Green}
    }
    if($gitee){
        try{
            $primary=Require-Https ([string]$gitee.url)
            if($primary.Host -ne 'gitee.com'){throw 'Primary EXE is not hosted on Gitee'}
            $results+=Test-Payload 'Gitee' $gitee.url $gitee
        }catch{$failures.Add("Gitee EXE: "+$_.Exception.Message);Write-Host ("FAIL "+$failures[$failures.Count-1]) -ForegroundColor Red}
    }
    if($github){
        try{
            $backup=@($github.mirrors | Where-Object {([Uri]$_).Host -eq 'github.com'}) | Select-Object -First 1
            if(-not $backup){throw 'No GitHub fallback EXE URL found'}
            $results+=Test-Payload 'GitHub' $backup $github
        }catch{$failures.Add("GitHub EXE: "+$_.Exception.Message);Write-Host ("FAIL "+$failures[$failures.Count-1]) -ForegroundColor Red}
    }
    Write-Host ""
    if($results.Count){$results | Format-Table Source,Version,Bytes -AutoSize | Out-Host}
    if($failures.Count){
        Write-Host ("FAILED: {0} check(s). Copy the FAIL lines when reporting." -f $failures.Count) -ForegroundColor Red
        exit 1
    }
    Write-Host 'ALL PASS: Gitee primary and GitHub fallback are ready.' -ForegroundColor Green
    exit 0
}finally{
    [Net.ServicePointManager]::SecurityProtocol=$previousProtocol
    # Delete only the exact test-created files and directory; never recurse.
    foreach($name in @('Gitee.exe','GitHub.exe')){
        $temporaryFile=Join-Path $testFolder $name
        if(Test-Path -LiteralPath $temporaryFile){Remove-Item -LiteralPath $temporaryFile -Force}
    }
    if(Test-Path -LiteralPath $testFolder){[IO.Directory]::Delete($testFolder)}
}
