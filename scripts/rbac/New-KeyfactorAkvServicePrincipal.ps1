<#
.SYNOPSIS
    Creates a service principal for the Keyfactor Azure KeyVault orchestrator extension with the
    minimum RBAC permissions required for the job types you intend to use.

.DESCRIPTION
    Creates the custom role definitions documented in rbac.md, creates an app registration and
    service principal, and assigns each role at the scope that job type actually needs.

    Role definitions created (depending on -Capability):

      Certificates  KeyfactorManageCerts       Inventory, Add, Remove   (resource group scope)
      Create        KeyfactorVaultCreator      Create new KeyVault      (resource group scope)
      Discovery     KeyfactorVaultDiscovery    Discovery                (subscription scope)

    The permission lists here are the same ones documented in rbac.md. If you change one, change
    both.

    Requires the Azure CLI, and an account holding Owner or User Access Administrator on the
    subscription (creating role definitions and role assignments both need
    Microsoft.Authorization/*/write).

.PARAMETER SubscriptionId
    The subscription that contains, or will contain, the KeyVaults to be managed.

.PARAMETER ResourceGroupName
    The resource group that contains, or will contain, the KeyVaults to be managed. Certificate
    permissions are assigned here rather than on individual vaults so that vaults created later
    inherit them.

.PARAMETER DisplayName
    Display name for the app registration. Defaults to keyfactor-akv.

.PARAMETER Capability
    Which job types the identity should be able to perform. Any combination of Certificates,
    Create and Discovery, or All. Defaults to Certificates.

.PARAMETER RoleNameSuffix
    Appended to each custom role name. Role definition names must be unique within a tenant, so
    set this if you are creating a second set (for a sandbox, say).

.EXAMPLE
    ./New-KeyfactorAkvServicePrincipal.ps1 -SubscriptionId <sub> -ResourceGroupName my-rg

    Creates a service principal that can perform Inventory, Add and Remove against any vault in
    my-rg, and nothing else.

.EXAMPLE
    ./New-KeyfactorAkvServicePrincipal.ps1 -SubscriptionId <sub> -ResourceGroupName my-rg -Capability All

    Creates a service principal that can additionally create new vaults in my-rg and discover
    vaults across the subscription.

.NOTES
    The client secret is printed once and never written to disk. Copy it into Command's store
    configuration before closing the window; if you lose it, reset it with
    az ad app credential reset --id <appId>.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$SubscriptionId,

    [Parameter(Mandatory)]
    [string]$ResourceGroupName,

    [string]$DisplayName = 'keyfactor-akv',

    [ValidateSet('Certificates', 'Create', 'Discovery', 'All')]
    [string[]]$Capability = @('Certificates'),

    [string]$RoleNameSuffix = ''
)

$ErrorActionPreference = 'Stop'

function Invoke-Az {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$AllowFailure
    )
    Write-Verbose "az $($Arguments -join ' ')"
    $output = & az @Arguments
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "az $($Arguments -join ' ') failed with exit code $LASTEXITCODE`n$output"
    }
    return $output
}

function New-RoleDefinitionJson {
    param(
        [Parameter(Mandatory)][string]$RoleName,
        [Parameter(Mandatory)][string]$Description,
        [string[]]$Actions = @(),
        [string[]]$DataActions = @(),
        [Parameter(Mandatory)][string]$AssignableScope
    )

    $definition = [ordered]@{
        Name             = $RoleName
        Description      = $Description
        Actions          = @($Actions)
        NotActions       = @()
        DataActions      = @($DataActions)
        NotDataActions   = @()
        AssignableScopes = @($AssignableScope)
    }

    return ($definition | ConvertTo-Json -Depth 5)
}

# --- permission lists, mirroring rbac.md -------------------------------------------------------

$certificateDataActions = @(
    'Microsoft.KeyVault/vaults/certificates/read'             # Inventory, and the existence check before an Add
    'Microsoft.KeyVault/vaults/certificates/import/action'    # Add
    'Microsoft.KeyVault/vaults/certificates/delete'           # Remove
    'Microsoft.KeyVault/vaults/certificates/recover/action'   # Add, when the alias is in the soft-deleted state
)

$createVaultActions = @(
    'Microsoft.KeyVault/vaults/write'                         # create the vault
    'Microsoft.KeyVault/vaults/read'                          # read the vault back
    'Microsoft.KeyVault/locations/operationResults/read'      # poll the create long-running operation
    'Microsoft.Resources/subscriptions/resourceGroups/read'   # resolve the target resource group
    'Microsoft.Resources/subscriptions/providers/read'        # only used when no Vault Region is supplied
)

$discoveryActions = @(
    'Microsoft.Resources/subscriptions/read'                  # list the subscriptions to search
    'Microsoft.KeyVault/vaults/read'                          # list the vaults in each subscription
)

# --- preflight ---------------------------------------------------------------------------------

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'The Azure CLI (az) was not found on PATH. Install it from https://aka.ms/azure-cli and run az login.'
}

$account = Invoke-Az @('account', 'show', '-o', 'json') -AllowFailure
if ($LASTEXITCODE -ne 0) {
    throw 'Not signed in to the Azure CLI. Run az login first.'
}
$accountInfo = $account | ConvertFrom-Json
Write-Host "Signed in as $($accountInfo.user.name) (tenant $($accountInfo.tenantId))" -ForegroundColor DarkGray

# Resolve the subscription before selecting it, so that "doesn't exist in cloud" becomes an
# actionable message rather than a raw CLI failure. The usual cause is being signed in to the
# wrong tenant, not a wrong subscription id.
$visibleSubscriptions = Invoke-Az @('account', 'list', '--all', '-o', 'json') | ConvertFrom-Json
$target = $visibleSubscriptions | Where-Object { $_.id -eq $SubscriptionId }

if (-not $target) {
    Write-Host ''
    Write-Host "Subscription $SubscriptionId is not visible to $($accountInfo.user.name)." -ForegroundColor Red
    Write-Host ''
    if ($visibleSubscriptions) {
        Write-Host 'Subscriptions this account can see:' -ForegroundColor Yellow
        $visibleSubscriptions | Select-Object name, id, tenantId, state | Format-Table -AutoSize | Out-String | Write-Host
    }
    else {
        Write-Host 'This account cannot see any subscriptions.' -ForegroundColor Yellow
    }
    Write-Host 'If the subscription belongs to a different tenant, sign in to that tenant first:' -ForegroundColor Yellow
    Write-Host '  az login --tenant <tenant id>' -ForegroundColor Yellow
    throw "Subscription $SubscriptionId is not available to the signed-in account."
}

if ($target.state -ne 'Enabled') {
    Write-Host "Subscription '$($target.name)' is in state '$($target.state)'." -ForegroundColor Yellow
}

Invoke-Az @('account', 'set', '--subscription', $SubscriptionId) | Out-Null
Write-Host "Target subscription: $($target.name) ($SubscriptionId)" -ForegroundColor DarkGray

$rgId = Invoke-Az @('group', 'show', '-n', $ResourceGroupName, '--query', 'id', '-o', 'tsv') -AllowFailure
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($rgId)) {
    Write-Host ''
    Write-Host "Resource group '$ResourceGroupName' was not found in subscription '$($target.name)'." -ForegroundColor Red
    Write-Host ''
    $groups = Invoke-Az @('group', 'list', '--query', '[].name', '-o', 'tsv') -AllowFailure
    if ($LASTEXITCODE -eq 0 -and $groups) {
        Write-Host 'Resource groups in this subscription:' -ForegroundColor Yellow
        $groups -split "`n" | Where-Object { $_.Trim() } | ForEach-Object { Write-Host "  $($_.Trim())" -ForegroundColor Yellow }
    }
    throw "Resource group '$ResourceGroupName' not found."
}

$subscriptionScope = "/subscriptions/$SubscriptionId"
Write-Host "Target resource group: $rgId" -ForegroundColor DarkGray

if ($Capability -contains 'All') {
    $Capability = @('Certificates', 'Create', 'Discovery')
}

# --- role definitions --------------------------------------------------------------------------

$plannedRoles = @()

if ($Capability -contains 'Certificates') {
    $plannedRoles += [pscustomobject]@{
        Name        = "KeyfactorManageCerts$RoleNameSuffix"
        Description = 'Inventory, Add and Remove certificates in any KeyVault within the scope. No control plane access.'
        Actions     = @()
        DataActions = $certificateDataActions
        Scope       = $rgId
    }
}
if ($Capability -contains 'Create') {
    $plannedRoles += [pscustomobject]@{
        Name        = "KeyfactorVaultCreator$RoleNameSuffix"
        Description = 'Create a new KeyVault within the scope. Grants no access to certificates inside any vault.'
        Actions     = $createVaultActions
        DataActions = @()
        Scope       = $rgId
    }
}
if ($Capability -contains 'Discovery') {
    $plannedRoles += [pscustomobject]@{
        Name        = "KeyfactorVaultDiscovery$RoleNameSuffix"
        Description = 'Search for KeyVaults across a subscription.'
        Actions     = $discoveryActions
        DataActions = @()
        Scope       = $subscriptionScope
    }
}

foreach ($role in $plannedRoles) {
    $existing = Invoke-Az @('role', 'definition', 'list', '--name', $role.Name, '--query', '[0].roleName', '-o', 'tsv') -AllowFailure

    if ($existing -eq $role.Name) {
        Write-Host "Role definition '$($role.Name)' already exists, leaving it as-is." -ForegroundColor Yellow
        Write-Host "  If it predates this script, compare it against rbac.md before relying on it." -ForegroundColor Yellow
        continue
    }

    # a custom role must be assignable at or above the scope it is assigned at; subscription scope
    # covers both the resource group and subscription assignments below
    $json = New-RoleDefinitionJson -RoleName $role.Name -Description $role.Description `
        -Actions $role.Actions -DataActions $role.DataActions -AssignableScope $subscriptionScope

    if ($PSCmdlet.ShouldProcess($role.Name, 'Create custom role definition')) {
        $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) "$($role.Name).json"
        try {
            $json | Out-File -FilePath $tempFile -Encoding utf8
            Invoke-Az @('role', 'definition', 'create', '--role-definition', "@$tempFile") | Out-Null
            Write-Host "Created role definition '$($role.Name)'." -ForegroundColor Green
        }
        finally {
            if (Test-Path $tempFile) { Remove-Item $tempFile -Force }
        }
    }
}

# --- app registration and service principal ------------------------------------------------------

$appId = Invoke-Az @('ad', 'app', 'list', '--display-name', $DisplayName, '--query', '[0].appId', '-o', 'tsv') -AllowFailure

if ([string]::IsNullOrWhiteSpace($appId)) {
    if ($PSCmdlet.ShouldProcess($DisplayName, 'Create app registration and service principal')) {
        $appId = Invoke-Az @('ad', 'app', 'create', '--display-name', $DisplayName, '--query', 'appId', '-o', 'tsv')
        Invoke-Az @('ad', 'sp', 'create', '--id', $appId) | Out-Null
        Write-Host "Created app registration '$DisplayName' ($appId)." -ForegroundColor Green
    }
}
else {
    Write-Host "App registration '$DisplayName' already exists ($appId), reusing it." -ForegroundColor Yellow
}

if ([string]::IsNullOrWhiteSpace($appId)) {
    Write-Host 'No app registration was created (-WhatIf). Stopping before role assignment.' -ForegroundColor DarkGray
    return
}

# role assignments need the service principal's OBJECT id, not the application (client) id
$objectId = Invoke-Az @('ad', 'sp', 'show', '--id', $appId, '--query', 'id', '-o', 'tsv')

# --- role assignments ----------------------------------------------------------------------------

foreach ($role in $plannedRoles) {
    if ($PSCmdlet.ShouldProcess("$($role.Name) at $($role.Scope)", 'Assign role')) {
        Invoke-Az @(
            'role', 'assignment', 'create',
            '--assignee-object-id', $objectId,
            '--assignee-principal-type', 'ServicePrincipal',
            '--role', $role.Name,
            '--scope', $role.Scope
        ) | Out-Null
        Write-Host "Assigned '$($role.Name)' at $($role.Scope)." -ForegroundColor Green
    }
}

# --- credential ------------------------------------------------------------------------------------

if ($PSCmdlet.ShouldProcess($DisplayName, 'Create client secret')) {
    $secret = Invoke-Az @('ad', 'app', 'credential', 'reset', '--id', $appId, '--query', 'password', '-o', 'tsv')
    $tenantId = ($account | ConvertFrom-Json).tenantId

    Write-Host ''
    Write-Host 'Store configuration values for Command:' -ForegroundColor Cyan
    Write-Host "  Tenant ID (ClientMachine) : $tenantId"
    Write-Host "  Application (client) ID   : $appId"
    Write-Host "  Service principal objectId: $objectId"
    Write-Host "  Client secret             : $secret"
    Write-Host ''
    Write-Host 'The secret is shown once and was not written to disk. If you lose it, run:' -ForegroundColor Yellow
    Write-Host "  az ad app credential reset --id $appId" -ForegroundColor Yellow
    Write-Host ''
    Write-Host 'Role assignments can take several minutes to propagate before data plane calls succeed.' -ForegroundColor DarkGray
    Write-Host 'Verify with Test-KeyfactorAkvPermissions.ps1 before configuring the store in Command.' -ForegroundColor DarkGray
}
