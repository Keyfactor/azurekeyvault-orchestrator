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

.PARAMETER AppId
    Target an existing app registration by its application (client) id instead of looking it up by
    display name. Required when more than one app registration shares the same display name -
    Microsoft Graph does not return them in a stable order, so the script refuses to choose.

.PARAMETER SkipCredential
    Do not create a client secret. Use when the identity already has a usable credential, or when
    you intend to authenticate with a managed identity or a certificate instead.

.PARAMETER ShowSecret
    Print the client secret to the console. By default it is copied to the clipboard and not shown,
    so it does not survive in scrollback, screenshots or pasted terminal output.

.NOTES
    The client secret is never written to disk and cannot be retrieved again afterwards.

    If the app registration already exists it is reused, and the new secret is APPENDED - existing
    credentials keep working. This matters because "az ad app credential reset" without --append
    clears every password and key on the app registration, which silently breaks any other
    integration authenticating as the same identity.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$SubscriptionId,

    [Parameter(Mandatory)]
    [string]$ResourceGroupName,

    [string]$DisplayName = 'keyfactor-akv',

    # Target an existing app registration by application (client) id, bypassing the display-name
    # lookup. Required when more than one app registration shares -DisplayName.
    [string]$AppId,

    [ValidateSet('Certificates', 'Create', 'Discovery', 'All')]
    [string[]]$Capability = @('Certificates'),

    [string]$RoleNameSuffix = '',

    # Do not create a client secret at all. Use when the identity already has a usable credential,
    # or when you intend to use a managed identity or certificate credential instead.
    [switch]$SkipCredential,

    # Print the client secret to the console. Off by default: the secret is copied to the clipboard
    # instead, so it does not end up in scrollback, a screenshot, a support ticket or a chat paste.
    [switch]$ShowSecret
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

if ($AppId) {
    # An explicit app id was supplied, so no display-name lookup is needed or wanted.
    $resolvedName = Invoke-Az @('ad', 'app', 'show', '--id', $AppId, '--query', 'displayName', '-o', 'tsv') -AllowFailure
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resolvedName)) {
        throw "App registration '$AppId' was not found, or you lack directory permission to read it."
    }
    $targetAppId = $AppId.Trim()
    $appExisted = $true
    Write-Host "Using app registration '$($resolvedName.Trim())' ($targetAppId)." -ForegroundColor DarkGray
}
else {
    # -o tsv with a flat --query gives one id per line. Piping multi-line JSON through
    # ConvertFrom-Json returns the array as a single pipeline object, so @() does not flatten it
    # and .Count reads 1 no matter how many apps matched.
    $idLines = Invoke-Az @('ad', 'app', 'list', '--display-name', $DisplayName, '--query', '[].appId', '-o', 'tsv')
    $matchingAppIds = @(($idLines -join "`n") -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })

    if ($matchingAppIds.Count -gt 1) {
        # Graph does not guarantee a stable order, so picking one would be a coin toss - and the
        # loser is the registration whose credentials get touched. Make the caller choose.
        Write-Host ''
        Write-Host "$($matchingAppIds.Count) app registrations are named '$DisplayName':" -ForegroundColor Red
        Write-Host ''
        Invoke-Az @('ad', 'app', 'list', '--display-name', $DisplayName,
            '--query', '[].{displayName:displayName,appId:appId,created:createdDateTime}', '-o', 'table') | Write-Host
        Write-Host ''
        Write-Host 'This script will not choose for you. Re-run with -AppId <application id> to name' -ForegroundColor Yellow
        Write-Host 'the one to use, or -DisplayName to create a new, unambiguous registration.' -ForegroundColor Yellow
        throw "Ambiguous app registration name '$DisplayName'."
    }

    $targetAppId = $null
    if ($matchingAppIds.Count -eq 1) { $targetAppId = $matchingAppIds[0] }
    $appExisted = -not [string]::IsNullOrWhiteSpace($targetAppId)
}

