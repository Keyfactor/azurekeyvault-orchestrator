<#
.SYNOPSIS
    Verifies that a service principal has exactly the permissions the Keyfactor Azure KeyVault
    orchestrator extension needs, by replaying the Azure calls the extension actually makes.

.DESCRIPTION
    Signs in as the service principal and issues one request per permission documented in rbac.md,
    reporting PASS or FAIL for each. On failure the action name is extracted from Azure's
    authorization error, which names the exact missing permission.

    The sign-in is isolated in a temporary AZURE_CONFIG_DIR, so your own az session is left alone.

    A pass here means the permission is sufficient. To also confirm it is necessary, assign the
    service principal nothing other than the roles under test - an inherited Reader or Contributor
    from the subscription or a management group will make every probe pass regardless. Check with:

      az role assignment list --assignee <appId> --all -o table

.PARAMETER SubscriptionId
    Subscription containing the resource group and vaults under test.

.PARAMETER ResourceGroupName
    Resource group containing the vaults under test.

.PARAMETER TenantId
    Tenant of the service principal.

.PARAMETER AppId
    Application (client) ID of the service principal.

.PARAMETER ClientSecret
    Client secret for the service principal.

.PARAMETER VaultName
    An existing vault to run the certificate probes against. Omit to skip the data plane probes.

.PARAMETER IncludeCreateVault
    Also probe vault creation. Creates a real vault named kf-rbac-probe-<random> which is NOT
    cleaned up; the command to remove it is printed at the end.

.PARAMETER IncludeWriteTests
    Also probe certificate import, delete and recover against -VaultName. Generates a throwaway
    self-signed certificate locally, imports it under the alias kf-rbac-probe, then deletes and
    recovers it. Windows only, because it uses New-SelfSignedCertificate.

.EXAMPLE
    ./Test-KeyfactorAkvPermissions.ps1 -SubscriptionId <sub> -ResourceGroupName my-rg `
        -TenantId <tid> -AppId <appId> -ClientSecret <secret> -VaultName my-vault

.EXAMPLE
    ./Test-KeyfactorAkvPermissions.ps1 -SubscriptionId <sub> -ResourceGroupName my-rg `
        -TenantId <tid> -AppId <appId> -ClientSecret <secret> -VaultName my-vault `
        -IncludeCreateVault -IncludeWriteTests
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [Parameter(Mandatory)][string]$ResourceGroupName,
    [Parameter(Mandatory)][string]$TenantId,
    [Parameter(Mandatory)][string]$AppId,
    [Parameter(Mandatory)][string]$ClientSecret,

    [string]$VaultName,
    [switch]$IncludeCreateVault,
    [switch]$IncludeWriteTests,

    # Which capabilities were actually granted, matching -Capability on the provisioning script.
    # Probes outside this set are expected to fail; if one passes, the principal holds permissions
    # from somewhere else and the rest of this report cannot be trusted.
    [ValidateSet('Certificates', 'Create', 'Discovery', 'All')]
    [string[]]$Capability = @('All')
)

$ErrorActionPreference = 'Stop'

$results = New-Object System.Collections.Generic.List[object]
$probeVaultName = $null
$probeAlias = 'kf-rbac-probe'

if ($Capability -contains 'All') {
    $Capability = @('Certificates', 'Create', 'Discovery')
}

# Which capability each job belongs to, so a probe can tell whether it is expected to succeed.
$jobCapability = @{
    'Discovery' = 'Discovery'
    'Create'    = 'Create'
    'Inventory' = 'Certificates'
    'Add'       = 'Certificates'
    'Remove'    = 'Certificates'
}

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Job,
        [Parameter(Mandatory)][string]$Expected,
        [Parameter(Mandatory)][string]$Status,
        [string]$Detail = ''
    )
    $results.Add([pscustomobject]@{
            Job      = $Job
            Expected = $Expected
            Status   = $Status
            Detail   = $Detail
        })

    $colour = 'Green'
    if ($Status -eq 'FAIL') { $colour = 'Red' }
    if ($Status -eq 'EXTRA') { $colour = 'Magenta' }
    if ($Status -eq 'SKIP' -or $Status -eq 'n/a') { $colour = 'DarkGray' }
    Write-Host ("  {0,-6} {1,-14} {2}" -f $Status, $Job, $Expected) -ForegroundColor $colour
    if ($Detail) { Write-Host "         $Detail" -ForegroundColor DarkGray }
}

function Invoke-AzCapture {
    <#
        Runs az and returns stdout and stderr as one string, without letting stderr become a
        terminating error.

        PowerShell 5.1 wraps every stderr line from a native command in an ErrorRecord, so with
        $ErrorActionPreference = 'Stop' a mere warning from az - "this certificate will be moved to
        the soft deleted state", for instance - aborts the script. Dropping to 'Continue' for the
        call and flattening ErrorRecords to strings keeps the text (we need it to spot the
        authorization message) without the control flow.
    #>
    param([Parameter(Mandatory)][string[]]$Arguments)

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $raw = & az @Arguments 2>&1
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    $lines = foreach ($item in $raw) {
        if ($item -is [System.Management.Automation.ErrorRecord]) { $item.ToString() } else { $item }
    }
    return ($lines -join "`n")
}

