<#
.SYNOPSIS
  Shows optimistic concurrency against the running API: two users update the same request
  with the same ETag. The first gets 200, the second gets 409 with the current state and
  who changed it. Works in Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\demo-409.ps1
  powershell -ExecutionPolicy Bypass -File .\scripts\demo-409.ps1 -Id 42
#>
param(
    [int]$Id = 0,
    [string]$BaseUrl = 'http://localhost:5080'
)

$ErrorActionPreference = 'Stop'

function Invoke-Api([string]$Method, [string]$Path, [hashtable]$Headers = @{}, [string]$Body = $null) {
    $params = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $Headers; UseBasicParsing = $true }
    if ($Body) {
        $params.ContentType = 'application/json; charset=utf-8'
        $params.Body = [Text.Encoding]::UTF8.GetBytes($Body)
    }
    try {
        $response = Invoke-WebRequest @params
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $response.Content; Headers = $response.Headers }
    } catch {
        $status = [int]$_.Exception.Response.StatusCode
        return [pscustomobject]@{ Status = $status; Body = $_.ErrorDetails.Message; Headers = @{} }
    }
}

function Get-Header($headers, [string]$name) { @($headers[$name])[0] }

# 1. Pick a request that is "InProgress" (it may move to Waiting or Completed).
if ($Id -eq 0) {
    $list = Invoke-Api GET '/api/requests?status=InProgress&pageSize=1'
    $Id = (($list.Body | ConvertFrom-Json).items | Select-Object -First 1).id
}

# 2. Both users read the request and get the same ETag.
$read = Invoke-Api GET "/api/requests/$Id"
$etag = Get-Header $read.Headers 'ETag'
$current = $read.Body | ConvertFrom-Json
Write-Host "Request $Id is '$($current.status)', ETag $etag"
Write-Host ''

# 3. Dana updates first.
$dana = Invoke-Api PATCH "/api/requests/$Id/status" @{ 'If-Match' = $etag; 'X-User-Name' = 'Dana' } '{"newStatus":"Waiting"}'
Write-Host "Dana  -> Waiting:   HTTP $($dana.Status)" -ForegroundColor Green

# 4. Yossi still holds the old ETag.
$yossi = Invoke-Api PATCH "/api/requests/$Id/status" @{ 'If-Match' = $etag; 'X-User-Name' = 'Yossi' } '{"newStatus":"Completed"}'
Write-Host "Yossi -> Completed: HTTP $($yossi.Status)" -ForegroundColor Yellow
Write-Host ''
Write-Host 'Body of the 409 response:'
Write-Host $yossi.Body

# 5. Without If-Match the API refuses to update at all.
$noEtag = Invoke-Api PATCH "/api/requests/$Id/status" @{ 'X-User-Name' = 'Yossi' } '{"newStatus":"Completed"}'
Write-Host ''
Write-Host "Without If-Match:   HTTP $($noEtag.Status)"

if ($dana.Status -ne 200 -or $yossi.Status -ne 409 -or $noEtag.Status -ne 428) { exit 1 }