if (-not $appExisted) {
    if ($PSCmdlet.ShouldProcess($DisplayName, 'Create app registration and service principal')) {
        $targetAppId = Invoke-Az @('ad', 'app', 'create', '--display-name', $DisplayName, '--query', 'appId', '-o', 'tsv')
        Invoke-Az @('ad', 'sp', 'create', '--id', $targetAppId) | Out-Null
        Write-Host "Created app registration '$DisplayName' ($targetAppId)." -ForegroundColor Green
    }
}
else {
    if (-not $AppId) {
        Write-Host "App registration '$DisplayName' already exists ($targetAppId), reusing it." -ForegroundColor Yellow
    }
    if (-not $SkipCredential) {
        Write-Host "  A new secret will be APPENDED to it. Credentials already on this app registration" -ForegroundColor Yellow
        Write-Host "  stay valid, so anything already authenticating as it keeps working." -ForegroundColor Yellow
        Write-Host "  Pass -SkipCredential if you do not need a new secret." -ForegroundColor Yellow
    }
}

if ([string]::IsNullOrWhiteSpace($targetAppId)) {
    Write-Host 'No app registration was created (-WhatIf). Stopping before role assignment.' -ForegroundColor DarkGray
    return
}

# role assignments need the service principal's OBJECT id, not the application (client) id
$objectId = Invoke-Az @('ad', 'sp', 'show', '--id', $targetAppId, '--query', 'id', '-o', 'tsv')

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

$secret = $null

if (-not $SkipCredential -and $PSCmdlet.ShouldProcess($DisplayName, 'Create client secret')) {
    # --append is deliberate. Without it, "az ad app credential reset" clears every existing
    # password and key on the app registration, which silently breaks anything already
    # authenticating as this identity. On a freshly created app there is nothing to append to, so
    # passing it unconditionally is safe.
    $secret = Invoke-Az @('ad', 'app', 'credential', 'reset', '--id', $targetAppId, '--append', '--display-name', 'keyfactor-orchestrator', '--query', 'password', '-o', 'tsv')
}

Write-Host ''
Write-Host 'Store configuration values for Command:' -ForegroundColor Cyan
Write-Host "  Tenant ID (ClientMachine) : $($accountInfo.tenantId)"
Write-Host "  Application (client) ID   : $targetAppId"
Write-Host "  Service principal objectId: $objectId"

if ($SkipCredential) {
    Write-Host '  Client secret             : not created (-SkipCredential)'
}
elseif ($secret) {
    if ($ShowSecret) {
        Write-Host "  Client secret             : $secret"
        Write-Host ''
        Write-Host 'The secret is above because you passed -ShowSecret. Treat the console scrollback as' -ForegroundColor Yellow
        Write-Host 'sensitive: do not paste it into a ticket, chat or screenshot.' -ForegroundColor Yellow
    }
    else {
        $copied = $false
        try {
            Set-Clipboard -Value $secret -ErrorAction Stop
            $copied = $true
        }
        catch {
            $copied = $false
        }

        if ($copied) {
            Write-Host '  Client secret             : copied to clipboard (not shown)'
            Write-Host ''
            Write-Host 'Paste it straight into the store configuration in Command, then clear your clipboard.' -ForegroundColor Yellow
        }
        else {
            Write-Host "  Client secret             : $secret"
            Write-Host ''
            Write-Host 'Could not reach the clipboard, so the secret is shown above. Treat the console' -ForegroundColor Yellow
            Write-Host 'scrollback as sensitive: do not paste it into a ticket, chat or screenshot.' -ForegroundColor Yellow
        }
    }

    Write-Host ''
    Write-Host 'The secret was not written to disk and cannot be retrieved again. To issue another:' -ForegroundColor Yellow
    Write-Host "  az ad app credential reset --id $targetAppId --append" -ForegroundColor Yellow
    Write-Host '  (omitting --append revokes every existing credential on this app registration)' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Role assignments can take several minutes to propagate before data plane calls succeed.' -ForegroundColor DarkGray
Write-Host 'Verify with Test-KeyfactorAkvPermissions.ps1 before configuring the store in Command.' -ForegroundColor DarkGray