function Test-Probe {
    <#
        Runs an az command and records whether it was authorized. Any non-authorization failure is
        reported as ERROR rather than FAIL, so a typo'd vault name is not mistaken for a missing
        permission.
    #>
    param(
        [Parameter(Mandatory)][string]$Job,
        [Parameter(Mandatory)][string]$Expected,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    Write-Verbose "az $($Arguments -join ' ')"
    $output = Invoke-AzCapture -Arguments $Arguments

    $granted = $jobCapability[$Job] -in $Capability

    if ($LASTEXITCODE -eq 0) {
        if ($granted) {
            Add-Result -Job $Job -Expected $Expected -Status 'PASS'
        }
        else {
            Add-Result -Job $Job -Expected $Expected -Status 'EXTRA' `
                -Detail "succeeded although $($jobCapability[$Job]) was not granted - the principal has permissions from elsewhere"
        }
        return $true
    }

    $deniedAction = $null
    if ($output -match "action '([^']+)'") { $deniedAction = $Matches[1] }

    if ($output -match 'AuthorizationFailed|does not have authorization|not authorized|Forbidden|ForbiddenByRbac') {
        $detail = 'denied'
        if ($deniedAction) { $detail = "denied action: $deniedAction" }
        if (-not $granted) {
            Add-Result -Job $Job -Expected $Expected -Status 'n/a' -Detail "denied as expected ($($jobCapability[$Job]) not granted)"
            return $false
        }
        Add-Result -Job $Job -Expected $Expected -Status 'FAIL' -Detail $detail
        return $false
    }

    $firstLine = ($output -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1)
    Add-Result -Job $Job -Expected $Expected -Status 'ERROR' -Detail $firstLine.Trim()
    return $false
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'The Azure CLI (az) was not found on PATH. Install it from https://aka.ms/azure-cli.'
}

# isolate the service principal sign-in so the caller's own az session is untouched
$configDir = Join-Path ([System.IO.Path]::GetTempPath()) ("kf-akv-rbac-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $configDir | Out-Null
$previousConfigDir = $env:AZURE_CONFIG_DIR
$env:AZURE_CONFIG_DIR = $configDir

try {
    Write-Host "Signing in as service principal $AppId (isolated session)" -ForegroundColor DarkGray
    Invoke-AzCapture -Arguments @('login', '--service-principal', '-u', $AppId, '-p', $ClientSecret, '--tenant', $TenantId, '--only-show-errors') | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Sign-in failed. Check the application ID, secret and tenant.'
    }
    Invoke-AzCapture -Arguments @('account', 'set', '--subscription', $SubscriptionId, '--only-show-errors') | Out-Null
    if ($LASTEXITCODE -ne 0) {
        # For a service principal this nearly always means no role assignment is visible on the
        # subscription yet, rather than a wrong id - a fresh assignment can take a few minutes, and
        # an assignment scoped below the subscription will not make it selectable here.
        Write-Host ''
        Write-Host "The service principal cannot select subscription $SubscriptionId." -ForegroundColor Red
        Write-Host 'Usual causes, in order of likelihood:' -ForegroundColor Yellow
        Write-Host '  1. The role assignment has not propagated yet - wait a few minutes and retry.' -ForegroundColor Yellow
        Write-Host '  2. The principal has no assignment at or above subscription scope.' -ForegroundColor Yellow
        Write-Host '  3. The subscription belongs to a different tenant than -TenantId.' -ForegroundColor Yellow
        Write-Host ''
        Write-Host 'Check what the principal actually holds (from your own session, not this one):' -ForegroundColor Yellow
        Write-Host "  az role assignment list --assignee $AppId --all -o table" -ForegroundColor Yellow
        throw "Service principal cannot access subscription $SubscriptionId."
    }

    Write-Host ''
    Write-Host 'Discovery job' -ForegroundColor Cyan
    Test-Probe -Job 'Discovery' -Expected 'Microsoft.Resources/subscriptions/read' `
        -Arguments @('account', 'list', '--refresh', '-o', 'none') | Out-Null
    Test-Probe -Job 'Discovery' -Expected 'Microsoft.KeyVault/vaults/read (subscription)' `
        -Arguments @('keyvault', 'list', '--subscription', $SubscriptionId, '-o', 'none') | Out-Null

    Write-Host ''
    Write-Host 'Create job' -ForegroundColor Cyan
    Test-Probe -Job 'Create' -Expected 'Microsoft.Resources/subscriptions/resourceGroups/read' `
        -Arguments @('group', 'show', '-n', $ResourceGroupName, '-o', 'none') | Out-Null
    Test-Probe -Job 'Create' -Expected 'Microsoft.Resources/subscriptions/providers/read' `
        -Arguments @('provider', 'show', '--namespace', 'Microsoft.Resources', '-o', 'none') | Out-Null
    Test-Probe -Job 'Create' -Expected 'Microsoft.KeyVault/vaults/read (resource group)' `
        -Arguments @('keyvault', 'list', '-g', $ResourceGroupName, '-o', 'none') | Out-Null

    if ($IncludeCreateVault) {
        $location = (Invoke-AzCapture -Arguments @('group', 'show', '-n', $ResourceGroupName, '--query', 'location', '-o', 'tsv')).Trim()
        if ($LASTEXITCODE -ne 0 -or -not $location) {
            Add-Result -Job 'Create' -Expected 'Microsoft.KeyVault/vaults/write' -Status 'SKIP' `
                -Detail "could not read the location of resource group '$ResourceGroupName' - resourceGroups/read probe above explains why"
        }
        else {
            $probeVaultName = 'kf-rbac-probe-' + (Get-Random -Minimum 10000 -Maximum 99999)
            Test-Probe -Job 'Create' -Expected 'Microsoft.KeyVault/vaults/write' `
                -Arguments @('keyvault', 'create', '-n', $probeVaultName, '-g', $ResourceGroupName, '-l', $location, '-o', 'none') | Out-Null
        }
    }
    else {
        Add-Result -Job 'Create' -Expected 'Microsoft.KeyVault/vaults/write' -Status 'SKIP' -Detail 'pass -IncludeCreateVault to probe this'
    }

    Write-Host ''
    Write-Host 'Inventory, Add and Remove jobs (data plane)' -ForegroundColor Cyan

    if (-not $VaultName) {
        Add-Result -Job 'Inventory' -Expected 'Microsoft.KeyVault/vaults/certificates/read' -Status 'SKIP' -Detail 'pass -VaultName to probe the data plane'
    }
    else {
        Test-Probe -Job 'Inventory' -Expected 'Microsoft.KeyVault/vaults/certificates/read' `
            -Arguments @('keyvault', 'certificate', 'list', '--vault-name', $VaultName, '-o', 'none') | Out-Null
        Test-Probe -Job 'Add' -Expected 'Microsoft.KeyVault/vaults/certificates/read (deleted)' `
            -Arguments @('keyvault', 'certificate', 'list-deleted', '--vault-name', $VaultName, '-o', 'none') | Out-Null

        if ($IncludeWriteTests) {
            if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') {
                Add-Result -Job 'Add' -Expected 'Microsoft.KeyVault/vaults/certificates/import/action' -Status 'SKIP' -Detail 'write tests need New-SelfSignedCertificate (Windows)'
            }
            else {
                $pfxPath = Join-Path $configDir 'probe.pfx'
                $pfxPassword = [guid]::NewGuid().ToString('N')
                $securePassword = ConvertTo-SecureString -String $pfxPassword -AsPlainText -Force

                $cert = New-SelfSignedCertificate -Subject "CN=$probeAlias" -CertStoreLocation 'Cert:\CurrentUser\My' `
                    -KeyExportPolicy Exportable -NotAfter (Get-Date).AddDays(2)
                try {
                    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
                }
                finally {
                    Remove-Item -Path ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -Force -ErrorAction SilentlyContinue
                }

                Test-Probe -Job 'Add' -Expected 'Microsoft.KeyVault/vaults/certificates/import/action' `
                    -Arguments @('keyvault', 'certificate', 'import', '--vault-name', $VaultName, '-n', $probeAlias, '--file', $pfxPath, '--password', $pfxPassword, '-o', 'none') | Out-Null

                Test-Probe -Job 'Remove' -Expected 'Microsoft.KeyVault/vaults/certificates/delete' `
                    -Arguments @('keyvault', 'certificate', 'delete', '--vault-name', $VaultName, '-n', $probeAlias, '-o', 'none') | Out-Null

                # the extension recovers a soft-deleted alias before re-importing it
                Test-Probe -Job 'Add' -Expected 'Microsoft.KeyVault/vaults/certificates/recover/action' `
                    -Arguments @('keyvault', 'certificate', 'recover', '--vault-name', $VaultName, '-n', $probeAlias, '-o', 'none') | Out-Null
            }
        }
        else {
            Add-Result -Job 'Add' -Expected 'certificates/import, delete, recover' -Status 'SKIP' -Detail 'pass -IncludeWriteTests to probe these'
        }
    }
}
finally {
    Invoke-AzCapture -Arguments @('logout', '--only-show-errors') | Out-Null
    $env:AZURE_CONFIG_DIR = $previousConfigDir
    if (Test-Path $configDir) { Remove-Item $configDir -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host ''
Write-Host 'Summary' -ForegroundColor Cyan
$results | Format-Table -AutoSize

$failed = @($results | Where-Object { $_.Status -eq 'FAIL' })
$errored = @($results | Where-Object { $_.Status -eq 'ERROR' })
$extra = @($results | Where-Object { $_.Status -eq 'EXTRA' })

if ($extra.Count -gt 0) {
    Write-Host ''
    Write-Host "$($extra.Count) probe(s) succeeded for capabilities that were not granted." -ForegroundColor Magenta
    Write-Host 'This principal holds permissions beyond the roles under test, so the PASS results' -ForegroundColor Magenta
    Write-Host 'above do not show that those roles are sufficient. Find the other assignments with:' -ForegroundColor Magenta
    Write-Host "  az role assignment list --assignee $AppId --all --include-inherited -o table" -ForegroundColor Magenta
    Write-Host 'Then retest with a principal that has nothing else.' -ForegroundColor Magenta
}

if ($probeVaultName) {
    Write-Host "A probe vault was created and left in place. To remove it:" -ForegroundColor Yellow
    Write-Host "  az keyvault delete -n $probeVaultName -g $ResourceGroupName" -ForegroundColor Yellow
    Write-Host "  az keyvault purge -n $probeVaultName   # only if the subscription allows purge" -ForegroundColor Yellow
}
if ($IncludeWriteTests -and $VaultName) {
    Write-Host "The probe certificate '$probeAlias' was left in $VaultName. To remove it:" -ForegroundColor Yellow
    Write-Host "  az keyvault certificate delete --vault-name $VaultName -n $probeAlias" -ForegroundColor Yellow
}

if ($errored.Count -gt 0) {
    Write-Host "$($errored.Count) probe(s) failed for reasons other than authorization - check the detail column." -ForegroundColor Yellow
}
if ($failed.Count -gt 0) {
    Write-Host "$($failed.Count) permission(s) missing." -ForegroundColor Red
    exit 1
}

Write-Host 'All probed permissions are present.' -ForegroundColor Green
